# socketio-unity

> ✅ **Stable for production use** — Public API frozen for v1.x

**Current:** v1.0.0 (2026-01-29) — Protocol hardening complete, API stability guaranteed.

**Next:** v1.1.0 — Developer experience, testing infrastructure, and performance transparency.

An **open-source, clean-room implementation** of a **Socket.IO v4 client for Unity**.

This project enables Unity applications to communicate with Socket.IO–powered backends 
(e.g. Node.js services) using a familiar **event-based `On` / `Emit` API**, with support 
for **Standalone and WebGL builds**.

The implementation is written **from scratch**, based solely on **public protocol
documentation** and **observed network behavior**, with **no dependency on paid or closed-source
Unity assets**.

---

## 🚧 Implementation Status

### ✅ Implemented

* Engine.IO v4 handshake (WebSocket-only)
* Engine.IO heartbeat / ping–pong watchdog
* Socket.IO v4 packet framing & parsing
* Event-based API (`On`, `Emit`)
* Default namespace (`/`)
* Custom namespaces (`/admin`, `/public`, etc.)
* Namespace multiplexing over a single connection
* Acknowledgement callbacks (ACKs)
* Automatic reconnect with exponential backoff
* Intentional vs unintentional disconnect handling
* Ping-timeout–triggered reconnect
* Standalone (Editor / Desktop) support
* **Binary payload support** (receive & emit)
* **Auth per namespace** (handshake extensions)
* **Unity Profiler markers** (zero-cost when disabled, via `SOCKETIO_PROFILER` define)
* **Unity Profiler counters** (live metrics, via `SOCKETIO_PROFILER_COUNTERS` define)
* **Packet tracing / debug tooling** (`SocketIOTrace`)
* **Unity main-thread dispatch** (`UnityMainThreadDispatcher`)
* **Memory pooling & GC optimization** (`ListPool`, `ObjectPool`, `BinaryPacketBuilderPool`)
* **RTT tracking** (`PingRttTracker` for round-trip latency measurement)
* **ACK timeout support** (configurable timeout with automatic expiration cleanup)
* **Event unsubscription** (`Off()` methods for handler cleanup)
* **IDisposable pattern** (`SocketIOClient`, `EngineIOClient` for proper resource cleanup)
* **Shutdown() method** (clean disconnect with full state reset)
* **Editor Network HUD** (real-time Scene View overlay via `SocketIO → Toggle Network HUD`)
* **Throughput tracking** (`SocketIOThroughputTracker` for bandwidth monitoring)

### ✅ WebGL Support (Production Verified)

* WebGL JavaScript bridge fully tested and operational
* Namespace support verified (`/`, `/webgl`, `/admin`)
* Binary data reception confirmed
* Reconnection behavior validated in browser

### ✅ v1.0.0 Milestone (2026-01-29)

* **API Stability Contract** - Public API frozen for v1.x releases
* **Basic Chat Sample** - Production-ready Hello World onboarding experience
* **Protocol Hardening** - Edge case handling and malformed packet protection
* **Namespace Disconnect Correctness** - Reliable multi-namespace lifecycle management
* **Scene/Domain Reload Safety** - Unity Editor workflow compatibility

---

## 🎯 Goals & Principles

* Provide a **transparent, inspectable, and extensible** Socket.IO client for Unity
* Maintain **protocol correctness** over undocumented hacks
* Ensure **identical behavior across Standalone and WebGL**
* Remain **clean-room compliant** and legally safe
* Serve as a long-term **community-driven alternative** to closed-source solutions

**Non-Goals:**
* Supporting Socket.IO v1 or v2
* Supporting Engine.IO long-polling
* Copying or mirroring any existing Socket.IO client implementation
* Being a drop-in replacement for any paid asset

---

## 📦 Supported Platforms

| Platform                | Status               |
| ----------------------- | -------------------- |
| Unity Editor            | ✅                    |
| Windows / macOS / Linux | ✅                    |
| WebGL                   | ✅ (verified)         |
| Mobile                  | ❓ (community tested) |

### Minimum Unity Version

| Feature | Minimum Version |
|---------|-----------------|
| Core functionality | Unity 2019.4 LTS |
| Newtonsoft.Json (built-in) | Unity 2020.1+ |
| Profiler Counters | Unity 2020.2+ |

---

## 🔒 API Stability

✅ **Stable for v1.0.0+**  
Core APIs (`Connect`, `Emit`, `On`, `Off`, `Of`, `Disconnect`) are **guaranteed stable** and won't break in v1.x releases.

⚠️ **May change in minor releases**  
Debugging tools (`SocketIOTrace`, profiler APIs) may evolve as we improve developer experience.

📖 **Full Details**: See [API_STABILITY.md](API_STABILITY.md) for the complete stability contract.

---

## 🚀 Installation

### Option 1: Unity Package Manager (Git URL)

1. Open Unity's Package Manager (`Window > Package Manager`)
2. Click `+` → `Add package from git URL`
3. Enter: `https://github.com/Magithar/socketio-unity.git`

### Option 2: Manual Installation

1. Download or clone this repository
2. Copy the `SocketIOUnity` folder into your Unity project's `Assets` folder

---

## 📦 Dependencies

### Required

| Package | Source | License | Purpose |
|---------|--------|---------|---------|
| **Newtonsoft.Json** | `com.unity.nuget.newtonsoft-json` | MIT | JSON serialization |
| **NativeWebSocket** | [endel/NativeWebSocket](https://github.com/endel/NativeWebSocket) | Apache 2.0 | WebSocket transport |

**Installation:**

1. **Newtonsoft.Json** — Included by default in Unity 2020.1+. For older versions, install via Package Manager.

2. **NativeWebSocket** — Install via Package Manager using git URL:
   ```
   https://github.com/endel/NativeWebSocket.git#upm
   ```

### Built-in (No Installation Needed)

| Dependency | Platform | Purpose |
|------------|----------|---------|
| **System.Net.WebSockets** | Standalone / Editor | Native WebSocket transport |
| **Browser WebSocket API** | WebGL | Via `SocketIOWebGL.jslib` bridge |

### Transport Abstraction

All network code is accessed through the `ITransport` interface, enabling:
- Platform-specific implementations
- Easy mocking for tests
- Future transport options (e.g., polling fallback)

---

## 🧠 Usage (Current API)

### Scene Setup

1. **Create an empty GameObject** in your scene (e.g., `SocketIOManager`)
2. **Attach the `SocketIOManager` script** to it
3. **(Optional) For testing:**
   - Attach `GameSocketTest` script to the same GameObject
   - Attach `AdminNamespaceTest` script to the same GameObject
4. **Configure the URL** in `SocketIOManager.cs` if needed (default: `ws://localhost:3000`)

The `SocketIOManager` uses Unity's singleton pattern and persists across scenes.

---

### Basic Connection

```csharp
var socket = SocketIOManager.Instance.Socket;

socket.OnConnected += () =>
{
    Debug.Log("🎮 Game connected");
};

socket.On("chat", data =>
{
    Debug.Log(data);
});

socket.Emit("chat", "Hello from Unity!");
```

---

### Connection State & Error Handling

```csharp
var socket = SocketIOManager.Instance.Socket;

// Check connection state
if (socket.IsConnected)
{
    socket.Emit("status", "online");
}

// Handle connection errors
socket.OnError += (error) =>
{
    Debug.LogError($"❌ Socket error: {error}");
    // Common errors: connection refused, timeout, invalid URL
};

// Handle disconnection
socket.OnDisconnected += () =>
{
    Debug.Log("🔌 Disconnected from server");
};
```

**Common Error Scenarios:**
| Error | Cause | Solution |
|-------|-------|----------|
| Connection refused | Server not running | Start the server |
| Timeout | Network issues or firewall | Check network/firewall settings |
| Invalid URL | Malformed WebSocket URL | Use `ws://` or `wss://` prefix |
| Auth failed | Invalid credentials | Check namespace auth payload |

---

### Event Unsubscription (`Off()`)

Always unsubscribe from events when destroying GameObjects to prevent memory leaks:

```csharp
public class MyComponent : MonoBehaviour
{
    private Action<string> chatHandler;
    private Action<byte[]> fileHandler;

    void Start()
    {
        var socket = SocketIOManager.Instance.Socket;

        // Store handler references for later cleanup
        chatHandler = (msg) => Debug.Log($"Chat: {msg}");
        fileHandler = (data) => Debug.Log($"File: {data.Length} bytes");

        socket.On("chat", chatHandler);
        socket.On("file", fileHandler);
    }

    void OnDestroy()
    {
        var socket = SocketIOManager.Instance?.Socket;
        if (socket != null)
        {
            // Unsubscribe to prevent memory leaks
            socket.Off("chat", chatHandler);
            socket.Off("file", fileHandler);
        }
    }
}
```

---

### Binary Events

Handle binary data (images, files, etc.) with typed handlers:

```csharp
// Receiving binary from server
socket.On("file", (byte[] data) =>
{
    Debug.Log($"📦 Received {data.Length} bytes");
    File.WriteAllBytes("received.bin", data);
});

// Receiving multiple binary attachments
socket.On("multi", (byte[] buf1) =>
{
    Debug.Log($"📦 First buffer: {buf1.Length} bytes");
});

// Binary with acknowledgement
socket.On("binary-ack", (byte[] data) =>
{
    Debug.Log($"📦 Binary ACK data: {data.Length} bytes");
});

// Emitting binary to server
byte[] payload = File.ReadAllBytes("data.bin");
socket.Emit("upload", payload, (response) =>
{
    Debug.Log($"✅ Server response: {response}");
});

// Emitting multiple values (binary + metadata)
// Note: Multiple binary attachments in a single emit is not currently supported.
// Use separate emits or combine data server-side.
```

---

### Namespace Usage

```csharp
var socket = SocketIOManager.Instance.Socket;

// Public namespace (no auth required)
var publicNs = socket.Of("/public");
publicNs.OnConnected += () =>
{
    Debug.Log("📢 /public connected");
};

// Admin namespace with authentication
var admin = socket.Of("/admin", new { token = "test-secret" });
admin.OnConnected += () =>
{
    Debug.Log("🔐 /admin connected");

    admin.Emit("ping", null, res =>
    {
        Debug.Log("🔐 admin ACK: " + res);
    });
};

// Handle auth failures (via event)
admin.On("connect_error", (err) =>
{
    Debug.LogError($"❌ /admin auth failed: {err}");
});
```

**Features:**
* Multiplexed over a single WebSocket connection
* Connected only after the root namespace (`/`)
* Automatically reconnected after disconnects
* Auth payload sent during namespace handshake

---

### Acknowledgement (ACK) Callbacks

```csharp
socket.Emit("getTime", null, response =>
{
    Debug.Log("⏱ Server time: " + response);
});

// With custom timeout (default: 5000ms)
socket.Emit("slowOperation", data, response =>
{
    if (response == null)
    {
        Debug.LogWarning("⏱ ACK timed out - no response from server");
    }
    else
    {
        Debug.Log("✅ Response: " + response);
    }
}, timeoutMs: 10000);
```

**Features:**
* Timeout-protected (callback receives `null` on timeout)
* Namespace-aware
* Automatically cleared on disconnect

**ACK Timeout Behavior:**
* When timeout expires, the callback is invoked with `null`
* The ACK is removed from the pending registry
* No retry is attempted - handle retry logic in your callback if needed

---

### Disconnect vs Shutdown

```csharp
var socket = SocketIOManager.Instance.Socket;

// Disconnect() - Intentional disconnect, can reconnect later
socket.Disconnect();
// - Stops auto-reconnect
// - Preserves namespace registrations
// - Can call Connect() again

// Shutdown() - Full cleanup, typically on application quit
socket.Shutdown();
// - Disconnects all namespaces
// - Clears all event handlers
// - Resets all internal state
// - Use in OnApplicationQuit or when completely done with socket
```

**When to use which:**
| Scenario | Method |
|----------|--------|
| User logs out, may log back in | `Disconnect()` |
| Switching servers | `Disconnect()` then `Connect(newUrl)` |
| Application quitting | `Shutdown()` |
| Disposing the socket permanently | `Shutdown()` + `Dispose()` |

---

### Reconnect Behavior

```csharp
// Automatic reconnection with exponential backoff
// No manual intervention needed
```

**Reconnects happen automatically when:**
* The server closes the connection
* A ping timeout occurs
* Network connectivity is lost

**Reconnects do NOT happen when:**
* `Disconnect()` is called intentionally
* The application is quitting

**Strategy:**
* Exponential backoff to avoid overwhelming the server
* Single reconnect loop (no duplicate attempts)
* Automatically stopped on successful connection

---

### Thread Safety

All callbacks are guaranteed to execute on Unity's main thread:

```csharp
socket.On("update", (data) =>
{
    // ✅ Safe to access Unity APIs here
    transform.position = ParsePosition(data);
    myText.text = data;
});

socket.OnConnected += () =>
{
    // ✅ Safe to instantiate GameObjects
    Instantiate(playerPrefab);
};
```

**Thread Safety Guarantees:**
* `OnConnected`, `OnDisconnected`, `OnError` - Main thread
* All `On()` event handlers - Main thread
* All ACK callbacks - Main thread
* Namespace events - Main thread

This is achieved via `UnityMainThreadDispatcher`, which queues callbacks from the WebSocket thread and processes them during Unity's Update loop.

---

### RTT & Throughput Monitoring

Access real-time network metrics for debugging or UI display:

```csharp
var socket = SocketIOManager.Instance.Socket;

// Round-trip time (ping latency in milliseconds)
float rtt = socket.PingRttMs;
Debug.Log($"Latency: {rtt}ms");

// Throughput tracking (requires SocketIOThroughputTracker)
// These values update every second
float sentPerSec = SocketIOThroughputTracker.SentBytesPerSec;
float recvPerSec = SocketIOThroughputTracker.ReceivedBytesPerSec;
Debug.Log($"↑ {sentPerSec:F0} B/s  ↓ {recvPerSec:F0} B/s");
```

**Note:** These properties are telemetry APIs and may change in minor releases. See [API Stability](#-api-stability).

---

### Scene & Domain Reload Safety

The socket system handles Unity Editor workflow correctly:

**Automatic Handling:**
* **Play → Stop** - Connections are cleaned up, no orphaned sockets
* **Domain Reload** - Static state is reset, reconnection works correctly
* **Scene Load** - `DontDestroyOnLoad` preserves `SocketIOManager` singleton

**Best Practices:**
```csharp
// In your MonoBehaviour
void OnDestroy()
{
    // Always unsubscribe when your object is destroyed
    socket?.Off("myEvent", myHandler);
}

void OnApplicationQuit()
{
    // Optional: explicit shutdown on quit
    SocketIOManager.Instance?.Socket?.Shutdown();
}
```

**What You Don't Need to Worry About:**
* WebSocket connections leaking between play sessions
* Duplicate reconnect loops after domain reload
* Stale callbacks firing after scene unload (if you unsubscribe properly)

---

## 🧱 Architecture Overview

### Directory Structure (UPM Package)

```
socketio-unity/
├── package.json
├── README.md
├── CHANGELOG.md
│
├── Runtime/                    # Runtime code (included in builds)
│   ├── SocketIOUnity.asmdef
│   ├── Core/
│   │   ├── EngineIO/           # Engine.IO v4 protocol
│   │   ├── SocketIO/           # Socket.IO client layer
│   │   ├── Protocol/           # Packet parsing
│   │   └── Pooling/            # GC optimization
│   ├── Debug/                  # Profiler & tracing
│   ├── Serialization/          # Binary handling
│   ├── Transport/              # WebSocket transports
│   ├── UnityIntegration/       # Unity lifecycle
│   └── Plugins/WebGL/          # WebGL jslib
│
├── Editor/                     # Editor-only code
│   ├── SocketIOUnity.Editor.asmdef
│   └── SocketIONetworkHud.cs
│
├── Samples~/                   # UPM importable samples
│   └── BasicChat/              # Production-ready Hello World
│       ├── BasicChatUI.cs
│       ├── BasicChatScene.unity
│       └── README.md
│
└── Documentation~/             # Package docs
    ├── ARCHITECTURE.md
    ├── BINARY_EVENTS.md
    ├── DEBUGGING_GUIDE.md
    ├── RECONNECT_BEHAVIOR.md
    └── WEBGL_NOTES.md
```

> **Note**: `Samples~/` contains UPM-style samples importable via Package Manager.

---

## 💬 Basic Chat Sample

The **Basic Chat** sample is the recommended starting point for learning socketio-unity. It's a production-ready "Hello World" that demonstrates:

- ✅ Connection lifecycle management
- ✅ Event handling (send/receive)
- ✅ Automatic reconnection
- ✅ Proper event cleanup (memory leak prevention)
- ✅ Main-thread safety

### Quick Tour

```csharp
// Get socket from SocketIOManager singleton
var socket = SocketIOManager.Instance.Socket;

// Subscribe to events
socket.OnConnected += OnConnected;
socket.On("chat", OnChatMessage);

// Connect and send
socket.Connect("ws://localhost:3000");
socket.Emit("chat", "Hello!");

// Clean up in OnDestroy
socket.Off("chat", OnChatMessage);
```

**📚 Full Documentation**: See [Samples~/BasicChat/README.md](SocketIOUnity/Samples~/BasicChat/README.md)

**🎯 Import**: Package Manager → Socket.IO Unity Client → Samples → "Basic Chat"

**Key Features:**
- Uses only APIs guaranteed stable for v1.x
- Full UI implementation with TextMesh Pro
- Comprehensive error handling
- Works on Editor, Standalone, and WebGL

---

## 🧪 Sample Test Scripts Reference

> **Note**: For a complete production example, start with the [Basic Chat Sample](#-basic-chat-sample) above.

All test scripts below are in `Samples~/`. Import them via Package Manager → Samples tab.

### Core Components

| Script | Purpose |
|--------|---------|
| `SocketIOManager.cs` | Singleton that manages the SocketIOClient instance. **Required in your scene.** |

### Test Scripts

| Script | What It Tests | How to Use |
|--------|---------------|------------|
| `BinaryEventTest.cs` | Binary event receive (`file`, `multi`) | Attach to any GameObject |
| `MainThreadDispatcherTest.cs` | Verifies all callbacks run on main thread | Attach to any GameObject |
| `NamespaceAuthTest.cs` | Auth success, rejection, and no-auth namespaces | Attach to SocketIOManager GameObject |
| `TraceDemo.cs` | Runtime trace level toggle UI | Attach to any GameObject |
| `WebGLTestController.cs` | WebGL browser testing with runtime UI | Attach to any GameObject (WebGL builds) |

### Test Server Requirements

Copy the `server.js` code from the **Test Server Setup** section below, then run:

```bash
npm init -y && npm install socket.io
node server.js
```

### Testing Checklist

| Feature | Script to Use | Expected Behavior |
|---------|---------------|-------------------|
| Binary events | `BinaryEventTest` | Receives `file` + `multi` events with byte counts |
| Namespace auth | `NamespaceAuthTest` | `/admin` connects, `/admin-bad` rejected, `/public` connects |
| Thread safety | `MainThreadDispatcherTest` | All callbacks show "✓ executed on main thread" |
| WebGL (browser) | `WebGLTestController` | Build WebGL, serve via HTTP, use on-screen buttons |

### WebGL Testing Steps

1. Add `SocketIOManager` + `WebGLTestController` to a scene
2. Build for WebGL (File → Build Settings → WebGL → Build)
3. Serve the build:
   ```bash
   cd /path/to/build && npx serve -p 8080
   ```
4. Open `http://localhost:8080` in browser
5. Use on-screen Connect/Disconnect/Ping/Message buttons
6. Check browser console (F12) for logs

---

### Component Hierarchy

```
SocketIOClient
 ├── EngineIOClient (IDisposable)
 │    ├── HandshakeInfo
 │    ├── HeartbeatController
 │    ├── PingRttTracker
 │    └── ITransport (via TransportFactory)
 │         ├── WebSocketTransport (Standalone)
 │         └── WebGLWebSocketTransport (WebGL)
 │
 ├── NamespaceManager
 │    └── NamespaceSocket[]
 │         ├── EventRegistry (On/Off handlers)
 │         └── AckRegistry (timeout-protected)
 │
 ├── BinaryPacketAssembler
 ├── ReconnectController
 └── UnityTickDriver

Debug Subsystem
 ├── SocketIOTrace → ITraceSink
 │    └── UnityDebugTraceSink (default)
 ├── ProfilerMarkers (SOCKETIO_PROFILER)
 ├── SocketIOProfilerCounters (SOCKETIO_PROFILER_COUNTERS)
 └── SocketIOThroughputTracker
```

### Key Design Principles

* **Single WebSocket connection** — All namespaces share one connection
* **Namespace multiplexing** — Multiple logical channels over one transport
* **Tick-driven** — No background threads, Unity-safe execution
* **Lifecycle safety** — Proper Unity lifecycle handling (Play/Stop/Quit)
* **Separation of concerns** — Protocol logic isolated from Unity integration
* **Resource cleanup** — `IDisposable` pattern for proper connection disposal
* **Event unsubscription** — `Off()` methods prevent memory leaks

---

## ✅ WebGL Status (Production Verified)

WebGL support has been **fully tested and verified**.

**✅ Implemented & Verified:**

* `SocketIOWebGL.jslib` — JavaScript WebSocket bridge with NativeWebSocket compatibility
* `WebGLSocketBridge.cs` — Unity MonoBehaviour for JS callbacks
* `WebGLWebSocketTransport.cs` — ITransport implementation
* `WebGLTestController.cs` — Sample controller for WebGL testing

**✅ Verified Features:**

* Root namespace (`/`) connection and events
* Custom namespaces (`/webgl`, `/admin`) with auth support
* Binary message handling in WebGL
* Reconnection behavior in browser
* Clean disconnect/reconnect cycles

**⚠️ Browser Cache Note:**

When iterating on WebGL builds, always force-refresh (`Cmd+Shift+R`) or use Incognito mode to avoid cached JS/WASM issues.

---

## 🔬 Unity Profiler Integration

SocketIOUnity includes optional Unity Profiler markers for performance analysis.

### Enable

Add this scripting define in **Player Settings → Scripting Define Symbols**:

```
SOCKETIO_PROFILER
```

### Markers

| Marker | Description |
|--------|-------------|
| `SocketIO.EngineIO.Parse` | Engine.IO packet parsing |
| `SocketIO.Event.Dispatch` | Event handler dispatch |
| `SocketIO.Binary.Assemble` | Binary frame assembly |
| `SocketIO.Ack.Resolve` | Acknowledgement resolution |
| `SocketIO.Reconnect.Tick` | Reconnection loop tick |

### How to Use

1. Enable `SOCKETIO_PROFILER` scripting define
2. Open **Window → Analysis → Profiler**
3. Select **CPU Usage**
4. Connect to server and emit events
5. View SocketIO markers under **Scripts**

### Performance

| Condition | Cost |
|-----------|------|
| Define OFF | **Zero** (code stripped) |
| Define ON | ~20-40ns per scope |
| GC allocs | **0** |

---

## 📊 Unity Profiler Counters

SocketIOUnity includes optional Unity Profiler Counters for real-time metrics (requires Unity 2020.2+).

### Enable

Add this scripting define in **Player Settings → Scripting Define Symbols**:

```
SOCKETIO_PROFILER_COUNTERS
```

### Available Counters

| Counter | Category | Description |
|---------|----------|-------------|
| `SocketIO.Bytes Sent` | Network | Total bytes sent |
| `SocketIO.Bytes Received` | Network | Total bytes received |
| `SocketIO.Packets/sec` | Network | Packets received per second |
| `SocketIO.Active Namespaces` | Scripts | Currently connected namespaces |
| `SocketIO.Pending ACKs` | Scripts | Outstanding acknowledgement callbacks |

### How to Use

1. Enable `SOCKETIO_PROFILER_COUNTERS` scripting define
2. Open **Window → Analysis → Profiler**
3. Click **Profiler Modules** (gear icon) → Enable **Custom Module**
4. View SocketIO counters under Network and Scripts categories

---

## 🔍 Packet Tracing

SocketIOUnity includes a configurable packet tracing system for debugging protocol issues.

### API

```csharp
using SocketIOUnity.Debugging;

// Configure trace level
TraceConfig.Level = TraceLevel.Protocol;  // Errors, Protocol, or Verbose

// Trace events are automatically logged by protocol code
// Categories: EngineIO, SocketIO, Namespace, Transport, Binary
```

### Trace Levels

| Level | Description |
|-------|-------------|
| `TraceLevel.Off` | Tracing disabled (default) |
| `TraceLevel.Errors` | Only errors |
| `TraceLevel.Protocol` | Errors + protocol packets |
| `TraceLevel.Verbose` | Full debug output |

### Custom Trace Sinks

```csharp
// Implement ITraceSink for custom output (file, network, UI overlay)
public class MyTraceSink : ITraceSink
{
    public void Emit(TraceEvent evt)
    {
        // Custom handling
    }
}

// Register custom sink
SocketIOTrace.SetSink(new MyTraceSink());
```

---

## 🧪 Development & Testing

### Test Server Setup

A Node.js test server is included for development and testing. To run it:

```bash
cd TestServer
npm install socket.io
node server.js
```

The test server runs on `http://localhost:3000` and provides:

* **Root namespace (`/`)** — No auth, binary events support
* **Admin namespace (`/admin`)** — Requires `token: "test-secret"`
* **Admin-bad namespace (`/admin-bad`)** — Always rejects auth (for testing)
* **Public namespace (`/public`)** — No auth required
* **WebGL namespace (`/webgl`)** — No auth, designed for browser testing

### Available Test Scenarios

| Namespace     | Auth Required | Description                          |
| ------------- | ------------- | ------------------------------------ |
| `/`           | ❌             | Text events, binary events, ACKs    |
| `/admin`      | ✅ `test-secret` | Auth-protected namespace           |
| `/admin-bad`  | ✅ (always fails) | Test auth rejection handling    |
| `/public`     | ❌             | Simple no-auth namespace            |
| `/webgl`      | ❌             | WebGL browser testing (ping/pong, message echo) |

### Binary Events Timeline (Root Namespace)

| Delay | Event        | Description                    |
| ----- | ------------ | ------------------------------ |
| 0s    | `hello`      | Text welcome message           |
| 2s    | `file`       | Single binary buffer           |
| 4s    | `multi`      | Two binary buffers             |
| 6s    | `binary-ack` | Binary with ACK callback       |

<details>
<summary><strong>View server.js code</strong></summary>

```javascript
const http = require("http");
const { Server } = require("socket.io");

const PORT = 3000;

// ======================================================
// HTTP SERVER (REQUIRED FOR UNITY / NATIVE WS)
// ======================================================
const httpServer = http.createServer();

const io = new Server(httpServer, {
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});

console.log(`🚀 Socket.IO server starting on port ${PORT}`);


// ======================================================
// ROOT NAMESPACE  ("/") — NO AUTH
// ======================================================
io.on("connection", (socket) => {
  console.log("✅ / ROOT CONNECTED:", socket.id);

  // ---- Text event
  socket.emit("hello", {
    message: "welcome",
    socketId: socket.id
  });

  // ---- Single binary (2s)
  setTimeout(() => {
    const buffer = Buffer.from("Hello");
    console.log("📤 / file (single binary)");
    socket.emit("file", buffer);
  }, 2000);

  // ---- Multi binary (4s)
  setTimeout(() => {
    const buf1 = Buffer.from([1, 2, 3]);
    const buf2 = Buffer.from([4, 5, 6]);
    console.log("📤 / multi (2 binaries)");
    socket.emit("multi", buf1, buf2);
  }, 4000);

  // ---- Binary + ACK (6s)
  setTimeout(() => {
    const payload = Buffer.from("ACK_TEST");
    console.log("📤 / binary-ack");

    socket.emit("binary-ack", payload, (ack) => {
      console.log("📥 / ACK from client:", ack);
    });
  }, 6000);

  // ---- Client → Server
  socket.on("ping-test", (msg) => {
    console.log("📩 / ping-test:", msg);
    socket.emit("pong-test", { serverTime: Date.now() });
  });

  socket.on("upload", (buffer, ack) => {
    console.log("📩 / upload received:", buffer.length, "bytes");
    if (ack) ack({ ok: true, size: buffer.length });
  });

  // ---- Basic Chat (for BasicChat sample)
  socket.on("chat", (msg) => {
    console.log("📩 / chat:", msg);
    socket.emit("chat", msg);  // Echo back
  });

  socket.on("disconnect", (reason) => {
    console.log("❌ / ROOT DISCONNECTED:", socket.id, reason);
  });
});


// ======================================================
// /admin — AUTH REQUIRED
// ======================================================
io.of("/admin").use((socket, next) => {
  const token = socket.handshake.auth?.token;
  console.log(`🔐 /admin auth token: "${token}"`);

  if (token === "test-secret") {
    console.log("✅ /admin AUTH OK");
    next();
  } else {
    console.log("❌ /admin AUTH FAIL");
    next(new Error("unauthorized"));
  }
});

io.of("/admin").on("connection", (socket) => {
  console.log("✅ /admin CONNECTED:", socket.id);

  socket.on("ping", (payload, ack) => {
    console.log("📩 /admin ping");
    if (ack) ack({ ok: true, adminTime: Date.now() });
  });

  socket.on("disconnect", (reason) => {
    console.log("❌ /admin DISCONNECTED:", socket.id, reason);
  });
});


// ======================================================
// /admin-bad — ALWAYS REJECT
// ======================================================
io.of("/admin-bad").use((socket, next) => {
  const token = socket.handshake.auth?.token;
  console.log(`🔐 /admin-bad token: "${token}"`);
  console.log("❌ /admin-bad AUTH INTENTIONAL FAIL");
  next(new Error("unauthorized"));
});


// ======================================================
// /public — NO AUTH
// ======================================================
io.of("/public").on("connection", (socket) => {
  console.log("✅ /public CONNECTED:", socket.id);

  socket.on("disconnect", () => {
    console.log("❌ /public DISCONNECTED:", socket.id);
  });
});


// ======================================================
// /webgl — WEBGL TESTING (NO AUTH)
// ======================================================
io.of("/webgl").on("connection", (socket) => {
  console.log("✅ /webgl CONNECTED:", socket.id);

  // Welcome message
  socket.emit("welcome", {
    message: "WebGL client connected!",
    socketId: socket.id,
    serverTime: Date.now()
  });

  // Ping → Pong (for latency testing)
  socket.on("ping", (payload) => {
    console.log("📩 /webgl ping:", payload);
    socket.emit("pong", {
      clientTime: payload,
      serverTime: new Date().toISOString(),
      roundtrip: "calculate on client"
    });
  });

  // Message echo
  socket.on("message", (msg) => {
    console.log("📩 /webgl message:", msg);
    socket.emit("message", {
      echo: msg,
      from: "server",
      timestamp: Date.now()
    });
  });

  // Simple text event
  socket.on("test", (data) => {
    console.log("📩 /webgl test:", data);
    socket.emit("test-response", { received: data, ok: true });
  });

  // Broadcast to all WebGL clients
  socket.on("broadcast", (msg) => {
    console.log("📢 /webgl broadcast:", msg);
    io.of("/webgl").emit("broadcast", {
      from: socket.id,
      message: msg
    });
  });

  socket.on("disconnect", (reason) => {
    console.log("❌ /webgl DISCONNECTED:", socket.id, reason);
  });
});


// ======================================================
// START SERVER
// ======================================================
httpServer.listen(PORT, () => {
  console.log(`✅ HTTP + WebSocket listening on ${PORT}`);

  console.log("\n📋 TEST SCENARIOS");
  console.log("1️⃣ /            → no auth + binary");
  console.log("2️⃣ /admin       → token='test-secret'");
  console.log("3️⃣ /admin-bad   → always unauthorized");
  console.log("4️⃣ /public      → no auth");
  console.log("5️⃣ /webgl       → WebGL browser testing\n");
});
```

</details>

---

## 📄 License

[MIT License](LICENSE) — Free for commercial and non-commercial use.

---

## 📝 Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and release notes.

---

## 🤝 Contributing

Contributions are welcome! Please:

1. **Open an issue first** to discuss significant changes
2. **Follow existing code style** and patterns
3. **Add tests** for new functionality when possible
4. **Update documentation** if adding/changing public APIs

For bug reports, please include:
* Unity version
* Platform (Editor/Standalone/WebGL)
* Server configuration
* Minimal reproduction steps

---

## ⚠️ Disclaimer

This project is **not affiliated with Socket.IO** or Unity Technologies.

All behavior is implemented using:

* Public protocol documentation
* Observed network behavior
* Independent engineering decisions
