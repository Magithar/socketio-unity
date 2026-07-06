# Configuration Variables and Scripting Defines

> All tuneable config and scripting defines across the package and test servers.

---

## Unity Scripting Defines

These are set in `Project Settings → Player → Scripting Define Symbols` or in `.asmdef` files. None are required for basic functionality.

| Define | Effect | Risk if enabled | Risk if missing |
|--------|--------|-----------------|-----------------|
| `SOCKETIO_PROFILER` | Enables `ProfilerMarker` sampling (~20ns overhead per marked call). Used in `ProfilerMarkers.cs`. | Very low (minor CPU overhead) | No profiler markers in Unity Profiler |
| `SOCKETIO_PROFILER_COUNTERS` | Enables real-time counters (bytes sent/received, packets, pending ACKs, active namespaces, throughput) via `SocketIOProfilerCounters`. Also requires `UNITY_2020_2_OR_NEWER`. | Very low (float math per packet) | No live counter data in diagnostics overlay |

No defines are secret or environment-sensitive.

---

## TestServer~ Environment Variables

The Node.js test servers in `TestServer~/` read environment variables at startup. All have sensible defaults for local development.

### Common to all servers

| Variable | Default | Used by | Purpose |
|----------|---------|---------|---------|
| `PORT` | `3000`–`3002` (per server) | All servers | HTTP + Socket.IO listen port |
| `NODE_ENV` | `development` | All (implicit) | Not explicitly read; affects socket.io logging |

### mirror-server.js (port 3002)

| Variable | Default | Purpose |
|----------|---------|---------|
| `PORT` | `3002` | Listen port |
| `MIRROR_SERVER_ADDRESS` | Host's LAN IP (auto-detected via `os.networkInterfaces()`) | Dedicated Mirror server address forwarded in `match_started` payload to clients; overrides the P2P host client's LAN IP when dedicated server mode is used |
| `MIRROR_KCP_PORT` | `0` (disabled) | KCP transport port for dedicated server; forwarded in `match_started` |
| `MIRROR_WS_PORT` | `0` (disabled) | WebSocket transport port for dedicated server; forwarded in `match_started` |

### basicchat-server.js (port 3002)

| Variable | Default | Purpose |
|----------|---------|---------|
| `PORT` | `3002` | Listen port |

### lobby-server.js (port 3001)

| Variable | Default | Purpose |
|----------|---------|---------|
| `PORT` | `3001` | Listen port |

### playersync-server.js (port 3003)

| Variable | Default | Purpose |
|----------|---------|---------|
| `PORT` | `3003` | Listen port |

### livedemo-server.js

| Variable | Default | Purpose |
|----------|---------|---------|
| `PORT` | `3000` | Listen port |

### server.js (binary/auth test server)

| Variable | Default | Purpose |
|----------|---------|---------|
| `PORT` | `3000` | Listen port |

---

## ReconnectConfig Runtime Parameters

These are set by game code at runtime (not environment variables). Documented here because they affect resilience behavior in production.

| Field | Default | Range | Effect |
|-------|---------|-------|--------|
| `initialDelay` | `1.0f` | seconds | Delay before first reconnect attempt |
| `multiplier` | `2.0f` | — | Backoff multiplier per attempt |
| `maxDelay` | `30.0f` | seconds | Maximum delay between attempts |
| `maxAttempts` | `-1` | -1 = unlimited | Stop reconnecting after N failures |
| `autoReconnect` | `true` | bool | Enable/disable automatic reconnection |
| `jitterPercent` | `0.0f` | 0–0.5 | Random ±% jitter on delay (prevents thundering herd) |

**Preset configs available:** `ReconnectConfig.Aggressive()`, `ReconnectConfig.Conservative()`, `ReconnectConfig.Default()`.

---

## No secrets

This is a client-side library. It has no API keys, database credentials, service tokens, or server secrets in the package itself. Auth data passed via `SocketIOClient.Of(ns, authPayload)` is game-owned — the library treats it as an opaque object serialized to JSON.

The test servers (`TestServer~/`) have no secret config: all CORS is `origin: '*'` and there is no authentication, rate limiting, or abuse protection. They are development-only tools.
