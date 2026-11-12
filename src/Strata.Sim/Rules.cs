using System.Runtime.CompilerServices;

namespace Strata.Sim;

/// <summary>Something the game may want to react to (shake the camera, hurt the player, play a sound).</summary>
public enum SimEventKind : byte { Explosion, Collapse, Ignition }
public readonly record struct SimEvent(SimEventKind Kind, int X, int Y, float Power);

/// <summary>
/// Material behaviour. Movement is generic by phase (powder, liquid, gas) using the material table; reactions come
/// from <see cref="Reactions"/>; fire spreads by flammability. A few materials have behaviour of their own: fire,
/// embers, explosions, crumbling shale, growth (grey lace, moss, roots, fungus), spores and cooling lava.
/// World edges are walls.
/// </summary>
public sealed partial class World
{
    static readonly int[] NX = [-1, 1, 0, 0];
    static readonly int[] NY = [0, 0, -1, 1];

    /// <summary>Events raised during the last <see cref="Step"/>. Capped so a chain reaction can't flood it.</summary>
    public readonly List<SimEvent> Events = [];
    const int MaxEvents = 64;

    void Emit(SimEventKind k, int x, int y, float power) { if (Events.Count < MaxEvents) Events.Add(new(k, x, y, power)); }

    void Update(ref Rng rng, int x, int y, int i, byte m)
    {
        // Special behaviour first; it can turn the cell into something else.
        switch (m)
        {
            case Mat.Fire: if (!FireTick(ref rng, x, y, i)) return; break;
            case Mat.Ember: if (!EmberTick(ref rng, x, y, i)) return; break;
            case Mat.Blast: BlastTick(ref rng, x, y); return;
            case Mat.Lava: if (!LavaTick(ref rng, x, y, i)) return; break;
            case Mat.Shale: ShaleTick(ref rng, x, y, i); return;
            case Mat.Mycelium: MyceliumGrow(ref rng, x, y, i); break;
            case Mat.Moss: MossGrow(ref rng, x, y); break;
            case Mat.Root: RootGrow(ref rng, x, y, i); break;
            case Mat.Fungus: FungusTick(ref rng, x, y); break;
            case Mat.Spore: if (!SporeLife(ref rng, x, y, i)) return; break;
            case Mat.Steam:
                if (y > 0 && Get(x, y - 1) == Mat.Rock && rng.Chance(.01f)) { SetCell(ref rng, x, y, Mat.Water, 0); return; }
                break;
            case Mat.Coldflame: if (!ColdflameTick(ref rng, x, y, i)) return; break;
            case Mat.Sunstone: SpreadHeat(ref rng, x, y, .15f); break;
            case Mat.Void: VoidTick(ref rng, x, y, i); return;
            case Mat.Ghostwater: if (Seep(ref rng, x, y)) return; break;
            case Mat.Skydust: if (React(ref rng, x, y, m)) return; FallUp(ref rng, x, y, m); return;
            case Mat.Glimmer: if (rng.Chance(.25f) && --_life[i] <= 1) { SetCell(ref rng, x, y, Mat.Empty, 0); return; } Drift(ref rng, x, y); return;
        }

        // Life running out.
        float decay = MaterialTable.DecayChance[m];
        if (decay > 0 && rng.Chance(decay) && --_life[i] <= 1)
        {
            byte into = MaterialTable.DecayInto[m];
            if (m == Mat.Steam && !rng.Chance(.35f)) into = Mat.Empty;
            SetCell(ref rng, x, y, into, MaterialTable.DefaultLife[into]);
            return;
        }

        // Reactions with a neighbour (one random neighbour per tick keeps the cost flat).
        if (React(ref rng, x, y, m)) return;

        // Movement by phase.
        switch (MaterialTable.PhaseOf[m])
        {
            case Phase.Powder: Fall(ref rng, x, y, m); break;
            case Phase.Liquid: Liquid(ref rng, x, y, m); break;
            case Phase.Gas:
                if (m == Mat.Spore) Drift(ref rng, x, y);
                else if (MaterialTable.Density[m] < 1) Rise(ref rng, x, y, m);
                else SinkGas(ref rng, x, y, m);
                break;
        }
    }

    // ---------- Queries ----------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool Lighter(int x, int y, float d)
    {
        if (!InBounds(x, y)) return false;
        byte u = _mat[y * Width + x];
        return !MaterialTable.Solid[u] && MaterialTable.Density[u] < d;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool Heavier(int x, int y, float d)
    {
        if (!InBounds(x, y)) return false;
        byte u = _mat[y * Width + x];
        return !MaterialTable.Solid[u] && !MaterialTable.Powder[u] && u != Mat.Lava && MaterialTable.Density[u] > d;
    }

    bool Clings(int x, int y, int fromX, int fromY)
    {
        for (int k = 0; k < 4; k++)
        {
            int nx = x + NX[k], ny = y + NY[k];
            if ((nx == fromX && ny == fromY) || !InBounds(nx, ny)) continue;
            byte u = _mat[ny * Width + nx];
            if (MaterialTable.Grippable[u] || u == Mat.Sand || u == Mat.Dirt || u == Mat.Clay) return true;
        }
        return false;
    }

    bool Near(int x, int y, byte m, int r)
    {
        for (int dy = -r; dy <= r; dy++)
        for (int dx = -r; dx <= r; dx++)
            if (InBounds(x + dx, y + dy) && _mat[(y + dy) * Width + x + dx] == m) return true;
        return false;
    }

    bool WetNear(int x, int y) => Near(x, y, Mat.Water, 2);

    // ---------- Reactions ----------

    bool React(ref Rng rng, int x, int y, byte m, float scale = 1)
    {
        int k = rng.Int(4), nx = x + NX[k], ny = y + NY[k];
        if (!InBounds(nx, ny)) return false;
        byte u = _mat[ny * Width + nx];
        var rules = Reactions.For(m, u);
        for (int r = 0; r < rules.Length; r++)
        {
            ref readonly var rule = ref rules[r];
            if (!rng.Chance(rule.Chance * scale)) continue;
            if (rule.BInto != Reaction.Keep) SetCell(ref rng, nx, ny, rule.BInto, MaterialTable.DefaultLife[rule.BInto]);
            if (rule.AInto != Reaction.Keep) { SetCell(ref rng, x, y, rule.AInto, MaterialTable.DefaultLife[rule.AInto]); return true; }
            return false;
        }
        return false;
    }

    /// <summary>Sets a flammable cell alight according to its material: fire, embers or an explosion.</summary>
    void Ignite(ref Rng rng, int x, int y, byte m)
    {
        byte into = MaterialTable.BurnsInto[m];
        byte life = (byte)Math.Min(250, MaterialTable.FireLife[m] + rng.Int(Math.Max(1, MaterialTable.FireLife[m] / 2)));
        if (into == Mat.Blast) life = 2;
        if (into == Mat.Ash || into == Mat.Empty) life = MaterialTable.DefaultLife[into];
        SetCell(ref rng, x, y, into, life);
        byte by = MaterialTable.BurnByproduct[m];
        if (by != 0)
        {
            int n = by == Mat.Spore ? 4 : 1;
            for (int k = 0; k < 4 && n > 0; k++)
            {
                int sx = x + NX[k], sy = y + NY[k];
                if (InBounds(sx, sy) && _mat[sy * Width + sx] == Mat.Empty && rng.Chance(.7f)) { SetCell(ref rng, sx, sy, by, MaterialTable.DefaultLife[by]); n--; }
            }
        }
    }

    /// <summary>Tries to light each neighbour by its flammability, scaled by how hot the source is.</summary>
    void SpreadHeat(ref Rng rng, int x, int y, float scale)
    {
        for (int k = 0; k < 4; k++)
        {
            int nx = x + NX[k], ny = y + NY[k];
            if (!InBounds(nx, ny)) continue;
            byte u = _mat[ny * Width + nx];
            float f = MaterialTable.Flammability[u];
            if (f > 0 && rng.Chance(f * scale)) Ignite(ref rng, nx, ny, u);
        }
    }

    // ---------- Movement ----------

    /// <summary>Could a liquid or powder of this density move from here (down, diagonally, or sideways for liquids)?</summary>
    bool CanFlow(int x, int y, float d, bool sideways) =>
        Lighter(x, y + 1, d) || Lighter(x - 1, y + 1, d) || Lighter(x + 1, y + 1, d) || (sideways && (Lighter(x - 1, y, d) || Lighter(x + 1, y, d)));

    void Fall(ref Rng rng, int x, int y, byte m)
    {
        float d = MaterialTable.Density[m];
        if (MaterialTable.Viscosity[m] > 0 && rng.Chance(MaterialTable.Viscosity[m])) { if (CanFlow(x, y, d, false)) MarkNext(x, y); return; }
        if (Lighter(x, y + 1, d)) { Swap(x, y, x, y + 1); return; }
        if (!rng.Chance(MaterialTable.Slide[m])) return;
        int dir = rng.Sign();
        if (Lighter(x + dir, y + 1, d) && Lighter(x + dir, y, d)) { Swap(x, y, x + dir, y + 1); return; }
        if (Lighter(x - dir, y + 1, d) && Lighter(x - dir, y, d)) Swap(x, y, x - dir, y + 1);
    }

    void Liquid(ref Rng rng, int x, int y, byte m)
    {
        float d = MaterialTable.Density[m];
        if (MaterialTable.Viscosity[m] > 0 && rng.Chance(MaterialTable.Viscosity[m])) { if (CanFlow(x, y, d, true)) MarkNext(x, y); return; }
        int disperse = MaterialTable.Dispersion[m];
        if (Lighter(x, y + 1, d)) { Swap(x, y, x, y + 1); return; }
        int dir = rng.Sign();
        if (Lighter(x + dir, y + 1, d)) { Swap(x, y, x + dir, y + 1); return; }
        if (Lighter(x - dir, y + 1, d)) { Swap(x, y, x - dir, y + 1); return; }
        for (int pass = 0; pass < 2; pass++)
        {
            int s = pass == 0 ? dir : -dir, best = int.MinValue;
            for (int k = 1; k <= disperse; k++)
            {
                int nx = x + s * k;
                if (!Lighter(nx, y, d)) break;
                best = nx;
                if (Lighter(nx, y + 1, d)) break;
            }
            if (best != int.MinValue) { Swap(x, y, best, y); return; }
        }
    }

    void Rise(ref Rng rng, int x, int y, byte m)
    {
        float d0 = MaterialTable.Density[m];
        // Gas with nowhere to go sleeps; gas that could move but didn't this tick asks to be visited again.
        if (!rng.Chance(MaterialTable.Rise[m]))
        {
            if (y == 0 || Heavier(x, y - 1, d0) || Heavier(x - 1, y - 1, d0) || Heavier(x + 1, y - 1, d0)) MarkNext(x, y);
            return;
        }
        if (y == 0) { if (rng.Chance(.25f)) SetCell(ref rng, x, y, Mat.Empty, 0); return; }
        float d = MaterialTable.Density[m];
        int dir = rng.Sign();
        // Sideways only towards somewhere it can keep rising, or as a rare drift; otherwise a pocket under a ceiling
        // would random-walk forever and never settle.
        bool sideUp = Heavier(x + dir, y, d) && Heavier(x + dir, y - 1, d);
        if (rng.Chance(.6f) && sideUp) { Swap(x, y, x + dir, y); return; }
        if (Heavier(x, y - 1, d)) { Swap(x, y, x, y - 1); return; }
        if (Heavier(x + dir, y - 1, d)) { Swap(x, y, x + dir, y - 1); return; }
        if (Heavier(x - dir, y - 1, d)) { Swap(x, y, x - dir, y - 1); return; }
        if (sideUp || (rng.Chance(.03f) && Heavier(x + dir, y, d))) Swap(x, y, x + dir, y);
    }

    /// <summary>Heavier-than-air gas: sinks, then creeps sideways along the floor and pools.</summary>
    void SinkGas(ref Rng rng, int x, int y, byte m)
    {
        float d = MaterialTable.Density[m];
        if (!rng.Chance(MaterialTable.Rise[m]))
        {
            bool Open(int ax, int ay) => Lighter(ax, ay, d) && !MaterialTable.Liquid[Get(ax, ay)];
            if (Open(x, y + 1) || Open(x - 1, y + 1) || Open(x + 1, y + 1)) MarkNext(x, y);
            return;
        }
        int dir = rng.Sign();
        if (Lighter(x, y + 1, d) && !MaterialTable.Liquid[Get(x, y + 1)]) { Swap(x, y, x, y + 1); return; }
        if (Lighter(x + dir, y + 1, d) && !MaterialTable.Liquid[Get(x + dir, y + 1)]) { Swap(x, y, x + dir, y + 1); return; }
        // Creep sideways only downhill, or now and then: heavy gas pools and then lies still.
        bool sideOk = Lighter(x + dir, y, d) && !MaterialTable.Liquid[Get(x + dir, y)];
        if (sideOk && (Lighter(x + dir, y + 1, d) || rng.Chance(.03f))) Swap(x, y, x + dir, y);
    }

    void Drift(ref Rng rng, int x, int y)
    {
        if (!rng.Chance(.35f)) return;
        int dx = rng.Int(3) - 1;
        int dy = rng.Chance(.55f) ? -1 : (rng.Chance(.5f) ? 0 : 1);
        if (InBounds(x + dx, y + dy) && _mat[(y + dy) * Width + x + dx] == Mat.Empty) Swap(x, y, x + dx, y + dy);
    }

    // ---------- Strange matter ----------

    /// <summary>Skydust: a powder that falls upward and piles on ceilings.</summary>
    void FallUp(ref Rng rng, int x, int y, byte m)
    {
        float d = MaterialTable.Density[m];
        bool Open(int ax, int ay) => InBounds(ax, ay) && !MaterialTable.Solid[_mat[ay * Width + ax]] && !MaterialTable.Powder[_mat[ay * Width + ax]] && MaterialTable.Density[_mat[ay * Width + ax]] < d;
        if (Open(x, y - 1)) { Swap(x, y, x, y - 1); return; }
        if (!rng.Chance(MaterialTable.Slide[m])) { if (Open(x - 1, y - 1) || Open(x + 1, y - 1)) MarkNext(x, y); return; }
        int dir = rng.Sign();
        if (Open(x + dir, y - 1) && Open(x + dir, y)) { Swap(x, y, x + dir, y - 1); return; }
        if (Open(x - dir, y - 1) && Open(x - dir, y)) Swap(x, y, x - dir, y - 1);
    }

    /// <summary>Cold fire: rises and fades like fire, spreads through methane and hydrogen, freezes what it touches.</summary>
    bool ColdflameTick(ref Rng rng, int x, int y, int i)
    {
        byte life = --_life[i];
        if (life == 0 || life > 250) { SetCell(ref rng, x, y, rng.Chance(.3f) ? Mat.Snow : Mat.Empty, 0); return false; }
        for (int k = 0; k < 4; k++)
        {
            int nx = x + NX[k], ny = y + NY[k];
            if (!InBounds(nx, ny)) continue;
            byte u = _mat[ny * Width + nx];
            if ((u == Mat.Methane || u == Mat.Hydrogen) && rng.Chance(.8f)) SetCell(ref rng, nx, ny, Mat.Coldflame, (byte)(30 + rng.Int(30)));
        }
        return true;
    }

    /// <summary>Ghostwater sitting on rock passes straight through it to the next open space below.</summary>
    bool Seep(ref Rng rng, int x, int y)
    {
        byte below = Get(x, y + 1);
        if (!MaterialTable.Solid[below] || below is Mat.Bedrock or Mat.Crystal or Mat.Void || !rng.Chance(.04f)) return false;
        for (int d = 2; d <= 10; d++)
        {
            byte c = Get(x, y + d);
            if (c is Mat.Bedrock or Mat.Crystal) return false;
            if (c == Mat.Empty || MaterialTable.Gas[c])
            {
                SetCell(ref rng, x, y + d, Mat.Ghostwater, 0);
                SetCell(ref rng, x, y, Mat.Empty, 0);
                return true;
            }
            if (!MaterialTable.Solid[c]) return false;
        }
        return false;
    }

    /// <summary>
    /// A void burrows: now and then it eats a neighbour and moves into the space, sometimes leaving a weaker void
    /// behind, until its strength (life) is spent. Hanging in open air, it sinks. Crystal, bedrock and obsidian hold it.
    /// </summary>
    void VoidTick(ref Rng rng, int x, int y, int i)
    {
        if (React(ref rng, x, y, Mat.Void)) return;
        if (!rng.Chance(.06f)) return;
        byte life = _life[i];
        int k = rng.Int(4), nx = x + NX[k], ny = y + NY[k];
        if (!InBounds(nx, ny)) return;
        byte u = _mat[ny * Width + nx];
        if (u is Mat.Void or Mat.Bedrock or Mat.Crystal or Mat.Obsidian) return;
        if (u == Mat.Empty || MaterialTable.Gas[u])
        {
            // Nothing to eat on that side: sink if there's open space below.
            if (Get(x, y + 1) == Mat.Empty) Swap(x, y, x, y + 1);
            return;
        }
        if (life <= 2) { SetCell(ref rng, x, y, Mat.Empty, 0); Emit(SimEventKind.Collapse, x, y, 2); return; }
        SetCell(ref rng, nx, ny, Mat.Void, (byte)(life - 1));
        SetCell(ref rng, x, y, rng.Chance(.15f) ? Mat.Void : Mat.Empty, (byte)(life / 3));
    }

    // ---------- Fire, embers, explosions, lava ----------

    bool FireTick(ref Rng rng, int x, int y, int i)
    {
        byte life = --_life[i];
        if (life == 0 || life > 250) { SetCell(ref rng, x, y, rng.Chance(.35f) ? Mat.Smoke : Mat.Empty, 60); return false; }
        for (int k = 0; k < 4; k++)
        {
            int nx = x + NX[k], ny = y + NY[k];
            if (!InBounds(nx, ny)) continue;
            byte u = _mat[ny * Width + nx];
            if (u == Mat.Water) { SetCell(ref rng, x, y, Mat.Steam, 70); return false; }
        }
        SpreadHeat(ref rng, x, y, 1);
        return true;
    }

    bool EmberTick(ref Rng rng, int x, int y, int i)
    {
        SpreadHeat(ref rng, x, y, .5f);
        if (y > 0 && _mat[i - Width] == Mat.Empty && rng.Chance(.01f)) SetCell(ref rng, x, y - 1, Mat.Smoke, 40);
        return true;
    }

    void BlastTick(ref Rng rng, int x, int y)
    {
        Explode(ref rng, x, y, 2 + rng.Int(2));
        SetCell(ref rng, x, y, Mat.Fire, (byte)(8 + rng.Int(8)));
    }

    /// <summary>
    /// An explosion: breaks weak solids into gravel, scatters powders, fills open space with fire and smoke, and
    /// turns nearby explosives into new blasts so the chain carries on.
    /// </summary>
    void Explode(ref Rng rng, int cx, int cy, int r)
    {
        Emit(SimEventKind.Explosion, cx, cy, r);
        // Resonite rings in sympathy: any within a wider reach goes off too, carrying the blast along its vein.
        int rr = r + 4;
        for (int y = cy - rr; y <= cy + rr; y++)
            for (int x = cx - rr; x <= cx + rr; x++)
                if (InBounds(x, y) && _mat[y * Width + x] == Mat.Resonite && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= rr * rr)
                    SetCell(ref rng, x, y, Mat.Blast, 2);
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
        {
            int dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy > r * r || !InBounds(x, y)) continue;
            byte u = _mat[y * Width + x];
            if (u == Mat.Blast) continue;
            if (MaterialTable.BurnsInto[u] == Mat.Blast && MaterialTable.Flammability[u] > 0) { SetCell(ref rng, x, y, Mat.Blast, 2); continue; }
            if (MaterialTable.Flammability[u] > 0) { Ignite(ref rng, x, y, u); continue; }
            float hard = MaterialTable.Hardness[u];
            if (MaterialTable.Solid[u] && hard > 0 && hard <= 1 && rng.Chance(.55f))
                SetCell(ref rng, x, y, rng.Chance(.5f) ? Mat.Gravel : Mat.Empty, 0);
            else if (MaterialTable.Powder[u] && rng.Chance(.5f)) SetCell(ref rng, x, y, Mat.Empty, 0);
            else if ((u == Mat.Empty || MaterialTable.Gas[u]) && rng.Chance(.5f))
                SetCell(ref rng, x, y, rng.Chance(.5f) ? Mat.Fire : Mat.Smoke, (byte)(6 + rng.Int(10)));
        }
    }

    bool LavaTick(ref Rng rng, int x, int y, int i)
    {
        SpreadHeat(ref rng, x, y, .5f);
        for (int k = 0; k < 4; k++) if (MaterialTable.Flammability[Get(x + NX[k], y + NY[k])] > 0) { MarkNext(x, y); break; }   // stay awake while something can catch
        if (y > 0 && _mat[i - Width] == Mat.Empty)
        {
            if (rng.Chance(.002f)) SetCell(ref rng, x, y - 1, Mat.Fire, 12);
            if (rng.Chance(.0004f)) { SetCell(ref rng, x, y, Mat.Basalt, 0); return false; }   // a crust forms
        }
        if (Tick % 3 != 0)                 // lava moves on every third tick only
        {
            if (CanFlow(x, y, MaterialTable.Density[Mat.Lava], true)) MarkNext(x, y);
            return false;
        }
        return true;
    }

    // ---------- Crumbling shale ----------

    /// <summary>
    /// Shale with nothing solid under it starts to fail: for a while it trickles grit (a warning you can see and
    /// hear), then it gives way as gravel. That leaves the shale above it unsupported, so collapses spread.
    /// </summary>
    void ShaleTick(ref Rng rng, int x, int y, int i)
    {
        // Held up from below, or spanning between solid neighbours on both sides like an arch.
        static bool Holds(byte m) => MaterialTable.Solid[m] || MaterialTable.Powder[m] || MaterialTable.Liquid[m];
        bool supported = Holds(Get(x, y + 1)) || (MaterialTable.Solid[Get(x - 1, y)] && MaterialTable.Solid[Get(x + 1, y)]);
        if (supported) { _life[i] = 0; return; }
        MarkNext(x, y);
        if (_life[i] == 0) { _life[i] = (byte)(60 + rng.Int(150)); return; }
        // Grit: about one grain per failing cell, a warning you can see before it goes.
        if (rng.Chance(.008f) && Get(x, y + 1) == Mat.Empty) SetCell(ref rng, x, y + 1, Mat.Sand, 0);
        if (--_life[i] <= 1)
        {
            SetCell(ref rng, x, y, Mat.Gravel, 0);
            Emit(SimEventKind.Collapse, x, y, 1);
        }
    }

    // ---------- Growth ----------

    void MyceliumGrow(ref Rng rng, int x, int y, int i)
    {
        if (_life[i] > 30 && rng.Chance(.0015f))
        {
            int k = rng.Int(4), sx = x + NX[k], sy = y + NY[k];
            if (InBounds(sx, sy) && _mat[sy * Width + sx] == Mat.Empty) { SetCell(ref rng, sx, sy, Mat.Spore, 200); _life[i] -= 4; }
        }
        if (!rng.Chance(.04f)) return;
        int d = rng.Int(4), nx = x + NX[d], ny = y + NY[d];
        if (!InBounds(nx, ny)) return;
        byte u = _mat[ny * Width + nx];
        if (u == Mat.Water) { _life[i] = 44; if (rng.Chance(.15f)) SetCell(ref rng, nx, ny, Mat.Empty, 0); }
        else if (u == Mat.Empty && _life[i] > 1 && Clings(nx, ny, x, y) && !Near(nx, ny, Mat.Salt, 1)) SetCell(ref rng, nx, ny, Mat.Mycelium, (byte)(_life[i] - 1));
        else if (u == Mat.Moss && rng.Chance(.04f)) SetCell(ref rng, nx, ny, Mat.Mycelium, _life[i]);
    }

    void MossGrow(ref Rng rng, int x, int y)
    {
        if (!rng.Chance(.012f)) return;
        int d = rng.Int(4), nx = x + NX[d], ny = y + NY[d];
        if (!InBounds(nx, ny) || _mat[ny * Width + nx] != Mat.Empty) return;
        if (Clings(nx, ny, x, y) && WetNear(nx, ny)) SetCell(ref rng, nx, ny, Mat.Moss, 0);
    }

    /// <summary>Roots grow down from where they hang, towards water, until their reach (life) runs out.</summary>
    void RootGrow(ref Rng rng, int x, int y, int i)
    {
        if (_life[i] <= 1 || !rng.Chance(.01f)) return;
        int nx = x + (rng.Chance(.2f) ? rng.Sign() : 0), ny = y + 1;
        if (!InBounds(nx, ny) || _mat[ny * Width + nx] != Mat.Empty) return;
        if (!Near(x, y, Mat.Water, 12) && !rng.Chance(.3f)) return;
        SetCell(ref rng, nx, ny, Mat.Root, (byte)(_life[i] - 1));
        _life[i] = 1;   // only the tip keeps growing
    }

    void FungusTick(ref Rng rng, int x, int y)
    {
        if (y > 0 && rng.Chance(.0015f) && Get(x, y - 1) == Mat.Empty) SetCell(ref rng, x, y - 1, Mat.Spore, 200);
    }

    bool SporeLife(ref Rng rng, int x, int y, int i)
    {
        if (rng.Chance(.3f) && --_life[i] <= 1) { SetCell(ref rng, x, y, Mat.Empty, 0); return false; }
        if (rng.Chance(.03f) && Clings(x, y, -1, -1) && WetNear(x, y) && !Near(x, y, Mat.Salt, 1)) { SetCell(ref rng, x, y, Mat.Mycelium, 30); return false; }
        return true;
    }
}
