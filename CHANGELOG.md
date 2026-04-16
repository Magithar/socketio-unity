# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.4.0] - 2026-04-16

### Added

- **Mirror Integration sample** (`package/Samples~/MirrorIntegration/`) — hybrid architecture combining Socket.IO backend with Mirror in-scene networking
  - Socket.IO owns matchmaking, session identity, and server-authoritative events; Mirror owns transform/physics sync between peers
  - `MirrorGameOrchestrator` — enforces startup/teardown order for the lobby→match transition; dual guard against duplicate `match_started` events
  - `GameIdentityRegistry` — bridges Mirror `netId` to Socket.IO `playerId` for routing backend events to the correct spawned object
  - `PlayerIdentityBridge` — registers each player's identity on spawn via Mirror `[Command]` and syncs the lobby display name to all clients
  - `GameEventBridge` — subscribes to `/game` namespace events (`score_update`, `player_killed`) only after `match_started`; never active during lobby phase
  - `MirrorPlayerController` — local player input only; red = local player, blue = remote peers
  - Graceful shutdown: emits `leave_room` before stopping Mirror to skip the 10-second reconnect grace window
- **`mirror-server.js`** test server (port 3002) — extends lobby server with `/game` namespace and HTTP endpoints to fire game events from a browser while Unity is running (`/test/score`, `/test/kill`, `/test/round-end`)
- **`hostAddress` in `match_started`** — server-assigned LAN IP forwarded through the full event chain (`LobbyNetworkManager` → `LobbyStateStore` → `LobbyUIController` → `GameOrchestrator`) so clients can pass it directly to Mirror for P2P host mode; JS `"undefined"`/`"null"` string values normalized to C# null so callers can safely null-check
- **Lobby sample assembly definition** (`SocketIOUnity.Samples.Lobby.asmdef`) — explicit asmdef allowing MirrorIntegration to reference Lobby types without circular dependencies
- **`ConnectionState` runtime tests** — runtime NUnit tests covering connection state transitions, complementing the existing EditMode suite
- **GitHub Pages deployment workflow** — automated live demo deployment on push to `main`

### Changed

- **Repo restructured** — all installable package content (Runtime, Editor, Samples, Tests, `package.json`) moved under `package/` subdirectory; `TestProject~/Packages/manifest.json` updated to reference the new path. Install URL is now `https://github.com/Magithar/socketio-unity.git?path=/package`.
- **`SocketIOManager` and `SocketIODiagnosticsOverlay` consolidated into BasicChat sample** — previously split across `Samples~/` root and a standalone `Samples~/Diagnostics/` folder; both now live in `Samples~/BasicChat/`. Import via Package Manager → Samples → "Basic Chat".
- **Test assemblies** — removed `UNITY_INCLUDE_TESTS` platform constraint from all test asmdefs; tests now compile and run on all platforms, not just in the Editor

## [1.3.1] - 2026-04-04

### Added

- **Combined LiveDemo server** (`livedemo-server.js`) — single-process server merging `/lobby` and `/playersync` namespaces for simpler deployment (Render, Railway, Fly)
- **Runtime `link.xml`** for IL2CPP stripping — preserves `UnityMainThreadDispatcher` and `UnityTickDriver` so IL2CPP builds (WebGL, iOS) don't strip critical runtime types
- **Lobby sample `link.xml`** — preserves `RoomState` and `LobbyPlayer` data models from IL2CPP stripping to fix JSON deserialization in WebGL builds

### Fixed

- **WebGL lobby ACK array wrapping** — server ACK responses wrapped in an array by Socket.IO are now unwrapped correctly on the client, fixing room creation and join failures in WebGL builds
- **IL2CPP stripping breaking lobby** — `RoomState` and `LobbyPlayer` classes were stripped by IL2CPP in WebGL builds, causing silent JSON deserialization failures

### Changed

- **WebGL build uses compressed assets** — switched from uncompressed `.data`/`.wasm`/`.framework.js` to `.unityweb` compressed format, reducing download size significantly
- **LiveDemo points at production server** — WebGL demo now connects to the deployed server instead of `localhost`

## [1.3.0] - 2026-04-03

### Added

- **Typed `SocketError`** — `OnError` now delivers `SocketError { ErrorType, Message }` instead of a raw string
  - `ErrorType` enum: `Transport`, `Auth`, `Timeout`, `Protocol`
  - Lets callers branch on error category without string parsing
- **`ConnectionState` enum** — `Disconnected` / `Connecting` / `Connected` / `Reconnecting`
  - `SocketIOClient.State` property for synchronous reads
  - `SocketIOClient.OnStateChanged` event fires on every transition
  - Used by samples to replace shadow-bool pattern for UI state
- **`SocketIODiagnosticsOverlay`** sample — runtime in-game panel (`package/Samples~/Diagnostics/`)
  - Toggle via `SocketIOManager.Instance.ShowDiagnostics = true`
  - Shows state (color-coded), RTT, active namespace count, pending ACK count, live event log
  - Optional throughput display (`SOCKETIO_PROFILER_COUNTERS` define required)
- **Namespace preservation across reconnects** — `On()` handlers and namespace registrations survive reconnect cycles; no re-registration needed
- **`LobbyStateIntegrationTests`** — runtime integration tests for socket state invariants and namespace connection timing
- **`StressTests` (EditMode)** — high packet rate (1 000 events), large binary bursts (1 MB / 10 MB), ACK storms (100 pending), reconnect floods (50 rapid cycles), memory footprint validation (1 000 subscribe/unsubscribe)
- **`InternalsVisibleTo` for stress assembly** — `SocketIOUnity.Tests.Stress` can access internals for deeper validation
- **LiveDemo sample** (`package/Samples~/LiveDemo/`) — End-to-end multiplayer demo combining Lobby and PlayerSync into a single scene with `GameOrchestrator` layer toggling, dual-server architecture, and seamless lobby → match transitions
- **Dedicated per-sample servers** — Each sample now runs on its own port: `basicchat-server.js` (:3002), `playersync-server.js` (:3003), `lobby-server.js` (:3001), `server.js` (:3000 for binary/auth tests)
- **WebGL clipboard plugin** — Native clipboard support for WebGL builds via `WebGLClipboard.jslib`
- **`player_identity` event handling** — Lobby sample now handles server-assigned player identity and fixes `IsHost` race condition

### Changed

- **`SocketIOClient.OnError`** — event type changed from `Action<string>` to `Action<SocketError>` (breaking change for any code using the old string form; see migration below)
- WebSocket lifecycle hardened — reconnect controller prevents race conditions, safety check ensures URL is set before attempting connect, WebSocket events rebound when a new socket instance is created

### Migration from v1.2.x `OnError`

```csharp
// Before (v1.2.x)
socket.OnError += (string msg) => Debug.LogError(msg);

// After
socket.OnError += (SocketError err) => Debug.LogError($"[{err.Type}] {err.Message}");
```

## [1.2.0] - 2026-03-18

**Minor release** — New Lobby sample. No API changes.

### Added

- **Lobby Sample** (`package/Samples~/Lobby/`): Production-style multiplayer lobby demonstrating reconnect recovery, host migration, and session identity
  - Room creation and join-by-code (6-character codes, e.g. `C9N7GR`)
  - Persistent `playerId` + `sessionToken` stored in `PlayerPrefs` — survives crashes and app restarts
  - Session token validation on reconnect prevents player slot spoofing
  - 10-second reconnect grace window — room slot held while player is offline; host migration fires automatically
  - Three-layer architecture: `LobbyNetworkManager` (transport) → `LobbyStateStore` (state) → `LobbyUIController` (view)
  - Room version tracking and player list diffing — no full list rebuilds
  - Full WebGL support via `TransportFactoryHelper.CreateDefault()`
  - Separate `lobby-server.js` on port 3001
- **`package.json` `samples` array**: All three samples (Basic Chat, Player Sync, Lobby) now appear in the Unity Package Manager Samples tab

### Stability

- **No API Changes**: New sample only
- **Backward Compatible**: Safe upgrade from v1.1.2

## [1.1.2] - 2026-03-05

**Patch release** — Reconnection stability fixes. No API changes.

### Fixed

- **`SocketIOClient.CreateFreshEngine()`**: Fully recreates engine, namespaces, and binary assembler on each reconnect — prevents stale state from a previous connection leaking into the new one
- **`SocketIOClient.Tick()`**: Caches the namespace list before iterating to avoid a `InvalidOperationException` (collection modified during enumeration) that could occur mid-reconnect
- **`SocketIOClient.HandleEngineClose()`**: Added race-condition guard (`if (IsConnected) return`) and calls `_namespaces.ResetAll()` before invoking disconnect handlers — prevents double-teardown and ensures namespace state is clean before user code runs
- **`SocketIOClient.HandleEngineMessage()`**: Re-registers all non-root namespaces after reconnect by sending `CONNECT` packets — namespaces were silently dropped after a reconnect cycle
- **`SocketIOClient.DestroyEngine()` / `AttemptReconnect()`**: Separated teardown and reconnect attempt into distinct methods for clarity and correctness
- **`PlayerNetworkSync` sample**: Re-attaches socket event handlers on reconnect to match the core reconnection fixes

### Stability

- **No API Changes**: All fixes are internal implementation changes
- **Backward Compatible**: Safe upgrade from v1.1.1

## [1.1.1] - 2026-02-28

**Patch release** — PlayerSync sample bug fixes. No API changes.

### Fixed

- **PlayerSync / RemotePlayer prefab**: Canvas render mode was `Screen Space - Overlay` instead of `World Space`
  - Label rendered on top of the entire screen UI rather than in 3D space above the player
- **PlayerSync / RemotePlayer prefab**: Canvas `Transform.localScale` was `(0, 0, 0)` — label was invisible at runtime
- **PlayerSync / RemotePlayer prefab**: Canvas `RectTransform.sizeDelta` was `(0, 0)` — no surface area to render text onto

### Added

- **`BillboardCanvas`** script (`package/Samples~/PlayerSync/Scripts/BillboardCanvas.cs`)
  - Attaches to the RemotePlayer Canvas child; copies camera rotation each `LateUpdate` so the label always faces the viewer regardless of camera angle or player direction

### Stability

- **No API Changes**: All fixes are confined to the PlayerSync sample assets
- **Backward Compatible**: Safe upgrade from v1.1.0

## [1.1.0] - 2026-02-28

### Added

- **ReconnectConfig**: Configurable reconnection strategy replacing hardcoded exponential backoff
  - `initialDelay`, `multiplier`, `maxDelay`, `maxAttempts`, `autoReconnect`, `jitterPercent` fields
  - `ReconnectConfig.Default()` — matches v1.0.x behavior (1s initial, 2x multiplier, 30s cap)
  - `ReconnectConfig.Aggressive()` — faster reconnection for development
  - `ReconnectConfig.Conservative()` — slower reconnection for production
  - Copy constructor `new ReconnectConfig(other)` for defensive copying
  - Jitter support to prevent thundering herd problem on mass reconnect
- **SocketIOClient.ReconnectConfig** property (get/set) for runtime reconnect configuration
- **ReconnectConfigTests**: Unit tests for defensive copy, factory methods, and v1.0.x compatibility
- **PlayerSync sample**: Real-time multiplayer position synchronization demo
  - Namespace pattern (`/playersync`), ReconnectConfig integration, WebGL support
  - Production-grade cleanup (`OnDestroy`, `isDestroyed` guard, explicit disconnect)
  - RTT display, connection status UI, network interpolation
- **GitHub Actions CI pipeline** using [`game-ci/unity-test-runner`](https://github.com/game-ci/unity-test-runner) — runs automated EditMode tests on every push and PR to `main`
  - Unity `2022.3.62f2` (LTS) on `ubuntu-latest`
  - `TestProject~/` standalone Unity project references the package as a local dependency
  - Test results uploaded as artifacts on every run (`if: always()`)
  - Git LFS enabled (`lfs: true`) for binary assets
  - `Library/` folder cached via `actions/cache` keyed on `package.json` + `TestProject~/Packages/manifest.json`

### Fixed

- `DontDestroyOnLoad` now skipped in EditMode/CI where `Application.isPlaying` is false

### Changed

- Updated README with v1.1.0 preview and PlayerSync sample reference
- Added `ReconnectConfig` to API stability contract

## [1.0.1] - 2026-02-05

**Patch release** — Critical bug fixes with no API changes.

### Added

- **Regression Tests**: Comprehensive test suite for all 4 bug fixes
  - `BugRegressionTests.cs` in `package/Tests/Runtime/`
  - Tests malformed JSON handling, ACK ID overflow, and wraparound behavior

### Fixed

- **BinaryPacketAssembler**: Added try-catch around `JArray.Parse()` to handle malformed JSON payloads gracefully
  - Previously could throw unhandled exception on invalid binary event JSON
  - Now logs error and uses empty array as fallback
  - Affects: `BinaryPacketAssembler.Start()` (internal method)
- **WebSocketTransport**: Removed event nullification in `Close()` method
  - Previously nullified transport events (`OnOpen`, `OnClose`, etc.), breaking reconnection
  - Events remain intact during close, allowing proper reconnection lifecycle
  - Affects: `WebSocketTransport.Close()` (internal transport layer)
- **WebSocket.cs**: Fixed static dictionary memory leak in WebGL builds
  - Added `RuntimeInitializeOnLoadMethod` to clear static `instances` dictionary on domain reload
  - Prevents orphaned WebSocket instances across Unity play mode sessions
  - Affects: `WebSocketFactory.instances` (internal WebGL bridge)
- **AckRegistry**: Fixed ACK ID integer overflow after 2 billion emits
  - ACK IDs now wrap to 1 when overflowing (skips 0 and negative numbers)
  - Prevents negative ACK IDs that could cause lookup failures
  - Affects: `AckRegistry.Register()` (internal ACK tracking)

### Changed

- No API changes — all fixes are internal implementation improvements
- Backward compatible — safe upgrade from v1.0.0
- Public API unchanged: `Connect()`, `Disconnect()`, `Emit()`, `On()`, `Off()`, `Of()` remain frozen

## [1.0.0] - 2026-01-29

**First stable release** — Production-ready Socket.IO v4 client for Unity.

### Added

- **AssemblyInfo.cs**: Assembly metadata with `InternalsVisibleTo` for test access
- **Basic Chat Sample**: Production-ready "Hello World" onboarding experience
  - Demonstrates connection lifecycle, event handling, reconnection, proper cleanup
  - Works on Editor, Standalone, and WebGL
- **API Stability Contract**: `API_STABILITY.md` documenting stability guarantees
- **Protocol Edge-Case Test Suite**: 38 comprehensive tests covering:
  - Empty/null packet handling
  - Invalid Socket.IO type rejection (out-of-range, non-numeric)
  - ACK ID overflow protection (Int64 overflow → null)
  - Binary packet separator validation
  - Namespace parsing correctness
  - Malformed JSON resilience (deferred validation)
  - Disconnect packet parsing (with/without trailing comma)

### Changed

- Moved **Toggle Network HUD** menu from `Tools → SocketIO` to top-level `SocketIO` menu
- Public API frozen for v1.x: `Connect`, `Disconnect`, `Emit`, `On`, `Off`, `Of`
- Internal APIs hidden — implementation details not exposed to consumers
- Debug/Telemetry APIs marked unstable: `SocketIOTrace`, profiler APIs may evolve
- Comprehensive README updates:
  - Connection state & error handling (`IsConnected`, `OnError`, `OnDisconnected`)
  - Event unsubscription (`Off()`) with proper cleanup examples
  - `Disconnect()` vs `Shutdown()` comparison
  - Thread safety guarantees (all callbacks on main thread)
  - RTT & throughput monitoring APIs
  - Scene/domain reload safety guidance
  - Minimum Unity version requirements
  - Contributing guidelines
  - Common error scenarios table

### Fixed

- **Protocol Hardening**:
  - Empty packets now return null instead of throwing
  - Invalid type characters (e.g., "4X") safely rejected
  - Out-of-range types (7+) safely rejected
  - Huge ACK IDs that overflow Int64 return null
  - Binary packets without `-` separator handled gracefully
- **Namespace Disconnect Correctness**:
  - Disconnect packets with namespace (`41/admin,`) parsed correctly
  - Disconnect packets without comma (`41/chat`) parsed correctly
  - Root disconnect (`41`) defaults to `/` namespace
- **Scene/Domain Reload Safety**:
  - No orphaned WebSocket connections between play sessions
  - Static state properly reset on domain reload
  - No duplicate reconnect loops after reload

## [0.3.0-alpha] - 2026-01-22

### Added

- **Debugging & Tracing System**: Comprehensive diagnostic tools for development
  - `SocketIOTrace` static API with configurable trace levels (None/Errors/Protocol/Verbose)
  - `ITraceSink` interface for custom log destinations (file, UI overlay, network)
  - `TraceConfig` for runtime trace level control
  - `TraceCategory` enum: EngineIO, SocketIO, Transport, Binary, Reconnect, Namespace, Ack
  - `UnityDebugTraceSink` default implementation for Unity Console output
- **Unity Profiler Integration**: Zero-cost performance monitoring
  - `ProfilerMarkers` for CPU profiling (enable via `SOCKETIO_PROFILER` define)
    - `SocketIO.EngineIO.Parse`, `SocketIO.Event.Dispatch`, `SocketIO.Binary.Assemble`
    - `SocketIO.Ack.Resolve`, `SocketIO.Reconnect.Tick`
  - `SocketIOProfilerCounters` for live metrics (enable via `SOCKETIO_PROFILER_COUNTERS` define)
    - Bytes Sent/Received, Packets/sec, Active Namespaces, Pending ACKs
  - `SocketIOThroughputTracker` for bandwidth monitoring
- **Editor Network HUD**: Real-time Scene View overlay (`SocketIO → Toggle Network HUD`)
  - Displays connection status, RTT, namespace count, pending ACKs, throughput
- **RTT Tracking**: `PingRttTracker` for round-trip latency measurement via Engine.IO PING timing
- **ACK Timeout Support**: `AckRegistry` with configurable timeout and automatic expiration cleanup
- **Sample Test Scripts**: Comprehensive test suite in `Samples/` folder
  - `WebGLTestController` for testing WebGL builds
  - `NamespaceAuthTest`, `BinaryEventTest`, `AdminNamespaceTest`
  - `MainThreadDispatcherTest`, `TraceDemo`
- `/webgl` test namespace in server.js for WebGL-specific testing
- `BinaryPacketBuilderPool` for zero-allocation binary packet construction
- **Event Unsubscription**: `Off()` methods on `SocketIOClient` and `NamespaceSocket` for handler cleanup
- **IDisposable Pattern**: `SocketIOClient` and `EngineIOClient` implement `IDisposable` for proper resource cleanup
- **Shutdown() Method**: Clean disconnect with full state reset

### Fixed

- **WebGL jslib missing symbols**: Added all required NativeWebSocket functions to `SocketIOWebGL.jslib`
- **WebGL namespace connection loops**: Fixed socket disposal and event handler cleanup in connection logic

### Changed

- Added DEBUGGING_GUIDE.md with comprehensive troubleshooting guide
- Documented all trace levels, categories, and custom sink examples
- Documented Unity Profiler integration and available markers/counters
- Updated WebGL status to production-verified

## [0.2.0-alpha] - 2026-01-11

### Added

- **WebGL Support**: Full WebGL transport implementation
  - `WebGLWebSocketTransport` for browser-based WebSocket connections
  - `WebGLSocketBridge` MonoBehaviour for JavaScript ↔ C# interop
  - JavaScript `.jslib` plugin for native browser WebSocket handling
- **Binary Data Support**: Complete Socket.IO v4 binary event handling
  - `BinaryPacketAssembler` for reconstructing multi-packet binary payloads
  - `BinaryPacketBuilder` for emitting binary data to server
  - Support for `byte[]` arguments in events and ACKs
- **Memory Pooling**: Zero-GC optimizations for mobile/WebGL
  - `ObjectPool<T>` generic pooling system
  - `ListPool<T>` for temporary list allocations
- **Main Thread Dispatcher**: `UnityMainThreadDispatcher` for thread-safe Unity API calls
- **Transport Factory Pattern**: `TransportFactory` for clean transport instantiation and reconnect safety
- **Engine.IO Heartbeat**: `HeartbeatController` for connection health monitoring
- Comprehensive README.md with installation instructions, usage examples, and architecture overview
- CHANGELOG.md for tracking version history
- Development & Testing section in README with Node.js test server code

### Changed

- Refactored transport layer to use factory pattern for WebGL compatibility
- `ReconnectController` lifetime now persists across reconnects for proper exponential backoff
- Improved namespace authentication with proper CONNECT packet formatting
- Enhanced reconnect logic with clean state reset on each attempt
- Added detailed usage examples for basic connections, namespaces, and ACKs
- Added architecture diagram showing component hierarchy
- Added directory structure documentation
- Documented reconnection behavior and strategy
- Added WebGL status and implementation details
- Added NativeWebSocket third-party attribution in WebSocket.cs

### Fixed

- Transport state leakage during reconnects
- Constructor mismatches in transport layer
- Event dispatch on non-main thread causing Unity API errors
- Binary event handlers now correctly receive `byte[]` instead of `string`

## [0.1.1-alpha] - 2026-01-05

### Added

- Engine.IO v4 handshake and heartbeat
- Socket.IO v4 packet framing
- Event-based API (On/Emit)
- Namespace support and multiplexing
- Acknowledgement callbacks
- Automatic reconnection with exponential backoff
- Standalone platform support

### Fixed

- Fixed WebSocketTransport implementation
- Added robust Socket.IO packet parser with namespace and ACK support
- Implemented spec-correct heartbeat and Unity tick integration

## [0.1.0-alpha] - 2026-01-05 [DEPRECATED]

> ⚠️ **This release is deprecated due to critical bugs. Use v0.1.1-alpha instead.**

### Added

- Initial alpha release
- Basic Engine.IO v4 implementation
- Basic Socket.IO v4 implementation

---

## [0.0.1-prep] - 2024-12-27

### Added

- Initial repository setup
- README with project scope and goals
- MIT License
- Clean-room legal declaration (LEGAL.md)
- Contribution guidelines (CONTRIBUTING.md)

---

## Version Guidelines

### Pre-1.0.0 (Alpha/Beta)

- **0.x.y-alpha**: Early development, expect breaking changes
- **0.x.y-beta**: Feature-complete for milestone, stabilizing
- API may change without notice before 1.0.0

### Post-1.0.0 (Stable)

- **Major (x.0.0)**: Breaking API changes
- **Minor (0.x.0)**: New features, backward-compatible
- **Patch (0.0.x)**: Bug fixes, backward-compatible

---

[Unreleased]: https://github.com/Magithar/socketio-unity/compare/v1.4.0...HEAD
[1.4.0]: https://github.com/Magithar/socketio-unity/compare/v1.3.1...v1.4.0
[1.3.1]: https://github.com/Magithar/socketio-unity/compare/v1.3.0...v1.3.1
[1.3.0]: https://github.com/Magithar/socketio-unity/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/Magithar/socketio-unity/compare/v1.1.2...v1.2.0
[1.1.2]: https://github.com/Magithar/socketio-unity/compare/v1.1.1...v1.1.2
[1.1.1]: https://github.com/Magithar/socketio-unity/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/Magithar/socketio-unity/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Magithar/socketio-unity/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Magithar/socketio-unity/compare/v0.3.0-alpha...v1.0.0
[0.3.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.2.0-alpha...v0.3.0-alpha
[0.2.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.1.1-alpha...v0.2.0-alpha
[0.1.1-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.1.0-alpha...v0.1.1-alpha
[0.1.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.0.1-prep...v0.1.0-alpha
[0.0.1-prep]: https://github.com/Magithar/socketio-unity/releases/tag/v0.0.1-prep
