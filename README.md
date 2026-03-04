# socketio-unity

Production-ready Socket.IO v4 client for Unity.
Supports WebGL, binary payloads, namespaces, authentication, and CI-tested stability.

Built for serious multiplayer and live backend systems.

[![CI](https://github.com/Magithar/socketio-unity/actions/workflows/ci.yml/badge.svg)](https://github.com/Magithar/socketio-unity/actions/workflows/ci.yml)
[![Release](https://img.shields.io/badge/release-v1.1.2-blue)](https://github.com/Magithar/socketio-unity/releases)
[![Unity 2020.1+](https://img.shields.io/badge/Unity-2020.1%2B-black?logo=unity&logoColor=white)](https://unity.com)
[![WebGL Supported](https://img.shields.io/badge/WebGL-Supported-brightgreen)](Documentation~/WEBGL_NOTES.md)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## 🎬 Demo

| Sample | Video |
|--------|-------|
| Basic Chat | [▶ Watch on YouTube](https://youtu.be/7dU89B9O50c) |
| Player Sync — WebGL Multiplayer | [▶ Watch on YouTube](https://www.youtube.com/watch?v=pdLP2jB7iEE) |

---

> ✅ **Stable for production use** — Public API frozen for v1.x

**Current:** v1.1.2 (2026-03-05) — Reconnection stability fixes (stale engine state, collection enumeration crash, namespace re-registration after reconnect).

Open-source, clean-room Socket.IO v4 client for Unity — written from scratch against the public
protocol spec with no dependency on paid or closed-source assets.
Provides a familiar **event-based `On` / `Emit` API** across **Standalone, WebGL, and Mobile** builds.

> ⚠️ **Transport scope:** This client uses **WebSocket transport only**. Engine.IO long-polling is intentionally not supported.

---

## 🚧 Implementation Status

### ✅ v1.1.2 Milestone (2026-03-05)

* **Reconnection Stability** - `CreateFreshEngine()` fully recreates engine state on each reconnect; prevents stale state, collection-modification crashes, and silently dropped namespaces after reconnect
* **PlayerNetworkSync Sample** - Re-attaches socket event handlers on reconnect to align with core reconnection fixes

### ✅ v1.1.1 Milestone (2026-02-28)

* **PlayerSync RemotePlayer Prefab Fixes** - Canvas render mode corrected to World Space, scale and size restored
* **BillboardCanvas Script** - Camera-facing label that always faces the viewer regardless of player direction

### ✅ v1.1.0 Milestone (2026-02-28)

* **PlayerSync Sample** - Production-grade multiplayer synchronization (9 components, 2 scenes, 3 Node.js servers)
* **ReconnectConfig** - Inspector-configurable backoff with jitter, factory presets, defensive copy
* **Mobile Support** - Android / iOS touch input, runtime URL configuration, dedicated mobile scene
* **CI Pipeline** - GitHub Actions + game-ci/unity-test-runner on every push/PR

### ✅ v1.0.0 Milestone (2026-01-29)

* **API Stability Contract** - Public API frozen for v1.x releases
* **Basic Chat Sample** - Production-ready Hello World onboarding experience
* **Protocol Hardening** - Edge case handling and malformed packet protection
* **Namespace Disconnect Correctness** - Reliable multi-namespace lifecycle management
* **Scene/Domain Reload Safety** - Unity Editor workflow compatibility

### ✅ Implemented

* Engine.IO v4 handshake (WebSocket-only)
* Engine.IO heartbeat / ping–pong watchdog
* Socket.IO v4 packet framing & parsing
* Event-based API (`On`, `Emit`)
* Default namespace (`/`)
* Custom namespaces (`/admin`, `/public`, etc.)
* Namespace multiplexing over a single connection
* Acknowledgement callbacks (ACKs)
* Automatic reconnect with configurable exponential backoff
* **ReconnectConfig** (v1.1.0) - Inspector-configurable reconnection strategy with jitter support
* **Connection state management** (`ConnectionState` enum: Disconnected/Connecting/Connected/Reconnecting)
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
* **Automated test suite** (Protocol edge case tests via SocketIO menu + Bug regression tests via Unity Test Runner)

### ✅ WebGL Support (Production Verified)

* WebGL JavaScript bridge fully tested and operational
* Namespace support verified (`/`, `/webgl`, `/admin`)
* Binary data reception confirmed
* Reconnection behavior validated in browser

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

## 🛡 Production Readiness

| Requirement | Status |
|-------------|--------|
| Stable public API (v1.x frozen) | ✅ |
| CI-validated (Unity 2022.3 LTS) | ✅ |
| Protocol edge-case tested (38 tests) | ✅ |
| Bug regression tests | ✅ |
| WebGL verified | ✅ |
| Mobile verified (Android / iOS) | ✅ |
| Configurable reconnect (ReconnectConfig) | ✅ |
| No GC spikes (object pooling) | ✅ |
| Main-thread safe (all callbacks) | ✅ |
| Domain reload safe | ✅ |
| Clean-room / legally safe | ✅ |
| IDisposable / no resource leaks | ✅ |

---

## 📦 Supported Platforms

| Platform                | Status               |
| ----------------------- | -------------------- |
| Unity Editor            | ✅                    |
| Windows / macOS / Linux | ✅                    |
| WebGL                   | ✅ (verified)         |
| Mobile (Android / iOS)  | ✅ (verified)         |

### Socket.IO / Engine.IO Version Compatibility

| Server Version | Supported |
|----------------|-----------|
| Socket.IO v4.x | ✅ |
| Socket.IO v3.x | ❌ |
| Socket.IO v2.x | ❌ |
| Engine.IO v4 (WebSocket) | ✅ |
| Engine.IO long-polling | ❌ intentionally excluded |

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

### Option 1: Unity Package Manager (Git URL) — Recommended

1. Open Unity's Package Manager (`Window > Package Manager`)
2. Click `+` → `Add package from git URL`
3. Enter: `https://github.com/Magithar/socketio-unity.git`

### Option 2: Manual Installation

1. Download or clone this repository
2. Copy the entire repository folder into your Unity project's `Packages/` directory
   (or add it as a local package via Package Manager → `Add package from disk` → `package.json`)

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

**Note on NativeWebSocket:** This project includes a modified version of `WebSocket.cs` from NativeWebSocket with Unity domain reload safety improvements (v1.0.1 bug fix). All modifications are documented in [NOTICE.md](NOTICE.md) for Apache 2.0 license compliance.

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

**Customization:**
By default, reconnection uses exponential backoff (1s → 2s → 4s → 8s → 16s → 30s max).
For custom behavior, see [Configuring Reconnection Behavior](#configuring-reconnection-behavior-v110).

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
├── API_STABILITY.md
│
├── Runtime/                    # Runtime code (included in builds)
│   ├── SocketIOUnity.asmdef
│   ├── AssemblyInfo.cs
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
│   ├── ProtocolEdgeCaseTests.cs  # Protocol edge case tests (MenuItem)
│   └── SocketIONetworkHud.cs
│
├── Tests/                      # Automated tests
│   └── Runtime/                # Runtime tests (NUnit)
│       ├── BugRegressionTests.cs
│       ├── ReconnectConfigTests.cs
│       └── SocketIOUnity.Tests.asmdef
│
├── Samples~/                   # UPM importable samples
│   ├── BasicChat/              # Production-ready Hello World
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
├── Documentation~/             # Package docs
│   ├── ARCHITECTURE.md
│   ├── BINARY_EVENTS.md
│   ├── DEBUGGING_GUIDE.md
│   ├── RECONNECT_BEHAVIOR.md
│   └── WEBGL_NOTES.md
│
└── TestProject~/               # CI test project (Unity 2022.3 LTS)
    ├── Assets/
    ├── Packages/               # References this package as local dependency
    └── ProjectSettings/
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

**📺 Video Walkthrough**: [Watch on YouTube](https://youtu.be/7dU89B9O50c)

**📚 Full Documentation**: See [BasicChat/README.md](Samples~/BasicChat/README.md)

**🎯 Import**: Package Manager → Socket.IO Unity Client → Samples → "Basic Chat"

**Key Features:**
- Uses only APIs guaranteed stable for v1.x
- Full UI implementation with TextMesh Pro
- Comprehensive error handling
- Works on Editor, Standalone, and WebGL

---

## 🎮 PlayerSync Sample

The **PlayerSync** sample is a production-grade real-time multiplayer demo (added in v1.1.0). It builds directly on the Basic Chat concepts and demonstrates:

- ✅ Real-time position synchronization across clients
- ✅ Player join / leave detection
- ✅ Namespace-based architecture (`/playersync`)
- ✅ Configurable reconnection with `ReconnectConfig` and jitter
- ✅ Network interpolation for smooth remote player movement
- ✅ RTT display and connection status UI
- ✅ Production-grade cleanup (`OnDestroy`, `isDestroyed` guard)
- ✅ Full WebGL support with automatic transport detection

### Quick Tour

```csharp
// Connect to root, then get namespace
rootSocket = new SocketIOClient(TransportFactoryHelper.CreateDefault());
rootSocket.Connect("ws://localhost:3000");
var ns = rootSocket.Of("/playersync");

// Configure reconnection with jitter
rootSocket.ReconnectConfig = new ReconnectConfig
{
    initialDelay  = 1f,
    multiplier    = 2f,
    maxDelay      = 30f,
    jitterPercent = 0.1f,  // Prevents thundering herd
};

// Receive existing players on connect
ns.On("existing_players", (string json) => { /* spawn remote players */ });

// Broadcast your position at 20Hz
ns.Emit("player_move", JsonConvert.SerializeObject(movePacket));
```

**📺 Video Walkthrough**: [Watch on YouTube](https://www.youtube.com/watch?v=pdLP2jB7iEE)

**📚 Full Documentation**: See [PlayerSync/README.md](Samples~/PlayerSync/README.md)

**🎯 Import**: Package Manager → Socket.IO Unity Client → Samples → "Player Sync"

**Key Features:**
- Namespace pattern (`rootSocket.Of("/playersync")`) over a single WebSocket
- 9 components, pre-configured scene, and 3 Node.js server examples
- Scales comfortably to 2–20 players (see scaling guide in the README)
- Works on Editor, Standalone, WebGL, and Mobile

> **New to socketio-unity?** Start with [Basic Chat](#-basic-chat-sample) first — PlayerSync builds on those foundations.

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
// Categories: EngineIO, SocketIO, Transport, Binary, Reconnect, Namespace, Ack
```

### Trace Levels

| Level | Description |
|-------|-------------|
| `TraceLevel.None` | Tracing disabled (default) |
| `TraceLevel.Errors` | Only errors |
| `TraceLevel.Protocol` | Errors + protocol packets |
| `TraceLevel.Verbose` | Full debug output |

### Custom Trace Sinks

```csharp
// Implement ITraceSink for custom output (file, network, UI overlay)
public class MyTraceSink : ITraceSink
{
    public void Emit(in TraceEvent evt)
    {
        // Custom handling
    }
}

// Register custom sink
SocketIOTrace.SetSink(new MyTraceSink());
```

---

## 🧪 Development & Testing

### Test Structure

SocketIOUnity includes comprehensive automated tests for protocol correctness and bug regression prevention.

**Test Organization:**

```
socketio-unity/
├── Editor/
│   ├── ProtocolEdgeCaseTests.cs      # Custom editor tests (MenuItem-based)
│   └── SocketIOUnity.Editor.asmdef
└── Tests/
    └── Runtime/
        ├── BugRegressionTests.cs      # Unity Test Runner (NUnit)
        └── SocketIOUnity.Tests.asmdef
```

**Two Types of Tests:**

| Test File | Type | How to Run |
|-----------|------|------------|
| **Editor/ProtocolEdgeCaseTests.cs** | Custom editor tool | Unity menu: **SocketIO → Run Protocol Edge Tests** |
| **Tests/Runtime/BugRegressionTests.cs** | NUnit tests | Unity Test Runner: **Window → General → Test Runner** |

**Protocol Edge Tests** validate Socket.IO protocol parsing including:
- Empty/null packet handling
- Invalid type validation (types 0-6)
- Binary packet parsing with attachments
- Namespace parsing edge cases
- ACK ID overflow protection
- Malformed JSON handling

**Bug Regression Tests** prevent previously fixed bugs from reoccurring:
- Binary packet assembler edge cases
- ACK registry integer overflow handling
- Invalid JSON graceful degradation

### CI Pipeline

SocketIOUnity uses **GitHub Actions** with [`game-ci/unity-test-runner`](https://github.com/game-ci/unity-test-runner) to run automated tests on every push and pull request to `main`.

**Pipeline:** `.github/workflows/ci.yml`

| Setting | Value |
|---------|-------|
| Trigger | Push / PR to `main` |
| Runner | `ubuntu-latest` |
| Unity version | `2022.3.62f2` (LTS) |
| Test mode | EditMode |
| Test project | `TestProject~/` |
| Artifacts | Test results uploaded on every run (`if: always()`) |
| Git LFS | Enabled (`lfs: true`) — required for binary assets |
| Library cache | Cached via `actions/cache`, keyed on `package.json` + `TestProject~/Packages/manifest.json` |

**`TestProject~/`** is a standalone Unity project that lives inside the repository. It references this package as a local dependency, giving the CI runner a complete Unity project to import and test against.

**Git LFS:** This repository uses Git LFS for binary assets. Contributors must have LFS installed (`git lfs install`) before cloning, otherwise assets will be corrupted and local runs may diverge from CI.

**Library cache:** The Unity `Library/` folder is cached between runs to speed up subsequent jobs. The cache key includes both `package.json` and `TestProject~/Packages/manifest.json`, so it invalidates automatically whenever package dependencies change — expect a slower first run after any dependency update.

**Required GitHub Secrets** (set in repository Settings → Secrets):

| Secret | Description |
|--------|-------------|
| `UNITY_LICENSE` | Unity license XML (from `unity-activate` action or manual export) |
| `UNITY_EMAIL` | Unity account email |
| `UNITY_PASSWORD` | Unity account password |

> See [game-ci docs](https://game.ci/docs/github/activation) for how to generate and add the Unity license secret.

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
// /playersync — PLAYER SYNC SAMPLE (NO AUTH)
// ======================================================

const players = {};

io.of("/playersync").on("connection", (socket) => {
  console.log("✅ /playersync CONNECTED:", socket.id);

  // Register player at origin
  players[socket.id] = { x: 0, y: 0, z: 0 };

  // 🔥 Send server-assigned ID to this client
  socket.emit("player_id", socket.id);
  console.log("📤 /playersync → player_id:", socket.id);

  // 🔥 Send existing players to the new player
  socket.emit("existing_players", players);
  console.log("📤 /playersync → existing_players:", Object.keys(players).length, "players");

  // 🔥 Notify other players that someone joined
  socket.broadcast.emit("player_join", socket.id);
  console.log("📢 /playersync → broadcast player_join:", socket.id);

  // Receive movement from client
  socket.on("player_move", (data) => {
    if (data && data.position) {
      players[socket.id] = data.position;

      // Broadcast to all other players
      socket.broadcast.emit("player_move", {
        id: socket.id,
        position: data.position
      });
    }
  });

  socket.on("disconnect", () => {
    console.log("❌ /playersync DISCONNECTED:", socket.id);

    // Remove player from list
    delete players[socket.id];

    // Notify other players
    socket.broadcast.emit("player_leave", socket.id);
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
  console.log("  /playersync  ← HERO FEATURE\n");
});
```

</details>

---

## 📄 License

[MIT License](LICENSE) — Free for commercial and non-commercial use.

---

## 📝 Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and release notes.

See [API_STABILITY.md](API_STABILITY.md) for the complete API stability contract.

---

## 🤝 Contributing

Contributions are welcome — but this project has one hard rule:

> 🚨 **Clean-room only.** Do not copy or port code from the official Socket.IO JS client, any paid Unity asset, or any other existing implementation. All contributions must be original and based on public protocol documentation.

If you're unsure whether your contribution complies, open a discussion before submitting.

**Quick guidelines:**
- Open an issue first to discuss significant changes
- Add tests for new functionality when possible
- Update documentation if adding or changing public APIs

**For bug reports, include:**
- Unity version and target platform (Editor / Standalone / WebGL / Mobile)
- Server configuration and Socket.IO server version
- Minimal reproduction steps

📄 **Full details**: See [CONTRIBUTING.md](CONTRIBUTING.md) for allowed/disallowed contributions, PR guidelines, and the complete clean-room rules.

---

