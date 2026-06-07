# Database Backup & Restore

Phase 0 deliverable. Balsm runs on SQLite (WAL mode) on the pharmacy machine, so
backups are local-first: an online copy of the live database, taken without stopping
the server, retained on a schedule, and restorable through a confirmed admin action.

## Components

| Piece | Type | Responsibility |
|---|---|---|
| `SqliteOnlineBackupService` | `IBackupService` | Takes a consistent online copy of the live DB using SQLite's backup API (no downtime), records a `BackupFile` row (filename, size, SHA-256, trigger, status). |
| `BackupScheduler` | `BackgroundService` | Reads cron + retention from `server_config`, runs scheduled backups, prunes old ones past the retention count. |
| `RestoreOrchestrator` | service | Verifies a backup (`PRAGMA integrity_check` + `foreign_key_check`), swaps it in, and signals a restart. |
| `BackupsController` | API | Admin endpoints for list / trigger / schedule / restore. |

## Configuration

`appsettings.json` → `Backup` section (`BackupOptions`):

```json
{
  "Backup": {
    "Directory": "backups",
    "MaxConcurrentBackups": 1
  }
}
```

Schedule + retention are **runtime** settings stored in the `server_config` table
(keys `backup_cron`, `backup_retention`), editable via the API below. Defaults:

- `backup_cron` = `0 2 * * *` (daily, 02:00)
- `backup_retention` = `30` (keep newest 30)

## Admin API

All routes are under `/api/v1/admin/backups` and require an authenticated admin
session (admin-auth middleware).

### List backups
```
GET /api/v1/admin/backups?page=1&pageSize=20
```
Returns `{ total, page, pageSize, items[] }`. `pageSize` is clamped to 1–100.

### Trigger an on-demand backup
```
POST /api/v1/admin/backups
```
Runs `BackupNowAsync(Manual)`. Returns the created backup's
`{ id, filename, sizeBytes, sha256, status, createdAt }`, or `400` with
`{ error }` on failure.

### Get schedule
```
GET /api/v1/admin/backups/schedule
→ { "cron": "0 2 * * *", "retention": 30 }
```

### Update schedule
```
PUT /api/v1/admin/backups/schedule
Content-Type: application/json

{ "cron": "0 3 * * *", "retention": 14 }
```
`cron` is validated with NCrontab; an invalid expression returns `400`.

### Restore a backup
```
POST /api/v1/admin/backups/{id}/restore
Content-Type: application/json

{ "confirmPhrase": "RESTORE" }
```
`confirmPhrase` **must** equal `RESTORE` (guards against accidental data loss).
The orchestrator integrity-checks the backup before swapping it in; on success it
returns `202 Accepted` and the service restarts to load the restored database.

## CLI / scripted use

There is no separate CLI binary — the endpoints above are the management surface.
Script a backup from the server host with `curl` (admin session cookie required):

```bash
# trigger a manual backup
curl -fsS -X POST https://localhost:5051/api/v1/admin/backups \
  --cookie "$ADMIN_COOKIE"

# change the schedule to hourly, keep 48
curl -fsS -X PUT https://localhost:5051/api/v1/admin/backups/schedule \
  --cookie "$ADMIN_COOKIE" -H 'Content-Type: application/json' \
  -d '{"cron":"0 * * * *","retention":48}'
```

## Operational notes

- Backups are written to `Backup:Directory` (relative to the content root unless
  absolute). Put this on a separate disk/volume for real durability.
- Each backup stores a SHA-256 so integrity can be re-verified out of band.
- Restore is destructive and triggers a restart — only the confirmed endpoint can
  initiate it.
- WAL checkpointing is handled by the online-backup API; no manual `wal_checkpoint`
  is needed before a backup.
