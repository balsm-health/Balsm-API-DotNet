---
context: Identity & Access
plane: cross-plane
features:
  - "P001: disclosure acceptance records (onboarding: no-cloud-sync/data-loss warning; retention disclosure)"
---

# Balsm.Disclosure

Disclosure-acceptance module: records which mandatory disclosures a user has acknowledged (device-loss warning pre-P002, deletion retention notice). On-device acceptance is canonical; this module holds the server-side record. Maps to the **Identity & Access** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/identity-access.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations).
