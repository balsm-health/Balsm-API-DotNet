# Care directory extract

Produces `data/care-directory/care_places.eg.ndjson.gz` — the care directory
import artifact — from Overture Maps.

## Running it

```bash
python3 -m venv .venv-extract
.venv-extract/bin/pip install duckdb
.venv-extract/bin/python tools/care-directory/extract.py
```

Options: `--release <overture-release>`, `--country <ISO2>`, `--out <dir>`.

The extract is **offline and by hand**, not part of the build. The API never
talks to S3 or Overpass — it reads only the committed artifact. That keeps CI
network-free and makes the exact bytes that populated production auditable and
re-playable.

Re-run when bumping to a newer Overture release. Commit the regenerated
artifact together with `manifest.json`, which records the release, row count and
sha256. Gzip is written with `mtime=0`, so re-extracting identical data produces
no diff.

## What it does

- Pre-filters on a coarse bbox so DuckDB can prune row groups, then cuts exactly
  on `addresses[1].country`. Roughly 4.9% of rows inside that box are Palestinian,
  Israeli, Jordanian, Saudi or Sudanese and are dropped by the country test.
- Maps Overture's several dozen health categories onto the app's seven types.
  `diagnostic_services` and `laboratory_testing` rows whose names carry imaging
  keywords route to `scan`; the rest go to `lab`.
- Emits **every** matching row regardless of confidence. The importer applies the
  floor, so it can be retuned in configuration without re-extracting.

Excluded outright: `pharmaceutical_companies` (wholesalers), 
`medical_research_and_development` and `medical_school` — not places a patient
can walk into.

## Known limits

- **No opening hours and no ratings.** Overture's schema has neither field.
  Hours arrive in Phase 2 from OpenStreetMap; ratings have no lawful free source
  at all and the column stays null.
- **One name per place, never a bilingual pair.** `names.common` is empty for
  every Egyptian row, so `name_script` records which script `name` is in and the
  importer fills the other side from the brand string where it can.
- **`operating_status` is NULL for every Egyptian row**, so it cannot be used to
  filter closed businesses. The confidence floor is the only quality gate.
- **The category taxonomy is noisy.** A ten-row Cairo sample turned up a
  veterinary clinic typed `hospital` and an exam-booking office typed
  `health_and_medical`, both below 0.6 confidence.

## Licences and attribution

**Overture Maps Places — CDLA-Permissive-2.0.** Storage, redistribution and
commercial use are permitted, with no share-alike. The disclaimer travels with
redistributed data. Attribution: **© Overture Maps Foundation**.

Note that the permissive licence covers the *places* theme specifically, which
derives from Meta and Microsoft data. Other Overture themes (buildings,
transportation) contain OSM-derived data and carry ODbL instead — do not assume
this licence across themes.

**OpenStreetMap — ODbL** (arrives in Phase 2, for `name_ar` and `opening_hours`).
Merging OSM values makes `care_place` an ODbL *Derived Database*. Serving query
results through `GET /care/entities` is a *Produced Work*, so attribution
suffices and the database itself need not be published — but distributing the
table would trigger share-alike. OSM-sourced values are stored in their own
columns to keep that boundary identifiable. Attribution:
**© OpenStreetMap contributors**.

The app already owes the OSM tile attribution today: `map_screen.dart` renders
`tile.openstreetmap.org` tiles, and that credit is a Phase 1 task.

**Google Maps is deliberately not a source.** Maps Platform Terms §3.2.4(a) bars
extraction, §3.2.3(b) bars storing Places content beyond `place_id`, and
§3.2.4(e) bars displaying Google content on a non-Google map — which this app's
OSM basemap is.
