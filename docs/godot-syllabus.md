# Godot syllabus

What "knowing Godot" means for this project, in ten units. Each unit has goals (things you can do without looking them up), what to read, a small exercise, and which learning games use it. The games are in [learning-roadmap.md](learning-roadmap.md). Godot here is 4.7 .NET, scripted in C#.

## How to use this

- **Read only what the current game needs.** Each game's README lists the units it touches. Read those pages, build the game, then come back to this page and tick the goals you can now do.
- **Docs first, then ask.** Press `F1` in the editor (or Ctrl-click a type in code) to open the class reference. When the docs don't answer it, ask Claude. Asking "why" is as useful as asking "how".
- **Goals are checkboxes.** Tick one only when you've done it in a game, not just read about it.

## Reference shelf

| What | Where | Use it for |
| --- | --- | --- |
| Official manual | [docs.godotengine.org](https://docs.godotengine.org/en/stable/) | Concepts and tutorials |
| Class reference | [All classes](https://docs.godotengine.org/en/stable/classes/index.html), or `F1` in the editor | Every node, method, signal and property |
| C# in Godot | [C# basics](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html), [C# API differences](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_differences.html) | Most examples online are GDScript; this page maps them to C# |
| C# style | [C# style guide](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_style_guide.html) | Naming and layout |
| Official demos | [godot-demo-projects](https://github.com/godotengine/godot-demo-projects) | Small working projects for almost every feature |
| Recipes | [Godot Recipes (KidsCanCode)](https://kidscancode.org/godot_recipes/4.x/) | Short, practical answers ("how do I...") |
| Guides | [GDQuest library](https://www.gdquest.com/library/) | Clear explanations of patterns and techniques |
| Best practices | [Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html), [When to use what node](https://docs.godotengine.org/en/stable/tutorials/best_practices/node_alternatives.html) | Deciding how to structure things |

Most community examples are written in GDScript. Translating them to C# is mostly mechanical (`move_and_slide()` becomes `MoveAndSlide()`, `$Node` becomes `GetNode<T>("Node")`, `signal hit` becomes `[Signal] public delegate void HitEventHandler();`), and it's good practice.

---

## Unit 1: Editor and scenes

**Goals**
- [ ] Explain node, scene, scene tree and instance in your own words.
- [ ] Build a scene in the editor, save it, and instance it inside another scene.
- [ ] Use the Inspector to change properties, and expose your own with `[Export]`.
- [ ] Find your way around the Scene dock, FileSystem dock, 2D view and Output panel, and use Play scene (`F6`) versus Play project (`F5`).
- [ ] Watch the running game's node tree in the Remote tab of the Scene dock.

**Read:** [Key concepts](https://docs.godotengine.org/en/stable/getting_started/introduction/key_concepts_overview.html), [Nodes and scenes](https://docs.godotengine.org/en/stable/getting_started/step_by_step/nodes_and_scenes.html), [Creating instances](https://docs.godotengine.org/en/stable/getting_started/step_by_step/instancing.html), [C# exports](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_exports.html)

**Exercise:** open a game's main scene, find a node you didn't write, change an exported number in the Inspector, and play to see the effect.

**Games:** all of them, starting with Touchdown.

## Unit 2: C# scripting and input

**Goals**
- [ ] Know when `_Ready`, `_Process`, `_PhysicsProcess` and `_UnhandledInput` run, and what `delta` means.
- [ ] Get other nodes with `GetNode<T>()`, `%UniqueName` and exported node references, and know which is best for what.
- [ ] Add actions in Project Settings → Input Map and read them with `Input.IsActionPressed` and `Input.GetAxis`.
- [ ] Use `GD.Print` and breakpoints to find out what your code is doing.

**Read:** [Your first script](https://docs.godotengine.org/en/stable/getting_started/step_by_step/scripting_first_script.html), [Listening to player input](https://docs.godotengine.org/en/stable/getting_started/step_by_step/scripting_player_input.html), [Idle and physics processing](https://docs.godotengine.org/en/stable/tutorials/scripting/idle_and_physics_processing.html), [Input examples](https://docs.godotengine.org/en/stable/tutorials/inputs/input_examples.html), [Scene unique nodes](https://docs.godotengine.org/en/stable/tutorials/scripting/scene_unique_nodes.html), [Debugging tools](https://docs.godotengine.org/en/stable/tutorials/scripting/debug/overview_of_debugging_tools.html)

**Exercise:** add a new action to the Input Map, bind it to a key and a gamepad button, and print something when it's pressed.

**Games:** all of them, starting with Touchdown.

## Unit 3: 2D physics and vectors

**Goals**
- [ ] Choose between `StaticBody2D`, `CharacterBody2D`, `RigidBody2D` and `Area2D`, and say why.
- [ ] Move a `CharacterBody2D` with `Velocity` and `MoveAndSlide`, including `IsOnFloor` and `UpDirection`.
- [ ] Push a `RigidBody2D` with `ApplyForce`, `ApplyImpulse` and `ApplyTorque`, and know why a force away from the centre makes it spin.
- [ ] Set collision layers and masks so the right things hit each other.
- [ ] Use vectors comfortably: length, normalising, dot product, rotating, and local versus global coordinates.

**Read:** [Physics introduction](https://docs.godotengine.org/en/stable/tutorials/physics/physics_introduction.html), [Using CharacterBody2D](https://docs.godotengine.org/en/stable/tutorials/physics/using_character_body_2d.html), [Using RigidBody](https://docs.godotengine.org/en/stable/tutorials/physics/rigid_body.html), [Using Area2D](https://docs.godotengine.org/en/stable/tutorials/physics/using_area_2d.html), [Vector math](https://docs.godotengine.org/en/stable/tutorials/math/vector_math.html), [Matrices and transforms](https://docs.godotengine.org/en/stable/tutorials/math/matrices_and_transforms.html), [RigidBody2D class](https://docs.godotengine.org/en/stable/classes/class_rigidbody2d.html)

**Exercise:** turn on Debug → Visible Collision Shapes, run a game, and explain every shape you see and what it collides with.

**Games:** Touchdown (rigid ship, character inside), Hold the Line, Crumble, Diorama Drive.

## Unit 4: How nodes talk to each other

**Goals**
- [ ] Connect a built-in signal in the editor and in code.
- [ ] Declare your own `[Signal]` and emit it with `EmitSignal`.
- [ ] Follow "call down, signal up": parents call children's methods, and children send signals to whoever is listening.
- [ ] Use groups to find or notify many nodes at once.
- [ ] Use an autoload for something truly global, and know when not to.

**Read:** [Using signals](https://docs.godotengine.org/en/stable/getting_started/step_by_step/signals.html), [C# signals](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_signals.html), [Groups](https://docs.godotengine.org/en/stable/tutorials/scripting/groups.html), [Singletons (autoload)](https://docs.godotengine.org/en/stable/tutorials/scripting/singletons_autoload.html), [Scene organization](https://docs.godotengine.org/en/stable/tutorials/best_practices/scene_organization.html)

**Exercise:** in a finished game, trace one signal from where it's emitted to every method that reacts to it.

**Games:** Touchdown (landing and crash signals, lever signals), every game after it.

## Unit 5: Game flow and UI

**Goals**
- [ ] Switch scenes with `ChangeSceneToFile` and restart with `ReloadCurrentScene`.
- [ ] Build a HUD on a `CanvasLayer` so it stays put while the camera moves.
- [ ] Lay out `Control` nodes with anchors and containers so they survive a window resize.
- [ ] Make a title screen, a pause menu (`GetTree().Paused` and process modes) and a game-over screen.
- [ ] Style UI with a `Theme`.

**Read:** [Changing scenes manually](https://docs.godotengine.org/en/stable/tutorials/scripting/change_scenes_manually.html), [Using SceneTree](https://docs.godotengine.org/en/stable/tutorials/scripting/scene_tree.html), [UI index](https://docs.godotengine.org/en/stable/tutorials/ui/index.html), [Size and anchors](https://docs.godotengine.org/en/stable/tutorials/ui/size_and_anchors.html), [Containers](https://docs.godotengine.org/en/stable/tutorials/ui/gui_containers.html), [Themes](https://docs.godotengine.org/en/stable/tutorials/ui/gui_skinning.html)

**Exercise:** add a new label to a HUD that shows something useful, and keep it in place when the window is resized.

**Games:** Touchdown, Pressure (level select), Deep Hand (UI-heavy).

## Unit 6: Feel

**Goals**
- [ ] Make a `Camera2D` follow smoothly, zoom and shake.
- [ ] Animate anything with a `Tween`, and animate a sequence with an `AnimationPlayer`.
- [ ] Add one-shot and continuous particles.
- [ ] Play sounds, positioned in space with `AudioStreamPlayer2D`, and route them through audio buses.
- [ ] Draw simple shapes yourself in `_Draw()` for placeholder art.

**Read:** [Tween class](https://docs.godotengine.org/en/stable/classes/class_tween.html), [Camera2D class](https://docs.godotengine.org/en/stable/classes/class_camera2d.html), [Animation introduction](https://docs.godotengine.org/en/stable/tutorials/animation/introduction.html), [2D particles](https://docs.godotengine.org/en/stable/tutorials/2d/particle_systems_2d.html), [Audio streams](https://docs.godotengine.org/en/stable/tutorials/audio/audio_streams.html), [Audio buses](https://docs.godotengine.org/en/stable/tutorials/audio/audio_buses.html), [Custom drawing in 2D](https://docs.godotengine.org/en/stable/tutorials/2d/custom_drawing_in_2d.html)

**Exercise:** take one moment in a game (a landing, a hit) and make it feel better with three small effects.

**Games:** the polish step of every game, especially Dig Beat.

## Unit 7: Data and saving

**Goals**
- [ ] Write a custom `Resource` class, make `.tres` files from it in the editor, and load them in code.
- [ ] Save and load game state to `user://` with `FileAccess` (JSON) or `ConfigFile`.
- [ ] Know the difference between `res://` and `user://`.

**Read:** [Resources](https://docs.godotengine.org/en/stable/tutorials/scripting/resources.html), [Saving games](https://docs.godotengine.org/en/stable/tutorials/io/saving_games.html), [Data paths](https://docs.godotengine.org/en/stable/tutorials/io/data_paths.html)

**Exercise:** save a best score and load it again on the next launch.

**Games:** Touchdown (best score), Deep Hand (cards as resources), Little Camp (save a run).

## Unit 8: Rendering

**Goals**
- [ ] Paint levels with `TileMapLayer` and a `TileSet`, including collision and custom data per tile.
- [ ] Light a scene with `PointLight2D`, `CanvasModulate` and `LightOccluder2D`.
- [ ] Render something into a `SubViewport` and show it as a texture.
- [ ] Write a simple `canvas_item` shader.
- [ ] Understand 2D transforms and draw order (z-index, `CanvasLayer`).

**Read:** [Using TileMaps](https://docs.godotengine.org/en/stable/tutorials/2d/using_tilemaps.html), [Using TileSets](https://docs.godotengine.org/en/stable/tutorials/2d/using_tilesets.html), [2D lights and shadows](https://docs.godotengine.org/en/stable/tutorials/2d/2d_lights_and_shadows.html), [Viewports](https://docs.godotengine.org/en/stable/tutorials/rendering/viewports.html), [Introduction to shaders](https://docs.godotengine.org/en/stable/tutorials/shaders/introduction_to_shaders.html), [Your first 2D shader](https://docs.godotengine.org/en/stable/tutorials/shaders/your_first_shader/your_first_2d_shader.html), [2D transforms](https://docs.godotengine.org/en/stable/tutorials/2d/2d_transforms.html)

**Exercise:** write a shader that flashes a sprite white when it gets hit.

**Games:** Touchdown (the cabin is a SubViewport), Pressure (tiles), Sand Jar (shaders).

## Unit 9: Systems

**Goals**
- [ ] Move an agent around obstacles with `NavigationAgent2D`.
- [ ] Write a small state machine in C# and keep it readable.
- [ ] Host and join a game over ENet, call `[Rpc]` methods, and sync nodes with `MultiplayerSpawner` and `MultiplayerSynchronizer`.
- [ ] Know who has authority over a node and why that matters.
- [ ] Cast a ray to check line of sight.

**Read:** [2D navigation](https://docs.godotengine.org/en/stable/tutorials/navigation/navigation_introduction_2d.html), [High-level multiplayer](https://docs.godotengine.org/en/stable/tutorials/networking/high_level_multiplayer.html), [Ray-casting](https://docs.godotengine.org/en/stable/tutorials/physics/ray-casting.html)

**Exercise:** give an enemy three states (idle, chase, flee) and draw its current state above its head.

**Games:** Hold the Line, Two Keys, Little Camp.

## Unit 10: Shipping

**Goals**
- [ ] Export a Windows build and run it on another PC.
- [ ] Find a slow frame with the profiler and the Monitors tab.
- [ ] Read an error in the Debugger and find the line that caused it.

**Read:** [Exporting projects](https://docs.godotengine.org/en/stable/tutorials/export/exporting_projects.html), [Exporting for Windows](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_windows.html), [The profiler](https://docs.godotengine.org/en/stable/tutorials/scripting/debug/the_profiler.html)

**Exercise:** send a build to a friend and watch them play without helping.

**Games:** every game ends here.

---

## Units per game

| Game | Units |
| --- | --- |
| 1 Touchdown | 1, 2, 3, 4, 5, 6, a little of 7 and 8 |
| 2 Pressure | 5, 7, 8 (tiles) |
| 3 Hold the Line | 3, 4, 9 (state machines), 10 (profiler) |
| 4 Crumble | 2 (gamepads), 3, 6 (camera) |
| 5 Deep Hand | 5, 6 (tweens), 7 |
| 6 Two Keys | 9 (multiplayer), 10 |
| 7 Dig Beat | 6 (audio, animation) |
| 8 Little Camp | 7, 9 (navigation, state machines) |
| 9 Diorama Drive | 3D versions of 1–3 |
| 10 Sand Jar | 8 (shaders), 10 (profiler) |
