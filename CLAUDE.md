# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

<!-- Universal rules live in ../Balsm-Core/agents/rules/AGENTS.md and CODING_STANDARDS.md (imported at the bottom of this file). Keep this file focused on what's specific to Balsm-API-DotNet. -->

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

### Per-module health endpoint — mandatory

Every module `Balsm.{Module}.Api` ships a liveness `HealthController` — no exceptions, including stub modules with no other endpoints.

- Route: `GET /{module-slug}/health` (lowercase module slug, hyphenated where the module's resource route is, e.g. `emergency-qr/health`). Class: `Controllers/HealthController.cs`, `[ApiController]`, `[AllowAnonymous]`.
- **Liveness only** — the controller must NOT touch a `DbContext`, repository, MediatR, or any module dependency (Api references Application only; a DB check would break the `Api → Application → Domain` layering). DB/migration readiness is gated globally by `ReadinessGate` + `MigrationRunner` and surfaced at `GET /api/v1/health` and `GET /status`.
- Response shape is fixed: `{ "data": { "status": "Healthy", "module": "{Module}", "version": "{assembly}", "timestamp": "{utc}" } }`.
- This is the one sanctioned exception to "every endpoint requires authentication" — health probes are anonymous. Document the exception in code with the `[AllowAnonymous]` attribute; do not add auth.
- **When you add a new module:** add its `HealthController`, a `GET /{slug}/health` request in the module's Insomnia collection (new collection if the module had none), and an integration test asserting `200` + `status=Healthy`. A module without a passing health endpoint is incomplete.

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

## Insomnia Collections — mandatory per API change

Every endpoint add/change/remove ships with a matching update to the module's Insomnia collection in the **same PR**. Out-of-sync collections are treated as a broken build.

### Layout

- One collection per module: `docs/api/insomnia/{module}.yaml` (e.g. `identity.yaml`, `prescription.yaml`, `pos.yaml`).
- **Binding rule — no exceptions:** every API change (add / modify / rename / delete an endpoint, request DTO, response DTO, route, auth requirement, or query/filter param) MUST update the owning module's collection in the **same commit/PR**. A controller change with no matching `docs/api/insomnia/*.yaml` diff is an incomplete change and fails review. New module ⇒ new collection file.
- **Current collections (keep this list in sync when a module is added or removed):**
  - `auth.yaml` — `AuthController` (`/auth/*`)
  - `account.yaml` — `AccountController` (`/account/*`)
  - `deletion.yaml` — `DeletionController` (`/deletion/*`)
  - `disclosure.yaml` — `DisclosureController` (`/disclosure/*`)
  - `emergency-qr.yaml` — `EmergencyQrController` (`/emergency-qr/*`)
  - `sessions.yaml` — `SessionsController` (`/sessions/*`) + `StatusController` (`/status`)
  - `entity.yaml` — Entity module + Identity `UsersController` (`/api/v1/admin/*`); also carries `entity/health` + `identity/health`
  - `inventory.yaml` — Inventory module (`inventory/health`; stub — health only until endpoints land)
  - `pos.yaml` — POS module (`pos/health`; stub)
  - `customer.yaml` — Customer module (`customer/health`; stub)
  - `prescription.yaml` — Prescription module (`prescription/health`; stub)
  - `platform.yaml` — `Balsm.API` host controllers (health, version, server-info, audit, backups, logs)
  - `supervisor.yaml` — Supervisor module (Standalone-only)
  - Every module collection's first folder is `Health` (`GET /{slug}/health`, anonymous).
- Use **Insomnia v5 file format** (`type: collection.insomnia.rest/5.0`, YAML). Do not commit v4 `_type: export` JSON dumps — they bloat diffs and lose folder structure.
- Group requests into folders per controller / resource (e.g. `Patients/`, `Appointments/`). Request name = HTTP verb + route template (e.g. `POST /api/v1/patients`).
- Top-level `environments` block defines four `subEnvironments`: `local` (`http://localhost:5050`), `dev` (`https://api-dev.balsm.health`), `stg` (`https://api-staging.balsm.health`), `prod` (`https://api.balsm.health`) — each with `base_url` + auth token vars. Never commit real tokens — use `{{ _.balsm_token }}` placeholders sourced from `.env.local`.

### Per-change checklist (PR blocked if any unchecked)

- [ ] New endpoint → new request added with verb, path, headers (`Authorization`, `Idempotency-Key` for writes, `X-Correlation-Id`), example body matching the command/request DTO, and example response matching the result DTO.
- [ ] Changed request/response DTO → request body + example response updated; old example removed, not left stale.
- [ ] Renamed/moved route → request path + folder updated to match controller route.
- [ ] Deleted endpoint → request removed from collection (no commented-out leftovers).
- [ ] Auth/permission changed → request `Authorization` header + a comment line in the request description naming the required permission.
- [ ] Pagination/filter params changed → request query params updated to current contract.
- [ ] Validation rule changed → at least one negative-case example request added or refreshed showing the rejection payload.

### v5 schema gotchas (import fails silently if violated)

The Insomnia desktop importer validates against a strict zod schema. The hand-written collections must match what Insomnia itself exports, **not** a simplified shape:

- `meta.created` / `meta.modified` (and the same on every folder, request, environment, cookieJar) are **epoch-millisecond numbers** (`1735689600000` = `2025-01-01T00:00:00Z`) — **not** ISO strings. ISO strings ⇒ `invalid_type: expected number`.
- `environments` is a **single object** (`{ name: Base Environment, meta, data, subEnvironments: [...] }`), **not** an array of environments. Per-env entries (`local`, `local-admin`, `staging`) go under `subEnvironments`. An array ⇒ `invalid_type: expected object`.
- Each folder/request carries its own nested `meta: { id, created, modified, sortKey }` — sort key lives at `meta.sortKey` (number, negative = top), **not** a top-level `metaSortKey`. `id`/`method`/`url`/`headers`/`body` stay top-level on the request.
- Top-level `cookieJar` (`{ name, meta, cookies: [] }`) is present.
- Round-trip a real Insomnia 10.x export to confirm the shape before authoring a new collection by hand; the repo's existing collections are the reference template.

### Determinism rules

- IDs in examples use fixed GUIDs (e.g. `00000000-0000-0000-0000-000000000001`) — never `Guid.NewGuid()` output.
- Timestamps in request/response **example bodies** use a fixed ISO-8601 value (e.g. `2025-01-01T00:00:00Z`) — never "now". (Schema `meta.created/modified` are epoch numbers — see gotchas above.)
- No PHI in examples. Use the synthetic-patient fixtures (`Jane Doe`, DOB `1990-01-01`, MRN `MRN-000001`).
- Folder + request order is stable across edits so diffs stay readable; the sort key (`meta.sortKey`) must be set, not auto-generated on save.

### Tooling

- After editing, validate locally: `npx insomnia-inso run collection --src docs/api/insomnia/{module}.yaml --env local` (smokes every request against a running API).
- CI runs the same `inso` validation across all collections; a request whose URL/verb does not resolve to a controller route fails the build.
- The collection is the source of truth for the admin UI's HTTP client examples and for the `docs/api/` reference site — keep it accurate.

## OpenAPI / Swagger — mandatory per API change

Every endpoint add/change/remove ships with an updated, **checked-in** OpenAPI 3.1 document in the **same PR**. Generated specs out of sync with controllers fail CI.

### Generation + storage

- Use the built-in `Microsoft.AspNetCore.OpenApi` pipeline in `Balsm.API` (no Swashbuckle). Emit specs at build time via `Microsoft.Extensions.ApiDescription.Server`.
- One spec file per module, versioned: `docs/api/openapi/v1/{module}.json` (e.g. `identity.json`, `prescription.json`). Aggregated `docs/api/openapi/v1/balsm.json` is built from the per-module files — do **not** hand-edit the aggregate.
- Each spec file uses the module's `Add{Module}Module()` document name so `MapOpenApi("/openapi/{documentName}.json")` resolves cleanly at runtime, and Scalar (`/scalar/v1`) renders it in dev.
- Build step: `dotnet build` triggers spec emission; `dotnet build /t:GenerateOpenApiSpec` regenerates without a full build. PR diff must show the regenerated JSON.

### Required annotations on every endpoint

- XML doc comments on the controller action **and** on each request/response DTO and command/query type. Comments propagate into the spec — terse "fixes bug" comments are not acceptable.
- `[ProducesResponseType<TDto>(StatusCodes.Status200OK)]` (or 201/204) **and** every non-2xx status the action can return (`400`, `401`, `403`, `404`, `409`, `422`, `429`). Every `4xx` returns `ProblemDetails`; declare `[ProducesResponseType<ProblemDetails>(StatusCodes.Status{Code})]`.
- `[Consumes("application/json")]` + `[Produces("application/json")]` (or `application/fhir+json` for FHIR endpoints).
- `[EndpointSummary("...")]` + `[EndpointDescription("...")]` + `[EndpointName("{Module}_{Action}")]` so client codegen produces stable, namespaced method names.
- `[Tags("{Module}/{Resource}")]` grouping; tag order must match controller order.
- Auth: `[Authorize(Policy = "...")]` is reflected via the `security` block; document the required permission in the action's `<remarks>` so it appears in Scalar.
- Examples: provide a request + a 2xx response example using `IOpenApiSchemaTransformer` or `[OpenApiExample]`. Examples use the same fixed GUIDs / synthetic patients as the Insomnia rule above — never PHI.

### Per-change checklist (PR blocked if any unchecked)

- [ ] New endpoint → action annotated, DTOs documented, request + response example added, `docs/api/openapi/v1/{module}.json` regenerated and committed.
- [ ] Changed DTO field, type, or nullability → schema regenerated; **breaking change** (rename, type change, removed field, tightened validation) gets a `x-balsm-breaking: true` extension on the operation and an entry in `docs/api/openapi/CHANGELOG.md`.
- [ ] Renamed/moved route → spec regenerated; old path removed (no deprecated stub left without `deprecated: true` + sunset date).
- [ ] Deleted endpoint → operation removed from spec; if any client depends on it, mark `deprecated: true` for one release first, then delete.
- [ ] Auth/permission changed → `security` requirement updated **and** the required permission documented in `<remarks>`.
- [ ] Validation rule changed → `400`/`422` `ProblemDetails` response example refreshed to match the new rule.
- [ ] Aggregated `balsm.json` rebuilt; spec lint passes (`npx @redocly/cli lint docs/api/openapi/v1/balsm.json --max-problems 0`).
- [ ] Backward-compat check passes (`npx oasdiff breaking docs/api/openapi/v1/balsm.{previous-version}.json docs/api/openapi/v1/balsm.json`) **or** breaking changes are listed in `CHANGELOG.md` with a migration note.

### Source-of-truth ordering

The controller + DTOs are the source of truth; the spec is generated. Never hand-patch the JSON to "fix" a diff — fix the annotation that produced it. The Insomnia collection consumes the same spec, so Insomnia drift usually means the spec was updated correctly but the collection was not regenerated.

## .NET Coding Standards (Balsm-API-specific quick refs)

- Domain-specific exception types — never `Exception`/`ApplicationException`.
- Structured logging always includes `CorrelationId`, `UserId`, `Action`, `Module`; never log PHI.
- `AsNoTracking()` for all read-only queries; prevent N+1 with `Include`/`ThenInclude` or batch queries.
- Migrations are reversible (`Up()` + `Down()`) and named descriptively.
- `ConfigureAwait(false)` in library/service code.
- `Task.WhenAll()` for independent parallel work; `ValueTask<T>` when methods frequently complete synchronously.
- FluentValidation at the API layer — one validator per command/query.

---

@../Balsm-Core/agents/rules/AGENTS.md

@../Balsm-Core/agents/rules/CODING_STANDARDS.md

@CODING_STANDARDS.md

## .NET Skills Router (dotnet-skills plugin)

Install once: `/plugin marketplace add Aaronontheweb/dotnet-skills` then `/plugin install dotnet-skills`. Update: `/plugin marketplace update`.

IMPORTANT: Prefer retrieval-led reasoning over pretraining for any .NET work in this repo.
Flow: skim repo patterns → consult `dotnet-skills` by name → implement smallest-change → note conflicts with Balsm rules.

When a dotnet-skills recommendation conflicts with this `CLAUDE.md`, `Balsm-Core/agents/rules/`, or `.claude/guidelines/`, the Balsm rules win. Cite the conflict in the PR description.

Routing (invoke by skill name):

- **C# / code quality** — `modern-csharp-coding-standards`, `csharp-concurrency-patterns`, `api-design`, `type-design-performance`
- **ASP.NET Core / Web (incl. Aspire)** — `aspire-service-defaults`, `aspire-integration-testing`, `aspire-configuration`, `mailpit-integration`, `mjml-email-templates`
- **Data** — `efcore-patterns`, `database-performance`
- **DI / config** — `dependency-injection-patterns`, `microsoft-extensions-configuration`
- **Testing** — `testcontainers-integration-tests`, `playwright-blazor-testing`, `snapshot-testing`, `verify-email-snapshots`, `playwright-ci-caching`
- **.NET tooling** — `dotnet-project-structure`, `dotnet-local-tools`, `package-management`, `serialization`, `dotnet-devcert-trust`, `ilspy-decompile`, `OpenTelemetry-NET-Instrumentation`
- **Akka.NET** (only if a module adopts it) — `akka-net-best-practices`, `akka-net-testing-patterns`, `akka-hosting-actor-patterns`, `akka-net-aspire-configuration`, `akka-net-management`

Quality gates (run when applicable):

- `dotnet-slopwatch` — after substantial new/refactor/LLM-authored code.
- `crap-analysis` — after tests added/changed in complex code.

Specialist agents (invoke for deep work):

- `dotnet-concurrency-specialist` — concurrency review, lock contention, async correctness.
- `dotnet-performance-analyst` — perf regressions, allocation review.
- `dotnet-benchmark-designer` — designing BenchmarkDotNet suites.
- `akka-net-specialist` — actor system design (if adopted).
- `docfx-specialist` — generated docs.
- `roslyn-incremental-generator-specialist` — source generator work.

## dotnet/skills Router (official .NET team marketplace)

Install once:
```
/plugin marketplace add dotnet/skills
/plugin install dotnet@dotnet-agent-skills
/plugin install dotnet-data@dotnet-agent-skills
/plugin install dotnet-diag@dotnet-agent-skills
/plugin install dotnet-msbuild@dotnet-agent-skills
/plugin install dotnet-nuget@dotnet-agent-skills
/plugin install dotnet-aspnetcore@dotnet-agent-skills
/plugin install dotnet-test@dotnet-agent-skills
/plugin install dotnet-upgrade@dotnet-agent-skills
/plugin install dotnet-ai@dotnet-agent-skills
/plugin install dotnet-template-engine@dotnet-agent-skills
/plugin install dotnet-blazor@dotnet-agent-skills
/plugin install dotnet-maui@dotnet-agent-skills
/plugin install dotnet-experimental@dotnet-agent-skills
/plugin install dotnet11@dotnet-agent-skills
```
Update: `/plugin update <plugin>@dotnet-agent-skills`. Restart Claude Code after install.

Routing (invoke plugin by name; the plugin exposes its own skills):

- **Core C# / .NET** — `dotnet`
- **EF Core / data access** — `dotnet-data`
- **Perf, debug, incident analysis** — `dotnet-diag`
- **MSBuild failures, build perf, modernization** — `dotnet-msbuild`
- **NuGet, dependency mgmt** — `dotnet-nuget` (mind `Directory.Packages.props`)
- **ASP.NET Core middleware, endpoints, APIs** — `dotnet-aspnetcore`
- **Test execution, filtering, MSTest, migration** — `dotnet-test`
- **Cross-framework version upgrades** — `dotnet-upgrade` (when bumping `global.json`)
- **AI / ML / LLM integration / RAG / MCP** — `dotnet-ai` (only if a module adopts AI)
- **dotnet new templates, scaffolding** — `dotnet-template-engine`
- **Blazor** — `dotnet-blazor` (admin UI is React+Vite; skip unless that changes)
- **MAUI** — `dotnet-maui` (mobile is Flutter; do not adopt without ADR)
- **Experimental / preview features** — `dotnet-experimental`
- **.NET 11 APIs + language features** — `dotnet11` (use only after `global.json` SDK bump)

Dashboard: https://dotnet.github.io/skills/

### Precedence when routers overlap

On the **same topic**, prefer in this order:

1. **Balsm rules** (regulatory > Balsm-Core > root CLAUDE.md > `.claude/guidelines/`) — never override.
2. **`dotnet/skills`** (official .NET team) — authoritative for .NET platform guidance.
3. **`dotnet-skills`** (Aaronontheweb community plugin) — authoritative for the niches it owns (Akka.NET, Aspire, Playwright/Blazor testing, snapshot/verify, slopwatch, crap-analysis, OpenTelemetry recipes, mjml/mailpit).

If the two routers contradict each other on a single topic and Balsm has no rule, prefer `dotnet/skills`. Cite the conflict in the PR description.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
