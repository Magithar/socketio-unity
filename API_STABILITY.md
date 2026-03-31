# API Stability for v1.0.0

This document defines the **stable public API surface** for socketio-unity v1.0.0+.

---

## ✅ Stable APIs (Won't Break)

These APIs are **guaranteed stable** for the v1.x lifecycle. Breaking changes will only occur in v2.0.0+.

### SocketIOClient

**Connection Management:**
```csharp
void Connect(string url)
void Disconnect()
void Shutdown()
void Dispose()
```

**Namespace Management:**
```csharp
NamespaceSocket Of(string ns, object auth = null)
```

**Event Handling (Default Namespace):**
```csharp
void On(string eventName, Action<string> handler)
void On(string eventName, Action<byte[]> handler)
void Off(string eventName, Action<string> handler)
void Off(string eventName, Action<byte[]> handler)
```

**Event Emission (Default Namespace):**
```csharp
void Emit(string eventName, object payload)
void Emit(string eventName, object payload, Action<string> ack, int timeoutMs = 5000)
```

**Properties & Events:**
```csharp
bool IsConnected { get; }
ConnectionState State { get; }        // v1.2.0+
event Action OnConnected
event Action OnDisconnected
event Action<SocketError> OnError     // v1.3.0+: typed error (was Action<string>)
event Action<ConnectionState> OnStateChanged  // v1.3.0+
```

**Reconnect Configuration (v1.1.0+):**
```csharp
ReconnectConfig ReconnectConfig { get; set; }
```

---

### ConnectionState (v1.2.0+)

```csharp
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting
}
```

---

### SocketError (v1.3.0+)

```csharp
public readonly struct SocketError
{
    public ErrorType Type { get; }
    public string Message { get; }
}

public enum ErrorType { Transport, Auth, Timeout, Protocol }
```

---

### NamespaceSocket

**Event Handling:**
```csharp
void On(string eventName, Action<string> handler)
void On(string eventName, Action<byte[]> handler)
void Off(string eventName, Action<string> handler)
void Off(string eventName, Action<byte[]> handler)
```

**Event Emission:**
```csharp
void Emit(string eventName, object payload)
void Emit(string eventName, object payload, Action<string> ack, int timeoutMs = 5000)
```

**Events:**
```csharp
event Action OnConnected
event Action OnDisconnected
```

---

### UnityMainThreadDispatcher

**Thread Safety:**
```csharp
static void Enqueue(Action action)
static bool IsInitialized { get; }
```

---

## ⚠️ Breaking Change in v1.3.0

### `OnError` Signature

`OnError` changed from `Action<string>` to `Action<SocketError>` to enable typed error handling.

```csharp
// v1.2.x (old)
socket.OnError += (string msg) => Debug.LogError(msg);

// v1.3.0+ (new)
socket.OnError += (SocketError err) => Debug.LogError($"[{err.Type}] {err.Message}");
```

This is the only breaking change in the v1.x lifecycle. All other stable APIs remain intact.

---

## ⚠️ May Change Before v2.0.0

These APIs are subject to change in minor releases (v1.x → v1.y).

### ReconnectConfig Fields

```csharp
// Direct field mutation on a retrieved config is supported in v1.x
// but the getter may return a copy in v2.0
socket.ReconnectConfig.maxDelay = 60f;  // Works in v1.x, avoid in new code
```

**Recommended pattern (forward-compatible):**
```csharp
var cfg = new ReconnectConfig { maxDelay = 60f };
socket.ReconnectConfig = cfg;
```

### Debugging & Profiler APIs

- `SocketIOTrace.*`
- `TraceConfig.*`
- `SocketIOProfilerCounters.*`
- `SocketIOThroughputTracker.*`

**Why:** These are editor/debugging tools, not core client functionality.

### Telemetry Properties

**On SocketIOClient:**
```csharp
int NamespaceCount { get; }
int PendingAckCount { get; }
float PingRttMs { get; }
```

**On NamespaceSocket:**
```csharp
int PendingAckCount { get; }
```

**Why:** These are primarily for the Editor HUD. May be moved to a separate debugging API.

### ITickable Interface Methods

```csharp
void Tick()  // On SocketIOClient and NamespaceSocket
```

**Why:** Part of `ITickable` interface for Unity tick integration. Public due to interface, but **do not call directly**.

---

## 🔒 Internal APIs (Not Guaranteed)

These are `internal` or in non-public namespaces. **Do not rely on these.**

- `EngineIOClient`
- `SocketPacketParser`
- `BinaryPacketAssembler`
- `ReconnectController`
- `NamespaceManager`
- Transport interfaces (`ITransport`, `WebSocketTransport`, etc.)

---

## Migration Promise

If we **must** break a stable API in v2.0.0, we will:

1. Mark the old API `[Obsolete]` in the last v1.x release
2. Provide a clear migration path in the changelog
3. Keep the old API working for at least one major version

---

## Recommendations for Users

✅ **Safe to use in production:**
- `SocketIOClient` core methods
- `NamespaceSocket` methods
- `UnityMainThreadDispatcher.Enqueue()`

⚠️ **Use with caution:**
- Debugging/profiler APIs (may change structure)
- Internal classes (may be refactored)

❌ **Avoid:**
- Directly instantiating transports
- Accessing internal protocol classes
