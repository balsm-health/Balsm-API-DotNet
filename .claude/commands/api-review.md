# API Review

Review all changed or staged API endpoint code for compliance with Balsm API conventions and coding standards.

## Instructions

1. Run `git diff HEAD` (or `git diff --cached`) to identify all changed files.
2. Read each changed file that touches API controllers, commands, queries, DTOs, or validators.
3. Analyze for the following:

### REST Conventions
- Resource naming follows plural convention (`/api/v1/appointments`, not `/api/v1/appointment`)
- URI-based versioning: `/api/v1/` prefix present
- Correct HTTP methods: GET for reads, POST for creates, PUT/PATCH for updates, DELETE for deletes
- Correct HTTP status codes: 200 OK, 201 Created, 204 No Content, 400 Bad Request, 401 Unauthorized, 403 Forbidden, 404 Not Found, 409 Conflict, 422 Unprocessable Entity
- Consistent error response structure across all endpoints

### Pagination, Filtering & Sorting
- All list endpoints support pagination (must never return unbounded results)
- Filtering parameters available for relevant fields
- Sorting supported with sensible defaults
- Cursor-based pagination for large datasets (offset pagination degrades on deep pages)

### Input Validation
- FluentValidation validator class exists for each command/query
- API-layer validates: data types, required fields, string lengths, format constraints
- Domain-layer validates: business rules, state transitions, invariants
- All validation errors returned at once (not fail-on-first)
- Validation error responses include: field name, error code, human-readable message

### DTO & Mapping
- Domain entities never exposed directly in API responses (must use DTOs)
- Domain entities never accepted as API input (must use command/request DTOs)
- Purpose-specific DTOs exist: `{Entity}DetailDto`, `{Entity}ListItemDto`, `Create{Entity}Request`, `Update{Entity}Request`
- DTOs are immutable (`record` types in C#)
- No domain logic in DTOs or mapping code

### Idempotency
- Create operations support `Idempotency-Key` header
- Update/delete operations don't fail if state is already as requested
- Payment and billing operations are strictly idempotent
- Prescription dispensation is idempotent (no double-dispense)

### Security
- Authentication required on every endpoint — no exceptions
- Permission check present before execution
- Parameterized queries only (no string concatenation with user input)
- `CancellationToken` accepted and propagated from controller → handler → repository

### Documentation
- API client collection in `/docs/api/` updated for new/changed endpoints

4. For each issue found, report:
   - File path and line number
   - Category and severity
   - Description
   - Suggested fix

5. If no issues are found, confirm the code passes API review.
