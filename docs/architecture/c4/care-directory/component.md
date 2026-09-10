# Care Directory — Level 3: Component

```mermaid
C4Component
  title Care Directory module — components

  Container_Boundary(api, "Balsm.CareDirectory.Api") {
    Component(care_ctrl, "CareController", "GET /care/entities — AllowAnonymous. Binds radius_km explicitly; query-string binding does not apply the JSON naming policy")
    Component(health_ctrl, "HealthController", "GET /care/health — liveness only, touches no DbContext")
  }

  Container_Boundary(app_layer, "Balsm.CareDirectory.Application") {
    Component(query, "SearchNearbyQuery", "MediatR query. Owns EffectiveLimit: default 200, ceiling 500")
    Component(dto, "CareEntityDto", "Wire DTO. name_ar / address_* / hours / phone / rating are NULLABLE")
  }

  Container_Boundary(domain, "Balsm.CareDirectory.Domain") {
    Component(place, "CarePlace", "Entity. Computes normalised Arabic columns on write so they cannot drift")
    Component(arabic, "ArabicText", "Folds hamza forms, ta marbuta, alif maqsura, tatweel, diacritics")
    Component(bilingual, "BilingualName", "Splits a one-field bilingual listing into an en/ar pair")
  }

  Container_Boundary(infra, "Balsm.CareDirectory.Infrastructure") {
    Component(handler, "SearchNearbyHandler", "Type/text filter in SQL, Haversine in memory, sort then Take(limit)")
    Component(importer, "CareDirectoryImporter", "Upserts the artifact on external_id, batched 500")
    Component(import_svc, "CareDirectoryImportService", "IHostedService — runs the importer after migrations")
    Component(ctx, "CareDirectoryDbContext", "care_place. LOCAL database (Database section), not CloudDatabase")
  }

  ComponentDb(db, "care_place", "SQLite / Postgres", "~18.9k rows. Indexed on (country_code,type), external_id unique, normalised Arabic columns")

  Rel(care_ctrl, query, "Send")
  Rel(query, handler, "handled by")
  Rel(handler, ctx, "AsNoTracking read")
  Rel(handler, arabic, "normalises the incoming q")
  Rel(handler, dto, "projects")
  Rel(import_svc, importer, "runs on startup")
  Rel(importer, place, "Create / UpdateFromImport")
  Rel(importer, bilingual, "recovers en/ar pairs")
  Rel(place, arabic, "computes norm columns")
  Rel(ctx, db, "EF Core")
```

## Layering

`Api → Application → Domain` and `Infrastructure → Domain`, as the module rules
require. The handler lives in `Infrastructure` rather than `Application` because
it touches a DbContext — the repo-wide convention that avoids a circular project
dependency.

`ArabicText` and `BilingualName` are pure functions in `Domain` with no
dependencies, which is why both the importer and the search handler can use them
without either layer reaching across.

## The two places correctness is easy to lose

**Search must compare normalised Arabic on both sides.** `EF.Functions.Like`
against the raw `name_ar` means a user searching `أشعة` never matches a place
stored as `اشعة` — the same word, the other spelling. The entity maintains
`name_ar_norm` on every write so the stored side can never drift from its source;
the handler normalises the query side.

**The limit must truncate after the sort.** `Take` before `OrderBy` would return
an arbitrary 200 rather than the nearest 200 — the difference between a usable
map and a random one.
