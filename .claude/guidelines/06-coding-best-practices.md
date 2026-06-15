# 06 — Coding Best Practices (delta from Balsm-Core)

This file is the **delta** for `Balsm-API-DotNet/` on top of `Balsm-Core/agents/rules/CODING_STANDARDS.md`. Read Core first; this file adds repo-specific concretizations and reinforces the rules most easily broken.

---

## 1. Result<T> Is the Failure Contract

- `Balsm.SharedKernel.Results.Result<T>` is the **only** way to express expected failures: validation, not-found, business-rule violation, conflict.
- Throwing exceptions for those cases is a bug. Reserve exceptions for truly unexpected failures (network down, DB unavailable, null reference where it cannot happen).
- Controllers translate `Result<T>` → HTTP:
  - `Success` → 200/201/204.
  - `Validation` → 400 `ProblemDetails`.
  - `NotFound` → 404 `ProblemDetails`.
  - `Conflict` → 409 `ProblemDetails`.
  - `Forbidden` (rule, not auth) → 403 `ProblemDetails`.
- Never leak internal exception messages or stack traces to clients. `ExceptionHandlingMiddleware` produces a sanitized `ProblemDetails` with `CorrelationId`.

## 2. Exception Types

- Use domain-specific exceptions, organized per module: `AppointmentConflictException`, `PrescriptionValidationException`, etc. **Never** `Exception`, `ApplicationException`, or `SystemException`.
- One `catch (Exception)` is allowed: in `ExceptionHandlingMiddleware` and in `BackgroundService` outer loops. Both re-throw or log structurally before swallowing.
- `try`/`catch` blocks must **never** be empty or log-only without a reason comment.

## 3. Async / Cancellation

- All public methods that do I/O are async. No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` anywhere in production code. Tests may use `.Result` only on already-completed tasks for assertion ergonomics.
- `CancellationToken` flows controller → handler → repository → driver. A method that drops the token is a bug.
- Library/service code (anything in `SharedKernel` or `Infrastructure`) uses `ConfigureAwait(false)`. Top-level controller actions do not need it.
- Parallel independent work: `Task.WhenAll`. Sequential awaits of independent tasks is a bug.
- `ValueTask<T>` for hot paths that often complete synchronously (cache hits, simple lookups). Not as a default.
- `async void` is banned everywhere except true event handlers.

## 4. Structured Logging

Every log call is structured. No string interpolation in messages. Required properties on every log line:

```csharp
_logger.LogInformation(
    "Appointment created. {CorrelationId} {UserId} {Module} {Action} {AppointmentId}",
    correlationId, userId, "Appointment", "Create", appointment.Id);
```

- Levels:
  - `Debug` — detailed diagnostic, off in production.
  - `Information` — significant business events.
  - `Warning` — recoverable issues, fallbacks, deprecated API hit.
  - `Error` — failures needing attention.
  - `Critical` — system-level (DB unreachable, host shutting down).
- **Never** log PHI, tokens, passwords, full request/response bodies. Log IDs and operation names. If a payload **must** be logged for debug, redact PHI fields and include a `// reason:` comment.
- Method entry/exit logs for clinical, financial, federation, and sync paths.
- Include `DurationMs` for any operation that can be slow.

## 5. EF Core / Database

- `AsNoTracking()` on every read-only query. Tracking is opt-in.
- Projections (`Select(... new XDto { ... })`) for read endpoints. Never load full entities then map in memory for reads.
- `Include`/`ThenInclude` only where the projection cannot express the join — projections are preferred.
- Pagination at the DB. Cursor pagination for lists expected > 10k rows; offset for small lists.
- Compiled queries (`EF.CompileAsyncQuery`) for hot-path queries (auth lookup, permission resolution, federation handshake).
- Existence checks: `AnyAsync(...)`, never `CountAsync(...) > 0`.
- Leading-wildcard `LIKE` is banned on large tables. Use FTS where needed.
- Batched inserts/updates for > 100 rows. Per-row `SaveChanges` in a loop is a bug.
- Raw SQL only when EF cannot express the query, and only inside `Infrastructure`. Always parameterized.

## 6. Idempotency (write endpoints)

- Every write endpoint accepts `Idempotency-Key` (client-generated GUID). The pipeline:
  1. Read key from header. If absent → 400.
  2. Look up an `IdempotencyRecord` by `(Module, Action, Key)`.
  3. If found + finalized → return the stored response.
  4. If found + in-flight → return 409 `idempotency_conflict`.
  5. If absent → create record, execute, persist response, mark finalized.
- Payment, billing, prescription dispensation, and federation sync handshakes are **always** idempotent. A duplicate is a bug in the caller; the server must absorb it cleanly.
- Domain events are published with an event id; consumers dedupe by id.

## 7. Validation

Two layers, both required:

- **API layer** — FluentValidation, one validator per command/query, lives in `Application/Validators/`. Validates shape, type, length, format, presence.
- **Domain layer** — invariants inside entity constructors and methods. Validates business rules, state transitions, cross-field invariants.

Validators return **all** errors at once, not first-error. Error payload includes: `field`, `code` (machine-readable), `message`.

Client-side validation is convenience; server **always** re-validates.

## 8. DTOs + Mapping

Three distinct models per entity:

1. **Domain model** — behavior, invariants. Lives in `Domain`. Never serialized.
2. **Persistence model** — EF entity. Lives in `Infrastructure` (or a dual-purpose Domain model when simple). Never returned from a controller.
3. **DTO** — API contract. `record` types, immutable. Lives in `Application` or `Api`.

- Map explicitly. Mapster is the default for this repo; manual mapping extensions are acceptable for simple cases. Do not introduce a second mapping library without an ADR.
- DTOs have **no** behavior, no domain logic, no validation attributes beyond serialization shape.
- Purpose-specific DTOs: `{Entity}DetailDto`, `{Entity}ListItemDto`, `Create{Entity}Request`, `Update{Entity}Request`.

## 9. SOLID — applied here

- **SRP** — if a class name needs "And" or "Manager", split it.
- **OCP** — extend via new MediatR handlers / new strategies. Do not modify existing working handlers.
- **LSP** — derived types must be substitutable. No `throw new NotImplementedException` to satisfy a base contract.
- **ISP** — `IReadRepository<T>` + `IWriteRepository<T>` are separate when only reads are needed. Don't force a query class to take a write port.
- **DIP** — constructor injection only. **No** `new SomeService()` in handlers, controllers, or domain services. Service locator is banned.

## 10. Code Organization

- File layout per class:
  1. Constants + static fields
  2. Private fields
  3. Constructor(s)
  4. Public properties
  5. Public methods
  6. Private methods
- Files cap at **300 lines**. Over → split. Split by responsibility, not by line count.
- **One public type per file**, file name matches the type.
- `internal` is the default visibility. `public` only when the type leaves the assembly. Domain types are `internal sealed` by default; `Application` exposes commands/queries/DTOs as `public sealed record`.

## 11. Naming (recap, this repo)

- Commands: `Create{Entity}Command`, `Update{Entity}Command`, `Delete{Entity}Command`.
- Queries: `Get{Entity}ByIdQuery`, `List{Entity}Query`, `Search{Entity}Query`.
- Handlers: `{Command|Query}Handler`.
- Validators: `{Command|Query}Validator`.
- Domain events: `{Entity}{Action}Event`.
- Repositories: `I{Entity}Repository` / `{Entity}Repository`.
- DTOs: `{Entity}Dto`, `{Entity}DetailDto`, `{Entity}ListItemDto`.
- Controllers: `{Entity}Controller`, route `/api/v1/{entities}` (plural, kebab-case for multi-word).

## 12. Testing — short form

Full rules in root `CLAUDE.md`. The discipline points worth repeating:

- AAA layout, blank-line separated, no `// Arrange` comments.
- One logical assertion per test.
- Test names: `Method_Scenario_Expected` (e.g. `Handle_WhenPatientNotFound_ReturnsNotFoundError`).
- Mirror SUT names: `{Sut}Tests`, `{Endpoint}EndpointTests`, `{Flow}E2ETests`.
- Deterministic: `IClock` abstraction, seeded `Random`, no real network, no shared state.
- Integration tests use `WebApplicationFactory<Program>` + SQLite in-memory (matches default provider).

## 13. Performance Defaults

- Response compression (gzip/brotli) on all responses > 1 KB.
- HTTP caching (`ETag`, `Last-Modified`, `Cache-Control`) on read endpoints where the resource has a clear modification timestamp.
- `IAsyncEnumerable<T>` for streaming large result sets — never materialize the entire collection into memory for export endpoints.
- `ArrayPool<T>` / `MemoryPool<T>` for temporary buffers in hot paths.
- `Span<T>` / `Memory<T>` for byte/string manipulation in parsers, formatters, federation framing.
- File uploads / downloads: streamed. Never buffer a full file in memory. Max body size limits enforced before processing.
- Slow-query monitoring: log every query > 100 ms with the SQL + parameters (parameters redacted for PHI fields).

## 14. Public API Surface — boring is good

- No new abstractions for one-time uses.
- No new packages without ADR + an explicit "why not the existing tool".
- No reflection, dynamic codegen, or expression trees in hot paths. Use source generators if needed.
- No `dynamic`. Ever.
- No `unsafe` without a security review.

## 15. C# Language Use

- Target the language version pinned in `Directory.Build.props`. Don't bump it casually.
- `record` for immutable data shapes (DTOs, events). `class` for mutable / behavioral types.
- `sealed` by default on concrete types unless explicitly intended for inheritance.
- `nullable` reference types **on** everywhere. A `?` annotation is a contract; do not silence it with `!` to make a warning go away.
- Pattern matching for control flow when it's clearer than `if`/`else`. Don't over-pattern.

## 16. Forbidden Patterns

These will be flagged in review:

- `catch (Exception ex) { /* nothing */ }` — silent swallow.
- `catch (Exception ex) { _logger.Log(ex); throw; }` without `// reason:` why the rethrow is necessary at this layer.
- `Thread.Sleep` in production code (use `Task.Delay`).
- `Task.Run` to wrap CPU-bound work inside a request — use it only at the orchestration layer with an explicit reason.
- `async void` outside event handlers.
- `Guid.NewGuid()` in an OpenAPI / Insomnia example.
- `DateTime.Now` / `DateTimeOffset.Now` — use `IClock`.
- New direct uses of `HttpClient.SendAsync` — use the configured `HttpClientFactory` named client.
