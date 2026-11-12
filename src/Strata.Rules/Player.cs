using System;
using Strata.Sim;

namespace Strata.Rules;

public enum Part { Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg }
public enum Hurt : byte { Fine, Hurt, Useless }

/// <summary>Per-part injuries. No health bar: effects come from which part is hurt.</summary>
public sealed class Body
{
    public readonly Hurt[] Parts = new Hurt[6];
    public bool Downed;

    public Hurt this[Part p] => Parts[(int)p];
    public void Injure(Part p, Hurt h) { if (h > Parts[(int)p]) Parts[(int)p] = h; }
    public int HurtLegs => (this[Part.LeftLeg] >= Hurt.Hurt ? 1 : 0) + (this[Part.RightLeg] >= Hurt.Hurt ? 1 : 0);
    public bool LegUseless => this[Part.LeftLeg] == Hurt.Useless || this[Part.RightLeg] == Hurt.Useless;
}

/// <summary>Plain input snapshot so the player logic does not depend on Godot.</summary>
public struct PlayerInput
{
    public bool Left, Right, Up, Down, JumpPressed, JumpHeld, ClipPressed;
    /// <summary>Analogue stick, -1..1. When non-zero it is used instead of Left/Right.</summary>
    public float MoveX;
}

/// <summary>
/// The delver: a box in cell space that collides with the simulated world, hangs on a winch line, gets hurt by
/// falls, burns and bad air. Godot-free.
/// </summary>
public sealed class Player
{
    // Tunables (cells, seconds)
    public float Gravity = 90, WalkSpeed = 40, JumpSpeed = 48, WinchSpeed = 30, RopeMax = 400;
    public float SafeFall = 95, BadFall = 120, DownFall = 140, TerminalFall = 150, ClipReach = 12;
    public float GroundAccel = 320, GroundBrake = 420, AirAccel = 160, CoyoteTime = .1f, JumpBufferTime = .12f;

    /// <summary>Set for one update when a jump starts or a hard landing happens, for camera and sound feedback.</summary>
    public bool Jumped;
    /// <summary>
    /// The bubble: a shell of force around you. Hard landings bounce instead of breaking legs, walls bounce you
    /// back, flames can't reach you, and you keep your momentum in the air. Turned on and off by the game, which
    /// runs it off its own battery.
    /// </summary>
    public bool Bubble;
    /// <summary>True on the tick the bubble bounced off something hard.</summary>
    public bool Bounced;
    public const float BubbleBounce = .55f;
    public float LandingImpact;

    float _coyote, _jumpBuffer;

    // Climbing: grab rock by pushing into it in the air, climb with W/S on limited stamina, kick off with jump,
    // and pull yourself over the top of a ledge. Sand, grey lace and moss crumble and can't be gripped.
    public float StaminaMax = 100, ClimbSpeed = 16, HangDrain = 5, ClimbDrain = 12, StaminaRecover = 40;
    public float Stamina;
    public bool Clinging, Mantled, GripFailed;
    public int ClingDir;
    float _regrabDelay;

    public static bool Grippable(byte m) => MaterialTable.Grippable[m];

    /// <summary>Is there grippable rock right beside the upper body on this side?</summary>
    bool WallBeside(int dir)
    {
        int wx = (int)MathF.Floor(X) + dir * (HalfW + 1);
        int y1 = (int)MathF.Floor(Y) - 2, y0 = (int)MathF.Floor(Y) - Height;
        int n = 0;
        for (int y = y0; y <= y1; y++) if (Grippable(_w.Get(wx, y))) n++;
        return n >= 2;
    }

    bool WetNear()
    {
        for (int dy = -Height; dy <= 1; dy += 2) for (int dx = -3; dx <= 3; dx++)
            if (_w.Get((int)X + dx, (int)Y + dy) == Mat.Water) return true;
        return false;
    }

    /// <summary>If the wall beside us ends near head height, step up onto it. Returns true when we climbed over.</summary>
    bool TryMantle(int dir)
    {
        int wx = (int)MathF.Floor(X) + dir * (HalfW + 1);
        int head = (int)MathF.Floor(Y) - Height;
        // Find the top of the wall column: the first solid cell from above, within reach of the hands.
        for (int top = head - 2; top <= (int)MathF.Floor(Y) - 2; top++)
        {
            if (!BlocksBody(_w.Get(wx, top))) continue;
            float nx = MathF.Floor(X) + dir * (HalfW + 2) + .5f, ny = top;
            if (Blocked(nx, ny) || BlocksBody(_w.Get(wx, top - 1))) return false;
            X = nx; Y = ny; VX = VY = 0; Grounded = true; Clinging = false; Mantled = true;
            return true;
        }
        return false;
    }

    static float MoveTowards(float v, float target, float maxDelta) =>
        MathF.Abs(target - v) <= maxDelta ? target : v + MathF.Sign(target - v) * maxDelta;
    public float AirMax = 150, BatteryMax = 180;
    /// <summary>Off for now while we tune caving: no air drain and no passing out. Gas still hurts.</summary>
    public bool AirEnabled = false;
    /// <summary>While we tune caving: being downed wears off, and lava burns before it kills.</summary>
    public bool Forgiving = true;
    float _lavaTime, _downTime, _stepCooldown;

    public const int HalfW = 1, Height = 7, MaxStep = 4;

    public float X, Y, VX, VY;       // Y is the feet
    public bool Grounded, Clipped, LampOn = true, Facing = true;
    public float RopeLen = 8, Air, Battery, LastLanding;
    public bool CarryingSample, InGas;
    /// <summary>What is at head height (the air being breathed).</summary>
    public byte Breathing;
    int _rngTick;
    public readonly Body Body = new();
    public string Message = "";

    readonly World _w;

    /// <summary>Where the line comes from: the winch boom. It can move (the winch is on the truck).</summary>
    public float AnchorX, AnchorY;

    public Player(World w, float anchorX, float anchorY)
    {
        _w = w; AnchorX = anchorX; AnchorY = anchorY;
        X = anchorX; Y = anchorY + RopeLen + Height;
        Air = AirMax; Battery = BatteryMax; Stamina = StaminaMax;
    }

    public (float x, float y) HookEnd => Clipped ? (X, Y - Height) : (AnchorX, AnchorY + RopeLen);

    /// <summary>How far the chest is from the hanging line (anchor straight down to the hook).</summary>
    public float DistanceToLine()
    {
        float cx = X, cy = Y - Height + 2;
        float t = Math.Clamp(cy - AnchorY, 0, RopeLen);
        float dx = cx - AnchorX, dy = cy - (AnchorY + t);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Solids, and powders heavy enough to stand on (ash, coal dust and embers you sink through).</summary>
    public static bool BlocksBody(byte m) => MaterialTable.Solid[m] || (MaterialTable.Powder[m] && MaterialTable.Density[m] >= 15);

    bool Blocked(float px, float py)
    {
        int x0 = (int)MathF.Floor(px) - HalfW, x1 = (int)MathF.Floor(px) + HalfW;
        int y1 = (int)MathF.Floor(py) - 1, y0 = y1 - Height + 1;
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            if (BlocksBody(_w.Get(x, y))) return true;
        return false;
    }

    byte CellAtHead() => _w.Get((int)MathF.Floor(X), (int)MathF.Floor(Y) - Height + 1);
    byte CellAtFeet() => _w.Get((int)MathF.Floor(X), (int)MathF.Floor(Y) - 1);

    public void Update(in PlayerInput inp, float dt)
    {
        Jumped = false; LandingImpact = 0; Mantled = false; GripFailed = false; Bounced = false;
        if (Bubble) Clinging = false;

        var b = Body;
        byte head = CellAtHead(), feet = CellAtFeet();
        bool inLiquid = feet is Mat.Water or Mat.Acid || head is Mat.Water;

        // Winch: W/S while clipped. Works even when downed, so a downed delver can be reeled out.
        if (Clipped && !Clinging)
        {
            if (inp.Up) RopeLen = MathF.Max(2, RopeLen - WinchSpeed * dt);
            if (inp.Down) RopeLen = MathF.Min(RopeMax, RopeLen + WinchSpeed * dt);
        }

        if (inp.ClipPressed && !b.Downed)
        {
            if (Clipped) { Clipped = false; Message = "Unclipped."; }
            else
            {
                if (DistanceToLine() < ClipReach)
                {
                    Clipped = true;
                    float dx = X - AnchorX, dy = (Y - Height) - AnchorY;
                    RopeLen = Math.Clamp(MathF.Sqrt(dx * dx + dy * dy), 2, RopeMax);
                    Message = "Clipped on.";
                }
                else Message = "The line is out of reach.";
            }
        }

        // Walking: quick acceleration and braking rather than instant speed, so it feels weighty but responsive.
        float speed = WalkSpeed * (b.HurtLegs == 0 ? 1 : b.HurtLegs == 1 ? .5f : .25f) * (inLiquid ? .6f : 1);
        float want = b.Downed ? 0 : inp.MoveX != 0 ? Math.Clamp(inp.MoveX, -1, 1) : (inp.Right ? 1 : 0) - (inp.Left ? 1 : 0);
        if (want != 0) Facing = want > 0;
        float target = want * speed;
        if (Clinging) { }
        else if (Clipped && !Grounded) VX += want * 40 * dt;      // swing on the line
        else if (Bubble && !Grounded && !inLiquid)
        {
            // In the bubble you keep your momentum: you can steer, but nothing brakes you in the air.
            float nv = VX + want * AirAccel * .6f * dt;
            if (MathF.Abs(nv) <= MathF.Max(MathF.Abs(VX), speed)) VX = nv;
        }
        else
        {
            bool turning = target != 0 && MathF.Sign(target) != MathF.Sign(VX) && VX != 0;
            float rate = Grounded || inLiquid ? (target == 0 || turning ? GroundBrake : GroundAccel) : AirAccel;
            VX = MoveTowards(VX, target, rate * dt);
        }

        // Jumping: a short grace period after leaving a ledge (coyote time), a buffered press just before landing,
        // and a lower jump when the button is let go early.
        _coyote = Grounded ? CoyoteTime : MathF.Max(0, _coyote - dt);
        _jumpBuffer = inp.JumpPressed ? JumpBufferTime : MathF.Max(0, _jumpBuffer - dt);
        bool canJump = (_coyote > 0 || Grounded) && !b.Downed && b.HurtLegs == 0 && !(Clipped && !Grounded) && !Clinging;
        if (_jumpBuffer > 0 && canJump) { VY = -JumpSpeed; _jumpBuffer = 0; _coyote = 0; Grounded = false; Jumped = true; }
        if (inLiquid && inp.Up && !Clipped && !b.Downed) VY = MathF.Max(VY - 60 * dt, -10);

        // Climbing.
        _regrabDelay = MathF.Max(0, _regrabDelay - dt);
        bool armsOk = !(b[Part.LeftArm] == Hurt.Useless && b[Part.RightArm] == Hurt.Useless) && !b.Downed;
        int push = want > .3f ? 1 : want < -.3f ? -1 : 0;
        if (!Bubble && !Clinging && !Grounded && !inLiquid && armsOk && push != 0 && Stamina > 5 && _regrabDelay <= 0 && VY > -25 && WallBeside(push))
        { Clinging = true; ClingDir = push; }
        if (Clinging)
        {
            float armFactor = (b[Part.LeftArm] >= Hurt.Hurt ? .7f : 1) * (b[Part.RightArm] >= Hurt.Hurt ? .7f : 1);
            if (Grounded || !armsOk || inLiquid) Clinging = false;
            else if (inp.JumpPressed)
            {
                // Kick off the wall: away and up.
                Clinging = false; _regrabDelay = .25f;
                VX = -ClingDir * 30; VY = -40; Stamina = MathF.Max(0, Stamina - 12); Jumped = true; _jumpBuffer = 0;
            }
            else if (push == -ClingDir) { Clinging = false; _regrabDelay = .2f; }
            else if (!WallBeside(ClingDir)) { if (!TryMantle(ClingDir)) Clinging = false; }
            else
            {
                float gripDrain = (inp.Up || inp.Down ? ClimbDrain : HangDrain) * (WetNear() ? 2 : 1) / armFactor;
                Stamina -= gripDrain * dt;
                if (Stamina <= 0) { Stamina = 0; Clinging = false; _regrabDelay = .6f; GripFailed = true; Message = "Your grip gives out."; }
                else
                {
                    VX = 0;
                    VY = inp.Up ? -ClimbSpeed * armFactor : inp.Down ? ClimbSpeed * 1.4f : 0;
                    if (inp.Up && TryMantle(ClingDir)) { }
                }
            }
        }
        if (Grounded) Stamina = MathF.Min(StaminaMax, Stamina + StaminaRecover * dt);
        else if (Clipped && !Clinging) Stamina = MathF.Min(StaminaMax, Stamina + 8 * dt);   // resting on the line

        float g = Clinging ? 0 : Gravity;
        if (!inLiquid && !Clipped && !Clinging)
        {
            if (VY < 0 && !inp.JumpHeld) g *= 2.6f;          // short hop
            else if (VY > 0) g *= 1.35f;                       // snappier fall
        }
        VY += g * (inLiquid ? .3f : 1) * dt;
        if (inLiquid) { VX *= .96f; VY *= .96f; }
        VY = MathF.Min(VY, TerminalFall);

        float preVY = VY;
        _stepCooldown = MathF.Max(0, _stepCooldown - dt);
        MoveAxis(VX * dt, 0);
        MoveAxis(0, VY * dt);

        // Rope constraint: hook point (top of the head) stays within RopeLen of the anchor.
        if (Clipped)
        {
            float hx = X - AnchorX, hy = (Y - Height) - AnchorY, d = MathF.Sqrt(hx * hx + hy * hy);
            if (d > RopeLen && d > 0)
            {
                float nx = hx / d, ny = hy / d, k = RopeLen / d;
                float tx = AnchorX + hx * k, ty = AnchorY + hy * k + Height;
                if (!Blocked(tx, ty)) { X = tx; Y = ty; }
                else if (!Blocked(X, ty)) Y = ty;
                float radial = VX * nx + VY * ny;
                if (radial > 0) { VX -= radial * nx; VY -= radial * ny; }
            }
        }

        // Landing in the bubble: a hard landing bounces, and nothing breaks.
        if (Bubble && Grounded && preVY > 0 && VY == 0 && preVY > 20)
        {
            LastLanding = preVY; LandingImpact = preVY;
            if (preVY > 45) { VY = -preVY * BubbleBounce; Grounded = false; Bounced = true; }
        }
        // Landing injuries.
        else if (Grounded && preVY > 0 && VY == 0 && preVY > 20)
        {
            LastLanding = preVY; LandingImpact = preVY;
            if (preVY > DownFall) { b.Injure(Part.LeftLeg, Hurt.Useless); b.Injure(Part.RightLeg, Hurt.Useless); b.Injure(Part.Torso, Hurt.Hurt); b.Downed = true; Message = "You hit hard and can't get up."; }
            else if (preVY > BadFall) { b.Injure(Part.LeftLeg, Hurt.Useless); b.Injure(Part.RightLeg, Hurt.Hurt); Message = "Something in your leg gave."; }
            else if (preVY > SafeFall) { b.Injure((preVY * 7) % 2 < 1 ? Part.LeftLeg : Part.RightLeg, Hurt.Hurt); Message = "Bad landing."; }
        }

        // Air, gas, burns.
        float breathe = MaterialTable.Liquid[head] ? 0 : MaterialTable.Suffocation[head];
        InGas = breathe > 0;
        Breathing = head;
        bool buried = BlocksBody(head);
        float drain = (InGas ? breathe : 1) * (b[Part.Torso] >= Hurt.Hurt ? 1.5f : 1) * (buried ? 10 : 1);
        // The worst air also hurts: foul air and mercury vapour damage the lungs, steam scalds.
        if (head is Mat.FoulGas or Mat.MercuryVapour && _rngTick++ % 120 == 0) { b.Injure(Part.Torso, Hurt.Hurt); Message = head == Mat.FoulGas ? "The air stinks of rotten eggs. Your chest burns." : "A metallic taste. You feel sick."; }
        if (head == Mat.Steam) { b.Injure(Part.Head, Hurt.Hurt); Message = "Scalding steam."; }
        if (MaterialTable.Liquid[head]) drain = MaterialTable.Suffocation[head] > 0 ? MaterialTable.Suffocation[head] : 8;
        if (AirEnabled)
        {
            Air -= drain * dt;
            if (Air <= 0) { Air = 0; if (!b.Downed) { b.Downed = true; Message = "Out of air."; } }
        }
        else Air = AirMax;
        bool inLava = false;
        for (int yy = (int)Y - Height; yy < (int)Y; yy++)
        {
            byte c = _w.Get((int)X, yy);
            if (c == Mat.Fire && !Bubble) { b.Injure(yy < Y - 4 ? Part.Torso : Part.LeftLeg, Hurt.Hurt); Message = "Burned."; }
            if (c == Mat.Lava) inLava = true;
            if (c == Mat.Acid) { b.Injure(Part.RightLeg, Hurt.Hurt); Message = "Acid through the boots."; }
        }

        // Lava: your boots and legs burn at once; stay in it and you go down.
        if (inLava)
        {
            _lavaTime += dt;
            b.Injure(Part.LeftLeg, Hurt.Hurt); b.Injure(Part.RightLeg, Hurt.Hurt);
            if (_lavaTime > (Forgiving ? 1f : 0)) { b.Injure(Part.LeftLeg, Hurt.Useless); b.Injure(Part.RightLeg, Hurt.Useless); b.Downed = true; Message = "Lava."; }
            else Message = "Your boots are burning. Get out of the lava.";
        }
        else _lavaTime = 0;

        // Getting back up: in forgiving mode a downed delver drags themself up after a few seconds out of danger.
        if (b.Downed && Forgiving)
        {
            bool danger = inLava || InGas && breathe >= 10;
            _downTime = danger ? 0 : _downTime + dt;
            if (_downTime > 4)
            {
                b.Downed = false; _downTime = 0;
                for (int p = 0; p < b.Parts.Length; p++) if (b.Parts[p] == Hurt.Useless) b.Parts[p] = Hurt.Hurt;
                Message = "You drag yourself back up.";
            }
        }

        if (LampOn) { Battery -= dt; if (Battery <= 0) { Battery = 0; LampOn = false; Message = "The lamp died."; } }

        // Fresh air above ground refills the suit.
        if (Y < CaveGen.Surface + 2 && !InGas && !b.Downed) Air = MathF.Min(AirMax, Air + 25 * dt);
    }

    void MoveAxis(float dx, float dy)
    {
        float steps = MathF.Ceiling(MathF.Max(MathF.Abs(dx), MathF.Abs(dy)) / .4f);
        if (steps < 1) { if (dy > 0) Grounded = Blocked(X, Y + .05f); return; }
        float sx = dx / steps, sy = dy / steps;
        if (dy != 0) Grounded = false;
        for (int i = 0; i < steps; i++)
        {
            if (!Blocked(X + sx, Y + sy)) { X += sx; Y += sy; continue; }
            if (sx != 0)
            {
                // Step up ledges up to MaxStep cells when walking, so rough ground doesn't stop you. Tall steps have a
                // short cooldown, so a jagged wall can't be run up like a staircase.
                bool stepped = false;
                int maxH = _stepCooldown > 0 ? 1 : MaxStep;
                for (int h = 1; h <= maxH && Grounded; h++)
                    if (!Blocked(X + sx, Y + sy - h - .01f)) { X += sx; Y -= h; stepped = true; if (h > 1) _stepCooldown = .15f; break; }
                if (stepped) continue;
                if (Bubble && MathF.Abs(VX) > 30) { VX = -VX * BubbleBounce; Bounced = true; }
                else VX = 0;
            }
            if (sy != 0)
            {
                if (sy > 0) Grounded = true;
                if (sy < 0 && Bubble && VY < -30) { VY = -VY * BubbleBounce; Bounced = true; }
                else VY = 0;
            }
            break;
        }
        if (dy == 0 && dx != 0 && Grounded && !Blocked(X, Y + .05f) && Blocked(X, Y + 1.05f)) Y += 1;   // step down
    }

    /// <summary>
    /// Called after the sim moved: if something landed in us, step to the nearest free spot close by (preferring up).
    /// Never moves into or through rock: if there's no free spot, loose stuff in the body is shoved aside, and solid
    /// rock stays put (dig out).
    /// </summary>
    public void Unstick()
    {
        if (!Blocked(X, Y)) return;
        float bestX = 0, bestY = 0, best = float.MaxValue;
        for (int dy = -4; dy <= 1; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                float cost = MathF.Abs(dx) * 1.2f + (dy < 0 ? -dy : dy * 2);
                if (cost >= best || Blocked(X + dx, Y + dy) || !PathClear(dx, dy)) continue;
                best = cost; bestX = dx; bestY = dy;
            }
        if (best < float.MaxValue) { X += bestX; Y += bestY; return; }
        // Buried: shove loose material (powder, liquid, gas) out of the body; rock stays.
        int x0 = (int)MathF.Floor(X) - HalfW, y1 = (int)MathF.Floor(Y) - 1;
        for (int y = y1 - Height + 1; y <= y1; y++)
            for (int x = x0; x <= x0 + 2 * HalfW; x++)
            {
                byte m = _w.Get(x, y);
                if (BlocksBody(m) && !MaterialTable.Solid[m]) _w.Set(x, y, Mat.Empty);
            }
    }

    /// <summary>Whether a small unstick move only passes through cells the body could move through, one cell at a time.</summary>
    bool PathClear(int dx, int dy)
    {
        int n = Math.Max(Math.Abs(dx), Math.Abs(dy));
        int solidHits = 0;
        for (int i = 1; i < n; i++)
            if (Blocked(X + (float)dx * i / n, Y + (float)dy * i / n)) solidHits++;
        return solidHits <= 1;
    }
}
