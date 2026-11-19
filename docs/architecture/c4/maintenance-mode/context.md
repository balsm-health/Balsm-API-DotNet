# Maintenance mode — Level 1: System Context

A Balsm super admin can put the platform into maintenance:

- `off` — normal operation.
- `readonly` — the patient app stays readable (records, medicines, emergency
  card) but pauses logging, adding, syncing, sharing, sign-in and new accounts.
  Enforced **in the app**; the API keeps accepting writes so older app builds
  keep working against a healthy server.
- `block` — the app shows a full-screen page; only the on-device emergency card
  and the country emergency number stay reachable. The API refuses
  non-essential writes with `503` + `Retry-After`.

An optional admin message and announced end time travel with the mode. The flag
holds no PHI and no per-user data.

```mermaid
C4Context
  title Maintenance mode — System Context

  Person(admin, "Balsm super admin", "Sets the mode, message and end time")
  Person(operator, "Cloud operator", "Pins the mode via deployment variables (Maintenance__Mode) where no admin portal exists")
  Person(patient, "Patient", "Sees the strip / page; paused actions explain themselves")

  System_Boundary(balsm, "Balsm Platform") {
    System(portal, "Admin portal (Supervisor)", "Standalone only. Session-cookie auth")
    System(api, "Balsm API (host)", "Stores the flag, serves it anonymously, guards writes in block mode")
    System(app, "Balsm Patient App", "Polls the flag on launch, on resume, every few minutes and on Check again")
  }

  Rel(admin, portal, "PUT /api/v1/admin/maintenance", "HTTPS + admin session")
  Rel(portal, api, "IMaintenanceModeService.SetAsync (in-process)")
  Rel(operator, api, "Maintenance:* configuration override")
  Rel(app, api, "GET /api/v1/maintenance", "HTTPS, anonymous")
  Rel(patient, app, "Uses")
```

Failure modes: if the read fails (offline, timeout, 5xx) the app treats the
mode as **unknown** and never blocks itself; the last known mode is kept only
while it is fresh.
