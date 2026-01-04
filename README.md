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
* Custom namespaces (`/admin`, etc.)
* Namespace multiplexing over a single connection
* Acknowledgement callbacks (ACKs)
* Automatic reconnect with exponential backoff
* Intentional vs unintentional disconnect handling
* Ping-timeout–triggered reconnect
* Standalone (Editor / Desktop) support

### 🚧 In Progress

* Binary payload support
* WebGL JavaScript bridge hardening
* Unity main-thread dispatch polish
* Memory pooling & GC optimization
* Packet tracing / debug tooling
* Auth per namespace (handshake extensions)

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

### Basic connection

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

### Namespace usage

```csharp
var socket = SocketIOManager.Instance.Socket;
var admin = socket.Of("/admin");

admin.OnConnected += () =>
{
    Debug.Log("🔐 /admin connected");

    admin.Emit("ping", null, res =>
    {
        Debug.Log("🔐 admin ACK: " + res);
    });
};
```

**Features:**
* Multiplexed over a single WebSocket connection
* Connected only after the root namespace (`/`)
* Automatically reconnected after disconnects

---

### Acknowledgement (ACK) callbacks

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

### Reconnect behavior

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
│   ├── SocketProtocol/  # Packet framing & parsing
│   └── Transport/       # Transport abstraction (WebSocket)
│
├── UnityIntegration/    # Unity lifecycle & tick integration
├── Samples/             # Example usage & test scenes
```

### Component Hierarchy

```
SocketIOClient
 ├── EngineIOClient
 │    ├── Handshake
 │    ├── Ping / Pong watchdog
 │    └── Transport
 ├── NamespaceManager
 │    └── NamespaceSocket
 ├── AckRegistry
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

WebGL support is **architecture-ready** but **not yet complete**.

Planned:

* `.jslib` WebSocket bridge
* Browser lifecycle handling
* Message marshaling between JS ↔ C#

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

* **Default namespace (`/`)** with `ping-test`, `getTime`, and `neverReply` events
* **Admin namespace (`/admin`)** with `ping` event
* Full ACK support
* CORS enabled for local testing

<details>
<summary><strong>View server.js code</strong></summary>

```javascript
const http = require("http");
const { Server } = require("socket.io");

const PORT = 3000;

// 🔥 Explicit HTTP server (REQUIRED for native WS clients)
const httpServer = http.createServer();

const io = new Server(httpServer, {
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});

console.log(`🚀 Socket.IO server starting on port ${PORT}`);


// ======================================================
// DEFAULT NAMESPACE  ("/")
// ======================================================
io.on("connection", (socket) => {
  console.log("✅ / CLIENT CONNECTED:", socket.id);

  socket.emit("hello", {
    message: "welcome",
    socketId: socket.id
  });

  socket.on("ping-test", (msg) => {
    console.log("📩 / ping-test:", msg);

    socket.emit("pong-test", {
      message: "pong",
      serverTime: Date.now()
    });
  });

  socket.on("neverReply", () => {
    console.log("🧪 / neverReply received — intentionally ignoring");
  });

  socket.on("getTime", (data, ack) => {
    console.log("🧪 / getTime received");

    setTimeout(() => {
      ack({
        serverTime: Date.now()
      });
    }, 500);
  });

  socket.on("disconnect", (reason) => {
    console.log("❌ / CLIENT DISCONNECTED:", socket.id, "Reason:", reason);
  });
});


// ======================================================
// ADMIN NAMESPACE  ("/admin")
// ======================================================
io.of("/admin").on("connection", (socket) => {
  console.log("✅ /admin CLIENT CONNECTED:", socket.id);

  socket.on("ping", (data, ack) => {
    console.log("📩 /admin ping received");

    ack({
      ok: true,
      adminTime: Date.now()
    });
  });

  socket.on("disconnect", (reason) => {
    console.log("❌ /admin CLIENT DISCONNECTED:", socket.id, "Reason:", reason);
  });
});


// 🔥 START SERVER
httpServer.listen(PORT, () => {
  console.log(`✅ HTTP + WebSocket listening on ${PORT}`);
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
