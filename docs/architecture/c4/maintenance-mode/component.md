# Maintenance mode — Level 3: Component

Maintenance is platform plumbing, not a bounded context: it lives in the shared
`Balsm.Infrastructure` (state + write guard) with one host controller and one
Supervisor controller. No module project is touched and no module references
another. State is three rows in the existing `PlatformDbContext.ServerConfigs`
key/value table — no migration.

```mermaid
C4Component
  title Maintenance mode — components

  Container_Boundary(host, "Balsm.API (composition root)") {
    Component(readCtl, "MaintenanceController", "ASP.NET Core", "GET /api/v1/maintenance — [AllowAnonymous], Cache-Control 15s")
    Component(pipeline, "Program.cs pipeline", "Middleware order", "… CORS → AuditEnricher → MaintenanceWriteGuard → AdminAuth → Authorization …")
  }

  Container_Boundary(sup, "Balsm.Supervisor (admin portal only)") {
    Component(adminCtl, "AdminMaintenanceController", "ASP.NET Core", "GET/PUT /api/v1/admin/maintenance — behind AdminAuthMiddleware")
    Component(sessions, "AdminSessionService", "Singleton", "Resolves the admin username for the audit actor")
  }

  Container_Boundary(infra, "Balsm.Infrastructure") {
    Component(svc, "MaintenanceModeService", "Singleton", "Validates input, persists, audits; 10s in-memory snapshot; config override wins")
    Component(guard, "MaintenanceWriteGuardMiddleware", "Middleware", "block ⇒ 503 for POST/PUT/PATCH/DELETE outside the allowlist")
    Component(audit, "IAuditLogWriter", "Scoped", "Writes Maintenance/MaintenanceModeChanged")
    ComponentDb(db, "PlatformDbContext.ServerConfigs", "EF Core", "maintenance_mode, _message, _ends_at, _updated_at")
  }

  Rel(readCtl, svc, "GetAsync")
  Rel(adminCtl, svc, "SetAsync(change, actor)")
  Rel(adminCtl, sessions, "GetUsername(token)")
  Rel(guard, svc, "GetAsync (snapshot)")
  Rel(svc, db, "read / upsert")
  Rel(svc, audit, "WriteAsync")
```

Write-guard allowlist (block mode): `/api/v1/admin/*` (so the admin can switch
it off), `/api/v1/federation/*` and `/connect` (server-to-server), `/auth/refresh`
and `/auth/sign-out` (keep sessions alive / let a patient sign out). All `GET`s
pass, so health probes, `/status`, the flag itself and the public emergency-QR
resolve stay up.
