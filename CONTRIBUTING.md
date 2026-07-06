# Contributing Guidelines

Thank you for your interest in contributing to **socketio-unity**.

Because this project is a clean-room implementation, contributions must follow
strict rules to maintain legal safety.

---

## 🚨 Clean-Room Rules (Mandatory)

By contributing to this project, you confirm that:

- You have **not copied** code from the official Socket.IO JavaScript client
- You have **not copied** code from paid or closed-source Unity Socket.IO assets
- You are **not porting or translating** existing implementations
- Your contribution is **original** and based on protocol documentation or behavior

If you are unsure whether your contribution complies, **do not submit it** —
open a discussion instead.

---

## ✅ Allowed Contributions

- Original protocol implementations based on public documentation
- Bug fixes and performance improvements
- Documentation improvements
- Tests and sample projects
- Platform compatibility fixes

---

## ❌ Disallowed Contributions

- Code derived from other Socket.IO client implementations
- Decompiled, reverse-engineered, or translated code
- Contributions that mimic internal structures of paid assets
- Copying undocumented quirks from other clients

---

## 🧪 Testing

All new features should include:
- Clear explanation of protocol behavior
- Reproducible test cases
- No reliance on undocumented behavior

---

## 📝 Pull Requests

- Keep PRs focused and small
- Explain *what* and *why*, not just *how*
- Reference protocol documentation when applicable

---

## 🤖 AI Agent Contributors

If you are an AI coding agent working in this repository, read `CLAUDE.md` in the repo root first. It covers:

- Which files are safe to edit and which need care
- Key architectural invariants that must be preserved across edits
- CI behavior (full EditMode + PlayMode suite, 76 tests, runs on every push)
- The public API stability contract for v1.x

---

Thank you for helping keep this project clean, open, and trustworthy.
