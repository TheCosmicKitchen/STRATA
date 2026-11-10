# Design summary

This is an offline digest of the [design doc](https://claude.ai/artifact/K4oP3NdUav4ST6iTKnvCZp) as of 8 Nov 2026. If the two disagree, the doc wins.

## Pillars
1. **Vast isolation.** The world is enormous and indifferent. There is nobody else on the radio but your crew.
2. **The machine is home.** The Crawler is big and slow, needs many hands, and leaving it is always a decision.
3. **Split the crew.** Some go below while others hold the top, joined only by a winch line and a radio.
4. **Life is fragile.** Bodies take specific injuries instead of losing points from a health bar. Death is permanent for that character.
5. **Everything is matter.** The underground is a discrete cell simulation where materials react with each other.
6. **Learn by experiment.** Fungal traits are hidden and rolled for each world. The journal records only what someone has actually observed.

## Field journal and the lab
- **Pages:** the crew writes the whole journal. The game adds nothing on its own: no pages, no names, no traits.
- **Pictures:** a camera (carried, or mounted on a drone) takes an accurate photo for a page. Without one, a player draws the specimen in the book by hand. *Open: freehand canvas, stamps, or both, and whether drawings sync over the network.*
- **Traits:** traits go in only as notes a player writes, so anything nobody records is lost. Naming a species is also a player act.
- **Field experiments:** rough and risky, such as burning, soaking, feeding or eating a sample.
- **The lab:** only with a lab module aboard. The crew can run controlled experiments at the bench (heat, freeze, shock, acid, co-culture) and read things the field can't show, such as toxicity, growth rate and what a species feeds on. Lab results still have to be logged by a player.

## Core loop
Survey → Traverse → Anchor → split into the surface crew (winch, power, lights, radio) and the delve team (map, sample, experiment) → Extract → Process (lab, grow room, journal) → Upgrade → go deeper.

## The Crawler
The Crawler is a walkable side cross-section. A station only works while someone stands at it.
- **Bridge:** helm and comms
- **Engine and reactor**
- **Winch and drill mast**
- **Lab and grow room**
- **Workshop and airlock**
- **Bunks and galley**

Modules are rooms you bolt in, and hull space is the limit on what you can fit.

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
5. Anchor and descend: the winch cage connects to one underground site, with radio.
6. Fungi v1: four species, the lab bench and the journal.
7. A vertical slice played with friends, then a go/no-go decision.

## Open questions
- What is at the bottom?
- Are there other people?
- How does solo play work?
- How do respawns work?
- How many creatures are there?
- Does the surface run the simulation too?
- Is there one shared camera or one per player?
