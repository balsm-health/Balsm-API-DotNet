# Performance Review

Review all changed or staged files for performance issues per Balsm .NET API coding standards.

## Instructions

1. Run `git diff HEAD` (or `git diff --cached`) to identify all changed files.
2. Read each changed file and analyze for the following performance issues:

### Database Performance (EF Core)
- `SELECT *` or loading full entities when only a subset is needed (must use projections)
- N+1 queries: related data loaded in a loop instead of eager loading (`Include`/`ThenInclude`) or batch queries
- Missing `AsNoTracking()` on read-only queries
- In-memory pagination: `.ToList()` followed by `.Skip()`/`.Take()` (must paginate at DB level)
- Missing indexes on columns used in WHERE, JOIN, or ORDER BY on tables with >10K rows
- `COUNT > 0` used instead of `EXISTS` for existence checks
- Leading wildcard `LIKE '%term%'` on large tables (use full-text search)
- Missing compiled queries (`EF.CompileAsyncQuery`) on hot-path queries
- Bulk operations (>100 records) not using batch methods

### Async Patterns
- Blocking async: `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` on async code
- Missing `CancellationToken` propagation from controller to repository
- `async void` methods (must return `Task` or `Task<T>`)
- Missing `ConfigureAwait(false)` in library/service code
- Sequential awaits on independent operations (should use `Task.WhenAll()`)
- Missing timeouts on external HTTP calls

### API Performance
- Missing pagination, filtering, or sorting on list endpoints
- Missing HTTP caching headers (ETag, Last-Modified, Cache-Control) on read endpoints
- Returning fields the client doesn't need (use purpose-specific DTOs)
- Missing response compression
- Offset pagination on large datasets (should use cursor-based)

### Caching
- Patient records, prescriptions, real-time inventory, or appointment availability cached (must never cache PHI)
- Cached items without explicit TTL
- Cache not invalidated on write operations
- Cache keys missing tenant ID (cross-tenant data leakage risk)

### Memory & Resources
- Full file buffered in memory instead of streaming
- Missing `using`/`await using` on disposable resources (`DbContext`, `HttpClient`, streams)
- Large collections materialized in memory (use `IAsyncEnumerable<T>` for streaming)
- Missing request body size limits

### Concurrency
- Missing connection pooling configuration
- Missing circuit breaker on external service calls
- Missing rate limiting on public endpoints
- Global locks instead of fine-grained resource-scoped locks

3. For each issue found, report:
   - File path and line number
   - Severity: HIGH / MEDIUM / LOW
   - Description of the issue
   - Suggested fix

4. If no issues are found, confirm the code passes performance review.
