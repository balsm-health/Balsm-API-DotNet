# Test Scaffold

Generate test boilerplate for a new API endpoint, command, query, or domain entity.

## Instructions

1. Determine from $ARGUMENTS (or ask the user) what to generate tests for:
   - A new API endpoint / command / query
   - A domain entity or service
   - A repository

2. Read the source file(s) to understand the code under test.

3. Generate test files following Balsm testing standards:

### Unit Tests (Domain / Business Logic)
```
tests/Modules/{ContextName}/Balsm.{ContextName}.UnitTests/
  {Feature}/{Entity}Tests.cs
  {Feature}/{Command}HandlerTests.cs
  {Feature}/{Query}HandlerTests.cs
  {Feature}/{Entity}ValidatorTests.cs
```
- Test domain logic, validation rules, business invariants
- Use in-memory or test database — do not mock the database
- Use synthetic data — never real patient data
- Test sad paths: validation failures, business rule violations, concurrency conflicts
- Target 80% coverage for domain logic

### Integration Tests (API Endpoints)
```
tests/Modules/{ContextName}/Balsm.{ContextName}.IntegrationTests/
  {Feature}/{Entity}EndpointTests.cs
```
- Test full request → response cycle through the API
- Verify HTTP status codes, response structure, and error formats
- Test authentication and permission enforcement
- Test pagination, filtering, and sorting on list endpoints
- Test idempotency (same request twice produces same result)
- Verify audit trail entries are created for clinical operations
- Pass `CancellationToken` in all async test calls

### Test Naming Convention
- `{MethodName}_Should{ExpectedBehavior}_When{Condition}`
- Example: `CreateAppointment_ShouldReturn201_WhenValidRequest`
- Example: `CreateAppointment_ShouldReturn400_WhenPatientIdMissing`

### Test Data
- Use the `TestDataBuilder` or `ObjectMother` pattern for test entities
- Never use real patient names, IDs, or clinical data — use fake generators
- Seed data via `WebApplicationFactory` or `DbContext` seeding in `TestBase`

4. After generating tests, confirm:
   - All happy paths are covered
   - All validation failure paths are covered
   - Authentication/authorization enforcement is tested
   - The test project compiles without errors
