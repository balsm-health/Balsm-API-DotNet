# Pregnancy guidance — Level 4: Dynamic (fetch and revalidate)

No module, MediatR handler, repository or DbContext is involved: the content is
a static file embedded in `Balsm.API` and loaded once per process.

```mermaid
C4Dynamic
  title Pregnancy guidance — open the pregnancy screen

  Container(screen, "PregnancyScreen", "Flutter", "Guidance card / Week by week")
  Container(usecase, "WatchPregnancyGuidanceUseCase", "pregnancy module", "Kept copy first, then fresh if newer")
  Container(adapter, "ApiPregnancyGuidance", "app adapter", "PregnancyGuidanceSource port")
  ContainerDb(kv, "GlobalKVDataSource", "SharedPreferences", "content.pregnancy_weeks.v1 = {etag, data}")
  Container(client, "DioContentApi", "packages/balsm_api", "If-None-Match, 200|304")
  Container(ctrl, "ContentController", "Balsm.API", "GET /api/v1/content/pregnancy-weeks")
  Container(content, "PregnancyWeeksContent", "Balsm.API", "Embedded JSON, validated + serialised once, ETag")

  Rel(screen, usecase, "1. watch (invalidated on every opening)")
  Rel(usecase, adapter, "2. cached() → emit kept copy")
  Rel(adapter, kv, "3. read {etag, data}")
  Rel(usecase, adapter, "4. refresh()")
  Rel(adapter, client, "5. pregnancyWeeks(etag)")
  Rel(client, ctrl, "6. GET + Bearer + If-None-Match")
  Rel(ctrl, content, "7. Current (lazy, once per process)")
  Rel(ctrl, client, "8a. 304 (tag matches) / 8b. 200 {data} + ETag")
  Rel(adapter, kv, "9. on 200: store {etag, data}")
  Rel(usecase, screen, "10. emit fresh copy if its version changed")
```

Failure paths: `401`, offline, timeout, malformed body → the adapter returns the
kept copy (or null); the card is hidden when there is nothing at all.
