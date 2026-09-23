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

## Schema

`care_provider` — one row per provider, keyed by the device-minted id.

| Column | Type | Note |
|---|---|---|
| `id` | uuid PK | device UUIDv7, never server-assigned |
| `user_id`, `health_profile_id` | uuid | pull filters on both (FR-510) |
| `type` | varchar(16) | one of the eight `CareProvider.AllowedTypes` |
| `name_ct` … `notes_ct` | bytea | nine AES-256-GCM columns; `name_ct` is the only non-null one |
| `created_at` | timestamptz | device clock, carried through |
| `updated_at` | timestamptz null | **server** clock — the LWW comparison key |
| `is_deleted`, `deleted_at` | bool / timestamptz null | tombstone (FR-507) |

Index `(user_id, health_profile_id, updated_at)` serves the incremental pull.

Migrations are generated for both providers: Npgsql in
`Balsm.CareTeam.Infrastructure/Migrations/`, SQLite in
`Balsm.CareTeam.Infrastructure.Migrations.Sqlite/Migrations/`.

## Sync semantics

Three operations, all scoped to the caller's `user_id` taken from the token.

- **Upsert** — idempotent on the client id. A new id inserts; an existing one is
  overwritten whole (last-writer-wins on the row, not per field). An id owned by
  another user answers `NotFound`, never a distinguishable `Forbidden`, so
  ownership cannot be probed. A tombstoned id answers `Tombstoned` rather than
  being re-created under the same primary key — a device that was offline while
  the row was deleted elsewhere must pull the tombstone, not push over it.
- **Delete** — tombstones, idempotent; a repeat succeeds. Unknown and not-owned
  both answer `NotFound`.
- **Pull** — `IgnoreQueryFilters()` so tombstones reach the client, filtered on
  `user_id` AND `health_profile_id`, `updated_at` ascending, exclusive `since`
  cursor. Decrypts on the way out.

`updated_at` is always the server's clock (`BaseDbContext.SetAuditFields`). The
client's `created_at` is carried through for first-write ordering only, so a
device whose clock is months fast cannot win every LWW comparison and freeze a
row.
