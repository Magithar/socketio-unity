# LiveDemo — Lobby + PlayerSync Integration

An end-to-end multiplayer demo that combines the [Lobby](../Lobby/README.md) and [PlayerSync](../PlayerSync/README.md) samples into a single scene with seamless phase transitions.

---

## Demo

> _Screenshot or GIF here — lobby room filling up, match starting, players moving in real-time._

---

## What This Demonstrates

- **Multi-phase gameplay**: Lobby room creation/joining flows directly into real-time player movement
- **Layer-based scene management**: A single Unity scene with two activatable layers instead of scene loading
- **Dual-server architecture**: Lobby server (port 3001) + PlayerSync server (port 3000) running side by side
- **Graceful transitions**: Match start, leave game, and lobby disconnect all handled cleanly

---

## Quick Start

```bash
npm install

# Terminal 1 — lobby server
npm run start:lobby        # http://localhost:3001, namespace /lobby

# Terminal 2 — playersync server
node server-playersync.js  # http://localhost:3000, namespace /playersync
```

```
Open LiveDemo scene in Unity → Press Play → Enter name → Create or Join Room → Start Match
```

To test multiplayer: build a standalone or open a second Unity Editor instance.

---

## How It Works

The scene contains two root layers managed by `GameOrchestrator`:

```
LiveDemo Scene
  ├── LobbyLayer (active at start)
  │     LobbyNetworkManager, LobbyStateStore, LobbyUIController
  │     → Room creation, join-by-code, ready up, host controls
  │
  ├── PlayerSyncLayer (inactive at start)
  │     PlayerNetworkSync, PlayerSpawner, PlayerController
  │     → Real-time position sync, remote player interpolation
  │
  └── GameOrchestrator (always active)
        → Listens for match_started / lobby disconnect
        → Toggles layers on/off
```

### Flow

```
┌─────────────┐   host clicks    ┌──────────────┐
│  Lobby       │   Start Match    │  PlayerSync   │
│  Layer       │ ───────────────► │  Layer        │
│  (active)    │   match_started  │  (activates)  │
└─────────────┘                   └──────┬────────┘
       ▲                                 │
       │   leave game / lobby disconnect │
       └─────────────────────────────────┘
```

1. **Startup** — `GameOrchestrator.Awake()` activates LobbyLayer, deactivates PlayerSyncLayer
2. **Match start** — `LobbyStateStore.OnMatchStarted` fires → orchestrator hides lobby, activates PlayerSync layer → `PlayerNetworkSync.Start()` connects to the game server
3. **Leave game** — `ReturnToLobby()` calls `PlayerNetworkSync.StopGame()`, hides game layer, re-shows lobby
4. **Lobby disconnect during game** — `LobbyStateStore.OnDisconnected` fires → automatic return to lobby

---

## Architecture

```
         ┌────────────────────┐     ┌────────────────────┐
         │   lobby-server.js  │     │ server-playersync.js│
         │   port 3001        │     │   port 3000         │
         │   namespace /lobby │     │ namespace /playersync│
         └────────┬───────────┘     └────────┬───────────┘
                  │                           │
                  ▼                           ▼
         ┌────────────────────┐     ┌────────────────────┐
         │LobbyNetworkManager │     │ PlayerNetworkSync   │
         └────────┬───────────┘     └────────┬───────────┘
                  │                           │
                  ▼                           ▼
         ┌────────────────────┐     ┌────────────────────┐
         │  LobbyStateStore   │     │   PlayerSpawner     │
         └────────┬───────────┘     └────────────────────┘
                  │
                  ▼
         ┌────────────────────┐
         │ GameOrchestrator   │  ← bridges both systems
         └────────────────────┘
```

### Components

| Component | Layer | Role |
|-----------|-------|------|
| **GameOrchestrator** | Always active | Flow controller — toggles layers on match start / leave / disconnect |
| **LobbyNetworkManager** | Lobby | Connects to `/lobby` namespace, emits room actions |
| **LobbyStateStore** | Lobby | Single source of truth for room state, fires C# events |
| **LobbyUIController** | Lobby | View layer — room UI, player list, ready/start buttons |
| **PlayerNetworkSync** | PlayerSync | Connects to `/playersync` namespace, sends position at 20Hz |
| **PlayerSpawner** | PlayerSync | Spawns/removes remote player capsules |
| **PlayerController** | PlayerSync | WASD/touch input for local player (blue capsule) |
| **RTTDisplay** | PlayerSync | Shows network latency |
| **ConnectionStatusDisplay** | PlayerSync | Shows socket connection state |

---

## Inspector Wiring

Select the **GameOrchestrator** GameObject and assign:

| Field | Assign |
|-------|--------|
| Store | LobbyStateStore component |
| Lobby Layer | Root GameObject containing all lobby UI |
| Player Sync Layer | Root GameObject containing PlayerNetworkSync and game world |
| Player Network Sync | PlayerNetworkSync component in the PlayerSync layer |

All Lobby and PlayerSync components use the same inspector wiring as their standalone samples — see their respective READMEs for details.

---

## Scene Hierarchy

```
LiveDemo
  ├── Directional Light
  ├── Main Camera
  ├── Plane (play area)
  ├── GameOrchestrator
  ├── LobbyManager
  │     ├── LobbyNetworkManager
  │     ├── LobbyStateStore
  │     └── LobbyUIController
  ├── Canvas
  │     ├── LobbyLayer (active at start)
  │     │     ├── StatusText
  │     │     ├── RoomPanel
  │     │     ├── ReconnectLabel
  │     │     └── HintLabel
  │     └── PlayerSyncLayer (inactive at start)
  │           ├── RTTDisplay
  │           ├── ConnectionStatusDisplay
  │           └── PlayerNetworkSync
  ├── PlayerSyncManager
  │     ├── PlayerNetworkSync
  │     └── PlayerSpawner
  └── LocalPlayer (blue capsule)
        └── PlayerController
```

---

## Known Limitations

- No persistent room across match phases — leaving the game returns to lobby but does not rejoin the room
- PlayerSync server has no authentication (development only)
- No scene-based loading — both phases live in a single scene via layer toggling
- No in-game chat or lobby chat

---

## Prerequisites

This sample combines the Lobby and PlayerSync samples. Familiarize yourself with each first:

1. [BasicChat](../BasicChat/README.md) — Socket.IO fundamentals
2. [PlayerSync](../PlayerSync/README.md) — real-time position sync
3. [Lobby](../Lobby/README.md) — room management, reconnection, host migration

The LiveDemo adds the orchestration layer that ties them together.
