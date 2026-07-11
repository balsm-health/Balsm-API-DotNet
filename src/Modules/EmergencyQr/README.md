---
context: Personal Health
plane: consumer
features:
  - "P001: emergency QR token mint (signed, TTL) resolving balsm.health/emergency/{token}"
  - "P001: compact device-signed public payload (blood type, allergies, contacts, conditions)"
  - "P002: resolution upgrade to signed Drive/iCloud URL + phone-dead trusted-contact link — future"
---

# Balsm.EmergencyQr

Emergency-access module: server-side surface of the emergency health card — token mint/resolution with TTL, read-only payload. The card content itself is patient-owned and device-resident; this module never stores PHI beyond the signed payload lifecycle. Maps to the **Personal Health** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/personal-health.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations).
