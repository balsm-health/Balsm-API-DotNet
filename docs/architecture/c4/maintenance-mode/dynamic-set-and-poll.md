# Maintenance mode — Level 4: Dynamic (set, poll, guarded write)

```mermaid
C4Dynamic
  title Super admin switches to block; the app notices; a write is refused

  Person(admin, "Super admin")
  Container(portal, "AdminMaintenanceController", "Supervisor")
  Container(svc, "MaintenanceModeService", "Balsm.Infrastructure")
  ContainerDb(db, "PlatformDbContext", "ServerConfigs + AuditLogs")
  Container(app, "Patient app", "Flutter")
  Container(read, "MaintenanceController", "Balsm.API")
  Container(guard, "MaintenanceWriteGuardMiddleware", "Balsm.Infrastructure")

  Rel(admin, portal, "1. PUT /api/v1/admin/maintenance {mode: block}", "admin session cookie")
  Rel(portal, svc, "2. SetAsync(change, 'admin:<user>')")
  Rel(svc, db, "3. upsert maintenance_* rows; insert audit row")
  Rel(app, read, "4. GET /api/v1/maintenance (launch / resume / timer / Check again)")
  Rel(read, svc, "5. GetAsync — snapshot ≤10s old")
  Rel(app, guard, "6. POST /… (old build or race)")
  Rel(guard, app, "7. 503 problem+json code=maintenance.blocked, Retry-After: 300")
```

Notes

- Validation failures (unknown mode, message > 280 chars, `ends_at` not in the
  future) return `400` with `code = maintenance.validation`; a configuration
  override returns `409 maintenance.config_override`.
- Switching to `off` clears the message and end time.
- Multi-replica cloud runs converge within the 10 s snapshot window.
