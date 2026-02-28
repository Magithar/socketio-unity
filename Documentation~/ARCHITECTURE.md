# SocketIOUnity Architecture

> Technical deep-dive into the internal architecture

---

## Component Hierarchy

```
SocketIOClient                          ← Main entry point
 │
 ├── EngineIOClient (IDisposable)       ← Engine.IO v4 layer
 │    ├── HandshakeInfo                 ← Session ID, ping intervals
 │    ├── HeartbeatController           ← Ping/pong watchdog
 │    ├── PingRttTracker                ← RTT measurement (uses Time.time)
 │    └── ITransport                    ← Platform abstraction
 │         ├── WebSocketTransport       ← Desktop/Editor
 │         └── WebGLWebSocketTransport  ← WebGL browser
 │
 ├── NamespaceManager                   ← Namespace multiplexing
 │    └── NamespaceSocket[]             ← Per-namespace state
 │         ├── EventRegistry            ← Event handlers (On/Off)
 │         └── AckRegistry              ← ACK callbacks (timeout-protected)
 │
 ├── BinaryPacketAssembler              ← Binary frame collection
 ├── ReconnectController                ← Exponential backoff
 └── UnityTickDriver                    ← Main-thread dispatch

Debug Subsystem
 ├── SocketIOTrace                      ← Configurable packet tracing
 │    └── ITraceSink                    ← Custom output targets
 │         └── UnityDebugTraceSink      ← Default: Debug.Log
 │
 ├── ProfilerMarkers                    ← Performance instrumentation
 ├── SocketIOProfilerCounters           ← Real-time network metrics
 └── SocketIOThroughputTracker          ← Bytes/packets per second
```

---

## Directory Structure (UPM Package Layout)

```
socketio-unity/                 # Package root
├── package.json                # UPM manifest
├── README.md
├── CHANGELOG.md
│
├── Runtime/                    # Runtime code (included in builds)
│   ├── SocketIOUnity.asmdef
│   ├── AssemblyInfo.cs
│   │
│   ├── Core/
│   │   ├── EngineIO/           # Engine.IO v4 protocol
│   │   │   ├── EngineIOClient.cs
│   │   │   ├── EngineMessage.cs
│   │   │   ├── HandshakeInfo.cs
│   │   │   ├── HeartbeatController.cs
│   │   │   └── PingRttTracker.cs
│   │   │
│   │   ├── SocketIO/           # Socket.IO client layer
│   │   │   ├── SocketIOClient.cs
│   │   │   ├── NamespaceManager.cs
│   │   │   ├── NamespaceSocket.cs
│   │   │   ├── EventRegistry.cs
│   │   │   ├── AckRegistry.cs
│   │   │   ├── AckEntry.cs
│   │   │   ├── ReconnectController.cs
│   │   │   └── ReconnectConfig.cs
│   │   │
│   │   ├── Protocol/           # Packet parsing
│   │   │   ├── SocketPacket.cs
│   │   │   ├── SocketPacketParser.cs
│   │   │   └── SocketPacketType.cs
│   │   │
│   │   └── Pooling/            # GC optimization
│   │       ├── ListPool.cs
│   │       └── ObjectPool.cs
│   │
│   ├── Debug/                  # Instrumentation
│   │   ├── ProfilerMarkers.cs
│   │   ├── SocketIOProfilerCounters.cs
│   │   ├── SocketIOThroughputTracker.cs
│   │   ├── SocketIOTrace.cs
│   │   ├── TraceConfig.cs
│   │   ├── TraceLevel.cs
│   │   ├── TraceCategory.cs
│   │   ├── TraceEvent.cs
│   │   ├── ITraceSink.cs
│   │   └── UnityDebugTraceSink.cs
│   │
│   ├── Serialization/          # Binary handling
│   │   ├── BinaryPacketAssembler.cs
│   │   ├── BinaryPacketBuilder.cs
│   │   └── BinaryPacketBuilderPool.cs
│   │
│   ├── Transport/              # Platform abstraction
│   │   ├── ITransport.cs
│   │   ├── TransportFactory.cs
│   │   ├── WebSocketTransport.cs
│   │   ├── WebSocket.cs
│   │   ├── WebGLWebSocketTransport.cs
│   │   └── WebGLSocketBridge.cs
│   │
│   ├── UnityIntegration/       # Unity lifecycle
│   │   ├── ITickable.cs
│   │   ├── UnityTickDriver.cs
│   │   └── UnityMainThreadDispatcher.cs
│   │
│   └── Plugins/WebGL/
│       └── SocketIOWebGL.jslib
│
├── Editor/                     # Editor-only code
│   ├── SocketIOUnity.Editor.asmdef
│   ├── ProtocolEdgeCaseTests.cs
│   └── SocketIONetworkHud.cs
│
├── Tests/                      # Automated tests
│   └── Runtime/
│       ├── BugRegressionTests.cs
│       ├── ReconnectConfigTests.cs
│       └── SocketIOUnity.Tests.asmdef
│
├── Samples~/                   # UPM importable samples
│   ├── BasicChat/
│   │   ├── BasicChatUI.cs
│   │   ├── BasicChatScene.unity
│   │   └── README.md
│   ├── PlayerSync/             # Real-time multiplayer demo
│   │   ├── README.md
│   │   ├── PlayerSyncScene.unity
│   │   └── Scripts/
│   ├── SocketIOManager.cs
│   ├── BinaryEventTest.cs
│   ├── MainThreadDispatcherTest.cs
│   ├── NamespaceAuthTest.cs
│   ├── TraceDemo.cs
│   └── WebGLTestController.cs
│
└── Documentation~/             # Package documentation
    ├── ARCHITECTURE.md
    ├── BINARY_EVENTS.md
    ├── DEBUGGING_GUIDE.md
    ├── RECONNECT_BEHAVIOR.md
    └── WEBGL_NOTES.md
```

> **Note**: `Samples~/` contains UPM-style samples importable via Package Manager.

---

## Layer Separation

### Transport Layer (`Transport/`)

| Class | Platform | Purpose |
|-------|----------|---------|
| `ITransport` | All | Transport interface |
| `WebSocketTransport` | Desktop/Editor | Native `System.Net.WebSockets` |
| `WebGLWebSocketTransport` | WebGL | Browser WebSocket via jslib |
| `TransportFactory` | All | Auto-selects by platform |
| `WebSocket.cs` | Desktop | Full WebSocket implementation |

### Engine.IO Layer (`Core/EngineIO/`)

- **Handshake negotiation** — Session ID, ping intervals
- **Heartbeat management** — Ping/pong keep-alive
- **RTT tracking** — Round-trip time measurement via `PingRttTracker`
- **Resource cleanup** — Implements `IDisposable`

### Socket.IO Layer (`Core/SocketIO/`)

- **Event registration** (`On` / `Off` / `Emit`)
- **Namespace management** — Multiplexed channels
- **Acknowledgements** — Timeout-protected request/response
- **Auth handshakes** — Per-namespace authentication

### Debug Layer (`Debug/`)

- **Packet tracing** — `SocketIOTrace` with configurable levels
- **Profiler markers** — Zero-cost when disabled (`SOCKETIO_PROFILER`)
- **Profiler counters** — Real-time metrics (`SOCKETIO_PROFILER_COUNTERS`)

---

## Data Flow

```
┌─────────────────────────────────────────────────────────┐
│                    Unity Game Code                       │
│                  socket.Emit("event", data)              │
└───────────────────────────┬─────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│                    SocketIOClient                        │
│   • Routes event to correct namespace                    │
│   • Builds Socket.IO packet (type 2: EVENT)             │
│   • Wraps in Engine.IO MESSAGE (type 4)                 │
└───────────────────────────┬─────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│                    EngineIOClient                        │
│   • Prepends Engine.IO type byte ("4")                  │
│   • Tracks bytes sent (throughput)                       │
│   • Sends raw string via transport                       │
└───────────────────────────┬─────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────┐
│                     ITransport                           │
│   • WebSocketTransport (Desktop)                         │
│   • WebGLWebSocketTransport (Browser)                    │
└───────────────────────────┬─────────────────────────────┘
                            │
                            ▼
                      [ WebSocket ]
                            │
                            ▼
                    [ Socket.IO Server ]
```

---

## Tick-Based Execution

All processing happens on Unity's main thread via `UnityTickDriver`:

```csharp
void Update()
{
    foreach (var tickable in _tickables)
        tickable.Tick();
}
```

**Tickable components:**
- `EngineIOClient` — Dispatches transport messages
- `HeartbeatController` — Checks ping timeout (uses `Time.time`)
- `ReconnectController` — Fires reconnect attempts
- `SocketIOThroughputTracker` — Updates per-second metrics

**Benefits:**
- ✅ No background threads
- ✅ Unity lifecycle safety
- ✅ Deterministic execution order
- ✅ Uses `Time.time` for Unity-compatible timing

---

## Memory Management

### Pooling (`Core/Pooling/`)

| Pool | Purpose |
|------|---------|
| `ListPool<T>` | Temporary lists for iteration |
| `ObjectPool<T>` | Reusable objects |
| `BinaryPacketBuilderPool` | Binary packet construction |

```csharp
var list = ListPool<byte[]>.Rent();
// Use...
ListPool<byte[]>.Return(list);
```

---

## Resource Cleanup

`EngineIOClient` implements `IDisposable` for proper cleanup:

```csharp
public void Dispose()
{
    Disconnect();
    _transport?.Close();
    // Clean up event handlers, timers, etc.
}
```

**EventRegistry** supports unsubscription to prevent memory leaks:

```csharp
Action<string> handler = data => Debug.Log(data);
socket.On("event", handler);
socket.Off("event", handler);  // Remove specific handler
socket.Off("event");           // Remove all handlers for event
```

---

## Platform Abstraction

### Desktop/Editor

Uses `System.Net.WebSockets.ClientWebSocket`:
- Full async/await support
- Native TLS handling
- Binary message support

### WebGL

Uses browser WebSocket via JavaScript interop:

```
SocketIOWebGL.jslib    →  JavaScript WebSocket API
       ↑                           ↓
WebGLSocketBridge.cs   ←  SendMessage() callbacks
       ↑
WebGLWebSocketTransport.cs (implements ITransport)
```

---

## Namespace Architecture

All namespaces share a single WebSocket:

```
WebSocket Connection
       │
       ├── / (default namespace)
       ├── /admin (auth required)
       ├── /public
       └── /chat
```

Each namespace has independent:
- Event handlers (`EventRegistry`)
- ACK registry (`AckRegistry`)
- Connection state
- Auth payload

---

## Debug & Instrumentation

### Trace Levels

| Level | Output |
|-------|--------|
| `Off` | Disabled (default) |
| `Errors` | Only errors |
| `Protocol` | Errors + packets |
| `Verbose` | Full debug |

### Profiler Defines

| Define | Feature |
|--------|---------|
| `SOCKETIO_PROFILER` | Profiler markers (~20ns overhead) |
| `SOCKETIO_PROFILER_COUNTERS` | Real-time counters |

### Custom Trace Sinks

```csharp
public class FileTraceSink : ITraceSink
{
    public void Emit(TraceEvent evt)
    {
        File.AppendAllText("socket.log", evt.ToString());
    }
}
SocketIOTrace.SetSink(new FileTraceSink());
```

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Single connection | Follows Socket.IO protocol spec |
| Tick-driven processing | Unity main-thread safety |
| Transport abstraction | Platform independence |
| No background threads | Avoids Unity lifecycle issues |
| Pooled allocations | Minimizes GC pressure |
| `IDisposable` pattern | Proper resource cleanup |
| `Time.time` for timing | Unity-compatible, pause-aware |
| `On`/`Off` event API | Prevents memory leaks |
