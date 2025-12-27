# socketio-unity

An open-source, clean-room implementation of the **Socket.IO v4 client for Unity**.

This project enables Unity applications to communicate with Socket.IO–powered backends
(such as Node.js microservices) using a familiar **event-based `On` / `Emit` API**, with
support for **Standalone and WebGL builds**.

The implementation is written **from scratch**, based solely on public protocol
documentation and observed network behavior.

---

## ✨ Features (Planned & In Progress)

- ✅ Socket.IO v4 protocol (WebSocket transport)
- ✅ Engine.IO v4 handshake & heartbeat
- 🚧 Standalone (Editor / Desktop) support
- 🚧 WebGL support via JavaScript bridge
- 🚧 Event-based API (`On`, `Emit`)
- 🚧 Automatic reconnect
- 🚧 Namespaces
- 🚧 Acknowledgements
- 🚧 Binary payloads

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
