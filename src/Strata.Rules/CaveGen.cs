using System;
using Strata.Sim;

namespace Strata.Rules;

/// <summary>
/// Builds the MVP world: a long strip of surface with the base at the west end, a grey-lace bloom and ore outcrops
/// along the way, and the dig site with its winch shaft near the east end, above a cave. Godot-free.
/// </summary>
public static class CaveGen
{
    public const int Surface = 48;          // nominal ground row
    public const int ShaftHalfWidth = 5;

    public sealed record Site(int BaseX, int WinchX, int AnchorY, int SampleX, int SampleY, int BloomX);

    public static Site Generate(World w, ulong seed)
    {
        var rng = new Rng(seed);
        int W = w.Width, H = w.Height;
        int baseX = 200, winchX = W - 700, bloomX = (baseX + winchX) / 2;
        float ox = rng.Float() * 1000, oy = rng.Float() * 1000;

        // Ground line: gentle hills, flat pads at the base and the dig site.
        var ground = new int[W];
        for (int x = 0; x < W; x++)
        {
            float h = 3 * MathF.Sin(x * .021f + ox) + 4 * MathF.Sin(x * .0067f + oy) + 1.5f * MathF.Sin(x * .09f);
            float flat = MathF.Min(Pad(x, baseX, 110), Pad(x, winchX, 70));
            ground[x] = Surface + (int)MathF.Round(h * flat);
        }

        // Rock mass with noise-carved voids; solid crust, and solid ground under the base.
        for (int y = 0; y < H; y++)
        for (int x = 0; x < W; x++)
        {
            byte m = Mat.Empty;
            if (y >= ground[x])
            {
                float depth = (y - Surface) / (float)(H - Surface);
                float n = Fbm(x * .035f + ox, y * .05f + oy);
                m = n > .52f - depth * .06f ? Mat.Empty : Mat.Rock;
                if (y < ground[x] + 8) m = Mat.Rock;
                if (Math.Abs(x - baseX) < 140 && y < Surface + 40) m = Mat.Rock;
            }
            w.Set(x, y, m);
        }

        // Shaft, chamber and side chambers under the winch.
        int chamberY = H - 70;
        for (int y = Surface; y < chamberY; y++)
            for (int x = winchX - ShaftHalfWidth; x <= winchX + ShaftHalfWidth; x++)
                w.Set(x, y, Mat.Empty);
        Ellipse(w, winchX, chamberY, 70, 26, Mat.Empty);
        Ellipse(w, winchX - 95, chamberY + 12, 40, 16, Mat.Empty);
        Ellipse(w, winchX + 100, chamberY - 10, 36, 20, Mat.Empty);

        // Worm tunnels.
        for (int t = 0; t < W / 90; t++)
        {
            float x = 300 + rng.Float() * (W - 300), y = Surface + 30 + rng.Float() * (H - Surface - 60), a = rng.Float() * 6.28f;
            for (int s = 0; s < 220; s++)
            {
                a += (rng.Float() - .5f) * .6f;
                x += MathF.Cos(a) * 1.5f; y += MathF.Sin(a) * .8f;
                if (y > Surface + 14) Ellipse(w, (int)x, (int)y, 3, 3, Mat.Empty);
            }
        }
        for (int x = 0; x < W; x++) for (int y = H - 6; y < H; y++) w.Set(x, y, Mat.Rock);

        // Ore: veins in the rock, plus a few outcrops at the surface along the road.
        for (int k = 0; k < W / 60; k++)
        {
            int x = 40 + rng.Int(W - 80), y = Surface + 12 + rng.Int(H - Surface - 30);
            Ellipse(w, x, y, 4 + rng.Int(6), 2 + rng.Int(4), Mat.Ore, onlyRock: true);
        }
        for (int k = 0; k < 4; k++)
        {
            int x = baseX + 220 + k * (winchX - baseX - 400) / 4 + rng.Int(60);
            for (int dx = -6; dx <= 6; dx++)
                for (int dy = 0; dy < 5 - Math.Abs(dx) / 3; dy++) w.Set(x + dx, ground[x + dx] + dy, Mat.Ore);
        }

        // Grey lace bloom on the surface: mounds of mycelium (fibre) halfway along the road.
        for (int x = bloomX - 40; x <= bloomX + 40; x++)
        {
            int h = (int)(4 + 3 * MathF.Sin(x * .4f) + rng.Int(3)) - Math.Abs(x - bloomX) / 12;
            for (int dy = 1; dy <= h; dy++) w.Set(x, ground[x] - dy, Mat.Mycelium, 30);
        }

        // Cave materials.
        for (int k = 0; k < W / 45; k++)
        {
            int x = 300 + rng.Int(W - 340), y = Surface + 20 + rng.Int(H - Surface - 80);
            if (Math.Abs(x - winchX) < ShaftHalfWidth + 4 && y < chamberY) continue;
            Ellipse(w, x, y, 6 + rng.Int(8), 4 + rng.Int(5), Mat.Sand, onlyRock: true);
        }
        FillLow(w, winchX + 100, chamberY - 10, 36, 20, Mat.Water, 8);
        FillLow(w, winchX - 95, chamberY + 12, 40, 16, Mat.CO2, 6, life: 255);
        for (int k = 0; k < W / 120; k++)
        {
            int x = 300 + rng.Int(W - 340), y = Surface + 40 + rng.Int(H - Surface - 120);
            if (Math.Abs(x - winchX) < 30) continue;
            Ellipse(w, x, y, 7, 5, Mat.Methane, onlyEmpty: true);
        }
        for (int k = 0; k < W; k++)
        {
            int x = rng.Int(W), y = Surface + 10 + rng.Int(H - Surface - 20);
            if (w.Get(x, y) == Mat.Empty && w.Get(x, y + 1) == Mat.Rock && Near(w, x, y, Mat.Water, 12)) w.Set(x, y, Mat.Moss);
        }
        // Grey lace inside the cave near the shaft, so fibre is also found below.
        for (int k = 0; k < 6; k++)
        {
            int x = winchX - 60 + rng.Int(120), y = chamberY - 20 + rng.Int(40);
            if (w.Get(x, y) == Mat.Empty) Ellipse(w, x, y, 3, 2, Mat.Mycelium, onlyEmpty: true, life: 30);
        }
        Ellipse(w, W - 40, H - 14, 18, 6, Mat.Lava, onlyEmpty: true);


        // Let loose sand and liquids settle before play.
        for (int i = 0; i < 150; i++) w.Step();

        // A walkable floor at the bottom of the shaft, leading to the sample, so the goal is always reachable.
        int floorY = chamberY + 18;
        for (int x = winchX - 24; x <= winchX + 44; x++)
        {
            for (int y = floorY; y < floorY + 6; y++) w.Set(x, y, Mat.Rock);
            for (int y = floorY - 14; y < floorY; y++) w.Set(x, y, Mat.Empty);
            if (x <= winchX - 22 || x >= winchX + 42) for (int y = floorY - 3; y < floorY; y++) w.Set(x, y, Mat.Rock);   // low lip keeps water out
        }

        int sx = winchX + 34, sy = floorY - 1;
        return new Site(baseX, winchX, Surface - 18, sx, sy, bloomX);
    }

    static float Pad(int x, int cx, int r)
    {
        int d = Math.Abs(x - cx);
        return d < r ? 0 : MathF.Min(1, (d - r) / 60f);
    }

    static void Ellipse(World w, int cx, int cy, int rx, int ry, byte m, bool onlyRock = false, bool onlyEmpty = false, byte life = 0)
    {
        for (int y = cy - ry; y <= cy + ry; y++)
        for (int x = cx - rx; x <= cx + rx; x++)
        {
            float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
            if (dx * dx + dy * dy > 1 || !w.InBounds(x, y) || y < Surface) continue;
            byte cur = w.Get(x, y);
            if (onlyRock && cur != Mat.Rock) continue;
            if (onlyEmpty && cur != Mat.Empty) continue;
            w.Set(x, y, m, life == 0 ? World.DefaultLife(m) : life);
        }
    }

    static void FillLow(World w, int cx, int cy, int rx, int ry, byte m, int depth, byte life = 0)
    {
        for (int y = cy + ry - depth; y <= cy + ry; y++)
        for (int x = cx - rx; x <= cx + rx; x++)
            if (w.InBounds(x, y) && w.Get(x, y) == Mat.Empty) w.Set(x, y, m, life == 0 ? World.DefaultLife(m) : life);
    }

    static bool Near(World w, int x, int y, byte m, int r)
    {
        for (int dy = -r; dy <= r; dy += 2) for (int dx = -r; dx <= r; dx += 2) if (w.Get(x + dx, y + dy) == m) return true;
        return false;
    }

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

    static float Hash(int x, int y) => (Rng.Mix((ulong)x, (ulong)y, 99) >> 40) / (float)(1 << 24);
}
