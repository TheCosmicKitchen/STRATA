# Learning roadmap: small Godot games

Status: proposed 12 Nov 2026, revised the same day for more variety.

## Why this exists

The caving MVP in [game/](../game/) works, but it barely uses Godot. One `Game.cs` draws the whole world into a single image, which skips most of the engine. This roadmap is a run of small games in **different genres**, each one finished and fun on its own. They aren't parts of STRATA. Each one teaches a few Godot systems well, and most of them also try out something STRATA will need later (a feel, a system, or an open design question).

Each game:

- **is finished end to end**: a title, a goal, a win or a loss, a restart, and a Windows build you could send to a friend.
- **takes one or two evenings.** If one starts to run long, cut scope.
- **lives in its own Godot .NET project** at `learn/NN-name/`, all in C#, so a 3D game or an odd genre never tangles with another.

## How we split the work

| Step | Who | What |
| --- | --- | --- |
| **Setup** | Claude (agentic) | Creates the project, Input Map, scene tree, placeholder art, title screen, win/lose/restart loop and export preset. Writes empty methods marked `// YOU:` with a short hint each, and a `README.md` with the goal, the Godot docs pages to read and a "stuck?" list. Checks that it builds and runs. |
| **Core** | You (hands-on) | Write the mechanic that makes the game fun: the part that teaches the Godot system. You do it in the editor and the code yourself. Claude answers questions and explains, and writes code only when you ask. |
| **Polish** | Claude, with you | Adds feel (screen shake, tweens, particles, sound), fixes bugs, tunes numbers, and makes the build. Then a short note in the README: what was fun, what you learned, and anything to carry into STRATA. |

Setup should never write the core. If the `// YOU:` parts are trivial, the game is too small; if they take more than an evening, setup should do more.

## The games

### 1. Touchdown: land it from inside ([learn/01-touchdown](../learn/01-touchdown/))
**Play:** a lander shown in cross-section, and you're the small person in the cabin. The left and right thruster levers are at opposite ends, with the engine block in between. Set one, run and jump to the other, and land on each pad in turn. Your weight tips the ship, thrusting presses you into the floor, and falling freely makes you float.
**You make:** walking and jumping when "down" keeps changing, the levers, the thrusters, and the landing or crash decision.
**Godot:** the editor and node tree, `CharacterBody2D` and `RigidBody2D` side by side, `Area2D` and signals, `ApplyForce` at an offset, the Input Map, and (already written, to read) a `SubViewport` with its own physics world, a HUD on a `CanvasLayer`, and switching scenes.
**STRATA:** a vehicle you drive from inside, with stations far apart. It's a small version of the Crawler interior and E3 Driving blind.

### 2. Pressure: grid puzzle
**Play:** hand-made levels where water is about to flow. Lay pipes, valves and drains on a grid so the water reaches the tank and floods nothing important. Undo is free and the levels get harder.
**You make:** the flow rules and the win check.
**Godot:** `TileMapLayer` with custom data per tile, levels as scenes, switching between scenes, a level-select menu built from `Control` nodes, an undo stack.
**STRATA:** working with grid data and fluids at a scale you can reason about.

### 3. Hold the Line: top-down arena survival
**Play:** a ten-minute run in a small arena. Waves of creatures come at you; between waves you choose one of three upgrades. Each run plays out differently.
**You make:** one weapon, two enemy behaviours, and the upgrade effects.
**Godot:** instancing scenes at runtime, `Area2D` hitboxes and hurtboxes, groups, custom signals, a HUD made from `Control` nodes, `Timer`, object pooling, and the profiler once there are 300 enemies on screen.
**STRATA:** creature behaviour and how many things the engine can move at once.

### 4. Crumble: local party game
**Play:** two to four players on one screen with controllers, on a platform that breaks apart under them. Shove each other off, and the last one standing wins the round.
**You make:** the shove, the crumbling tiles and the round scoring.
**Godot:** several gamepads (device IDs), joining players at runtime, physics impulses, a shared camera that zooms to fit everyone.
**STRATA:** tries out one of the design's open questions: one shared camera, or one per player?

### 5. Deep Hand: card game
**Play:** a solo card game of your own design. You draw rope, lamp and pick cards to go down a cave one depth at a time, and each card you play changes what's ahead. Five minutes a run.
**You make:** the card rules and the dig resolution.
**Godot:** `Control` layout with containers and themes, drag and drop, `Tween`, custom `Resource` classes saved as `.tres` files for the card data.
**STRATA:** data-driven content and UI, which the journal and items will both need.

### 6. Two Keys: online co-op puzzle
**Play:** two players on two PCs. Each sees half of the room: one sees the levers, the other sees what they move. Talk it through to escape.
**You make:** the puzzle logic and what each player is shown.
**Godot:** `ENetMultiplayerPeer` hosting and joining, `[Rpc]` methods, authority, `MultiplayerSpawner` and `MultiplayerSynchronizer`, a lobby screen.
**STRATA:** the design's biggest risk (networking) and its "split the crew" pillar, on a small scale.

### 7. Dig Beat: rhythm game
**Play:** dig through layers of rock in time with the music. Swings on the beat break more rock; missing the beat clogs the shaft.
**You make:** the timing window and scoring.
**Godot:** `AudioStreamPlayer` and syncing to the playback position, `AnimationPlayer`, audio buses and effects.
**STRATA:** audio, which hasn't been touched yet.

### 8. Little Camp: colony sim
**Play:** a few small workers you can't control directly. Mark jobs (chop, carry, build a wall) and keep the camp fed and lit through three nights while something prowls outside.
**You make:** how workers pick jobs and carry things.
**Godot:** `NavigationRegion2D` and `NavigationAgent2D`, a simple state machine, autoloads, saving and loading to JSON with `FileAccess`, a day–night cycle with `CanvasModulate`.
**STRATA:** a base that has to be kept up, and saving a run.

### 9. Diorama Drive: a 3D detour
**Play:** drive a small truck around a 3D island and deliver crates without tipping over.
**You make:** the truck setup and the deliveries.
**Godot:** Godot's 3D side: `Node3D`, `VehicleBody3D`, lights and shadows, CSG for blocking out the level, `Camera3D` follow.
**STRATA:** no direct link. It's here for variety and to show whether 3D tempts you.

### 10. Sand Jar: falling-sand toy
**Play:** a sandbox jar using the real `Strata.Sim`. Pour materials, set fires and grow grey lace; hidden challenges unlock as you experiment ("make obsidian", "fill the jar with light").
**You make:** the tools that pour and the challenge checks.
**Godot:** drawing the sim into chunk textures, a material-id shader, `WorkerThreadPool`, and the profiler and Monitors.
**STRATA:** the fix for the MVP's single big image, and a test of whether the sim is fun to play with on its own.

See [godot-syllabus.md](godot-syllabus.md) for the units, goals and reading list each game draws on.

## Order

Play 1 to 3 in order, since they cover the basics. After that, pick whichever sounds most fun that week. Two tips: do Two Keys (6) before STRATA's own networking work, and Sand Jar (10) before reworking how the MVP is drawn.

Mix freely: the plan should change once you've played the first two. Add a game if one occurs to you, and drop any that stops sounding fun.
