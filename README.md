# socketio-unity

> ⚠️ **Early development — API may change before v1.0.0**

An **open-source, clean-room implementation** of the **Socket.IO v4 client for Unity**.

This project enables Unity applications to communicate with Socket.IO–powered backends
(e.g. Node.js services) using a familiar **event-based `On` / `Emit` API**, with support for
**Standalone and WebGL builds**.

The implementation is written **from scratch**, based solely on **public protocol
documentation** and **observed network behavior**, with no dependency on paid or closed-source
Unity assets.

---

## 🚧 Implementation Status

### Implemented
- ✅ Engine.IO v4 handshake (WebSocket-only)
- ✅ Engine.IO heartbeat / ping–pong
- ✅ Socket.IO v4 packet framing & parsing
- ✅ Event-based API (`On`, `Emit`)
- ✅ Automatic reconnect (basic)
- ✅ Namespace routing (single & multiple namespaces)

### In Progress
- 🚧 Acknowledgement callbacks (acks)
- 🚧 Binary payload support
- 🚧 WebGL JavaScript bridge hardening
- 🚧 Reconnect backoff tuning
- 🚧 Unity main-thread dispatch polish

> ⚠️ API surface may change before `v1.0.0`

## ✨ Feature Roadmap

- Engine.IO v4 (WebSocket transport)
- Socket.IO v4 framing
- Event-based API (`On`, `Emit`)
- Namespaces
- Automatic reconnect
- Acknowledgements
- Binary payloads
- Standalone (Editor / Desktop) support
- WebGL support via JavaScript bridge

> ⚠️ This project is under active early development.

---

## 🎯 Goals

- Provide a **transparent, inspectable, and extensible** Socket.IO client for Unity
- Maintain **protocol correctness** over undocumented hacks
- Ensure **identical behavior across Standalone and WebGL**
- Remain **clean-room compliant** and legally safe
- Serve as a long-term **community-driven alternative** to closed-source solutions

---

## 🚫 Non-Goals

- Supporting Socket.IO v1 or v2
- Supporting Engine.IO long-polling
- Copying or mirroring any existing Socket.IO client implementation
- Being a drop-in replacement for any paid asset

---

## 📦 Supported Platforms (Planned)

| Platform | Status |
|--------|--------|
| Unity Editor | 🚧 |
| Windows / macOS / Linux | 🚧 |
| WebGL | 🚧 |
| Mobile | ❓ (community tested) |


## 📦 Dependencies

This project uses a **pluggable transport abstraction** (`ITransport`).

Depending on the target platform, it relies on:

- **System.Net.WebSockets** — Standalone / Desktop builds
- **NativeWebSocket** — WebGL builds  
  (used by `Core/Transport/WebSocketTransport.cs`)

All third-party dependencies are used **as-is** and are accessed strictly
through the `ITransport` abstraction layer.

---

## 🧠 Usage (Planned API)

```csharp
var socket = SocketIOClient.Connect("https://localhost:3000");

socket.On("connect", () =>
{
    Debug.Log("Connected!");
});

socket.On("chat", data =>
{
    Debug.Log(data);
});

socket.Emit("chat", "Hello from Unity!");
