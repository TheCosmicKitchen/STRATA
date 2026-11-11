# Design summary

This is an offline digest of the [design doc](https://claude.ai/artifact/K4oP3NdUav4ST6iTKnvCZp) as of 8 Nov 2026, plus loop v2 (base, vehicles, crafting, found tech), adopted 10 Nov 2026 from [loop-v2.md](loop-v2.md) and not yet copied into the design doc. If the two disagree on anything else, the doc wins.

## Pillars
1. **Vast isolation.** The world is enormous and indifferent. There is nobody else on the radio but your crew.
2. **The base is home.** You build it where you dig, move it when you must, and defend it. The Crawler is its legs. Leaving it is always a decision.
3. **Split the crew.** There are more places to be than people: the base, the winch line down below, and vehicles far away, joined only by radio.
4. **Life is fragile.** Bodies take specific injuries instead of losing points from a health bar. Death is permanent for that character.
5. **Everything is matter.** The underground is a discrete cell simulation where materials react with each other.
6. **Learn by experiment.** Fungal traits are hidden and rolled for each world. The journal records only what someone has actually observed. The same goes for recipes and for working out what found tech does.

**Design rules that apply everywhere**
- **Hands-on, not menus.** Experiments, crafting, repairs and station work are things a character physically does with tools in the world. Big mistakes stay possible.
- **Trouble comes from choices.** Nothing breaks on a random timer and there are no Barotrauma-style breaches or leaks. Problems trace to how the crew drives, what nobody was watching, and creatures drawn by noise and light.

## Field journal and the lab
- **Pages:** the crew writes the whole journal. The game adds nothing on its own: no pages, no names, no traits.
- **Pictures:** a camera (carried, or mounted on a drone) takes an accurate photo for a page. Without one, a player draws the specimen in the book by hand. *Open: freehand canvas, stamps, or both, and whether drawings sync over the network.*
- **Traits:** traits go in only as notes a player writes, so anything nobody records is lost. Naming a species is also a player act.
- **Field experiments:** rough and risky, such as burning, soaking, feeding or eating a sample.
- **The lab:** only with a lab module at the base. It is a room you work in by hand (heat, freeze, cut, acid, co-culture, a sealed chamber), not a machine with a menu. It shows things the field can't, such as toxicity, growth rate and what a species feeds on, through things you see and hold (a gas sniffer, a magnifier). Lab accidents happen in your home. Results still have to be logged by a player.

## Core loop
Scout (runner) → move the base (Crawler) and set it up at the dig site → split the crew: delve below on the winch, hold the base, or go on an expedition to far ruins (runner, hauler) → bring back resources, specimens and tech → at the base: craft, install and repair tech, study, write the journal → go deeper here, or pack up and move on.

## Base and vehicles
**Base.** Set up where you excavate, unpacked from the Crawler as modules: generator, workshop, bunks and galley, lab, grow room, storage, and the shaft with its winch. What the Crawler can carry when packed is the limit on modules. Setting up and packing take real work. Where you put it matters: soft ground, vents, creature paths, how far it is from anything.

**Vehicles.** Driven from inside; a station only works while someone is at it. Bad driving and not watching have physical consequences in all of them.

| Vehicle | Good at | Bad at | Job |
| --- | --- | --- | --- |
| **Crawler** (excavator) | Carries the base, drill mast, heavy winch; very tough | Very slow, loud, thirsty; attracts creatures | Moving the base, opening deep shafts |
| **Runner** (fast scout) | Fast, quiet, long range | Fragile, tiny cargo, one or two seats; crashes injure | Scouting, reaching far ruins, radio relay |
| **Hauler** (truck) | Large cargo bed, towing | Needs firm ground; bogs and spills when loaded; loud | Bringing loot and tech home, towing wrecks |

There are more vehicles than players on purpose. Vehicles are found or rebuilt with tech, not bought; a run may start with only the Crawler.

**Crawler interior** (walkable side cross-section): helm and lookout, engine, winch and drill mast, and the packed base modules.

## Resources, crafting and tech
- **Resources** come from the world: rock, sand, metal ore, obsidian, water, fungal matter, methane, salt.
- **Crafting** turns them into everyday consumables and simple gear: rope, pitons, flares, filters, splints, fuel, jars, ladders and shaft supports. It is done by hand at the workshop, can fail, and can hurt you. Recipes are learned and written in the journal, not unlocked.
- **Tech is only found, never crafted:** pumps, drill heads, battery cells, radio sets, cameras, drones, engine blocks, heaters, lamp modules. It is a physical object you find (deep caves, dead outposts), carry or haul home, and install by hand. Found tech is often damaged; it can be repaired with crafted parts but never duplicated, and breaking it is permanent. Long-term progress is the pile of things the crew has dragged home.

**No airlock chores.** Unlike Barotrauma, the hatch is normally just a door. Sealing and decontamination matter only when a real hazard is outside or on you, such as poison gas, spores on your suit or acid on your boots. Skip the airlock then, and you bring the hazard inside.

## Simulation (first draft of the rules, as in the mockup)
**Materials:** rock, sand, water, methane, fire, steam, lava, acid, grey lace (mycelium), spores, lantern moss, obsidian, smoke and CO₂.

**Reactions:**
- Fire ignites methane, spores and mycelium. Burning mycelium bursts into spores.
- Water puts out fire and turns to steam.
- Lava and water make obsidian and steam.
- Acid dissolves rock and gives off CO₂. CO₂ is heavier than air and pools in low places.
- Mycelium creeps along rock and drinks water.
- Moss spreads only near water and gives off light.
- Spores seed new mycelium on wet rock.

Light spreads through open cells and is blocked by solid ones, so anything unlit is black.

## Tech decisions
- **Simulation core:** a C++ GDExtension with chunked grids (64×64), checkerboard multithreading and dirty rectangles. GDScript handles everything else.
- **Rendering:** each chunk is a texture, and a material-id-to-colour shader draws it. Light is calculated on a separate pass.
- **Collision:** marching squares builds a collision polygon for each chunk. Pieces of solid that break loose become RigidBody2D debris.
- **Networking:** the host is authoritative and is the only machine that runs the simulation. Clients receive run-length-encoded chunk diffs at 10–20 Hz. There is no lockstep. The first transport is ENet, then GodotSteam.
- **Data:** materials, reactions and fungal traits live in data files.

## Roadmap
1. A sand sandbox: the C++ simulation with 8 materials, chunking, rendering and lighting.
2. Two players watching a flood stream from host to client. **This is the biggest risk, so test it first.**
3. Bodies: the character controller, rope, falls, air, the downed state and carrying a friend.
4. The Crawler on its own: walkable, with helm, engine and winch.
5. Base and descent: set up a base at one site, and the winch connects it to the underground, with radio.
6. Fungi v1: four species, the lab room and the journal.
7. Far trip: a runner finds tech, a hauler brings it home, and it gets installed.
8. A vertical slice played with friends, then a go/no-go decision.

The ash-can alpha plan in [specs/alphas.md](specs/alphas.md) is the detailed version of this roadmap.

## Open questions
- What is at the bottom?
- Are there other people?
- How does solo play work?
- How do respawns work?
- How many creatures are there?
- Does the surface run the simulation too?
- Is there one shared camera or one per player?
- Is the base fully packable, or can parts be left behind as outposts?
- Can tech be studied in the lab to craft simpler copies, or is it strictly finite?
- How many vehicles at the start, and how are more obtained?
- One shared fuel, or a different one per vehicle?
- Does a 2-player crew get a smaller fleet, or the same fleet with more left unattended?
- Does night or weather force everyone back to base, or can expeditions camp out?
