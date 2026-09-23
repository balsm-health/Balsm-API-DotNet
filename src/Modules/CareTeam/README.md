---
context: Personal Health
plane: consumer
features:
  - "P001: cloud mirror of the patient's care team (care_provider rows) so the roster survives device loss"
  - "P001: field-level AES-256-GCM encryption of nine free-text columns under a dedicated key (FR-502)"
  - "P001: decryption audit log — actor, source IP, correlation id, row count (FR-504)"
  - "P001: incremental pull by updated_at cursor with tombstones, scoped per health profile (FR-508/FR-510)"
  - "P001: hard purge of rows and audit trail on account deletion (FR-512)"
---

# Balsm.CareTeam

Cloud mirror of the patient's own care team — the doctors, pharmacies and labs
they entered themselves. Maps to the **Personal Health** bounded context —
canvas: `Balsm-Core/architecture/bounded-contexts/personal-health.md`.

The on-device SQLCipher database remains the write path and the source the app's
UI reads (ADR-11); this module is a mirror, never the primary. It exists because
the pre-existing encrypted blob backup targets the patient's own Google Drive and
needs a Google session, so patients who signed up with email OTP or Apple had no
backup at all.

> **PHI posture change.** This module makes care team the **second** category of
> PHI held on Balsm servers, after `date_of_birth`. ADR-10 ("PHI never on
> Supabase") is amended for it. Unlike `EmergencyQr` — where the key never leaves
> the device — the care-team key is server-side, so encryption at rest defends
> against a stolen dump or backup, not against Balsm itself. Spec and rationale:
> `Balsm-Core/specs/003-care-team-cloud-sync/spec.md` (FR-500..FR-515).

Layers: `Api` → `Application` → `Domain`, `Infrastructure` → `Domain`
(+ SQLite migrations). Handlers that touch the DbContext live in `Infrastructure`.

Field encryption is not a module concern — it reuses
`Balsm.Infrastructure/Encryption/CareTeamEncryptionService`, beside
`DobEncryptionService`, so both cloud-PHI fields share one envelope format.

C4: `docs/architecture/c4/care-team/` (context, container, dynamic-sync).
