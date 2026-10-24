# Care Directory — Level 4: Dynamic, artifact import on startup

How ~19k Egyptian health places reach `care_place`, from a public S3 bucket to a
row a patient can call.

```mermaid
sequenceDiagram
  autonumber
  actor Dev as Engineer
  participant Extract as tools/care-directory/extract.py
  participant S3 as Overture S3 (public)
  participant Repo as data/care-directory/*.ndjson.gz
  participant Host as Balsm.API host
  participant Mig as MigrationRunner
  participant Imp as CareDirectoryImportService
  participant Db as care_place

  rect rgb(245,245,250)
    note over Dev,Repo: OFFLINE — by hand, on an Overture release bump
    Dev->>Extract: run (pinned release, country=EG)
    Extract->>S3: DuckDB query over GeoParquet
    S3-->>Extract: 38,395 rows (all confidences)
    Extract->>Repo: write .ndjson.gz (mtime=0) + manifest.json (release, count, sha256)
    Dev->>Repo: commit
  end

  rect rgb(240,248,244)
    note over Host,Db: RUNTIME — every boot
    Host->>Mig: StartAsync (hosted service, registered first)
    Mig->>Db: MigrateAsync
    Mig-->>Host: ReadinessGate.SetReady
    Host->>Imp: StartAsync (registered after Mig, so schema exists)
    alt artifact missing
      Imp-->>Host: log WARNING, continue — a blank directory must not stop the host
    else artifact present
      Imp->>Repo: open (probes AppContext.BaseDirectory then ContentRootPath)
      loop per line, batched 500
        Imp->>Imp: skip if confidence < floor for type
        Imp->>Imp: split bilingual name / normalise Arabic
        Imp->>Db: upsert on external_id (Overture GERS id)
      end
      Imp-->>Host: 18,919 inserted, 19,476 below floor
    end
  end
```

## Decisions this sequence encodes

**Hosted service, not a CLI.** The directory ships inside a self-hosted
deployment. An operator who never ran an import command would be left with a
blank map and no obvious cause, so the import follows `MigrationRunner`'s
pattern and runs itself.

**Registration order is load-bearing.** Hosted services start in registration
order. `MigrationRunner` is registered in `Balsm.Infrastructure`, the importer in
`AddCareDirectoryInfrastructure` which runs after — so the schema exists before
the first insert.

**Both roots are probed for the artifact.** `<Content>` copies to the *build
output* directory, which is where a published deployment runs from; under
`dotnet run` the content root is the project directory instead. Resolving from
only one silently finds nothing in development.

**Upsert on the GERS id, not on name or coordinates.** A re-import refreshes in
place and never duplicates, and `Id`/`CreatedAt` survive it. That stability is
what curated overrides and user corrections will key to in Phases 3–4.

**Every row is extracted, the floor is applied at import.** Confidence is
configuration (`CareDirectory:MinConfidence`, with per-type overrides), so it can
be retuned without re-extracting.

## Failure modes

| failure | behaviour |
|---|---|
| artifact missing | WARNING, host starts, directory empty |
| artifact corrupt / unparseable line | exception caught, ERROR logged, host stays up |
| import slower than boot | host reports ready before the import finishes; the map is briefly sparse, never wrong |
| re-import of identical data | all rows counted as updated, none inserted, no duplicates |
