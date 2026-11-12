using System;
using System.Collections.Generic;
using Strata.Sim;

namespace Strata.Rules;

/// <summary>
/// Generates a deep world for caving: a thin strip of surface, then the earth in depth zones, each with its own
/// host rock, seams and themed chambers, with the starter base in a safe granite chamber in the upper middle and a
/// shaft from it up to the surface. Godot-free.
///
/// Zones, top to bottom:
///   soil    – dirt and clay, hanging roots, water seeps
///   upper   – limestone and rock: moss grottos, glow-worm halls, salt caves with brine, ice pockets, crystal geodes
///   middle  – rock with shale bands and granite: coal galleries with gas, grey-lace forests, ore chambers, oil seeps
///   deep    – basalt and granite: lava tubes, sulfur vents with foul air, quicksilver pools, hydrogen pockets
///   bedrock
/// </summary>
public static class Underground
{
    public const int Surface = CaveGen.Surface;

    public enum Theme
    {
        Grotto, GlowHall, SaltCave, IcePocket, Geode, RootHall, CoalGallery, FungusForest, ShaleHall, OreChamber, OilSeep, LavaTube, SulfurVent, Quicksilver, HydrogenPocket, Plain,
        // Strange places
        FrostHollow, SkydustGrotto, GhostSpring, SunstoneHearth, MiteNest, VoidScar, ResoniteVein,
    }
    public sealed record Chamber(int X, int Y, int RX, int RY, Theme Theme);
    public sealed record Site(int BaseX, int BaseY, int ShaftX, int SampleX, int SampleY, IReadOnlyList<Chamber> Chambers)
    {
        public int SoilBottom => Surface + 34;
        public int UpperBottom => 230;
        public int MiddleBottom => 420;
        /// <summary>Where the surface truck parks its winch (the base's shaft).</summary>
        public int WinchX => ShaftX;
    }

    /// <summary>Half the width of the starter base chamber, in cells.</summary>

    public const int BaseHalf = 130;


    public static Site Generate(World w, ulong seed, int settleSteps = 120)
    {
        var rng = new Rng(seed ^ 0xC0FFEE);
        int W = w.Width, H = w.Height;
        float ox = rng.Float() * 1000, oy = rng.Float() * 1000;
        int soilBottom = Surface + 34, upperBottom = Math.Min(230, H / 2 - 40), middleBottom = Math.Min(420, H - 180), bedrockTop = H - 10;
        int baseX = W / 2, baseY = upperBottom - 6;          // floor of the base chamber

        // ---- 1. Host rock by depth, with veins and seams ----
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            byte m;
            if (y < Surface) m = Mat.Empty;
            else if (y >= bedrockTop) m = Mat.Bedrock;
            else if (y < soilBottom) m = Fbm(x * .04f + ox, y * .08f) > .55f ? Mat.Clay : Mat.Dirt;
            else if (y < upperBottom) m = Fbm(x * .02f + oy, y * .03f) > .5f ? Mat.Limestone : Mat.Rock;
            else if (y < middleBottom)
            {
                float band = MathF.Sin(y * .11f + Fbm(x * .01f, y * .01f) * 6);
                m = band > .75f ? Mat.Shale : Fbm(x * .015f + 40, y * .02f) > .62f ? Mat.Granite : Mat.Rock;
                // Coal seams: thin wavy bands.
                if (MathF.Abs(MathF.Sin(y * .05f + x * .004f + Fbm(x * .02f, y * .02f) * 3)) < .07f) m = Mat.Coal;
            }
            else m = Fbm(x * .02f + 90, y * .02f) > .55f ? Mat.Granite : Mat.Basalt;
            w.Set(x, y, m);
        }
        // Ore and mineral pockets.
        Veins(w, ref rng, W / 50, soilBottom + 10, middleBottom, Mat.Ore, 3, 7);
        Veins(w, ref rng, W / 90, upperBottom, H - 20, Mat.Copper, 3, 6);
        Veins(w, ref rng, W / 120, middleBottom, H - 20, Mat.Sulfur, 4, 8);
        Veins(w, ref rng, W / 140, soilBottom, upperBottom, Mat.Salt, 4, 8);

        // ---- 2. Caves: noise voids, then chambers and tunnels ----
        for (int y = soilBottom + 2; y < bedrockTop - 4; y++)
        for (int x = 2; x < W - 2; x++)
        {
            // Winding passages along the ridges of a noise field (connected, cave-like), plus a few open voids.
            float depth = (y - Surface) / (float)(H - Surface);
            float ridge = MathF.Abs(Fbm(x * .012f + ox * 2, y * .022f + oy * 2) - .5f);
            float ridge2 = MathF.Abs(Fbm(x * .02f + 300 + ox, y * .012f + 200) - .5f);
            float width = .03f + .012f * Fbm(x * .05f, y * .05f + 50);    // roomy: passages are about 10-16 cells across
            bool passage = ridge < width || ridge2 < width * .6f;
            bool hollow = Fbm(x * .03f + ox * 2, y * .045f + oy * 2) > .72f - depth * .04f;
            if (passage || hollow) Carve(w, x, y);
        }
        var chambers = new List<Chamber>();
        int tries = W * H / 9000;
        for (int k = 0; k < tries; k++)
        {
            int rx = 22 + rng.Int(36), ry = 12 + rng.Int(16);
            int cx = 20 + rx + rng.Int(W - 40 - 2 * rx), cy = soilBottom + 8 + ry + rng.Int(Math.Max(1, bedrockTop - 20 - soilBottom - 2 * ry));
            if (Math.Abs(cx - baseX) < BaseHalf + 20 && Math.Abs(cy - baseY) < 70) continue;   // keep clear of the base
            bool overlaps = false;
            foreach (var c in chambers) if (Math.Abs(c.X - cx) < c.RX + rx + 6 && Math.Abs(c.Y - cy) < c.RY + ry + 6) { overlaps = true; break; }
            if (overlaps) continue;
            var theme = PickTheme(ref rng, cy, soilBottom, upperBottom, middleBottom);
            chambers.Add(new Chamber(cx, cy, rx, ry, theme));
            Blob(w, ref rng, cx, cy, rx, ry, Mat.Empty);
        }
        // Tunnels: from the base outwards, and between neighbouring chambers, so most of the cave is reachable.
        foreach (var c in chambers)
        {
            Chamber? nearest = null; int best = int.MaxValue;
            foreach (var o in chambers) { if (o == c) continue; int d = Math.Abs(o.X - c.X) + Math.Abs(o.Y - c.Y); if (d < best) { best = d; nearest = o; } }
            if (nearest != null && best < 400) Tunnel(w, ref rng, c.X, c.Y, nearest.X, nearest.Y, 5);
        }
        for (int k = 0; k < 6; k++)
        {
            var c = chambers.Count > 0 ? chambers[rng.Int(chambers.Count)] : null;
            if (c != null) Tunnel(w, ref rng, baseX + (k % 2 == 0 ? -(BaseHalf - 20) : BaseHalf - 20), baseY - 6, c.X, c.Y, 5);
        }

        // ---- 3. Dress each chamber in its theme ----
        foreach (var c in chambers) Dress(w, ref rng, c);
        // Soil: roots hang from the ceilings of shallow voids, water seeps in.
        for (int x = 2; x < W - 2; x++)
            for (int y = soilBottom - 6; y < upperBottom; y++)
                if (w.Get(x, y) == Mat.Empty && MaterialTable.Solid[w.Get(x, y - 1)] && rng.Chance(y < soilBottom + 30 ? .06f : .01f))
                    w.Set(x, y, Mat.Root, (byte)(6 + rng.Int(20)));

        // ---- 4. The starter base: a granite-shelled chamber, a flat metal floor, timber posts, a shaft up ----
        Blob(w, ref rng, baseX, baseY - 16, BaseHalf + 16, 34, Mat.Granite, onlyIfNot: Mat.Bedrock, solidOnly: false);
        for (int y = baseY - 34; y < baseY; y++)
            for (int x = baseX - BaseHalf; x <= baseX + BaseHalf; x++)
            {
                float dx = (x - baseX) / (float)BaseHalf, dy = (y - (baseY - 4)) / 28f;
                if (dx * dx * dx * dx + dy * dy < 1) w.Set(x, y, Mat.Empty);   // boxy: tall walls, flat-ish roof
            }
        for (int x = baseX - BaseHalf; x <= baseX + BaseHalf; x++) for (int y = baseY; y < baseY + 3; y++) w.Set(x, y, Mat.Metal);
        // Specimen tanks: open-topped metal tanks with real material in them, to play with.
        foreach (var (tx, fill) in new[] { (baseX - 96, Mat.Water), (baseX - 86, Mat.Slime) })
        {
            for (int y = baseY - 9; y < baseY; y++) { w.Set(tx - 3, y, Mat.Metal); w.Set(tx + 3, y, Mat.Metal); }
            for (int y = baseY - 6; y < baseY; y++) for (int x = tx - 2; x <= tx + 2; x++) w.Set(x, y, fill);
        }
        // (Timber posts are drawn by the game as background; solid posts here boxed the player in.)
        for (int y = Surface - 2; y < baseY - 28; y++)
            for (int x = baseX - 4; x <= baseX + 4; x++) w.Set(x, y, Mat.Empty);
        for (int y = Surface; y < baseY - 28; y++) { w.Set(baseX - 5, y, Mat.Metal); w.Set(baseX + 5, y, Mat.Metal); }   // lined shaft
        // Exits: a doorway at each end into the tunnels.
        for (int y = baseY - 14; y < baseY; y++) for (int x = baseX - BaseHalf - 18; x < baseX - BaseHalf + 2; x++) Carve(w, x, y);
        for (int y = baseY - 14; y < baseY; y++) for (int x = baseX + BaseHalf - 2; x < baseX + BaseHalf + 18; x++) Carve(w, x, y);
        // ...and a tunnel from each doorway out through the granite shell to the nearest chamber on that side,
        // starting level so you can walk straight out.
        foreach (int side in new[] { -1, 1 })
        {
            int dx0 = baseX + side * (BaseHalf + 18);
            for (int y = baseY - 14; y < baseY; y++) for (int x = dx0; x != dx0 + side * 30; x += side) Carve(w, x, y);
            Chamber? target = null; int best = int.MaxValue;
            foreach (var c in chambers)
            {
                if (Math.Sign(c.X - baseX) != side) continue;
                int d = Math.Abs(c.X - dx0) + 2 * Math.Abs(c.Y - baseY);
                if (d < best) { best = d; target = c; }
            }
            if (target != null) Tunnel(w, ref rng, dx0 + side * 30, baseY - 6, target.X, target.Y, 5);
        }

        // ---- 5. Make the generated world stable, then let it settle ----
        // Shale left hanging by the carving comes down now, so that in play only the player's own digging (or a
        // blast) sets off a collapse. Repeat until nothing more would fail.
        for (int pass = 0; pass < 60; pass++)
        {
            int failed = 0;
            for (int y = soilBottom; y < bedrockTop; y++)
            for (int x = 1; x < W - 1; x++)
            {
                if (w.Get(x, y) != Mat.Shale) continue;
                byte below = w.Get(x, y + 1);
                bool held = MaterialTable.Solid[below] || MaterialTable.Liquid[below]
                    || (MaterialTable.Solid[w.Get(x - 1, y)] && MaterialTable.Solid[w.Get(x + 1, y)]);
                if (!held) { w.Set(x, y, Mat.Empty); failed++; }
            }
            if (failed == 0) break;
        }
        // Soil hanging over open space slumps now too.
        for (int y = bedrockTop; y >= Surface; y--)
            for (int x = 1; x < W - 1; x++)
                if (w.Get(x, y) is Mat.Dirt or Mat.Sand && w.Get(x, y + 1) == Mat.Empty) { w.Set(x, y, Mat.Empty); }

        // ---- 6. Let it settle ----
        for (int i = 0; i < settleSteps; i++) w.Step();

        // ---- 7. The goal: a sample on the floor of a deep chamber ----
        Chamber? goal = null;
        foreach (var c in chambers) if (c.Y > middleBottom - 60 && (goal == null || Math.Abs(c.X - baseX) > Math.Abs(goal.X - baseX) / 2)) { goal = c; if (rng.Chance(.4f)) break; }
        goal ??= chambers.Count > 0 ? chambers[^1] : new Chamber(baseX, baseY + 100, 10, 6, Theme.Plain);
        int sx = goal.X, sy = goal.Y;
        while (sy < H - 12 && !Player.BlocksBody(w.Get(sx, sy + 1))) sy++;
        return new Site(baseX, baseY, baseX, sx, sy, chambers);
    }

    static Theme PickTheme(ref Rng rng, int y, int soil, int upper, int middle)
    {
        Theme[] pool = y < upper
            ? [Theme.Grotto, Theme.Grotto, Theme.GlowHall, Theme.SaltCave, Theme.IcePocket, Theme.Geode, Theme.RootHall, Theme.Plain, Theme.FrostHollow, Theme.SkydustGrotto]
            : y < middle
                ? [Theme.CoalGallery, Theme.CoalGallery, Theme.FungusForest, Theme.FungusForest, Theme.ShaleHall, Theme.OreChamber, Theme.OilSeep, Theme.Plain, Theme.GhostSpring, Theme.ResoniteVein]
                : [Theme.LavaTube, Theme.LavaTube, Theme.SulfurVent, Theme.Quicksilver, Theme.HydrogenPocket, Theme.Geode, Theme.Plain, Theme.SunstoneHearth, Theme.MiteNest, Theme.VoidScar];
        return pool[rng.Int(pool.Length)];
    }

    /// <summary>Fills a chamber with what its theme calls for: wall linings, floor pools, ceiling growths, trapped gas.</summary>
    static void Dress(World w, ref Rng rng, Chamber c)
    {
        int floor = FloorY(w, c), ceil = CeilY(w, c);
        switch (c.Theme)
        {
            case Theme.Grotto:
                Pool(w, c, floor, 4 + rng.Int(4), Mat.Water);
                Line(w, ref rng, c, .5f, Mat.Moss, ceilingToo: false);
                Hang(w, ref rng, c, ceil, Mat.Glowworm, .25f, 1);
                break;
            case Theme.GlowHall:
                Hang(w, ref rng, c, ceil, Mat.Glowworm, .6f, 2);
                Pool(w, c, floor, 2, Mat.Water);
                break;
            case Theme.SaltCave:
                Lining(w, ref rng, c, 3, Mat.Salt, .8f);
                Pool(w, c, floor, 5, Mat.Brine);
                Spikes(w, ref rng, c, floor, Mat.Crystal, 6);
                break;
            case Theme.IcePocket:
                Lining(w, ref rng, c, 3, Mat.Ice, .9f);
                Pool(w, c, floor, 4, Mat.Water);
                break;
            case Theme.Geode:
                Lining(w, ref rng, c, 2, Mat.Crystal, .9f);
                Spikes(w, ref rng, c, floor, Mat.Crystal, 10);
                Hang(w, ref rng, c, ceil, Mat.Crystal, .3f, 3);
                break;
            case Theme.RootHall:
                Hang(w, ref rng, c, ceil, Mat.Root, .5f, 8);
                Pool(w, c, floor, 2, Mat.Mud);
                break;
            case Theme.CoalGallery:
                Lining(w, ref rng, c, 2, Mat.Coal, .6f);
                Floor(w, c, floor, 2, Mat.CoalDust);
                Trap(w, c, ceil, c.RY / 2, Mat.Methane);
                break;
            case Theme.FungusForest:
                Pool(w, c, floor, 2, Mat.Water);
                Line(w, ref rng, c, .7f, Mat.Mycelium, ceilingToo: true);
                for (int k = 0; k < c.RX / 6; k++) Mushroom(w, ref rng, c.X - c.RX + 4 + rng.Int(2 * c.RX - 8), floor - 1);
                break;
            case Theme.ShaleHall:
                Lining(w, ref rng, c, 4, Mat.Shale, 1f, ceilingOnly: true);
                Floor(w, c, floor, 2, Mat.Gravel);
                break;
            case Theme.OreChamber:
                Lining(w, ref rng, c, 2, rng.Chance(.5f) ? Mat.Ore : Mat.Copper, .5f);
                break;
            case Theme.OilSeep:
                Pool(w, c, floor, 5, Mat.Water);
                Pool(w, c, floor - 5, 2, Mat.Oil);
                Floor(w, c, floor - 7, 1, Mat.Tar);
                break;
            case Theme.LavaTube:
                Lining(w, ref rng, c, 2, Mat.Basalt, .8f);
                Pool(w, c, floor, 3 + rng.Int(3), Mat.Lava);
                Spikes(w, ref rng, c, floor - 4, Mat.Obsidian, 4);
                break;
            case Theme.SulfurVent:
                Lining(w, ref rng, c, 2, Mat.Sulfur, .6f);
                Pool(w, c, floor, 6, Mat.FoulGas, life: 255);
                break;
            case Theme.Quicksilver:
                Pool(w, c, floor, 3, Mat.Quicksilver);
                Lining(w, ref rng, c, 2, Mat.Granite, .7f);
                break;
            case Theme.HydrogenPocket:
                Lining(w, ref rng, c, 2, Mat.Copper, .5f);
                Trap(w, c, ceil, c.RY, Mat.Hydrogen);
                break;

            // ---- Strange places ----
            case Theme.FrostHollow:
                Lining(w, ref rng, c, 2, Mat.Ice, .8f);
                Pool(w, c, floor, 3, Mat.Cryo);
                Floor(w, c, floor - 3, 3, Mat.Snow);
                break;
            case Theme.SkydustGrotto:
                Trap(w, c, ceil, 4, Mat.Skydust);          // dunes on the ceiling
                Hang(w, ref rng, c, ceil + 4, Mat.Glowworm, .2f, 1);
                Pool(w, c, floor, 2, Mat.Water);
                break;
            case Theme.GhostSpring:
                Pool(w, c, floor, 4, Mat.Ghostwater);
                Line(w, ref rng, c, .3f, Mat.Moss, ceilingToo: false);
                break;
            case Theme.SunstoneHearth:
                Lining(w, ref rng, c, 2, Mat.Sunstone, .25f);
                Pool(w, c, floor, 2, Mat.Water);
                Spikes(w, ref rng, c, floor - 2, Mat.Sunstone, 4);
                break;
            case Theme.MiteNest:
                Lining(w, ref rng, c, 3, Mat.Ore, .7f);
                Pool(w, c, floor, 2, Mat.Mite);
                break;
            case Theme.VoidScar:
                // Voids held in a cage of crystal. Break the crystal and they get out.
                Lining(w, ref rng, c, 3, Mat.Crystal, 1f);
                for (int k = 0; k < 3; k++)
                {
                    int vx = c.X - c.RX / 2 + rng.Int(Math.Max(1, c.RX)), vy = floor - 3 - rng.Int(3);
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                        if (w.Get(vx + dx, vy + dy) != Mat.Bedrock) w.Set(vx + dx, vy + dy, Math.Abs(dx) == 2 || Math.Abs(dy) == 2 ? Mat.Crystal : Mat.Void, 60);
                }
                break;
            case Theme.ResoniteVein:
                for (int x = c.X - c.RX * 2; x <= c.X + c.RX * 2; x++)
                {
                    int vy = c.Y + c.RY + 2 + (int)(2 * MathF.Sin(x * .15f));
                    if (MaterialTable.Solid[w.Get(x, vy)] && w.Get(x, vy) != Mat.Bedrock) w.Set(x, vy, Mat.Resonite);
                }
                Trap(w, c, ceil, c.RY / 2, Mat.Hydrogen);
                break;
        }
    }

    // ---------- Shapes ----------

    static void Carve(World w, int x, int y) { byte m = w.Get(x, y); if (m != Mat.Bedrock && m != Mat.Metal) w.Set(x, y, Mat.Empty); }

    static void Blob(World w, ref Rng rng, int cx, int cy, int rx, int ry, byte m, byte onlyIfNot = 255, bool solidOnly = false)
    {
        float wob = rng.Float() * 10;
        for (int y = cy - ry - 4; y <= cy + ry + 4; y++)
        for (int x = cx - rx - 4; x <= cx + rx + 4; x++)
        {
            if (!w.InBounds(x, y) || y < Surface) continue;
            float a = MathF.Atan2(y - cy, x - cx), r = 1 + .18f * MathF.Sin(a * 3 + wob) + .1f * MathF.Sin(a * 7 + wob * 2);
            float dx = (x - cx) / (rx * r), dy = (y - cy) / (ry * r);
            if (dx * dx + dy * dy > 1) continue;
            byte cur = w.Get(x, y);
            if (cur == Mat.Bedrock || cur == onlyIfNot || (solidOnly && !MaterialTable.Solid[cur])) continue;
            w.Set(x, y, m, World.DefaultLife(m));
        }
    }

    static void Tunnel(World w, ref Rng rng, int x0, int y0, int x1, int y1, int r)
    {
        float x = x0, y = y0, phase = rng.Float() * 6.28f;
        for (int s = 0; s < 3000; s++)
        {
            float dx = x1 - x, dy = y1 - y, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < 3) break;
            // Head roughly towards the target, but meander: a slow swing plus a little jitter.
            float a = MathF.Atan2(dy, dx) + 1.1f * MathF.Sin(s * .045f + phase) + .6f * MathF.Sin(s * .013f + phase * 2) + (rng.Float() - .5f) * .5f;
            x += MathF.Cos(a) * 1.5f; y += MathF.Sin(a) * 1.5f;
            int rr = r + (int)(1.5f * (1 + MathF.Sin(s * .09f + phase)));   // widens and narrows
            for (int ty = (int)y - rr; ty <= (int)y + rr; ty++)
                for (int tx = (int)x - rr; tx <= (int)x + rr; tx++)
                    if ((tx - x) * (tx - x) + (ty - y) * (ty - y) <= rr * rr && ty > Surface + 12) Carve(w, tx, ty);
        }
    }

    static void Veins(World w, ref Rng rng, int count, int y0, int y1, byte m, int rmin, int rmax)
    {
        for (int k = 0; k < count; k++)
        {
            int x = 10 + rng.Int(w.Width - 20), y = y0 + rng.Int(Math.Max(1, y1 - y0));
            int rx = rmin + rng.Int(rmax - rmin), ry = 1 + rng.Int(3);
            for (int ty = y - ry; ty <= y + ry; ty++)
                for (int tx = x - rx; tx <= x + rx; tx++)
                {
                    float dx = (tx - x) / (float)rx, dy = (ty - y) / (float)ry;
                    if (dx * dx + dy * dy <= 1 && MaterialTable.Solid[w.Get(tx, ty)] && w.Get(tx, ty) != Mat.Bedrock) w.Set(tx, ty, m);
                }
        }
    }

    static int FloorY(World w, Chamber c) { int y = c.Y; while (y < c.Y + c.RY * 2 && !MaterialTable.Solid[w.Get(c.X, y + 1)]) y++; return y; }
    static int CeilY(World w, Chamber c) { int y = c.Y; while (y > c.Y - c.RY * 2 && !MaterialTable.Solid[w.Get(c.X, y - 1)]) y--; return y; }

    /// <summary>Fills the bottom of a chamber with a liquid (or heavy gas) up to a depth.</summary>
    static void Pool(World w, Chamber c, int floor, int depth, byte m, byte life = 0)
    {
        for (int y = floor - depth + 1; y <= floor; y++)
            for (int x = c.X - c.RX - 4; x <= c.X + c.RX + 4; x++)
                if (w.Get(x, y) == Mat.Empty && Enclosed(w, x, y)) w.Set(x, y, m, life == 0 ? World.DefaultLife(m) : life);
    }

    static void Floor(World w, Chamber c, int floor, int depth, byte m) => Pool(w, c, floor, depth, m);

    /// <summary>Fills the top of a chamber with a light gas.</summary>
    static void Trap(World w, Chamber c, int ceil, int depth, byte m)
    {
        for (int y = ceil; y < ceil + depth; y++)
            for (int x = c.X - c.RX - 4; x <= c.X + c.RX + 4; x++)
                if (w.Get(x, y) == Mat.Empty && Enclosed(w, x, y)) w.Set(x, y, m, World.DefaultLife(m));
    }

    /// <summary>True when there is solid somewhere to both sides within a short distance (stops pools pouring out of tunnels).</summary>
    static bool Enclosed(World w, int x, int y)
    {
        bool l = false, r = false;
        for (int d = 1; d < 50 && !(l && r); d++) { l |= MaterialTable.Solid[w.Get(x - d, y)]; r |= MaterialTable.Solid[w.Get(x + d, y)]; }
        return l && r;
    }

    /// <summary>Replaces the rock around a chamber's edge with a lining material.</summary>
    static void Lining(World w, ref Rng rng, Chamber c, int thick, byte m, float chance, bool ceilingOnly = false)
    {
        for (int y = c.Y - c.RY * 2; y <= c.Y + c.RY * 2; y++)
        for (int x = c.X - c.RX * 2; x <= c.X + c.RX * 2; x++)
        {
            byte cur = w.Get(x, y);
            if (!MaterialTable.Solid[cur] || cur is Mat.Bedrock or Mat.Metal or Mat.Wood) continue;
            if (ceilingOnly && y > c.Y) continue;
            bool nearAir = false;
            for (int d = 1; d <= thick && !nearAir; d++)
                nearAir = w.Get(x, y + d) == Mat.Empty || w.Get(x, y - d) == Mat.Empty || w.Get(x + d, y) == Mat.Empty || w.Get(x - d, y) == Mat.Empty;
            if (nearAir && rng.Chance(chance)) w.Set(x, y, m);
        }
    }

    /// <summary>A coat of something on the surfaces of a chamber (moss, grey lace).</summary>
    static void Line(World w, ref Rng rng, Chamber c, float chance, byte m, bool ceilingToo)
    {
        for (int y = c.Y - c.RY * 2; y <= c.Y + c.RY * 2; y++)
        for (int x = c.X - c.RX * 2; x <= c.X + c.RX * 2; x++)
        {
            if (w.Get(x, y) != Mat.Empty) continue;
            bool onFloor = MaterialTable.Solid[w.Get(x, y + 1)], onCeil = ceilingToo && MaterialTable.Solid[w.Get(x, y - 1)];
            if ((onFloor || onCeil) && rng.Chance(chance)) w.Set(x, y, m, World.DefaultLife(m));
        }
    }

    /// <summary>Things hanging from the ceiling: glow worms, roots, crystals.</summary>
    static void Hang(World w, ref Rng rng, Chamber c, int ceil, byte m, float chance, int maxLen)
    {
        for (int x = c.X - c.RX * 2; x <= c.X + c.RX * 2; x++)
        {
            int y = c.Y; while (y > c.Y - c.RY * 2 && w.Get(x, y - 1) == Mat.Empty) y--;
            if (!MaterialTable.Solid[w.Get(x, y - 1)] || !rng.Chance(chance)) continue;
            int len = 1 + rng.Int(maxLen);
            for (int k = 0; k < len && w.Get(x, y + k) == Mat.Empty; k++) w.Set(x, y + k, m, m == Mat.Root ? (byte)(len + 10) : (byte)0);
        }
    }

    /// <summary>Spikes rising from the floor (crystals, obsidian).</summary>
    static void Spikes(World w, ref Rng rng, Chamber c, int floor, byte m, int count)
    {
        for (int k = 0; k < count; k++)
        {
            int x = c.X - c.RX + rng.Int(2 * c.RX), h = 2 + rng.Int(6);
            int y = floor; while (y < floor + 10 && w.Get(x, y + 1) == Mat.Empty) y++;
            for (int d = 0; d < h; d++) { if (w.Get(x, y - d) != Mat.Empty) break; w.Set(x, y - d, m); if (d < h / 2) w.Set(x + 1, y - d, m); }
        }
    }

    /// <summary>A giant cap fungus: a stalk of grey lace with a wide fungus cap.</summary>
    static void Mushroom(World w, ref Rng rng, int x, int floor)
    {
        int h = 6 + rng.Int(10), r = 3 + rng.Int(5);
        int y = floor; while (y < floor + 10 && w.Get(x, y + 1) == Mat.Empty) y++;
        for (int d = 0; d < h; d++) if (w.Get(x, y - d) == Mat.Empty) w.Set(x, y - d, Mat.Mycelium, 1);
        int top = y - h;
        for (int dy = -2; dy <= 0; dy++)
            for (int dx = -r - dy; dx <= r + dy; dx++)
                if (w.Get(x + dx, top + dy) == Mat.Empty) w.Set(x + dx, top + dy, Mat.Fungus);
    }

    // ---------- Noise ----------

    static float Fbm(float x, float y)
    {
        float s = 0, a = .5f, f = 1;
        for (int o = 0; o < 4; o++) { s += a * Noise(x * f, y * f); a *= .5f; f *= 2; }
        return s;
    }

    static float Noise(float x, float y)
    {
        int xi = (int)MathF.Floor(x), yi = (int)MathF.Floor(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }

    static float Hash(int x, int y) => (Rng.Mix((ulong)x, (ulong)y, 77) >> 40) / (float)(1 << 24);
}
