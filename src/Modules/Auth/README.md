---
context: Identity & Access
plane: cross-plane
features:
  - "P001: registration + login (email/phone + password, Google, Apple)"
  - "P001: OTP flows"
  - "P004: cloud-first auth with local JWT fallback (cached credentials, offline registration queue)"
  - "P004: brute-force lockout (5 failures → 15 min)"
  - "P010: Supabase JWT bridge validation (cached public key, offline-capable, ADR-03)"
---

# Balsm.Auth

Authentication module: credential flows, token issuance/refresh, and the Supabase JWT bridge. Maps to the **Identity & Access** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/identity-access.md`.

Constitutional: 100% test coverage on auth endpoints, both cloud and local paths.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations).
