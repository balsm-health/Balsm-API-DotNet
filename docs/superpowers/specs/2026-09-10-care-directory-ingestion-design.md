# Care Directory Ingestion — Design

**Date:** 2026-09-10
**Status:** Draft for review
**Repos:** `Balsm-API-DotNet` (primary), `balsm_app` (small UI + fixture changes)

## Goal

Replace the fabricated `care_place` seed with a real, licensed, re-importable
Egyptian health-facility directory: **Overture Maps as the spine, OpenStreetMap
to fill Arabic names and opening hours, a curated overlay for partners, and
user-submitted corrections for freshness.**

## Why now

`CarePlaceConfiguration.cs` seeds 12 rows through EF `HasData`, baked into the
initial migration. They are not placeholders — they are **real institutions
carrying invented contact details**:

| seeded value | reality |
|---|---|
| `Qasr Al-Aini Hospital` / `+20 2 2365 1234` | real hospital, invented phone |
| `El-Ezaby Pharmacy` / `+20 2 2574 3210` | real chain, invented phone |
| `Ain Shams Specialized Hospital` / rating `4.3` | real hospital, invented rating |
| `Qasr Al-Aini` at `29.9765, 31.2357` | actual location ≈ `30.03, 31.23` |
| `Dr. Sara Kamal Clinic` | invented practitioner |

A patient who taps *call* on a real hospital's card and reaches an invented
number is a live defect. Removing this seed is the primary driver; better
coverage is the secondary benefit.

## Non-goals

- Ratings. No lawful free source has them (see Data Sources). The column is
  retired from the UI rather than filled with invented values.
- Countries other than Egypt. The importer is parameterised by country but only
  `EG` is imported now.
- Real-time sync. Overture publishes monthly; a monthly re-import is sufficient.
- Replacing the OSM basemap. `flutter_map` and OSM tiles stay.

## Data sources

### Overture Maps — primary

Release `2026-08-19.0`, `theme=places/type=place`, GeoParquet on public S3.
**Licence: CDLA-Permissive 2.0** — storage, redistribution and commercial use
are all permitted; no share-alike.

Measured for Egypt (`addresses[1].country = 'EG'`):

- 73,631,092 places globally
- **38,395 Egyptian health rows** under the full type mapping; 18,850 survive
  the 0.65 confidence floor
- phone present on **~91%** of rows, and that rate is flat across every
  confidence threshold
- provenance per row: `meta` 97%, `Microsoft` and `Foursquare` ~1.4% each
- **no opening-hours field exists in the schema**
- **no ratings field exists in the schema**
- `operating_status` is NULL for every Egyptian row — it cannot be used to
  filter closed businesses

### OpenStreetMap — supplement

1,971 Egyptian health features (1,653 nodes, 311 ways, 7 relations) via
Overpass. **Licence: ODbL.** Sparse where Overture is strong (phone ~11%) but
carries two things Overture lacks: explicit `name:ar`/`name:en` pairs (~37%) and
`opening_hours` (~17%).

### Google — rejected

Google Maps Platform Terms §3.2.4(a) bars extraction, §3.2.3(b) bars storing
Places content beyond `place_id`, and §3.2.4(e) bars displaying Google content
on a non-Google map — which `map_screen.dart` is. Scraping is additionally a
breach of the consumer Google ToS. Rejected on legal grounds, and separately on
provenance: scraped rows carry no defensible answer to "where did this come
from?", which partner and regulatory due diligence will ask.

## Licensing obligations this creates

1. **OSM attribution is already owed and already missing.** `map_screen.dart`
   renders `tile.openstreetmap.org` tiles with no credit. `© OpenStreetMap
   contributors` must appear on the map surface. This is a pre-existing bug
   that this work fixes.
2. **Merging OSM values makes `care_place` an ODbL Derived Database.** Serving
   query results through `GET /care/entities` is a *Produced Work* — attribution
   suffices, the database itself need not be published. Distributing the table
   would trigger share-alike.
3. To keep that boundary legible, OSM-sourced values are stored in their own
   columns rather than blended into Overture columns, so the ODbL-encumbered
   subset is always identifiable.
4. CDLA-Permissive 2.0 requires the disclaimer text to travel with redistributed
   data. An `ATTRIBUTION.md` beside the import artifact carries both notices.

## Architecture

```
Overture parquet (S3)  ─┐
                        ├─ extract  ─→  care_places.eg.ndjson.gz  ─→  importer  ─→  care_place
OSM Overpass (ODbL)    ─┘   (DuckDB)      (versioned artifact)      (dotnet)          ▲
                                                                                      │
                          curated overlay (care_place_override) ──────────────────────┤
                          user corrections (care_place_report, moderated) ────────────┘
```

**Extract is offline and reproducible.** A DuckDB script produces a versioned
NDJSON artifact; the .NET importer only ever reads that artifact. Consequences:
the API build has no S3 or Overpass dependency, CI needs no network, and the
exact bytes that populated production are auditable and re-playable.

**Import is idempotent, keyed on the Overture GERS id.** Re-running upserts;
it never duplicates. This is what lets curated values survive a monthly refresh.

**The importer is a console project**, not an EF migration. 13k+ rows in
`HasData` would bloat every migration snapshot and make the directory
un-updatable without a schema migration.

## Data model changes

`care_place` gains:

| column | type | purpose |
|---|---|---|
| `external_id` | text, null, unique | Overture GERS id; null for curated-only rows |
| `source` | text, not null | `overture` \| `osm` \| `curated` \| `user` |
| `confidence` | double, null | Overture confidence, retained for re-filtering |
| `updated_at` | timestamptz, not null | last import or curation touch |

Nullability corrections — these are currently `IsRequired()` and were only
satisfiable by inventing values:

- `hours` → nullable (no Overture source; OSM covers ~17%)
- `rating` → nullable (no lawful source at all)
- `phone` → nullable (~91% coverage, not 100%)
- `name_ar`, `address_ar` → nullable (see Bilingual Names)

New table `care_place_override` — curated values keyed by `external_id`, one
nullable column per overridable field. Applied after each import, so curation
outlives refreshes. Rows here are the partner data: verified phones, real
opening hours, corrected bilingual names.

New table `care_place_report` — user-submitted corrections (`wrong_phone`,
`closed`, `moved`, `wrong_name`), non-PHI, never auto-applied. Moderation
promotes a report into `care_place_override`.

## Type mapping

The app has six types (`hospital | clinic | pharmacy | lab | scan | store`);
Overture has dozens. Mapping, with measured Egyptian counts:

| Balsm type | Overture categories |
|---|---|
| `hospital` | `hospital` (6,640) |
| `pharmacy` | `pharmacy` (5,929) |
| `lab` | `laboratory_testing` (1,770), `diagnostic_services` minus imaging matches |
| `scan` | `radiologist` (168) + imaging-named `diagnostic_services` (see below) |
| `store` | `medical_supply` (460), `eyewear_and_optician` (1,034) |
| `dentist` | `dentist` (6,134), `cosmetic_dentist` (705), `general_dentistry` (256), `orthodontist` (192), `pediatric_dentist` (78), `oral_surgeon` (72) |
| `clinic` | every remaining category under `taxonomy.hierarchy[1] = 'health_care'` — obstetrician (1,136), physical therapy (893), dermatologist (630), pediatrician (507), and ~30 more |

Excluded outright: `pharmaceutical_companies` (wholesalers, not retail),
`medical_research_and_development`, `medical_school`.

**`diagnostic_services` → `lab` needs revisiting before implementation.**
Mapping all 2,789 rows to `lab` leaves `scan` with **168 rows nationally, 92
after filtering** — a filter tab that returns almost nothing for a country of
110 million. Egyptian radiology centres are near-certainly inside
`diagnostic_services` rather than under `radiologist`.

The extract must sample `diagnostic_services` names for radiology markers
(`أشعة`, `سكان`, `scan`, `radiology`, `imaging`, `MRI`, `X-ray`) and route
matches to `scan`. A name-based split is defensible here because Egyptian
imaging centres name themselves explicitly — unlike the transliteration case,
this is matching a keyword, not inventing a value.

**`dentist` is a seventh app type** (decided 2026-09-10). The dental bucket is
**4,729 rows** above the confidence floor — second only to `clinic`, which drops
to 4,901 once dentistry leaves it. Two types of comparable size beats one type
carrying a third of the directory under a stethoscope icon.

Requires a new `CareEntityType` enum entry in `care_entity.dart` with its own
icon, colour pair, and bilingual label, plus the `dentist` wire value
server-side.

**`scan` stays independent** (decided 2026-09-10) rather than merging into
`lab`. Honest size after the imaging split: **177 rows nationally** at the 0.65
floor, 299 unfiltered. The split works — sampled matches are genuine imaging
centres (`مركز سموحة سكان للأشعة`, `Darweesh Scan Center`) — but Overture holds
few Egyptian radiology centres under any category. `scan` will be a small tab
until curation grows it.

## Quality gate

The category taxonomy is noisy. A ten-row Cairo sample surfaced a veterinary
clinic typed `hospital`, a Prometric exam-booking office typed
`health_and_medical`, and what appears to be a gym typed `pharmacy`.

`confidence` separates them: the junk rows scored 0.30–0.59, the genuine
facilities 0.70–0.90.

**Default floor: `CareDirectory:MinConfidence = 0.65`, read from configuration,
not a constant** — retuning must not require a redeploy.

**Per-type overrides are supported**, because one global floor serves the types
badly in opposite directions: `pharmacy` loses 62% of its rows to the floor
while `scan` is starved at 177. Configuration shape:

```
CareDirectory:MinConfidence          = 0.65
CareDirectory:MinConfidenceByType:scan = 0.50   # 245 rows instead of 177
```

Measured effect of the floor, full mapping:

| Balsm type | all | ≥0.65 | phone @0.65 |
|---|---|---|---|
| `clinic` | 19,605 | 9,630 | 97% |
| `hospital` | 6,640 | 4,602 | 79% |
| `pharmacy` | 5,929 | 2,260 | 96% |
| `lab` | 4,559 | 1,887 | 97% |
| `store` | 1,494 | 379 | 93% |
| `scan` | 168 | 92 | 95% |
| **total** | **38,395** | **18,850** | |

Filtering costs volume but not phone coverage. Hospitals are the exception at
79% — notably lower than every other type, and the type where a missing phone
matters most; a curation target. Pharmacy is the noisiest
category (5,929 → 2,260 at 0.65, a 62% cut) and also the highest-traffic one,
which is where the curated overlay earns its keep — the top five chains
(19011, Misr, Roshdy, El Ezaby, Yasser Hefny) account for 136 branches.

## Arabic text normalisation

Egyptian listings spell the same word several ways — `أشعة` and `اشعة` (hamza
vs. bare alif) both occur freely, as do `ة`/`ه` and `ى`/`ي`. Naive matching
silently drops a large share of Arabic rows.

Every Arabic comparison — import-time category keyword matching AND runtime
search — normalises first: `أ إ آ → ا`, `ة → ه`, `ى → ي`, strip tatweel `ـ`.

**This is a live defect in `SearchNearbyHandler`, not just an import concern.**
`EF.Functions.Like(p.NameAr, pattern)` compares raw strings, so a user typing
`أشعة` today fails to match a facility stored as `اشعة`. Fix: persist a
normalised `name_ar_norm` / `address_ar_norm` column, index it, and match the
normalised query against it.

## Bilingual names

Overture carries **one** name per place in `names.primary`, in whichever script
the owner typed. `names.common` is empty for every Egyptian row — there is no
language map to read. Arabic-script share varies sharply by category: labs 84%,
hospitals 81%, pharmacies 69%, **dentists 33%**.

So a complete `name_en` + `name_ar` pair does not exist in the source, and the
schema's current `IsRequired()` on both is only satisfiable by inventing data.

Resolution, in priority order per row:

1. **`care_place_override`** — curated pair, where a human has supplied one.
2. **Brand-string split.** Overture brand names are frequently already
   bilingual in one field: `Misr Pharmacies صيدليات مصر`,
   `Ezz Laboratories - معامل عز لاب`, `Dawi Clinics - عيادات داوي`. Splitting on
   the script boundary yields a free, correct pair for branded rows.
3. **OSM merge** on proximity + name similarity, taking `name:ar` / `name:en`.
4. **Leave the missing side null.** No transliteration — a mangled hospital name
   is worse than an absent one, because patients recognise institutions by name.

The API DTO exposes `name_en` / `name_ar` as nullable, and the client falls back
to the language it has:

```dart
String pick(L10nText t, {required bool ar}) {
  final want = ar ? t.ar : t.en;
  return want.isNotEmpty ? want : (ar ? t.en : t.ar);
}
```

Showing an Arabic name in the English UI is correct behaviour — it is the
facility's actual name.

## Client changes (`balsm_app`)

1. New `CareEntityType.dentist` enum entry — wire value `dentist`, own icon,
   colour pair, and `(en: 'Dentists', ar: 'أطباء أسنان')` label. Sits between
   `clinic` and `pharmacy` in the enum so the filter chips keep a sensible order.
2. `pick()` falls back across languages as above.
3. `map_screen.dart:295` renders `'${e.rating} / 5'` unconditionally; with no
   rating source this shows a bare `" / 5"`. Rating is removed from both the
   list meta row (`:240`) and the detail sheet (`:295`).
4. Same treatment for `hours` where absent — omit the row, don't render an
   empty clock.
5. Add `© OpenStreetMap contributors` attribution to the map surface.
6. `FakeCareDirectoryApi` is **unchanged**. Its rows (`E2E General Hospital`,
   `+20 2 0000 0001`) are correctly synthetic — unmistakable in a screenshot and
   impossible to confuse with a real facility. Deriving fixtures from real
   Overture rows would put real institutions' names and phone numbers into test
   data, which is a regression, not an improvement.

## Testing

- **Importer**: unit tests over a checked-in 50-row NDJSON fixture — type
  mapping, confidence filtering, brand-string splitting, upsert idempotency
  (import twice, assert row count stable and curated overrides preserved).
  No network.
- **`CareDirectorySearchTests` is rewritten.** It currently asserts
  `HaveCount(12)` and that `"El-Ezaby Pharmacy"` sorts first — it depends on
  production seed data. Tests insert their own fixtures instead. This coupling
  is a defect independent of this work.
- **Extract script**: not unit-tested. It is a reproducible offline query whose
  output artifact is reviewed and versioned.

## Rollout

The seed lives in the *initial* migration, so removal is a new migration issuing
`DELETE FROM care_place WHERE id LIKE '00000000-0000-0000-0001-%'` plus the
column changes. Both migration projects (Postgres and Sqlite) need it.

Order matters: **delete the fabricated rows in the same deploy that imports real
ones**, or the map goes empty in between.

## Phases

1. **Unblock** — remove the fabricated seed, schema changes, extract script,
   importer, type mapping, confidence gate, client null-handling, OSM
   attribution. Ships a real Egyptian directory.
2. **Arabic + hours** — OSM merge for `name_ar` and `opening_hours`.
3. **Curated overlay** — `care_place_override` plus an admin path for partner
   data.
4. **User corrections** — `care_place_report`, submission UI, moderation.

Phase 1 is the one that removes the defect. 2–4 improve a directory that is
already correct.

## Open questions

1. **Is the NDJSON artifact committed to the repo or attached to releases?**
   Committed (~1 MB gzipped) is auditable and reproducible; released keeps the
   repo lean.
2. **Does `rating` leave the schema entirely**, or stay nullable against a
   future user-ratings feature? Assumed **nullable and kept** — reversible
   either way, and it costs nothing to retain.
