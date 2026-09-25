<p align="center">
  <img src=".github/banner.png" alt="واجهة بلسم البرمجية · Balsm API" width="880">
</p>

# Balsm API

.NET 10 modular monolith backing the Balsm healthcare platform. Serves the patient
app (`balsm_app`), the admin panel, and public endpoints (emergency QR resolve, care
directory). The patient app keeps PHI on-device — this API stores only encrypted or
non-PHI data.

SDK pinned in `global.json` (10.0.x, `latestFeature` roll-forward).
`TreatWarningsAsErrors=true`; package versions centralized in `Directory.Packages.props`.

## Layout

| Path | Role |
|---|---|
| `src/Balsm.API/` | Host: DI, pipeline, OutputCache, rate limiting, admin SPA hosting. Binds `http://0.0.0.0:5050` (Standalone also HTTPS :5051). |
| `src/Balsm.SharedKernel/` | Domain base types, MediatR plumbing, shared abstractions. |
| `src/Balsm.Infrastructure/` | Cross-module infrastructure (DB provider wiring, storage). |
| `src/Balsm.Supervisor/` | Process supervisor for standalone deployments. |
| `src/Modules/<Name>/` | Bounded contexts (14): Account, Auth, CareDirectory, Customer, Deletion, Disclosure, EmergencyQr, Entity, Geofence, Identity, Inventory, POS, Prescription, Sessions. Each splits `Domain / Application / Infrastructure / Api`. |
| `admin-ui/` | Admin panel (Vite + React 19 + TS), served by the API at `/admin` in Standalone mode. |
| `tunnel-registry/` | Cloudflare Worker (wrangler). |
| `tests/` | `Balsm.API.Tests` (unit), `Balsm.API.IntegrationTests`, `Balsm.Supervisor.Tests`, per-module tests under `tests/Modules/`. |

## Setup & run

```bash
dotnet restore Balsm.API.slnx
dotnet build   Balsm.API.slnx -c Release
dotnet run --project src/Balsm.API        # http://localhost:5050
```

Database: SQLite by default (`Database:Provider=Sqlite`) — zero setup. Postgres is
wired; switch with `Database:Provider` + `Database:ConnectionString`. Migrations live
per module; always pass both projects:

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/<M>/Balsm.<M>.Infrastructure \
  --startup-project src/Balsm.API --context <M>DbContext
```

Every migration implements both `Up()` and `Down()`.

## Tests

```bash
dotnet test Balsm.API.slnx
dotnet test tests/Balsm.API.Tests --filter "FullyQualifiedName~Foo"
```

## API reference

- OpenAPI specs: `docs/api/openapi/`
- **Insomnia collections** — one per module in `docs/api/insomnia/{module}.yaml`.
  Binding rule: every endpoint/DTO/route/auth change updates the owning module's
  collection in the same commit. Out-of-sync collections fail review.
- Every module exposes `GET /{slug}/health`; a module without a passing health
  endpoint is incomplete.

## Architecture docs

- C4 diagrams: `docs/architecture/c4/` (per-context: care-directory, caching-rate-limit, …)
- Feature specs & plans: `docs/superpowers/specs/`, `docs/superpowers/plans/`
- Ops: `docs/ops/` (hosting), `docs/backup-and-restore.md`
- Repo-specific agent guidance: [CLAUDE.md](CLAUDE.md); org-wide rules in
  `../Balsm-Core/agents/rules/`

## Deployment

Self-contained bundles: `./scripts/publish-standalone.sh [rid]` (version from
`Directory.Build.props`). `DeploymentMode=Standalone` serves the admin SPA and
starts the self-signed HTTPS admin listener.

## Security conventions

- Never hardcode secrets or connection strings — environment variables only.
- Soft-delete for clinical/patient records — never hard-delete.
- No PHI in logs, commits, fixtures, or docs; synthetic data only.
