# Loop v2 proposal: base, vehicles, crafting, salvaged tech

Status: adopted 10 Nov 2026 and copied into [design-summary.md](design-summary.md), [specs/prototypes.md](specs/prototypes.md) (E6 rewritten, E10–E12 added) and [specs/alphas.md](specs/alphas.md) (A3 Base camp, A4 Far trip, A5 Grey lace, A6 The trip). Not yet copied into the claude.ai design doc. Kept as the record of why the loop changed; the knock-on list below is now done.

## What changes

The single Crawler that is home, workshop and digger becomes a small fleet around a base.

| Before | After |
| --- | --- |
| One Crawler carries everything | A **base** set up at the dig site, plus **three vehicle roles** |
| The Crawler is home | The base is home; vehicles are how you reach, dig and haul |
| Upgrades come from somewhere unspecified | **Crafting** from local resources for everyday things; **tech** only as physical objects found in the wild and brought back |

## The pieces

### Base
- Set up where you excavate: unpacked from the Crawler as modules (generator, workshop, bunks, lab, grow room, storage). Hull space limit becomes "what the Crawler can carry when packed".
- Setting up and packing up take real time and work, so moving the base is a decision, not a menu click.
- The base holds the dig shaft and the winch. The underground delve happens from here.
- The base is a place with consequences, in line with the "trouble comes from your choices" and hands-on rules: a badly placed base sits on soft ground, too close to a vent, or in a creature's path; an unwatched base attracts things at night; a fire in the workshop is a fire in your home.

### Vehicles
| Role | Strengths | Weaknesses | Main job |
| --- | --- | --- | --- |
| **Crawler** (excavator) | Carries the base, drill mast, heavy winch; survives anything | Very slow, loud, thirsty; attracts creatures | Moving the base, opening deep shafts |
| **Runner** (fast scout: a light buggy or bike) | Fast, quiet, long range | Fragile, tiny cargo, one or two seats; a crash is serious (injuries per body part) | Surveying, reaching far ruins, finding tech, radio relay |
| **Hauler** (truck) | Big cargo bed, towing, medium speed | Needs roads or firm ground; bogs easily when loaded; loud | Bringing loot and tech back; towing a broken runner |

- Each vehicle is driven from inside, and the "consequences of piloting and not watching" from E3 apply to all three, scaled: the runner flips, the hauler's load shifts and spills, the Crawler shakes its own crew.
- More vehicles than players is intended. With 2–4 crew, someone is always choosing what to leave unattended: the base, a parked hauler, the shaft.
- Vehicles are found or rebuilt, not bought (see tech). A run might start with only the Crawler.

### Resources and crafting
- **Local resources** come from the world itself, mostly the falling-sand underground and the surface: rock, sand, metal ore, obsidian, water, fungal matter, methane, salt.
- **Crafting** turns those into everyday consumables and simple gear: rope, pitons, flares, filters, splints, fuel, glass jars, simple structures (ladders, supports for a shaft).
- Crafting is hands-on, per the embodied rule: you carry materials to the workshop, place them, work them with tools. Recipes are learned by the crew, not unlocked; the journal is where you write them down.
- Crafting can fail and can be dangerous (a forge fire, methane in a confined workshop).

### Tech
- **Tech is only found, never crafted.** Examples: a working pump, a drill head, a battery cell, a radio set, a camera, a drone, an engine block, a filter mask, a heater, a lamp module.
- Tech is a physical object. Someone has to find it (deep in a cave or at a dead outpost), carry it out (heavy items need the winch, a second person, or the hauler), bring it back to base, and install it by hand.
- Found tech is often damaged; it can be repaired with crafted parts but not duplicated. Breaking it is permanent loss.
- Tech is where long-term progression lives: the crew's capability is literally the pile of things they have dragged home.
- Where tech comes from is a lore hook (earlier expeditions, something older) without breaking "nobody else on the radio".

## The new loop

```
Scout (runner) ──> find a site or a ruin
      │
      ▼
Move base (Crawler) ──> set up at the dig site
      │
      ├──> Delve (winch, underground) ──> resources, specimens, buried tech
      │
      ├──> Expedition (runner / hauler) ──> far ruins ──> tech, haul it back
      │
      ▼
Base: craft from resources, install and repair tech, study specimens, write the journal
      │
      ▼
Go deeper here, or pack up and move on
```

Splitting the crew now happens on three axes: base vs. below (the winch line), base vs. away (radio range), and who drives what.

## What this does to the pillars

| Pillar | Change |
| --- | --- |
| 1 Vast isolation | Stronger: runners go far from everyone |
| 2 The machine is home | **Reword** to "The base is home": built, moved, defended. The Crawler is the base's legs |
| 3 Split the crew | Stronger: more places to be than people |
| 4 Life is fragile | Unchanged; vehicles add crash injuries |
| 5 Everything is matter | Unchanged; now also the source of crafting resources |
| 6 Learn by experiment | Extends to recipes and to working out what found tech does |

## Knock-on changes to the specs

These are proposed, not yet made.

**Prototypes** ([prototypes.md](specs/prototypes.md))
- E3 Driving blind: keep, as the Crawler. Later variants for runner and hauler reuse its consequence rules.
- E6 Surface traverse: becomes **runner scouting**: fast, fragile, long range, survey pings, a crash risk.
- E9 Places above ground: keep; it is where tech is found.
- New **E10 Base camp**: pick a site, unpack modules by hand, place them, keep it running through one night with one person.
- New **E11 Haul it home**: find one heavy piece of tech at a ruin, get it onto the hauler, drive it back without losing it or the truck.
- New **E12 Workshop**: hands-on crafting from carried resources, with ways to get hurt.

**Alphas** ([alphas.md](specs/alphas.md))
- A0–A2 unchanged (network, the line, bad air).
- A3 "Home" becomes **base and Crawler**: set up base at one site, delve from it, one move to a second site.
- New alpha between A3 and A5: **Far trip**: runner finds tech, hauler brings it back, it gets installed and changes what the crew can do.
- A5 The trip: includes the full fleet and at least one piece of tech that matters.

## Open questions

1. Is the base fully packable into the Crawler, or can parts be left behind (an outpost network)?
2. Can tech be studied to craft simpler copies (a reason for the lab), or is it strictly finite?
3. How many vehicles at the start of a run, and how are more obtained (found wrecks repaired with tech)?
4. Fuel: one shared resource, or does each vehicle burn something different?
5. Does a 2-player crew get a smaller fleet, or the same fleet with more left unattended?
6. Does night or weather force everyone back to base, or can expeditions camp out?
