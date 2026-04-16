# Getting Started — Build Multiplayer in 5 Minutes

This guide takes you from zero to a working multiplayer connection in Unity. No prior Socket.IO experience needed.

---

## What You'll Build

A Unity scene that connects to a local server, sends a message, and receives it back. Once this works, you're ready for the [PlayerSync](../package/Samples~/PlayerSync/README.md) and [Lobby](../package/Samples~/Lobby/README.md) samples.

---

## Step 1: Install the Package (1 min)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL**
3. Paste:

```
https://github.com/Magithar/socketio-unity.git?path=/package
```

4. Click **Add**

> Requires Unity 2020.1+ and internet access for the initial import.

---

## Step 2: Start the Test Server (1 min)

You need Node.js installed. ([Download here](https://nodejs.org) if you don't have it.)

```bash
cd TestServer~
npm install
npm run start:basicchat
```

You should see:

```
BasicChat server listening on :3002
```

Leave this terminal open.

---

## Step 3: Import the Basic Chat Sample (1 min)

1. In Package Manager, find **Socket.IO Unity Client**
2. Expand the **Samples** section
3. Click **Import** next to **Basic Chat**

Unity imports the sample into `Assets/Samples/Socket.IO Unity Client/`.

> If Unity prompts to import **TextMesh Pro Essentials** — click Import.

---

## Step 4: Open the Scene and Press Play (30 sec)

1. Open `BasicChatScene.unity` from the imported sample folder
2. Press **Play**
3. Type anything in the input field → click **Send**

You should see your message echoed back in the chat log.

**Status shows "Connected"?** You're done. The library is working.

---

## What Just Happened

```
Unity (BasicChatUI)
    │
    ├── socket.Connect("ws://localhost:3002")    ← opens WebSocket
    ├── socket.Emit("chat", "Hello!")            ← sends event to server
    └── socket.On("chat", msg => ...)            ← receives echo back

Node.js (basicchat-server.js)
    └── socket.on("chat", msg => socket.emit("chat", msg))  ← echo
```

The key APIs — `Connect`, `Emit`, `On`, `Off` — are the same across all platforms (Editor, Standalone, WebGL, Mobile). All callbacks execute on Unity's main thread automatically.

---

## Common Issues

**"Connection failed" / status stays "Connecting"**
- Is the server running? Check the terminal for `BasicChat server listening on :3002`
- Is port 3002 blocked by a firewall? Try disabling it temporarily

**"NullReferenceException on SocketIOManager.Instance"**
- The `SocketIOManager` GameObject must be in the scene — it's included in `BasicChatScene.unity`
- If you built your own scene, add an empty GameObject with `SocketIOManager.cs` attached

**Messages not appearing in chat log**
- Check that `BasicChatUI` has all Inspector references assigned (StatusText, ChatLog, MessageInput)
- Open Unity Console for errors

**WebGL build — no connection**
- Use `ws://` not `wss://` for local testing
- Enable CORS on your server
- Check browser console (F12) for errors

---

## Next Steps

You've got the basics. Here's where to go next:

| I want to... | Go here |
|---|---|
| Sync player positions across clients | [PlayerSync sample](../package/Samples~/PlayerSync/README.md) |
| Build a multiplayer lobby with rooms | [Lobby sample](../package/Samples~/Lobby/README.md) |
| See lobby + match flow end-to-end | [LiveDemo sample](../package/Samples~/LiveDemo/README.md) |
| Integrate Socket.IO with Mirror | [Mirror Integration sample](../package/Samples~/MirrorIntegration/README.md) |
| See connection state / RTT in-game | Enable `SocketIOManager.Instance.ShowDiagnostics = true` |
| Run on WebGL / browser | [WebGL Notes](WEBGL_NOTES.md) |
| Understand the full API | [README API Guide](../README.md#usage) |
| Debug network traffic | [Debugging Guide](DEBUGGING_GUIDE.md) |
| Understand reconnection | [Reconnect Behavior](RECONNECT_BEHAVIOR.md) |

---

## The Learning Path

```
Basic Chat (this guide)
    ↓
PlayerSync — real-time position sync, namespaces, reconnect
    ↓
Lobby — rooms, host migration, session identity, reconnect recovery
    ↓
LiveDemo — end-to-end lobby → match flow in a single scene
    ↓
Mirror Integration — hybrid Socket.IO + Mirror architecture (requires Mirror)
```

Each sample builds on the previous one. Read the sample READMEs — they explain the architecture, not just the setup.
