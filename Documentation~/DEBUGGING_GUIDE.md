# Debugging Guide

> Tools and techniques for diagnosing SocketIOUnity issues

---

## Quick Start

```csharp
// Enable verbose tracing
TraceConfig.Level = TraceLevel.Verbose;

// Watch Unity Console for [SocketIO:EngineIO], [SocketIO:Binary], etc.
```

---

## Trace System

### Configuration

```csharp
using SocketIOUnity.Debugging;

// Set trace level
TraceConfig.Level = TraceLevel.Protocol;
```

### Trace Levels

| Level | Output |
|-------|--------|
| `None` | Nothing (default) |
| `Errors` | Connection errors only |
| `Protocol` | Errors + packet flow |
| `Verbose` | Everything (very noisy) |

### Categories

| Category | Description |
|----------|-------------|
| `EngineIO` | Low-level transport events |
| `SocketIO` | Event dispatch and handling |
| `Transport` | WebSocket open/close/errors |
| `Binary` | Binary frame assembly |
| `Reconnect` | Reconnection attempts |
| `Namespace` | Namespace connect/disconnect |
| `Ack` | Acknowledgement tracking |

### Sample Output (Protocol Level)

```
[SocketIO:EngineIO] Handshake received (sid=abc123, pingInterval=25000ms, pingTimeout=20000ms)
[SocketIO:SocketIO] → CONNECT /
[SocketIO:SocketIO] ← CONNECT / {"sid":"xyz789"}
[SocketIO:SocketIO] → EVENT ["chat","hello"]
[SocketIO:SocketIO] ← EVENT ["chat","world"]
[SocketIO:Binary] ← BINARY 256 bytes
[SocketIO:Reconnect] Reconnect attempt 1 firing now
```

---

## Custom Trace Sinks

### File Logger

```csharp
public class FileTraceSink : ITraceSink
{
    private StreamWriter _writer;

    public FileTraceSink(string path)
    {
        _writer = new StreamWriter(path, append: true);
    }

    public void Emit(in TraceEvent evt)
    {
        _writer.WriteLine($"[{evt.Timestamp:HH:mm:ss.fff}] [{evt.Category}] {evt.Message}");
        _writer.Flush();
    }
}

// Register
SocketIOTrace.SetSink(new FileTraceSink("socketio.log"));
```

### UI Overlay

```csharp
public class UITraceSink : ITraceSink
{
    private Queue<string> _lines = new Queue<string>();

    public void Emit(in TraceEvent evt)
    {
        _lines.Enqueue($"[{evt.Category}] {evt.Message}");
        while (_lines.Count > 20) _lines.Dequeue();
    }

    public string GetText() => string.Join("\n", _lines);
}
```

---

## Unity Profiler Integration

### Enable Profiler Markers

Add scripting define:
```
SOCKETIO_PROFILER
```

### View in Profiler

1. Window → Analysis → Profiler
2. Select CPU Usage
3. Look under "Scripts" for SocketIO markers

### Available Markers

| Marker | Purpose |
|--------|---------|
| `SocketIO.EngineIO.Parse` | Packet parsing time |
| `SocketIO.Event.Dispatch` | Handler execution |
| `SocketIO.Binary.Assemble` | Binary reconstruction |
| `SocketIO.Ack.Resolve` | ACK callback time |
| `SocketIO.Reconnect.Tick` | Reconnect loop overhead |

---

## Profiler Counters

### Enable Counters

Add scripting define:
```
SOCKETIO_PROFILER_COUNTERS
```

### Available Counters

| Counter | Category |
|---------|----------|
| Bytes Sent | Network |
| Bytes Received | Network |
| Packets/sec | Network |
| Active Namespaces | Scripts |
| Pending ACKs | Scripts |

### View in Profiler

1. Open Profiler
2. Click gear icon → Profiler Modules
3. Enable "Custom Module"
4. View SocketIO counters

---

## Common Issues

### "Connection Refused"

**Symptoms:**
```
[SocketIO:Transport] Transport error: Connection refused
```

**Checklist:**
- [ ] Server running on correct port?
- [ ] Firewall blocking connection?
- [ ] Using correct protocol (ws:// vs wss://)?

### "Heartbeat Timeout"

**Symptoms:**
```
[SocketIO:EngineIO] Engine.IO heartbeat timeout
```

**Causes:**
- Network latency > `pingTimeout`
- Server overloaded
- Long-running handler blocking main thread

**Fix:**
- Increase server `pingTimeout`
- Check for blocking code in event handlers

### "Namespace Auth Failed"

**Symptoms:**
```
[SocketIO:Namespace] /admin connect_error: unauthorized
```

**Checklist:**
- [ ] Auth object matches server expectations?
- [ ] Token not expired?
- [ ] Using `Of()` with auth parameter?

```csharp
// Correct
socket.Of("/admin", new { token = "secret" });

// Wrong (no auth)
socket.Of("/admin");
```

### "Binary Event Not Received"

**Symptoms:**
- Handler never called for binary events

**Checklist:**
- [ ] Handler signature matches? (`byte[]`, not `string`)
- [ ] Event name matches exactly?
- [ ] Server sending binary correctly?

```csharp
// ✅ Correct
socket.On("file", (byte[] data) => { ... });

// ❌ Wrong type
socket.On("file", (string data) => { ... });
```

### "Reconnect Loop"

**Symptoms:**
```
[SocketIO:Reconnect] Reconnect attempt 1 firing now
[SocketIO:Reconnect] Reconnect attempt 2 firing now
...forever
```

**Causes:**
- Server immediately rejecting connections
- Auth always failing
- Server port changed

---

## Debug Checklist

### Connection Issues

1. [ ] Enable `TraceLevel.Protocol`
2. [ ] Verify server URL (including port)
3. [ ] Check handshake in logs
4. [ ] Verify ping/pong cycle

### Event Issues

1. [ ] Verify event name matches exactly
2. [ ] Check handler signature
3. [ ] Enable `TraceLevel.Verbose`
4. [ ] Log received events to find name

### Binary Issues

1. [ ] Enable `TraceLevel.Verbose`
2. [ ] Check "← BINARY" messages
3. [ ] Verify attachment count in packet
4. [ ] Check placeholder replacement

### Namespace Issues

1. [ ] Root namespace connected first?
2. [ ] Auth object serializes correctly?
3. [ ] Server has matching namespace defined?
4. [ ] Check for `connect_error` event

---

## Network Debugging

### Wireshark Filter

```
tcp.port == 3000 and websocket
```

### Browser DevTools (WebGL)

1. F12 → Network tab
2. Filter: "WS"
3. Click connection → Messages tab

### Expected Handshake

```
→ GET /socket.io/?EIO=4&transport=websocket
← 0{"sid":"xxx","upgrades":[],"pingInterval":25000,"pingTimeout":20000}
→ 40
← 40{"sid":"abc"}
← 42["hello",{}]
```

---

## Test Server

For debugging, use the test server from the main README:

1. Copy the `server.js` code from README's **"Test Server Setup"** section
2. Run:
   ```bash
   npm init -y && npm install socket.io
   node server.js
   ```

### Available Namespaces

| Namespace | Auth | Behavior |
|-----------|------|----------|
| `/` | No | Full test suite |
| `/admin` | `token: "test-secret"` | Auth success |
| `/admin-bad` | Always fails | Test auth rejection |
| `/public` | No | Simple namespace |
| `/webgl` | No | WebGL browser testing |

---

## Reporting Issues

When reporting bugs, include:

1. **Trace log** at `TraceLevel.Verbose`
2. **Unity version**
3. **Target platform** (Editor/Desktop/WebGL)
4. **Server framework** (Node.js Socket.IO version)
5. **Minimal reproduction** steps
