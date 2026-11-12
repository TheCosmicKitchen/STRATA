# STRATA (working title)

A 2D co-op expedition game for 2–4 friends, made in Godot 4. You crew a huge tracked machine, the Crawler, across a dead surface that is generated for each world. Then you anchor it and climb down into an earth run by a falling-sand particle simulation, in the spirit of Noita. The goal is Barotrauma's isolation and shared-vehicle tension without the slapstick. Life is fragile, the crew splits between the machine and the dark, and fungi have hidden traits that you learn by experimenting.

## Where things are

| What | Where |
| --- | --- |
| Living design doc (edit and comment there) | [STRATA — Game Design Notes](https://claude.ai/artifact/K4oP3NdUav4ST6iTKnvCZp) |
| Published interactive mockups | [STRATA mockups](https://claude.ai/artifact/LDVDo9uA4VjxFwn4pSPDCR) |
| Mockups source (open in a browser) | [mockups/strata-mockups.html](mockups/strata-mockups.html) |
| Design summary for working in this repo | [docs/design-summary.md](docs/design-summary.md) |
| Godot learning roadmap (small games in varied genres) and syllabus | [docs/learning-roadmap.md](docs/learning-roadmap.md), [docs/godot-syllabus.md](docs/godot-syllabus.md), games in [learn/](learn/) |
| Prototype specs (playable diagrams and experiments, for delegation) | [docs/specs/prototypes.md](docs/specs/prototypes.md) |
| Ash-can alpha plan (end-to-end fun tests) | [docs/specs/alphas.md](docs/specs/alphas.md) |
| Networking spec (safe peer-to-peer) | [docs/specs/networking.md](docs/specs/networking.md) |
| Playable prototypes (HTML) and the loop animation | [mockups/prototypes/](mockups/prototypes/), [mockups/loop-animation.html](mockups/loop-animation.html) |
| Godot game (C#, Godot 4.7.2 .NET) | [game/](game/) |
| Materials manual (readable, searchable; published) | [STRATA Materials Manual](https://claude.ai/artifact/EGjgQEydntnEQvewRE7sue), source [docs/manual/](docs/manual/) (rebuild with `node docs/manual/build.js`) |
| Materials and interactions (generated from the code) | [docs/specs/materials.md](docs/specs/materials.md) |
| Simulation core (plain C#) and its tests | [src/Strata.Sim](src/Strata.Sim), [tests/Strata.Sim.Tests](tests/Strata.Sim.Tests) |

The design doc is the source of truth. `docs/design-summary.md` is a short offline copy of the decisions so far.

## Play the MVP

Needs the Godot 4.7.2 **.NET** editor and the .NET 10 SDK.

1. Build once: `dotnet build game/Strata.Game.csproj`
2. Run: open `game/project.godot` in Godot .NET and press Play, or from a terminal run the .NET Godot executable with `--path game`.

You start in the starter base, a granite chamber about 50 m down with a shaft up to the surface (the truck is parked up top; surface travel comes later). The earth is in depth zones: soil with hanging roots; limestone with grottos, glow-worm halls, salt caves, ice pockets and crystal geodes; a middle band of rock and shale with coal galleries full of gas, grey-lace forests, ore chambers and oil seeps; and deep basalt with lava tubes, sulfur vents, quicksilver pools and hydrogen pockets.

| Action | Keyboard and mouse | Controller |
| --- | --- | --- |
| Move, or drive | `A` / `D` | Left stick or d-pad |
| Creep the truck (parking) | `W` / `S` | Stick up / down |
| Reel the line in / out, swim | `W` / `S` | Left stick or d-pad up / down |
| Aim lamp, digging and flares | Mouse | Right stick |
| Jump | `Space` | A |
| Grab a rock wall | Move into it in the air | Stick into it |
| Climb / kick off / let go | `W`/`S` · `Space` · move away | Stick up/down · A · stick away |
| Clip on / off the line | `C` | B |
| Use: get in or out of the truck, forge, bench, winch, sample | `E` | X |
| Lamp on / off | `F` | Y |
| Dig, or strike the bench | Left mouse | RT |
| Spray the chosen material (any of 35, unlimited for now) | Right mouse | LT |
| Choose what to spray | Mouse wheel, `Z` / `X` | LB / RB |
| Throw a flare | `Q` | R3 (click the right stick) |
| Choose what you hold (crafting) | `1`–`7`, `Tab` | – |
| Pause / restart / new world | `P` / `R` / `N` | Start / Back / – |

For now air is switched off and injuries are forgiving: lava burns before it kills, you get back up a few seconds after being downed, and resting at the base mends you. Press `N` for a freshly generated cave.

Along the way: digging fills your pack (rock gives stone, sand gives sand, ore gives ore, grey lace gives fibre). At the base the generator charges your lamp and the truck refuels when parked by the shed. Craft by hand: put things in the forge with `E`, or lay them on the bench with `E` and strike it.

<details>
<summary>Recipes (spoilers; the game never shows these)</summary>

- Forge: ore → metal, sand → glass. Fibre burns. Taking something out while it glows burns your hand.
- Bench: 3 fibre → rope (splice it onto the winch with `E` for +36 m of line), 2 metal → pick (digs faster and wider), glass + fibre → 2 flares. Anything else is junk.
</details>

## Tools for working on the caves

Run these with the Godot .NET executable (`godot` below).

- **Materials sandbox:** `godot --path game sandbox.tscn`. Paint any of the 48 materials and watch them interact: left mouse paint, right mouse erase, wheel or `Q`/`E` choose material, `[` `]` brush size, `Space` pause, `C` clear, `G` load the reaction gallery, `1`–`5` speed.
- **Reaction gallery:** `godot --path game sandbox.tscn -- --gallery=<folder>` lays out every reaction, every flammable material and a few set pieces in labelled boxes, and saves screenshots at 0.5, 3, 10 and 30 seconds.
- **Cave map:** `godot --headless --path game -- --map=<file.png> [--map-steps=N] [--seed=N]` saves the whole generated world, one pixel per cell, after N simulated steps, and prints the chambers and material counts.
- **Materials doc:** `dotnet run --project tools/Strata.Sim.Bench -- --materials-doc docs/specs/materials.md`.
- **Tests and benchmark:** `dotnet test` (every reaction and every flammable material is tested automatically) and `dotnet run -c Release --project tools/Strata.Sim.Bench`.
