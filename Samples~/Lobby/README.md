# Unity Multiplayer Lobby — Socket.IO

A production-style multiplayer lobby for Unity demonstrating reconnect recovery, host migration, session identity, and clean three-layer networking architecture.

---

## Demo

> _Screenshot or GIF here — players joining, host disconnecting, host migrating, player reconnecting._

---

## Features

- Room creation and join-by-code (6-character codes, e.g. `C9N7GR`)
- Persistent player identity — `playerId` separate from `socket.id`, survives reconnects
- Session token authentication — prevents player slot spoofing on reconnect
- 10-second reconnect grace window — room slot held while player is offline
- Host migration — automatically promotes next connected player when host leaves
- `ConnectionState` + `OnStateChanged` for reactive UI — no shadow bool tracking
- Full WebGL support — automatic transport detection via `TransportFactoryHelper`
- Trace-based structured server logs — per-player `traceId` stable across reconnects

---

## Why This Project Exists

Most Unity networking samples focus on a specific SDK or transport layer. This project focuses on the architecture underneath: reconnect recovery, host migration, session identity, and clean separation between transport, state, and UI. The Socket.IO layer is swappable — the patterns are the point.

---

## Quick Start

```bash
npm install
npm run start:lobby   # or: npm run dev:lobby (auto-restart)
```

```
Open LobbyScene in Unity → Press Play → Enter name → Create or Join Room
```

Server output:
```
🚀 Lobby server running on http://localhost:3001
🛰  Socket.IO namespace: /lobby
```

To test multiplayer: build a standalone or open a second Unity Editor instance.

---

## Architecture

Three clean layers — no layer crosses its boundary:

```
         ┌──────────────────────┐
         │   Socket.IO Server   │
         │   lobby-server.js    │
         └──────────┬───────────┘
                    │  room_state snapshots
                    ▼
         ┌──────────────────────┐
         │  LobbyNetworkManager │  transport layer
         │  namespace /lobby    │  emits & receives events
         └──────────┬───────────┘
                    │  ApplyRoomState / FireX
                    ▼
         ┌──────────────────────┐
         │    LobbyStateStore   │  single source of truth
         │  CurrentRoom         │  fires semantic C# events
         │  LocalPlayerId       │  diffs player lists
         │  SessionToken        │
         └──────────┬───────────┘
                    │  OnRoomStateChanged / OnPlayerJoined / etc.
                    ▼
         ┌──────────────────────┐
         │   LobbyUIController  │  view layer
         │   no socket access   │  reacts to store events only
         └──────────────────────┘
```

### Component Responsibilities

| Component | Role |
|-----------|------|
| **LobbyNetworkManager** | Transport layer. Manages `/lobby` namespace socket. Emits all client actions, receives events, feeds state into `LobbyStateStore` |
| **LobbyStateStore** | Single source of truth. Owns `CurrentRoom`, `LocalPlayerId`, `SessionToken`, `IsConnected`. Fires 8 semantic C# events. Handles player list diffing |
| **LobbyUIController** | View layer. Subscribes to store events only. Manages player row lifecycle via `_playerRows` dictionary and reconnect/rejoin coroutines |
| **RoomState** | Data model for the full room snapshot |
| **LobbyPlayer** | Data model for a single player entry |

---

## Reconnect Flow

```
Disconnect detected
  → store.SetConnected(false)
  → ReconnectPanel shown
  → _hadRoomBeforeDisconnect = true (if in a room)
  → server marks player status="disconnected"
  → server starts 10s grace timer

Player clicks Reconnect
  → networkManager.Reconnect()
  → ConnectToLobby() re-establishes socket

On reconnect:
  → store.SetConnected(true)
  → if _hadRoomBeforeDisconnect:
      → ReconnectSession(savedPlayerId, savedRoomId, savedToken)
      → server cancels grace timer, restores player slot
      → RejoinTimeout coroutine starts (5s client-side guard)

      ┌─────────────────────────────────┐
      │ room_state received within 5s   │
      │ → timeout cancelled             │
      │ → UI restored to room view      │
      └─────────────────────────────────┘

      ┌─────────────────────────────────┐
      │ 5s timeout expires              │
      │ → saved room ID cleared         │
      │ → return to lobby selection     │
      │ → "Room no longer available"    │
      └─────────────────────────────────┘

      ┌─────────────────────────────────┐
      │ reconnect_player ack = error    │
      │ (room gone, token invalid, etc) │
      │ → timeout cancelled immediately │
      │ → return to lobby selection     │
      │ → "Previous room no longer      │
      │    available"                   │
      └─────────────────────────────────┘

If 10s server grace expires before reconnect:
  → player removed from room
  → room_state broadcast to remaining players
  → client ReconnectSession ack returns error
  → HandleError fast-fail path triggers immediately
  → UI falls back to lobby selection (no 5s wait)
```

---

## Socket.IO Events Reference

| Event | Direction | Purpose |
|-------|-----------|---------|
| `create_room` | Client → Server (ack) | Create a new room; ack: `{ ok, roomId, playerId, sessionToken }` |
| `join_room` | Client → Server (ack) | Join existing room; ack: `{ ok, roomId, playerId, sessionToken }` |
| `reconnect_player` | Client → Server (ack) | Restore a session within grace window; sends `{ playerId, roomId, sessionToken }`; ack: `{ ok, roomId, playerId }` |
| `leave_room` | Client → Server (ack) | Intentional exit; no grace period |
| `player_ready` | Client → Server | Toggle or set ready state |
| `start_match` | Client → Server | Host only; emits `match_started` to room |
| `room_state` | Server → Client | Full authoritative room snapshot |
| `player_removed` | Server → Client | Player permanently removed; `{ playerId, name, reason }` where reason is `"left"` or `"reconnect_timeout"` |
| `match_started` | Server → Client | Host started the match; `{ sceneName }` |

---

## Room State Structure

```json
{
  "roomId": "C9N7GR",
  "hostId": "abc123def",
  "version": 4,
  "players": [
    { "id": "abc123def", "name": "jason", "ready": true,  "status": "connected" },
    { "id": "xyz789ghi", "name": "hello", "ready": false, "status": "disconnected" }
  ]
}
```

`status` is `"connected"` normally. During the 10-second reconnect grace period it becomes `"disconnected"` — the player row shows `(Reconnecting...)` and the ready icon turns yellow. After the grace period expires the player is removed entirely.

---

## Server Logs

The server emits structured logs with stable per-player trace IDs that survive socket reconnects:

```
[Lobby] 🔌 socket connected: sckAbC
[Lobby][T:a3f9bc][Room:C9N7GR][P:abc123def] 🏠 room created by "jason" socket=sckAbC
[Lobby][Room:C9N7GR] state broadcast v1 players=1 host=abc123def

[Lobby][T:ee1204][Room:C9N7GR][P:xyz789ghi] 🚪 "hello" joined socket=sckDeF
[Lobby][Room:C9N7GR] state broadcast v2 players=2 host=abc123def

[Lobby][T:a3f9bc][Room:C9N7GR][P:abc123def] ⚠️  "jason" disconnected — grace 10s started
[Lobby][Room:C9N7GR] state broadcast v3 players=2 host=abc123def
[Lobby][T:a3f9bc][Room:C9N7GR] 👑 host migrated abc123def → xyz789ghi
[Lobby][Room:C9N7GR] state broadcast v4 players=2 host=xyz789ghi

[Lobby][T:a3f9bc][Room:C9N7GR][P:abc123def] ♻️  "jason" reconnected socket sckAbC → sckGhI
[Lobby][Room:C9N7GR] state broadcast v5 players=2 host=xyz789ghi
```

`[T:traceId]` — short correlation ID generated at join, stable across reconnects.
`[Room:roomId]` — the 6-character room code.
`[P:playerId]` — the persistent player ID (omitted for room-level events like host migration).

---

## Session Identity

Each player is issued two credentials at join time:

| Credential | Stored in | Purpose |
|---|---|---|
| `playerId` | `PlayerPrefs` | Identifies the player slot across reconnects |
| `sessionToken` | `PlayerPrefs` | Proves ownership of that slot — prevents spoofing |

The token is generated server-side and returned in the `create_room` / `join_room` ack. It is never broadcast to other players and never included in `room_state`.

On `reconnect_player` the server rejects any request where `sessionToken` does not match:

```javascript
if (!sessionToken || sessionToken !== player.sessionToken)
    return ack({ ok: false, error: 'Invalid session token' });
```

Both credentials are cleared from `PlayerPrefs` on intentional leave. They are retained across crashes and app restarts so the reconnect window can be used after an unexpected exit.

> **Note:** This is a development-grade pattern. In production, tokens should be cryptographically random, stored server-side with expiry, and transmitted over TLS.

---

## Production Safeguards

- **Room version tracking** — `LobbyStateStore` ignores duplicate `room_state` packets via an internal version counter
- **Player list diffing** — store computes deltas and fires per-player events; UI never does a full list rebuild
- **Rejoin timeout** — 5s coroutine guard prevents the UI from hanging if the room is gone after reconnect
- **In-flight join guard** — prevents duplicate join emits during the reconnect sequence
- **Fast-fail on rejoin error** — `HandleError` immediately cancels the rejoin coroutine and returns to lobby selection when `reconnect_player` ack returns an error
- **Host migration via snapshot** — host transfers are reflected automatically in the next `room_state`; no separate event needed
- **Clean coroutine lifecycle** — `StopCoroutine` called on disconnect before starting new coroutines

---

## Connection Health (Heartbeat)

Socket.IO natively handles frozen connections via a ping/pong mechanism. The server sends a `ping` every `pingInterval` ms. If no `pong` arrives within `pingTimeout` ms the socket is forcibly disconnected, triggering the disconnect handler and starting the 10-second grace period.

```javascript
const io = new Server(httpServer, {
    pingInterval: 25_000,  // ms between pings  (default 25 000)
    pingTimeout:  20_000,  // ms to wait for pong (default 20 000)
});
```

With defaults, a frozen client is detected and its grace period starts within ~45 seconds. Reduce `pingInterval` for faster detection at the cost of more network traffic.

---

## WebGL Support

WebGL is fully supported. `TransportFactoryHelper.CreateDefault()` automatically selects `WebGLWebSocketTransport` in WebGL builds.

For production WebGL builds, change `serverUrl` from `http://localhost:3001` to your deployed server URL in the `LobbyNetworkManager` Inspector.

Your server must have CORS enabled (already included in `lobby-server.js`):

```javascript
cors: { origin: "*", methods: ["GET", "POST"] }
```

---

## Server Reference

The `lobby-server.js` is in `TestServer~/` at the project root. Run with:

```bash
cd TestServer~
npm install
npm run start:lobby   # or: npm run dev:lobby (auto-restart with nodemon)
```

See the full server: [`TestServer~/lobby-server.js`](../../TestServer~/lobby-server.js)

```javascript
/**
 * Lobby Server — Socket.IO multiplayer lobby for SocketIOUnity
 *
 * Features:
 *   - Room creation and joining via 6-character codes
 *   - Persistent player IDs — separate from socket.id, survive reconnect
 *   - 10-second reconnect grace period (player slot held on disconnect)
 *   - Host migration when host disconnects
 *   - Room cleanup when last player leaves
 *
 * DEVELOPMENT SERVER ONLY — no auth, rate-limiting, or abuse protection.
 */
```

> Full source: [`TestServer~/lobby-server.js`](../../TestServer~/lobby-server.js)

---

## Setup

### Requirements

- Unity 2020.3 or later
- Node.js 14+ and npm
- TextMeshPro package (auto-imported)

### Scene Hierarchy

```
LobbyScene
  - LobbyManagers
      - LobbyNetworkManager component
      - LobbyStateStore component
      - LobbyUIController component
  - Canvas
      - LobbySelectionPanel
          - PlayerNameInput
          - CreateRoomButton
          - JoinRoomCodeInput / JoinRoomButton
      - RoomPanel
          - RoomCodeText
          - PlayerListScrollView → Viewport → Content
          - ReadyButton / StartMatchButton / LeaveRoomButton / CopyRoomCodeButton
          - ConnectionStatusText
          - ReconnectPanel
              - ReconnectLabel / ReconnectButton
```

### Inspector Wiring

Select the **LobbyManagers** GameObject and configure **LobbyUIController**:

| Field | Assign |
|-------|--------|
| Network Manager | LobbyManagers (LobbyNetworkManager) |
| Store | LobbyManagers (LobbyStateStore) |
| Lobby Selection Panel | LobbySelectionPanel |
| Player Name Input | PlayerNameInput |
| Create Room Button | CreateRoomButton |
| Join Room Code Input | JoinRoomCodeInput |
| Join Room Button | JoinRoomButton |
| Room Panel | RoomPanel |
| Room Code Text | RoomCodeText |
| Leave Room Button | LeaveRoomButton |
| Copy Room Code Button | CopyRoomCodeButton |
| Ready Button | ReadyButton |
| Ready Button Label | ReadyButton → Text (TMP) child |
| Start Match Button | StartMatchButton |
| Player List Content | Content (inside Viewport) |
| Player Row Prefab | `Prefab/PlayerRowPrefab` asset |
| Connection Status Text | ConnectionStatusText |
| Reconnect Panel | ReconnectPanel |
| Reconnect Button | ReconnectButton |

### ScrollView Setup

**Viewport** — requires `Image` component enabled with alpha > 0, and `Mask` component (Show Mask Graphic: off).

**Content** — `Vertical Layout Group` (Child Force Expand Height: off, Spacing: 4) + `Content Size Fitter` (Vertical Fit: Preferred Size).

**PlayerRowPrefab** — `Layout Element` (Preferred Height: 40) with two named children:

| Name | Component | Purpose |
|------|-----------|---------|
| `NameText` | TextMeshProUGUI | Player name (+ `[Host]` tag) |
| `ReadyIcon` | Image | Green = ready, Gray/Yellow = not ready / reconnecting |

---

## Known Limitations

- No room capacity limit
- No spectator mode
- No lobby chat
- `start_match` logs to console only — scene loading is left to the integrating project
- Development server only — no auth, rate limiting, or abuse protection

---

## Prerequisites

New to Socket.IO Unity? Start with [BasicChat](../BasicChat/README.md), then [PlayerSync](../PlayerSync/README.md). This sample builds on those concepts and adds acknowledgement callbacks, namespace-based multi-server architecture, manual reconnection flow, and stateful room management.
