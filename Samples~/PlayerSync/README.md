# PlayerSync Sample

## Overview

This sample demonstrates real-time multiplayer player synchronization using Socket.IO with Unity. Players can move around and see other connected players in real-time. The sample includes:

- Real-time position synchronization
- Player join/leave detection
- RTT (Round Trip Time) display
- Production-grade architecture with proper separation of concerns
- Socket.IO namespace support (`/playersync`)
- **Full WebGL support** - runs in browsers with automatic transport detection

## Prerequisites

**New to Socket.IO Unity?** Start with the [BasicChat sample](../BasicChat/README.md) first.

BasicChat covers the fundamentals in a simpler context:
- Connection lifecycle (connect, disconnect, error handling)
- Basic event handling (`Emit`, `On`, `Off`)
- Main-thread safety guarantees
- Proper cleanup and unsubscribe patterns

**PlayerSync builds on these concepts and adds:**
- Multi-client synchronization (seeing other players in real-time)
- Namespace-based architecture (`/playersync` instead of root `/`)
- Advanced reconnection handling with visual feedback
- Production-grade component organization and dependency injection
- Network interpolation for smooth movement
- Configurable reconnection strategies (ReconnectConfig with jitter)

If you're comfortable with BasicChat, you're ready for PlayerSync!

## Security Notice

The included server is a minimal development server for local testing only.
It does not implement authentication, validation, or abuse protection.

**Do not use this server code in production environments.**

## Setup Instructions

### Requirements

- Unity 2020.3 or later
- Node.js 14+ and npm
- TextMeshPro package (should be auto-imported)

### Unity Setup

The scene hierarchy follows a production-grade architecture:

```
PlayerSyncScene
  - Main Camera
  - Directional Light
  - Plane
  - LocalPlayer (Blue capsule - your player)
      - PlayerController component
  - EventSystem
  - PlayerSyncManager
      - PlayerNetworkSync component
      - PlayerSpawner component
  - Canvas
      - RTTText (TMP) - Shows ping/latency
      - StatusText (TMP) - Shows connection status
      - UIManager
          - RTTDisplay component
              - Rtt Text: Reference to RTTText
              - Network Sync: Reference to PlayerSyncManager
          - ConnectionStatusDisplay component
              - Status Text: Reference to StatusText
              - Network Sync: Reference to PlayerSyncManager
  - DontDestroyOnLoad (for spawned remote players)
```

**Key Architecture Principles:**

- **Separation of Concerns**: UI logic on UI GameObjects, network logic on managers
- **Dependency Injection**: Components get references via SerializeField
- **Namespace Pattern**: Connect to root socket first, then use `.Of("/namespace")`

**Required Setup in Inspector:**

After opening the scene, configure these references in the Inspector:

1. **Canvas Setup** (if not already done):
   - Right-click Canvas → UI → Text - TextMeshPro (create two: "RTTText" and "StatusText")
   - Position RTTText in top-right corner
   - Position StatusText in top-left corner

2. Select **PlayerSyncManager** GameObject
3. In **PlayerNetworkSync** component:
   - **Local Player Transform**: Drag **LocalPlayer** from hierarchy
   - **Controller**: Should already reference LocalPlayer (PlayerController)
   - **Spawner**: Should already reference PlayerSpawner
   - **Reconnection Settings** (optional, defaults to standard exponential backoff):
     - **Initial Delay**: First reconnect attempt delay (default: 1s)
     - **Multiplier**: Backoff growth rate (default: 2 = exponential doubling)
     - **Max Delay**: Maximum delay cap (default: 30s)
     - **Max Attempts**: Limit reconnection attempts (default: -1 = unlimited)
     - **Auto Reconnect**: Enable automatic reconnection (default: true)
     - **Jitter Percent**: Random variance to prevent thundering herd (default: 0 = disabled)

4. In **PlayerSpawner** component:
   - **Remote Player Prefab**: Drag **RemotePlayer** prefab from Prefabs folder

5. Select **UIManager** GameObject (under Canvas)
6. In **RTTDisplay** component:
   - **Rtt Text**: Drag **RTTText** from Canvas
   - **Network Sync**: Drag **PlayerSyncManager** from hierarchy

7. Add **ConnectionStatusDisplay** component to **UIManager**:
   - **Status Text**: Drag **StatusText** from Canvas
   - **Network Sync**: Drag **PlayerSyncManager** from hierarchy

### Server Setup

1. Navigate to the server directory (create if it doesn't exist):

```bash
cd server
```

2. Install dependencies:

```bash
npm install socket.io
```

3. Create `server.js` with the Socket.IO server (see server code below)

4. Start the server:

```bash
node server.js
```

Server should be running on `http://localhost:3000`

## How to Run

### Testing Multiplayer

To see the full multiplayer experience (including remote players):

1. **Build the game**: File → Build Settings → Build and Run
2. **Play in Editor**: Press Play in Unity Editor
3. **Result**: Both instances will connect and see each other

**Note**: Running only one instance will show just your blue player. The server correctly returns your player in the `existing_players` list, but the client skips spawning itself (see console log: "Skipping self: [player_id]").

### Testing with WebGL

**WebGL builds are fully supported!** The sample automatically uses the correct transport for browser environments.

**Quick Start:**
1. **Build for WebGL**: File → Build Settings → WebGL → Build and Run
2. **Start server**: Ensure your Socket.IO server is running with CORS enabled
3. **Play in Editor**: Press Play in Unity Editor to test multiplayer
4. **Result**: Browser and Editor instances will see each other in real-time

**Manual Build & Serve:**

If you prefer to manually serve the WebGL build:

```bash
# Build from Unity (File → Build Settings → WebGL → Build)
# Then serve the build folder
cd /path/to/webgl-build
npx serve -p 8080
```

**Server Requirements:**

Your Socket.IO server **must have CORS enabled** for WebGL to work:

```javascript
const io = require("socket.io")(3000, {
  cors: {
    origin: "*",  // Or specify your domain
    methods: ["GET", "POST"]
  }
});
```

The sample server code already includes this configuration.

**Server URL Configuration:**

The sample uses platform-specific server URLs configured in the Unity Inspector:

- **Editor**: Uses `editorServerUrl` (default: `http://localhost:3000`)
- **WebGL/Standalone/Mobile**: Uses `productionServerUrl` (default: `http://localhost:3000` for local testing)

**For Local Testing (WebGL):**
- The default `productionServerUrl` is set to `http://localhost:3000` ✅
- WebGL builds will connect to your local Socket.IO server
- No configuration changes needed for local development

**For Production Deployment:**
1. Select **PlayerSyncManager** in Unity Hierarchy
2. In **PlayerNetworkSync** Inspector → **Server Configuration**
3. Change **Production Server Url** to your production server (e.g., `https://your-game-server.com:3000`)
4. Rebuild for WebGL

**Runtime URL Override (Advanced):**
- Users can set custom server URLs via PlayerPrefs without rebuilding
- See `ServerUrlInput.cs` for UI implementation example
- Custom URLs persist across sessions and override Inspector defaults

**What You'll See:**

- **Browser (WebGL)**: Your blue player + Unity Editor's red player
- **Unity Editor**: Your blue player + Browser's red player
- **Connection Status**: "[OK] Connected" in green (top-left)
- **RTT**: Shows 0 ms on localhost (expected - deploy remotely for real latency)

**Browser Console:**

Open Developer Tools (F12) to see:
- Unity Debug.Log output in Console tab
- WebSocket traffic in Network tab (filter by "WS")
- Connection events and player synchronization

**Supported Browsers:**
- ✅ Chrome/Chromium (recommended)
- ✅ Firefox
- ✅ Safari
- ✅ Edge

**WebGL Architecture:**

The sample uses `TransportFactoryHelper.CreateDefault()` which automatically:
- Uses `WebGLWebSocketTransport` for WebGL builds (JavaScript bridge)
- Uses `WebSocketTransport` for standalone builds (native WebSocket)
- Creates `WebGLSocketBridge` GameObject automatically (no manual setup needed)

### Understanding What You See

**IMPORTANT**: This can be confusing at first!

**In EACH window, you will see:**

- **1 BLUE capsule** = YOU (the player you control with WASD)
- **1 RED capsule** = THE OTHER PLAYER (synchronized from the other window)

**Example:**

- **Mac Build window**: Press WASD → BLUE moves (you're controlling it). RED moves automatically (that's the Editor player being synced).
- **Unity Editor window**: Press WASD → BLUE moves (you're controlling it). RED moves automatically (that's the Mac Build player being synced).

**You are ALWAYS blue in your own window.** The red player is always the OTHER window's player, shown to you in real-time. This is exactly how multiplayer games work - you see yourself as one color, and other players as a different color.

### Controls

- **WASD** or **Arrow Keys**: Move your player
- Player movement is clamped to (-10, 10) on X and Z axes
- Movement only enabled after receiving `player_id` from server

## Architecture

### Socket.IO Events Reference

Quick reference for all events used in this sample:

| Event | Direction | Frequency | Purpose |
|-------|-----------|-----------|---------|
| `player_id` | Server → Client | Once (on connect) | Server assigns authoritative player identity |
| `existing_players` | Server → Client | Once (on connect) | Initial state sync - dictionary of all connected players |
| `player_move` | Client ⇄ Server | 20Hz (default) | Broadcast position updates to other clients |
| `player_join` | Server → Others | On join | Notify existing players when new player connects |
| `player_leave` | Server → Others | On leave | Notify remaining players when player disconnects |

**Performance Notes:**
- `player_move` is high-frequency (20Hz = 20 messages/sec per player) - not logged to avoid console spam
- All other events are low-frequency (once per connection/disconnection) - logged for debugging
- With N players, each position update triggers N-1 broadcasts (O(N) complexity per update)

### Connection Pattern

```csharp
// Connect to root first
rootSocket = new SocketIOClient(TransportFactoryHelper.CreateDefault());
rootSocket.Connect("http://localhost:3000");

// Then get namespace
namespaceSocket = rootSocket.Of("/playersync");

// Public API for RTTDisplay compatibility
public SocketIOClient Socket => rootSocket;
```

### Why Namespace Pattern Matters

**The pattern:**
```csharp
rootSocket.Of("/playersync")  // Get namespace socket
```

**Why not just connect directly to `/playersync`?**

Socket.IO namespaces provide **logical multiplexing over a single WebSocket connection**. This architectural pattern offers significant advantages:

**✅ Single Physical Connection:**
```csharp
// ❌ WRONG - Multiple WebSocket connections (wasteful)
var chatSocket = new SocketIOClient(...);
chatSocket.Connect("http://server:3000/chat");

var gameSocket = new SocketIOClient(...);
gameSocket.Connect("http://server:3000/game");

var adminSocket = new SocketIOClient(...);
adminSocket.Connect("http://server:3000/admin");

// ✅ CORRECT - One WebSocket, multiple logical channels
var rootSocket = new SocketIOClient(...);
rootSocket.Connect("http://server:3000");

var chatNs = rootSocket.Of("/chat");    // Logical channel for chat
var gameNs = rootSocket.Of("/game");    // Logical channel for game state
var adminNs = rootSocket.Of("/admin");  // Logical channel for admin commands
```

**Benefits:**
- **Resource Efficiency**: One TCP connection, one thread, shared transport layer
- **Shared Reconnection Logic**: Single `ReconnectConfig` handles all namespaces automatically
- **Logical Separation**: Different event handlers, different authorization per namespace
- **Scalability**: Can move namespaces to different backend servers later without client changes
- **Lower Latency**: No additional connection handshakes for each feature

**Real-World Use Case:**
```
/              → Server health checks, global announcements
/playersync    → Player position synchronization (this sample)
/chat          → Text chat messages
/voice         → Voice chat metadata
/admin         → Administrative commands (requires authentication)
```

All over **one WebSocket connection**, with namespace-level access control on the server.

### Event Flow

1. **Connection**: Client connects to root → gets `/playersync` namespace → namespace connects
2. **Player ID**: Server sends `player_id` → client can now move and send position
3. **Existing Players**: Server sends `existing_players` dictionary → client spawns remote players
4. **Position Updates**: Client sends `player_move` at configured interval (default 50ms/20Hz) → other clients receive and update (not logged to avoid spam)
5. **Join/Leave**: Server broadcasts `player_join`/`player_leave` → clients spawn/remove players
6. **Graceful Shutdown**: `OnDestroy()` called (Unity stops, scene change, etc.) → sets `isDestroyed` flag → stops coroutines → explicitly disconnects socket → server broadcasts `player_leave` → other clients remove the player
7. **Unexpected Disconnection**: Connection lost → root socket disconnect detected → stops position updates → cleans up remote players → enters reconnecting state
8. **Reconnection**: Automatic retry with configurable exponential backoff (default: 1s → 2s → 4s → 8s → 16s → 30s max, customizable via ReconnectConfig) → creates fresh socket → reconnects → receives new player_id → syncs with server

### Component Responsibilities

- **PlayerNetworkSync**: Socket.IO connection, event handlers, reconnection state management, broadcasts LocalPlayer position at configured interval (default 50ms/20Hz), configures ReconnectConfig for automatic reconnection, **handles proper cleanup in `OnDestroy()` with event handler protection**
- **PlayerController**: Local player movement input (WASD) - attached to LocalPlayer GameObject
- **PlayerSpawner**: Spawns/updates/removes remote player GameObjects (red capsules), cleanup on disconnect
- **RemotePlayerMovement**: Smooth interpolation for remote players - attached to RemotePlayer prefab
- **RTTDisplay**: Displays network latency in top-right corner (on UIManager under Canvas)
- **ConnectionStatusDisplay**: Shows real-time connection status (Connected/Disconnected/Reconnecting) with color coding (on UIManager under Canvas)
- **ReconnectConfig**: Configurable reconnection strategy (initialDelay, multiplier, maxDelay, maxAttempts, jitterPercent) - exposed in Inspector

### Data Structures

```csharp
[Serializable]
public class MovePacket
{
    public string id;           // Player ID
    public PositionData position;
}

[Serializable]
public class PositionData
{
    public float x, y, z;

    public PositionData(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    public Vector3 ToVector3() => new Vector3(x, y, z);
}
```

### Cleanup and Lifecycle Management

The sample implements production-grade cleanup to prevent common multiplayer issues:

**Problem: Ghost Players**

When Unity Editor stops or a client crashes, the socket connection might not immediately notify the server, leaving "ghost players" in other clients.

**Solution: Explicit Cleanup in `OnDestroy()`**

```csharp
private void OnDestroy()
{
    // 1. Set destroyed flag FIRST (prevents event handler execution)
    isDestroyed = true;

    // 2. Stop all coroutines
    if (positionRoutine != null) StopCoroutine(positionRoutine);
    if (reconnectRoutine != null) StopCoroutine(reconnectRoutine);

    // 3. Disconnect socket explicitly
    if (rootSocket != null) rootSocket.Disconnect();

    // 4. Clean up remote players
    if (spawner != null) spawner.RemoveAllRemotePlayers();
}
```

**Why `isDestroyed` Flag?**

When `OnDestroy()` is called, Unity destroys the component but socket events may still fire from background threads:

```csharp
// ALL event handlers check this flag first
rootSocket.OnError += (error) =>
{
    if (isDestroyed) return; // ← Exit immediately if destroyed
    // ... rest of handler
};
```

**What Happens:**

1. **Unity Editor stops** → `OnDestroy()` called
2. **Flag set** → `isDestroyed = true` (before disconnecting)
3. **Socket disconnects** → triggers async events (`OnError`, `OnDisconnected`)
4. **Event handlers check flag** → see `isDestroyed = true` → exit immediately
5. **No errors** → handlers don't try to access destroyed component ✅

**Result:**
- ✅ Server receives disconnect event properly
- ✅ Other clients receive `player_leave` event
- ✅ Remote players removed from all clients
- ✅ No "accessing destroyed object" errors
- ✅ Clean console logs showing successful disconnection

## Known Limitations

- **RTT shows 0 ms on localhost**: This is expected behavior. To see realistic RTT values, deploy the server to a remote machine or use network simulation tools
- **Update rate**: Configurable via Inspector (default 50ms/20Hz). Range: 10ms-1000ms
- **No state reconciliation**: Client-authoritative movement with no server validation
- **Interpolation speed**: Fixed at 10 units/second. Could be made configurable per-player based on network conditions

## WebGL Troubleshooting

### Connection Fails in Browser

**Check CORS Configuration:**
```javascript
// Your server.js MUST include:
const io = new Server(httpServer, {
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});
```

**Check Browser Console (F12):**
- CORS errors → Fix server CORS configuration
- "Mixed content" errors → Use `wss://` with `https://`
- "WebGLSocketBridge not found" → Bridge is auto-created, check for JavaScript errors

### Browser Cache Issues

When rebuilding WebGL, browsers may cache old files:

**Solutions:**
- **Force refresh**: `Cmd+Shift+R` (Mac) or `Ctrl+Shift+R` (Windows)
- **Use Incognito/Private mode** for clean testing
- **Clear browser cache** for localhost

### "Must be served over HTTP" Error

WebGL builds cannot run from `file://` protocol:

**Solution:**
```bash
cd /path/to/webgl-build
npx serve -p 8080
```

Then open `http://localhost:8080` in browser.

### Players Not Syncing

**Check:**
1. Server is running (`node server.js`)
2. Both clients show "[OK] Connected" status
3. Browser console shows no errors
4. Network tab shows WebSocket connection (filter by "WS")

**Debug Steps:**
```bash
# Check server logs
node server.js

# Should see:
# ✅ /playersync CONNECTED: [socket_id]
# 📤 /playersync → player_id: [socket_id]
# 📤 /playersync → existing_players: 2 players
```

## Debugging

PlayerNetworkSync includes detailed logging:

- Connection status to root and namespace
- Player ID assignment
- existing_players dictionary contents with self-filtering
- Player join/leave events
- Error messages with full stack traces

Check Unity Console for detailed logs prefixed with emojis (✅, ❌, 📦) for easy identification.

## Testing Reconnection

The sample includes automatic reconnection with visual feedback:

### Visual Indicators

Watch the **Connection Status** (top-left):

- **[OK] Connected** (green) - Active connection, can move and see other players
- **[...] Connecting...** (yellow) - Initial connection in progress
- **[X] Disconnected** (red) - No connection
- **[!] Reconnecting... (attempt N)** (orange) - Automatic retry in progress

### How to Test

1. **Start normally**: Run server and Unity → Status shows "[OK] Connected" in green
2. **Simulate disconnect**: Stop the server (Ctrl+C)
3. **Watch the UI**:
   - Status immediately changes to "[!] Reconnecting... (attempt 1)" in orange (root socket disconnect detected)
   - Your blue player stops moving
   - All red players disappear
   - Attempt counter increments with each retry
4. **Restart server**: Run `node server.js` again
5. **Verify reconnection**:
   - Unity automatically reconnects within a few seconds (exponential backoff)
   - Status changes to "[OK] Connected" in green
   - You can move again
   - Other players respawn if they're still connected
   - No need to restart Unity - reconnection is fully automatic

### Expected Behavior

- **Disconnect detection**: Both root socket and namespace socket monitor connection state
- **Configurable exponential backoff**: Default 1s → 2s → 4s → 8s → 16s → 30s (max), customizable via ReconnectConfig in Inspector
- **Optional jitter**: Can add random variance (jitterPercent) to prevent thundering herd when many clients reconnect simultaneously
- **Max attempts**: Optionally limit reconnection attempts (default: unlimited)
- **Fresh socket creation**: New SocketIOClient instance created on each reconnection attempt
- **Clean state**: Remote players removed on disconnect, position updates stopped
- **Fresh sync**: New player ID and full state sync on reconnect
- **No duplicates**: Only one position update coroutine and one reconnection coroutine running at a time
- **Clean logging**: Position updates (20Hz) are not logged to avoid console spam

## Server Code Example

```javascript
const io = require("socket.io")(3000, {
  cors: { origin: "*" },
});

const playersyncNsp = io.of("/playersync");
const players = {};

playersyncNsp.on("connection", (socket) => {
  console.log(`Player connected: ${socket.id}`);

  players[socket.id] = { x: 0, y: 0, z: 0 };

  // Send player their ID
  socket.emit("player_id", socket.id);

  // Send existing players
  socket.emit("existing_players", players);

  // Notify others
  socket.broadcast.emit("player_join", socket.id);

  socket.on("player_move", (data) => {
    players[data.id] = data.position;
    socket.broadcast.emit("player_move", data);
  });

  socket.on("disconnect", () => {
    delete players[socket.id];
    socket.broadcast.emit("player_leave", socket.id);
    console.log(`Player disconnected: ${socket.id}`);
  });
});

console.log("Server running on http://localhost:3000");
```

## Customizing Reconnection Behavior

The sample uses `ReconnectConfig` (added in v1.1.0) to configure automatic reconnection. You can customize this in the Unity Inspector or via code.

### Inspector Configuration

Select **PlayerSyncManager** → **PlayerNetworkSync** component → **Reconnection Settings**:

**Default Configuration (matches v1.0.x behavior):**
- Initial Delay: `1` second
- Multiplier: `2` (exponential doubling)
- Max Delay: `30` seconds
- Max Attempts: `-1` (unlimited)
- Auto Reconnect: `true`
- Jitter Percent: `0` (disabled)

Results in: **1s → 2s → 4s → 8s → 16s → 30s → 30s → ...**

### Common Configurations

**Local Development (Fast Iteration):**
- Initial Delay: `0.5` second
- Multiplier: `1.5`
- Max Delay: `10` seconds
- Jitter Percent: `0.1` (±10%)

Results in: **0.5s → 0.75s → 1.13s → 1.69s → 2.53s → 3.8s → 5.7s → 8.5s → 10s → ...**

**Production (Conservative):**
- Initial Delay: `2` seconds
- Multiplier: `2.5`
- Max Delay: `60` seconds
- Max Attempts: `10`
- Jitter Percent: `0.15` (±15%)

Results in: **2s → 5s → 12.5s → 31.25s → 60s → 60s → ... (stops after 10 attempts)**

### Programmatic Configuration

```csharp
// Apply custom config
rootSocket.ReconnectConfig = new ReconnectConfig
{
    initialDelay = 1f,
    multiplier = 2f,
    maxDelay = 30f,
    maxAttempts = -1,
    jitterPercent = 0.1f
};

// Or use factory methods
rootSocket.ReconnectConfig = ReconnectConfig.Aggressive();    // Fast
rootSocket.ReconnectConfig = ReconnectConfig.Conservative();  // Slow
rootSocket.ReconnectConfig = ReconnectConfig.Default();       // Standard
```

### Understanding Jitter

**Without jitter (jitterPercent = 0):**
- All clients reconnect at exactly 1.000s, 2.000s, 4.000s...
- When server restarts, ALL clients hit it simultaneously
- Can overwhelm server (thundering herd problem)

**With jitter (jitterPercent = 0.1):**
- Clients reconnect at 0.9s-1.1s, 1.8s-2.2s, 3.6s-4.4s...
- Reconnection load is spread over time
- Server handles requests more smoothly

**Recommended:** Use `0.1` (10%) jitter in production with many concurrent clients.

## Scaling Considerations

**Important:** Understand the performance characteristics before deploying to production.

### Current Implementation Limits

This sample is optimized for **small lobbies (2-20 players)**. Here's why:

**The Math:**
```
Each player emits at 20Hz (20 position updates/second)
Server performs O(N) broadcast for each update
Each update → sent to (N - 1) other players

20 players:
  20 players × 20 updates/sec = 400 updates/sec
  400 updates × 19 recipients = 7,600 messages/sec ✅ Works fine

100 players:
  100 players × 20 updates/sec = 2,000 updates/sec
  2,000 updates × 99 recipients = 198,000 messages/sec ❌ Not viable
```

**Bottlenecks:**
- **CPU**: O(N²) message complexity per frame (every player to every other player)
- **Bandwidth**: Each client receives position updates from all other players
- **Server Memory**: Linear growth with player count (acceptable)

### When You Need to Scale Beyond 20 Players

Consider these architectural patterns:

**1. Interest Management (Area of Interest)**
```
Only send updates for players within visible range:
- Player A can only see players within 50 units
- Reduces O(N) broadcasts to O(k) where k = nearby players
- Typical reduction: 100 players → only 5-10 nearby
```

**2. Room Sharding**
```
Split players into separate rooms/instances:
- Max 20 players per room
- Rooms are completely isolated
- Load balanced across server instances
```

**3. Update Rate Throttling**
```
Reduce update frequency based on distance:
- Nearby players: 20Hz (smooth)
- Medium distance: 10Hz (acceptable)
- Far distance: 5Hz (good enough)
```

**4. Server Infrastructure**

For large-scale deployment, upgrade from the development server:

```javascript
// Current: Single-process development server
const io = require("socket.io")(3000);

// Production: Horizontal scaling with Redis adapter
const io = require("socket.io")(3000);
const redisAdapter = require("@socket.io/redis-adapter");
const { createClient } = require("redis");

const pubClient = createClient({ host: "redis-host", port: 6379 });
const subClient = pubClient.duplicate();

io.adapter(redisAdapter(pubClient, subClient));

// Now you can run multiple Node.js instances
// All instances share the same room/namespace state via Redis
```

**Benefits of Redis Adapter:**
- Multiple Node.js processes handling different players
- Shared state across all server instances
- Can scale horizontally (add more servers)
- Load balancer distributes WebSocket connections

### Performance Monitoring

Add these metrics before deploying:

```csharp
// Track message rate
private int messagesPerSecond;
private float lastMeasureTime;

void Update()
{
    if (Time.time - lastMeasureTime > 1f)
    {
        Debug.Log($"Network: {messagesPerSecond} msgs/sec");
        messagesPerSecond = 0;
        lastMeasureTime = Time.time;
    }
}
```

**Warning Signs:**
- Messages/sec > 10,000: Consider interest management
- Frame drops when players join: CPU bottleneck (reduce update rate)
- Network bandwidth > 1 MB/sec per client: Implement delta compression

### Recommended Approach

**For Your First Launch:**
1. Start with this sample's architecture (2-20 players) ✅
2. Monitor actual player counts and performance
3. Add room sharding when you consistently hit 15+ players
4. Implement interest management if you need 50+ players in same space
5. Deploy Redis adapter when you need multiple server instances

**Don't Prematurely Optimize:**
- 90% of multiplayer games have < 20 concurrent players per instance
- This sample handles that perfectly with minimal complexity
- Only add scaling infrastructure when you have actual data showing you need it

## Production Checklist

- ✅ Proper namespace connection pattern
- ✅ Separation of UI and network logic
- ✅ Dependency injection via SerializeField
- ✅ Clean logging (important events only, no high-frequency spam)
- ✅ Error handling on socket operations
- ✅ Self-filtering in existing_players
- ✅ Position interpolation for smooth remote movement
- ✅ Configurable automatic reconnection (ReconnectConfig with exponential backoff)
- ✅ Jitter support to prevent thundering herd problem
- ✅ Defensive copying on config assignment (prevents external mutation bugs)
- ✅ Max reconnection attempts (optional limit)
- ✅ Root socket disconnect detection and handling
- ✅ Fresh socket creation on reconnection attempts
- ✅ Proper cleanup of coroutines and remote players on disconnect
- ✅ **Proper OnDestroy cleanup** - explicit socket disconnection, prevents ghost players
- ✅ **Event handler protection** - `isDestroyed` flag prevents accessing destroyed components
- ✅ **WebGL compatibility** - automatic transport detection, browser-tested
- ✅ **CORS support** - sample server includes proper CORS configuration

## Future Production Enhancements

- Server-authoritative movement validation
- Authentication & session management
- Rate limiting and abuse detection
- Observability (metrics & logging)
- Horizontal scaling with Redis adapter
