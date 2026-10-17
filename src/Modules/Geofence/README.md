---
context: Identity & Access
plane: cross-plane
features:
  - "P001: Egypt-only signup geo-fence by IP (ADR-09; denied-country list, blocked-signup response)"
---

# Balsm.Geofence

Signup geo-fencing module: enforces the Egypt-only launch policy (ADR-09 — dodges HIPAA-tier hosting until P016 revenue). Maps to the **Identity & Access** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/identity-access.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations).
