# Materials and interactions

Generated from `src/Strata.Sim/Materials.cs` and `Reactions.cs` by `dotnet run --project tools/Strata.Sim.Bench -- --materials-doc docs/specs/materials.md`. Do not edit by hand.

Every reaction and every flammable material below has an automatic test in `tests/Strata.Sim.Tests/InteractionTests.cs`.

## Solids

| | Material | Density | Burns | Light | Climb | Dig | Breathing | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| <span style="color:#504C48">■</span> | rock | 1 |  |  | yes | ×1 |  | Acid eats it slowly. |
| <span style="color:#CCC2AC">■</span> | grey lace | 1 | → fire + spores |  |  | ×0.5 |  | Creeps along rock and drinks water. Bursts into spores when it burns. Salt kills it. |
| <span style="color:#60E8C0">■</span> | lantern moss | 1 | → fire | 0.55 |  | ×0.4 |  | Glows. Spreads only near water. |
| <span style="color:#282230">■</span> | obsidian | 1 |  |  | yes | ×4 |  |  |
| <span style="color:#9A5E3A">■</span> | iron ore | 1 |  |  | yes | ×1.2 |  | Throws sparks when struck. Acid turns it into hydrogen. |
| <span style="color:#1A1818">■</span> | bedrock | 1 |  |  | yes | no |  | Cannot be dug. |
| <span style="color:#6E6660">■</span> | granite | 1 |  |  | yes | ×3 |  | Slow to dig, solid to stand under. |
| <span style="color:#46484E">■</span> | shale | 1 |  |  | yes | ×0.6 |  | Brittle. Once nothing holds it up it trickles grit, then gives way as gravel. |
| <span style="color:#8E8A7C">■</span> | limestone | 1 |  |  | yes | ×0.8 |  | Acid eats it fast and gives off CO2. |
| <span style="color:#343436">■</span> | basalt | 1 |  |  | yes | ×2 |  | Cooled lava. |
| <span style="color:#232122">■</span> | coal | 1 | → embers + smoke |  | yes | ×0.8 |  | Smoulders into embers for a long time. Digging it raises explosive coal dust. |
| <span style="color:#C8B43A">■</span> | sulfur | 1 | → fire + foul air |  |  | ×0.7 |  | Burns with a choking gas. |
| <span style="color:#D8D2CA">■</span> | salt | 1 |  |  | yes | ×0.8 |  | Water dissolves it into brine. Kills grey lace and slime. |
| <span style="color:#A8D4E8">■</span> | ice | 1 |  |  |  | ×0.7 |  | Melts near anything hot. Slowly freezes the water touching it. Slippery: can't be gripped. |
| <span style="color:#9FE0D8">■</span> | crystal | 1 |  | 0.22 | yes | ×1.5 |  | Glows faintly. Shatters into shards. Grows slowly in brine. |
| <span style="color:#8A6A52">■</span> | clay | 1 |  |  | yes | ×0.8 |  | Soaks up water and slumps into mud. |
| <span style="color:#3E8A72">■</span> | copper ore | 1 |  |  | yes | ×1.2 |  | Acid turns it into hydrogen. |
| <span style="color:#B07A6A">■</span> | cap fungus | 1 | → fire + spores |  |  | ×0.4 |  | Giant mushroom flesh. Sheds spores. Burns. |
| <span style="color:#6E5236">■</span> | roots | 1 | → embers |  | yes | ×0.6 |  | Hang from the soil and grow down towards water. Good to climb. |
| <span style="color:#7AD8F0">■</span> | glow worms | 1 |  | 0.6 |  | ×0.2 |  | Colonies on ceilings. Light the way. Die in smoke and bad air. |
| <span style="color:#7A5A3C">■</span> | timber | 1 | → embers + smoke |  | yes | ×1 |  | Built supports and walls. |
| <span style="color:#8A9098">■</span> | plate metal | 1 |  |  | yes | ×5 |  | Built walls. Acid turns it into hydrogen. |
| <span style="color:#F0A040">■</span> | sunstone | 1 |  | 0.5 | yes | ×1.5 |  | A stone that never cools. Boils water into steam forever, melts ice and snow, and slowly sets flammable things alight. Cryo kills its heat. |
| <span style="color:#0A0610">■</span> | void | 1 |  |  |  | no |  | A hole in the world. Eats whatever it touches, a little at a time, until it is spent. Crystal, bedrock and obsidian hold it. Sunstone and void annihilate. |
| <span style="color:#3FB8A8">■</span> | resonite | 1 |  | 0.15 | yes | ×1.2 |  | A ringing mineral. A blast nearby makes a whole vein of it go off in sympathy, carrying the explosion far through the rock. |

## Powders

| | Material | Density | Burns | Light | Climb | Dig | Breathing | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| <span style="color:#987E58">■</span> | sand | 20 |  |  |  | ×0.5 |  |  |
| <span style="color:#5E5A56">■</span> | gravel | 30 |  |  |  | ×0.5 |  | Heavy and steep. What collapsing shale becomes. |
| <span style="color:#5A4636">■</span> | dirt | 18 |  |  |  | ×0.4 |  | Turns to mud with water. |
| <span style="color:#7C7872">■</span> | ash | 4 |  |  |  | ×0.2 |  | Light; floats on water and smothers flames. |
| <span style="color:#2E2B2A">■</span> | coal dust | 6 | explodes |  |  | ×0.2 | ×2 | Explodes when lit. |
| <span style="color:#B8ECE4">■</span> | crystal shards | 22 |  | 0.08 |  | ×0.3 |  |  |
| <span style="color:#D8501E">■</span> | embers | 8 |  | 0.7 |  | ×1 |  | Glowing coals. Set flammable things alight. Burn down to ash. |
| <span style="color:#E8F0F4">■</span> | snow | 5 |  |  |  | ×0.2 |  | Fluffy and light. Melts near anything hot. |
| <span style="color:#A48CE0">■</span> | skydust | 3 | → glimmer | 0.1 |  | ×0.2 |  | Falls up. Piles in dunes on the ceiling. Water weighs it down into mud; fire turns it into glimmer. |

## Liquids

| | Material | Density | Burns | Light | Climb | Dig | Breathing | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| <span style="color:#26587C">■</span> | water | 10 |  |  |  | ×1 | ×8 | Puts out fire, dissolves salt, turns clay and dirt to mud, freezes slowly against ice. |
| <span style="color:#EC6022">■</span> | lava | 15 |  | 0.9 |  | no |  | Sets things alight, makes obsidian with water and basalt with ice, slowly crusts over. |
| <span style="color:#80C440">■</span> | acid | 11 |  |  |  | ×1 | ×6 | Eats rock and limestone into CO2, metal and ore into hydrogen. |
| <span style="color:#2E6F7A">■</span> | brine | 11.5 |  |  |  | ×1 | ×8 | Salty water. Sinks under fresh water, never freezes, grows crystals, leaves salt when boiled. |
| <span style="color:#4A3828">■</span> | mud | 13 |  |  |  | ×1 | ×9 | Thick and slow. Dries back to dirt in heat. |
| <span style="color:#2A2418">■</span> | oil | 8 | → fire + smoke |  |  | ×1 | ×8 | Floats on water. Burns fast and smoky. |
| <span style="color:#141012">■</span> | tar | 12.5 | → fire + smoke |  |  | ×1 | ×9 | Barely moves. Burns for a long time. |
| <span style="color:#B8BCC4">■</span> | quicksilver | 40 |  | 0.04 |  | ×1 | ×8 | Very heavy. Gives off poisonous vapour when heated. |
| <span style="color:#7AA43C">■</span> | slime | 12 | → ash | 0.05 |  | ×1 | ×9 | Creeps and digests anything living it touches. Salt kills it. |
| <span style="color:#9CE6FF">■</span> | cryo | 9 |  | 0.12 |  | ×1 | ×8 | A liquid colder than anything natural. Freezes water solid, quenches lava and fire, boils off into snow. With methane it makes cold fire. |
| <span style="color:#3A4A66">■</span> | mites | 13 |  | 0.06 |  | ×1 | ×9 | A creeping paste of tiny machines. Eats metal and ore to make more of itself, and dies back into plate metal when it starves. Lava melts it down, acid dissolves it, cryo stops it. |
| <span style="color:#7FD8D0">■</span> | ghostwater | 9.5 |  | 0.25 |  | ×1 | ×8 | Water that seeps straight through solid rock to the next open space below. Bedrock and crystal stop it. Mixing with ordinary water, it loses the trick. |

## Gass

| | Material | Density | Burns | Light | Climb | Dig | Breathing | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| <span style="color:#5C4670">■</span> | methane | 0.3 | → fire |  |  | ×1 | ×4 | Invisible until lit. Burns in a rolling front. |
| <span style="color:#FFB046">■</span> | fire | 0.6 |  | 1 |  | ×1 | ×6 |  |
| <span style="color:#A8B0B8">■</span> | steam | 0.4 |  |  |  | ×1 | ×2 | Condenses back to water on rock ceilings. |
| <span style="color:#D6CC9C">■</span> | spores | 0.8 | → fire |  |  | ×1 | ×3 | Drift. Seed new grey lace on wet rock. Flash-burn. |
| <span style="color:#3E3E40">■</span> | smoke | 0.5 |  |  |  | ×1 | ×5 |  |
| <span style="color:#4A604E">■</span> | CO2 | 1.6 |  |  |  | ×1 | ×6 | Heavier than air. Pools in hollows. You can't see it. |
| <span style="color:#6A7888">■</span> | hydrogen | 0.1 | explodes |  |  | ×1 | ×3 | Rises fast, gathers under ceilings, explodes when lit. |
| <span style="color:#6E6A3A">■</span> | foul air | 1.4 |  |  |  | ×1 | ×12 | Rotten-egg gas. Heavy and poisonous. Water absorbs it and turns to acid. |
| <span style="color:#8A8E96">■</span> | mercury vapour | 2.2 |  |  |  | ×1 | ×14 | Very poisonous. Settles back out as quicksilver. |
| <span style="color:#FFF0C0">■</span> | blast | 0.6 |  | 1 |  | no |  | An explosion front. Breaks weak rock, sets off other explosives. |
| <span style="color:#6FB8FF">■</span> | cold fire | 0.6 |  | 0.85 |  | ×1 | ×4 | A blue flame that burns by freezing. Spreads through methane and hydrogen like fire, but turns water and lava solid instead of boiling them. Meeting real fire, both go out in a burst of steam. |
| <span style="color:#F4E6A0">■</span> | glimmer | 0.9 |  | 0.7 |  | ×1 |  | Drifting motes of light. Glow worms puff it out when they eat spores. Harmless. |

## Reactions

Chance is per tick, per contact, at 60 ticks a second.

| A | B | Chance | A becomes | B becomes | |
| --- | --- | --- | --- | --- | --- |
| water | lava | 1 | steam | obsidian | Lava quenched by water turns to obsidian in a burst of steam. |
| brine | lava | 1 | steam | obsidian | Brine on lava boils off and leaves obsidian. |
| brine | fire | 0.5 | salt | steam | Boiling brine leaves salt behind. |
| ice | lava | 1 | water | basalt | Lava against ice: the ice melts, the lava sets to basalt. |
| ice | fire | 0.5 | water | steam | Fire melts ice. |
| ice | embers | 0.1 | water | ash | Embers melt ice and go out. |
| ice | steam | 0.05 | water | water | Steam melts ice and condenses. |
| water | ice | 0.0008 | ice | (unchanged) | Water touching ice slowly freezes. |
| water | embers | 0.5 | steam | ash | Water puts out embers. |
| quicksilver | lava | 0.05 | mercury vapour | (unchanged) | Lava boils quicksilver into poisonous vapour. |
| quicksilver | fire | 0.1 | mercury vapour | (unchanged) | Fire boils quicksilver into poisonous vapour. |
| quicksilver | embers | 0.03 | mercury vapour | (unchanged) | Embers boil quicksilver. |
| mud | lava | 0.2 | dirt | (unchanged) | Lava bakes mud dry. |
| mud | fire | 0.3 | dirt | smoke | Fire dries mud to dirt. |
| mud | embers | 0.05 | dirt | (unchanged) | Embers dry mud. |
| limestone | lava | 0.002 | CO2 | (unchanged) | Lava cooks limestone, giving off CO2. |
| fire | ash | 0.05 | smoke | (unchanged) | Ash smothers flames. |
| water | salt | 0.01 | brine | empty | Water dissolves salt into brine. |
| water | clay | 0.01 | empty | mud | Clay soaks up water and slumps into mud. |
| water | dirt | 0.02 | mud | mud | Water and dirt make mud. |
| water | foul air | 0.01 | acid | empty | Water absorbs foul air and turns to acid. |
| brine | crystal | 0.0005 | crystal | (unchanged) | Crystals grow slowly in brine. |
| acid | rock | 0.015 | CO2 | CO2 | Acid eats rock, giving off CO2. |
| acid | sand | 0.015 | (unchanged) | CO2 | Acid dissolves sand. |
| acid | limestone | 0.2 | (unchanged) | CO2 | Acid fizzes through limestone, giving off lots of CO2. |
| acid | granite | 0.002 | CO2 | CO2 | Acid barely marks granite. |
| acid | basalt | 0.008 | CO2 | CO2 | Acid eats basalt slowly. |
| acid | obsidian | 0.004 | CO2 | CO2 | Acid barely marks obsidian. |
| acid | iron ore | 0.05 | hydrogen | empty | Acid on iron ore gives off hydrogen. |
| acid | copper ore | 0.05 | hydrogen | empty | Acid on copper ore gives off hydrogen. |
| acid | plate metal | 0.03 | hydrogen | empty | Acid eats plate metal, giving off hydrogen. |
| acid | grey lace | 0.05 | (unchanged) | empty | Acid dissolves grey lace. |
| acid | lantern moss | 0.05 | (unchanged) | empty | Acid dissolves moss. |
| acid | cap fungus | 0.05 | (unchanged) | empty | Acid dissolves fungus. |
| acid | roots | 0.03 | (unchanged) | empty | Acid dissolves roots. |
| acid | timber | 0.02 | (unchanged) | empty | Acid eats timber. |
| acid | water | 0.002 | water | (unchanged) | Acid diluted by water becomes water. |
| grey lace | salt | 0.01 | ash | (unchanged) | Salt kills grey lace. |
| slime | salt | 0.2 | water | (unchanged) | Salt kills slime, leaving water. |
| slime | lantern moss | 0.02 | (unchanged) | slime | Slime digests moss. |
| slime | grey lace | 0.02 | (unchanged) | slime | Slime digests grey lace. |
| slime | cap fungus | 0.02 | (unchanged) | slime | Slime digests fungus. |
| slime | roots | 0.01 | (unchanged) | slime | Slime digests roots. |
| slime | glow worms | 0.05 | (unchanged) | slime | Slime eats glow worms. |
| glow worms | smoke | 0.1 | empty | (unchanged) | Glow worms die in smoke. |
| glow worms | foul air | 0.1 | empty | (unchanged) | Glow worms die in foul air. |
| glow worms | CO2 | 0.02 | empty | (unchanged) | Glow worms suffocate in CO2. |
| glow worms | fire | 0.5 | ash | (unchanged) | Fire kills glow worms. |
| mercury vapour | rock | 0.003 | quicksilver | (unchanged) | Mercury vapour condenses on cold rock. |
| cryo | water | 0.3 | (unchanged) | ice | Cryo freezes water solid. |
| cryo | lava | 1 | steam | obsidian | Cryo flash-quenches lava into obsidian. |
| cryo | fire | 0.8 | (unchanged) | smoke | Cryo snuffs fire. |
| cryo | methane | 0.2 | (unchanged) | cold fire | Cryo touching methane lights it with a cold blue fire. |
| cryo | brine | 0.1 | ice | salt | Cryo freezes brine, splitting it into ice and salt. |
| cryo | quicksilver | 0.1 | (unchanged) | plate metal | Cryo freezes quicksilver into a lump of metal. |
| cryo | sunstone | 0.3 | steam | rock | Cryo kills sunstone's heat, leaving ordinary rock. |
| cryo | mites | 0.05 | (unchanged) | plate metal | Cryo freezes mites dead into plate metal. |
| cold fire | fire | 1 | steam | steam | Cold fire and real fire cancel out in a burst of steam. |
| cold fire | water | 0.3 | (unchanged) | ice | Cold fire freezes water. |
| cold fire | lava | 0.3 | (unchanged) | obsidian | Cold fire freezes lava. |
| snow | fire | 0.5 | water | steam | Snow melts in fire. |
| snow | lava | 1 | steam | basalt | Snow on lava steams and crusts it. |
| snow | embers | 0.2 | water | ash | Snow puts out embers. |
| snow | steam | 0.1 | water | water | Steam melts snow. |
| snow | sunstone | 0.1 | water | (unchanged) | Sunstone melts snow. |
| water | sunstone | 0.05 | steam | (unchanged) | Sunstone boils water away, endlessly. |
| ice | sunstone | 0.1 | water | (unchanged) | Sunstone melts ice. |
| mud | sunstone | 0.05 | dirt | (unchanged) | Sunstone bakes mud dry. |
| quicksilver | sunstone | 0.02 | mercury vapour | (unchanged) | Sunstone boils quicksilver into vapour. |
| glow worms | spores | 0.05 | (unchanged) | glimmer | Glow worms eat spores and puff out glimmer. |
| water | skydust | 0.05 | empty | mud | Water soaks skydust into ordinary mud, which falls. |
| ghostwater | water | 0.005 | water | (unchanged) | Ghostwater mixed with water becomes plain water. |
| mites | plate metal | 0.03 | (unchanged) | mites | Mites eat plate metal and multiply. |
| mites | iron ore | 0.02 | (unchanged) | mites | Mites eat iron ore and multiply. |
| mites | copper ore | 0.02 | (unchanged) | mites | Mites eat copper ore and multiply. |
| mites | lava | 0.3 | plate metal | (unchanged) | Lava melts mites down into plate metal. |
| mites | acid | 0.1 | hydrogen | empty | Acid dissolves mites, giving off hydrogen. |
| void | sunstone | 1 | empty | empty | Void and sunstone annihilate each other. |

## Behaviour that isn't a pairwise rule

- **Fire** spreads to anything flammable by its flammability, turns water to steam, and burns out to smoke.
- **Embers** set flammable neighbours alight at half the rate of fire and burn down to ash.
- **Explosions** (lit coal dust or hydrogen) break weak solids into gravel, scatter powders, fill open space with fire and smoke, and set off other explosives nearby, so pockets go up in a chain.
- **Shale** holds while it sits on something or spans between solid neighbours. Once unsupported it trickles grit for one to four seconds, then gives way as gravel, which leaves its neighbours unsupported in turn.
- **Lava** sets flammables alight, sometimes flares, slowly crusts over into basalt where it meets open air, and moves only every third tick.
- **Grey lace** creeps over rock, drinks water, sheds spores, and won't grow next to salt. **Spores** drift and seed grey lace on wet rock.
- **Lantern moss** spreads only near water. **Roots** grow down towards water from where they hang. **Cap fungus** sheds spores.
- **Steam** condenses to water on rock ceilings. Heavy gases (**CO2**, **foul air**, **mercury vapour**) sink and pool; light ones rise.
- Still places are swept for slow reactions every 16 ticks, so a quiet pool still dissolves salt.
