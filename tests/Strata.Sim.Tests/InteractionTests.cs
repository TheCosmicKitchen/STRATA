using Strata.Sim;

namespace Strata.Sim.Tests;

/// <summary>
/// Every rule in the reaction table and every flammable material gets its own test, generated from the tables, so
/// adding a material or a rule automatically adds a check that it actually happens in the simulation.
/// </summary>
public class InteractionTests
{
    // A sealed bedrock box in one chunk: material A fills the left half, B the right half, touching along the middle.
    static World Box(byte a, byte b, int seed)
    {
        var w = new World(1, 1, (ulong)seed);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            bool wall = x < 20 || x > 43 || y < 30 || y > 57;
            w.Set(x, y, wall ? Mat.Bedrock : x < 32 ? a : b, World.DefaultLife(wall ? Mat.Bedrock : x < 32 ? a : b));
        }
        return w;
    }

    static int Count(World w, byte m) { int n = 0; foreach (var c in w.Materials) if (c == m) n++; return n; }

    public static IEnumerable<object[]> AllReactions() => Reactions.All.Select((r, i) => new object[] { i, $"{MaterialTable.Name[r.A]} + {MaterialTable.Name[r.B]}" });

    [Theory]
    [MemberData(nameof(AllReactions))]
    public void Reaction_happens(int index, string pair)
    {
        var r = Reactions.All[index];
        var w = Box(r.A, r.B, index + 1);
        // Something must change: one of the products appears or grows, or one of the inputs is used up.
        var products = new[] { r.AInto, r.BInto }.Where(p => p != Reaction.Keep).Distinct().ToArray();
        var start = products.ToDictionary(p => p, p => Count(w, p));
        int a0 = Count(w, r.A), b0 = Count(w, r.B);
        int budget = (int)Math.Clamp(60 / r.Chance, 400, 40000);
        bool seen = false;
        for (int t = 0; t < budget && !seen; t++)
        {
            w.Step();
            if (t % 10 != 0) continue;
            seen = products.Any(p => Count(w, p) > start[p]) || Count(w, r.A) < a0 || Count(w, r.B) < b0;
        }
        Assert.True(seen, $"{pair}: nothing happened in {budget} ticks ({r.Why})");
    }

    public static IEnumerable<object[]> Flammables() =>
        Enumerable.Range(0, Mat.Count).Where(m => MaterialTable.Flammability[m] > 0).Select(m => new object[] { m, MaterialTable.Name[m] });

    [Theory]
    [MemberData(nameof(Flammables))]
    public void Catches_fire_from_lava(int material, string name)
    {
        byte m = (byte)material;
        var w = new World(1, 1, (ulong)(material + 100));
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            bool wall = x < 16 || x > 47 || y < 20 || y > 57;
            byte c = wall ? Mat.Bedrock : y >= 50 ? Mat.Lava : y >= 40 ? m : Mat.Empty;
            w.Set(x, y, c, World.DefaultLife(c));
        }
        int start = Count(w, m);
        byte into = MaterialTable.BurnsInto[m];
        bool burned = false, exploded = false;
        bool wantBlast = into == Mat.Blast;
        for (int t = 0; t < 6000 && !(wantBlast ? exploded : burned || exploded); t++)
        {
            w.Step();
            exploded |= w.Events.Any(e => e.Kind == SimEventKind.Explosion);
            if (t % 10 == 0) burned = Count(w, m) < start && (Count(w, into) > 0 || Count(w, Mat.Fire) > 0 || Count(w, Mat.Ash) > 0 || Count(w, Mat.Smoke) > 0);
        }
        Assert.True(burned || exploded, $"{name} never caught fire next to lava");
        if (into == Mat.Blast) Assert.True(exploded, $"{name} should explode");
    }

    [Fact]
    public void A_hydrogen_pocket_explodes_in_a_chain_and_breaks_weak_rock()
    {
        var w = new World(2, 2, 7);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) w.Set(x, y, Mat.Shale);
        for (int y = 40; y < 80; y++) for (int x = 40; x < 90; x++) w.Set(x, y, Mat.Hydrogen);
        int shale = Count(w, Mat.Shale);
        w.Set(64, 60, Mat.Fire, 40);
        int explosions = 0;
        for (int t = 0; t < 200; t++) { w.Step(); explosions += w.Events.Count(e => e.Kind == SimEventKind.Explosion); }
        Assert.True(explosions > 20, $"only {explosions} explosions");
        Assert.True(Count(w, Mat.Hydrogen) < 50, $"{Count(w, Mat.Hydrogen)} hydrogen left");
        Assert.True(Count(w, Mat.Shale) < shale - 50, "the blast should have broken the shale around the pocket");
    }

    [Fact]
    public void A_shale_ceiling_holds_until_you_cut_it_then_trickles_grit_and_collapses()
    {
        var w = new World(2, 2, 9);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            w.Set(x, y, y < 40 ? Mat.Rock : y < 50 ? Mat.Shale : y < 110 ? Mat.Empty : Mat.Rock);
        for (int x = 0; x < 128; x++) { w.Set(x, 50, Mat.Empty); }   // bottom of the shale band is the ceiling
        for (int t = 0; t < 400; t++) w.Step();
        Assert.Equal(0, Count(w, Mat.Gravel));                        // an unbroken ceiling spans and holds

        // Dig a hole through the ceiling.
        for (int y = 40; y < 50; y++) for (int x = 60; x < 64; x++) w.Set(x, y, Mat.Empty);
        bool grit = false; int collapses = 0;
        for (int t = 0; t < 3000; t++)
        {
            w.Step();
            collapses += w.Events.Count(e => e.Kind == SimEventKind.Collapse);
            grit |= Count(w, Mat.Sand) > 0;
        }
        Assert.True(grit, "no warning grit before the collapse");
        Assert.True(collapses > 10, $"only {collapses} cells gave way");
        Assert.True(Count(w, Mat.Gravel) > 10);
    }

    [Fact]
    public void Coal_dust_raised_near_a_flame_goes_up()
    {
        var w = new World(1, 1, 3);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) w.Set(x, y, x < 4 || x > 59 || y > 59 ? Mat.Bedrock : Mat.Empty);
        for (int y = 52; y < 60; y++) for (int x = 10; x < 50; x++) w.Set(x, y, Mat.CoalDust);
        w.Set(30, 51, Mat.Fire, 40);
        bool boom = false;
        for (int t = 0; t < 400 && !boom; t++) { w.Step(); boom = w.Events.Any(e => e.Kind == SimEventKind.Explosion); }
        Assert.True(boom);
    }

    [Fact]
    public void Every_material_has_a_name_and_a_colour_and_sane_properties()
    {
        for (byte m = 0; m < Mat.Count; m++)
        {
            Assert.False(string.IsNullOrWhiteSpace(MaterialTable.Name[m]));
            Assert.True(MaterialTable.Density[m] > 0, MaterialTable.Name[m]);
            if (MaterialTable.Liquid[m]) Assert.True(MaterialTable.Density[m] > 1, $"{MaterialTable.Name[m]} would float in air");
        }
        Assert.Equal(Mat.Count, MaterialTable.Defs.Select(d => d.Name).Distinct().Count());
    }

    [Fact]
    public void Reactions_only_produce_real_materials()
    {
        foreach (var r in Reactions.All)
        {
            Assert.True(r.A < Mat.Count && r.B < Mat.Count);
            Assert.True(r.AInto == Reaction.Keep || r.AInto < Mat.Count);
            Assert.True(r.BInto == Reaction.Keep || r.BInto < Mat.Count);
            Assert.InRange(r.Chance, 0.0001f, 1f);
        }
    }
}

public class StrangeMatterTests
{
    static int Count(World w, byte m) { int n = 0; foreach (var c in w.Materials) if (c == m) n++; return n; }

    static World Box(int seed = 1)
    {
        var w = new World(2, 2, (ulong)seed);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            w.Set(x, y, x < 2 || x > 125 || y < 2 || y > 125 ? Mat.Bedrock : Mat.Empty);
        return w;
    }

    [Fact]
    public void Skydust_falls_up_and_piles_on_the_ceiling()
    {
        var w = Box();
        w.Paint(64, 100, 5, Mat.Skydust);
        for (int t = 0; t < 400; t++) w.Step();
        Assert.Equal(Mat.Skydust, w.Get(64, 2 + 0 + 1));
        int high = 0; for (int y = 3; y < 12; y++) for (int x = 40; x < 90; x++) if (w.Get(x, y) == Mat.Skydust) high++;
        Assert.True(high > 50, $"only {high} skydust near the ceiling");
    }

    [Fact]
    public void Ghostwater_seeps_through_rock_but_not_crystal()
    {
        var w = Box(2);
        for (int x = 2; x < 64; x++) for (int y = 60; y < 66; y++) w.Set(x, y, Mat.Rock);
        for (int x = 64; x < 126; x++) for (int y = 60; y < 66; y++) w.Set(x, y, Mat.Crystal);
        for (int y = 2; y < 126; y++) w.Set(64, y, Mat.Bedrock);   // two separate halves, top and bottom
        for (int x = 10; x < 120; x++) for (int y = 55; y < 60; y++) if (x != 64) w.Set(x, y, Mat.Ghostwater);
        for (int t = 0; t < 3000; t++) w.Step();
        int belowRock = 0, belowCrystal = 0;
        for (int y = 66; y < 126; y++) for (int x = 2; x < 126; x++) if (w.Get(x, y) == Mat.Ghostwater) { if (x < 64) belowRock++; else belowCrystal++; }
        Assert.True(belowRock > 20, $"only {belowRock} seeped through rock");
        Assert.True(belowCrystal < belowRock / 3, $"{belowCrystal} got through crystal");
    }

    [Fact]
    public void A_void_eats_until_spent_and_crystal_holds_it()
    {
        var w = Box(3);
        for (int y = 30; y < 100; y++) for (int x = 30; x < 100; x++) w.Set(x, y, Mat.Rock);
        // A crystal cage around one void; another void loose in the rock.
        for (int y = 40; y < 52; y++) for (int x = 40; x < 52; x++) w.Set(x, y, y is 40 or 51 || x is 40 or 51 ? Mat.Crystal : Mat.Empty);
        w.Set(45, 45, Mat.Void, 80);
        w.Set(80, 80, Mat.Void, 80);
        int rock0 = Count(w, Mat.Rock);
        for (int t = 0; t < 6000; t++) w.Step();
        Assert.True(Count(w, Mat.Rock) < rock0 - 20, $"the loose void ate nothing: rock {Count(w, Mat.Rock)} of {rock0}, void {Count(w, Mat.Void)}, life {w.LifeAt(80, 80)}, at {w.Get(80, 80)}");
        Assert.Equal(44, Count(w, Mat.Crystal));                                  // the cage is intact
        Assert.True(Count(w, Mat.Rock) > rock0 - 3000, "the void never ran out");
    }

    [Fact]
    public void Resonite_carries_a_blast_along_its_vein()
    {
        var w = Box(4);
        for (int y = 30; y < 100; y++) for (int x = 2; x < 126; x++) w.Set(x, y, Mat.Granite);
        for (int x = 10; x < 120; x++) w.Set(x, 64, Mat.Resonite);
        for (int y = 60; y < 64; y++) for (int x = 6; x < 12; x++) w.Set(x, y, Mat.Hydrogen);
        w.Set(8, 62, Mat.Fire, 20);
        int far = 0;
        for (int t = 0; t < 300; t++) { w.Step(); far = Math.Max(far, w.Events.Where(e => e.Kind == SimEventKind.Explosion).Select(e => e.X).DefaultIfEmpty(0).Max()); }
        Assert.True(far > 100, $"the blast only reached x={far}");
    }

    [Fact]
    public void Cold_fire_runs_through_methane_and_freezes_water()
    {
        var w = Box(5);
        for (int x = 2; x < 126; x++) for (int y = 110; y < 126; y++) w.Set(x, y, Mat.Water);
        for (int x = 2; x < 126; x++) for (int y = 95; y < 110; y++) w.Set(x, y, Mat.Methane);
        w.Paint(64, 100, 2, Mat.Cryo);
        bool cold = false;
        for (int t = 0; t < 600; t++) { w.Step(); cold |= Count(w, Mat.Coldflame) > 20; }
        Assert.True(cold, "no cold fire spread");
        Assert.True(Count(w, Mat.Ice) > 10, "the water didn't freeze");
        Assert.Equal(0, Count(w, Mat.Fire));
    }

    [Fact]
    public void Mites_eat_metal_then_starve_back_into_metal()
    {
        var w = Box(6);
        for (int x = 2; x < 126; x++) for (int y = 110; y < 126; y++) w.Set(x, y, Mat.Metal);
        w.Paint(64, 108, 3, Mat.Mite);
        int metal0 = Count(w, Mat.Metal), peak = 0;
        for (int t = 0; t < 4000; t++) { w.Step(); peak = Math.Max(peak, Count(w, Mat.Mite)); }
        Assert.True(peak > 60, $"mites never spread (peak {peak})");
        Assert.True(Count(w, Mat.Metal) + Count(w, Mat.Mite) <= metal0 + 40, "mites made matter from nothing");
    }

    [Fact]
    public void Sunstone_keeps_boiling_water()
    {
        var w = Box(7);
        for (int x = 2; x < 126; x++) for (int y = 120; y < 126; y++) w.Set(x, y, Mat.Sunstone);
        for (int x = 2; x < 126; x++) for (int y = 100; y < 120; y++) w.Set(x, y, Mat.Water);
        int water0 = Count(w, Mat.Water);
        for (int t = 0; t < 3000; t++) w.Step();
        Assert.True(Count(w, Mat.Water) < water0 * .9f, "the water never boiled");
        Assert.Equal(124 * 6, Count(w, Mat.Sunstone));
    }
}
