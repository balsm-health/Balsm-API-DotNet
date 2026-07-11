---
context: Identity & Access
plane: cross-plane
features:
  - "P000: identity persistence foundation (DbContext registered for MigrationRunner)"
  - "P004: workspace membership + roles (Owner/Admin/Member; 3 hard-coded roles)"
  - "P004: invite codes (one-time, 6-char, configurable expiry)"
  - "P014: full permissions engine (custom roles, per-action grants) — future"
---

# Balsm.Identity

Core identity module: workspace membership, roles, and the permission-lookup abstraction (swappable for the P014 engine). Maps to the **Identity & Access** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/identity-access.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations).
