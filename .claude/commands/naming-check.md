# Naming Check

Verify naming conventions across changed files per Balsm .NET API coding standards.

## Instructions

1. Run `git diff HEAD` (or `git diff --cached`) to identify all changed files.
2. Read each changed file and verify naming conventions:

### .NET Naming Conventions

| Pattern | Convention | Example |
|---------|-----------|---------|
| Commands | `Create{Entity}Command`, `Update{Entity}Command`, `Delete{Entity}Command` | `CreateAppointmentCommand` |
| Queries | `Get{Entity}ByIdQuery`, `List{Entity}Query`, `Search{Entity}Query` | `ListAppointmentsQuery` |
| Events | `{Entity}{Action}Event` | `AppointmentCreatedEvent`, `PrescriptionDispensedEvent` |
| Handlers | `{Command\|Query}Handler` | `CreateAppointmentCommandHandler` |
| Validators | `{Command\|Query}Validator` | `CreateAppointmentCommandValidator` |
| Repositories | `I{Entity}Repository` / `{Entity}Repository` | `IAppointmentRepository` |
| Services | `I{Domain}Service` / `{Domain}Service` | `ISchedulingService` |
| DTOs | `{Entity}Dto`, `{Entity}DetailDto`, `{Entity}ListItemDto` | `AppointmentDetailDto` |
| Controllers | `{Entity}Controller` (plural route) | `AppointmentsController` → `/api/v1/appointments` |
| Exceptions | `{Context}{Problem}Exception` | `AppointmentConflictException` |
| Migrations | Descriptive name | `AddStatusToAppointments`, `CreateLabOrderTable` |

### Ubiquitous Language
- "appointment" not "reservation" or "booking"
- "admission" not "internal reservation"
- "dispensation" not "pharmacy fulfillment"
- "specimen" not "sample"
- "entity" not "organization" or "facility"
- Terms must be consistent across code, API endpoints, database schemas, and documentation

### General
- File names match class names (one public class per file)
- Files under 300 lines (single responsibility)
- No abbreviations in public identifiers (except well-known: `Id`, `Dto`, `Api`)
- `async` methods end with `Async` suffix (e.g., `GetByIdAsync`)

3. For each violation, report:
   - File path and line number
   - Current name and what it should be
   - Convention violated

4. If no issues are found, confirm naming conventions are followed.
