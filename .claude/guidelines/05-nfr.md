# 05 — Non-Functional Requirements (Balsm-API-DotNet)

Repo-specific operational floor. Extends the platform NFRs in `Balsm-Core/NON_FUNCTIONAL_REQUIREMENTS.md`. Where this file is stricter, **this file wins** for this repo; where Core is stricter, Core wins. Never weaken Core.

---

## 1. Latency Targets

Measured at the HTTP boundary, p95, against the default SQLite provider, single-node Standalone deployment, warm cache.

| Endpoint class | p95 latency | p99 latency |
|---|---|---|
| Simple read (single entity by id, list with paging ≤ 50) | ≤ 200 ms | ≤ 400 ms |
| Complex read (joins, filters, projections) | ≤ 500 ms | ≤ 1 s |
| Write (create / update / soft-delete) | ≤ 1 s | ≤ 2 s |
| Federation handshake / heartbeat | ≤ 300 ms | ≤ 800 ms |
| Auth (login, token refresh) | ≤ 400 ms | ≤ 1 s |
| Admin static SPA shell | ≤ 100 ms | ≤ 250 ms |

Cloud / hosted mode adds ≤ 100 ms p95 budget for network egress; everything else is the same.

A change that regresses p95 by > 10% on any endpoint class **must** ship with a benchmark + a rationale, or it does not merge.

## 2. Throughput + Concurrency

- The Standalone target is a pharmacy laptop / clinic NUC. Floor: **50 concurrent reads** + **10 concurrent writes** without sustained queueing.
- Cold start (process launch → first 200 OK): **≤ 10 s** in Standalone single-file publish.
- Background work (sync loop, heartbeat, backup, federation) must never block request threads. Use `IHostedService` / `BackgroundService` with bounded channels.

## 3. Availability

- Standalone: the binary is the availability boundary. A bad migration, a thrown unhandled exception in `Program.cs`, or a panic in `BackgroundService` brings the whole site down. Treat boot-path code as **safety-critical**:
  - All boot-path I/O wrapped in try/catch with structured logging before re-throwing.
  - Migrations are reversible (`Down()` exists). A failed `Up()` must not corrupt the DB; surface the error and exit non-zero.
  - `BackgroundService` exceptions are caught + logged + the service restarts with backoff. Never let one background loop kill the host.
- Cloud / hosted: target is **99.9%** monthly (43 min/month error budget). Anything that consumes budget needs an action item logged within 24 h.

## 4. Resource Budgets

- Memory at idle, Standalone, no traffic: **≤ 200 MB**.
- Memory under target throughput (50 reads + 10 writes/s): **≤ 700 MB**.
- File handles open at idle: **≤ 300**. A leak past 1000 must page.
- Backup writes use streaming (`SqliteOnlineBackupService`). Never load the DB into memory to copy it.

## 5. Observability — minimum surface

Every request and every background-loop tick emits a structured log line with:

- `CorrelationId` (per request, propagated to all logs in the call chain).
- `UserId` (or `system` for background work, or `anonymous` for pre-auth).
- `Action` (controller + handler name, or background service name).
- `Module` (the bounded context).
- `DurationMs`.
- `Outcome` (`success`, `validation_error`, `auth_error`, `not_found`, `conflict`, `unexpected_error`).

Additional rules:

- **PHI never appears in logs.** Log entity IDs only. A logger that wraps a domain object must call a `ToLogString()` that strips PHI, or it must not be called.
- Sentry is wired (commit `ef58b48` introduced it). Errors are tagged with `CorrelationId`, `Module`, `DeploymentMode`. Do not add a parallel error sink.
- Serilog is configured via `appsettings*.json` with an **explicit assembly list** (`ConfigurationReaderOptions`) — required for single-file publish. Do not switch to reflection-based loading.
- Metrics: every endpoint contributes to a request-count + duration histogram tagged by `route`, `status`, `module`. Background services contribute `tick_count`, `tick_duration`, `last_error_ts`.

## 6. Security Floor

This is a **healthcare API**. The minimum:

- Every endpoint authenticated + authorized. The only exceptions: `/healthz`, `/openapi`, `/scalar` in dev. Every exception has an `[AllowAnonymous]` plus a `// reason:` comment.
- Soft-delete only for clinical or patient data. Hard deletes require a documented retention exception in the migration.
- Parameterized queries only. No string-concat SQL. No raw SQL outside `Infrastructure`.
- All write endpoints accept `Idempotency-Key`. See `06-coding-best-practices.md` §6.
- Field-level encryption for diagnoses, medications, lab results — when these modules land. The shape is in `Balsm-Core/CERTIFICATIONS.md` §3; follow it before storing the first row.
- Secrets only via environment variables or platform secret managers. No secrets in `appsettings*.json` committed to git. CI runs `gitleaks` + `trufflehog` per `balsm-ai:implementing-secrets-scanning-in-ci-cd`.
- Rate limiting on all auth + write endpoints (token bucket per IP + per user). `429` returns `Retry-After`.
- TLS: Standalone uses self-signed cert managed by `CertificateService` (admin only). Cloud uses platform TLS termination. Never serve admin over HTTP.
- Audit log: every write to clinical, prescription, or billing data emits an audit entry with previous value, new value, user, timestamp. No exceptions for "internal jobs".

## 7. Data Integrity

- All clinical operations are ACID. Multi-entity writes go through a single `DbContext.SaveChangesAsync()` + `IDbContextTransaction` when crossing aggregate roots.
- Optimistic concurrency with row versions / xmin / `LastModifiedAt` checks on all editable clinical entities. Silent overwrites are forbidden.
- Referential integrity enforced at the DB. No nullable FKs to "patch later".
- Code lookups (ICD-10, CPT, SNOMED CT, LOINC, RxNorm) follow `Balsm-Core/CERTIFICATIONS.md` rules — never approximate, never invent custom codes.

## 8. Backups

- `SqliteOnlineBackupService` runs on the schedule in `BackupScheduler`. Default: hourly incremental + nightly full, retained 30 days.
- Backup destination is the path returned by `GetBackupPath()`. **Never** print PHI in backup logs, only the path + size + SHA256.
- Restore goes through `RestoreOrchestrator`. A restore acquires an exclusive lock, replaces the DB, runs migrations, and re-validates schema before accepting traffic.

## 9. Self-Update (Standalone)

- Source: GitHub releases at `Supervisor:GitHubRepository`.
- Verify signature + SHA256 of the downloaded artifact before replacing the binary.
- Self-update never runs while a backup or restore is in progress.
- Rollback path: previous binary kept for one cycle; failed boot triggers automatic rollback.

## 10. Cancellation + Async

- Every controller action accepts a `CancellationToken` and propagates it through to MediatR → handler → repository.
- All I/O is async — no `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` in production code paths.
- External HTTP calls (federation, GitHub releases, tunnel registry) have explicit timeouts. No unbounded waits.
- Long-running endpoints (sync handshakes) honor cancellation within ≤ 1 s of the client disconnecting.
