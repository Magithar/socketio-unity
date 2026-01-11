# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed

### Fixed

### Documentation

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

[Unreleased]: https://github.com/Magithar/socketio-unity/compare/v0.2.0-alpha...HEAD
[0.2.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.1.1-alpha...v0.2.0-alpha
[0.1.1-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.1.0-alpha...v0.1.1-alpha
[0.1.0-alpha]: https://github.com/Magithar/socketio-unity/compare/v0.0.1-prep...v0.1.0-alpha
[0.0.1-prep]: https://github.com/Magithar/socketio-unity/releases/tag/v0.0.1-prep
