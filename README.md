# socketio-unity

> ⚠️ **Early development — API may change before v1.0.0**

An **open-source, clean-room implementation** of a **Socket.IO v4 client for Unity**.

This project enables Unity applications to communicate with Socket.IO–powered backends
(e.g. Node.js services) using a familiar **event-based `On` / `Emit` API**, with support for
**Standalone and WebGL builds**.

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

### 🚧 In Progress

* WebGL JavaScript bridge hardening (core implemented, needs testing)
* Packet tracing / debug tooling

### ✅ Recently Completed

* Unity main-thread dispatch (`UnityMainThreadDispatcher`)
* Memory pooling & GC optimization (`ListPool`, `ObjectPool`, `BinaryPacketBuilderPool`)

> ⚠️ API surface may change before `v1.0.0`

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
| WebGL                   | 🚧                   |
| Mobile                  | ❓ (community tested) |

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

This project uses a **pluggable transport abstraction** (`ITransport`).

Depending on the target platform, it relies on:

* **System.Net.WebSockets** — Standalone / Desktop builds
* **NativeWebSocket** — Editor / Standalone (and future WebGL bridge)

All third-party dependencies are used **as-is** and accessed strictly
through the `ITransport` abstraction layer.

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
```

**Features:**
* Timeout-protected
* Namespace-aware
* Automatically cleared on disconnect

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

## 🧱 Architecture Overview

### Directory Structure

```
SocketIOUnity/
├── Core/
│   ├── EngineIO/        # Engine.IO v4 handshake & heartbeat
│   ├── SocketIO/        # Socket.IO client, namespaces, events, acks
│   ├── Protocol/        # Packet framing & parsing
│   └── Pooling/         # Memory pooling (ListPool, ObjectPool)
│
├── Serialization/       # Binary packet assembly & building
├── Transport/           # Transport abstraction (WebSocket, WebGL)
├── UnityIntegration/    # Unity lifecycle & tick integration
│
├── Plugins/
│   └── WebGL/
│       └── SocketIOWebGL.jslib  # JavaScript WebSocket bridge
│
└── Samples/             # Example scripts (SocketIOManager, tests)
```

### Component Hierarchy

```
SocketIOClient
 ├── EngineIOClient
 │    ├── HandshakeInfo
 │    ├── HeartbeatController
 │    └── ITransport (via TransportFactory)
 ├── NamespaceManager
 │    └── NamespaceSocket
 │         ├── EventRegistry
 │         └── AckRegistry
 ├── BinaryPacketAssembler
 ├── ReconnectController
 └── UnityTickDriver
```

### Key Design Principles

* **Single WebSocket connection** — All namespaces share one connection
* **Namespace multiplexing** — Multiple logical channels over one transport
* **Tick-driven** — No background threads, Unity-safe execution
* **Lifecycle safety** — Proper Unity lifecycle handling (Play/Stop/Quit)
* **Separation of concerns** — Protocol logic isolated from Unity integration

---

## ⚠️ WebGL Status

WebGL support has **core implementation** but requires **production testing**.

**✅ Implemented:**

* `SocketIOWebGL.jslib` — JavaScript WebSocket bridge
* `WebGLSocketBridge.cs` — Unity MonoBehaviour for JS callbacks
* `WebGLWebSocketTransport.cs` — ITransport implementation

**🚧 Needs Testing:**

* Browser lifecycle edge cases
* Binary message handling in WebGL
* Reconnect behavior in browser

> 🚧 WebGL builds are **not production-ready yet**

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

### Available Test Scenarios

| Namespace     | Auth Required | Description                          |
| ------------- | ------------- | ------------------------------------ |
| `/`           | ❌             | Text events, binary events, ACKs    |
| `/admin`      | ✅ `test-secret` | Auth-protected namespace           |
| `/admin-bad`  | ✅ (always fails) | Test auth rejection handling    |
| `/public`     | ❌             | Simple no-auth namespace            |

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
// START SERVER
// ======================================================
httpServer.listen(PORT, () => {
  console.log(`✅ HTTP + WebSocket listening on ${PORT}`);

  console.log("\n📋 TEST SCENARIOS");
  console.log("1️⃣ /            → no auth + binary");
  console.log("2️⃣ /admin       → token='test-secret'");
  console.log("3️⃣ /admin-bad   → always unauthorized");
  console.log("4️⃣ /public      → no auth\n");
});
```

</details>

---

## 📄 License

[MIT License](LICENSE) — Free for commercial and non-commercial use.

---

## ⚠️ Disclaimer

This project is **not affiliated with Socket.IO** or Unity Technologies.

All behavior is implemented using:

* Public protocol documentation
* Observed network behavior
* Independent engineering decisions
