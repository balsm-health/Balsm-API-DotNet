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

## Partition key: the user, not the health profile

Care-team rows are stored with a `health_profile_id`, but the pull filters on
**`user_id` only**. A health profile id is minted on the device
(`ensureSelfHealthProfile`), so a patient signing in on a replacement phone has a
different one — filtering on it returned zero rows and silently defeated the
entire feature for anyone without a Drive backup to restore from. The client maps
pulled rows onto its own local profile when it merges.

The user boundary still holds absolutely: a pull never returns another user's
rows, and a not-owned id answers 404.

**Follow-up before dependant profiles (P00X) ship:** with a single partition, a
dependant's roster and the account holder's would merge on a restored device.
Separating them needs a server-side notion of a health profile, which is its own
piece of work.

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

## Endpoints

| Route | Auth | Purpose |
|---|---|---|
| `GET /care-team/providers?health_profile_id=&since=` | JWT | Incremental pull, tombstones included |
| `POST /care-team/providers` | JWT | Upsert one row, idempotent on the client id |
| `DELETE /care-team/providers/{id}` | JWT | Tombstone |
| `GET /care-team/health` | anonymous | Module liveness (no DB, no dependencies) |

The user id always comes from the token's `NameIdentifier`/`sub` claim, never
from the request body — a client cannot write into another patient's roster by
forging a field.

Status codes: `200` upsert ok · `204` delete ok · `404` unknown **or** not-owned
(deliberately indistinguishable) · `409` tombstoned id · `422` invalid `type`.

## Audit trail

`care_team_audit_log` — one row per request that actually decrypted ciphertext
(FR-504), mirroring the FR-048 pattern for DOB.

| Column | Note |
|---|---|
| `user_id`, `health_profile_id` | whose rows were read |
| `actor` | the `NameIdentifier` claim of the caller |
| `source_ip` | `HttpContext.Connection.RemoteIpAddress` |
| `correlation_id` | `HttpContext.TraceIdentifier` |
| `row_count` | how many rows were decrypted |
| `occurred_at` | server clock |

It records **how much** was read, never **what** — the audit trail must not
become a second copy of the PHI it guards. A pull that returns nothing writes no
row, so an empty incremental poll does not flood the table.

## Account deletion

`DeletionPurgeJob` (Deletion module) calls `PurgeCareTeamAsync` inside its
per-account purge loop, hard-deleting both `care_provider` and
`care_team_audit_log` for that `user_id` with `IgnoreQueryFilters()` — without
it the tombstoned rows would survive the purge that is supposed to erase them.

That job hardcodes every context it purges; there is no purge-participant
abstraction. **Any future module holding user data must be added there**, or its
rows quietly outlive the account. The cross-module project reference this needs
(`Deletion.Infrastructure` → `CareTeam.*`) follows the existing
`Deletion.Infrastructure` → `Account.*` precedent.

## A trap worth remembering

`[Consumes("application/json")]` was briefly applied at **controller** level. It
is an action constraint, so `GET` and `DELETE` — which send no `Content-Type` —
matched no action at all, fell through to the admin SPA fallback, and answered
`200 text/html`. A PHI feed returning 200 to an unauthenticated caller, with
every unit test still green because the controller method itself was never
reached.

Integration tests over real HTTP are what caught it. `[Consumes]` now sits only
on the one action that has a request body.
