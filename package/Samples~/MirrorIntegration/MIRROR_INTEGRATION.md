# Mirror + Socket.IO — Hybrid Architecture Guide

> Integrate socketio-unity with Mirror for backend-connected multiplayer games

---

## Who This Guide Is For

This guide is for developers who want to use **both** libraries in the same project:

- You have Mirror working for in-scene networking and want to add a backend for matchmaking, lobbies, or server-authoritative events
- You have socketio-unity handling your backend and want to add Mirror for local physics and transform sync

This guide assumes familiarity with both libraries individually. If you are new to socketio-unity, start with the Lobby sample (`../Lobby/README.md`).

---

## The Core Principle: Two Systems, Two Jobs

**socketio-unity** is a WebSocket client. It connects your game to a Node.js backend and handles everything that requires a server to broker: matchmaking, lobbies, session identity, authoritative scores, chat, and reconnection recovery. The backend is the source of truth; Socket.IO is the pipe.

**Mirror** is an in-scene networking stack. It synchronises transforms, physics, and animation state between players. It has no concept of a backend server and cannot validate gameplay events.

Mirror syncs simulation state between peers, but the backend remains **authoritative for all game outcomes** (scores, kills, round end). Mirror never validates — it only synchronizes.

These systems do not compete. A session that uses both has exactly one WebSocket to the backend (Socket.IO) and one Mirror transport connection. They share a single integration point: the moment a match is confirmed by the backend.

---

## Architecture

```
Socket.IO (Node.js Backend)          Mirror (In-Scene)
─────────────────────────────        ──────────────────
Matchmaking, lobbies, session ──►    StartClient() → dedicated server
Scores, kills, round state           NetworkTransform, Rigidbody
Reconnect recovery, host ID          NetworkBehaviour lifecycle
```

### Session Timeline

```
Client connects → /lobby namespace
Server emits player_identity (playerId, sessionToken)
Client creates or joins a room
Host emits start_match { sceneName, hostAddress }
Server broadcasts match_started { sceneName, hostAddress, kcpPort, wsPort }
────────────────────────────────────────────────────────────────────────────
MirrorGameOrchestrator.HandleMatchStarted fires
  → GameEventBridge.Subscribe()
  → ServerMode drives Mirror connection:
      DedicatedKCP:       KcpTransport.Port = kcpPort (reflection, walks MultiplexTransport children)
      DedicatedWebSocket: SimpleWebTransport.Port = wsPort (same)
      PeerToPeer:         host → StartHost(), others → StartClient(hostAddress)
  → Both layers active in parallel
────────────────────────────────────────────────────────────────────────────
Mirror: NetworkTransform syncs position/physics each frame
Socket.IO: /game namespace receives score_update, player_killed
────────────────────────────────────────────────────────────────────────────
Match ends → ReturnToLobby()
  1. StopHost() or StopClient()
  2. GameEventBridge.Cleanup()
  3. GameIdentityRegistry.Clear()
  4. LeaveRoom() emit → server skips grace timer
```

---

## ServerMode — Explicit Intent in the Inspector

`MirrorGameOrchestrator` exposes a `ServerMode` enum as an inspector dropdown. Set it once per scene — no code changes needed to switch between modes.

| Mode | Mirror role | When to use |
|---|---|---|
| `PeerToPeer` | Host runs `StartHost()`, others `StartClient(hostAddress)` | Local / LAN testing |
| `DedicatedKCP` | All clients `StartClient(hostAddress:kcpPort)` | Dedicated server, native builds (UDP) |
| `DedicatedWebSocket` | All clients `StartClient(hostAddress:wsPort)` | Dedicated server, WebGL builds (WebSocket) |

In `DedicatedKCP` / `DedicatedWebSocket` mode, the orchestrator casts the active transport and sets the port before calling `StartClient()`. If the port arrives as `0` (no env vars set), the transport's inspector-configured default is used unchanged — so local testing still works without env vars.

---

## Decision Table: What Goes Where

| Concern | Socket.IO | Mirror | Notes |
|---------|-----------|--------|-------|
| Matchmaking / lobby rooms | Yes | — | `create_room`, `join_room`, `match_started` |
| Session identity across reconnects | Yes | — | `LocalPlayerId` + `SessionToken` |
| Player transform / position | — | Yes | `NetworkTransform`; Mirror interpolates locally |
| Rigidbody / physics sync | — | Yes | `NetworkRigidbody` |
| Animation state | — | Yes | `NetworkAnimator` |
| Global chat | Yes | — | emit `chat` on `/game` namespace |
| Scores, kill feed, round state | Yes | — | Server-authoritative; backend validates |
| Host migration | Yes | — | Lobby server owns `hostId`, not Mirror |
| Reconnect recovery | Yes | — | `ReconnectConfig`, `SessionToken`, grace window |
| Anti-cheat / server validation | Yes | — | Only the Node.js backend can be trusted |
| WebGL browser support | Yes | Partial | socketio-unity fully supports WebGL; Mirror via `DedicatedWebSocket` mode works with SimpleWebTransport |

---

## Dedicated Server vs P2P Host Mode

**Recommended: Dedicated server mode (`DedicatedKCP` or `DedicatedWebSocket`).**

Set `MIRROR_SERVER_ADDRESS`, `MIRROR_KCP_PORT`, and `MIRROR_WS_PORT` as environment variables on the lobby server. `mirror-server.js` injects them into every `match_started` broadcast automatically — no client changes required. All players (including the one who clicked Start Match) connect as Mirror clients to the dedicated server.

```json
{ "sceneName": "GameScene", "hostAddress": "host.example.net", "kcpPort": 32367, "wsPort": 31869 }
```

**P2P host mode (`PeerToPeer`) — local / LAN only.**

The backend forwards the host's self-reported LAN IP to other players. Works reliably on the same machine or LAN. Not suitable for cross-internet play without NAT traversal or relay infrastructure.

```json
{ "sceneName": "GameScene", "hostAddress": "192.168.1.10", "kcpPort": null, "wsPort": null }
```

> ⚠️ P2P host mode requires NAT traversal or relay infrastructure (e.g. Steam Networking, Epic Online Services, or a dedicated TURN relay) for cross-internet use.

---

## `match_started` Payload Contract

```json
{
  "sceneName":   "GameScene",
  "hostAddress": "host.example.net",
  "kcpPort":     32367,
  "wsPort":      31869
}
```

| Field | Type | P2P mode | Dedicated mode |
|---|---|---|---|
| `hostAddress` | `string \| null` | Host's LAN IP | Dedicated server hostname |
| `kcpPort` | `int \| null` | `null` | Edgegap-assigned UDP port |
| `wsPort` | `int \| null` | `null` | Edgegap-assigned WebSocket port |

`MirrorGameOrchestrator` handles missing values:
- Port is `0` / `null` → transport uses its inspector-configured default port
- `hostAddress` is null in `PeerToPeer` → falls back to `"localhost"` in Editor/dev builds; returns to lobby in production
- `hostAddress` is null in `DedicatedKCP` / `DedicatedWebSocket` → always logs an error and returns to lobby

---

## New Scripts (MirrorIntegration Sample)

All scripts are in `Scripts/`. For full API docs, inspector wiring, prefab setup, and scene hierarchy, see [README.md](README.md).

### `GameIdentityRegistry.cs`
Static lookup: Mirror `netId (uint)` ↔ Socket.IO `playerId (string)`.
Call `Clear()` on `ReturnToLobby()` and on `store.OnDisconnected`.

### `PlayerIdentityBridge.cs`
`NetworkBehaviour` — attach to Mirror player prefab.
Responsibilities:
- Registers `netId ↔ playerId` in `GameIdentityRegistry` via `[Command]`.
- Syncs the player's display name (from `LobbyStateStore.CurrentRoom.players`) to all clients via a `[SyncVar]` hook. Falls back to `LocalPlayerId` if no display name is set.
- Updates the `nameLabel` (`TMP_Text`) on all clients when the name changes. Mirror SyncVar hooks do not fire on the host — `CmdSetDisplayName` calls the hook manually for the host case.

Uses `FindObjectOfType<LobbyStateStore>()` — Mirror spawned prefabs cannot hold inspector references to scene objects.

### `BillboardCanvas.cs` (from PlayerSync sample)
Attach to the Canvas child of the player prefab. Rotates the world-space canvas to face `Camera.main` every `LateUpdate`, keeping the name label readable from any camera angle.

### `GameEventBridge.cs`
`MonoBehaviour` — attach to a persistent manager in the game scene.
Subscribes to `/game` namespace (`score_update`, `player_killed`) via `lobbyNetworkManager.Socket.Of("/game")`.
Always caches `Action<string>` handler references and calls `Off()` in `Cleanup()` / `OnDestroy()`.

### `MirrorGameOrchestrator.cs`
`MonoBehaviour` — replaces `GameOrchestrator` for Mirror-enabled scenes.

Starts Mirror only after `store.OnMatchStarted` fires. The `ServerMode` inspector dropdown controls how Mirror connects:

- `PeerToPeer` — host calls `StartHost()`, others call `StartClient(hostAddress)`
- `DedicatedKCP` — all clients call `StartClient(hostAddress)` with `KcpTransport.Port` set from `kcpPort` (via property reflection, walks `MultiplexTransport` children)
- `DedicatedWebSocket` — all clients call `StartClient(hostAddress)` with `SimpleWebTransport.Port` set from `wsPort` (same)

Enforces the mandatory teardown order in `ReturnToLobby()`. Wire all fields via inspector — no singletons.

---

## Inspector Wiring (MirrorGameOrchestrator)

```
store                → LobbyStateStore (no singleton — must be wired)
lobbyNetworkManager  → LobbyNetworkManager (no singleton — must be wired)
mirrorNetworkManager → Mirror NetworkManager component
gameEventBridge      → GameEventBridge component
serverMode           → PeerToPeer / DedicatedKCP / DedicatedWebSocket
lobbyLayer           → Root GameObject of lobby UI
gameLayer            → Root GameObject of game world
```

Mirror's `playerPrefab` should include `PlayerIdentityBridge` and `MirrorPlayerController`.

**Player prefab wiring (`PlayerIdentityBridge`):**
```
nameLabel  → NameLabel (TextMeshPro UI on the Canvas child)
```

---

## Local Test Server

Use `TestServer~/mirror-server.js` to run the backend locally during development:

```bash
cd TestServer~
npm install
npm run start:mirror   # or: npm run dev:mirror (auto-restart on save)
```

The server runs on **port 3002** and exposes HTTP endpoints you can hit from a browser while Unity is in Play mode:

| URL | What it does |
|-----|-------------|
| `localhost:3002/test` | List active rooms and player IDs |
| `localhost:3002/test/score?roomId=X&playerId=Y&score=50` | Emit `score_update` to a room |
| `localhost:3002/test/kill?roomId=X&victimId=Y` | Emit `player_killed` to a room |
| `localhost:3002/test/round-end?roomId=X&winnerId=Y` | Emit `round_end` to a room |

**Dedicated server env vars (optional for local testing):**

Set these on the process before starting to simulate dedicated server mode locally:

```bash
MIRROR_SERVER_ADDRESS=localhost MIRROR_KCP_PORT=7777 MIRROR_WS_PORT=7778 npm run start:mirror
```

When unset, `match_started` emits `kcpPort: null` / `wsPort: null` and `PeerToPeer` mode works as normal.

---

## Graceful Shutdown — Mandatory Order

```csharp
// Step 1 — Mirror first (sends peer disconnect before socket closes)
if (NetworkServer.active) mirrorNetworkManager.StopHost();
else mirrorNetworkManager.StopClient();

// Step 2 — Clean /game namespace handlers
gameEventBridge.Cleanup();

// Step 3 — Clear netId ↔ playerId mappings
GameIdentityRegistry.Clear();

// Step 4 — Intentional leave (server skips 10-second reconnect grace window)
lobbyNetworkManager.LeaveRoom();

// NOTE: socket.Shutdown() is omitted — LobbyNetworkManager.OnDestroy() handles it.
// If returning to a lobby scene that reuses the socket, do not call Shutdown().
```

Reversing steps 1 and 4 is the most common mistake: if you call `Shutdown()` before `StopHost()`, Mirror tries to send disconnect packets over a closed transport.

---

## Common Pitfalls

**1. Starting Mirror before `match_started`**
Calling `StartHost()` from a button before the backend confirms a match creates an orphaned Mirror session the lobby server does not know about. Always start Mirror inside `HandleMatchStarted`.

**2. Disconnecting Socket.IO before `leave_room`**
The server starts a 10-second reconnect grace timer. Other players see the leaving player as "Reconnecting..." for 10 seconds. Always emit `leave_room` (via `LeaveRoom()`) before returning to lobby.

**3. Forgetting `Off()` in `GameEventBridge`**
The `EventRegistry` holds the delegate reference. Failing to `Off()` on destroy causes callbacks to fire against null Unity objects. Always cache handler references and call `Cleanup()` in `OnDestroy()`.

**4. Using Mirror `[Command]` for kill or score validation**
Mirror `[Command]` goes to the Mirror host — a client — which can be spoofed. Route all validation through Socket.IO. The backend emits the result; Mirror executes the visual effect.

**5. Double-spawning when migrating from pure Socket.IO**
If your project previously handled `player_join` / `player_leave` via Socket.IO (e.g. PlayerSync sample), disable those handlers during the Mirror game phase. Mirror owns all in-scene player object lifecycle.

**6. Reading `lobbyNetworkManager.Socket` inside Mirror callbacks**
Mirror callbacks (`OnStartServer`, `OnClientConnect`) may fire before other components' `Awake()`. Always acquire socket references in `Start()`, or set explicit Script Execution Order.

**7. Confusing Socket.IO RTT with Mirror RTT**
`socket.PingRttMs` measures round-trip to the Node.js backend.
`NetworkTime.rtt` measures round-trip between Mirror peers.
They are independent numbers measuring different network paths.

**8. `StartClient()` failure leaves player stranded**
If `StartClient()` fails (bad IP, NAT, relay timeout), Mirror fires `OnClientDisconnect`. Wire that event to call `ReturnToLobby()` so the player returns to the lobby instead of seeing a blank game screen.

**9. `ServerMode` / transport type mismatch**
`MirrorGameOrchestrator` uses reflection to walk the transport tree (including `MultiplexTransport` children) and find the right transport by type name. If `KcpTransport` / `SimpleWebTransport` is not present anywhere in the hierarchy, the inspector port is used unchanged. Check the warning log: `"KcpTransport not found in transport hierarchy; using inspector port"`. Ensure the `MultiplexTransport` on `NetworkManager` has both transports wired.

**10. Missing env vars on the deployed lobby server**
If `MIRROR_SERVER_ADDRESS` is not set on Render, `match_started` emits `kcpPort: null`. Clients in `DedicatedKCP` mode fall back to the transport's inspector port and attempt to connect to whatever `hostAddress` the host sent (their LAN IP in P2P). Set all three env vars and redeploy to enable dedicated server mode.

---

## Related Documentation

| I want to... | Go here |
|---|---|
| Understand socketio-unity's internal architecture | [ARCHITECTURE.md](../../../Documentation~/ARCHITECTURE.md) |
| See the lobby pattern this guide builds on | [Lobby/README.md](../Lobby/README.md) |
| See the LiveDemo orchestrator this guide extends | [LiveDemo/README.md](../LiveDemo/README.md) |
| Configure reconnection and the grace window | [RECONNECT_BEHAVIOR.md](../../../Documentation~/RECONNECT_BEHAVIOR.md) |
| Use Socket.IO in a WebGL build alongside Mirror | [WEBGL_NOTES.md](../../../Documentation~/WEBGL_NOTES.md) |
| Send binary payloads efficiently | [BINARY_EVENTS.md](../../../Documentation~/BINARY_EVENTS.md) |
