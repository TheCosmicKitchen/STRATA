using System;
using Strata.Sim;

namespace Strata.Rules;

/// <summary>
/// The hauler, driven along the surface. For now it goes anywhere: each wheel rides on the highest ground within a
/// few cells, so it rolls over outcrops and mounds and bridges narrow gaps such as the shaft. Crashes, bogging and
/// impassable terrain come later. Godot-free.
/// </summary>
public sealed class Truck
{
    public float MaxSpeed = 100, Accel = 50, Brake = 80, FuelMax = 100, Burn = .6f;
    public const int HalfLength = 11, WheelInset = 3, WheelReach = 6, BoomReach = 3;
    public float CreepSpeed = 14;

    public float X, Speed, Fuel;
    public bool Facing = true;
    public float RearY, FrontY;       // suspension height of the west (-x) and east (+x) wheels
    public float SuspensionRate = 14;
    readonly World _w;

    public Truck(World w, float x) { _w = w; X = x; Fuel = FuelMax; Settle(1, snap: true); }

    /// <summary>Height of the first solid cell from the top in one column.</summary>
    public float GroundAt(float x)
    {
        int xi = (int)x;
        for (int y = 0; y < _w.Height - 1; y++)
        {
            byte m = _w.Get(xi, y);
            if (Player.BlocksBody(m) || m == Mat.Water) return y;
        }
        return _w.Height;
    }

    /// <summary>What a wheel rests on: the highest ground within <see cref="WheelReach"/> cells.</summary>
    public float WheelGround(float x)
    {
        float best = _w.Height;
        for (int d = -WheelReach; d <= WheelReach; d++) best = MathF.Min(best, GroundAt(x + d));
        return best;
    }

    /// <summary>Wheels follow the ground through a springy suspension instead of snapping to every bump.</summary>
    void Settle(float dt, bool snap = false)
    {
        float r = WheelGround(X - HalfLength + WheelInset), f = WheelGround(X + HalfLength - WheelInset);
        float k = snap ? 1 : 1 - MathF.Exp(-SuspensionRate * dt);
        RearY += (r - RearY) * k; FrontY += (f - FrontY) * k;
    }

    /// <summary>Body tilt in radians, positive when the east end is lower.</summary>
    public float Angle => MathF.Atan2(FrontY - RearY, 2 * (HalfLength - WheelInset));

    /// <summary>Tip of the winch boom at the back of the truck, where the line hangs from.</summary>
    public (float x, float y) WinchAnchor
    {
        get
        {
            int dir = Facing ? 1 : -1;
            float bx = X - dir * (HalfLength + BoomReach);
            float wheelY = dir > 0 ? RearY : FrontY;
            return (bx, wheelY - 14);
        }
    }

    public float Y => MathF.Min(RearY, FrontY);

    /// <summary>
    /// <paramref name="throttle"/> drives in a direction (-1 west, +1 east); from a standstill the truck turns to face
    /// that way. <paramref name="creep"/> moves slowly forward (-1) or backward (+1) without turning, for parking.
    /// </summary>
    /// <returns>A message worth showing, else null.</returns>
    public string? Update(float throttle, float creep, float dt)
    {
        int dir = Facing ? 1 : -1;
        if (throttle != 0 && MathF.Sign(throttle) != dir && MathF.Abs(Speed) < 2) { Facing = !Facing; dir = -dir; }
        bool fuel = Fuel > 0;
        if (throttle != 0 && fuel)
        {
            Speed += throttle * Accel * dt;
            Fuel = MathF.Max(0, Fuel - Burn * MathF.Abs(throttle) * dt);
        }
        else if (creep != 0 && fuel)
        {
            float target = -creep * dir * CreepSpeed;
            Speed += Math.Clamp(target - Speed, -Brake * dt, Brake * dt);
            Fuel = MathF.Max(0, Fuel - Burn * .3f * dt);
        }
        else Speed -= MathF.Sign(Speed) * MathF.Min(MathF.Abs(Speed), Brake * dt);
        Speed = Math.Clamp(Speed, -MaxSpeed, MaxSpeed);

        X = Math.Clamp(X + Speed * dt, HalfLength + 2, _w.Width - HalfLength - 2);
        Settle(dt);
        return Fuel <= 0 && (throttle != 0 || creep != 0) ? "Out of fuel." : null;
    }
}
