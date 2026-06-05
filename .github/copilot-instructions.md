# Balsm API — Repo-Specific Instructions

> Universal rules are in the org-level copilot-instructions.md (balsm-io/.github).
> When working locally, workspace settings load Balsm-Core/agents/rules/AGENTS.md directly.

## API-Specific Rules

- .NET modular monolith — respect module and layer boundaries: API → Business → Data
- All endpoints require authentication and permission checks — no exceptions
- Use parameterized queries — never concatenate user input into SQL
- Soft-delete only — never hard-delete clinical or patient data
- Every new endpoint requires an integration test
- Use `Result<T>` pattern for expected failures — do not throw exceptions for business rule violations
- All I/O must be async — never block with `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`
- Pass and respect `CancellationToken` through controller → handler → repository
