# Balsm API — Agent Instructions

> Universal rules live in [`../Balsm-Roadmap/agents/rules/AGENTS.md`](../Balsm-Roadmap/agents/rules/AGENTS.md) and [`../Balsm-Roadmap/agents/rules/CODING_STANDARDS.md`](../Balsm-Roadmap/agents/rules/CODING_STANDARDS.md). Repo-specific architecture, commands, and composition-root details live in [`CLAUDE.md`](./CLAUDE.md). Read both before making non-trivial changes.

This file is the [agents.md](https://agents.md) entry point for tools that do not auto-discover `CLAUDE.md` (Codex CLI, Jules, OpenHands, Zed, Aider, Sourcegraph Amp, etc.). The rules below are the same ones mirrored in `.cursorrules`, `.windsurfrules`, and `.github/copilot-instructions.md` — edit the upstream `../Balsm-Roadmap/agents/rules/AGENTS.md` first, then propagate.

## Repository

Balsm API — .NET 10 modular monolith backing the Balsm healthcare platform. SDK pinned in `global.json`; `TreatWarningsAsErrors=true` everywhere; centralized package versions in `Directory.Packages.props`.

## Common Commands

```bash
dotnet restore Balsm.API.slnx
dotnet build   Balsm.API.slnx -c Release
dotnet test    Balsm.API.slnx
dotnet test    tests/Balsm.API.Tests --filter "FullyQualifiedName~Foo"
dotnet run     --project src/Balsm.API
```

EF Core migrations are per-module; always pass both `--project` (the module's `Infrastructure` project) and `--startup-project src/Balsm.API` plus `--context {Module}DbContext`. Every migration must implement both `Up()` and `Down()`. See [`CLAUDE.md`](./CLAUDE.md) for the full command reference (admin UI, tunnel registry, publish scripts, Docker).

## Architecture (quick reference)

- Modular monolith under `src/Modules/{Customer,Entity,Identity,Inventory,POS,Prescription}/`. Each module ships four projects: `Domain`, `Application`, `Infrastructure`, `Api`.
- Layer direction inside a module: `Api → Application → Domain` and `Infrastructure → Domain`. `Domain` has no module-specific outward dependencies.
- A module project must **never** reference another module's projects. Cross-module communication goes through `Balsm.SharedKernel` contracts or domain events.
- Each module owns its own `DbContext` and database schema — no shared tables.
- `Balsm.API` is the only composition root. Cross-module wiring happens there via each module's `Add{Module}Module()` / `Add{Module}Infrastructure(IConfiguration)` extension and `ModuleRegistration` marker.
- `Balsm.Supervisor` is loaded conditionally when `DeploymentMode=Standalone` (adds admin panel API, mDNS, self-update, federation auth, certificate management, HTTPS listener on `port+1`).

## Planning Phase — C4 Diagrams (mandatory)

Before any non-trivial implementation (new module, new bounded context, new cross-module flow, new external integration, or any change to `Program.cs` composition root), produce C4 diagrams **first** and confirm them with the user before writing code.

- Author diagrams in Mermaid (`C4Context`, `C4Container`, `C4Component`, `C4Dynamic`).
- Required levels by scope:
  - **New bounded context / module** — Level 1 (System Context) + Level 2 (Container) showing `Api`, `Application`, `Infrastructure`, `Domain` and the module's `DbContext`.
  - **New cross-module flow or domain event** — Level 3 (Component) for each module involved + Level 4 (Dynamic) sequencing `MediatR` → handler → repository → `DbContext`.
  - **New external integration** (HL7/FHIR, DICOM, payment, federation, mDNS, Supervisor self-update) — Level 1 + Level 4 (Dynamic) including trust boundary, auth, and failure modes.
- Save under `docs/architecture/c4/{module-or-feature}/` as `.md` files (e.g. `context.md`, `container.md`, `component-{name}.md`, `dynamic-{flow}.md`). Link them in the PR description.
- Diagrams must reflect the hard rules above (no cross-module references, layer direction, per-module DbContext, composition only in `Balsm.API`).
- Update the same diagrams in the same PR if reality diverges during implementation — stale C4 is worse than no C4.
- Skip C4 only for: typo fixes, single-method refactors inside an existing class, dependency bumps, doc-only changes.

## Testing Requirements — full coverage mandatory

Code is not "done" until it ships with **unit + integration + end-to-end** tests covering every reachable path. No PR merges without all three layers where the layer applies.

- **Unit** — every class with logic (domain entities/VOs, MediatR handlers, FluentValidation validators, domain services, mappers). No DB, no HTTP, no clock, no filesystem. Lives in `tests/Balsm.{Module}.UnitTests/`.
- **Integration** — every controller endpoint and every `Infrastructure` repository, via `WebApplicationFactory<Program>` against SQLite in-memory. Lives in `tests/Balsm.API.Tests/Controllers/{Module}/` for endpoints; `tests/Balsm.{Module}.IntegrationTests/` for repository-only flows.
- **End-to-end** — every cross-module user flow, every clinical workflow, every Standalone-mode boot path (mDNS, HTTPS admin, self-update), and every external integration (HL7/FHIR, DICOM, payment, federation). Lives in `tests/Balsm.E2E.Tests/{Flow}/`.

**Coverage gates:**
- Domain + Application: 100% line and branch.
- Infrastructure + Api: ≥ 90% line, ≥ 80% branch. `[ExcludeFromCodeCoverage]` requires a `// reason:` comment.
- Overall solution: ≥ 95% line, enforced via Coverlet thresholds in `Directory.Build.props`; CI fails below.
- Every bug fix lands with a regression test that fails before and passes after.

**Per-change checklist (PR blocked if any unchecked):** new/changed domain → unit test; new/changed handler/validator/mapper → unit test; new/changed endpoint or repository → integration test; new/changed cross-module flow, domain event, or external integration → E2E test; coverage gates pass; no `[Skip]`/`[Ignore]`/commented-out tests; tests deterministic (`IClock` abstraction, seeded `Random`, no network, no shared global state).

**Naming:** `{Sut}Tests` / `{Endpoint}EndpointTests` / `{Flow}E2ETests`; method pattern `Method_Scenario_Expected`; AAA layout separated by blank lines.

## Insomnia Collections — mandatory per API change

Every endpoint add/change/remove ships with a matching update to the module's Insomnia collection in the **same PR**. Out-of-sync collections fail CI.

- One collection per module at `docs/api/insomnia/{module}.yaml`, **Insomnia v5 file format** (`type: collection.insomnia.rest/5.0`, YAML). No v4 JSON dumps.
- Group requests into folders per controller; request name = HTTP verb + route template (e.g. `POST /api/v1/patients`).
- Environments: `local` (`http://localhost:5050`), `local-admin` (`https://localhost:5051`), `staging`. Tokens via `{{ _.balsm_token }}` from `.env.local` — never commit real tokens.
- **Per-change checklist:** new endpoint → new request with auth header, `Idempotency-Key` on writes, `X-Correlation-Id`, example request/response matching the DTOs; changed DTO → bodies + responses refreshed; renamed route → path + folder updated; deleted endpoint → request removed (no commented-out leftovers); auth/permission change → required-permission noted in the request description; pagination/filter change → query params updated; validation change → negative-case example added or refreshed.
- **Determinism:** fixed example GUIDs (`00000000-0000-0000-0000-000000000001`), fixed ISO-8601 timestamps, synthetic patient fixtures only (no PHI), stable `metaSortKey` for diff readability.
- Validate locally and in CI with `npx insomnia-inso run collection --src docs/api/insomnia/{module}.yaml --env local`; a request that does not resolve to a controller route fails the build.

## API-Specific Rules

- Respect module + layer boundaries: `Api → Application → Domain` (and `Infrastructure → Domain`); no cross-module project references.
- Every endpoint requires authentication and permission checks — no exceptions.
- Soft-delete only — never hard-delete clinical or patient data.
- Use parameterized queries — never concatenate user input into SQL.
- `Result<T>` (`Balsm.SharedKernel.Results`) for expected failures; reserve exceptions for unexpected failures.
- All I/O is async — never block with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`.
- Pass and respect `CancellationToken` through controller → handler → repository.
- Every new endpoint requires an integration test under `tests/Balsm.API.Tests/Controllers/{Module}/`.

## .NET Naming Conventions

- Commands: `Create{Entity}Command`, `Update{Entity}Command`, `Delete{Entity}Command`
- Queries: `Get{Entity}ByIdQuery`, `List{Entity}Query`, `Search{Entity}Query`
- Handlers: `{Command|Query}Handler`; Validators: `{Command|Query}Validator`
- Events: `{Entity}{Action}Event` (e.g., `AppointmentCreatedEvent`)
- Repositories: `I{Entity}Repository` / `{Entity}Repository`
- DTOs: `{Entity}Dto`, `{Entity}DetailDto`, `{Entity}ListItemDto`
- Controllers: `{Entity}Controller`, plural resource route `/api/v1/{entities}`

## .NET Coding Standards (Balsm-API-specific quick refs)

- Domain-specific exception types — never `Exception`/`ApplicationException`.
- Structured logging always includes `CorrelationId`, `UserId`, `Action`, `Module`; never log PHI.
- `AsNoTracking()` for all read-only queries; prevent N+1 with `Include`/`ThenInclude` or batch queries.
- Migrations are reversible (`Up()` + `Down()`) and named descriptively.
- `ConfigureAwait(false)` in library/service code.
- `Task.WhenAll()` for independent parallel work; `ValueTask<T>` when methods frequently complete synchronously.
- FluentValidation at the API layer — one validator per command/query.

## Project Slash Commands (`.claude/commands/`)

Repo ships Balsm-specific Claude Code commands. Equivalent prompts work for other agents:

- `/security-review`, `/phi-scan`, `/audit-trail-check` — healthcare safety gates on changed files.
- `/api-review`, `/ddd-review`, `/naming-check`, `/performance-review` — standards conformance on the diff.
- `/module-scaffold` — generate a new bounded-context module skeleton per AGENTS.md.
- `/test-scaffold` — generate integration test boilerplate for a new endpoint/command/query.

Mirror rules live in `.cursor/rules/`, `.windsurf/rules/`, and `.github/copilot-instructions.md`. The upstream source of truth is [`../Balsm-Roadmap/agents/rules/AGENTS.md`](../Balsm-Roadmap/agents/rules/AGENTS.md); never let the mirrors drift.
