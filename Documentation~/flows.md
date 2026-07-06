# Connection and Event Flows

> Permission-relevant journeys, trust-boundary crossings, and side effects for each major flow.

---

## 1. Connection Establishment

**Entry point:** `SocketIOClient.Connect(string url)`

```
Game code
  │
  ▼ Connect(url) — stores _lastUrl, sets state = Connecting
SocketIOClient
  │
  ▼ EngineIOClient.Connect(url) — BuildEngineIOUrl() prepends ws/wss, appends ?EIO=4&transport=websocket
ITransport (WebSocketTransport or WebGLWebSocketTransport)
  │
  ▼ [WebSocket opened — no callback yet, Engine.IO v4 waits for OPEN packet]
  │
  ▼ TEXT "0{...}" — Engine.IO OPEN packet with HandshakeInfo {sid, pingInterval, pingTimeout}
EngineIOClient.HandleOpen()
  ├── HeartbeatController.Start(pingInterval, pingTimeout) — watchdog armed
  ├── PingRttTracker.SetPingInterval()
  ├── _isConnected = true
  └── OnOpen → SocketIOClient.HandleEngineOpen()
              └── _defaultNamespace.SendConnect() — Socket.IO "0" or "0/ns,{auth}" packet
                  │
                  ▼ Server replies "0" or "0/ns,{...}"
              SocketIOClient.HandleEngineMessage()
              └── case Connect, ns == "/":
                    ├── SetState(Connected)
                    ├── _reconnect.Reset()
                    ├── OnConnected event fires (game code callback)
                    └── non-default namespaces get SendConnect()
```

**Trust boundary:** The server's `HandshakeInfo` is deserialized with `Newtonsoft.Json`. Malformed JSON throws — caught, error emitted, connection aborted.

**Side effects:** Two `MonoBehaviour` GameObjects are created on first connection if absent (`[SocketIO] MainThreadDispatcher`, `[SocketIOUnity Tick Driver]`). Both are `DontDestroyOnLoad`.

---

## 2. Intentional Disconnect

**Entry point:** `SocketIOClient.Disconnect()` or `SocketIOClient.Shutdown()`

```
Game code → Disconnect()
  ├── _intentionalDisconnect = true
  ├── SetState(Disconnected)
  ├── _reconnect.Stop()
  └── DestroyEngine()
        ├── EngineIOClient.Disconnect() → transport.Close()
        └── _engine = null

Transport close triggers HandleTransportClose()
  ├── Cleanup() — UnityTickDriver.Unregister, _isConnected = false, heartbeat stopped
  └── OnClose → SocketIOClient.HandleEngineClose()
        ├── OnDisconnected fires
        ├── each ns.HandleDisconnect() — fires OnDisconnected + "disconnect" event, clears ACKs
        └── _intentionalDisconnect == true → early return (no reconnect)
```

`Shutdown()` additionally calls `UnityTickDriver.Unregister(this)` for `SocketIOClient` itself.

---

## 3. Automatic Reconnect

**Trigger:** `HandleEngineClose()` when `_intentionalDisconnect == false` and `autoReconnect == true`.

```
HandleEngineClose()
  ├── OnDisconnected fires
  ├── per-namespace HandleDisconnect() — all ACKs cleared
  ├── SetState(Reconnecting)
  └── _reconnect.Start() [idempotent — guarded by _enabled check]

ReconnectController.Tick() [called every frame via SocketIOClient.Tick()]
  ├── checks _config.maxAttempts — if exhausted: Stop() + HandleReconnectExhausted() → SetState(Disconnected)
  └── if Time.time >= _nextAttemptTime:
        ├── _attempt++
        ├── AttemptReconnect() → ReconnectEngine() (engine recreated, _namespaces PRESERVED)
        │     └── _engine.Connect(_lastUrl) — full handshake restarts
        └── ScheduleNext() — exponential backoff: min(initialDelay × multiplier^attempt, maxDelay) ± jitter

On reconnect success (OPEN + CONNECT "/" received):
  ├── SetState(Connected)
  ├── _reconnect.Reset()
  ├── OnConnected fires
  └── non-default namespaces automatically send CONNECT (handlers already registered survive)
```

**Invariant:** `ReconnectEngine()` preserves `_namespaces`. `On()` subscriptions from before disconnect are still active after reconnect.

---

## 4. Namespace Authentication

**Entry point:** `SocketIOClient.Of(string ns, object auth)` or first call to `_namespaces.Get(ns, auth)`.

```
NamespaceManager.Get("/admin", new { token = "abc" })
  ├── creates NamespaceSocket("/admin", root, auth={token="abc"})
  └── if root "/" already connected → immediately SendConnect()

NamespaceSocket.SendConnect()
  └── builds packet: "0/admin,{\"token\":\"abc\"}"  ← auth in plaintext
      └── SocketIOClient.SendEnginePacket("4" + packet) → transport
```

**Auth constraint:** Auth is captured at namespace creation and cannot be changed for an existing namespace (warning logged, new auth ignored). Auth is re-sent on every reconnect automatically because `SendConnect()` is called again.

**Trust boundary:** Auth payload is serialized to JSON and sent in plaintext in the Socket.IO CONNECT packet. Users must use `wss://` in production if auth contains secrets.

---

## 5. Event Emission

**Entry point:** `socket.Emit(eventName, payload)` or `socket.Emit(eventName, payload, ack, timeoutMs)`.

```
SocketIOClient.Emit(eventName, payload)
  └── _defaultNamespace.Emit(eventName, payload)
        ├── guard: if (!_connected) return  ← silently drops if not connected
        └── _root.EmitInternal("/", eventName, payload, null)
              ├── json = JsonConvert.SerializeObject([eventName, payload])
              ├── packet = "2" + json                    (type EVENT = 2)
              └── _engine.SendRaw("4" + packet)
                    └── transport.SendText("42[\"eventName\",...]")

With ACK:
  └── ackId = _acks.Register(callback, TimeSpan.FromMilliseconds(timeoutMs))
      packet = "2" + ackId + json
```

**Side effect:** `AckRegistry` stores the callback with expiry. `NamespaceSocket.Tick()` calls `_acks.RemoveExpired()` each frame — timed-out ACKs are silently discarded (no error callback).

---

## 6. Event Receipt

**Path:** server text frame → game code callback

```
Transport.OnTextMessage(raw)
  └── EngineIOClient.HandleTextMessage(raw)
        ├── validates Engine.IO type byte (0-4 range)
        └── type == Message (4):
              OnMessage → SocketIOClient.HandleEngineMessage(payload)
                └── SocketPacketParser.Parse(payload)
                      ├── returns null on malformed → OnError(Protocol) + return
                      └── returns SocketPacket {Type, Namespace, AckId, JsonPayload}

switch packet.Type:
  case Event:
    nsSocket.HandleEvent(JsonPayload)
      ├── JArray.Parse(JsonPayload)
      ├── eventName = arr[0]
      ├── data = arr[1] (string) or null
      └── EventRegistry.Emit(eventName, data)
            └── per handler: UnityMainThreadDispatcher.Enqueue(() => handler(data))

  case Ack:
    nsSocket.HandleAck(ackId, JsonPayload)
      └── AckRegistry.Resolve(ackId, payload)
            └── UnityMainThreadDispatcher.Enqueue(() => callback(payload))
```

**Main-thread dispatch:** All `Action<string>` and `Action<byte[]>` handlers are guaranteed to run on Unity's main thread via `ConcurrentQueue<Action>` drained in `UnityMainThreadDispatcher.Update()`.

---

## 7. Binary Event Flow

**Protocol:** Socket.IO binary events send a JSON frame with `{"_placeholder":true,"num":N}` tokens, followed by N raw binary WebSocket frames.

```
Server sends: TEXT "451-/ns,[\"event\",{\"_placeholder\":true,\"num\":0}]"
              BINARY <raw bytes>

EngineIOClient.HandleTextMessage → type == Message(4) → OnMessage:
  SocketIOClient.HandleEngineMessage:
    case BinaryEvent:
      if _binaryAssembler.IsWaiting → Abort() (overlapping packet protection)
      _binaryAssembler.Start(packet)  ← stores expected count, parses JSON skeleton

EngineIOClient.HandleBinaryMessage → OnBinary:
  SocketIOClient.HandleEngineBinary(data):
    _binaryAssembler.AddBinary(data)
    if count == expected:
      _binaryAssembler.Build()
        ├── ReplacePlaceholders() — walks JArray, swaps placeholders with byte[] buffers
        └── returns (type, ackId, eventName, args, ns)
      nsSocket.HandleBinaryEvent(eventName, args)
        └── EventRegistry.EmitBinary(eventName, bytes)
              └── UnityMainThreadDispatcher.Enqueue(() => handler(bytes))
```

---

## 8. Heartbeat

**Direction:** Engine.IO v4 — server pings, client pongs.

```
HeartbeatController.Start(pingInterval, pingTimeout)
  └── arms watchdog; timeout = (pingInterval + pingTimeout) ms

Server sends PING ("2") every pingInterval ms:
  HandleEngineMessage → Ping:
    ├── PingRttTracker.OnPingReceived()
    ├── transport.SendText("3")  ← PONG
    └── HeartbeatController.OnPing() ← resets watchdog

HeartbeatController.Tick() [every frame]:
  if now > _lastPingTime + timeoutSeconds:
    OnTimeout → OnError(Timeout) + Disconnect()
```

---

## Not applicable

- **Emails** — this library sends no email and has no notification templates.
- **Cron / scheduled jobs** — no background jobs exist in the package or test servers.
- **SEO** — the GitHub Pages WebGL demo has no SEO metadata management layer.
- **Embedded agents / automation** — no AI agents or automation pipelines in the codebase.
