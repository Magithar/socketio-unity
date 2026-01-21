# Reconnect Behavior

> How SocketIOUnity handles connection loss and automatic recovery

---

## Overview

The `ReconnectController` provides automatic reconnection with:
- **Exponential backoff** to avoid server overload
- **Single-loop guarantee** to prevent duplicate attempts
- **Lifecycle awareness** to respect intentional disconnects

---

## When Reconnection Triggers

### ✅ Automatic Reconnect

| Scenario | Trigger |
|----------|---------|
| Server closes connection | Transport `OnClose` event |
| Ping timeout | No server ping within `pingInterval + pingTimeout` |
| Network loss | WebSocket error followed by close |
| Transport error | `OnError` event from transport layer |

### ❌ No Reconnect

| Scenario | Reason |
|----------|--------|
| `Disconnect()` called | Intentional disconnect flag set |
| `Shutdown()` called | Clean application exit |
| Application quitting | Unity `OnApplicationQuit` detected |
| Already reconnecting | Idempotent guard prevents restart loop |

---

## Backoff Strategy

Delays follow exponential backoff with a 30-second cap:

| Attempt | Delay |
|---------|-------|
| 1 | 1 second |
| 2 | 2 seconds |
| 3 | 4 seconds |
| 4 | 8 seconds |
| 5 | 16 seconds |
| 6+ | 30 seconds (max) |

### Formula

```csharp
float delay = Mathf.Min(Mathf.Pow(2, _attempt), MaxDelay); // MaxDelay = 30
```

---

## State Machine

```
          ┌──────────────────────────────────────┐
          │                                      │
          ▼                                      │
      CONNECTED                                  │
          │                                      │
          │ (connection lost)                    │
          ▼                                      │
      RECONNECTING ──────(success)───────────────┘
          │
          │ (tick)
          ▼
      WAIT_FOR_DELAY
          │
          │ (delay elapsed)
          ▼
      ATTEMPT_CONNECT
          │
          ├──(fail)──→ WAIT_FOR_DELAY (next attempt)
          │
          └──(success)──→ CONNECTED
```

---

## API

### Automatic (Default)

Reconnection is automatic. No code needed:

```csharp
var socket = SocketIOManager.Instance.Socket;
socket.Connect("ws://localhost:3000");

// Reconnects automatically if disconnected
```

### Manual Control

```csharp
// Stop reconnection attempts
socket.Disconnect(); // Sets intentional disconnect flag

// This will NOT trigger reconnection
```

---

## Namespace Reconnection

When the root namespace reconnects, all registered namespaces automatically:

1. Receive the new connection
2. Re-send their `CONNECT` packets (with auth if configured)
3. Resume normal operation

```csharp
// Namespaces persist across reconnects
var admin = socket.Of("/admin", new { token = "secret" });

// After reconnect, /admin automatically reconnects with same auth
```

---

## Trace Logging

Enable `TraceLevel.Protocol` to see reconnect activity:

```
[Reconnect] Reconnect attempt 1 firing now
[Reconnect] Next reconnect in 1.0s (attempt 2)
[Reconnect] Reconnect attempt 2 firing now
[EngineIO] Handshake received (sid=abc123, pingInterval=25000ms)
```

---

## Key Implementation Details

### Single-Loop Guarantee

```csharp
public void Start()
{
    if (_enabled)
        return; // 🔥 CRITICAL — prevents restart loop

    _enabled = true;
    _attempt = 0;
    ScheduleNext();
}
```

### Reset on Success

```csharp
private void HandleEngineOpen()
{
    _reconnect.Reset(); // Clears enabled flag and attempt count
    // ...
}
```

### Time-Based Ticks

```csharp
public void Tick()
{
    if (!_enabled)
        return;

    if (Time.time >= _nextAttemptTime)
    {
        _attempt++;
        _reconnectAction.Invoke();
        ScheduleNext();
    }
}
```

---

## Edge Cases

| Case | Behavior |
|------|----------|
| Rapid disconnect/reconnect | Only one reconnect loop active |
| Auth failure on namespace | Namespace emits `connect_error`, no retry |
| Server restart | Client reconnects when server is back |
| Network blip | Fast recovery (first attempt after 1s) |
