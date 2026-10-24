# Balsm-API-DotNet — Coding Standards

> Extends the shared standards in
> [Balsm-Core/agents/rules/CODING_STANDARDS.md](../Balsm-Core/agents/rules/CODING_STANDARDS.md).
> Shared principles (error handling, SOLID, logging, validation, DTOs,
> idempotency, caching) apply as written there; this file adds the .NET
> specifics.

---

## 1. Naming Conventions

- commands: `Create{Entity}Command`, `Update{Entity}Command`, `Delete{Entity}Command`
- queries: `Get{Entity}ByIdQuery`, `List{Entity}Query`, `Search{Entity}Query`
- events: `{Entity}{Action}Event` (e.g., `AppointmentCreatedEvent`, `PrescriptionDispensedEvent`)
- handlers: `{Command|Query}Handler` (e.g., `CreateAppointmentCommandHandler`)
- validators: `{Command|Query}Validator` (e.g., `CreateAppointmentCommandValidator`)
- repositories: `I{Entity}Repository` (interface), `{Entity}Repository` (implementation)
- services: `I{Domain}Service` (interface), `{Domain}Service` (implementation)
- DTOs: `{Entity}Dto`, `{Entity}DetailDto`, `{Entity}ListItemDto`
- API controllers: `{Entity}Controller` (plural resource in route: `/api/v1/appointments`)

## 2. Project Structure

- every bounded context is a separate class-library project — never combine contexts
- `src/Modules/{ContextName}/` with:
  - `Balsm.{ContextName}.Domain/` — entities, value objects, domain events, repository interfaces
  - `Balsm.{ContextName}.Application/` — commands, queries, handlers, validators, DTOs
  - `Balsm.{ContextName}.Infrastructure/` — repository implementations, EF configurations, external clients
  - `Balsm.{ContextName}.Api/` — controllers, middleware, filters
- shared kernel: `Balsm.SharedKernel`
- modules never reference other module packages directly — shared contracts or domain events only

## 3. Validation & DTO Tech Notes

- FluentValidation for API-layer validation — one validator class per command/query
- DTOs are C# `record` types (immutability per shared §5)
- map explicitly and consistently (mapping extensions / Mapster / AutoMapper profiles)

## 4. Database Patterns (EF Core)

- always use projections — never `SELECT *` or full entities for reads
- prevent N+1 — eager loading (`Include`/`ThenInclude`) or batch queries; verify with query logging
- paginate at the database level; cursor-based pagination for large/deep datasets
- every migration reversible (`Up()` + `Down()`); names: `Add{Feature}_{Entity}Table`, `Add{Column}To{Entity}`, `Create{Index}On{Entity}`
- indexes on every column in `WHERE`/`JOIN`/`ORDER BY` for tables >10K rows; composite indexes for multi-column filters
- `AsNoTracking()` for all read-only queries
- raw SQL only when the ORM cannot express the query — always parameterized
- `EXISTS` over `COUNT > 0`; no leading-wildcard `LIKE` on large tables (full-text instead)
- batch operations >100 records; monitor and alert on slow queries (>100ms)
- compiled queries (`EF.CompileAsyncQuery`) for hot paths

## 5. Async Patterns

- all I/O async — never block with `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`
- pass and respect `CancellationToken` end to end (controller → repository)
- `ValueTask<T>` for frequently-synchronous completions (cache hits)
- never `async void`
- `ConfigureAwait(false)` in library/service code
- `Task.WhenAll()` for independent parallel operations
- timeouts on all external HTTP calls

## 6. API Performance

- targets: <200ms simple reads, <500ms complex reads, <1s writes
- response compression (gzip/brotli); HTTP caching headers (`ETag`, `Last-Modified`, `Cache-Control`) on reads
- minimal payloads (sparse fieldsets / purpose-specific DTOs); `204 No Content` where no body is needed
- request deduplication for identical concurrent requests

## 7. Memory & Resource Management

- `IAsyncEnumerable<T>` for streaming large result sets — never materialize whole collections
- `ArrayPool<T>`/`MemoryPool<T>` for hot-path buffers; `Span<T>`/`Memory<T>` for high-throughput manipulation
- dispose everything disposable (`using`/`await using`)
- stream file uploads/downloads; cap request body sizes
- low/zero-allocation patterns in hot paths

## 8. Concurrency & Throughput

- connection pooling with tuned pool sizes
- circuit breakers on external calls; rate limiting on public endpoints
- background processing (queues/hosted services) for non-synchronous work (reports, email, audit persistence)
- fine-grained locks scoped to the protected resource — no global locks
