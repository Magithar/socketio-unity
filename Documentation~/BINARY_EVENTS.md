# Binary Events

> How SocketIOUnity handles binary data (images, files, buffers)

---

## Protocol Overview

Socket.IO v4 uses a **placeholder-based** binary protocol:

1. Server sends JSON with `{"_placeholder": true, "num": N}` markers
2. Additional binary frames follow in order
3. Client reconstructs by replacing placeholders with actual buffers

### Example Wire Format

**Server sends image:**
```
51-["image",{"_placeholder":true,"num":0}]  ← JSON with placeholder
<binary frame: actual image bytes>           ← Raw binary follows
```

---

## Receiving Binary

### Single Buffer

```csharp
socket.On("file", (byte[] data) =>
{
    Debug.Log($"📦 Received {data.Length} bytes");
    File.WriteAllBytes("downloaded.bin", data);
});
```

### Multiple Buffers

```csharp
socket.On("chunks", (byte[] part1) =>
{
    // First buffer of multi-buffer event
    Debug.Log($"📦 Part 1: {part1.Length} bytes");
});

// Additional buffers are delivered via subsequent binary frames
```

### JSON + Binary Mixed

```csharp
socket.On("upload-result", (string json) =>
{
    // If server sends { success: true, data: <Buffer> }
    // The JSON handler receives the metadata
    var result = JsonConvert.DeserializeObject<UploadResult>(json);
});
```

---

## Emitting Binary

### Simple Binary Emit

```csharp
byte[] payload = File.ReadAllBytes("data.bin");
socket.Emit("upload", payload);
```

### With Acknowledgement

```csharp
byte[] payload = File.ReadAllBytes("image.png");
socket.Emit("upload", payload, (response) =>
{
    Debug.Log($"✅ Server ACK: {response}");
});
```

---

## Assembly Process

The `BinaryPacketAssembler` handles reconstruction:

```
┌─────────────────────────────────────────────────────────┐
│              Incoming Binary Event Flow                  │
└─────────────────────────────────────────────────────────┘

1. Receive "51-[...]" packet
   └──→ Parse attachment count (1) and JSON payload
   └──→ Store in BinaryPacketAssembler.Start()

2. Receive binary frame
   └──→ AddBinary() stores buffer
   └──→ Returns true when count matches expected

3. Build final event
   └──→ ReplacePlaceholders() recursively swaps markers
   └──→ Dispatch to event handlers
```

### Code Path

```csharp
// BinaryPacketAssembler.cs
public bool AddBinary(byte[] data)
{
    _buffers.Add(data);
    return _buffers.Count == _expected;
}

public (string eventName, JArray args, string ns) Build()
{
    ReplacePlaceholders(_json);
    return (eventName, _json, _namespace);
}
```

---

## Placeholder Format

Socket.IO uses this JSON structure:

```json
{
  "_placeholder": true,
  "num": 0
}
```

Where `num` is the zero-indexed position in the binary frame sequence.

### Recursive Replacement

Placeholders can be nested in arrays or objects:

```json
["event", { "file": {"_placeholder": true, "num": 0}, "meta": "info" }]
```

The assembler walks the entire JSON tree to find and replace all placeholders.

---

## Performance

### Memory Pooling

Binary assembly uses pooled lists to avoid GC:

```csharp
var children = ListPool<JToken>.Rent();
// ... process ...
ListPool<JToken>.Return(children);
```

### Profiler Integration

Binary assembly is instrumented:

```csharp
using (SocketIOProfiler.Binary_Assembly.Auto())
{
    _buffers.Add(data);
    return _buffers.Count == _expected;
}
```

---

## Packet Types

| Type | Code | Description |
|------|------|-------------|
| EVENT | 2 | Text event |
| **BINARY_EVENT** | **5** | Binary event (with attachments) |
| ACK | 3 | Text acknowledgement |
| **BINARY_ACK** | **6** | Binary acknowledgement |

### Format

```
5<attachment-count>-<namespace>,<ack-id>["event",{placeholder}]
```

Example: `51-/admin,42["upload",{"_placeholder":true,"num":0}]`

---

## Error Handling

| Error | Handling |
|-------|----------|
| Malformed packet | Logged, frame discarded |
| Missing binary frames | Assembly times out (not yet implemented) |
| Placeholder index OOB | Logged, placeholder left in place |
| Disconnect mid-assembly | `Abort()` clears pending state |

---

## Server-Side Example

**Node.js (Socket.IO v4):**

```javascript
// Emit single buffer
const buffer = Buffer.from("Hello");
socket.emit("file", buffer);

// Emit multiple buffers
const buf1 = Buffer.from([1, 2, 3]);
const buf2 = Buffer.from([4, 5, 6]);
socket.emit("multi", buf1, buf2);

// With ACK
socket.emit("binary-ack", buffer, (response) => {
    console.log("Client responded:", response);
});
```

---

## WebGL Considerations

Binary works in WebGL via the jslib bridge:

```javascript
ws.onmessage = (e) => {
    if (typeof e.data === "string") {
        SendMessage("WebGLSocketBridge", "JSOnText", e.data);
    } else {
        // Binary handling
        const bytes = new Uint8Array(e.data);
        const ptr = _malloc(bytes.length);
        HEAPU8.set(bytes, ptr);
        SendMessage("WebGLSocketBridge", "JSOnBinary", ptr + "," + bytes.length);
        _free(ptr);
    }
};
```

> ✅ **WebGL binary support verified** — tested with single buffers, multi-buffer events, and binary ACKs
