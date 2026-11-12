namespace Strata.Sim;

/// <summary>
/// Material ids. 0–15 match the reference sim in mockups/strata-mockups.html (plus ore); the rest were added for
/// the caving pass. Ids are stable: saves, the network protocol and tests refer to them.
/// </summary>
public static class Mat
{
    public const byte Empty = 0, Rock = 1, Sand = 2, Water = 3, Methane = 4, Fire = 5, Steam = 6, Lava = 7,
        Acid = 8, Mycelium = 9, Spore = 10, Moss = 11, Obsidian = 12, Smoke = 13, CO2 = 14, Ore = 15;

    // Solids
    public const byte Bedrock = 16, Granite = 17, Shale = 18, Limestone = 19, Basalt = 20, Coal = 21, Sulfur = 22,
        Salt = 23, Ice = 24, Crystal = 25, Clay = 26, Copper = 27;
    // Powders
    public const byte Gravel = 28, Dirt = 29, Ash = 30, CoalDust = 31, Shards = 32, Ember = 33;
    // Liquids
    public const byte Brine = 34, Mud = 35, Oil = 36, Tar = 37, Quicksilver = 38, Slime = 39;
    // Gases
    public const byte Hydrogen = 40, FoulGas = 41, MercuryVapour = 42;
    // Living and built
    public const byte Fungus = 43, Root = 44, Glowworm = 45, Wood = 46, Metal = 47;
    // Energy
    public const byte Blast = 48;
    // Strange matter
    public const byte Cryo = 49, Snow = 50, Coldflame = 51, Sunstone = 52, Skydust = 53, Glimmer = 54, Mite = 55,
        Ghostwater = 56, Void = 57, Resonite = 58;

    public const int Count = 59;
}

public enum Phase : byte { Gas, Liquid, Powder, Solid }

/// <summary>Everything the simulation and the game need to know about one material.</summary>
public sealed record MaterialDef
{
    public required string Name;
    public required uint Color;            // 0xRRGGBB
    public required Phase Phase;
    public float Density = 1;              // heavier displaces lighter (air is 1)
    public float Viscosity;                // liquids, powders, gases: chance per tick of not moving at all
    public int Dispersion = 4;             // liquids: how far sideways they spread per tick
    public float Slide = 1;                // powders: chance of sliding diagonally (low = steep piles)
    public float Rise = .5f;               // gases: chance per tick of trying to move
    public float DecayChance;              // per tick, chance of losing one point of life
    public byte DecayInto;                 // what it becomes when life runs out
    public byte DefaultLife;
    public float Flammability;             // chance per tick of catching from adjacent fire, embers or lava
    public byte BurnsInto = Mat.Fire;      // what it becomes when it catches
    public byte FireLife = 40;             // life of the fire (or ember) it turns into
    public byte BurnByproduct;             // released into an empty neighbour when it catches (smoke, spores, gas)
    public float Emission;                 // light it gives off, 0..1
    public bool Grippable;                 // can be climbed
    public float Hardness = 1;             // digging time multiplier; 0 means it cannot be dug
    public float Suffocation;              // air drain multiplier when breathed (0 = harmless)
    public bool Hot;                       // melts ice, dries mud, boils quicksilver
    public bool Organic;                   // living matter: slime eats it, salt kills grey lace
    public bool AlwaysActive;              // must be visited every tick even when nothing moved near it
    public bool Strange;                   // sci-fi matter: behaves by the same rules, mostly, with a few surprises
    public bool Cold;                      // freezes water, quenches lava and fire
    public string Notes = "";
}

/// <summary>
/// Material properties as flat arrays indexed by id, so the hot loop does no object lookups.
/// <see cref="Defs"/> is the readable source; the arrays are filled from it.
/// </summary>
public static class MaterialTable
{
    public static readonly MaterialDef[] Defs = BuildDefs();

    public static readonly string[] Name = Map(d => d.Name);
    public static readonly uint[] Color = Map(d => d.Color);
    public static readonly Phase[] PhaseOf = Map(d => d.Phase);
    public static readonly float[] Density = Map(d => d.Density);
    public static readonly float[] Viscosity = Map(d => d.Viscosity);
    public static readonly int[] Dispersion = Map(d => d.Dispersion);
    public static readonly float[] Slide = Map(d => d.Slide);
    public static readonly float[] Rise = Map(d => d.Rise);
    public static readonly float[] DecayChance = Map(d => d.DecayChance);
    public static readonly byte[] DecayInto = Map(d => d.DecayInto);
    public static readonly byte[] DefaultLife = Map(d => d.DefaultLife);
    public static readonly float[] Flammability = Map(d => d.Flammability);
    public static readonly byte[] BurnsInto = Map(d => d.BurnsInto);
    public static readonly byte[] FireLife = Map(d => d.FireLife);
    public static readonly byte[] BurnByproduct = Map(d => d.BurnByproduct);
    public static readonly float[] Emission = Map(d => d.Emission);
    public static readonly bool[] Grippable = Map(d => d.Grippable);
    public static readonly float[] Hardness = Map(d => d.Hardness);
    public static readonly float[] Suffocation = Map(d => d.Suffocation);
    public static readonly bool[] Hot = Map(d => d.Hot);
    public static readonly bool[] Organic = Map(d => d.Organic);
    public static readonly bool[] Cold = Map(d => d.Cold);
    public static readonly bool[] Strange = Map(d => d.Strange);

    /// <summary>Solid cells never move and block movement.</summary>
    public static readonly bool[] Solid = Map(d => d.Phase == Phase.Solid);
    public static readonly bool[] Powder = Map(d => d.Phase == Phase.Powder);
    public static readonly bool[] Liquid = Map(d => d.Phase == Phase.Liquid);
    public static readonly bool[] Gas = Map(d => d.Phase == Phase.Gas);

    /// <summary>Skipped entirely by the update loop unless it has reactions or special behaviour.</summary>
    public static readonly bool[] Inert = new bool[Mat.Count];
    public static readonly bool[] AlwaysActive = Map(d => d.AlwaysActive);

    static MaterialTable()
    {
        for (int m = 0; m < Mat.Count; m++)
            Inert[m] = m == Mat.Empty || (Defs[m].Phase == Phase.Solid && !Defs[m].AlwaysActive && !Reactions.HasAny((byte)m) && !SpecialSolid((byte)m));
    }

    /// <summary>Solids with behaviour of their own (crumbling, growing) that must run when visited.</summary>
    static bool SpecialSolid(byte m) => m is Mat.Shale or Mat.Mycelium or Mat.Moss or Mat.Root or Mat.Fungus or Mat.Glowworm or Mat.Ice
        or Mat.Sunstone or Mat.Void or Mat.Resonite;

    static T[] Map<T>(Func<MaterialDef, T> f) { var a = new T[Mat.Count]; for (int i = 0; i < Mat.Count; i++) a[i] = f(Defs[i]); return a; }

    static MaterialDef[] BuildDefs()
    {
        var d = new MaterialDef[Mat.Count];
        void S(byte id, MaterialDef def) => d[id] = def;

        // ---- The original fifteen, plus ore ----
        S(Mat.Empty, new() { Name = "empty", Color = 0x1E1D1C, Phase = Phase.Gas, Density = 1, Hardness = 0 });
        S(Mat.Rock, new() { Name = "rock", Color = 0x504C48, Phase = Phase.Solid, Grippable = true, Notes = "Acid eats it slowly." });
        S(Mat.Sand, new() { Name = "sand", Color = 0x987E58, Phase = Phase.Powder, Density = 20, Hardness = .5f });
        S(Mat.Water, new() { Name = "water", Color = 0x26587C, Phase = Phase.Liquid, Density = 10, Dispersion = 4, Suffocation = 8, Notes = "Puts out fire, dissolves salt, turns clay and dirt to mud, freezes slowly against ice." });
        S(Mat.Methane, new() { Name = "methane", Color = 0x5C4670, Phase = Phase.Gas, Density = .3f, Rise = .35f, Flammability = 1, FireLife = 32, Suffocation = 4, Notes = "Invisible until lit. Burns in a rolling front." });
        S(Mat.Fire, new() { Name = "fire", Color = 0xFFB046, Phase = Phase.Gas, Density = .6f, Rise = .6f, DefaultLife = 40, Emission = 1, Hot = true, Suffocation = 6, AlwaysActive = true });
        S(Mat.Steam, new() { Name = "steam", Color = 0xA8B0B8, Phase = Phase.Gas, Density = .4f, Rise = .5f, DecayChance = .6f, DecayInto = Mat.Water, DefaultLife = 70, Hot = true, Suffocation = 2, AlwaysActive = true, Notes = "Condenses back to water on rock ceilings." });
        S(Mat.Lava, new() { Name = "lava", Color = 0xEC6022, Phase = Phase.Liquid, Density = 15, Viscosity = .66f, Dispersion = 1, Emission = .9f, Hot = true, Hardness = 0, Notes = "Sets things alight, makes obsidian with water and basalt with ice, slowly crusts over." });
        S(Mat.Acid, new() { Name = "acid", Color = 0x80C440, Phase = Phase.Liquid, Density = 11, Dispersion = 3, Suffocation = 6, AlwaysActive = true, Notes = "Eats rock and limestone into CO2, metal and ore into hydrogen." });
        S(Mat.Mycelium, new() { Name = "grey lace", Color = 0xCCC2AC, Phase = Phase.Solid, DefaultLife = 30, Flammability = .05f, FireLife = 40, BurnByproduct = Mat.Spore, Hardness = .5f, Organic = true, Notes = "Creeps along rock and drinks water. Bursts into spores when it burns. Salt kills it." });
        S(Mat.Spore, new() { Name = "spores", Color = 0xD6CC9C, Phase = Phase.Gas, Density = .8f, DefaultLife = 200, Flammability = .3f, FireLife = 8, Suffocation = 3, Organic = true, AlwaysActive = true, Notes = "Drift. Seed new grey lace on wet rock. Flash-burn." });
        S(Mat.Moss, new() { Name = "lantern moss", Color = 0x60E8C0, Phase = Phase.Solid, Flammability = .012f, FireLife = 30, Emission = .55f, Hardness = .4f, Organic = true, Notes = "Glows. Spreads only near water." });
        S(Mat.Obsidian, new() { Name = "obsidian", Color = 0x282230, Phase = Phase.Solid, Grippable = true, Hardness = 4 });
        S(Mat.Smoke, new() { Name = "smoke", Color = 0x3E3E40, Phase = Phase.Gas, Density = .5f, Rise = .5f, DecayChance = 1, DecayInto = Mat.Empty, DefaultLife = 60, Suffocation = 5, AlwaysActive = true });
        S(Mat.CO2, new() { Name = "CO2", Color = 0x4A604E, Phase = Phase.Gas, Density = 1.6f, Rise = .6f, DecayChance = .12f, DecayInto = Mat.Empty, DefaultLife = 220, Suffocation = 6, Notes = "Heavier than air. Pools in hollows. You can't see it." });
        S(Mat.Ore, new() { Name = "iron ore", Color = 0x9A5E3A, Phase = Phase.Solid, Grippable = true, Hardness = 1.2f, Notes = "Throws sparks when struck. Acid turns it into hydrogen." });

        // ---- Solids ----
        S(Mat.Bedrock, new() { Name = "bedrock", Color = 0x1A1818, Phase = Phase.Solid, Grippable = true, Hardness = 0, Notes = "Cannot be dug." });
        S(Mat.Granite, new() { Name = "granite", Color = 0x6E6660, Phase = Phase.Solid, Grippable = true, Hardness = 3, Notes = "Slow to dig, solid to stand under." });
        S(Mat.Shale, new() { Name = "shale", Color = 0x46484E, Phase = Phase.Solid, Grippable = true, Hardness = .6f, Notes = "Brittle. Once nothing holds it up it trickles grit, then gives way as gravel." });
        S(Mat.Limestone, new() { Name = "limestone", Color = 0x8E8A7C, Phase = Phase.Solid, Grippable = true, Hardness = .8f, Notes = "Acid eats it fast and gives off CO2." });
        S(Mat.Basalt, new() { Name = "basalt", Color = 0x343436, Phase = Phase.Solid, Grippable = true, Hardness = 2, Notes = "Cooled lava." });
        S(Mat.Coal, new() { Name = "coal", Color = 0x232122, Phase = Phase.Solid, Grippable = true, Flammability = .02f, BurnsInto = Mat.Ember, FireLife = 250, BurnByproduct = Mat.Smoke, Hardness = .8f, Notes = "Smoulders into embers for a long time. Digging it raises explosive coal dust." });
        S(Mat.Sulfur, new() { Name = "sulfur", Color = 0xC8B43A, Phase = Phase.Solid, Flammability = .05f, FireLife = 60, BurnByproduct = Mat.FoulGas, Hardness = .7f, Notes = "Burns with a choking gas." });
        S(Mat.Salt, new() { Name = "salt", Color = 0xD8D2CA, Phase = Phase.Solid, Grippable = true, Hardness = .8f, Notes = "Water dissolves it into brine. Kills grey lace and slime." });
        S(Mat.Ice, new() { Name = "ice", Color = 0xA8D4E8, Phase = Phase.Solid, Hardness = .7f, Notes = "Melts near anything hot. Slowly freezes the water touching it. Slippery: can't be gripped." });
        S(Mat.Crystal, new() { Name = "crystal", Color = 0x9FE0D8, Phase = Phase.Solid, Grippable = true, Emission = .22f, Hardness = 1.5f, Notes = "Glows faintly. Shatters into shards. Grows slowly in brine." });
        S(Mat.Clay, new() { Name = "clay", Color = 0x8A6A52, Phase = Phase.Solid, Grippable = true, Hardness = .8f, Notes = "Soaks up water and slumps into mud." });
        S(Mat.Copper, new() { Name = "copper ore", Color = 0x3E8A72, Phase = Phase.Solid, Grippable = true, Hardness = 1.2f, Notes = "Acid turns it into hydrogen." });

        // ---- Powders ----
        S(Mat.Gravel, new() { Name = "gravel", Color = 0x5E5A56, Phase = Phase.Powder, Density = 30, Slide = .35f, Hardness = .5f, Notes = "Heavy and steep. What collapsing shale becomes." });
        S(Mat.Dirt, new() { Name = "dirt", Color = 0x5A4636, Phase = Phase.Powder, Density = 18, Slide = .6f, Hardness = .4f, Notes = "Turns to mud with water." });
        S(Mat.Ash, new() { Name = "ash", Color = 0x7C7872, Phase = Phase.Powder, Density = 4, Viscosity = .4f, Slide = .8f, Hardness = .2f, Notes = "Light; floats on water and smothers flames." });
        S(Mat.CoalDust, new() { Name = "coal dust", Color = 0x2E2B2A, Phase = Phase.Powder, Density = 6, Viscosity = .5f, Slide = .9f, Flammability = 1, BurnsInto = Mat.Blast, Hardness = .2f, Suffocation = 2, Notes = "Explodes when lit." });
        S(Mat.Shards, new() { Name = "crystal shards", Color = 0xB8ECE4, Phase = Phase.Powder, Density = 22, Slide = .8f, Emission = .08f, Hardness = .3f });
        S(Mat.Ember, new() { Name = "embers", Color = 0xD8501E, Phase = Phase.Powder, Density = 8, Slide = .7f, DecayChance = .5f, DecayInto = Mat.Ash, DefaultLife = 200, Emission = .7f, Hot = true, AlwaysActive = true, Notes = "Glowing coals. Set flammable things alight. Burn down to ash." });

        // ---- Liquids ----
        S(Mat.Brine, new() { Name = "brine", Color = 0x2E6F7A, Phase = Phase.Liquid, Density = 11.5f, Dispersion = 3, Suffocation = 8, Notes = "Salty water. Sinks under fresh water, never freezes, grows crystals, leaves salt when boiled." });
        S(Mat.Mud, new() { Name = "mud", Color = 0x4A3828, Phase = Phase.Liquid, Density = 13, Viscosity = .6f, Dispersion = 1, Suffocation = 9, Notes = "Thick and slow. Dries back to dirt in heat." });
        S(Mat.Oil, new() { Name = "oil", Color = 0x2A2418, Phase = Phase.Liquid, Density = 8, Dispersion = 5, Flammability = .6f, FireLife = 30, BurnByproduct = Mat.Smoke, Suffocation = 8, Notes = "Floats on water. Burns fast and smoky." });
        S(Mat.Tar, new() { Name = "tar", Color = 0x141012, Phase = Phase.Liquid, Density = 12.5f, Viscosity = .9f, Dispersion = 1, Flammability = .05f, FireLife = 140, BurnByproduct = Mat.Smoke, Suffocation = 9, Notes = "Barely moves. Burns for a long time." });
        S(Mat.Quicksilver, new() { Name = "quicksilver", Color = 0xB8BCC4, Phase = Phase.Liquid, Density = 40, Viscosity = .2f, Dispersion = 3, Emission = .04f, Suffocation = 8, Notes = "Very heavy. Gives off poisonous vapour when heated." });
        S(Mat.Slime, new() { Name = "slime", Color = 0x7AA43C, Phase = Phase.Liquid, Density = 12, Viscosity = .7f, Dispersion = 1, Flammability = .02f, BurnsInto = Mat.Ash, Emission = .05f, Suffocation = 9, Organic = true, AlwaysActive = true, Notes = "Creeps and digests anything living it touches. Salt kills it." });

        // ---- Gases ----
        S(Mat.Hydrogen, new() { Name = "hydrogen", Color = 0x6A7888, Phase = Phase.Gas, Density = .1f, Rise = .8f, Flammability = 1, BurnsInto = Mat.Blast, Suffocation = 3, Notes = "Rises fast, gathers under ceilings, explodes when lit." });
        S(Mat.FoulGas, new() { Name = "foul air", Color = 0x6E6A3A, Phase = Phase.Gas, Density = 1.4f, Rise = .5f, DecayChance = .05f, DecayInto = Mat.Empty, DefaultLife = 220, Suffocation = 12, Notes = "Rotten-egg gas. Heavy and poisonous. Water absorbs it and turns to acid." });
        S(Mat.MercuryVapour, new() { Name = "mercury vapour", Color = 0x8A8E96, Phase = Phase.Gas, Density = 2.2f, Rise = .5f, DecayChance = .08f, DecayInto = Mat.Quicksilver, DefaultLife = 200, Suffocation = 14, Notes = "Very poisonous. Settles back out as quicksilver." });

        // ---- Living and built ----
        S(Mat.Fungus, new() { Name = "cap fungus", Color = 0xB07A6A, Phase = Phase.Solid, Flammability = .04f, FireLife = 40, BurnByproduct = Mat.Spore, Hardness = .4f, Organic = true, Notes = "Giant mushroom flesh. Sheds spores. Burns." });
        S(Mat.Root, new() { Name = "roots", Color = 0x6E5236, Phase = Phase.Solid, Grippable = true, DefaultLife = 25, Flammability = .03f, BurnsInto = Mat.Ember, FireLife = 120, Hardness = .6f, Organic = true, Notes = "Hang from the soil and grow down towards water. Good to climb." });
        S(Mat.Glowworm, new() { Name = "glow worms", Color = 0x7AD8F0, Phase = Phase.Solid, Emission = .6f, Hardness = .2f, Organic = true, Notes = "Colonies on ceilings. Light the way. Die in smoke and bad air." });
        S(Mat.Wood, new() { Name = "timber", Color = 0x7A5A3C, Phase = Phase.Solid, Grippable = true, Flammability = .03f, BurnsInto = Mat.Ember, FireLife = 150, BurnByproduct = Mat.Smoke, Hardness = 1, Notes = "Built supports and walls." });
        S(Mat.Metal, new() { Name = "plate metal", Color = 0x8A9098, Phase = Phase.Solid, Grippable = true, Hardness = 5, Notes = "Built walls. Acid turns it into hydrogen." });

        // ---- Energy ----
        S(Mat.Blast, new() { Name = "blast", Color = 0xFFF0C0, Phase = Phase.Gas, Density = .6f, DefaultLife = 2, Emission = 1, Hot = true, Hardness = 0, AlwaysActive = true, Notes = "An explosion front. Breaks weak rock, sets off other explosives." });

        // ---- Strange matter ----
        S(Mat.Cryo, new() { Name = "cryo", Color = 0x9CE6FF, Phase = Phase.Liquid, Density = 9, Dispersion = 4, DecayChance = .01f, DecayInto = Mat.Snow, DefaultLife = 160, Emission = .12f, Cold = true, Suffocation = 8, AlwaysActive = true, Strange = true, Notes = "A liquid colder than anything natural. Freezes water solid, quenches lava and fire, boils off into snow. With methane it makes cold fire." });
        S(Mat.Snow, new() { Name = "snow", Color = 0xE8F0F4, Phase = Phase.Powder, Density = 5, Viscosity = .3f, Slide = .5f, Cold = true, Hardness = .2f, Strange = true, Notes = "Fluffy and light. Melts near anything hot." });
        S(Mat.Coldflame, new() { Name = "cold fire", Color = 0x6FB8FF, Phase = Phase.Gas, Density = .6f, Rise = .5f, DefaultLife = 50, Emission = .85f, Cold = true, Suffocation = 4, AlwaysActive = true, Strange = true, Notes = "A blue flame that burns by freezing. Spreads through methane and hydrogen like fire, but turns water and lava solid instead of boiling them. Meeting real fire, both go out in a burst of steam." });
        S(Mat.Sunstone, new() { Name = "sunstone", Color = 0xF0A040, Phase = Phase.Solid, Grippable = true, Emission = .5f, Hot = true, Hardness = 1.5f, Strange = true, Notes = "A stone that never cools. Boils water into steam forever, melts ice and snow, and slowly sets flammable things alight. Cryo kills its heat." });
        S(Mat.Skydust, new() { Name = "skydust", Color = 0xA48CE0, Phase = Phase.Powder, Density = 3, Slide = .7f, Flammability = .1f, BurnsInto = Mat.Glimmer, Emission = .1f, Hardness = .2f, Strange = true, Notes = "Falls up. Piles in dunes on the ceiling. Water weighs it down into mud; fire turns it into glimmer." });
        S(Mat.Glimmer, new() { Name = "glimmer", Color = 0xF4E6A0, Phase = Phase.Gas, Density = .9f, DecayChance = .25f, DecayInto = Mat.Empty, DefaultLife = 160, Emission = .7f, AlwaysActive = true, Strange = true, Notes = "Drifting motes of light. Glow worms puff it out when they eat spores. Harmless." });
        S(Mat.Mite, new() { Name = "mites", Color = 0x3A4A66, Phase = Phase.Liquid, Density = 13, Viscosity = .6f, Dispersion = 1, DecayChance = .002f, DecayInto = Mat.Metal, DefaultLife = 120, Emission = .06f, Suffocation = 9, AlwaysActive = true, Strange = true, Notes = "A creeping paste of tiny machines. Eats metal and ore to make more of itself, and dies back into plate metal when it starves. Lava melts it down, acid dissolves it, cryo stops it." });
        S(Mat.Ghostwater, new() { Name = "ghostwater", Color = 0x7FD8D0, Phase = Phase.Liquid, Density = 9.5f, Dispersion = 3, Emission = .25f, Suffocation = 8, Strange = true, Notes = "Water that seeps straight through solid rock to the next open space below. Bedrock and crystal stop it. Mixing with ordinary water, it loses the trick." });
        S(Mat.Void, new() { Name = "void", Color = 0x0A0610, Phase = Phase.Solid, DefaultLife = 80, Hardness = 0, AlwaysActive = true, Strange = true, Notes = "A hole in the world. Eats whatever it touches, a little at a time, until it is spent. Crystal, bedrock and obsidian hold it. Sunstone and void annihilate." });
        S(Mat.Resonite, new() { Name = "resonite", Color = 0x3FB8A8, Phase = Phase.Solid, Grippable = true, Emission = .15f, Hardness = 1.2f, Strange = true, Notes = "A ringing mineral. A blast nearby makes a whole vein of it go off in sympathy, carrying the explosion far through the rock." });

        for (int i = 0; i < Mat.Count; i++) if (d[i] == null) throw new InvalidOperationException($"Material {i} has no definition");
        return d;
    }
}
