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

### E3 — Crawler stations

**Question:** Does "a station only works while someone stands at it" create interesting juggling, or just walking?

**Player does:** Walks one figure through a side cross-section of the Crawler (three decks, ladders, six rooms as in the mockup). `E` uses the station you are standing at; leaving it stops it.

**Rules**
- Stations: helm (moves the Crawler toward a destination marker; progress bar), engine (must be stoked every so often or power falls), winch (holds a load; unattended it slips slowly), bunk (nothing yet).
- Things go wrong on timers: an engine fault, a slipping winch, a hull breach letting in dust. Each needs presence at a specific station.
- Up to three simple bot crewmates can be assigned to a station with number keys, to compare solo against crewed play. Bots only stand where told.
- Goal: get the Crawler to the marker with the winch load intact.

**Tunables:** walk speed, ladder speed, fault frequency, stoke interval, winch slip rate, number of bots (0–3), Crawler length.

**Readouts:** distance to go, power, load slip, seconds spent walking vs seconds spent at stations.

**Done when:** with 0 bots it is barely survivable, with 3 it is comfortable, and the walking-to-working ratio is under one half with defaults.

### E4 — Fungus bench

**Question:** Is discovering hidden traits by experiment satisfying when the game writes nothing down for you?

**Player does:** Has four unnamed specimens in jars on a bench. Drags a tool onto a jar: flame, water, cold, acid, a light, a dead insect, or another specimen. Watches what happens. Writes notes in a paper journal on the right (free text, Caveat font), on pages the player creates and names.

**Rules**
- Each specimen gets 3 hidden traits rolled from the seed, from a pool such as: flammable, explodes when burnt, glows when wet, releases spores when disturbed, eats metal, dormant in cold, toxic fumes when heated, grows toward light, parasitises another species.
- Reactions are shown only visually in the jar (small sand-sim chamber or simple sprite states); no text ever names a trait.
- Some tests destroy the specimen. Spares are limited.
- The journal is entirely the player's: no auto pages, no auto names, no checkmarks.
- At the end, a "field test" poses three situations ("the passage is full of specimen B and you have a flare") and the player picks an action; the outcome follows the real traits. This is the only feedback on whether the notes were right.

**Tunables:** traits per species, spares per species, trait pool size, seed, whether a sketch area is included on journal pages.

**Readouts:** experiments run, specimens left.

**Done when:** two different seeds produce species that need different experiments to tell apart, and a player's own notes are enough to pass the field test.

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

### E6 — Surface traverse

**Question:** Is driving a slow machine across empty ground atmospheric, and how long before it is boring?

**Player does:** Drives the Crawler in side view across generated terrain (height-map hills, soft ground, a ravine). Throttle up/down, a survey ping that reveals what is underground ahead as a rough blob, and "anchor" which only works on firm level ground.

**Rules**
- Fuel burns with throttle and slope. Soft ground slows and can bog the machine; getting out costs fuel.
- Weather rolls through: dust that cuts visibility, cold that raises fuel burn.
- The survey ping is noisy and costs power; three candidate sites are hidden along the route with different depth and "promise".
- Goal: choose a site and anchor with enough fuel left to leave again.

**Tunables:** Crawler top speed, fuel capacity, burn rates, ping noise, terrain roughness, weather frequency, route length, seed.

**Readouts:** fuel, speed, distance, ground firmness under the tracks.

**Done when:** we know roughly how many minutes of driving feel good before a decision is needed, as a number.

### E7 — Bringing it inside

**Question:** Does "the airlock only matters when there is a real hazard" produce good decisions, without becoming a chore?

**Reuses:** E3's Crawler interior.

**Player does:** Returns from outside to the hatch several times in a row. Each return rolls a condition: clean, spores on suit, acid on boots, gas outside, an injured crewmate being carried, or low air.

**Rules**
- The hatch is a plain door by default: walk through, no wait.
- If contaminated, the player may cycle the airlock (takes time, costs power) or just walk in. Walking in brings the hazard inside: spores settle and later grow in the grow room, gas spreads room to room through open doors, acid damages the floor station nearest the hatch.
- Contamination is shown on the figure itself (dust on the suit, smoking boots), never as an icon.
- Pressure to skip comes from the roll: low air or a bleeding crewmate make the wait costly.

**Tunables:** cycle time, power cost, spread rates inside, how visible contamination is, frequency of each condition.

**Readouts:** time spent in airlock, interior contamination per room.

**Done when:** players sometimes skip the airlock on purpose and can say afterwards that it was a decision, not an oversight.

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
| E3 Crawler stations | A3 |
| E7 Bringing it inside | A3 |
| E6 Surface traverse | A3 |
| E4 Fungus bench | A4 |

Suggested order: D2 and E1 first (biggest technical risk, biggest fun question), then E2, E5, D1, D3, E3, E4, E7, E6.
