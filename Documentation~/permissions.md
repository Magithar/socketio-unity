# Auth Model and Trust Boundaries

> This is a client-side library. There is no server-side permission enforcement here. This document covers the auth handshake the library sends, what the server is expected to enforce, and where the trust boundary sits.

---

## Auth Model

socketio-unity has **no role-based access control**. All permission enforcement is server-side. The library provides a mechanism to pass an auth payload during namespace connection; what the server does with it is outside the library's scope.

### How auth works

```csharp
// Game code passes auth at namespace acquisition
var ns = socket.Of("/admin", new { token = "player_session_token" });
```

Internally, `NamespaceSocket.SendConnect()` serializes the auth object:

```
Socket.IO packet sent:  "0/admin,{"token":"player_session_token"}"
Engine.IO framing:       "40/admin,{"token":"player_session_token"}"
```

The server receives this and is responsible for validating it before sending a CONNECT acknowledgement. If the server rejects the auth, it sends a CONNECT_ERROR packet; the library surfaces this as a `connect_error` event on the namespace socket.

### Auth constraints enforced by the library

| Constraint | Enforcement point |
|------------|------------------|
| Auth cannot be changed for an existing namespace | `NamespaceManager.Get()` — new auth on existing ns is silently ignored with a `LogWarning` |
| Auth is re-sent automatically on reconnect | `NamespaceSocket.SendConnect()` is called each reconnect cycle with the original auth |
| Auth is serialized with `Newtonsoft.Json.JsonConvert.SerializeObject` | Any serializable object is accepted |

### What the library does NOT enforce

- Token expiry
- Role or scope validation
- Rate limiting
- Server identity verification (no certificate pinning)
- Payload size limits on auth objects

---

## Trust Boundaries

```
┌─────────────────────────────────────────────────────────┐
│  Client (Unity game, user-controlled device)            │
│                                                         │
│  SocketIOClient → EngineIOClient → ITransport           │
│                                                         │
│  Auth payload assembled here and sent over the wire     │
└───────────────────────────────────┬─────────────────────┘
                                    │  WebSocket (ws:// or wss://)
                                    │  ← TRUST BOUNDARY →
┌───────────────────────────────────▼─────────────────────┐
│  Server (developer-controlled)                          │
│                                                         │
│  All authorization decisions live here.                 │
│  Socket.IO server validates auth, enforces namespaces,  │
│  manages rooms, and controls who can emit what.         │
└─────────────────────────────────────────────────────────┘
```

**Key implication:** The client is untrusted. Server code must validate every event's authority independently — a connected socket on `/admin` does not mean the client is an admin unless the server verified the auth token on CONNECT.

---

## Production Security Checklist

These are responsibilities of the game developer, not the library:

- [ ] Use `wss://` (TLS) for any production connection where auth tokens are sent
- [ ] Auth tokens should be short-lived and server-issued (not hardcoded)
- [ ] Server must verify auth on the CONNECT event for restricted namespaces
- [ ] Server must validate event payloads and sender identity independently of namespace auth
- [ ] Test servers in `TestServer~/` have `cors: origin: '*'` and no auth — replace for production

---

## Namespace Isolation

Each namespace is a separate logical channel sharing one WebSocket. The library enforces:
- Separate `EventRegistry` per namespace (events on `/` don't fire handlers on `/admin`)
- Separate `AckRegistry` per namespace
- Separate `_connected` flag per namespace

The server controls which namespaces a client can join. A server-initiated DISCONNECT on one namespace does not affect others.
