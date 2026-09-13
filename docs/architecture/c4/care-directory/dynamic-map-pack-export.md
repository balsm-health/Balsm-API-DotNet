# Care Directory — Level 4: Dynamic, nightly map-pack export

One run of `MapPackExportJob`, from the cron tick to a governorate becoming
downloadable (or staying exactly as it was).

```mermaid
sequenceDiagram
  autonumber
  participant Job as MapPackExportJob
  participant Runner as MapPackExportRunner
  participant Registry as governorates.json
  participant R2 as CloudflareR2ObjectStore
  participant Db as map_pack_artifact
  participant Places as care_place

  Job->>Job: cron fires (default 01:00 UTC)
  alt R2 not configured
    Job-->>Job: log WARNING, return — host stays up, catalogue unchanged
  else configured
    Job->>Registry: load (probes AppContext.BaseDirectory then ContentRootPath)
    alt registry missing/empty
      Job-->>Job: log WARNING, return
    else 27 governorates loaded
      Job->>Runner: RunAsync

      rect rgb(240,248,244)
        note over Runner,Db: reconcile basemaps — what CI already uploaded
        Runner->>R2: ListObjectsV2("packs/") + HeadObject per key (sha256 metadata)
        R2-->>Runner: keys, sizes, sha256
        Runner->>Runner: parse "packs/{id}-{version}.pmtiles", keep newest per id
        loop each matched governorate
          Runner->>Db: upsert Basemap row (Publish if new, Republish if version changed)
        end
      end

      rect rgb(245,245,250)
        note over Runner,Places: export places — the job's own write
        Runner->>Places: AsNoTracking, load all rows once
        loop each of 27 governorates
          Runner->>Runner: bbox-filter this governorate's places
          Runner->>Runner: build ndjson.gz + sha256 (PlacesSnapshotExporter)
          Runner->>Db: read existing Places row's PlaceCount
          alt new count < 50% of previous
            Runner-->>Runner: log ERROR, skip — previous snapshot + CDN object stay live
          else
            Runner->>R2: PutObject "places/{id}-{yyyyMMdd}.ndjson.gz"
            Runner->>Db: upsert Places row, SaveChanges immediately
          end
        end
      end
    end
  end
```

## Decisions this sequence encodes

**Basemap reconciliation and places export are two different trust
relationships to the same table**, covered in
[context-map-packs.md](./context-map-packs.md#trust-boundary-two-writers-one-reader-no-runtime-coupling-between-them):
one catches the manifest up to what CI already published, the other is the
job's own export. They run in the same method for scheduling convenience
only — neither depends on the other's result.

**Places are loaded once, not once per governorate.** ~38k rows in memory is
cheap next to 27 separate filtered queries a night, and it is what lets the
bbox split (`GovernorateRef.Contains`) be a pure, unit-tested function rather
than 27 EF translations of the same predicate.

**The guard compares against the row already in the table, not against
yesterday's file.** `ExportGuard.ShouldPublish` only ever sees
`(previousCount, newCount)` — a governorate with no prior Places row always
publishes (there is nothing to have dropped from), which is what lets a
brand-new governorate onboard on its first night rather than being
permanently refused for having "0 previous places."

**SaveChanges runs after every governorate, not once at the end.** A
transient R2 or DB failure on governorate 15 must not roll back the 14 that
already succeeded and already have real bytes sitting on the CDN — a
half-finished run should leave "some governorates refreshed tonight," never
"none did."

**A refused snapshot is silent to the app.** `GET /care/packs` still returns
the previous Places row — same version, same URL, same count — because
nothing in the table changed. The only visible trace of a refusal is the
ERROR log; there is no user-facing "export failed" state, on purpose: a
patient looking for offline packs should see yesterday's good data, not a
warning about a job they've never heard of.

## Failure modes

| failure | behaviour |
|---|---|
| a basemap key has no `sha256` metadata | that key is skipped with a WARNING — it was not written by `publish.py` and could not be verified on download anyway |
| a governorate's new place count is a large drop from its last snapshot | that governorate is skipped for the night; every other governorate still runs; previous snapshot stays advertised |
| R2 `PutObject` throws for one governorate | caught, logged, next governorate proceeds — see the per-governorate `try/catch` in `MapPackExportRunner.ExportPlacesAsync` |
| the whole run throws before reaching the loop (e.g. `ListObjectsV2` fails) | caught by `MapPackExportJob`'s outer handler; logged; the next cron tick tries again |
| host restarts mid-run | no partial-run bookkeeping — governorates already `SaveChanges`d keep their update, the rest wait for the next scheduled run |
