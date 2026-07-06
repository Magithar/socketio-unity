# Migration Guide

Breaking changes across the v1.x line, in one place. For the full change history see [CHANGELOG.md](CHANGELOG.md). The stable API surface is defined in [API_STABILITY.md](API_STABILITY.md); anything listed there as ✅ Stable will not break within v1.x.

There is exactly **one** breaking change to the core client in the entire v1.x line (the `OnError` signature, v1.3.0). The other entry below affects a **sample** you may have copied, not the shipped library.

---

## v1.2.x → v1.3.0 — `OnError` is now typed (core)

`SocketIOClient.OnError` changed from `Action<string>` to `Action<SocketError>`, so callers can branch on error category instead of parsing strings.

```csharp
// Before (v1.2.x)
socket.OnError += (string msg) => Debug.LogError(msg);

// After (v1.3.0+)
socket.OnError += (SocketError err) => Debug.LogError($"[{err.Type}] {err.Message}");
```

`SocketError` exposes `Type` (`ErrorType.Transport | Auth | Timeout | Protocol`) and `Message`. This is the only breaking change to the core client in v1.x.

---

## v1.4.0 → v1.5.0 — `LobbyStateStore.OnMatchStarted` gained ports (sample only)

**Affects only the Lobby / MirrorIntegration samples.** The core Socket.IO client is unaffected. If you copied `LobbyUIController`, `GameOrchestrator`, or `MirrorGameOrchestrator` from v1.4.0 and subscribed to `OnMatchStarted`, add the two dedicated-server port parameters:

```csharp
// Before (v1.4.0)
store.OnMatchStarted += (string hostAddress, string mode) => { ... };

// After (v1.5.0)
store.OnMatchStarted += (string hostAddress, string mode, int kcpPort, int wsPort) => { ... };
```

`kcpPort` / `wsPort` are `0` when the server does not supply dedicated-server ports (P2P mode), so existing P2P flows can keep ignoring them.

---

## v1.5.0 → v1.6.0 — no breaking changes

v1.6.0 is a reliability release. All additions are backward-compatible:

- **`ReconnectConfig.connectTimeoutMs`** (default `10000`) is a new additive field. Existing configs get the default and behave unchanged for fast-connecting servers; set it to `0` to disable the connect-establishment timeout entirely.
- Binary-placeholder and WebGL text-routing hardening are internal fixes with no API change.

No migration required.
