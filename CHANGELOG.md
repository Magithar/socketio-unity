# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Fixed

### Documentation

## [1.0.0] — 2026-01-29

**First stable release** — Production-ready Socket.IO v4 client for Unity.

### Added
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

### Fixed
- **Protocol Hardening**:
  - Empty packets now return null instead of throwing
  - Invalid type characters (e.g., "4X") safely rejected
  - Out-of-range types (47+) safely rejected
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

### Stability
- **Public API frozen for v1.x**: `Connect`, `Disconnect`, `Emit`, `On`, `Off`, `Of`
- **Internal APIs hidden**: Implementation details not exposed to consumers
- **Debug/Telemetry APIs marked unstable**: `SocketIOTrace`, profiler APIs may evolve

### Documentation
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

## [0.3.0-alpha] - 2026-01-22

### Added
- **Debugging & Tracing System**: Comprehensive diagnostic tools for development
  - `SocketIOTrace` static API with configurable trace levels (Off/Errors/Protocol/Verbose)
  - `ITraceSink` interface for custom log destinations (file, UI overlay, network)
  - `TraceConfig` for runtime trace level control
  - `TraceCategory` enum: EngineIO, SocketIO, Namespace, Transport, Binary, Reconnect
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
  - `MainThreadDispatcherTest`, `TraceDemo`, `ProfilerCounterTest`
- `/webgl` test namespace in server.js for WebGL-specific testing
- `BinaryPacketBuilderPool` for zero-allocation binary packet construction
- **Event Unsubscription**: `Off()` methods on `SocketIOClient` and `NamespaceSocket` for handler cleanup
- **IDisposable Pattern**: `SocketIOClient` and `EngineIOClient` implement `IDisposable` for proper resource cleanup
- **Shutdown() Method**: Clean disconnect with full state reset

### Fixed
- **WebGL jslib missing symbols**: Added all required NativeWebSocket functions to `SocketIOWebGL.jslib`
- **WebGL namespace connection loops**: Fixed socket disposal and event handler cleanup in connection logic

### Documentation
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
  - `BinaryAssembler` for reconstructing multi-packet binary payloads
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

### Fixed
- Transport state leakage during reconnects
- Constructor mismatches in transport layer
- Event dispatch on non-main thread causing Unity API errors
- Binary event handlers now correctly receive `byte[]` instead of `string`

### Documentation
- Added detailed usage examples for basic connections, namespaces, and ACKs
- Added architecture diagram showing component hierarchy
- Added directory structure documentation
- Documented reconnection behavior and strategy
- Added WebGL status and implementation details
- Added NativeWebSocket third-party attribution in WebSocket.cs

## [0.1.1-alpha] - 2026-01-05

### Fixed
- Fixed WebSocketTransport implementation
- Added robust Socket.IO packet parser with namespace and ACK support
- Implemented spec-correct heartbeat and Unity tick integration

### Added
- Engine.IO v4 handshake and heartbeat
- Socket.IO v4 packet framing
- Event-based API (On/Emit)
- Namespace support and multiplexing
- Acknowledgement callbacks
- Automatic reconnection with exponential backoff
- Standalone platform support

## [0.1.0-alpha] - 2026-01-05 [DEPRECATED]

> ⚠️ **This release is deprecated due to critical bugs. Use v0.1.1-alpha instead.**

### Added
- Initial alpha release
- Basic Engine.IO v4 implementation
- Basic Socket.IO v4 implementation

### Known Issues
- WebSocketTransport had critical bugs
- Packet parser issues with namespaces and ACKs
- Heartbeat timing issues

---

## [0.0.1-prep] - 2024-12-27

### Added
- Initial repository setup
- README with project scope and goals
- MIT License
- Clean-room legal declaration (LEGAL.md)
- Contribution guidelines (CONTRIBUTING.md)

### Notes
- No protocol or runtime code included
- Establishes legal and architectural foundation

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

[Unreleased]: https://github.com/Magithar/socketio-unity/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/Magithar/socketio-unity/compare/v0.3.0-alpha...v1.0.0
[0.3.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.2.0-alpha...v0.3.0-alpha
[0.2.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.1.1-alpha...v0.2.0-alpha
[0.1.1-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.1.0-alpha...v0.1.1-alpha
[0.1.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.0.1-prep...v0.1.0-alpha
[0.0.1-prep]: https://github.com/Magithar/socketio-unity/releases/tag/v0.0.1-prep
