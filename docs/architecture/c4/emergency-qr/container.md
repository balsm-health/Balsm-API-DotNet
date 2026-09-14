# Emergency QR — Level 2: Container

The module follows the standard four-project layout; handlers that touch the
DbContext live in Infrastructure (repo rule: no Application→EF dependency).

```mermaid
C4Container
  title EmergencyQr module — Containers

  Person(patient, "Patient app", "Flutter (modules/emergency_card + packages/balsm_api)")
  Person(responder, "Public resolve page")

  System_Boundary(mod, "src/Modules/EmergencyQr") {
    Container(apiproj, "Balsm.EmergencyQr.Api", "ASP.NET controllers", "EmergencyQrController: mint, active, resolve, revoke, ciphertext update. HealthController: GET /emergency-qr/health")
    Container(app_layer, "Balsm.EmergencyQr.Application", "MediatR contracts", "MintEmergencyQrCommand · UpdateEmergencyQrCiphertextCommand · RevokeEmergencyQrCommand · GetActiveQrQuery · ResolveEmergencyQrQuery")
    Container(infra, "Balsm.EmergencyQr.Infrastructure", "EF Core + handlers", "EmergencyQrDbContext, one handler per command/query, Npgsql + Sqlite migration assemblies")
    Container(domain, "Balsm.EmergencyQr.Domain", "Entities", "EmergencyQrToken: Mint(ttl∈{0,1h,6h,24h,7d}), UpdateCiphertext (active-only), Revoke, IsActive = not revoked ∧ (no expiry ∨ future expiry)")
  }

  ContainerDb(db, "emergency_qr_token", "SQLite / PostgreSQL", "jti · user_id · ciphertext · profile_etag · preferred_language · ttl_seconds · expires_at (NULL = permanent) · revoked_at")

  Rel(patient, apiproj, "JWT-authenticated endpoints")
  Rel(responder, apiproj, "GET /resolve/{jti}, anonymous")
  Rel(apiproj, app_layer, "IMediator.Send")
  Rel(app_layer, infra, "handled by")
  Rel(infra, domain, "materialises / mutates")
  Rel(infra, db, "EF Core")
```

## Contract notes

- Responses use the platform envelope `{ data, error }` with snake_case keys
  (`token_id`, `expires_at`, `ciphertext`, `preferred_language`, `ttl_seconds`).
  `expires_at` is `null` for permanent tokens — clients must treat it as
  nullable everywhere.
- `profile_etag` (≤ 8 chars) is a client-computed fingerprint of the snapshot;
  the server stores it opaquely. It exists so the app can tell whether the
  ciphertext it last pushed still matches the on-device profile.
- Insomnia collection: `docs/api/insomnia/emergency-qr.yaml`. OpenAPI:
  `docs/api/openapi/v1/all.json`.
