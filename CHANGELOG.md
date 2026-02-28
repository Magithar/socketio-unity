# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
  - `BugRegressionTests.cs` in `Tests/Runtime/`
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

[Unreleased]: https://github.com/Magithar/socketio-unity/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/Magithar/socketio-unity/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/Magithar/socketio-unity/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/Magithar/socketio-unity/compare/v0.3.0-alpha...v1.0.0
[0.3.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.2.0-alpha...v0.3.0-alpha
[0.2.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.1.1-alpha...v0.2.0-alpha
[0.1.1-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.1.0-alpha...v0.1.1-alpha
[0.1.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.0.1-prep...v0.1.0-alpha
[0.0.1-prep]: https://github.com/Magithar/socketio-unity/releases/tag/v0.0.1-prep
