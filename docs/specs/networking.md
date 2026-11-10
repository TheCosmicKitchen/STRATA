# Networking spec (safe peer-to-peer)

Status: draft for alpha A0. Numbers marked *(to measure)* are starting guesses to be replaced by results from prototype D2 and alpha A0.

## 1. What "peer-to-peer" means here

One player hosts on their own machine; up to three friends connect to that player. There is no dedicated server. The shape is a star: clients talk only to the host, never to each other.

```
client ─┐
client ─┼─ host (runs the simulation and all rules)
client ─┘
```

The host is a player too, so the host's machine is both server and game.

## 2. What "safe" means here

STRATA is a co-op game played with friends, so the aim is not anti-cheat. The aim is that joining or hosting a game can never harm a player's machine, expose more about them than needed, or let one peer wreck the session for the others by accident or with little effort.

**Trust model**

| Party | Trusted to | Not trusted to |
| --- | --- | --- |
| Host | Decide everything about the world | Send well-formed data. A client must survive a hostile or buggy host. |
| Client | Report its own player's inputs | Decide anything about the world, other players, or its own injuries and inventory |
| The network | Nothing | Deliver, order, or keep packets intact |

**Goals, in priority order**
1. No remote code execution, file access or resource loading triggered by network data, in either direction.
2. No crash, hang or memory exhaustion from malformed, oversized or flooded messages.
3. Players' IP addresses are not exposed to each other in the shipped game.
4. Only invited people can join a session.
5. A misbehaving client cannot change world state beyond what its own player could legitimately do.

**Non-goals:** preventing the host from cheating (they own the world), hiding information from a client that modifies its own game (a client could reveal dark cells it has been sent), competitive integrity.

## 3. Layers and seams

All protocol logic lives in a plain C# library, `Strata.Net`, with no Godot types. It talks to the outside through one small interface:

```
ITransport
  Send(peerId, channel, reliable, bytes)
  event Received(peerId, channel, bytes)
  event PeerConnected(peerId) / PeerDisconnected(peerId, reason)
  Disconnect(peerId)
```

Implementations: `EnetTransport` (development), `SteamTransport` (shipping), `LoopbackTransport` (tests, with configurable delay, loss and reordering). Swapping transports must not touch anything above this line.

Consequences of this seam:
- **No Godot high-level RPC and no Godot Variant serialisation for gameplay.** Godot is used only as a pipe for byte arrays. Object decoding stays disabled everywhere. This removes the largest class of engine-level network risks and lets the protocol be unit-tested and fuzzed without the engine.
- Every message is a hand-written binary layout with a reader that checks bounds on every field.

## 4. Transports

| | ENet direct | Steam relay |
| --- | --- | --- |
| Used for | Development, alphas A0–A2 | Later alphas and release |
| How friends connect | Host shares address and port; needs port forwarding or a VPN-style tool such as Tailscale | Steam invite or lobby; no router setup |
| IP exposure | Peers see the host's IP, host sees clients' | Hidden behind Valve's relay network |
| Encryption | Off by default. Enable DTLS for any session over the open internet | Built in |
| Identity | None; anyone with the address can try to connect | Steam account |

Rules:
- Direct ENet sessions are for people who already trust each other. The host screen says plainly that sharing the address shares their IP.
- The library choice for Steam in C# is open (GodotSteam's C# bindings are still beta, see the research notes). The `ITransport` seam is what makes that choice changeable later.
- No custom NAT punch-through or home-made relay servers.

## 5. Joining a session

1. Client connects. Until the handshake completes, the host accepts only a `Hello` from that peer and nothing else; the peer receives no world data.
2. `Hello` (client → host): protocol version, game build id, hash of the data files (materials, reactions), display name, and a join token.
3. Host checks, in order: session not full (max 4 including host), versions and data hash match, join token correct, peer not on the session's kick list. Any failure → `Reject(reason code)` and disconnect.
4. `Welcome` (host → client): the client's player id, world seed and parameters, current tick.
5. Host starts streaming chunks around the client's spawn; the client spawns when the first ring of chunks has arrived.

Details:
- **Join token:** on ENet, a random code shown on the host's screen (at least 6 characters from a 32-symbol alphabet), entered by the joiner. Three wrong tokens from one address → refuse that address for the session. On Steam, lobby membership (friends-only or invite-only) replaces the token.
- **Handshake timeout:** a peer that has not completed the handshake within 10 seconds is dropped.
- **Display names:** at most 24 characters, control characters and markup stripped, shown only through plain-text controls.
- The host can kick a player; a kicked peer cannot rejoin that session.
- Data files are never sent over the network. A mismatch is a refusal with a readable message ("host has different game data").

## 6. Channels

| Channel | Delivery | Carries |
| --- | --- | --- |
| 0 Control | Reliable, ordered | Handshake, join/leave, kick, session end |
| 1 Events | Reliable, ordered | Things that happen once: injury applied, item picked up, journal edit, station occupied |
| 2 Input | Unreliable, newest wins | Client inputs |
| 3 Entities | Unreliable, newest wins | Positions and state of players, rope, debris |
| 4 Chunks | Unreliable | World cell data |

Every message starts with a 1-byte type. Unknown type on a channel → count as a protocol violation (section 9).

## 7. Streaming the world

Only the host simulates. Clients hold a replica grid that is written only by chunk messages.

**Principle: send state, not deltas.** A chunk message contains the current cells of a rectangle, not a change relative to an earlier message. Any message can be lost, duplicated or arrive late without corrupting the replica; a newer message simply overwrites.

**Chunk message**
```
type        1 byte
chunk x, y  2 × int16
version     uint32   (host increments each time the chunk changes)
rect        4 bytes  (x, y, w, h inside the 64×64 chunk; w and h ≥ 1; x+w ≤ 64; y+h ≤ 64)
encoding    1 byte   (0 raw, 1 run-length)
payload     raw: w×h bytes, one material id per cell
            run-length: pairs (count 1–255, material id) whose counts sum to exactly w×h
```
The client drops a message whose version is older than what it already has for an overlapping area.

**What gets sent to whom**
- Interest area per client: chunks intersecting that client's camera view, plus a margin of one chunk. The client reports its view rectangle on the Input channel; the host clamps it to a maximum size, so a client cannot ask for the whole world.
- Each send tick, the host gives each client a byte budget and fills it in priority order: dirty chunks nearest the client's view centre first, then by time since last sent. What does not fit waits for the next tick.
- Healing: each send tick, one clean chunk from the interest area is resent in full, round-robin, so lost messages are repaired within a few seconds without acknowledgements.
- A chunk entering a client's interest area for the first time is sent in full.

**Per-cell data on the wire** is the material id only (1 byte). Visual variation (shade) is derived on the client from cell position, not sent. Cell lifetime and other internal sim fields stay on the host.

**Starting numbers** *(to measure)*

| | Value |
| --- | --- |
| Sim tick | 60 Hz |
| Chunk send tick | 15 Hz |
| Budget per client | 30 kB/s average, 60 kB/s burst |
| Max chunk message | 4,110 bytes (raw 64×64 plus 14-byte header) |
| Target staleness of a visible chunk | under 250 ms normally, under 500 ms in a flood |

With three clients the host uploads up to about 0.75–1.5 Mbit/s. If A0 shows that is too much for ordinary home connections, levers are, in order: lower send rate, smaller margin, coarser far chunks.

## 8. Players and other entities

- **Inputs** (client → host, 30 Hz): movement axes, buttons, aim, view rectangle, and a sequence number.
- **Own movement:** for the first alphas, each client moves its own character locally against its replica grid and reports position; the host accepts it only if it passes sanity checks (speed and acceleration limits, not inside solid cells, no teleport). On failure the host snaps the player back. This is simple and feels responsive; the cost is that a modified client could move in ways the checks allow. Acceptable for co-op with friends. Revisit if it causes disagreement between what host and client see around fast-changing terrain.
- **Everything with consequences is decided by the host:** injuries, air, death, item pickup and use, digging and placing cells, station use, rope and winch physics, fungal reactions. Clients send the intent ("use item in slot 2 toward this point"); the host checks it is possible (range, line of sight, cooldown, item actually held) and applies it.
- **Entity snapshots** (host → client, 15–20 Hz): id, kind, position, velocity, small state bits. Clients interpolate between snapshots.

## 9. Hardening rules

These are requirements, each one testable.

**Parsing**
1. Every read checks remaining length first. A short or over-long message is dropped whole.
2. Every count, size, index and enum from the network is range-checked before use. Material ids outside the table, chunk coordinates outside the world, rects outside 64×64, zero or overflowing run lengths → drop.
3. Run-length decoding writes into a fixed buffer of w×h and stops with an error if the runs do not sum to exactly w×h. Decoded size never depends on anything but the already-validated rect.
4. No message causes an allocation larger than a fixed per-message cap. No recursion driven by message content.
5. Floating-point values from the network are rejected if not finite, then clamped.
6. Parsing never throws out of the network layer: a bad message becomes a counted violation, not an exception in game code.

**Never driven by network data**
7. File paths, resource paths, scene names, script names, type names, URLs. Things are referred to by small integer ids looked up in tables built from local data.
8. Reflection, dynamic loading, expression evaluation, or Godot object/Variant decoding.
9. Text rendering with markup. All peer-supplied text (names, journal notes, chat) is length-capped and shown as plain text with BBCode disabled.

**Limits**

| Limit | Value |
| --- | --- |
| Peers per session | 4 including host |
| Max message size, any channel | 8 kB |
| Client → host messages | 120 per second |
| Client → host bytes | 16 kB/s |
| Journal note | 2,000 characters |
| Journal drawing | Fixed canvas (for example 256×192, 16 colours), sent as a bounded run-length image through the same checked decoder |
| Journal edits | 4 per second per client |
| Idle timeout | 15 seconds without any packet |

Exceeding a rate limit drops the excess. Repeated violations (malformed messages or sustained flooding; threshold about 20 within 10 seconds) disconnect the peer with a reason shown to the host.

**Both directions**
10. Clients apply the same parsing rules to data from the host. A client must not crash, hang or run out of memory whatever a host sends.
11. Clients ignore any message that only a host may send if it somehow arrives from elsewhere, and hosts ignore host-only message types from clients.

**Privacy**
12. The game sends nothing to any third party. No analytics or crash upload without an explicit opt-in.
13. Logs written to disk do not include other players' IP addresses.

## 10. When things go wrong

- **Client drops:** their character stays in the world as downed-in-place for a grace period (suggested 2 minutes) so they can rejoin into the same body; after that the host decides what happens to it. *(Design question: see open questions.)*
- **Host drops:** the session ends for everyone. No host migration. Clients show "connection to host lost". The world and journal exist only in the host's save.
- **Version mismatch:** refused at handshake with a clear message.
- **Bad connection:** the client shows a small indicator when the newest chunk or entity data is older than one second, so stale views are not mistaken for reality.

## 11. Testing

- **Unit tests** for every message: round-trip, and one test per validation rule.
- **Fuzzing:** feed random and mutated bytes into the parser for both roles. Pass means no exception escapes, no allocation above the cap, and bounded time per message. This runs without Godot.
- **Bad-link runs:** `LoopbackTransport` with delay, jitter, loss, duplication and reordering; assert that the replica converges to the host's grid within a set time after activity stops.
- **Hostile-peer script:** a test client that sends oversized messages, floods, wrong types and impossible inputs; the host must stay up and other clients must not notice.
- **Soak:** 60 minutes, four peers, scripted floods and fires; watch memory and upload.
- A0's pass line (in [alphas.md](alphas.md)) is the acceptance test for this spec.

## 12. Open questions

- Which C# Steam library, and when to switch from ENet.
- Whether journal drawings sync live or only when a page is closed.
- What happens to the character of a player who disconnects and does not return, given permanent death.
- Whether clients keep a copy of the journal or world after a session (today: no).
- In-game voice with radio effects versus relying on external voice chat.
- Whether client-reported movement survives contact with fast terrain changes, or the host must simulate player movement.
- Whether far chunks need a coarse representation, or the interest area is enough.
