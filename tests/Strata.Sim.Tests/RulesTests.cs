using Strata.Rules;
using Strata.Sim;

namespace Strata.Sim.Tests;

public class CraftingTests
{
    static (Item, int)? StrikeUntilDone(Bench b)
    {
        for (int i = 0; i < Bench.StrikesNeeded - 1; i++) Assert.Null(b.Strike());
        return b.Strike();
    }

    [Fact]
    public void Three_fibre_twist_into_rope()
    {
        var b = new Bench();
        b.Place(Item.Fibre); b.Place(Item.Fibre); b.Place(Item.Fibre);
        Assert.Equal((Item.Rope, 1), StrikeUntilDone(b));
        Assert.Empty(b.On);
    }

    [Fact]
    public void Two_metal_make_a_pick_and_glass_with_fibre_makes_two_flares()
    {
        var b = new Bench();
        b.Place(Item.Metal); b.Place(Item.Metal);
        Assert.Equal((Item.Pick, 1), StrikeUntilDone(b));
        b.Place(Item.Glass); b.Place(Item.Fibre);
        Assert.Equal((Item.Flare, 2), StrikeUntilDone(b));
    }

    [Fact]
    public void Wrong_combinations_make_junk_and_are_lost()
    {
        var b = new Bench();
        b.Place(Item.Stone); b.Place(Item.Ore);
        Assert.Equal((Item.Junk, 1), StrikeUntilDone(b));
        Assert.Empty(b.On);
    }

    [Fact]
    public void Adding_to_the_bench_restarts_the_work_and_it_holds_four_things()
    {
        var b = new Bench();
        b.Place(Item.Fibre); b.Strike(); b.Strike();
        b.Place(Item.Fibre);
        Assert.Equal(0, b.Strikes);
        b.Place(Item.Fibre); b.Place(Item.Fibre);
        Assert.False(b.Place(Item.Fibre));
    }

    [Fact]
    public void Forge_smelts_ore_and_melts_sand_after_cooking_and_stays_hot_for_a_while()
    {
        var f = new Forge();
        f.Put(Item.Ore);
        Assert.False(f.Done);
        f.Update(f.CookTime + .1f);
        Assert.True(f.Done);
        Assert.True(f.Hot);
        f.Update(f.CoolTime);
        Assert.False(f.Hot);
        Assert.Equal(Item.Metal, f.TakeOut());

        f.Put(Item.Sand); f.Update(f.CookTime + .1f);
        Assert.Equal(Item.Glass, f.TakeOut());
    }

    [Fact]
    public void Taking_something_out_early_gives_it_back_unchanged()
    {
        var f = new Forge();
        f.Put(Item.Ore); f.Update(1);
        Assert.Equal(Item.Ore, f.TakeOut());
        Assert.Null(f.Content);
    }

    [Fact]
    public void Fibre_burns_away_in_the_forge()
    {
        var f = new Forge();
        f.Put(Item.Fibre);
        Assert.True(f.Burning);
        f.Update(f.CookTime + .1f);
        Assert.Null(f.Content);
    }

    [Fact]
    public void Pack_respects_capacity_for_bulk_but_not_tools()
    {
        var p = new Pack { Capacity = 3 };
        Assert.True(p.Add(Item.Ore, 3));
        Assert.False(p.Add(Item.Sand));
        Assert.True(p.Add(Item.Flare, 5));
        Assert.True(p.Full);
    }

    [Fact]
    public void Digging_fractions_add_up_to_whole_units()
    {
        var p = new Pack();
        for (int i = 0; i < 10; i++) p.AddPartial(Item.Stone, .1f);
        Assert.InRange(p[Item.Stone], 0, 1);
        for (int i = 0; i < 10; i++) p.AddPartial(Item.Stone, .1f);
        Assert.InRange(p[Item.Stone], 1, 2);
    }
}

public class TruckTests
{
    static World Flat(int groundY, int chunksX = 4)
    {
        var w = new World(chunksX, 2, 1);
        for (int x = 0; x < w.Width; x++) for (int y = groundY; y < w.Height; y++) w.Set(x, y, Mat.Rock);
        return w;
    }

    [Fact]
    public void Drives_on_flat_ground_and_burns_fuel()
    {
        var w = Flat(60);
        var t = new Truck(w, 30);
        for (int i = 0; i < 120; i++) t.Update(1, 0, 1 / 60f);
        Assert.True(t.X > 40);
        Assert.True(t.Fuel < t.FuelMax);
        Assert.Equal(60, t.Y);
    }

    [Fact]
    public void Bridges_a_shaft_sized_gap_without_dropping_in()
    {
        var w = Flat(60);
        for (int x = 100; x < 111; x++) for (int y = 60; y < 120; y++) w.Set(x, y, Mat.Empty);
        var t = new Truck(w, 40);
        float lowest = 0;
        for (int i = 0; i < 300; i++) { t.Update(1, 0, 1 / 60f); lowest = MathF.Max(lowest, t.Y); }
        Assert.True(t.X > 130, $"truck at {t.X}");
        Assert.True(lowest <= 60, $"truck sank to {lowest}");
    }

    [Fact]
    public void Rolls_over_mounds_and_outcrops()
    {
        var w = Flat(60);
        for (int x = 100; x < 120; x++) for (int y = 53; y < 60; y++) w.Set(x, y, Mat.Mycelium, 30);
        var t = new Truck(w, 40);
        for (int i = 0; i < 300; i++) t.Update(1, 0, 1 / 60f);
        Assert.True(t.X > 150, $"truck at {t.X}");
    }

    [Fact]
    public void Does_not_move_without_fuel()
    {
        var w = Flat(60);
        var t = new Truck(w, 40) { Fuel = 0 };
        for (int i = 0; i < 120; i++) t.Update(1, 0, 1 / 60f);
        Assert.Equal(40, t.X, 1);
    }
}

public class WorldGenTests
{
    [Fact]
    public void Site_has_base_road_and_a_reachable_sample_below_the_winch()
    {
        var w = new World(40, 6, 5);
        var s = CaveGen.Generate(w, 5);
        Assert.True(s.BaseX < s.BloomX && s.BloomX < s.WinchX);
        Assert.True(Player.BlocksBody(w.Get(s.BaseX, CaveGen.Surface)));
        Assert.Equal(Mat.Empty, w.Get(s.WinchX, CaveGen.Surface + 20));
        Assert.True(s.SampleY > CaveGen.Surface + 100);
        Assert.Equal(Mat.Empty, w.Get(s.SampleX, s.SampleY));
        Assert.True(Player.BlocksBody(w.Get(s.SampleX, s.SampleY + 1)) || w.Get(s.SampleX, s.SampleY + 1) == Mat.Water);
        int ore = 0, fibre = 0;
        foreach (var m in w.Materials) { if (m == Mat.Ore) ore++; if (m == Mat.Mycelium) fibre++; }
        Assert.True(ore > 200, $"ore cells: {ore}");
        Assert.True(fibre > 100, $"grey lace cells: {fibre}");
    }
}

public class MovementTests
{
    static (World w, Player p) Flat()
    {
        var w = new World(4, 2, 1);
        for (int x = 0; x < w.Width; x++) for (int y = 100; y < w.Height; y++) w.Set(x, y, Mat.Rock);
        var p = new Player(w, 0, 0) { X = 60, Y = 100 };
        for (int i = 0; i < 10; i++) p.Update(new PlayerInput(), 1 / 60f);
        return (w, p);
    }

    static float JumpApex(bool hold)
    {
        var (_, p) = Flat();
        float top = p.Y;
        p.Update(new PlayerInput { JumpPressed = true, JumpHeld = hold }, 1 / 60f);
        for (int i = 0; i < 90; i++) { p.Update(new PlayerInput { JumpHeld = hold }, 1 / 60f); top = MathF.Min(top, p.Y); }
        return 100 - top;
    }

    [Fact]
    public void Holding_jump_goes_higher_than_tapping_it()
    {
        float held = JumpApex(true), tapped = JumpApex(false);
        Assert.True(held > tapped * 1.5f, $"held {held}, tapped {tapped}");
        Assert.True(held > 8, $"held jump only {held} cells");
    }

    [Fact]
    public void Walking_accelerates_rather_than_jumping_to_full_speed()
    {
        var (_, p) = Flat();
        p.Update(new PlayerInput { Right = true }, 1 / 60f);
        Assert.True(p.VX > 0 && p.VX < p.WalkSpeed);
        for (int i = 0; i < 30; i++) p.Update(new PlayerInput { Right = true }, 1 / 60f);
        Assert.Equal(p.WalkSpeed, p.VX, 1);
    }

    [Fact]
    public void A_jump_pressed_just_after_walking_off_a_ledge_still_works()
    {
        var w = new World(4, 2, 1);
        for (int x = 0; x < 70; x++) for (int y = 100; y < w.Height; y++) w.Set(x, y, Mat.Rock);
        var p = new Player(w, 0, 0) { X = 60, Y = 100 };
        for (int i = 0; i < 10; i++) p.Update(new PlayerInput(), 1 / 60f);
        int frames = 0;
        while (p.Grounded && frames++ < 120) p.Update(new PlayerInput { Right = true }, 1 / 60f);
        p.Update(new PlayerInput { Right = true, JumpPressed = true, JumpHeld = true }, 1 / 60f);
        Assert.True(p.Jumped || p.VY < 0, "coyote jump did not fire");
    }

    [Fact]
    public void Steps_up_ledges_up_to_four_cells()
    {
        var (w, p) = Flat();
        for (int x = 70; x < 90; x++) for (int y = 96; y < 100; y++) w.Set(x, y, Mat.Rock);
        for (int i = 0; i < 120 && p.X < 80; i++) p.Update(new PlayerInput { Right = true }, 1 / 60f);
        Assert.True(p.X >= 80 && p.Y < 97.5f, $"at {p.X},{p.Y}");
    }

    [Fact]
    public void Rock_landing_in_you_never_pushes_you_up_through_rock()
    {
        var (w, p) = Flat();
        // A solid slab right through the body and well above the head: there's nowhere free nearby.
        for (int x = 50; x < 70; x++) for (int y = 80; y < 100; y++) w.Set(x, y, Mat.Rock);
        for (int i = 0; i < 60; i++) { p.Unstick(); p.Update(new PlayerInput { Up = true, Right = true }, 1 / 60f); }
        Assert.True(p.Y > 95, $"zipped up to y={p.Y}");
    }

    [Fact]
    public void Sand_in_your_body_is_shoved_aside_or_stepped_out_of()
    {
        var (w, p) = Flat();
        for (int x = 55; x < 66; x++) for (int y = 85; y < 100; y++) w.Set(x, y, Mat.Sand);
        p.Unstick();
        Assert.True(p.Y > 95, $"zipped up to y={p.Y}");
        for (int i = 0; i < 30; i++) { p.Unstick(); p.Update(new PlayerInput { Right = true }, 1 / 60f); }
        Assert.True(p.X > 60.5f, "still stuck in the sand");
    }

    [Fact]
    public void Walking_into_a_jagged_wall_does_not_run_up_it()
    {
        var (w, p) = Flat();
        // A rough near-vertical wall: each column three cells higher than the last.
        for (int k = 0; k < 15; k++) for (int y = 100 - 3 * (k + 1); y < 100; y++) w.Set(66 + k, y, Mat.Rock);
        for (int i = 0; i < 20; i++) p.Update(new PlayerInput { Right = true }, 1 / 60f);
        Assert.True(p.Y > 100 - 15, $"ran up the wall to y={p.Y} in a third of a second");
    }
}

public class ClimbingTests
{
    // Floor at y=100, a wall of the given material from x=80 to 99 rising to y=top.
    static (World w, Player p) Wall(byte wall, int top = 60)
    {
        var w = new World(4, 2, 1);
        for (int x = 0; x < w.Width; x++) for (int y = 100; y < w.Height; y++) w.Set(x, y, Mat.Rock);
        for (int x = 80; x < 100; x++) for (int y = top; y < 100; y++) w.Set(x, y, wall);
        var p = new Player(w, 0, 0) { X = 74, Y = 100 };
        for (int i = 0; i < 10; i++) p.Update(new PlayerInput(), 1 / 60f);
        return (w, p);
    }

    static void JumpAt(Player p)
    {
        p.Update(new PlayerInput { Right = true, JumpPressed = true, JumpHeld = true }, 1 / 60f);
        for (int i = 0; i < 30 && !p.Clinging; i++) p.Update(new PlayerInput { Right = true, JumpHeld = true }, 1 / 60f);
    }

    [Fact]
    public void Jumping_into_rock_grabs_it_and_climbing_up_pulls_you_over_the_top()
    {
        var (_, p) = Wall(Mat.Rock);
        JumpAt(p);
        Assert.True(p.Clinging, "did not grab the wall");
        bool mantled = false;
        for (int i = 0; i < 600 && !mantled; i++) { p.Update(new PlayerInput { Right = true, Up = true }, 1 / 60f); mantled |= p.Mantled; }
        Assert.True(mantled, $"never got over the top; at {p.X},{p.Y}, stamina {p.Stamina}");
        Assert.True(p.X > 80 && p.Y <= 60.5f, $"standing at {p.X},{p.Y}");
    }

    [Fact]
    public void Hanging_too_long_runs_out_of_stamina_and_you_fall()
    {
        var (_, p) = Wall(Mat.Rock, top: 20);
        JumpAt(p);
        Assert.True(p.Clinging);
        bool failed = false;
        for (int i = 0; i < 60 * 40 && !failed; i++) { p.Update(new PlayerInput { Right = true }, 1 / 60f); failed |= p.GripFailed; }
        Assert.True(failed, "grip never failed");
        Assert.False(p.Clinging);
    }

    [Fact]
    public void Sand_cannot_be_gripped()
    {
        var (_, p) = Wall(Mat.Sand);
        JumpAt(p);
        Assert.False(p.Clinging);
    }

    [Fact]
    public void Jumping_off_a_wall_kicks_you_away_from_it()
    {
        var (_, p) = Wall(Mat.Rock, top: 20);
        JumpAt(p);
        Assert.True(p.Clinging);
        p.Update(new PlayerInput { Right = true, JumpPressed = true, JumpHeld = true }, 1 / 60f);
        Assert.False(p.Clinging);
        Assert.True(p.VX < 0 && p.VY < 0, $"velocity {p.VX},{p.VY}");
    }

    [Fact]
    public void Stamina_comes_back_on_the_ground()
    {
        var (_, p) = Wall(Mat.Rock, top: 20);
        p.Stamina = 10;
        for (int i = 0; i < 180; i++) p.Update(new PlayerInput(), 1 / 60f);
        Assert.Equal(p.StaminaMax, p.Stamina, 1);
    }
}

public class UndergroundGenTests
{
    static int Count(World w, byte m) { int n = 0; foreach (var c in w.Materials) if (c == m) n++; return n; }

    [Fact]
    public void Generates_a_safe_base_varied_chambers_and_settles_down()
    {
        var w = new World(24, 8, 11);
        var s = Underground.Generate(w, 11, settleSteps: 60);

        // The base: a metal floor, open air above it, and the shaft up to the surface.
        Assert.Equal(Mat.Metal, w.Get(s.BaseX, s.BaseY));
        Assert.Equal(Mat.Empty, w.Get(s.BaseX, s.BaseY - 5));
        Assert.Equal(Mat.Empty, w.Get(s.BaseX, Underground.Surface + 10));

        // Variety: many themes, and the signature materials actually present.
        var themes = s.Chambers.Select(c => c.Theme).Distinct().Count();
        Assert.True(themes >= 8, $"only {themes} themes");
        foreach (var m in new[] { Mat.Water, Mat.Moss, Mat.Glowworm, Mat.Coal, Mat.Shale, Mat.Ore, Mat.Lava, Mat.Root, Mat.Mycelium })
            Assert.True(Count(w, m) > 0, $"no {MaterialTable.Name[m]} in the world");

        // The goal sits on something solid, below the base.
        Assert.True(s.SampleY > s.BaseY && Player.BlocksBody(w.Get(s.SampleX, s.SampleY + 1)));

        // Running on: growth stays bounded and most of the world goes back to sleep.
        int lace0 = Count(w, Mat.Mycelium);
        int awake = 0, visited = 0;
        for (int t = 0; t < 300; t++) { w.Step(); awake = w.AwakeChunksLastStep; visited = w.CellsVisitedLastStep; }
        int chunks = w.ChunksX * w.ChunksY;
        Assert.True(awake < chunks * .5f, $"{awake} of {chunks} chunks still awake");
        Assert.True(visited < w.Width * w.Height / 10, $"{visited} cells visited per step after settling");
        Assert.True(Count(w, Mat.Mycelium) < lace0 * 3 + 500, "grey lace is running away");
    }
}

public class BubbleTests
{
    static (World w, Player p) Drop(bool bubble, float vy)
    {
        var w = new World(4, 4, 1);
        for (int x = 0; x < w.Width; x++) for (int y = 200; y < w.Height; y++) w.Set(x, y, Mat.Rock);
        var p = new Player(w, 0, 0) { X = 60, Y = 150, VY = vy, Bubble = bubble };
        return (w, p);
    }

    static bool Hurt(Player p) => p.Body.Parts.Any(h => h != Strata.Rules.Hurt.Fine) || p.Body.Downed;

    [Fact]
    public void A_terminal_speed_landing_in_the_bubble_bounces_and_hurts_nothing()
    {
        var (_, p) = Drop(true, 150);
        bool bounced = false;
        for (int i = 0; i < 40; i++) { p.Update(new PlayerInput(), 1 / 60f); bounced |= p.Bounced; }
        Assert.True(bounced, "no bounce");
        Assert.False(Hurt(p), "hurt in the bubble");
    }

    [Fact]
    public void The_same_landing_without_the_bubble_hurts()
    {
        var (_, p) = Drop(false, 150);
        for (int i = 0; i < 40; i++) p.Update(new PlayerInput(), 1 / 60f);
        Assert.True(Hurt(p));
    }

    [Fact]
    public void The_bubble_keeps_your_momentum_in_the_air()
    {
        var w = new World(4, 4, 1);
        var a = new Player(w, 0, 0) { X = 60, Y = 50, VX = 90, Bubble = true };
        var b = new Player(w, 0, 0) { X = 60, Y = 50, VX = 90 };
        for (int i = 0; i < 20; i++) { a.Update(new PlayerInput(), 1 / 60f); b.Update(new PlayerInput(), 1 / 60f); }
        Assert.True(a.VX > 85, $"bubble lost momentum: {a.VX}");
        Assert.True(b.VX < 60, $"no bubble kept momentum: {b.VX}");
    }
}
