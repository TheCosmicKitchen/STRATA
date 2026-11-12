# Touchdown: your guide

This is your path through the first game, one step at a time. Each step says **what to do**, **what to learn** and **how to know it worked**. Tick the boxes as you go.

It never gives you the finished code. It tells you what to look up, what to try and which questions to ask yourself. When you're stuck for more than about 15 minutes, ask Claude: for a hint, an explanation, or what an error means.

Plan on three or four sittings: Step 0 and the tour, then one or two jobs per sitting.

---

## Step 0: Get it running (15 min)

- [ ] Build it from a terminal at the repo root:
  ```bash
  dotnet build learn/01-touchdown/Touchdown.csproj
  ```
- [ ] Open Godot 4.7 .NET, click **Import**, and pick `learn/01-touchdown/project.godot`.
- [ ] Press **F5** (Play project). You should see the title screen. Press Space: the ship sits on pad 0 and nothing moves yet. That's expected.
- [ ] Press **Esc** twice to quit.

**Learn:** F5 runs the main scene (set in Project Settings → Application → Run). **F6** runs only the scene you have open, which is handy for testing one piece.

---

## Step 1: Tour the editor (30–45 min)

Don't write any code yet. The goal is to know where things are.

**Read first:** [Key concepts](https://docs.godotengine.org/en/stable/getting_started/introduction/key_concepts_overview.html) and [Nodes and scenes](https://docs.godotengine.org/en/stable/getting_started/step_by_step/nodes_and_scenes.html). They're short.

- [ ] **FileSystem dock** (bottom left): find every `.tscn` (scene) and `.cs` (script). Notice that scenes and their scripts sit next to each other by feature.
- [ ] Open `ship/Ship.tscn`. In the **Scene dock** (top left), click each node and look at the **Inspector** (right):
  - `Ship` is a `RigidBody2D`. Find `Mass`, `Center Of Mass`, `Contact Monitor` and `Angular Damp`. Why do you think contact monitoring is on? (Hint: job 4.)
  - `Hull`, `LeftFoot` and `RightFoot` are collision shapes. Click one and see its outline in the 2D view.
  - `LeftThruster` and `RightThruster` are `Marker2D`s: just named points.
  - `InteriorViewport` is a `SubViewport`. The cabin lives inside it.
- [ ] With the `Ship` node selected, scroll the Inspector to the script section and find the **exported** fields: `Max Thrust`, `Safe Speed` and so on. They come from `[Export]` lines in `Ship.cs`. Open `Ship.cs` and match them up.
- [ ] Open `ship/Interior.tscn`. Find the walls, the two levers, the crew and the crate. Click `LeftLever` and notice it's an **instance** of `Lever.tscn` (it has a film-clapper icon). Click the icon to open the original.
- [ ] Open `ship/Lever.tscn`. Select the `Lever` node and look at **Collision → Layer** and **Mask** in the Inspector. Which layer does it look at? Now check the same on `Crew.tscn`. Do they match?
- [ ] Turn on **Debug → Visible Collision Shapes** (top menu), press F5 and look at the shapes in the running game. Leave it on while you work.
- [ ] While the game runs, click **Remote** at the top of the Scene dock. This is the live node tree. Click `Ship` and watch its values in the Inspector.
- [ ] Open **Project → Project Settings → Input Map** and find `move_left`, `jump`, `lever_up` and the rest. These names are what your code will use.

**You've got it when** you can say, in your own words: what a scene is, what an instance is, what `[Export]` does, and what layer and mask mean.

---

## Step 2: Job 1, walking in a changing cabin (1–2 h)

**File:** `ship/Crew.cs`, the `_PhysicsProcess` method.

**Read:** [Your first script](https://docs.godotengine.org/en/stable/getting_started/step_by_step/scripting_first_script.html), [Idle and physics processing](https://docs.godotengine.org/en/stable/tutorials/scripting/idle_and_physics_processing.html), [Using CharacterBody2D](https://docs.godotengine.org/en/stable/tutorials/physics/using_character_body_2d.html), [Listening to player input](https://docs.godotengine.org/en/stable/getting_started/step_by_step/scripting_player_input.html). Most examples online are GDScript; [C# differences](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_differences.html) maps names across.

**Learn:**
- `_PhysicsProcess(delta)` runs 60 times a second, in step with physics. `delta` is the time since the last call (about 0.0167 s), so multiplying by it makes things per-second.
- A `CharacterBody2D` doesn't move by itself. You set `Velocity` and then call `MoveAndSlide()`, which moves it and slides along walls.
- Gravity is just something you add to the velocity every frame.

**Do it in small steps, running the game after each one:**

- [ ] **2a. Fall.** Add the cabin's gravity times `delta` to `Velocity`, then call `MoveAndSlide()`. Run it: you should drop onto the floor and stay there.
  *Ask yourself:* what happens if you forget `delta`? Try it and see.
- [ ] **2b. Walk.** Read the left/right input as a number from -1 to 1 (`Input.GetAxis`). Change the sideways part of `Velocity` toward `input * Speed`. Use `Mathf.MoveToward` with `Acceleration * delta` so you speed up and slow down smoothly.
  *Check:* you can walk to both walls.
- [ ] **2c. Jump.** If you're on the floor and "jump" was *just* pressed this frame, set the upward speed. Remember that in Godot **y points down**, so up is negative y.
  *Check:* you can jump over the engine block and the crate.
- [ ] **2d. Make "down" follow the ship.** This is the real lesson. So far you've treated "sideways" as x and "up" as -y. Now the cabin's gravity can point any way. You need to:
  1. Work out `up`: the opposite of gravity, normalised (length 1).
  2. Work out `right`: `up` turned 90°. The hint in the code gives you the formula.
  3. Split `Velocity` into "how much along right" and "how much along up". That's what the **dot product** does: `Velocity.Dot(right)`.
  4. Change those two numbers exactly as in 2b and 2c.
  5. Rebuild the velocity: `right * side + up * rise`.
  6. Set `UpDirection = up` so `IsOnFloor()` knows which way the floor is.

  **Read:** [Vector math](https://docs.godotengine.org/en/stable/tutorials/math/vector_math.html), especially "Dot product".

  *Check:* you can't test tilting yet because the ship doesn't fly. Do a quick hack: in `Interior.cs` `_Ready`, set `Gravity = new Vector2(300, 850);` and run. You should stand and walk on a slope. **Remove the hack afterwards.**

**Done when:** you can walk end to end, jump both obstacles, and the slope hack works.

**Concepts to be able to explain:** delta time; `Velocity` plus `MoveAndSlide`; why `IsOnFloor` needs `UpDirection`; what a dot product gives you.

---

## Step 3: Job 2, the levers (45 min–1 h)

**File:** `ship/Lever.cs`.

**Read:** [Using Area2D](https://docs.godotengine.org/en/stable/tutorials/physics/using_area_2d.html), [Using signals](https://docs.godotengine.org/en/stable/getting_started/step_by_step/signals.html), [C# signals](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_signals.html).

**Learn:**
- An `Area2D` doesn't block anything. It just notices when bodies go in or out, and announces that with the **signals** `BodyEntered` and `BodyExited`.
- `_Ready` already connects those signals to your two methods: `BodyEntered += OnBodyEntered;`. Find that line. That's what "connecting a signal" looks like in C#.
- The mask decides what the area can notice. You checked in Step 1 that it's the crew layer.

**Steps:**
- [ ] **3a.** In `OnBodyEntered`, put `GD.Print("entered: " + body.Name);` and run. Walk to a lever and watch the **Output** panel. Seeing the message is your proof that the signal works.
- [ ] **3b.** Replace the print: if the body is a `Crew` (`body is Crew`), remember it's in reach. Do the opposite in `OnBodyExited`.
  *Check:* the lever's handle glows yellow when you stand at it.
- [ ] **3c.** In `_PhysicsProcess`: while the crew is in reach, read `lever_up` and `lever_down` (`Input.GetAxis` works here too) and move `Throttle` by `Rate * delta`. Keep it between 0 and 1 with `Mathf.Clamp`.
- [ ] **3d.** When the throttle moves, `EmitSignal(SignalName.ThrottleChanged, Throttle)`. Nothing listens yet, but that's how the lever tells the world. It's the "signal up" half of "call down, signal up".

*Check:* the LEFT and RIGHT bars on the HUD move, and the flame appears under the ship (no thrust yet).

**Concepts to be able to explain:** what a signal is, who emits it and who listens; Area2D compared with a body; layers and masks.

---

## Step 4: Job 3, thrusters (1 h)

**File:** `ship/Ship.cs`, the `FireThrusters` method.

**Read:** [Using RigidBody](https://docs.godotengine.org/en/stable/tutorials/physics/rigid_body.html), and `ApplyForce` on the [RigidBody2D class page](https://docs.godotengine.org/en/stable/classes/class_rigidbody2d.html) (or press F1 in the editor and search for RigidBody2D). Skim [Matrices and transforms](https://docs.godotengine.org/en/stable/tutorials/math/matrices_and_transforms.html) for what `Transform.Y` means.

**Learn:**
- A `RigidBody2D` is moved by the physics engine. You don't set its position; you push it.
- `ApplyForce(force, offset)` pushes at a point. Pushing through the centre moves it; pushing off-centre also **turns** it. That turning is the whole game.
- `GlobalTransform.Y` is the direction the ship's own "down" points in the world, so `-GlobalTransform.Y` is its "up". It turns as the ship turns.

**Steps:**
- [ ] **4a.** Push the ship up through its centre, ignoring the levers: `ApplyForce(up * 3000)`. It should take off straight. Then delete that line.
- [ ] **4b.** For each thruster, push along the ship's up by `MaxThrust * throttle`, **at that thruster's offset** (its global position minus the ship's global position).
  *Check:* push only the left lever up. The ship should tip toward the right as the left side lifts. With both levers equal it should rise straight, until your weight tips it.
- [ ] **4c.** Burn fuel: subtract `BurnPerSecond * throttle * delta` per thruster. With no fuel, don't push.
  *Check:* the fuel bar drops faster the harder you burn.
- [ ] **Fly it!** Try to hover. Notice how standing at one end changes the balance, and how the floor shifts under you.

**Concepts to be able to explain:** force versus position; why off-centre force makes torque; local directions compared with world directions.

---

## Step 5: Job 4, landing or crash (30–45 min)

**File:** `ship/Ship.cs`, the `OnBodyEntered` method.

**Read:** [C# signals](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_signals.html) again. This time you're emitting with arguments.

**Learn:**
- `Ship` declares two signals at the top: `TouchedDown` and `Crashed`. Find where `Main.cs` connects to them (in `_Ready`). That's who's listening.
- `BodyEntered` only fires because `Contact Monitor` is on (from the Step 1 question).
- When a collision is reported, the physics engine has already slowed the ship down. That's why `PreviousVelocity` exists.

**Steps:**
- [ ] **5a.** Print the speed (`PreviousVelocity.Length()`) and `TiltDegrees` on every hit, and fly around bumping into things. Get a feel for the numbers.
- [ ] **5b.** If too fast or too tilted: call `Wreck()` and emit `Crashed` with the speed and tilt. Otherwise emit `TouchedDown` with the body. Once wrecked, ignore any later hits.
- [ ] **5c.** Play a full run: land on pad 1 and watch the HUD move to pad 2. Then crash on purpose.

**Done when:** you've won a run, or at least landed on pad 2.

**Concepts to be able to explain:** declaring a signal compared with emitting one compared with connecting to one; why `Main` decides "right pad?" and not `Ship`.

---

## Step 6: Wrap up

- [ ] Tick the goals you can now do in Units 1–4 of the [syllabus](../../docs/godot-syllabus.md).
- [ ] Fill in **Notes after playing** at the bottom of `README.md`: what was fun, what was annoying, and what clicked.
- [ ] Tell Claude you're done. Next is polish: sound, particles, a best score, maybe a second player in the cabin.

## When you're stuck

1. Read the error in the **Output** or **Debugger** panel. It names the file and line.
2. `GD.Print` the values you think you know. Usually one of them is wrong.
3. Check the README's **Stuck?** list.
4. Ask Claude. Good questions look like "I expected X, but Y happens, here's my code", or "why does Godot do Z?".
