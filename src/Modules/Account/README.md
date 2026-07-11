---
context: Identity & Access
plane: cross-plane
features:
  - "P001: username selection (@handle, 3-30 chars, reserved blocklist, case-insensitive unique)"
  - "P001: profile metadata (display_name, bio, avatar_url, dob_year — non-PHI per ADR-10)"
  - "P001: country/language change (CountryCode, Bcp47Tag value objects)"
---

# Balsm.Account

User account profile module: username lifecycle (choose, validate against reserved blocklist, release on purge) and non-PHI profile metadata. Maps to the **Identity & Access** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/identity-access.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations). Communicates with sibling modules through integration events and shared contracts only.
