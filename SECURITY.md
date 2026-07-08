# Security Policy

## Scope

`socketio-unity` is a **client-side** Unity package. It contains no server code in the shipped artifact. The trust boundary sits between server-supplied data and the client's internal state — the library's job is to parse untrusted server input defensively and never enforce server-side authorization (that is the server's responsibility). See [Documentation~/permissions.md](Documentation~/permissions.md) for the full auth/trust model.

## Supported Versions

Security fixes are applied to the latest `1.x` release. Older minor versions are not back-patched.

| Version | Supported |
|---------|-----------|
| 1.7.x   | ✅        |
| < 1.7   | ❌ (upgrade) |

## Reporting a Vulnerability

Please report suspected vulnerabilities **privately** — do not open a public issue for an unfixed security problem.

- Use GitHub's [private vulnerability reporting](https://github.com/Magithar/socketio-unity/security/advisories/new) (Security → Report a vulnerability), or
- Email the maintainer (see the GitHub profile for [@Magithar](https://github.com/Magithar)).

Include the Unity version, target platform (Editor / Standalone / WebGL / mobile), a minimal reproduction, and the impact you observed. Expect an acknowledgement within a reasonable window; fixes ship in a patch or minor release with credit in the CHANGELOG unless you prefer to remain anonymous.

## Known Hardening Notes

- **Transport encryption is the developer's responsibility.** Auth payloads passed to `Of("/ns", authObject)` are sent in plaintext in the Socket.IO CONNECT packet. Always use `wss://` in production when auth carries session tokens or identity data. See [Documentation~/GETTING_STARTED.md](Documentation~/GETTING_STARTED.md#security-notes).
- **Server-input hardening.** A full static security audit was performed on 2026-06-27. The two medium findings it surfaced — a binary-placeholder null-dereference and fragile WebGL text-message routing — were fixed in **v1.6.0**. The audit's positive findings (consistent null-returning parser, main-thread dispatcher isolation, ACK overflow handling, no `TypeNameHandling` in deserialization) remain in place.
- **Bounded main-thread dispatch queue.** As of **v1.7.0**, `UnityMainThreadDispatcher` caps its pending-action queue at `MaxQueueLength` (default 10000, drop-newest overflow, counted in `DroppedActionCount`) — closes the previously undefendable gap below. Set `MaxQueueLength` to 0 or negative to restore the pre-v1.7 unbounded behavior.

## What This Package Cannot Defend Against

- A malicious or compromised **server** you connect to. Validate server-sent gameplay data in your own code.
- Man-in-the-middle on unencrypted `ws://`. Use `wss://`.
