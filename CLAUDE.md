# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

<!-- Universal rules live in ../Balsm-Roadmap/agents/rules/AGENTS.md and CODING_STANDARDS.md (imported at the bottom of this file). Keep this file focused on what's specific to Balsm-API-DotNet. -->

## Repository

Balsm API — .NET 10 modular monolith backing the Balsm healthcare platform. SDK pinned in `global.json` (`10.0.101`, `latestFeature` roll-forward); `TreatWarningsAsErrors=true` and centralized package versions (`Directory.Packages.props`) apply to every project.

## Common Commands

### Build / test / run
```bash
dotnet restore Balsm.API.slnx
dotnet build Balsm.API.slnx -c Release
dotnet test  Balsm.API.slnx                          # all tests
dotnet test  tests/Balsm.API.Tests --filter "FullyQualifiedName~Foo"   # single test/class

dotnet run --project src/Balsm.API                   # F5 equivalent; reads appsettings*.json
```

The API binds to `http://0.0.0.0:5050` by default (Standalone mode). In Standalone the host also starts an HTTPS listener on `port+1` (5051) for the admin panel using a self-signed cert managed by `CertificateService`.

### EF Core migrations
DbContexts live per module under `src/Modules/{Module}/Balsm.{Module}.Infrastructure`. Always specify both the migrations project and a startup project, e.g.:
```bash
dotnet ef migrations add Add{Feature}_{Entity}Table \
  --project src/Modules/Identity/Balsm.Identity.Infrastructure \
  --startup-project src/Balsm.API \
  --context {Module}DbContext

dotnet ef database update \
  --project src/Modules/Identity/Balsm.Identity.Infrastructure \
  --startup-project src/Balsm.API \
  --context {Module}DbContext
```
Default provider is SQLite (`Database__Provider=Sqlite`); Npgsql is also wired up — switch via `Database:Provider` + `Database:ConnectionString`. Every migration must implement both `Up()` and `Down()`.

### Admin UI (Vite + React 19 + TS)
```bash
cd admin-ui
npm install
npm run dev        # vite dev server
npm run build      # tsc -b && vite build → output served by API at /admin
```
The API serves the built admin SPA from `src/Balsm.API/wwwroot/admin` only when `DeploymentMode=Standalone`.

### Tunnel registry (Cloudflare Worker)
```bash
cd tunnel-registry
npm install
npm run dev        # wrangler dev
npm run deploy     # wrangler deploy
```

### Publish self-contained bundles
```bash
./scripts/publish-standalone.sh                      # all RIDs, version from Directory.Build.props
./scripts/publish-standalone.sh osx-arm64            # one RID
./scripts/publish-standalone.sh --version 1.2.3      # override version
./scripts/publish-all.sh win-x64 linux-x64           # API-only bundles (no Supervisor wiring change)
```
Both scripts publish single-file, self-contained executables for `osx-arm64`, `linux-x64`, `win-x64` into `artifacts/` and produce `tar.gz`/`zip` archives plus SHA-256 checksums in `artifacts/dist/`.

### Docker
```bash
docker compose up --build       # builds Dockerfile, exposes :5000, sqlite at /data/balsm.db
```

## Architecture

### Modular monolith — `src/`
- **`Balsm.SharedKernel`** — domain primitives reused by every module: `Result<T>`/`Error` (the failure-handling contract), domain event base types, repository abstractions, pagination. No EF/ASP.NET dependencies.
- **`Balsm.Infrastructure`** — cross-cutting host plumbing: `BaseDbContext`, `BaseRepository`, `CorrelationIdMiddleware`, `ExceptionHandlingMiddleware`, shared DI (`AddSharedInfrastructure`). Each module's DbContext derives from `BaseDbContext`.
- **`Balsm.Supervisor`** — Standalone-only admin module (admin panel API, mDNS broadcast, self-update, federation auth, certificate management). Registered conditionally in `Program.cs` when `DeploymentMode=Standalone`.
- **`src/Modules/{Customer,Entity,Identity,Inventory,POS,Prescription}/`** — each bounded context ships four projects:
  - `Balsm.{Module}.Domain` — entities, value objects, domain events, repository interfaces. No outward dependencies beyond `SharedKernel`.
  - `Balsm.{Module}.Application` — MediatR command/query handlers, FluentValidation validators, DTOs.
  - `Balsm.{Module}.Infrastructure` — EF Core `DbContext` + configurations, repository implementations, `Add{Module}Infrastructure(IConfiguration)` extension.
  - `Balsm.{Module}.Api` — controllers + `Add{Module}Module()` extension + `ModuleRegistration` marker class used by `Program.cs` to register the assembly's controllers.
- **`Balsm.API`** — composition root. `Program.cs` is the only place that wires modules together; cross-module references go through these `Add{Module}Module`/`Add{Module}Infrastructure` extensions, never through direct project-to-project references between modules.

### Module boundaries (hard rules)
- A module project must never reference another module's projects. Cross-module communication is via shared contracts in `SharedKernel` or domain events.
- Layer direction inside a module: `Api → Application → Domain` and `Infrastructure → Domain` (Infrastructure may also be referenced by the composition root only). `Domain` depends on nothing module-specific.
- Each module owns its own database schema/DbContext. No shared tables across modules.

### Composition root flow (`src/Balsm.API/Program.cs`)
1. Determine `DeploymentMode` (`Standalone` vs anything else — Standalone enables Kestrel HTTPS for admin, Supervisor module, admin SPA static files, federation/admin auth middleware).
2. Configure Serilog via `appsettings*.json` (uses an explicit `ConfigurationReaderOptions` assembly list so single-file publish keeps sinks discoverable — don't switch to default reflection-based loading).
3. `AddSharedInfrastructure` → each `Add{Module}Module()` (Application + Api) → each `Add{Module}Infrastructure(IConfiguration)` (DbContext + repositories) → conditionally `AddSupervisorModule`.
4. Register module assemblies as MVC `ApplicationPart`s via each module's `ModuleRegistration` marker.
5. Middleware order matters in Standalone: `CorrelationId → ExceptionHandling → (admin static files before UseRouting) → UseRouting → AdminAuth → FederationAuth → Authorization → MapControllers → MapFallbackToFile("/admin/...")`.

### Deployment modes
- **Standalone** (`DeploymentMode=Standalone`, default `appsettings.json`) — single-file executable for on-prem/desktop install. Includes Supervisor module, mDNS (`balsm.local`), HTTPS admin panel, self-update from GitHub releases (`Supervisor:GitHubRepository`), first-run sentinel.
- **Cloud / hosted** — set `DeploymentMode` to anything else; Supervisor is excluded, HTTPS redirect is enabled, admin SPA is not served.
- The same binary supports `UseWindowsService()` and `UseSystemd()` for platform-native daemon hosting.

## API-Specific Rules

- Respect module + layer boundaries: `API → Application → Domain` (and `Infrastructure → Domain`); no cross-module project references.
- Every endpoint requires authentication and permission checks — no exceptions.
- Soft-delete only — never hard-delete clinical or patient data.
- Use parameterized queries — never concatenate user input into SQL.
- `Result<T>` (`Balsm.SharedKernel.Results`) for expected failures; reserve exceptions for unexpected failures.
- All I/O is async — never block with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- Pass and respect `CancellationToken` through controller → handler → repository.
- Every new endpoint requires an integration test under `tests/Balsm.API.Tests/Controllers/`.

## .NET Naming Conventions

- Commands: `Create{Entity}Command`, `Update{Entity}Command`, `Delete{Entity}Command`
- Queries: `Get{Entity}ByIdQuery`, `List{Entity}Query`, `Search{Entity}Query`
- Handlers: `{Command|Query}Handler`; Validators: `{Command|Query}Validator`
- Events: `{Entity}{Action}Event` (e.g., `AppointmentCreatedEvent`)
- Repositories: `I{Entity}Repository` / `{Entity}Repository`
- DTOs: `{Entity}Dto`, `{Entity}DetailDto`, `{Entity}ListItemDto`
- Controllers: `{Entity}Controller`, plural resource route `/api/v1/{entities}`

## Planning Phase — C4 Diagrams (mandatory)

Before any non-trivial implementation (new module, new bounded context, new cross-module flow, new external integration, or any change to `Program.cs` composition root), produce C4 diagrams **first** and confirm them with the user before writing code.

- Author diagrams in Mermaid (`mermaidjs` C4 syntax: `C4Context`, `C4Container`, `C4Component`, `C4Dynamic`) so they render in GitHub, PR descriptions, and the admin UI.
- Required levels per scope:
  - **New bounded context / module** — Level 1 (System Context) showing the module's place in the modular monolith + external actors, **and** Level 2 (Container) showing `Api`, `Application`, `Infrastructure`, `Domain` projects and their DbContext.
  - **New cross-module flow or domain event** — Level 3 (Component) for each module involved, **and** Level 4 (Dynamic) sequencing the event/command path including `MediatR`, repository, DbContext.
  - **New external integration** (HL7/FHIR, DICOM, payment, federation, mDNS, Supervisor self-update) — Level 1 + Level 4 (Dynamic) showing trust boundary, auth, and failure modes.
- Save diagrams under `docs/architecture/c4/{module-or-feature}/` as `.md` files (one diagram per file, e.g. `context.md`, `container.md`, `component-{name}.md`, `dynamic-{flow}.md`). Link them from the PR description.
- Diagrams must reflect the hard rules in this file: no cross-module project references, layer direction `Api → Application → Domain` + `Infrastructure → Domain`, per-module DbContext, composition wiring only in `Balsm.API`.
- After implementation, update the same diagrams in the same PR if reality diverges — stale C4 is worse than no C4.
- Skip C4 only for: typo fixes, single-method refactors inside an existing class, dependency bumps, doc-only changes.

## Testing Requirements — full coverage mandatory

Code is not "done" until it ships with **unit + integration + end-to-end** tests covering every reachable path. No PR merges without all three layers where the layer applies.

### Layer ownership

- **Unit tests** — every class with logic: domain entities/value objects, MediatR handlers, FluentValidation validators, domain services, mappers. Run against in-memory fakes; no DB, no HTTP, no clock, no filesystem. Live next to the module in `tests/Balsm.{Module}.UnitTests/` (create the project if missing).
- **Integration tests** — every controller endpoint and every `Infrastructure` repository. Use `WebApplicationFactory<Program>` + a real `DbContext` against SQLite in-memory (matches default provider). Live in `tests/Balsm.API.Tests/Controllers/{Module}/` for endpoints and `tests/Balsm.{Module}.IntegrationTests/` for repository-only flows.
- **End-to-end tests** — every cross-module user flow, every clinical workflow, every Standalone-mode boot path (mDNS, HTTPS admin, self-update sentinel), and every external integration (HL7/FHIR, DICOM, payment, federation). Drive the running API over HTTP, exercise the SPA admin where applicable, and assert against domain events emitted. Live in `tests/Balsm.E2E.Tests/{Flow}/`.

### Coverage gates

- **Domain + Application layers**: 100% line and branch coverage. These layers are pure logic — no excuses.
- **Infrastructure + Api layers**: ≥ 90% line, ≥ 80% branch. Untested branches must be tagged with `[ExcludeFromCodeCoverage]` **and** a `// reason:` comment justifying the exclusion (e.g. trivial DI registration, framework boilerplate).
- **Overall solution**: ≥ 95% line. Enforce via `dotnet test --collect:"XPlat Code Coverage"` + Coverlet thresholds in `Directory.Build.props`; CI fails below threshold.
- Every bug fix lands with a regression test that fails before the fix and passes after — no "tested manually".

### Per-change checklist (block PR if any unchecked)

- [ ] New/changed domain entity, value object, or domain service → unit tests added/updated.
- [ ] New/changed command, query, handler, validator, or mapper → unit tests added/updated.
- [ ] New/changed endpoint or repository → integration tests added/updated under `tests/Balsm.API.Tests/Controllers/{Module}/` (or the module's `IntegrationTests` project).
- [ ] New/changed cross-module flow, domain event, or external integration → E2E test added/updated under `tests/Balsm.E2E.Tests/`.
- [ ] Coverage gates above still pass locally (`dotnet test Balsm.API.slnx --collect:"XPlat Code Coverage"`).
- [ ] No `[Skip]`, `[Ignore]`, or commented-out tests introduced.
- [ ] Tests are deterministic: no real clock (`IClock` abstraction), no `Random` without seed, no network, no shared global state.

### Naming + structure

- Test classes mirror SUT name: `{Sut}Tests` (unit), `{Endpoint}EndpointTests` (integration), `{Flow}E2ETests` (E2E).
- Test method pattern: `Method_Scenario_Expected` (e.g. `Handle_WhenPatientNotFound_ReturnsNotFoundError`).
- AAA layout (Arrange / Act / Assert) — separated by blank lines, not comments.
- One assertion concept per test; multiple `Assert` calls only when verifying one logical outcome.

## .NET Coding Standards (Balsm-API-specific quick refs)

- Domain-specific exception types — never `Exception`/`ApplicationException`.
- Structured logging always includes `CorrelationId`, `UserId`, `Action`, `Module`; never log PHI.
- `AsNoTracking()` for all read-only queries; prevent N+1 with `Include`/`ThenInclude` or batch queries.
- Migrations are reversible (`Up()` + `Down()`) and named descriptively.
- `ConfigureAwait(false)` in library/service code.
- `Task.WhenAll()` for independent parallel work; `ValueTask<T>` when methods frequently complete synchronously.
- FluentValidation at the API layer — one validator per command/query.

---

@../Balsm-Roadmap/agents/rules/AGENTS.md

@../Balsm-Roadmap/agents/rules/CODING_STANDARDS.md
