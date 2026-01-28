# Basic Chat Sample

A minimal "Hello World" example demonstrating how to use **socketio-unity** safely in production.

## ✨ What This Sample Shows

✅ **Connection lifecycle** - Connecting to a Socket.IO server  
✅ **Event handling** - Sending and receiving custom events  
✅ **Auto-reconnect** - Built-in reconnection behavior  
✅ **Main-thread safety** - All callbacks execute on Unity's main thread  
✅ **Proper cleanup** - Unsubscribing events to prevent memory leaks

## 🚫 Non-Goals

This sample intentionally **does not** demonstrate:
- Rooms or lobbies
- State synchronization
- Gameplay-specific patterns
- Advanced features (ACKs, namespaces, binary data)

**Why?** This is a "Hello World" meant to be simple enough to memorize. It uses **only APIs guaranteed stable for v1.x**.

---

## 📋 Prerequisites

- **TextMesh Pro** - This sample uses TMP_Text and TMP_InputField components
  - Unity will prompt to import TMP Essentials on first use
- **SocketIOManager** - A singleton script that manages the Socket.IO client lifecycle
  - Included in `Samples/SocketIOManager.cs`
  - Must be added to the scene as a GameObject

---

## 🎮 How to Use

### 1. Import the Sample

1. Open **Package Manager** (Window → Package Manager)
2. Select **Socket.IO Unity Client** package
3. Expand **Samples** section
4. Click **Import** next to "Basic Chat"

### 2. Set Up the Test Server

This sample requires a Socket.IO server that echoes `chat` events.

#### Option A: Use the Included Test Server

If you have the `socketio-test-server` from the repository:

```bash
cd socketio-test-server
npm install
node server.js
```

The server will start on `ws://localhost:3000`.

#### Option B: Minimal Server (3 Lines)

Create your own echo server:

```js
const io = require('socket.io')(3000);

io.on('connection', socket => {
  socket.on('chat', msg => socket.emit('chat', msg));
});
```

### 3. Open the Unity Scene

1. Navigate to `Assets/SocketIOUnity/Samples/BasicChat/`
2. Open `BasicChatScene.unity` in Unity
3. Press **Play**
4. Type messages in the input field and click **Send**

You should see:
- **Connection status** updates (Connecting → Connected)
- **Your messages** echoed back from the server
- **Reconnection behavior** if you stop/restart the server

---

## 🏗️ Scene Structure

```
BasicChatScene
├── Canvas (UI)
│   ├── Panel
│   │   ├── Scroll View → ChatLog (TMP_Text, displays messages)
│   │   ├── InputField → MessageInput (TMP_InputField)
│   │   ├── SendButton (Button)
│   │   └── StatusText (TMP_Text, connection status)
│   └── BasicChatUI (MonoBehaviour script)
│
├── EventSystem
└── SocketIOManager (singleton GameObject)
    └── SocketIOManager.cs (manages Socket.IO client)
```

---

## 🧠 Understanding the Code

### Core API Usage

```csharp
// 1. Get socket from SocketIOManager singleton
socket = SocketIOManager.Instance.Socket;

// 2. Subscribe to events (in Start())
socket.OnConnected += OnConnected;
socket.OnDisconnected += OnDisconnected;
socket.OnError += OnError;
socket.On("chat", OnChatMessage);

// 3. Connect to server
socket.Connect("ws://localhost:3000");

// 4. Send events
socket.Emit("chat", messageText);

// 5. Clean up in OnDestroy() (prevents memory leaks)
socket.OnConnected -= OnConnected;
socket.OnDisconnected -= OnDisconnected;
socket.OnError -= OnError;
socket.Off("chat", OnChatMessage);
```

### Main-Thread Safety

All Socket.IO callbacks execute on Unity's main thread, so it's safe to:
- Update UI elements (`Text`, `InputField`)
- Instantiate GameObjects
- Call Unity APIs

**No** `UnityMainThreadDispatcher` or coroutines required.

---

## 🌐 Platform Support

This sample works identically on:
- ✅ **Editor** (Windows, macOS, Linux)
- ✅ **Standalone** builds (Windows, macOS, Linux)
- ✅ **WebGL** builds

The transport layer is automatically selected based on the platform.

---

## 🔒 API Stability Guarantee

All APIs used in this sample are **frozen for v1.x**:

| API | Status |
|-----|--------|
| `Connect(string url)` | ✅ Stable |
| `Emit(string eventName, string data)` | ✅ Stable |
| `On(string eventName, Action<string> handler)` | ✅ Stable |
| `Off(string eventName, Action<string> handler)` | ✅ Stable |
| `OnConnected`, `OnDisconnected`, `OnError` | ✅ Stable |

**No breaking changes** will occur in any v1.x release.

---

## 💡 Next Steps

Once you understand this sample, explore:
- **Namespaces** (`socket.Of("/custom")`) for logical separation
- **Binary events** for sending images/files
- **Acknowledgments** for request-response patterns
- **Room-based multiplayer** patterns (see future samples)

---

## 🐛 Troubleshooting

### "Connection failed"
- Verify the server is running on `localhost:3000`
- Check server console for error messages
- Ensure firewall allows WebSocket connections

### "Messages not appearing"
- Check the server is echoing `chat` events correctly
- Open browser console (for WebGL) to see network activity
- Verify `SocketIOManager` GameObject exists in the scene hierarchy
- Ensure `BasicChatUI` script has all UI references assigned in the Inspector

### "NullReferenceException on SocketIOManager.Instance"
- Make sure the `SocketIOManager` GameObject is in the scene
- Check that `SocketIOManager.cs` is attached to the GameObject
- The SocketIOManager must be active before `BasicChatUI.Start()` executes

### WebGL-Specific Issues
- WebGL requires **CORS enabled** on the server
- Use `ws://localhost:3000` (not `https://` for local testing)
- Check browser console for CORS errors

---

## 📚 Further Reading

- [Socket.IO Protocol Docs](https://socket.io/docs/v4/)
- [Package README](../../README.md)
- [API_STABILITY.md](../../API_STABILITY.md) - Version guarantees
