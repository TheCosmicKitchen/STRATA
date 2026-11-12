using System.Diagnostics;
using Strata.Sim;

// Usage: dotnet run -c Release --project tools/Strata.Sim.Bench [chunksX] [chunksY] [steps]
//        dotnet run --project tools/Strata.Sim.Bench -- --materials-doc docs/specs/materials.md
if (args.Length == 2 && args[0] == "--materials-doc") { File.WriteAllText(args[1], MaterialsDoc()); Console.WriteLine($"wrote {args[1]}"); return; }

if (args.Length == 2 && args[0] == "--materials-json") { File.WriteAllText(args[1], MaterialsJson()); Console.WriteLine($"wrote {args[1]}"); return; }
if (args.Length == 1 && args[0] == "--awake-report") { AwakeReport(); return; }

int cx = args.Length > 0 ? int.Parse(args[0]) : 16;
int cy = args.Length > 1 ? int.Parse(args[1]) : 12;
int steps = args.Length > 2 ? int.Parse(args[2]) : 600;

Run("busy cave (everything awake)", Busy);
Run("settled cave with one flood", Flood);

void Run(string name, Func<World> make)
{
    var w = make();
    for (int i = 0; i < 30; i++) w.Step();
    long visited = 0; int awake = 0;
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < steps; i++) { w.Step(); visited += w.CellsVisitedLastStep; awake += w.AwakeChunksLastStep; }
    sw.Stop();
    double ms = sw.Elapsed.TotalMilliseconds / steps;
    Console.WriteLine($"{name,-32} {w.Width}x{w.Height}: {ms,6:F2} ms/step, {visited / steps,8} cells/step " +
                      $"({100.0 * visited / steps / (w.Width * w.Height),5:F1}%), {awake / (double)steps,5:F1}/{cx * cy} chunks awake");
}

World Busy()
{
    var w = new World(cx, cy, 1);
    var r = new Rng(1);
    for (int y = 0; y < w.Height; y++)
    for (int x = 0; x < w.Width; x++)
    {
        float f = r.Float();
        byte m = f < .2f ? Mat.Rock : f < .45f ? Mat.Sand : f < .65f ? Mat.Water : f < .7f ? Mat.Methane : Mat.Empty;
        w.Set(x, y, m, World.DefaultLife(m));
    }
    return w;
}

World Flood()
{
    var w = new World(cx, cy, 2);
    var r = new Rng(2);
    for (int y = 0; y < w.Height; y++)
    for (int x = 0; x < w.Width; x++)
    {
        double n = Math.Sin(x * .05) * Math.Cos(y * .07) + Math.Sin((x + y) * .02);
        w.Set(x, y, n > .2 ? Mat.Rock : Mat.Empty);
    }
    for (int i = 0; i < 400; i++) w.Step();
    w.Paint(w.Width / 2, 20, 30, Mat.Water, onlyIntoEmpty: true);
    return w;
}

static string MaterialsDoc()
{
    var sb = new System.Text.StringBuilder();
    string N(byte m) => m == Reaction.Keep ? "(unchanged)" : MaterialTable.Name[m];
    sb.AppendLine("# Materials and interactions");
    sb.AppendLine();
    sb.AppendLine("Generated from `src/Strata.Sim/Materials.cs` and `Reactions.cs` by `dotnet run --project tools/Strata.Sim.Bench -- --materials-doc docs/specs/materials.md`. Do not edit by hand.");
    sb.AppendLine();
    sb.AppendLine("Every reaction and every flammable material below has an automatic test in `tests/Strata.Sim.Tests/InteractionTests.cs`.");
    foreach (var phase in new[] { Phase.Solid, Phase.Powder, Phase.Liquid, Phase.Gas })
    {
        sb.AppendLine();
        sb.AppendLine($"## {phase}s");
        sb.AppendLine();
        sb.AppendLine("| | Material | Density | Burns | Light | Climb | Dig | Breathing | Notes |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        for (byte m = 1; m < Mat.Count; m++)
        {
            var d = MaterialTable.Defs[m];
            if (d.Phase != phase) continue;
            string burns = d.Flammability > 0 ? (d.BurnsInto == Mat.Blast ? "explodes" : $"→ {N(d.BurnsInto)}{(d.BurnByproduct != 0 ? $" + {N(d.BurnByproduct)}" : "")}") : "";
            string dig = d.Hardness == 0 ? "no" : $"×{d.Hardness:0.#}";
            sb.AppendLine($"| <span style=\"color:#{d.Color:X6}\">■</span> | {d.Name} | {d.Density:0.#} | {burns} | {(d.Emission > 0 ? $"{d.Emission:0.##}" : "")} | {(d.Grippable ? "yes" : "")} | {dig} | {(d.Suffocation > 0 ? $"×{d.Suffocation:0}" : "")} | {d.Notes} |");
        }
    }
    sb.AppendLine();
    sb.AppendLine("## Reactions");
    sb.AppendLine();
    sb.AppendLine("Chance is per tick, per contact, at 60 ticks a second.");
    sb.AppendLine();
    sb.AppendLine("| A | B | Chance | A becomes | B becomes | |");
    sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
    foreach (var r in Reactions.All)
        sb.AppendLine($"| {N(r.A)} | {N(r.B)} | {r.Chance:0.####} | {N(r.AInto)} | {N(r.BInto)} | {r.Why} |");
    sb.AppendLine();
    sb.AppendLine("## Behaviour that isn't a pairwise rule");
    sb.AppendLine();
    sb.AppendLine("- **Fire** spreads to anything flammable by its flammability, turns water to steam, and burns out to smoke.");
    sb.AppendLine("- **Embers** set flammable neighbours alight at half the rate of fire and burn down to ash.");
    sb.AppendLine("- **Explosions** (lit coal dust or hydrogen) break weak solids into gravel, scatter powders, fill open space with fire and smoke, and set off other explosives nearby, so pockets go up in a chain.");
    sb.AppendLine("- **Shale** holds while it sits on something or spans between solid neighbours. Once unsupported it trickles grit for one to four seconds, then gives way as gravel, which leaves its neighbours unsupported in turn.");
    sb.AppendLine("- **Lava** sets flammables alight, sometimes flares, slowly crusts over into basalt where it meets open air, and moves only every third tick.");
    sb.AppendLine("- **Grey lace** creeps over rock, drinks water, sheds spores, and won't grow next to salt. **Spores** drift and seed grey lace on wet rock.");
    sb.AppendLine("- **Lantern moss** spreads only near water. **Roots** grow down towards water from where they hang. **Cap fungus** sheds spores.");
    sb.AppendLine("- **Steam** condenses to water on rock ceilings. Heavy gases (**CO2**, **foul air**, **mercury vapour**) sink and pool; light ones rise.");
    sb.AppendLine("- Still places are swept for slow reactions every 16 ticks, so a quiet pool still dissolves salt.");
    return sb.ToString();
}


static void AwakeReport()
{
    var w = new World(24, 8, 11);
    Strata.Rules.Underground.Generate(w, 11, settleSteps: 60);
    for (int t = 0; t < 280; t++) w.Step();
    Array.Clear(w.WakesBy);
    for (int t = 0; t < 20; t++) w.Step();
    Console.WriteLine("wake-ups per step by material: " + string.Join(", ", Enumerable.Range(0, Mat.Count).Where(m => w.WakesBy[m] > 0).OrderByDescending(m => w.WakesBy[m]).Select(m => $"{MaterialTable.Name[m]} {w.WakesBy[m] / 20}")));
    var counts = new int[Mat.Count];
    for (int cy = 0; cy < w.ChunksY; cy++) for (int cx = 0; cx < w.ChunksX; cx++)
    {
        if (!w.ChunkAwake(cx, cy, out int x0, out int y0, out int x1, out int y1)) continue;
        for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) { byte m = w.Get(x, y); if (!MaterialTable.Inert[m]) counts[m]++; }
    }
    Console.WriteLine($"awake chunks {w.AwakeChunksLastStep}/{w.ChunksX * w.ChunksY}, visited {w.CellsVisitedLastStep}");
    for (int m = 1; m < Mat.Count; m++) if (counts[m] > 0) Console.WriteLine($"  {MaterialTable.Name[m],-16} {counts[m]}");
}


static string MaterialsJson()
{
    var mats = Enumerable.Range(1, Mat.Count - 1).Select(i =>
    {
        var d = MaterialTable.Defs[i];
        return new Dictionary<string, object?>
        {
            ["id"] = i, ["name"] = d.Name, ["color"] = $"#{d.Color:X6}", ["phase"] = d.Phase.ToString(), ["density"] = d.Density,
            ["viscosity"] = d.Viscosity, ["flammability"] = d.Flammability, ["burnsInto"] = d.Flammability > 0 ? MaterialTable.Name[d.BurnsInto] : null,
            ["byproduct"] = d.BurnByproduct != 0 ? MaterialTable.Name[d.BurnByproduct] : null, ["emission"] = d.Emission, ["grippable"] = d.Grippable,
            ["hardness"] = d.Hardness, ["suffocation"] = d.Suffocation, ["hot"] = d.Hot, ["organic"] = d.Organic, ["cold"] = d.Cold, ["strange"] = d.Strange,
            ["decayInto"] = d.DecayChance > 0 ? MaterialTable.Name[d.DecayInto] : null, ["notes"] = d.Notes,
        };
    });
    string N(byte m) => m == Reaction.Keep ? "" : MaterialTable.Name[m];
    var reactions = Reactions.All.Select(r => new Dictionary<string, object> { ["a"] = N(r.A), ["b"] = N(r.B), ["chance"] = r.Chance, ["aInto"] = N(r.AInto), ["bInto"] = N(r.BInto), ["why"] = r.Why });
    return System.Text.Json.JsonSerializer.Serialize(new { materials = mats, reactions });
}
