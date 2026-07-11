---
context: Identity & Access
plane: cross-plane
features:
  - "P001: multi-device sessions with remote logout"
  - "P004: device registry + immediate revocation"
---

# Balsm.Sessions

Session and device registry module: active-session listing, remote revocation (immediately blocks API calls from revoked devices). Maps to the **Identity & Access** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/identity-access.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations).
