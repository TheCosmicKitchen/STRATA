# Ash-can alphas

An ash-can alpha is a small, real, end-to-end build of STRATA in Godot that 2–4 friends can play over the network in one sitting. It exists to answer one question about fun. After the playtest, the scene code goes in the bin. Only what we learned, and the plain C# libraries underneath, carry forward.

This differs from the [prototypes](prototypes.md): those are single-player HTML toys for tuning one mechanic. An alpha is the actual engine, actual networking, actual friends.

## Rules for every alpha

1. **One question.** Each alpha has one fun question and a pass line written down before building starts.
2. **End to end.** It starts from a launch screen, a friend joins, you play a full session, it ends. No "imagine this part".
3. **Timeboxed.** Set a budget before starting (suggestion: two weeks of evenings). If it is not playable by then, cut from the build list, never extend.
4. **Ugly on purpose.** Rectangles for people, flat colours, no menus beyond host/join, no settings, no save games, placeholder or no sound unless the alpha's question is about sound.
5. **What is kept, what is binned.** Kept: the plain C# libraries (`Strata.Sim`, `Strata.Net`, and game-rule libraries) if they held up, plus data files. Binned: every Godot scene and script written for the alpha. If something in the bin was good, it gets rewritten properly later, not copied.
6. **Tunables are live.** A debug panel (host only) exposes the alpha's key numbers so they can be changed mid-session.
7. **Same playtest every time** (see below), so alphas can be compared.

## Playtest protocol

- 2–4 players, on separate machines, over the internet (not LAN), for the session length the alpha states.
- Record the session (screen and voice) with everyone's consent.
- The developer does not explain anything beyond the controls.
- Afterwards each player answers, alone and in writing, before any group talk:
  1. What was the best moment?
  2. When were you bored, and what were you doing?
  3. When were you confused?
  4. Did you want to swap roles with someone? Which role?
  5. Would you play again tomorrow? (yes / maybe / no)
- Also noted: every crash or desync, peak upload from the host, any moment people laughed or went silent.

**Decision after each alpha:** pass (move on), tune (same alpha, new numbers, one more session), or pivot (the question got a "no"; change the design before building further). Write the decision and the reason at the bottom of this file.

## The alphas

### A0 — Two in a hole

**Question (technical, not fun):** Can a host stream a live falling-sand cave to a friend over a normal home connection without it tearing?

**Build**
- Sim core with 8 materials (rock, sand, water, methane, fire, steam, smoke, acid), chunks, dirty rects. Threading optional.
- Chunk rendering with a colour shader. No lighting yet.
- Host/join by direct address. The state-streaming protocol from [networking.md](networking.md), including limits and handshake.
- Each player is a rectangle that walks, jumps and can dig one cell and place sand. A key dumps a large lake.

**Cut:** injuries, rope, lamp, Crawler, fungi, sound.

**Session:** 15 minutes of deliberately trying to break it.

**Pass line:** with one client, the host's upload stays under 300 kbit/s on average and under 500 kbit/s at peak during a flood; the client never shows a stale chunk for more than half a second; no crash from malformed or dropped packets. With three clients it remains playable.

**If it fails:** this is the project's biggest risk. Options, in order: lower the send rate, shrink the streamed area, send coarse far chunks, reconsider what clients simulate locally. Do not start A1 until A0 passes.

**Fed by:** prototypes D1, D2, D3.

### A1 — The line

**Question:** Is the split between the person below and the person at the winch tense and fun for both?

**Build**
- One fixed, hand-made cave under a static winch frame. No Crawler.
- Winch station: one player holds it to operate it. Rope with length and tension.
- Delver: lamp with battery, air supply, clip on/off the line, carry one sample.
- Darkness: unlit cells are black for everyone, including the operator, who sees only gauges and whatever a single floodlight at the shaft top shows.
- Injuries v0: legs and torso only; falling hurts; downed state; a second delver can carry the first.
- Radio: use the players' own voice chat, plus an in-game toggle that mutes game audio cues by depth. (Real radio simulation is out of scope.)
- Goal: bring up three samples. The session ends when samples are up or everyone below is downed.

**Cut:** fungi traits, hazards beyond falling and running out of air, Crawler, journal.

**Session:** 20–30 minutes, swapping roles halfway.

**Pass line:** at least half the players answer "yes" to playing again, and nobody names the winch role as the boring one.

**Fed by:** E1, E2, E5.

### A2 — Bad air

**Question:** Do the simulated materials create stories on their own?

**Build (on top of A1's libraries)**
- All first-draft materials and reactions from the design summary.
- Gas you cannot see without a sensor; fire that spreads through methane; acid that eats the floor; CO₂ pooling in hollows.
- Injuries v1: all body parts, burns, gas exposure, a three-item field kit.
- Two or three caves chosen at random from a small hand-made set, so players cannot memorise one.
- Tools: flare, digging tool, one rope anchor.

**Cut:** Crawler, fungi traits, journal, generation.

**Session:** 30 minutes.

**Pass line:** every session produces at least one unplanned event that players retell afterwards ("and then the whole pocket went up"), and deaths are understood, not felt as random.

**Fed by:** D3, E5.

### A3 — Base camp

**Question:** Does a base you set up yourself feel like home, and is the Crawler as its legs (slow, loud, consequential to drive) worth the trouble?

**Build**
- A walkable Crawler with helm, engine, lookout and winch. Stations work only while attended. Piloting and not-watching consequences as in E3; one creature drawn by noise and light.
- A short surface with two dig sites a few minutes apart.
- At a site, the base is unpacked by hand from the Crawler: generator, workshop, bunks, winch frame over the shaft. Placement consequences as in E10. The winch connects to the site's cave (reusing A2's caves).
- A minimal workshop (E12): rope, pitons and flares from resources brought up from the cave.
- Power budget shared by winch, lights and heating.
- One night at the base.
- The hatch rule: a plain door unless something hazardous is outside or on you; skipping decontamination then brings it in.
- Mid-session: pack up and move to the second site.

**Cut:** runner and hauler, found tech, lab, grow room, surface generation, weather beyond one event.

**Session:** 45–60 minutes, enough to set up, delve, survive a night and move once.

**Pass line:** crews of 2 and of 4 both get through a session without anyone idle for more than a couple of minutes; at least one argument happens about who stays at the base; players can name something about their base they would set up differently next time.

**Fed by:** E3, E7, E10, E12.

### A4 — Far trip

**Question:** Is going far from the base for tech, and bringing it home, worth splitting the crew for, and does found tech feel like real progress?

**Build (on top of A3)**
- A runner and a hauler parked at the base.
- A larger surface with one dead outpost (E9) a few minutes' drive away by runner and a long drive by hauler. Radio range ends partway.
- At the outpost: a few small finds and one heavy piece of tech (for example a pump that lets the shaft go deeper by clearing water, or a battery cell that powers the base silently at night).
- Getting the heavy tech home by hauler (E11), and installing it at the base by hand.
- Crash injuries in the runner, cargo damage in the hauler.

**Cut:** fungi traits, lab, more than one outpost, more than one piece of heavy tech.

**Session:** 60 minutes.

**Pass line:** the crew splits between base and expedition without being told to, the trip home with the tech is mentioned as a highlight, and after installing it they use it straight away to do something they could not do before.

**Fed by:** E6, E9, E11, E12.

### A5 — Grey lace

**Question:** Does learning fungal traits by experiment, with a journal the players write themselves, pay off across more than one session?

**Build**
- Four species with hidden traits rolled per world. The same world is used for two sessions on different days.
- Field experiments (burn, soak, feed) and the lab room at the base (E4): hands-on, with accidents.
- The journal: player-made pages, typed notes, a photo from a camera (found tech), or a hand drawing. Shared between players and kept between the two sessions. Recipes from the workshop go in it too.
- At least two traits matter underground: one that can kill you, one that can save you.

**Cut:** grow room, more than four species, drones.

**Session:** two sessions of 30–45 minutes, days apart.

**Pass line:** in the second session, players open the journal unprompted and act on something written in it.

**Fed by:** E4.

### A6 — The trip

**Question:** Does the whole loop hold together for an evening? This is the go/no-go build.

**Build:** A3, A4 and A5 joined: scout with the runner, move the base with the Crawler, set it up, split between delve, base and expedition, haul tech home, craft and install, study fungi, then pack up and move to a deeper second site. One generated element (cave layout from a seed) so it can be replayed.

**Session:** 60–90 minutes.

**Pass line:** players ask when they can play again. If they do not, stop and reassess the design before investing in production.

## Order and gates

A0 is a gate: nothing else starts until it passes. A1 is the first fun test and the most important one; if the split crew is not fun in A1, pillars 2 and 3 need rethinking before any base or vehicle work. A2 to A5 can be reordered depending on what A1 teaches, except that A4 needs A3's base. A6 comes last.

## Decision log

| Alpha | Date | Players | Result | Why, and what changed |
| --- | --- | --- | --- | --- |
| | | | | |
