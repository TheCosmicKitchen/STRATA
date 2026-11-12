# 1. Touchdown

**Land the ship from inside it.** You're a small person in the cabin of a lander, shown in cross-section. The left thruster's lever is at the left end of the cabin and the right one's is at the right end, with the engine block in between. Set a lever, run and jump to the other one, and try not to land on your face. Your own weight pulls down on whichever end you stand at. When the ship falls freely, you float.

Land on pads 1 to 4 in order. Each landing gives back some fuel. Touching down too fast or too tilted is a crash.

**Units from the [syllabus](../../docs/godot-syllabus.md):** 1 Editor and scenes · 2 C# and input · 3 Physics · 4 Signals. You'll also see 5 (scenes and HUD), 6 (camera, particles) and 8 (a SubViewport) in the code that's already written.

## Run it

1. Build once (and after every code change if you're not using the editor's Build button): `dotnet build learn/01-touchdown/Touchdown.csproj`
2. Open `learn/01-touchdown/project.godot` in Godot 4.7 .NET and press `F5`.

Right now it runs but nothing moves: that's your job.

## How it's put together

Open each scene in the editor and click through its nodes before you start.

| Scene | What it is |
| --- | --- |
| `title/Title.tscn` | Title screen. Space loads `main/Main.tscn`. |
| `main/Main.tscn` | One game. `Main.cs` places the ship, listens for its signals, moves the camera and runs the HUD. |
| `world/Terrain.cs`, `world/Pad.tscn` | The ground is built in code when the scene starts; pads are instanced from `Pad.tscn`. |
| `ship/Ship.tscn` | A `RigidBody2D` with the hull, feet and two thruster markers. It also holds a **SubViewport** containing the cabin. |
| `ship/Interior.tscn` | The cabin: walls, two `Lever`s, the `Crew` and a loose crate. |
| `hud/Hud.tscn` | Instruments on a `CanvasLayer`. |

**The trick:** the cabin isn't in the same physics world as the ship. A `SubViewport` has its own world, so the cabin is a small room that never moves. The `InteriorView` sprite shows that room's picture on the hull, so it turns with the ship. Every physics frame:

- the ship works out what "down" feels like inside it (gravity minus its own acceleration, turned into the ship's frame) and hands that to the cabin as `Interior.Gravity`;
- the cabin reports where everything with weight is, and the ship pushes down at those points.

That's why tilting the ship tilts "down" for the crew, thrusting presses you into the floor, and falling makes you float. You don't have to write any of it, but read `Ship.UpdateCabinGravity` once your crew walks.

## Your four jobs

Each one is a method marked `// YOU:` with hints in its comment. Do them in this order; each makes the game a bit more alive.

| # | Where | What | Done when |
| --- | --- | --- | --- |
| 1 | `ship/Crew.cs` `_PhysicsProcess` | Walk and jump with `CharacterBody2D`, with "down" coming from the cabin. Start with plain down, then make it follow `_interior.Gravity`. | You can walk to both ends and jump the engine block and the crate. |
| 2 | `ship/Lever.cs` | Use the `Area2D`'s `BodyEntered`/`BodyExited` signals to know when you're in reach, then move `Throttle` with W/S. | The handle glows when you're close and moves when you push. The HUD bars move. |
| 3 | `ship/Ship.cs` `FireThrusters` | Push the `RigidBody2D` with `ApplyForce` at each thruster, and burn fuel. | The ship lifts, and one lever on its own makes it turn. |
| 4 | `ship/Ship.cs` `OnBodyEntered` | Decide landing or crash, and emit the right signal. | Hard landings explode. Soft ones on the next pad count, and the HUD moves on to the next pad. |

After job 3 the game is flyable. After job 4 it's winnable.

**Rules for this part:** Claude doesn't write these four methods unless you ask. Ask anything else: why something works, what a node does, what an error means, or a hint when you're stuck.

## Read for each job

- **Job 1:** [Using CharacterBody2D](https://docs.godotengine.org/en/stable/tutorials/physics/using_character_body_2d.html), [Listening to player input](https://docs.godotengine.org/en/stable/getting_started/step_by_step/scripting_player_input.html), [Vector math](https://docs.godotengine.org/en/stable/tutorials/math/vector_math.html) (the dot product part)
- **Job 2:** [Using Area2D](https://docs.godotengine.org/en/stable/tutorials/physics/using_area_2d.html), [Using signals](https://docs.godotengine.org/en/stable/getting_started/step_by_step/signals.html), [C# signals](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_signals.html)
- **Job 3:** [Using RigidBody](https://docs.godotengine.org/en/stable/tutorials/physics/rigid_body.html), [RigidBody2D class](https://docs.godotengine.org/en/stable/classes/class_rigidbody2d.html) (`ApplyForce`), [Matrices and transforms](https://docs.godotengine.org/en/stable/tutorials/math/matrices_and_transforms.html)
- **Job 4:** [C# signals](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_signals.html) again: you're emitting this time.

## Stuck?

- **Nothing happens when I change code.** Rebuild: the hammer icon at the top right of the editor, or `dotnet build`.
- **The crew falls through the floor or doesn't fall at all.** You must call `MoveAndSlide()` once per physics frame, after setting `Velocity`.
- **`IsOnFloor()` is always false.** Set `UpDirection` before `MoveAndSlide()`. It defaults to straight up, which is wrong once the ship tilts.
- **The lever never notices me.** Debug → Visible Collision Shapes shows the lever's reach box. Its mask is the crew layer (2) and the crew is on layer 2, so check the signal is connected (`_Ready` does it) and that your method runs (`GD.Print`).
- **The ship spins wildly.** `ApplyForce`'s second argument is an offset from the ship's centre in world directions, not a position in the world. Subtract `GlobalPosition`.
- **The ship goes sideways instead of up.** "Up" for the ship is `-GlobalTransform.Y`, not `Vector2.Up`.
- **Every landing is a crash.** By the time `BodyEntered` runs, the hit has already slowed the ship. Use `PreviousVelocity`.

Turn on Debug → Visible Collision Shapes any time to see what's touching what. The Remote tab in the Scene dock shows the live node tree while the game runs.

## Testing without playing

`dev/DevTools.cs` can drive the game from the command line and take screenshots. Claude uses it to check things; you can too:

```bash
godot --path learn/01-touchdown -- --drive=land --shots=C:/temp/td --quit=25
```

## Polish (later, with Claude)

Ideas for once the four jobs work. Pick what sounds fun:

- Push the crate by walking into it (apply an impulse to the rigid bodies you collide with).
- Crew injuries: a hard landing hurts, a crash with the crew still standing up is worse.
- Thruster sound and particles, a landing "thunk", the camera shaking on hard touches.
- Best time and fuel saved to `user://` (Unit 7).
- Two players in the cabin, one per lever (Unit 2: gamepads).
- Wind on later legs. A cargo pickup on each pad that has to be carried in by hand.

## Notes after playing

*What was fun, what wasn't, what you learned, and anything for STRATA. Fill this in at the end.*
