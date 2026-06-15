# 01 — API Development Rules (.NET 10 Modular Monolith)

Concrete rules for code inside `Balsm-API-DotNet/`. Extends `Balsm-Core/agents/rules/AGENTS.md` §2 (Architecture & Domain Rules) — read that first.

---

## 1. SDK + Build

- SDK pinned via `global.json` (`10.0.101`, `latestFeature` roll-forward). Never bump in a feature PR.
- `TreatWarningsAsErrors=true` repo-wide. **Do not add `<NoWarn>`** to silence warnings; fix the warning. Exceptions require a `<!-- reason: -->` comment naming the rule and the justification.
- Package versions are centralized in `Directory.Packages.props`. Add a package **only** by editing that file — never inline `<Version>` in a `.csproj`.
- Solution file is `Balsm.API.slnx` (slnx, not sln). All new projects must be added to it.

## 2. Module Layout (hard rules)

Every bounded context under `src/Modules/{Module}/` ships exactly four projects:

```
Balsm.{Module}.Domain          // entities, value objects, domain events, repository interfaces
Balsm.{Module}.Application     // MediatR handlers, FluentValidation validators, DTOs
Balsm.{Module}.Infrastructure  // EF DbContext + configurations, repository implementations
Balsm.{Module}.Api             // controllers, Add{Module}Module() extension, ModuleRegistration marker
```

- **No cross-module project references.** Module A's `.csproj` must not reference Module B's `.csproj`. Cross-module communication is via:
  - shared contracts in `Balsm.SharedKernel`, or
  - domain events.
- **Layer direction** inside a module:
  - `Api → Application → Domain`
  - `Infrastructure → Domain` (Infrastructure is referenced by the composition root **only**; no other module touches it).
- **Domain depends on nothing module-specific.** No EF, no ASP.NET, no MediatR, no FluentValidation in `Domain`.
- **Each module owns its DbContext + schema.** No shared tables across modules. No `DbContext` in one module references another module's entities.
- Cross-module reads happen via **public read DTOs** exposed through `Application` queries, not via direct entity access.

## 3. Composition Root (`src/Balsm.API/Program.cs`)

This file is the **only** place that wires modules. Do not add module-to-module wiring anywhere else.

Required order in `Program.cs`:

1. Resolve `DeploymentMode` (`Standalone` vs anything else).
2. Configure Serilog using the explicit `ConfigurationReaderOptions` assembly list — **do not** switch to default reflection-based loading (single-file publish loses sinks).
3. `AddSharedInfrastructure()`.
4. For each module: `Add{Module}Module()` (Application + Api).
5. For each module: `Add{Module}Infrastructure(IConfiguration)` (DbContext + repositories).
6. Conditionally: `AddSupervisorModule()` **only** if `DeploymentMode=Standalone`.
7. Register MVC `ApplicationPart`s via each module's `ModuleRegistration` marker class.

Middleware order (Standalone) — do not reorder:

```
CorrelationId → ExceptionHandling → (admin static files BEFORE UseRouting) →
UseRouting → AdminAuth → FederationAuth → Authorization → MapControllers →
MapFallbackToFile("/admin/...")
```

## 4. Deployment Modes

- **Standalone** (`DeploymentMode=Standalone`, default `appsettings.json`) — single-file binary for on-prem/desktop:
  - Kestrel binds HTTP `0.0.0.0:5050` + HTTPS `:5051` (self-signed cert via `CertificateService`).
  - Supervisor module loaded, mDNS broadcast on `balsm.local`, admin SPA served from `wwwroot/admin`, GitHub self-update via `Supervisor:GitHubRepository`.
- **Cloud / hosted** — `DeploymentMode != Standalone`:
  - Supervisor module excluded.
  - HTTPS redirect enabled.
  - Admin SPA not served.
- The same binary supports `UseWindowsService()` and `UseSystemd()`. Daemon hosting is platform-aware in `Program.cs` — do not duplicate.

## 5. MediatR + CQRS

- One handler per command/query. Handler does **one** thing: validate → call domain → persist → publish events.
- Cross-cutting concerns go in MediatR pipeline behaviors (validation, logging, transaction). Do not duplicate them inside handlers.
- A query handler **never** mutates state. A command handler **never** returns a domain entity — only a DTO or `Result<T>`.
- FluentValidation runs in the pipeline. Validators live in `Application/Validators/`. One validator per command/query.

## 6. EF Core

- Default provider is **SQLite** (`Database__Provider=Sqlite`). Npgsql is wired but optional. **Never** write provider-specific SQL outside `Infrastructure` — and when you do, gate it on `Database:Provider`.
- All read queries use `AsNoTracking()`. Track only when you intend to write.
- Prevent N+1: use `Include`/`ThenInclude` or projection queries. If a list endpoint loads a child collection, the integration test must assert query count.
- Use projections (`Select(... new XDto { ... })`) for read endpoints — never load full entities to map to DTOs in memory.
- Pagination at the database (`Skip`/`Take` or keyset). Never `.ToList()` then paginate.

## 7. Migrations

- Migrations live under `src/Modules/{Module}/Balsm.{Module}.Infrastructure/Migrations/`.
- Every migration implements both `Up()` and `Down()`. A migration with no-op `Down()` is rejected in review.
- Name pattern: `Add{Feature}_{Entity}Table`, `Add{Column}To{Entity}`, `Create{Index}On{Entity}`.
- Generate with both `--project` and `--startup-project`:

  ```bash
  dotnet ef migrations add Add{Feature}_{Entity}Table \
    --project src/Modules/{Module}/Balsm.{Module}.Infrastructure \
    --startup-project src/Balsm.API \
    --context {Module}DbContext
  ```

- Migrations are **applied on boot** via `MigrationRunner`. Do not add a separate manual migration step in deploys.

## 8. Controllers & Routing

- Route: `/api/v1/{resource}` — resource plural, kebab-case for multi-word (`/care-teams`).
- Controllers are thin: bind → forward to MediatR → translate `Result<T>` to HTTP. No business logic.
- Every controller action requires:
  - `[Authorize(Policy = "...")]` (or a deliberate `[AllowAnonymous]` with a `// reason:` comment).
  - `[ProducesResponseType<TDto>(StatusCodes.Status200OK)]` plus every non-2xx it can return (400/401/403/404/409/422/429), each `ProblemDetails`.
  - `[Consumes("application/json")]` + `[Produces("application/json")]` (or `application/fhir+json` for FHIR endpoints).
  - `[EndpointSummary]`, `[EndpointDescription]`, `[EndpointName("{Module}_{Action}")]`, `[Tags("{Module}/{Resource}")]`.
  - XML doc comment on the action **and** every DTO it touches.
- Write endpoints accept and honor an `Idempotency-Key` header — see `06-coding-best-practices.md` §6.
- Every endpoint propagates `CancellationToken` to MediatR.

## 9. Per-Module Checklist (block PR if any unchecked)

- [ ] Layer direction respected (`Api → Application → Domain`, `Infrastructure → Domain` only).
- [ ] No cross-module project references.
- [ ] DbContext + migrations live in this module's `Infrastructure` project.
- [ ] `Add{Module}Module()` + `Add{Module}Infrastructure(IConfiguration)` exist and are wired in `Program.cs`.
- [ ] `ModuleRegistration` marker class registered as an MVC `ApplicationPart`.
- [ ] Insomnia collection updated (`docs/api/insomnia/{module}.yaml`) — see root `CLAUDE.md`.
- [ ] OpenAPI spec regenerated (`docs/api/openapi/v1/{module}.json`) — see root `CLAUDE.md` and `02-cross-repo-contracts.md`.
- [ ] Unit + integration + (where applicable) E2E tests landed in same PR — see root `CLAUDE.md` Testing Requirements.
- [ ] C4 diagrams added/updated under `docs/architecture/c4/{module-or-feature}/` for new modules/flows.

## 10. Common Commands

```bash
dotnet restore Balsm.API.slnx
dotnet build  Balsm.API.slnx -c Release
dotnet test   Balsm.API.slnx
dotnet test   tests/Balsm.API.Tests --filter "FullyQualifiedName~Foo"
dotnet run --project src/Balsm.API

# Publish
./scripts/publish-standalone.sh                  # all RIDs
./scripts/publish-standalone.sh osx-arm64        # one RID
./scripts/publish-standalone.sh --version 1.2.3  # override version
./scripts/publish-all.sh win-x64 linux-x64       # API-only bundles

# Docker
docker compose up --build
```

## 11. Admin UI (Vite + React 19 + TS)

- Source: `admin-ui/`. Built output is copied into `src/Balsm.API/wwwroot/admin` and served **only** when `DeploymentMode=Standalone`.
- Do not introduce a new HTTP client in the admin UI — extend `admin-ui/src/api.ts`.
- TypeScript types in `admin-ui/src/api.ts` mirror server DTOs. When a server DTO changes, update `api.ts` in the **same PR**.
