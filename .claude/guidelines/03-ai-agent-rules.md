# 03 — AI Agent Operating Rules (this repo)

Rules for Claude Code, Cursor, Copilot, Windsurf, Cline, Codex, or any other coding agent working inside `Balsm-API-DotNet/`. Extends `Balsm-Core/agents/rules/AGENTS.md` §1 (General Behavior). Read that first.

---

## 1. Graphify-First (mandatory)

This repo has a knowledge graph at `graphify-out/`. Hooks enforce it.

- Before reading source files for **codebase questions**, run:
  - `graphify query "<question>"` — scoped subgraph, usually a small fraction of raw grep.
  - `graphify path "<A>" "<B>"` — relationship between two concepts.
  - `graphify explain "<concept>"` — focused concept summary.
- Only fall back to raw `Read`/`grep` after graphify has oriented you, **or** when you are about to modify/debug specific lines.
- `graphify-out/wiki/index.md` (when present) is the navigation entry point for broad questions.
- `graphify-out/GRAPH_REPORT.md` is the architecture overview — read only when query/path/explain are not enough.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- **Pass this rule down to subagents** — include "graphify before reading" in every subagent prompt that touches code.

## 2. Plan-First for Non-Trivial Changes (C4 mandatory)

Per root `CLAUDE.md`, before any:

- New module / bounded context,
- New cross-module flow or domain event,
- New external integration (HL7/FHIR, DICOM, payment, federation, mDNS, Supervisor self-update),
- Any change to `Program.cs` composition root,

… produce **C4 diagrams first** in Mermaid (`C4Context`, `C4Container`, `C4Component`, `C4Dynamic`), save them under `docs/architecture/c4/{module-or-feature}/`, link them from the PR description, and **confirm with the user before writing code**.

Diagrams must reflect:

- No cross-module project references.
- Layer direction `Api → Application → Domain`, `Infrastructure → Domain`.
- Per-module DbContext.
- Composition wiring only in `Balsm.API`.

If reality diverges during implementation, update the diagrams in the **same PR**. Stale C4 is worse than no C4.

Skip C4 only for: typo fixes, single-method refactors inside an existing class, dependency bumps, doc-only changes.

## 3. Tests Are Not Optional

Per root `CLAUDE.md` (Testing Requirements):

- Domain + Application layers: **100% line and branch coverage**.
- Infrastructure + Api: ≥ 90% line, ≥ 80% branch.
- Solution overall: ≥ 95% line.
- Every bug fix lands with a regression test that fails before and passes after.
- No `[Skip]`, `[Ignore]`, commented-out tests, real clock, real network, or shared global state.

Enforcement: `dotnet test --collect:"XPlat Code Coverage"` with Coverlet thresholds in `Directory.Build.props`. CI fails below.

An agent that ships code without all applicable test layers (unit + integration + E2E where relevant) has produced an incomplete change.

## 4. Commit Policy

- Commit **after every task** — do not batch unrelated tasks.
- One logical change per commit. No mixed formatting + functional commits.
- Conventional Commits style, present tense.
- **Never** `--force`, `--no-verify`, `--no-gpg-sign`, or amend a published commit. If a pre-commit hook fails, fix the underlying issue and create a **new** commit.
- Branches only — never commit to `main` directly.

## 5. Required Skills for Sensitive Changes

When the change touches the listed area, run the matching `Balsm-AI` skill before opening the PR. Mark which skill ran in the PR description.

| Area touched | Required skill(s) |
|---|---|
| Auth, tokens, sessions | `balsm-ai:testing-jwt-token-security`, `balsm-ai:implementing-api-key-security-controls` |
| API rate-limiting, throttling | `balsm-ai:implementing-api-rate-limiting-and-throttling` |
| Secrets, env, config | `balsm-ai:implementing-secrets-scanning-in-ci-cd`, `balsm-ai:implementing-secrets-management-with-vault` |
| SQL paths, dynamic queries | `balsm-ai:exploiting-sql-injection-vulnerabilities` (read-only as a defensive checklist) |
| File upload, deserialization | `balsm-ai:exploiting-insecure-deserialization`, `balsm-ai:testing-for-xxe-injection-vulnerabilities` |
| Federation, tunnel, external HTTP | `balsm-ai:exploiting-server-side-request-forgery`, `balsm-ai:exploiting-http-request-smuggling` |
| Permission checks, RBAC paths | `balsm-ai:exploiting-broken-function-level-authorization`, `balsm-ai:exploiting-idor-vulnerabilities` |
| Any clinical / PHI surface | `phi-scan` (project command), plus a healthcare-threat-model pass (`Balsm-Core/.claude/skills/healthcare-threat-modeling`) |

Project commands in `.claude/commands/` (`api-review`, `audit-trail-check`, `ddd-review`, `module-scaffold`, `naming-check`, `performance-review`, `phi-scan`, `security-review`, `test-scaffold`) are the **default** review pipeline for any non-trivial change. Run the ones that apply.

### 5b. dotnet-skills (Aaronontheweb/dotnet-skills plugin)

Installed via `/plugin install dotnet-skills`. Routing table per change area:

| Area touched | dotnet-skills to consult |
|---|---|
| New module / handler / DTO | `modern-csharp-coding-standards`, `api-design`, `type-design-performance` |
| Concurrency, parallel, threads | `csharp-concurrency-patterns` + agent `dotnet-concurrency-specialist` |
| EF Core query, migration, schema | `efcore-patterns`, `database-performance` |
| DI registration, options pattern | `dependency-injection-patterns`, `microsoft-extensions-configuration` |
| Integration tests | `testcontainers-integration-tests`, `aspire-integration-testing` |
| Snapshot / verify tests | `snapshot-testing`, `verify-email-snapshots` |
| Project layout / packaging | `dotnet-project-structure`, `package-management`, `serialization` |
| Observability / tracing | `OpenTelemetry-NET-Instrumentation` |
| Local dev cert (Standalone TLS) | `dotnet-devcert-trust` |
| Post-refactor or LLM-authored code | `dotnet-slopwatch` (quality gate) |
| Post-test-change in complex code | `crap-analysis` (quality gate) |
| Perf regression suspected | agent `dotnet-performance-analyst`, then `dotnet-benchmark-designer` for benchmarks |
| Source generator work | agent `roslyn-incremental-generator-specialist` |

**Precedence:** dotnet-skills is **advisory**. Where it conflicts with this repo's `CLAUDE.md`, `Balsm-Core/agents/rules/`, or these guidelines, Balsm rules win. Cite the conflict in the PR description.

**Akka.NET skills** (`akka-net-*`) are present in the plugin but not currently used by this repo — do not adopt without an ADR.

### 5c. dotnet/skills (official .NET team marketplace)

Installed via `/plugin marketplace add dotnet/skills` + per-plugin `/plugin install <plugin>@dotnet-agent-skills`. Routing per change area:

| Area touched | dotnet/skills plugin to consult |
|---|---|
| Core C# / .NET runtime | `dotnet` |
| EF Core query, migration, schema, repository | `dotnet-data` (cross-check with Aaronontheweb `efcore-patterns`; prefer official on conflict) |
| Perf regression, debug, incident triage | `dotnet-diag` |
| Build failure, MSBuild perf, modernization | `dotnet-msbuild` |
| NuGet packages, `Directory.Packages.props`, vuln scan | `dotnet-nuget` |
| Controllers, middleware, minimal APIs, endpoints | `dotnet-aspnetcore` (cross-check with Aaronontheweb `aspire-*` for Aspire specifics) |
| Test runs, filters, MSTest, migration | `dotnet-test` (cross-check with Aaronontheweb `testcontainers-*` / `snapshot-testing` for those niches) |
| `global.json` SDK bump, language version upgrade, framework migration | `dotnet-upgrade` |
| AI / ML / LLM / RAG / MCP in .NET | `dotnet-ai` (only if a module adopts AI; gate on `Balsm-Core/AI_GOVERNANCE.md`) |
| `dotnet new` template, scaffolding | `dotnet-template-engine` (do **not** use to bypass `01-api-dev-rules.md` module layout) |
| Blazor component work | `dotnet-blazor` (skip — admin UI is React+Vite; flag if migration considered) |
| MAUI | `dotnet-maui` (skip — mobile is Flutter; do not adopt without ADR) |
| Preview / experimental features | `dotnet-experimental` (gate on ADR before adoption) |
| .NET 11 APIs / language features | `dotnet11` (only after `global.json` SDK bump) |

**Router precedence on overlapping topics:**

1. Balsm rules (regulatory > Balsm-Core > root `CLAUDE.md` > these guidelines) — never override.
2. `dotnet/skills` (official) — authoritative for .NET platform guidance.
3. `dotnet-skills` (Aaronontheweb community) — authoritative for its niches: Akka.NET, Aspire, Playwright/Blazor testing, snapshot/verify, slopwatch, crap-analysis, OpenTelemetry recipes, mjml/mailpit.

If the two routers contradict on a single topic and Balsm has no rule, prefer `dotnet/skills`. Cite the conflict in the PR description.

**Plugins skipped by default:** `dotnet-maui`, `dotnet-blazor` — not used. `dotnet11` — until `global.json` SDK bump. Adopting any of these requires an ADR.

## 6. Action Scope & Reversibility

- Local, reversible actions (edits, tests, local runs) — proceed.
- Destructive or shared-state actions (`rm -rf`, `git reset --hard`, force-push, `git branch -D`, package downgrade, CI/CD edits, sending messages, posting PR comments, modifying shared infra) — **confirm with the user first** unless the user has explicitly pre-authorized in this session.
- Authorization is **scoped**: a one-time approval doesn't carry forward.
- Never bypass a failing hook or check with `--no-verify` style flags. Fix the root cause.
- Never upload PHI or repo contents to third-party renderers / pastebins / external AI services.

## 7. Documentation Updates Travel With Code

A user request that adds, modifies, or removes a business requirement, feature, or behavioral rule must update **all affected docs in the same PR**:

- `Balsm-Core/BUSINESS_FEATURES.md` for feature specs,
- `Balsm-Core/PHASED_DELIVERY_STEPS.md` for delivery tasks,
- `Balsm-Core/GLOSSARY.md` for new/changed domain terms,
- `Balsm-Core/NON_FUNCTIONAL_REQUIREMENTS.md` for NFR shifts,
- `Balsm-Core/CERTIFICATIONS.md` for standards/certification compliance,
- `Balsm-Core/agents/rules/AGENTS.md` + `CODING_STANDARDS.md` for agent behavior or coding-convention changes,
- This repo's `.claude/` (commands, skills, guidelines) for tooling changes,
- This repo's `docs/api/openapi/`, `docs/api/insomnia/`, `docs/architecture/c4/` for API or architecture changes.

Code without doc updates is incomplete.

## 8. What Agents Must Not Do

- Do not invent new modules, layers, naming conventions, or abstractions. Follow what exists.
- Do not add comments, docstrings, or type annotations to code you did not change.
- Do not "improve" surrounding code while fixing a bug.
- Do not add error handling, validation, or fallbacks for scenarios that cannot happen.
- Do not leave `TODO`s without a linked issue.
- Do not paste real patient data, production secrets, or live tokens into prompts, code, fixtures, or commit messages. **All AI conversations are potentially logged.**
- Do not weaken security controls to fix a bug or simplify code.
- Do not bypass permission checks "for tests" — tests run with explicit test identities.

## 9. End-of-Task Hand-Off

When finishing a change, leave the next agent (or yourself) a clean state:

- `dotnet build Balsm.API.slnx -c Release` passes.
- `dotnet test Balsm.API.slnx` passes with coverage thresholds met.
- `graphify update .` ran if code changed.
- OpenAPI + Insomnia regenerated/updated if endpoints changed.
- C4 diagrams updated if architecture changed.
- PR description lists: scope, consumer impact (Flutter / website), test layers run, skills run, breaking-change flag.
