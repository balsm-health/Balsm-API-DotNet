# Care Directory Ingestion — Implementation Plan (Phase 1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the 12 fabricated `care_place` rows with ~18,850 real, licensed Egyptian health facilities imported from Overture Maps.

**Architecture:** An offline DuckDB script extracts Overture GeoParquet into a versioned NDJSON artifact committed to the repo. A .NET hosted service imports that artifact into `care_place` on startup, after migrations, idempotently keyed on the Overture GERS id. The Flutter client gains a seventh entity type and stops rendering fields that no longer have values.

**Tech Stack:** .NET 10, EF Core (Sqlite + Npgsql migration sets), MediatR, xUnit + FluentAssertions + NSubstitute; Flutter 3.41.9 via fvm, Riverpod; DuckDB (extract only, not a runtime dependency).

**Spec:** `docs/superpowers/specs/2026-09-10-care-directory-ingestion-design.md`

**Repos:** `Balsm-API-DotNet` (Tasks 1–4) and `balsm_app` (Tasks 5–7). Both are on their MVP branches: `feature/phase-01/mvp` and `feature/mvp`.

## Global Constraints

- **Never log, print, or transmit PHI. Never fabricate sample PHI in code or fixtures.** Care directory data is public business information, NOT PHI — but the fabricated-data failure this plan fixes is the same class of mistake, so no invented names, phones, coordinates, or ratings anywhere, including tests.
- **Never hardcode secrets, API keys, passwords, or connection strings.**
- JSON wire format is snake_case via the host's global `JsonNamingPolicy.SnakeCaseLower` (`Program.cs:367`). DTO properties stay PascalCase; do not add `[JsonPropertyName]`.
- Query-string binding does NOT apply the JSON naming policy — snake_case query parameters need an explicit `[FromQuery(Name = "...")]`, as `CareController.Entities` already does for `radius_km`.
- Soft-delete only for clinical data. Care directory is reference data and is exempt; the importer may hard-delete rows that leave the source.
- Dart formatter `page_width: 120`. All Flutter commands run through fvm: `fvm flutter`, `fvm dart`.
- Commit convention: `[Tag] Description`, e.g. `[Feature]`, `[Fix]`, `[Test]`, `[Refactor]`, `[Docs]`.
- Care directory lives in the LOCAL database (`Database` section, default `Data Source=balsm.db`), NOT `CloudDatabase`.
- The repo has TWO migration sets for `CareDirectoryDbContext`: Npgsql inline in `Balsm.CareDirectory.Infrastructure/Migrations/`, Sqlite in `Balsm.CareDirectory.Infrastructure.Migrations.Sqlite/Migrations/`. Every schema change needs both.

## Task Ordering

Two constraints the task numbers do not convey:

**Task 3 before Task 2.** `CarePlace.Create` computes the normalised Arabic
columns by calling `ArabicText.Normalize`, so Task 2 does not compile until
`ArabicText` exists. Implement Task 3 first, or land a stub in Task 2 and fill
it in Task 3.

**Task 6 before Task 7.** Task 2 makes the wire fields nullable; the Flutter
parser casts them with `as String` and throws on null. Between those two
commits the care directory is broken in the client — Task 6 is the fix and must
not trail the UI work.

**Tasks 2 and 4 ship together.** Task 2 deletes the fabricated rows; Task 4
imports the real ones. Between them the directory is EMPTY. That is fine inside
a branch, but the two must reach any running environment in the same deploy or
the map goes blank for users.

## File Structure

**`Balsm-API-DotNet`**

| path | responsibility |
|---|---|
| `tools/care-directory/extract.py` | DuckDB extract → NDJSON. Offline, run by hand, not part of the build. |
| `tools/care-directory/README.md` | How to re-run the extract, and the licence notices that travel with the data. |
| `data/care-directory/care_places.eg.ndjson.gz` | Versioned import artifact. |
| `data/care-directory/manifest.json` | Overture release, extraction date, row count, sha256 of the artifact. |
| `src/Modules/CareDirectory/Balsm.CareDirectory.Domain/Entities/CarePlace.cs` | Entity — gains provenance columns, loses required-ness on unsourced fields. |
| `src/.../Balsm.CareDirectory.Domain/ArabicText.cs` | Arabic normalisation. Pure function, no dependencies. |
| `src/.../Balsm.CareDirectory.Infrastructure/Configuration/CarePlaceConfiguration.cs` | EF mapping — the fabricated `HasData` seed is deleted from here. |
| `src/.../Balsm.CareDirectory.Infrastructure/Import/CareDirectoryImporter.cs` | Reads the artifact, upserts on `external_id`. |
| `src/.../Balsm.CareDirectory.Infrastructure/Import/CareDirectoryImportService.cs` | `IHostedService` that runs the importer after migrations. |
| `src/.../Balsm.CareDirectory.Infrastructure/Import/CareDirectoryOptions.cs` | Bound config: confidence floors, artifact path, enable flag. |
| `tests/Modules/Balsm.CareDirectory.Tests/` | Handler, normalisation, and importer tests. |

**`balsm_app`**

| path | responsibility |
|---|---|
| `packages/balsm_api/lib/src/care_directory/responses.dart` | Nullable wire fields — without this the client throws on every response. |
| `packages/balsm_api/test/care_directory/responses_test.dart` | New — null-tolerance of the parser. |
| `app/lib/balsm_app/care/care_entity.dart` | `CareEntityType.dentist`; `pick()` cross-language fallback. |
| `app/lib/balsm_app/screens/map_screen.dart` | Null-safe rating/hours rows; OSM attribution. |
| `app/test/care_filter_test.dart` | Extended for the seventh type. |
| `app/test/care_entity_test.dart` | New — `pick()` fallback behaviour. |

---

### Task 1: Overture extract script and artifact

**Files:**
- Create: `tools/care-directory/extract.py`
- Create: `tools/care-directory/README.md`
- Create: `data/care-directory/manifest.json` (written by the script)
- Create: `data/care-directory/care_places.eg.ndjson.gz` (written by the script)

**Interfaces:**
- Consumes: nothing.
- Produces: the NDJSON line schema every later task depends on. One JSON object per line:
  ```json
  {"external_id":"08f1...","type":"pharmacy","name":"صيدليات الشلقاني","name_script":"ar",
   "address":"...","lat":30.0444,"lng":31.2357,"phone":"+201111123032",
   "confidence":0.78,"brand":null,"source":"overture"}
  ```
  `type` is one of `hospital|clinic|dentist|pharmacy|lab|scan|store`. `name_script` is `ar` or `en`. `phone`, `address`, `brand` are nullable. No `hours`, no `rating` — Overture has neither.

- [ ] **Step 1: Write the extract script**

`tools/care-directory/extract.py`:

```python
#!/usr/bin/env python3
"""Extract Egyptian health places from Overture Maps into an import artifact.

Offline and reproducible: pin OVERTURE_RELEASE, run, commit the result. The API
never talks to S3 — it only ever reads the artifact this produces.

Usage:  python3 extract.py [--release 2026-08-19.0] [--country EG]
Requires: pip install duckdb
"""
import argparse, gzip, hashlib, json, pathlib, sys
import duckdb

OVERTURE_RELEASE = "2026-08-19.0"
BBOX = "bbox.xmin BETWEEN 24.7 AND 36.9 AND bbox.ymin BETWEEN 21.9 AND 31.7"

# Imaging keywords route diagnostic_services/laboratory_testing rows to `scan`.
# Both Arabic spellings of أشعة/اشعة are listed because Egyptian listings use
# each freely and DuckDB's ILIKE does not normalise hamza forms.
IMAGING = [
    "scan", "radiolog", "imaging", "x-ray", "xray", "mri", "tomograph",
    "ultrasound", "sonar", "doppler",
    "أشعة", "اشعة", "أشعه", "اشعه", "سكان", "رنين", "سونار", "مقطعية", "دوبلر", "تصوير",
]

DENTAL = ("dentist", "cosmetic_dentist", "general_dentistry", "orthodontist",
          "pediatric_dentist", "oral_surgeon")
EXCLUDED = ("pharmaceutical_companies", "medical_research_and_development", "medical_school")


def build_sql(release: str, country: str) -> str:
    img = " OR ".join(f"names.primary ILIKE '%{t}%'" for t in IMAGING)
    dental = ", ".join(f"'{c}'" for c in DENTAL)
    excluded = ", ".join(f"'{c}'" for c in EXCLUDED)
    src = (f"s3://overturemaps-us-west-2/release/{release}"
           "/theme=places/type=place/*.parquet")
    return f"""
WITH mapped AS (
SELECT
  id                                                  AS external_id,
  CASE
    WHEN categories.primary = 'hospital' THEN 'hospital'
    WHEN categories.primary = 'pharmacy' THEN 'pharmacy'
    WHEN categories.primary = 'radiologist' THEN 'scan'
    WHEN categories.primary IN ('diagnostic_services','laboratory_testing')
         AND ({img}) THEN 'scan'
    WHEN categories.primary IN ('diagnostic_services','laboratory_testing') THEN 'lab'
    WHEN categories.primary IN ({dental}) THEN 'dentist'
    WHEN categories.primary IN ('medical_supply','eyewear_and_optician') THEN 'store'
    WHEN taxonomy.hierarchy[1] = 'health_care' THEN 'clinic'
  END                                                 AS type,
  names.primary                                       AS name,
  CASE WHEN regexp_matches(names.primary, '[؀-ۿ]') THEN 'ar' ELSE 'en' END AS name_script,
  addresses[1].freeform                               AS address,
  ST_Y(geometry)                                      AS lat,
  ST_X(geometry)                                      AS lng,
  phones[1]                                           AS phone,
  confidence,
  brand.names.primary                                 AS brand,
  'overture'                                          AS source
FROM read_parquet('{src}')
WHERE {BBOX}
  AND addresses[1].country = '{country}'
  AND categories.primary NOT IN ({excluded})
  AND names.primary IS NOT NULL
)
SELECT * FROM mapped WHERE type IS NOT NULL ORDER BY external_id
"""


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--release", default=OVERTURE_RELEASE)
    ap.add_argument("--country", default="EG")
    ap.add_argument("--out", default="data/care-directory")
    args = ap.parse_args()

    con = duckdb.connect()
    con.execute("INSTALL httpfs; LOAD httpfs; INSTALL spatial; LOAD spatial;")
    con.execute("CREATE SECRET IF NOT EXISTS (TYPE s3, PROVIDER config, REGION 'us-west-2');")

    print(f"querying Overture {args.release} for {args.country} ...", file=sys.stderr)
    cols = [d[0] for d in con.execute(build_sql(args.release, args.country)).description]
    rows = con.execute(build_sql(args.release, args.country)).fetchall()
    print(f"  {len(rows)} rows", file=sys.stderr)

    out_dir = pathlib.Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    artifact = out_dir / f"care_places.{args.country.lower()}.ndjson.gz"

    # mtime=0 keeps the gzip byte-identical across runs of identical data, so a
    # re-extract that changes nothing produces no diff.
    with gzip.GzipFile(artifact, "wb", mtime=0) as fh:
        for row in rows:
            rec = dict(zip(cols, row))
            fh.write((json.dumps(rec, ensure_ascii=False, sort_keys=True) + "\n").encode())

    digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
    (out_dir / "manifest.json").write_text(json.dumps({
        "overture_release": args.release,
        "country": args.country,
        "row_count": len(rows),
        "artifact": artifact.name,
        "sha256": digest,
        "license": "CDLA-Permissive-2.0",
        "attribution": "© Overture Maps Foundation",
    }, indent=2) + "\n")
    print(f"wrote {artifact} ({len(rows)} rows, sha256 {digest[:12]}…)", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 2: Run it and verify the row count**

```bash
cd Balsm-API-DotNet
python3 -m venv .venv-extract && .venv-extract/bin/pip -q install duckdb
.venv-extract/bin/python tools/care-directory/extract.py
```

Expected: roughly **38,000 rows** before any confidence filtering (the importer applies the floor, not the extract — keeping every row lets the floor be retuned without re-extracting). `manifest.json` records the exact count.

- [ ] **Step 3: Sanity-check the type distribution**

```bash
gzcat data/care-directory/care_places.eg.ndjson.gz | python3 -c "
import sys, json, collections
c = collections.Counter(json.loads(l)['type'] for l in sys.stdin)
print(c)"
```

Expected shape: `clinic` ≈ 14,900, `dentist` ≈ 7,400, `hospital` ≈ 6,600, `pharmacy` ≈ 5,900, `lab` ≈ 4,400, `store` ≈ 1,500, `scan` ≈ 260. If `scan` is below 200 or `dentist` is absent, the CASE ordering is wrong — `dentist` must be tested before the `health_care` catch-all, and the imaging branch before the plain lab branch.

- [ ] **Step 4: Write the README with licence notices**

`tools/care-directory/README.md` must state: the Overture release pinned, how to re-run, that Overture Places is CDLA-Permissive-2.0 and requires `© Overture Maps Foundation`, and that when Phase 2 merges OSM the artifact additionally carries ODbL obligations and `© OpenStreetMap contributors`.

- [ ] **Step 5: Commit**

```bash
git add tools/care-directory data/care-directory
git commit -m "[Feature] Care directory: Overture extract script and Egypt artifact"
```

---

### Task 2: Schema migration — remove the fabricated seed

**Files:**
- Modify: `src/Modules/CareDirectory/Balsm.CareDirectory.Domain/Entities/CarePlace.cs`
- Modify: `src/Modules/CareDirectory/Balsm.CareDirectory.Infrastructure/Configuration/CarePlaceConfiguration.cs`
- Modify: `src/Modules/CareDirectory/Balsm.CareDirectory.Application/Queries/SearchNearbyQuery.cs` (the `CareEntityDto`)
- Create: migrations in BOTH `Balsm.CareDirectory.Infrastructure/Migrations/` and `Balsm.CareDirectory.Infrastructure.Migrations.Sqlite/Migrations/`
- Modify: `tests/Modules/Balsm.CareDirectory.Tests/CareDirectorySearchTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `CarePlace.Create(...)` gains `externalId`, `source`, `confidence` and takes nullable `hours`, `phone`, `rating`, `nameAr`, `addressAr`. `CareEntityDto` exposes `string? NameAr`, `string? AddressAr`, `string? Hours`, `string? Phone`, `double? Rating`. Task 4 constructs `CarePlace` through this factory.

- [ ] **Step 1: Update the entity**

`CarePlace.cs` — new properties and relaxed nullability:

```csharp
public string? NameAr { get; private set; }
public string? AddressAr { get; private set; }
public string? Hours { get; private set; }
public string? Phone { get; private set; }
public double? Rating { get; private set; }

/// Overture GERS id. Null for curated-only rows that exist in no upstream source.
public string? ExternalId { get; private set; }

/// Provenance: overture | osm | curated | user.
public string Source { get; private set; } = "curated";

/// Upstream confidence, retained so the floor can be retuned without re-importing.
public double? Confidence { get; private set; }

public DateTime UpdatedAt { get; private set; }

/// Normalised Arabic name/address for search. See ArabicText.Normalize.
public string? NameArNorm { get; private set; }
public string? AddressArNorm { get; private set; }
```

Replace the `Create` factory with one taking the new shape, and add an `UpdateFromImport` method the importer uses for the upsert path. `UpdateFromImport` must NOT touch `Id` or `CreatedAt`, and must set `UpdatedAt = DateTime.UtcNow`.

**`NameArNorm` and `AddressArNorm` have private setters, so callers cannot
populate them.** Both `Create` and `UpdateFromImport` compute them internally:

```csharp
NameArNorm = ArabicText.Normalize(nameAr);
AddressArNorm = ArabicText.Normalize(addressAr);
```

That keeps the invariant "norm always matches its source column" inside the
entity, where it cannot drift. It makes `CarePlace` depend on `ArabicText` —
both live in the Domain assembly, so there is no new project reference, but it
does mean **Task 3 must land before Task 2 compiles.** Either implement
`ArabicText` first, or add it as a stub in Task 2 and fill it in Task 3.

- [ ] **Step 2: Delete the fabricated seed**

In `CarePlaceConfiguration.cs`, delete the entire `Seed` array, the `SeedTime` constant, and the `builder.HasData(...)` call. Add the new column mappings (`external_id`, `source`, `confidence`, `updated_at`, `name_ar_norm`, `address_ar_norm`), drop `IsRequired()` from `name_ar`, `address_ar`, `hours`, `phone`, and add:

```csharp
builder.HasIndex(x => x.ExternalId).IsUnique();
builder.HasIndex(x => x.NameArNorm);
builder.HasIndex(x => x.AddressArNorm);
```

- [ ] **Step 3: Generate both migration sets**

```bash
cd src/Modules/CareDirectory/Balsm.CareDirectory.Infrastructure
Database__Provider=postgresql dotnet ef migrations add CareDirectoryRealDataSchema
Database__Provider=sqlite dotnet ef migrations add CareDirectoryRealDataSchema \
  --project ../Balsm.CareDirectory.Infrastructure.Migrations.Sqlite
```

- [ ] **Step 4: Add the seed deletion to both migrations**

EF generates `DeleteData` calls for the removed `HasData` rows. **Verify they are present** — if the generated `Up` lacks them, add explicitly:

```csharp
migrationBuilder.Sql(
    "DELETE FROM care_place WHERE id::text LIKE '00000000-0000-0000-0001-%'");  // Npgsql
```

```csharp
migrationBuilder.Sql(
    "DELETE FROM care_place WHERE id LIKE '00000000-0000-0000-0001-%'");        // Sqlite
```

The two differ because SQLite has no `::text` cast and stores Guid as TEXT already.

- [ ] **Step 5: Rewrite the tests to own their fixtures**

`CareDirectorySearchTests` currently asserts `HaveCount(12)` and that `"El-Ezaby Pharmacy"` sorts first — it depends on production seed rows. Replace the fixture setup with explicit inserts. Fixture names must be obviously synthetic, never real institutions:

```csharp
private async Task SeedAsync(params (string Type, string Name, double Lat, double Lng)[] rows)
{
    foreach (var (type, name, lat, lng) in rows)
    {
        _db.CarePlaces.Add(CarePlace.Create(
            externalId: $"test-{name.ToLowerInvariant().Replace(' ', '-')}",
            type: type, nameEn: name, nameAr: null,
            addressEn: "Test Address", addressAr: null,
            lat: lat, lng: lng, hours: null, phone: null, rating: null,
            countryCode: "EG", source: "overture", confidence: 0.9));
    }
    await _db.SaveChangesAsync();
}

[Fact]
public async Task Search_SortsByDistanceAscending()
{
    await SeedAsync(
        ("pharmacy", "Fixture Pharmacy At Origin", OriginLat, OriginLng),
        ("hospital", "Fixture Hospital Far", 30.1300, 31.2830));

    var result = await new SearchNearbyHandler(_db).Handle(
        new SearchNearbyQuery(OriginLat, OriginLng, null, null, null), CancellationToken.None);

    result.Should().HaveCount(2);
    result[0].NameEn.Should().Be("Fixture Pharmacy At Origin");
    result[0].DistanceKm.Should().BeApproximately(0.0, 0.001);
}
```

Keep the existing type-filter and radius-filter tests, re-pointed at fixture rows.

- [ ] **Step 6: Update the handler's DTO projection**

`SearchNearbyHandler` constructs `CareEntityDto` from `x.Place.Hours`,
`.Phone`, `.Rating`, `.NameAr`, `.AddressAr` — all now nullable. The projection
compiles once `CareEntityDto`'s corresponding parameters are nullable (Task 2,
Step 1). No null-coalescing: a null must reach the wire as JSON `null`, not as
an empty string, so the client can tell "absent" from "empty".

- [ ] **Step 7: Run the tests**

```bash
cd Balsm-API-DotNet && dotnet test tests/Modules/Balsm.CareDirectory.Tests
```
Expected: PASS. Nullable-reference warnings from the DTO change must be resolved, not suppressed.

- [ ] **Step 8: Commit**

```bash
git add -A src/Modules/CareDirectory tests/Modules/Balsm.CareDirectory.Tests
git commit -m "[Fix] Care directory: delete the fabricated seed, add provenance columns"
```

---

### Task 3: Arabic search normalisation

**Files:**
- Create: `src/Modules/CareDirectory/Balsm.CareDirectory.Domain/ArabicText.cs`
- Create: `tests/Modules/Balsm.CareDirectory.Tests/ArabicTextTests.cs`
- Modify: `src/Modules/CareDirectory/Balsm.CareDirectory.Infrastructure/Handlers/SearchNearbyHandler.cs`

**Interfaces:**
- Consumes: `CarePlace.NameArNorm` / `AddressArNorm` from Task 2.
- Produces: `ArabicText.Normalize(string?) → string?`. Task 4's importer calls it to populate the norm columns.

**Why:** `EF.Functions.Like(p.NameAr, pattern)` compares raw strings. Egyptian listings spell the same word both ways — `أشعة` and `اشعة` — so a user searching one spelling never matches a facility stored under the other. This is a live defect independent of the import.

- [ ] **Step 1: Write the failing test**

`ArabicTextTests.cs`:

```csharp
public sealed class ArabicTextTests
{
    [Theory]
    [InlineData("أشعة", "اشعه")]      // hamza-above alif -> bare alif, ta marbuta -> ha
    [InlineData("اشعة", "اشعه")]      // already bare alif
    [InlineData("إشعاع", "اشعاع")]    // hamza-below alif
    [InlineData("آمنة", "امنه")]      // madda alif
    [InlineData("مستشفى", "مستشفي")]  // alif maqsura -> ya
    [InlineData("مـستـشفى", "مستشفي")] // tatweel stripped
    public void Normalize_FoldsOrthographicVariants(string input, string expected)
        => ArabicText.Normalize(input).Should().Be(expected);

    [Fact]
    public void Normalize_MakesBothSpellingsOfRadiologyEqual()
        => ArabicText.Normalize("مركز الشروق للأشعة")
            .Should().Be(ArabicText.Normalize("مركز الشروق للاشعة"));

    [Fact]
    public void Normalize_PassesLatinThroughLowercased()
        => ArabicText.Normalize("Cairo Scan").Should().Be("cairo scan");

    [Fact]
    public void Normalize_ReturnsNullForNull() => ArabicText.Normalize(null).Should().BeNull();
}
```

- [ ] **Step 2: Run it — expect a compile failure**

```bash
dotnet test tests/Modules/Balsm.CareDirectory.Tests --filter ArabicTextTests
```
Expected: FAIL — `ArabicText` does not exist.

- [ ] **Step 3: Implement**

`ArabicText.cs`:

```csharp
namespace Balsm.CareDirectory.Domain;

/// <summary>
/// Folds the orthographic variation Arabic search has to survive. Egyptian
/// listings spell the same word several ways — أشعة and اشعة both occur freely —
/// so raw string comparison silently drops a large share of matches.
/// </summary>
public static class ArabicText
{
    public static string? Normalize(string? value)
    {
        if (value is null) return null;

        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case 'أ':  // أ hamza above
                case 'إ':  // إ hamza below
                case 'آ':  // آ madda
                case 'ٱ':  // ٱ wasla
                    sb.Append('ا'); break;   // ا bare alif
                case 'ة': sb.Append('ه'); break;  // ة -> ه
                case 'ى': sb.Append('ي'); break;  // ى -> ي
                case 'ـ': break;                        // ـ tatweel: drop
                default:
                    // Drop combining diacritics (fatha/damma/kasra/shadda/sukun).
                    if (ch is >= 'ً' and <= 'ْ') break;
                    sb.Append(char.ToLowerInvariant(ch));
                    break;
            }
        }
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run — expect PASS**

```bash
dotnet test tests/Modules/Balsm.CareDirectory.Tests --filter ArabicTextTests
```

- [ ] **Step 5: Use it in the handler**

In `SearchNearbyHandler.Handle`, replace the free-text block with a normalised comparison. Match Arabic against the indexed norm columns and Latin against the raw ones:

```csharp
if (!string.IsNullOrWhiteSpace(query.Query))
{
    var raw = $"%{query.Query}%";
    var norm = $"%{ArabicText.Normalize(query.Query)}%";
    q = q.Where(p =>
        EF.Functions.Like(p.NameEn, raw) ||
        EF.Functions.Like(p.AddressEn, raw) ||
        (p.NameArNorm != null && EF.Functions.Like(p.NameArNorm, norm)) ||
        (p.AddressArNorm != null && EF.Functions.Like(p.AddressArNorm, norm)));
}
```

- [ ] **Step 6: Add a handler test proving the cross-spelling match**

```csharp
[Fact]
public async Task Search_MatchesArabicAcrossHamzaSpellings()
{
    await SeedArabicAsync(nameAr: "مركز الشروق للاشعة");   // stored without hamza

    var result = await new SearchNearbyHandler(_db).Handle(
        new SearchNearbyQuery(OriginLat, OriginLng, null, null, "أشعة"),  // searched with hamza
        CancellationToken.None);

    result.Should().HaveCount(1);
}
```

`SeedArabicAsync` must populate `NameArNorm` via `ArabicText.Normalize`, exactly as the importer will.

- [ ] **Step 7: Run all module tests, then commit**

```bash
dotnet test tests/Modules/Balsm.CareDirectory.Tests
git add -A && git commit -m "[Fix] Care directory: normalise Arabic for search across hamza spellings"
```

---

### Task 4: Import the artifact on startup

**Files:**
- Create: `src/.../Balsm.CareDirectory.Infrastructure/Import/CareDirectoryOptions.cs`
- Create: `src/.../Balsm.CareDirectory.Infrastructure/Import/CareDirectoryImporter.cs`
- Create: `src/.../Balsm.CareDirectory.Infrastructure/Import/CareDirectoryImportService.cs`
- Modify: `src/.../Balsm.CareDirectory.Infrastructure/DependencyInjection.cs`
- Modify: `src/.../Balsm.CareDirectory.Infrastructure/Balsm.CareDirectory.Infrastructure.csproj`
- Modify: `src/Balsm.API/appsettings.json`
- Create: `tests/Modules/Balsm.CareDirectory.Tests/CareDirectoryImporterTests.cs`
- Create: `tests/Modules/Balsm.CareDirectory.Tests/Fixtures/care_places.sample.ndjson`

**Interfaces:**
- Consumes: the NDJSON line schema from Task 1; `CarePlace.Create` / `UpdateFromImport` from Task 2; `ArabicText.Normalize` from Task 3.
- Produces: `CareDirectoryImporter.ImportAsync(Stream, CancellationToken) → Task<ImportResult>` where `ImportResult` is `record ImportResult(int Inserted, int Updated, int Skipped)`.

**Why a hosted service, not a CLI:** the care directory lives in the local `balsm.db` and the deployment is self-hosted. Asking an operator to run an import command by hand would leave the map empty on any install that skipped it. `MigrationRunner` already auto-migrates on boot; the import follows the same pattern.

- [ ] **Step 1: Write the options type**

```csharp
namespace Balsm.CareDirectory.Infrastructure.Import;

public sealed class CareDirectoryOptions
{
    public const string SectionName = "CareDirectory";

    /// Import on startup. Off in tests, which drive the importer directly.
    public bool ImportOnStartup { get; set; } = true;

    /// Artifact path, relative to the content root.
    public string ArtifactPath { get; set; } = "data/care-directory/care_places.eg.ndjson.gz";

    /// Rows below this are dropped. Overture's category taxonomy is noisy —
    /// a sampled vet clinic typed `hospital` scored 0.70, junk scored 0.30–0.59.
    public double MinConfidence { get; set; } = 0.65;

    /// Per-type overrides. One global floor serves the types badly in opposite
    /// directions: pharmacy loses 62% of its rows while scan is starved at 177.
    public Dictionary<string, double> MinConfidenceByType { get; set; } = new();

    public double FloorFor(string type) =>
        MinConfidenceByType.TryGetValue(type, out var v) ? v : MinConfidence;
}
```

- [ ] **Step 2: Write the failing importer test**

`Fixtures/care_places.sample.ndjson` — 6 uncompressed lines, real Overture-shaped values but obviously-fixture names, covering: one high-confidence row, one below the global floor, one `scan` row below the global floor but above a `scan` override, one Arabic-named row, one bilingual brand row, one row with a null phone.

```csharp
[Fact]
public async Task Import_SkipsRowsBelowTheConfidenceFloor()
{
    var opts = new CareDirectoryOptions { MinConfidence = 0.65 };
    var result = await new CareDirectoryImporter(_db, opts).ImportAsync(OpenFixture(), default);

    result.Skipped.Should().BeGreaterThan(0);
    _db.CarePlaces.Should().OnlyContain(p => p.Confidence >= 0.65);
}

[Fact]
public async Task Import_AppliesPerTypeFloor()
{
    var opts = new CareDirectoryOptions
    {
        MinConfidence = 0.65,
        MinConfidenceByType = new() { ["scan"] = 0.50 },
    };
    await new CareDirectoryImporter(_db, opts).ImportAsync(OpenFixture(), default);

    _db.CarePlaces.Should().Contain(p => p.Type == "scan" && p.Confidence < 0.65);
}

[Fact]
public async Task Import_IsIdempotent()
{
    var opts = new CareDirectoryOptions();
    await new CareDirectoryImporter(_db, opts).ImportAsync(OpenFixture(), default);
    var first = await _db.CarePlaces.CountAsync();

    var second = await new CareDirectoryImporter(_db, opts).ImportAsync(OpenFixture(), default);

    (await _db.CarePlaces.CountAsync()).Should().Be(first, "re-import upserts, never duplicates");
    second.Inserted.Should().Be(0);
    second.Updated.Should().Be(first);
}

[Fact]
public async Task Import_SplitsBilingualBrandNames()
{
    await new CareDirectoryImporter(_db, new CareDirectoryOptions()).ImportAsync(OpenFixture(), default);

    var row = await _db.CarePlaces.SingleAsync(p => p.ExternalId == "fixture-bilingual-brand");
    row.NameEn.Should().Be("Misr Pharmacies");
    row.NameAr.Should().Be("صيدليات مصر");
}

[Fact]
public async Task Import_PopulatesNormalisedArabicColumns()
{
    await new CareDirectoryImporter(_db, new CareDirectoryOptions()).ImportAsync(OpenFixture(), default);

    var row = await _db.CarePlaces.SingleAsync(p => p.ExternalId == "fixture-arabic");
    row.NameArNorm.Should().Be(ArabicText.Normalize(row.NameAr));
}
```

- [ ] **Step 3: Run — expect failure**

```bash
dotnet test tests/Modules/Balsm.CareDirectory.Tests --filter CareDirectoryImporterTests
```
Expected: FAIL — `CareDirectoryImporter` does not exist.

- [ ] **Step 4: Implement the importer**

`CareDirectoryImporter.ImportAsync` reads the stream line by line (gzip-decompressed when the path ends `.gz`), deserialises each line, and for each row:

1. Skip when `confidence < opts.FloorFor(type)`.
2. Resolve names. `name_script == "en"` → `NameEn = name`, `NameAr = null`; `"ar"` → `NameAr = name`, `NameEn = null`. When `brand` contains BOTH scripts, split on the script boundary and use the halves for `NameEn`/`NameAr` — trim separators (`-`, `–`, whitespace) from each side.
3. `NameArNorm = ArabicText.Normalize(NameAr)`, likewise `AddressArNorm`.
4. Upsert on `ExternalId`: existing row → `UpdateFromImport`, else `CarePlace.Create`.

**`NameEn` and `NameAr` are both nullable now, but at least one must be non-null** — assert this and count a violating row as `Skipped`.

Process in batches of 500 with a single `SaveChangesAsync` per batch; 38k individual saves would take minutes.

- [ ] **Step 5: Run — expect PASS**

- [ ] **Step 6: Wire the hosted service and config**

`CareDirectoryImportService : IHostedService` resolves a scope, reads the artifact from `IHostEnvironment.ContentRootPath`, and runs the importer — but only when `ImportOnStartup` is true and the artifact exists. A missing artifact logs a warning and continues; it must NOT crash the host.

**Ordering matters:** register it AFTER `MigrationRunner` so the schema exists. Hosted services start in registration order, and `MigrationRunner` is registered in `Balsm.Infrastructure.DependencyInjection` — verify at runtime that the import log line follows "All migrations complete".

In `DependencyInjection.AddCareDirectoryInfrastructure`:

```csharp
services.Configure<CareDirectoryOptions>(configuration.GetSection(CareDirectoryOptions.SectionName));
services.AddHostedService<CareDirectoryImportService>();
```

In the csproj, ship the artifact:

```xml
<ItemGroup>
  <Content Include="..\..\..\..\data\care-directory\**" Link="data\care-directory\%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

In `appsettings.json`:

```json
"CareDirectory": {
  "ImportOnStartup": true,
  "MinConfidence": 0.65,
  "MinConfidenceByType": { "scan": 0.50 }
}
```

- [ ] **Step 7: Verify end to end**

```bash
cd src/Balsm.API && dotnet run
curl -s "http://localhost:5000/care/entities?lat=30.0444&lng=31.2357&radius_km=5" | python3 -m json.tool | head -40
```
Expected: real Cairo facilities, distance-sorted, none of them named `Qasr Al-Aini Hospital` with a `+20 2 2365 1234` phone. Confirm the import log line reports ~18,850 inserted.

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "[Feature] Care directory: import the Overture artifact on startup"
```

---

### Task 5: Seventh entity type — dentist

**Files:**
- Modify: `app/lib/balsm_app/care/care_entity.dart`
- Modify: `app/test/care_filter_test.dart`

**Interfaces:**
- Consumes: the `dentist` wire value the importer writes (Task 1/4).
- Produces: `CareEntityType.dentist` for the filter menu, which enumerates `CareEntityType.values` at `map_screen.dart:664` and therefore picks it up with no further change.

**Design note — needs confirmation:** `lucide_icons 0.257.0` has no tooth glyph. Candidates present in the package: `smile`, `smilePlus`, `bone`, `syringe`, `microscope`. This plan uses `LucideIcons.smile` and a rose colour pair, but **DesignSync is the source of truth for Balsm iconography and palette, and the local reference-prototype is stale** — confirm both before shipping, and treat the values below as placeholders for the design decision, not for the code.

- [ ] **Step 1: Write the failing test**

In `care_filter_test.dart`, add a dentist fixture and a filter assertion:

```dart
_entity('d', CareEntityType.dentist, en: 'Smile Dental', ar: 'عيادة الابتسامة'),
```

```dart
test('dentist filters independently of clinic', () {
  final r = filterCareEntities(all, query: '', types: {CareEntityType.dentist});
  expect(ids(r), ['d'], reason: 'dentists are their own type, not a clinic subset');
});

test('dentist maps from its wire value', () {
  expect(CareEntityType.fromWire('dentist'), CareEntityType.dentist);
});
```

- [ ] **Step 2: Run — expect failure**

```bash
cd app && fvm flutter test test/care_filter_test.dart
```
Expected: FAIL — `CareEntityType.dentist` is not defined.

- [ ] **Step 3: Add the enum entry**

In `care_entity.dart`, between `clinic` and `pharmacy` so the filter chips keep a sensible order:

```dart
dentist('dentist', LucideIcons.smile, 0xFFE0568F, 0xFFFCE9F1, (en: 'Dentists', ar: 'أطباء أسنان')),
```

Labels live in the enum as `L10nText`, not in the i69n bundle, so no `build_runner` regeneration is needed.

- [ ] **Step 4: Run — expect PASS**

```bash
fvm flutter test test/care_filter_test.dart
```

Note the existing test `every type ticked matches the all-types result` uses `CareEntityType.values.toSet()` and so covers the new type automatically.

- [ ] **Step 5: Commit**

```bash
git add app/lib/balsm_app/care/care_entity.dart app/test/care_filter_test.dart
git commit -m "[Feature] Care map: dentist as a seventh entity type"
```

---

### Task 6: Accept nullable fields in the API client

**Files:**
- Modify: `packages/balsm_api/lib/src/care_directory/responses.dart`
- Modify: `app/lib/balsm_app/care/care_entity.dart` (`CareEntity.fromResponse`)
- Create: `packages/balsm_api/test/care_directory/responses_test.dart`

**Interfaces:**
- Consumes: the nullable `CareEntityDto` wire shape from Task 2.
- Produces: `CareEntityResponse` with `String? nameAr, addressAr, hours, phone`. Task 7 depends on `CareEntity` mapping those to `''`.

**Why this task exists and must precede Task 7:** `CareEntityResponse.fromJson`
currently does `json['name_ar'] as String`. A JSON `null` fails that cast and
**throws**, so the moment the API starts returning nullable fields, every
care-directory response fails to parse and the map goes blank with an error —
not a degraded render, a hard failure. This is the one ordering constraint in
the plan that cannot be reordered.

- [ ] **Step 1: Write the failing test**

`packages/balsm_api/test/care_directory/responses_test.dart`:

```dart
void main() {
  test('parses a row with no Arabic name, hours, phone or rating', () {
    // Overture supplies one name per place and has no hours or ratings field
    // at all, so a majority of real rows arrive with nulls here.
    final r = CareEntityResponse.fromJson(const {
      'id': 'fixture-1',
      'type': 'pharmacy',
      'name_en': 'Fixture Pharmacy',
      'name_ar': null,
      'address_en': 'Fixture Street',
      'address_ar': null,
      'lat': 30.0444,
      'lng': 31.2357,
      'hours': null,
      'phone': null,
      'distance_km': 0.6,
      'rating': null,
    });

    expect(r.nameEn, 'Fixture Pharmacy');
    expect(r.nameAr, isNull);
    expect(r.hours, isNull);
    expect(r.phone, isNull);
    expect(r.rating, isNull);
  });

  test('parses an Arabic-only row', () {
    final r = CareEntityResponse.fromJson(const {
      'id': 'fixture-2', 'type': 'lab',
      'name_en': null, 'name_ar': 'معمل الاختبار',
      'address_en': null, 'address_ar': 'شارع الاختبار',
      'lat': 30.05, 'lng': 31.23,
      'hours': null, 'phone': null, 'distance_km': null, 'rating': null,
    });

    expect(r.nameEn, isNull);
    expect(r.nameAr, 'معمل الاختبار');
  });
}
```

- [ ] **Step 2: Run — expect failure**

```bash
cd packages/balsm_api && fvm dart test test/care_directory/responses_test.dart
```
Expected: FAIL — `type 'Null' is not a subtype of type 'String' in type cast`.

- [ ] **Step 3: Make the fields nullable**

In `responses.dart`, change `nameAr`, `addressAr`, `hours`, `phone` — and
`nameEn`, `addressEn`, since an Arabic-only row has no English side — to
`String?`, and parse with `as String?`:

```dart
nameEn: json['name_en'] as String?,
nameAr: json['name_ar'] as String?,
addressEn: json['address_en'] as String?,
addressAr: json['address_ar'] as String?,
hours: json['hours'] as String?,
phone: json['phone'] as String?,
```

Drop `required` from those constructor parameters.

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Map nulls to empty strings at the domain boundary**

`CareEntity` keeps non-null `String` fields — the UI's emptiness checks in
Task 7 are written against `''`, and one nullability convention per layer is
easier to reason about than two. In `CareEntity.fromResponse`:

```dart
name: (en: r.nameEn ?? '', ar: r.nameAr ?? ''),
addr: (en: r.addressEn ?? '', ar: r.addressAr ?? ''),
hours: r.hours ?? '',
phone: r.phone ?? '',
```

- [ ] **Step 6: Run the app suite and the E2E fake's tests**

```bash
cd packages/core && fvm flutter test test/test_kit/fake_apis_test.dart
cd ../../app && fvm flutter test
```

`FakeCareDirectoryApi` constructs `CareEntityResponse` with all fields
populated; dropping `required` keeps it compiling unchanged. It stays as it is —
its rows are correctly synthetic and must not be re-derived from real data.

- [ ] **Step 7: Commit**

```bash
git add -A packages/balsm_api app/lib/balsm_app/care/care_entity.dart
git commit -m "[Fix] Care directory client: accept nullable name, hours and phone"
```

---

### Task 7: Stop rendering fields that have no source

**Files:**
- Modify: `app/lib/balsm_app/care/care_entity.dart` (the `pick` function)
- Modify: `app/lib/balsm_app/screens/map_screen.dart` (lines ~239–240, ~293–295, and the tile layer)
- Create: `app/test/care_entity_test.dart`

**Interfaces:**
- Consumes: `CareEntity.rating` / `.hours` / `.phone`, which Task 6 maps to `''` when the API omits them.
- Produces: nothing downstream.

**Why:** `map_screen.dart:295` renders `'${e.rating} / 5'` unconditionally. With ratings gone the detail sheet shows a bare `" / 5"`. Removing fabricated ratings *exposes* this, so the fix ships with the import, not after.

- [ ] **Step 1: Write the failing test for the language fallback**

`app/test/care_entity_test.dart`:

```dart
void main() {
  test('pick falls back to the other language when one is missing', () {
    // Overture carries ONE name per place — never a bilingual pair — so an
    // Arabic-only facility must still render in the English UI under its real
    // name rather than as a blank.
    const arabicOnly = (en: '', ar: 'صيدليات الشلقاني');
    expect(pick(arabicOnly, ar: false), 'صيدليات الشلقاني');
    expect(pick(arabicOnly, ar: true), 'صيدليات الشلقاني');

    const englishOnly = (en: 'Cairo Scan Center', ar: '');
    expect(pick(englishOnly, ar: true), 'Cairo Scan Center');
  });

  test('pick prefers the requested language when both exist', () {
    const both = (en: 'Misr Pharmacies', ar: 'صيدليات مصر');
    expect(pick(both, ar: true), 'صيدليات مصر');
    expect(pick(both, ar: false), 'Misr Pharmacies');
  });
}
```

- [ ] **Step 2: Run — expect failure**

```bash
fvm flutter test test/care_entity_test.dart
```
Expected: FAIL — current `pick` returns `''` for the missing side.

- [ ] **Step 3: Implement the fallback**

```dart
/// Resolves a bilingual label, falling back to the other language when the
/// requested one is absent. Overture supplies one name per place — never a
/// pair — so showing a facility's real Arabic name in the English UI is
/// correct behaviour, and better than a blank row.
String pick(L10nText t, {required bool ar}) {
  final want = ar ? t.ar : t.en;
  if (want.isNotEmpty) return want;
  return ar ? t.en : t.ar;
}
```

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Make the meta rows conditional**

In `map_screen.dart`, the list card (~239–240) and the detail sheet (~293–295) render hours and rating unconditionally. Guard both:

```dart
if (e.hours.isNotEmpty) _metaBit(LucideIcons.clock, e.hours, s),
if (e.rating.isNotEmpty) _metaBit(LucideIcons.star, e.rating, s, star: true),
```

```dart
if (e.hours.isNotEmpty) _detailRow(LucideIcons.clock, e.hours, s),
if (e.rating.isNotEmpty) _detailRow(LucideIcons.star, '${e.rating} / 5', s, star: true),
```

Same treatment for `phone`, which is now null for ~9% of rows — a call button that dials nothing is worse than an absent one.

- [ ] **Step 6: Add the OpenStreetMap attribution**

The tile layer at `map_screen.dart:459` renders `tile.openstreetmap.org` with no credit. ODbL requires attribution, and this is owed today regardless of this project. Add `flutter_map`'s attribution widget below the `TileLayer`:

```dart
const RichAttributionWidget(
  attributions: [TextSourceAttribution('OpenStreetMap contributors')],
),
```

Once Phase 2 merges OSM place data, add `© Overture Maps Foundation` alongside it.

- [ ] **Step 7: Run the app suite**

```bash
cd app && fvm flutter test
```
Expected: PASS. Widget tests that assert on a rating row need re-pointing at a fixture that has one.

- [ ] **Step 8: Commit**

```bash
git add -A app/lib app/test
git commit -m "[Fix] Care map: hide unsourced fields, fall back across languages, credit OSM"
```

---

## Verification

After all seven tasks:

1. `cd Balsm-API-DotNet && dotnet test` — full backend suite green.
2. `cd balsm_app && scripts/all_tests.sh` — note the pre-existing failures recorded before this work: 20 golden failures in `packages/core`, 3 in the repo-root `sentry_allowlist_test`, and a flaky `InMemoryRateLimitStoreTests.TryConsume_ConcurrentCalls_NeverExceedLimit` (~1 in 8). Anything beyond those is a regression from this plan.
3. Boot the API, hit `GET /care/entities?lat=30.0444&lng=31.2357&radius_km=5`, and confirm real facilities with no fabricated phone numbers.
4. Run the app against it and confirm: seven filter types, pins on the map, no bare `" / 5"`, and OSM attribution visible.
5. `SELECT count(*) FROM care_place WHERE id LIKE '00000000-0000-0000-0001-%'` returns 0.

## Out of scope (Phases 2–4)

OSM merge for `name_ar` and `opening_hours`; the `care_place_override` curated overlay; `care_place_report` user corrections. All three are specced in the design doc and none of them block Phase 1.
