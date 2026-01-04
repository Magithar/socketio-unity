# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Comprehensive README.md with installation instructions, usage examples, and architecture overview
- CHANGELOG.md for tracking version history
- Development & Testing section in README with Node.js test server code
- Clear project goals, non-goals, and platform support matrix
- Installation instructions (Unity Package Manager + Manual)

### Changed
- Improved README structure with better formatting and organization
- Enhanced code examples with consistent formatting

### Documentation
- Added detailed usage examples for basic connections, namespaces, and ACKs
- Added architecture diagram showing component hierarchy
- Added directory structure documentation
- Documented reconnection behavior and strategy
- Added WebGL status and roadmap information

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

[Unreleased]: https://github.com/Magithar/socketio-unity/compare/v0.1.1-alpha...HEAD
[0.1.1-alpha]: https://github.com/Magithar/socketio-unity/releases/tag/v0.1.1-alpha
[0.1.0-alpha]: https://github.com/Magithar/socketio-unity/releases/tag/v0.1.0-alpha
[0.0.1-prep]: https://github.com/Magithar/socketio-unity/releases/tag/v0.0.1-prep
