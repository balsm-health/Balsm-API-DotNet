# Module Scaffold

Scaffold a new bounded context module with the correct folder structure for the Balsm .NET API.

## Instructions

1. Determine from $ARGUMENTS (or ask the user):
   - Module name (bounded context name, e.g., "Labs", "Radiology", "Pharmacy")

2. Verify the module name aligns with one of the 13 bounded contexts defined in AGENTS.md. If it's a new context, confirm with the user before proceeding.

### .NET Module Structure

Create under `src/Modules/{ContextName}/`:

```
Balsm.{ContextName}.Domain/
  Entities/
  ValueObjects/
  Events/
  Repositories/    (interfaces only)
  Services/        (domain service interfaces)
  Exceptions/

Balsm.{ContextName}.Application/
  Commands/
  Queries/
  Handlers/
  Validators/
  DTOs/
  Mappings/

Balsm.{ContextName}.Infrastructure/
  Repositories/    (implementations)
  Configurations/  (EF Core entity configurations)
  Services/        (external service clients)
  Migrations/

Balsm.{ContextName}.Api/
  Controllers/
  Middleware/
  Filters/
```

### Starter Files to Create

- A placeholder entity in `Domain/Entities/{Entity}.cs`
- A repository interface in `Domain/Repositories/I{Entity}Repository.cs`
- A repository implementation in `Infrastructure/Repositories/{Entity}Repository.cs`
- An EF Core DbContext configuration in `Infrastructure/Configurations/{Entity}Configuration.cs`
- A controller stub in `Api/Controllers/{Entity}Controller.cs`
- Module registration class: `{ContextName}Module.cs` for DI

### Code Standards to Follow

- Entities inherit from `BaseEntity` (from `Balsm.SharedKernel`)
- Repository interface extends `IRepository<T>` from shared kernel
- Controller methods are async, return `IActionResult`, accept `CancellationToken`
- Module registration adds all services and repositories to the DI container
- All commands/queries use `Result<T>` return type (not exceptions for business failures)

3. After scaffolding, report:
   - All files created with their paths
   - Any manual steps required (e.g., EF Core migration, registering module in `Program.cs`)
