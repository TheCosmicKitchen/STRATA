# Prototype specs

These are briefs for small, throwaway, playable pages. Each one is written so it can be handed to another model (or person) on its own, together with the "Shared rules" section below. Nothing here is game code; nothing built from these specs ships.

There are two kinds:

- **Playable diagrams (D1–D3)** make a system visible so we can reason about it. Success means "I now understand how this behaves".
- **Experiments (E1–E7)** test a feeling. Success means "we know whether this is fun, and which numbers make it so".

## How to delegate one

Give the builder: this file's "Shared rules" section, the one spec, and `mockups/strata-mockups.html` (style and sand-sim reference). Ask for one HTML file back plus the "Report back" answers. One builder per spec; specs do not depend on each other unless "Reuses" says so.

## Shared rules (apply to every spec)

**Form**
- One self-contained file at `mockups/prototypes/<id>-<name>.html`, for example `mockups/prototypes/e1-winch-descent.html`. Inline CSS and JS. No build step, no frameworks, no external assets. Google Fonts is the only allowed network request.
- Target about 600–900 lines. If it needs more, the scope is wrong: cut features, not quality.
- Must open by double-clicking the file (works from `file://`), in current Chrome and Firefox, at 60 fps on a mid-range laptop.

**Look**
- Use the mockup's tokens: background `#0c0f11`, panel `#13171a`, line `#262d32`, ink `#e4ddcf`, muted `#8e958f`, rust `#c96a3b`, glow `#6fdcc0`, warn `#e3a33f`, crit `#e2553f`. Fonts: Big Shoulders Stencil Display (titles), Barlow (body), JetBrains Mono (readouts), Caveat (handwriting).
- Dark only. Pixel-art canvases use `image-rendering: pixelated`.
- No health bars, no score, no XP, anywhere. If a spec needs to show body state, use the per-part silhouette from the mockup.

**Structure of every page**
1. Title, one sentence stating the question the page answers.
2. The play area (canvas).
3. A **tuning panel**: every number the spec lists under "Tunables" is a labelled slider or toggle with its live value shown. Tunables are stored in the URL hash so a setup can be shared by copying the link. A "Reset to defaults" button.
4. A **readout strip** in monospace with the values listed under "Readouts".
5. A controls legend.

**Behaviour**
- Fixed-step simulation (60 steps per second) decoupled from rendering. Seeded random generator (mulberry32, as in the mockup); the seed is a tunable. Same seed and same inputs give the same run.
- Keyboard and mouse only. `R` restarts with the same seed, `N` picks a new seed, `P` pauses.
- Pause automatically when the tab is hidden.

**Sand sim, where a spec says "uses the sand sim"**
- Port the falling-sand code from the `Particle simulation` block of `mockups/strata-mockups.html`: same material ids, densities and reaction rules. Grid size is per spec. Do not invent new materials unless the spec lists them.

**Out of scope for all of them**
- Real networking, accounts, saving to disk, sound (unless the spec says so), mobile layout, accessibility polish beyond readable contrast and visible focus.

**Report back** (the builder answers these in their final message)
1. Which defaults felt best, and which tunables mattered most.
2. Anything in the spec that turned out to be unclear or contradictory.
3. What they cut to stay in budget.

---

## Playable diagrams

### D1 — Sim tick, slowed down

**Question:** How do chunks, dirty rects and checkerboard passes actually behave, and how much work do they save?

**Shows:** A 512×384 cell world (8×6 chunks of 64×64) running the sand sim. Overlays, each toggleable: chunk grid; each chunk's dirty rectangle (outlined in glow); which checkerboard pass a chunk belongs to (four passes: even/even, odd/even, even/odd, odd/odd); sleeping chunks dimmed.

**User can:** paint materials with the mouse (palette of 8); step one tick, one pass, or run; slow the tick rate down to 1 per second so the four passes are visibly separate.

**Rules to implement**
- A chunk keeps a dirty rect: the bounding box of cells that changed this tick, grown by 1 cell. Next tick only cells inside the rect are visited. An empty rect means the chunk sleeps.
- A cell that moves across a chunk border wakes the neighbour and extends its rect.
- The four passes are processed in order; inside a pass, chunks are independent (in the real game they run on separate threads; here just colour them the same).

**Tunables:** tick rate, chunk size (32/64/128), rect grow margin (0–2), overlays on/off, seed.

**Readouts:** cells visited this tick vs total cells (and as a percentage), awake chunks, tick time in ms.

**Done when:** pouring water into a still cave lights up only the chunks it passes through, and they go dark again when it settles. Setting margin to 0 visibly breaks something (cells freeze at rect edges), which is the point.

### D2 — Net pipeline

**Question:** What does a client actually see when it gets the world as chunk messages over a limited, lossy connection?

**Shows:** Two canvases side by side. Left is the "host" running the sand sim (256×192 cells). Right is the "client", which never runs the sim: it only applies messages. Between them, an animated strip showing messages in flight as small blocks sized by byte count.

**Rules to implement**
- Every send tick, the host builds messages for dirty chunks. A message is: chunk id, chunk version, rect, then the rect's cells run-length encoded (pairs of count and material id). If RLE is larger than raw, send raw. Messages carry the current contents of the rect, not a difference from the previous message, so a lost message is harmless once a newer one arrives.
- The host has a byte budget per send tick. Chunks are ordered by priority: distance to the client's view centre, then how long since they were last sent. Whatever does not fit waits.
- A fake link applies delay, jitter and random loss to each message.
- Round-robin refresh: each send tick, one visible chunk is resent in full even if clean, so losses heal.
- The client view is a movable rectangle (drag it); only chunks intersecting it, plus one chunk of margin, are sent.

**Tunables:** send rate (5–30 Hz), byte budget per second (5–200 kB/s), latency (0–400 ms), jitter, loss (0–20%), refresh on/off, RLE on/off.

**Readouts:** bytes per second (current, peak, average), messages per second, compression ratio, number of client cells that differ from the host right now, age of the stalest visible chunk.

**Stress buttons:** "Flood" (dump a lake), "Burn" (ignite a methane pocket), "Collapse" (turn a rock ceiling to sand).

**Done when:** you can find the budget below which a flood visibly tears on the client, and read off the number. That number goes into `docs/specs/networking.md`.

### D3 — Reaction table

**Question:** Is the material and reaction set coherent, and what should the data file format look like?

**Shows:** A matrix of all materials against all materials. Each cell of the matrix shows what happens when the two touch (for example "obsidian + steam"), or is blank. Clicking a matrix cell drops the two materials together in a small sand-sim chamber (96×64) below so you can watch it.

**Rules to implement**
- Reactions are not hard-coded in the update loop. They live in one JSON-like object at the top of the file: `{a, b, becomesA, becomesB, chance, needs}`. The sim reads only that table. Materials likewise: `{id, name, colour, phase (solid/powder/liquid/gas), density, flammable, lifetime}`.
- An "Edit data" panel shows the table as text; editing it and pressing Apply rebuilds the sim.

**Tunables:** per-reaction chance (via the text panel), chamber seed.

**Readouts:** count of materials, count of reactions, pairs with no reaction.

**Done when:** every reaction in `docs/design-summary.md` is reproducible by clicking, and a new material can be added by editing only the data panel.

---

## Experiments

### E1 — Winch descent

**Question:** Is being lowered into the dark on a rope tense and readable, with nothing else in the game?

**Player does:** Controls one figure hanging from a winch line in a shaft above a cave (uses the sand sim, 240×160). `W`/`S` ask the winch to reel in or pay out; `A`/`D` swing; `Space` unclips from or clips to the line when touching it; `F` toggles the helmet lamp. On foot: walk, jump, climb one-cell steps.

**Rules**
- Light: only cells reached by the lamp cone or by glowing materials (fire, lava, moss) are drawn; everything else is black. The lamp has a battery.
- Falling: landing speed above a threshold injures a leg; well above it, the figure is downed. No health bar: a small body silhouette shows which part is hurt. A hurt leg halves walk speed and disables jumping.
- Air: a suit air gauge drains; in methane or CO₂ pockets you cannot see the gas, only a suit sensor that ticks faster.
- The winch moves slowly and stops when the rope would go slack or exceed its length.
- Goal: reach a marked sample at the bottom, clip back on, and get reeled out before air runs out.

**Tunables:** winch speed, rope length, lamp cone angle and range, battery life, air supply, safe and downed fall speeds, swing strength, gravity, seed.

**Readouts:** depth, rope out, air, battery, last landing speed.

**Done when:** a first-time player takes a fall injury at least once in three runs with defaults, and can explain why it happened.

### E2 — Two ends of the line

**Question:** Is the surface job interesting, or is the person at the winch just waiting?

**Reuses:** E1's cave and figure.

**Player does:** Two browser windows on the same machine, linked with `BroadcastChannel` (no server). Window A is the delver from E1, except the winch is no longer theirs to control. Window B is the surface operator and shows **no view of the cave**: only a winch lever, rope-out and tension gauges, a power budget shared between winch, floodlight and radio, and a text radio.

**Rules**
- The delver asks for rope by radio. Radio messages are typed text; with distance and rock between the two, characters are randomly replaced by static.
- Tension spikes when the delver falls on the line; if it stays above a limit the operator must brake or the line is damaged (a damaged line reels slower).
- Power: the generator supplies less than winch + floodlight + radio need together, so the operator chooses what is on.
- A surface event fires at a random time (a dust squall that cuts radio for a while, or a generator stall that needs a hold-to-restart) so the operator has something to manage besides the lever.

**Tunables:** radio garble per 10 cells of depth, power supply, each device's draw, tension limit, event frequency, whether the operator gets a depth gauge at all.

**Readouts (operator window):** rope out, tension, power draw vs supply.

**Done when:** two people can play it sitting back to back, and afterwards the operator can say what they were worried about.

### E3 — Driving blind

**Question:** When nothing breaks on a timer, do the crew's own choices (how the pilot drives, whether anyone is watching) create enough trouble and tension?

**Not Barotrauma.** No random faults, no hull breaches, no leaks to patch, no repair-minigame-at-a-station loop. A first build used timer-driven faults including a hull breach and was rejected as a copy. Every problem in this prototype must trace back to something the crew did or failed to notice.

**Player does:** Walks one figure through a side cross-section of the Crawler (three decks, ladders, rooms as in the mockup) while it crosses a scrolling side-view landscape outside. Stations work only while someone is at them (`E`): helm, engine, winch, and two places to *look* from: a lookout hatch on the roof and a rear window. Everything outside is dark except what the Crawler's lights reach.

**Consequences of piloting** (the helm sets speed and can steer around obstacles by choosing the low or high track at forks)
- Driving fast over rough ground rattles the machine: anything not strapped down inside slides and falls (crates in the workshop, jars in the lab), and a crew member on a ladder can be thrown off (fall injury as in E5).
- Hitting a boulder hard bends a track: the Crawler pulls to one side and loses top speed until someone goes outside through the hatch with a tool and works on it by hand (exposed while doing it).
- Soft ground taken at speed bogs the machine; rocking it out needs engine and helm together.
- A slope taken at an angle tips the hull: the winch load swings and can strike the mast.
- Engine noise and the floodlight carry. Running loud and bright is faster and safer to drive, but attracts things (below).

**Consequences of not watching**
- The helm window only shows ahead and close. What is beside, behind or above can only be seen from the lookout hatch and the rear window, and only within light.
- Terrain warnings (a ravine edge, a rockfall, a soft patch) are visible from the lookout a few seconds before the helm can see them. If nobody is looking, the pilot finds out by hitting them.

**Creatures** (one kind is enough for the prototype)
- Something that lives in the dark follows vibration and light. It approaches from the sides or behind, slowly, and is visible only as eye-glints or movement at the edge of the light, and only from a lookout point.
- If it reaches the Crawler unseen it latches on and does something physical and specific: chews a track (as a bent track), tears at the winch line, or climbs to the roof and waits by the hatch.
- If it is seen in time, the crew can respond: kill the engine and lights and wait (it loses interest), turn the floodlight on it (it flinches back, for a while), or speed away over rough ground (with the rattling consequences above).
- Anyone who goes outside while it is latched on is in danger. No damage numbers; injuries per body part.

**Bots:** up to three bot crewmates can be sent to a station with number keys, to compare solo and crewed play. Bots only stand where told; a bot at a lookout *does not* warn you: the player must glance at what that lookout sees. A small inset per manned lookout shows its view, and an inset is shown only if someone is there.

**Goal:** reach the marker with the winch load and crew intact.

**Tunables:** walk and ladder speed, Crawler top speed, rattle threshold (speed × roughness), boulder and soft-ground frequency, how far ahead the lookout sees, creature attraction to noise and to light, creature speed, how long lights-off makes it lose interest, track-repair time, number of bots (0–3), Crawler length, seed.

**Readouts:** distance to go, speed, noise level, light on/off, time spent watching vs walking vs at the helm.

**Done when:** every bad thing that happens in a run can be traced by the player to a choice ("I was going too fast", "no one was at the lookout"), and players start posting someone at the lookout without being told to.

### E4 — The lab room

**Question:** Is discovering hidden traits by doing experiments with your own hands satisfying, and dangerous enough that a careless experiment can go badly wrong?

**Not a machine.** There is no bench menu, no drag-onto-jar, no gauges panel. The player is a figure standing in a room, and every experiment is something the figure physically does with a tool in hand. A first build of this spec as a menu-driven bench was rejected as too clunky.

**Setting:** the Crawler's lab module in side view (uses the sand sim for the room's air, liquids and fire, about 200×110 cells): a work table, shelves, a sink with a tap, a cold locker, a small vent fan with a switch, a door to the corridor, and a sample rack holding four unnamed specimens in glass jars.

**Player does:** Walks around the room (`A`/`D`, `W` to climb onto a step), picks things up and puts them down (`E`, one item per hand: left mouse uses the right-hand item, right mouse the left-hand one), aims with the mouse, and does things:
- Take a jar off the rack, set it on the table, open or close its lid.
- Pick a specimen out with tongs or with bare hands (bare hands touch it).
- Tools on shelves: lighter, water flask, acid vial with dropper, scalpel, tongs, a steel bolt, a dead insect in a tin, a hand lamp, a magnifier (hold it near something to see a zoomed inset of what is under it), a gas sniffer (a handheld sensor that clicks faster near bad air; the only instrument, and it is a thing you hold), a fire blanket, a bucket.
- Put a jar in the cold locker and close it; take it out later.
- Turn the vent fan on or off; open or close the door.

**What can go wrong** (the point of the prototype)
- Fire spreads to the table, paper, the journal if it is left out, and the figure's sleeves (burn on that arm).
- Spores released in a closed room fill it; without the vent on they drift through an open door into the corridor (shown as a strip on the right) and settle there.
- Fumes from a heated specimen poison whoever breathes them, per body part as in E5 (at least: coughing, blurred vision, then downed).
- An exploding specimen breaks its jar and anything near it, injuring the hand holding it.
- Acid spilled on the floor eats into it and through boots.
- Bare-handed contact with some species causes a delayed reaction on that hand.
- Mistakes are not undone. `R` restarts the whole room.

**Rules**
- Each specimen gets 3 hidden traits rolled from the seed, from a pool such as: flammable, explodes when burnt, glows when wet, releases spores when disturbed, eats metal, dormant in cold, toxic fumes when heated, grows toward light, parasitises another species, irritates skin.
- Reactions are shown only in the world: in the jar, in the room's air, on the figure. No text ever names a trait.
- Spares are limited: the rack has 2 extra jars of each species.
- The journal is a physical book on a shelf. Pick it up and press `J` to open it full screen (the game pauses while reading or writing): free text in the Caveat font on pages the player creates and names, plus an optional sketch area. The game never writes in it. If it burns, it is gone for that run.
- At the end, a "field test" (button, outside the room fiction) poses three situations and the player picks an action; outcomes follow the real traits.

**Tunables:** traits per species, spares, trait pool size, fire spread rate, spore drift rate, fume toxicity, vent strength, delay of skin reactions, room size, seed.

**Readouts:** experiments run, specimens left, time. Body state only through the silhouette and how the figure moves.

**Done when:** a player learns at least two traits per species, and at least one test run ends in a self-inflicted accident that the player can explain afterwards.

### E5 — Fragile body

**Question:** Can injuries per body part be read at a glance and change how you play, without a health bar?

**Player does:** Runs a figure through a short hazard course (uses the sand sim, 240×120): a drop, an acid pool, a burning passage, a CO₂ hollow, falling sand.

**Rules**
- Body parts: head, torso, each arm, each leg. States: fine, hurt, useless.
- Effects: leg hurt → slow, no jump; leg useless → crawl. Arm hurt → slow climb; arm useless → cannot climb or carry. Torso hurt → air drains faster. Head hurt → vision narrows and blurs. Burns and acid act on the part that touched the material.
- Bleeding or gas exposure progress over time and end in "downed" (can crawl slowly, cannot act). There is no death in this prototype.
- A field kit has three items (splint, dressing, air canister), each fixing one kind of problem partly.
- The only UI for the body is the silhouette and the way the figure moves.

**Tunables:** thresholds for each injury, crawl speed, kit contents, how fast downed arrives, whether the silhouette is always shown or only on key press.

**Readouts:** none for body state (that is the test). Time, distance.

**Done when:** a watcher who cannot see the silhouette can tell from the figure's movement which part is hurt.

### E6 — Runner scouting

**Question:** Is driving a fast, fragile scout far from the base thrilling, and does the distance home weigh on you?

**Player does:** Drives the runner (a light buggy for one or two) in side view across a long generated landscape (height-map hills, rocks, soft ground, ravines, a few ruins on the horizon), starting from the base at the left. Throttle, brake, lean forward/back in the air (`W`/`S`) to land on the wheels, headlight on/off, and a survey ping. The driver can get out (`E`) to walk, and back in.

**Rules**
- **Speed is the point and the danger.** Air off a crest is easy. Landing nose- or tail-first or hitting a rock fast flips or crashes the runner: injuries per body part to the driver as in E5, and damage to a wheel or the engine that slows or stops it. No damage numbers; the runner's state shows in how it drives (pulls to one side, engine coughs).
- **Distance home:** fuel is small. A dial shows fuel and a rough "can I get back" mark that the player must judge, not a number. Running out means walking.
- **Radio range** from the base fades with distance; beyond it, the runner is alone (signal meter only).
- **Survey ping:** noisy, shows a rough blob of what lies below; marks a site on the player's own map only if the player places a marker by hand.
- **Ruins** on the way are just visible shapes; driving to one ends the run with "found" (E9 is what happens there).
- **Night and weather:** a short day cycle; at night only the headlight; dust storms cut visibility.
- **Creatures:** quiet driving with lights off at night passes unnoticed; headlight and high revs draw the E3 creature, which can chase a slow or stuck runner.
- Goal: find a ruin or a promising site and get back to base.

**Tunables:** top speed, acceleration, air control strength, crash thresholds (landing angle, impact speed), fuel capacity and burn, radio range, ping noise, terrain roughness, ruin distance, day length, weather frequency, creature attraction, seed.

**Readouts:** speed, fuel dial, signal meter, time of day. Body and runner state only through the silhouette and the way it drives.

**Done when:** players push their luck on the way out and get nervous on the way back, and we know roughly how far a ruin should be (in minutes of driving) for that to happen.

### E7 — Bringing it inside

**Question:** Does "the airlock only matters when there is a real hazard" produce good decisions, without becoming a chore?

**Reuses:** E10's base layout if it exists, otherwise E3's Crawler interior. The hatch is the base's door.

**Player does:** Returns from outside to the hatch several times in a row. Each return rolls a condition: clean, spores on suit, acid on boots, gas outside, an injured crewmate being carried, or low air.

**Rules**
- The hatch is a plain door by default: walk through, no wait.
- If contaminated, the player may cycle the airlock (takes time, costs power) or just walk in. Walking in brings the hazard inside: spores settle and later grow in the grow room, gas spreads room to room through open doors, acid damages the floor station nearest the hatch.
- Contamination is shown on the figure itself (dust on the suit, smoking boots), never as an icon.
- Pressure to skip comes from the roll: low air or a bleeding crewmate make the wait costly.

**Tunables:** cycle time, power cost, spread rates inside, how visible contamination is, frequency of each condition.

**Readouts:** time spent in airlock, interior contamination per room.

**Done when:** players sometimes skip the airlock on purpose and can say afterwards that it was a decision, not an oversight.

### E8 — Climbing and the unknown cave

**Question:** Is moving through a cave you cannot see, with tools for climbing, a puzzle worth solving, and does mapping it yourself feel good?

**Player does:** Explores a larger generated cave (uses the sand sim, 320×240) from a fixed entry point with a lamp, aiming to find three marked features (a glowing moss grotto, an underground lake, a deep drop) and get back out.

**Rules**
- Climbing: the figure grabs ledges automatically when jumping toward one; hangs from any rock edge (`Shift` held); climbs rough rock slowly with stamina that recovers when standing. Sand and mycelium are not climbable; wet rock is slippery (stamina drains twice as fast).
- Tools, each with limited count:
  - **Pitons** (`1`): hammered into rock to anchor the rope; the player can rappel down from a piton and climb back up the rope.
  - **Rope coil** (`2`): one rope, fixed length, can be left behind at an anchor and retrieved later.
  - **Flares** (`3`): thrown, light a large area for 20 seconds, ignite methane.
  - **Chalk marks** (`4`): leave a small glowing mark on the wall, visible from afar in the dark.
- The cave is black except where light reaches. Cells the player has seen become **remembered**: drawn faintly grey and colourless, and they do not update (sand that collapsed later still looks like it used to until seen again).
- A **map** (`M`) is a blank paper sheet. It shows nothing automatically. The player draws on it with the mouse (pencil, eraser) while the game is paused. A tunable switches to an "auto map" for comparison, which records remembered cells.
- Hazards: loose sand ceilings that collapse when you dig or land heavily nearby, falls (as in E1), a methane pocket.
- No air limit in this prototype; a soft timer (lamp battery) ends the run.

**Tunables:** stamina amount and recovery, climb speed, piton count, rope length, flare count and duration, lamp range, remembered-cell brightness, auto map on/off, cave size and branching, seed.

**Readouts:** stamina, items left, battery, features found (0/3), time.

**Done when:** players use pitons and chalk without being told to, and with auto map off at least some players draw a map that gets them back out.

### E9 — Places above ground

**Question:** Are surface sites outside the Crawler worth leaving the machine for, and do they feel different from caves?

**Player does:** Picks one of three site types from a menu and explores it on foot in side view, starting from the Crawler's hatch on the left. A radio range ring and a tether marker show how far from the Crawler you are.

**Site types** (each generated from the seed, each about three screens wide):
- **Dead outpost.** The remains of an earlier expedition, long empty: collapsed prefab rooms, a toppled radio mast, a buried vehicle. Doors that are jammed (need a pry bar, takes time), floors that sag and can give way into a cellar, and three things to find: a written log page (short, in-world text in the Caveat font, saying what they found and nothing about what happened to them), a salvageable part, a sealed sample case.
- **Salt flat with a sinkhole.** Wide open, almost no cover, bright and empty. Wind gusts push the figure. A white-out can roll in: visibility falls to a few metres and the Crawler's lights are the only landmark. The sinkhole at the far end opens into a short cave section (uses the sand sim, 160×100) as a teaser of what is below.
- **Surface bloom.** A field of tall fungal growth around a vent. Spore clouds drift with the wind and are only visible in certain light; standing in them fouls the suit filter. Some growths burst when touched. One rare specimen to bring back.

**Rules (all sites)**
- **Warmth** replaces air: it drains outdoors faster in wind and at night, and recovers only inside the Crawler. Low warmth slows movement and then fumbles item use.
- **Radio range:** beyond a distance, messages to the Crawler degrade (shown as a range ring and a signal meter; no real messages in this prototype).
- **Day/night:** a short cycle (a few minutes); at night only the lamp and the Crawler's floodlight give light, as in the caves.
- Returning to the hatch with anything found ends the run with a list of what was brought back and what happened to the body.
- The game never labels or explains finds; the log page is the only text.

**Tunables:** warmth drain (calm, wind, night), wind strength and gust frequency, white-out frequency, day length, radio range, floor collapse chance, spore density, site width, seed.

**Readouts:** warmth, distance from the Crawler, signal strength, time of day, finds carried.

**Done when:** each site type produces a different kind of decision about when to turn back, and players can say which site they would want to visit next time.

E9 is also where **tech** is found: in the dead outpost, at least one find is a physical piece of tech (for example a pump or a battery cell) that is heavy, so carrying it slows the figure and uses both hands.

### E10 — Base camp

**Question:** Is choosing a spot and setting up a base by hand satisfying, and does a base you built feel like home worth defending?

**Player does:** Arrives in the Crawler at a dig area (side view, a few screens wide, generated: firm ground, soft ground, a gas vent, a rock overhang, a creature trail marked only by scratches and droppings). Walks out and unpacks the base by hand: carries or drags each module out of the Crawler's hold, places it, and connects it (power cable from the generator, a walkway, the winch frame over a chosen spot for the shaft).

**Rules**
- Modules: generator, workshop, bunks, lab, storage, and the winch frame. Each is a physical crate that unfolds where it is placed; heavier ones need two people or the Crawler's crane (one bot helper is available to compare solo and duo).
- **Placement matters, and nothing tells you:** soft ground sinks a module slowly, tilting it; next to the vent, gas seeps into whatever is downwind; under the overhang, rocks fall in the night if the ground is disturbed by the drill; on the creature trail, the night visit comes straight through.
- Cables and walkways have fixed lengths, so spreading out costs cable; packing tight concentrates fire and gas risk.
- **One night:** after setup, a night passes in a few minutes. The generator is loud and lights attract the creature from E3. The player can keep watch from the winch frame, turn lights off, or sleep in the bunk (which skips time but leaves no one watching). Morning shows what happened.
- **Packing up** is the same work in reverse and is timed; a "leave now" event (a dust storm on the horizon) tests how fast it can be done.

**Tunables:** module weight, carry speed, cable length, sink rate on soft ground, vent spread, overhang collapse chance, creature attraction to noise and light, night length, packing time, bot helper on/off, seed.

**Readouts:** time spent setting up, time to pack, modules connected, power draw. What went wrong in the night is shown in the world, not listed.

**Done when:** two players argue about where to put the generator, and a bad placement causes a problem that night that they can trace back to it.

### E11 — Haul it home

**Question:** Is bringing a heavy, fragile piece of found tech back to base a tense trip in its own right?

**Reuses:** E9's dead outpost (or a simplified version) and E6's vehicle handling, with the hauler instead of the runner.

**Player does:** Starts at the outpost with the hauler parked nearby. Gets one heavy piece of tech (an engine block or a pump) out of a collapsed building and onto the hauler, straps it down by hand, and drives it back to base across E6-style terrain.

**Rules**
- **Getting it out:** the tech is too heavy to carry; it must be dragged (slow, noisy), levered with a pry bar, or lifted with the hauler's small crane (positioning the truck matters). Dragging it over rough floor can damage it.
- **Loading:** the bed has strap points. Unstrapped or badly strapped cargo shifts on slopes and bumps, and can fall off. Straps are placed by hand (click a strap point, then the cargo).
- **Driving loaded:** the hauler is slower, tips more easily on slopes, bogs in soft ground, and is loud. Rough driving rattles the tech, and enough knocks damage it.
- **Damage** shows on the object itself (dents, a hissing pipe, a loose part), never as a percentage. At the base, an end screen shows the object as it arrived.
- Optional second piece of cargo (a spare wheel) to test whether players overload the bed.
- Creature as in E3, attracted by noise.

**Tunables:** tech weight, drag speed and damage, crane reach, strap strength, hauler speed loaded/unloaded, tip angle, bog threshold, rattle damage, route length, creature attraction, seed.

**Readouts:** speed, fuel, distance to base. Cargo state only on the object.

**Done when:** players slow down on rough ground on their own because they are worried about the cargo, and at least one run loses or breaks the tech in a way they can explain.

### E12 — Workshop

**Question:** Is hands-on crafting from carried resources satisfying, and can it go wrong in interesting ways?

**Setting:** the base workshop in side view (uses the sand sim for air, fire and smoke, about 180×100 cells): a workbench, a forge with bellows, a vice, a water barrel, a vent flap, shelves, a door, and piles of raw resources carried in: rock, metal ore, sand, salt, fungal fibre, a methane canister, obsidian.

**Player does:** Walks the room and works by hand, one item per hand as in E4: puts ore in the forge, pumps the bellows, pulls glowing metal out with tongs, hammers it on the bench, quenches it in water; twists fungal fibre into rope; melts sand into glass in the forge; packs a flare from salt and methane; shapes obsidian into a blade.

**Rules**
- **No recipe list, no crafting menu.** The game does not tell you what makes what. A handful of results exist (rope, piton, flare, glass jar, filter, splint, blade, ladder rung); each is made by doing roughly the right physical steps with the right materials. Wrong steps produce junk, not an error message.
- **Recipes go in the journal.** A journal book is on a shelf (as in E4). The player writes down what worked.
- **Quality varies** with how well the steps were done (metal pulled at the right glow, quenched in time). Quality shows on the object (a well-made piton looks clean; a bad one is bent) and matters when used.
- **Danger:** the forge heats the room; methane near the forge ignites; hot metal burns the hand that grabs it without tongs; the vent flap closed with the forge running fills the room with smoke (E5-style breathing injuries); a dropped glowing piece starts a fire on the floor.
- **Found tech helps:** one optional piece of tech (a working blower, toggle "tech: blower") replaces the bellows and makes the forge hotter and faster, and also louder and more dangerous. This tests whether tech feels like a real upgrade.

**Tunables:** forge heat rate, glow window for good metal, quench window, rope twist time, glass melt time, methane ignition distance, smoke rate, burn threshold, tech on/off, seed.

**Readouts:** items made, time. Body state only through the silhouette and the figure.

**Done when:** a new player works out at least three recipes without help, writes them down, and burns themselves or fills the room with smoke at least once.

---

## Which prototype feeds which alpha

| Prototype | Feeds (see [alphas.md](alphas.md)) |
| --- | --- |
| D1 Sim tick | A0 |
| D2 Net pipeline | A0, networking budgets |
| D3 Reaction table | A0, A2 |
| E1 Winch descent | A1 |
| E2 Two ends of the line | A1 |
| E5 Fragile body | A1, A2 |
| E3 Driving blind | A3 |
| E7 Bringing it inside | A3 |
| E6 Runner scouting | A4 |
| E4 The lab room | A5 |
| E8 Climbing and the unknown cave | A1, A2 |
| E9 Places above ground | A4 |
| E10 Base camp | A3 |
| E11 Haul it home | A4 |
| E12 Workshop | A3, A4 |

Suggested order: D2 and E1 first (biggest technical risk, biggest fun question), then E2, E5, D1, D3, E3, E10, E4, E12, E6, E11, E7.
