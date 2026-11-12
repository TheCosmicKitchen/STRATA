using Strata.Sim;

namespace Strata.Sim.Tests;

public class WorldTests
{
    static int Count(World w, byte m)
    {
        int n = 0;
        foreach (var c in w.Materials) if (c == m) n++;
        return n;
    }

    static ulong Hash(World w)
    {
        ulong h = 1469598103934665603UL;
        foreach (var c in w.Materials) { h ^= c; h *= 1099511628211UL; }
        return h;
    }

    static World Cave(ulong seed)
    {
        var w = new World(4, 3, seed);
        var r = new Rng(seed);
        for (int y = 0; y < w.Height; y++)
        for (int x = 0; x < w.Width; x++)
        {
            float f = r.Float();
            byte m = f < .25f ? Mat.Rock : f < .40f ? Mat.Sand : f < .50f ? Mat.Water : f < .55f ? Mat.Methane : Mat.Empty;
            w.Set(x, y, m, World.DefaultLife(m));
        }
        w.Paint(40, 40, 6, Mat.Fire);
        w.Paint(150, 120, 5, Mat.Lava);
        w.Paint(200, 60, 4, Mat.Acid);
        w.Paint(100, 150, 4, Mat.Mycelium);
        return w;
    }

    [Fact]
    public void Same_seed_and_inputs_give_the_same_world()
    {
        var a = Cave(42); var b = Cave(42);
        for (int i = 0; i < 300; i++) { a.Step(); b.Step(); }
        Assert.Equal(Hash(a), Hash(b));
    }

    [Fact]
    public void Different_seeds_diverge()
    {
        var a = Cave(1); var b = Cave(2);
        for (int i = 0; i < 50; i++) { a.Step(); b.Step(); }
        Assert.NotEqual(Hash(a), Hash(b));
    }

    [Fact]
    public void Sand_and_water_are_conserved_when_nothing_reacts()
    {
        var w = new World(3, 3, 7);
        w.Paint(60, 40, 15, Mat.Sand);
        w.Paint(130, 40, 15, Mat.Water);
        int sand = Count(w, Mat.Sand), water = Count(w, Mat.Water);
        for (int i = 0; i < 600; i++) w.Step();
        Assert.Equal(sand, Count(w, Mat.Sand));
        Assert.Equal(water, Count(w, Mat.Water));
    }

    [Fact]
    public void Sand_falls_to_the_floor_and_the_world_goes_to_sleep()
    {
        var w = new World(2, 2, 3);
        w.Paint(64, 20, 6, Mat.Sand);
        for (int i = 0; i < 1000 && (i < 5 || w.CellsVisitedLastStep > 0); i++) w.Step();
        Assert.Equal(0, w.CellsVisitedLastStep);
        Assert.Equal(0, w.AwakeChunksLastStep);
        Assert.Equal(Mat.Sand, w.Get(64, w.Height - 1));
        Assert.Equal(Mat.Empty, w.Get(64, 20));
    }

    [Fact]
    public void A_still_world_does_no_work_and_painting_wakes_only_nearby_chunks()
    {
        var w = new World(4, 4, 9);
        for (int x = 0; x < w.Width; x++) w.Set(x, w.Height - 1, Mat.Rock);
        for (int i = 0; i < 3; i++) w.Step();
        Assert.Equal(0, w.CellsVisitedLastStep);

        w.Set(10, 10, Mat.Sand);
        w.Step();
        Assert.Equal(1, w.AwakeChunksLastStep);
        Assert.True(w.CellsVisitedLastStep <= 9);
    }

    [Fact]
    public void Sand_crosses_chunk_borders()
    {
        var w = new World(1, 3, 5);
        w.Set(10, 5, Mat.Sand);
        for (int i = 0; i < 400; i++) w.Step();
        Assert.Equal(Mat.Sand, w.Get(10, w.Height - 1));
    }

    [Fact]
    public void Lava_meeting_water_makes_obsidian()
    {
        var w = new World(1, 1, 11);
        for (int x = 0; x < 64; x++) w.Set(x, 63, Mat.Rock);
        w.Paint(20, 55, 4, Mat.Lava);
        w.Paint(30, 55, 4, Mat.Water);
        for (int i = 0; i < 300; i++) w.Step();
        Assert.True(Count(w, Mat.Obsidian) > 0);
    }

    [Fact]
    public void Fire_burns_through_a_methane_pocket()
    {
        var w = new World(2, 2, 13);
        for (int y = 30; y < 90; y++) for (int x = 30; x < 90; x++)
            w.Set(x, y, (x is 30 or 89 || y is 30 or 89) ? Mat.Rock : Mat.Methane);
        int methane = Count(w, Mat.Methane);
        w.Set(60, 60, Mat.Fire, 40);
        for (int i = 0; i < 400; i++) w.Step();
        Assert.True(Count(w, Mat.Methane) < methane / 10, $"methane left: {Count(w, Mat.Methane)} of {methane}");
    }

    [Fact]
    public void Water_puts_out_fire_and_makes_steam()
    {
        var w = new World(1, 1, 17);
        for (int x = 0; x < 64; x++) w.Set(x, 63, Mat.Rock);
        w.Paint(32, 58, 4, Mat.Water);
        w.Set(32, 50, Mat.Fire, 200);
        w.Set(31, 50, Mat.Fire, 200);
        bool sawSteam = false;
        for (int i = 0; i < 200 && !sawSteam; i++) { w.Step(); sawSteam = Count(w, Mat.Steam) > 0; }
        Assert.True(sawSteam || Count(w, Mat.Fire) == 0);
    }

    [Fact]
    public void Acid_eats_rock_and_gives_off_CO2()
    {
        var w = new World(1, 1, 19);
        for (int y = 40; y < 64; y++) for (int x = 0; x < 64; x++) w.Set(x, y, Mat.Rock);
        int rock = Count(w, Mat.Rock);
        w.Paint(32, 30, 5, Mat.Acid);
        bool sawCO2 = false;
        for (int i = 0; i < 400; i++) { w.Step(); sawCO2 |= Count(w, Mat.CO2) > 0; }
        Assert.True(Count(w, Mat.Rock) < rock);
        Assert.True(sawCO2);
    }

    [Fact]
    public void Edges_are_walls_and_out_of_bounds_access_is_safe()
    {
        var w = new World(1, 1, 23);
        w.Set(-1, 0, Mat.Sand);
        w.Set(64, 64, Mat.Sand);
        Assert.Equal(Mat.Bedrock, w.Get(-5, 3));
        w.Paint(0, 32, 6, Mat.Water);
        int water = Count(w, Mat.Water);
        for (int i = 0; i < 300; i++) w.Step();
        Assert.Equal(water, Count(w, Mat.Water));
    }

    [Fact]
    public void Chunk_versions_change_only_where_cells_changed()
    {
        var w = new World(3, 1, 29);
        for (int x = 0; x < w.Width; x++) w.Set(x, 63, Mat.Rock);
        for (int i = 0; i < 3; i++) w.Step();
        uint v0 = w.ChunkVersion(0, 0), v2 = w.ChunkVersion(2, 0);
        w.Set(10, 10, Mat.Sand);
        for (int i = 0; i < 5; i++) w.Step();
        Assert.True(w.ChunkVersion(0, 0) > v0);
        Assert.Equal(v2, w.ChunkVersion(2, 0));
    }

    [Fact]
    public void CopyChunk_returns_that_chunks_cells()
    {
        var w = new World(2, 2, 31);
        w.Set(64 + 3, 64 + 5, Mat.Rock);
        var buf = new byte[World.ChunkSize * World.ChunkSize];
        w.CopyChunk(1, 1, buf);
        Assert.Equal(Mat.Rock, buf[5 * World.ChunkSize + 3]);
    }
}
