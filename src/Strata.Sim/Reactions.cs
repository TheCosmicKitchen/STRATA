namespace Strata.Sim;

/// <summary>
/// What happens when two materials touch. Each rule says: when <see cref="A"/> is next to <see cref="B"/>, with
/// <see cref="Chance"/> per tick, A becomes <see cref="AInto"/> and B becomes <see cref="BInto"/> (<see cref="Keep"/>
/// means unchanged). Rules are looked up from either side, so each pair is written once.
/// Fire and ignition are handled by flammability in the material table, not here.
/// </summary>
public readonly record struct Reaction(byte A, byte B, float Chance, byte AInto, byte BInto, string Why)
{
    public const byte Keep = 255;
}

public static class Reactions
{
    const byte K = Reaction.Keep;

    public static readonly Reaction[] All =
    [
        // Water and heat
        new(Mat.Water, Mat.Lava, 1f, Mat.Steam, Mat.Obsidian, "Lava quenched by water turns to obsidian in a burst of steam."),
        new(Mat.Brine, Mat.Lava, 1f, Mat.Steam, Mat.Obsidian, "Brine on lava boils off and leaves obsidian."),
        new(Mat.Brine, Mat.Fire, .5f, Mat.Salt, Mat.Steam, "Boiling brine leaves salt behind."),
        new(Mat.Ice, Mat.Lava, 1f, Mat.Water, Mat.Basalt, "Lava against ice: the ice melts, the lava sets to basalt."),
        new(Mat.Ice, Mat.Fire, .5f, Mat.Water, Mat.Steam, "Fire melts ice."),
        new(Mat.Ice, Mat.Ember, .1f, Mat.Water, Mat.Ash, "Embers melt ice and go out."),
        new(Mat.Ice, Mat.Steam, .05f, Mat.Water, Mat.Water, "Steam melts ice and condenses."),
        new(Mat.Water, Mat.Ice, .0008f, Mat.Ice, K, "Water touching ice slowly freezes."),
        new(Mat.Water, Mat.Ember, .5f, Mat.Steam, Mat.Ash, "Water puts out embers."),
        new(Mat.Quicksilver, Mat.Lava, .05f, Mat.MercuryVapour, K, "Lava boils quicksilver into poisonous vapour."),
        new(Mat.Quicksilver, Mat.Fire, .1f, Mat.MercuryVapour, K, "Fire boils quicksilver into poisonous vapour."),
        new(Mat.Quicksilver, Mat.Ember, .03f, Mat.MercuryVapour, K, "Embers boil quicksilver."),
        new(Mat.Mud, Mat.Lava, .2f, Mat.Dirt, K, "Lava bakes mud dry."),
        new(Mat.Mud, Mat.Fire, .3f, Mat.Dirt, Mat.Smoke, "Fire dries mud to dirt."),
        new(Mat.Mud, Mat.Ember, .05f, Mat.Dirt, K, "Embers dry mud."),
        new(Mat.Limestone, Mat.Lava, .002f, Mat.CO2, K, "Lava cooks limestone, giving off CO2."),
        new(Mat.Fire, Mat.Ash, .05f, Mat.Smoke, K, "Ash smothers flames."),

        // Dissolving and soaking
        new(Mat.Water, Mat.Salt, .01f, Mat.Brine, Mat.Empty, "Water dissolves salt into brine."),
        new(Mat.Water, Mat.Clay, .01f, Mat.Empty, Mat.Mud, "Clay soaks up water and slumps into mud."),
        new(Mat.Water, Mat.Dirt, .02f, Mat.Mud, Mat.Mud, "Water and dirt make mud."),
        new(Mat.Water, Mat.FoulGas, .01f, Mat.Acid, Mat.Empty, "Water absorbs foul air and turns to acid."),
        new(Mat.Brine, Mat.Crystal, .0005f, Mat.Crystal, K, "Crystals grow slowly in brine."),

        // Acid
        new(Mat.Acid, Mat.Rock, .015f, Mat.CO2, Mat.CO2, "Acid eats rock, giving off CO2."),
        new(Mat.Acid, Mat.Sand, .015f, K, Mat.CO2, "Acid dissolves sand."),
        new(Mat.Acid, Mat.Limestone, .2f, K, Mat.CO2, "Acid fizzes through limestone, giving off lots of CO2."),
        new(Mat.Acid, Mat.Granite, .002f, Mat.CO2, Mat.CO2, "Acid barely marks granite."),
        new(Mat.Acid, Mat.Basalt, .008f, Mat.CO2, Mat.CO2, "Acid eats basalt slowly."),
        new(Mat.Acid, Mat.Obsidian, .004f, Mat.CO2, Mat.CO2, "Acid barely marks obsidian."),
        new(Mat.Acid, Mat.Ore, .05f, Mat.Hydrogen, Mat.Empty, "Acid on iron ore gives off hydrogen."),
        new(Mat.Acid, Mat.Copper, .05f, Mat.Hydrogen, Mat.Empty, "Acid on copper ore gives off hydrogen."),
        new(Mat.Acid, Mat.Metal, .03f, Mat.Hydrogen, Mat.Empty, "Acid eats plate metal, giving off hydrogen."),
        new(Mat.Acid, Mat.Mycelium, .05f, K, Mat.Empty, "Acid dissolves grey lace."),
        new(Mat.Acid, Mat.Moss, .05f, K, Mat.Empty, "Acid dissolves moss."),
        new(Mat.Acid, Mat.Fungus, .05f, K, Mat.Empty, "Acid dissolves fungus."),
        new(Mat.Acid, Mat.Root, .03f, K, Mat.Empty, "Acid dissolves roots."),
        new(Mat.Acid, Mat.Wood, .02f, K, Mat.Empty, "Acid eats timber."),
        new(Mat.Acid, Mat.Water, .002f, Mat.Water, K, "Acid diluted by water becomes water."),

        // Life
        new(Mat.Mycelium, Mat.Salt, .01f, Mat.Ash, K, "Salt kills grey lace."),
        new(Mat.Slime, Mat.Salt, .2f, Mat.Water, K, "Salt kills slime, leaving water."),
        new(Mat.Slime, Mat.Moss, .02f, K, Mat.Slime, "Slime digests moss."),
        new(Mat.Slime, Mat.Mycelium, .02f, K, Mat.Slime, "Slime digests grey lace."),
        new(Mat.Slime, Mat.Fungus, .02f, K, Mat.Slime, "Slime digests fungus."),
        new(Mat.Slime, Mat.Root, .01f, K, Mat.Slime, "Slime digests roots."),
        new(Mat.Slime, Mat.Glowworm, .05f, K, Mat.Slime, "Slime eats glow worms."),
        new(Mat.Glowworm, Mat.Smoke, .1f, Mat.Empty, K, "Glow worms die in smoke."),
        new(Mat.Glowworm, Mat.FoulGas, .1f, Mat.Empty, K, "Glow worms die in foul air."),
        new(Mat.Glowworm, Mat.CO2, .02f, Mat.Empty, K, "Glow worms suffocate in CO2."),
        new(Mat.Glowworm, Mat.Fire, .5f, Mat.Ash, K, "Fire kills glow worms."),

        // Vapours settling
        new(Mat.MercuryVapour, Mat.Rock, .003f, Mat.Quicksilver, K, "Mercury vapour condenses on cold rock."),

        // Strange matter: cold
        new(Mat.Cryo, Mat.Water, .3f, K, Mat.Ice, "Cryo freezes water solid."),
        new(Mat.Cryo, Mat.Lava, 1f, Mat.Steam, Mat.Obsidian, "Cryo flash-quenches lava into obsidian."),
        new(Mat.Cryo, Mat.Fire, .8f, K, Mat.Smoke, "Cryo snuffs fire."),
        new(Mat.Cryo, Mat.Methane, .2f, K, Mat.Coldflame, "Cryo touching methane lights it with a cold blue fire."),
        new(Mat.Cryo, Mat.Brine, .1f, Mat.Ice, Mat.Salt, "Cryo freezes brine, splitting it into ice and salt."),
        new(Mat.Cryo, Mat.Quicksilver, .1f, K, Mat.Metal, "Cryo freezes quicksilver into a lump of metal."),
        new(Mat.Cryo, Mat.Sunstone, .3f, Mat.Steam, Mat.Rock, "Cryo kills sunstone's heat, leaving ordinary rock."),
        new(Mat.Cryo, Mat.Mite, .05f, K, Mat.Metal, "Cryo freezes mites dead into plate metal."),
        new(Mat.Coldflame, Mat.Fire, 1f, Mat.Steam, Mat.Steam, "Cold fire and real fire cancel out in a burst of steam."),
        new(Mat.Coldflame, Mat.Water, .3f, K, Mat.Ice, "Cold fire freezes water."),
        new(Mat.Coldflame, Mat.Lava, .3f, K, Mat.Obsidian, "Cold fire freezes lava."),
        new(Mat.Snow, Mat.Fire, .5f, Mat.Water, Mat.Steam, "Snow melts in fire."),
        new(Mat.Snow, Mat.Lava, 1f, Mat.Steam, Mat.Basalt, "Snow on lava steams and crusts it."),
        new(Mat.Snow, Mat.Ember, .2f, Mat.Water, Mat.Ash, "Snow puts out embers."),
        new(Mat.Snow, Mat.Steam, .1f, Mat.Water, Mat.Water, "Steam melts snow."),
        new(Mat.Snow, Mat.Sunstone, .1f, Mat.Water, K, "Sunstone melts snow."),

        // Strange matter: heat that never ends
        new(Mat.Water, Mat.Sunstone, .05f, Mat.Steam, K, "Sunstone boils water away, endlessly."),
        new(Mat.Ice, Mat.Sunstone, .1f, Mat.Water, K, "Sunstone melts ice."),
        new(Mat.Mud, Mat.Sunstone, .05f, Mat.Dirt, K, "Sunstone bakes mud dry."),
        new(Mat.Quicksilver, Mat.Sunstone, .02f, Mat.MercuryVapour, K, "Sunstone boils quicksilver into vapour."),

        // Strange matter: light, dust and seeping water
        new(Mat.Glowworm, Mat.Spore, .05f, K, Mat.Glimmer, "Glow worms eat spores and puff out glimmer."),
        new(Mat.Water, Mat.Skydust, .05f, Mat.Empty, Mat.Mud, "Water soaks skydust into ordinary mud, which falls."),
        new(Mat.Ghostwater, Mat.Water, .005f, Mat.Water, K, "Ghostwater mixed with water becomes plain water."),

        // Strange matter: machines and holes
        new(Mat.Mite, Mat.Metal, .03f, K, Mat.Mite, "Mites eat plate metal and multiply."),
        new(Mat.Mite, Mat.Ore, .02f, K, Mat.Mite, "Mites eat iron ore and multiply."),
        new(Mat.Mite, Mat.Copper, .02f, K, Mat.Mite, "Mites eat copper ore and multiply."),
        new(Mat.Mite, Mat.Lava, .3f, Mat.Metal, K, "Lava melts mites down into plate metal."),
        new(Mat.Mite, Mat.Acid, .1f, Mat.Hydrogen, Mat.Empty, "Acid dissolves mites, giving off hydrogen."),
        new(Mat.Void, Mat.Sunstone, 1f, Mat.Empty, Mat.Empty, "Void and sunstone annihilate each other."),
    ];

    static readonly Reaction[][] _byPair = Build();

    static Reaction[][] Build()
    {
        var lists = new List<Reaction>[Mat.Count * Mat.Count];
        foreach (var r in All)
        {
            Add(lists, r.A, r.B, r);
            if (r.A != r.B) Add(lists, r.B, r.A, new Reaction(r.B, r.A, r.Chance, r.BInto, r.AInto, r.Why));
        }
        var result = new Reaction[Mat.Count * Mat.Count][];
        for (int i = 0; i < result.Length; i++) result[i] = lists[i]?.ToArray() ?? [];
        return result;
    }

    static void Add(List<Reaction>?[] lists, byte a, byte b, Reaction r) => (lists[a * Mat.Count + b] ??= []).Add(r);

    /// <summary>Rules for a cell of material <paramref name="a"/> next to <paramref name="b"/>, written from a's side.</summary>
    public static Reaction[] For(byte a, byte b) => _byPair[a * Mat.Count + b];

    public static bool HasAny(byte m)
    {
        foreach (var r in All) if (r.A == m || r.B == m) return true;
        return false;
    }
}
