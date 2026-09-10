#!/usr/bin/env python3
"""Extract Egyptian health places from Overture Maps into an import artifact.

Offline and reproducible: pin the release, run, commit the result. The API never
talks to S3 — it only ever reads the artifact this produces, so CI needs no
network and the exact bytes that populated production stay auditable.

Overture Places is CDLA-Permissive-2.0: storage, redistribution and commercial
use are permitted. Attribution "© Overture Maps Foundation" travels with the
data (see README.md).

Usage:   python3 tools/care-directory/extract.py [--release ...] [--country EG]
Requires: pip install duckdb
"""
import argparse
import gzip
import hashlib
import json
import pathlib
import sys

import duckdb

OVERTURE_RELEASE = "2026-08-19.0"

# Coarse pre-filter so DuckDB can prune row groups before the country test.
# Deliberately wider than Egypt; addresses[1].country does the exact cut, which
# measured ~4.9% of rows inside this box as PS/IL/JO/SA/SD.
BBOX = "bbox.xmin BETWEEN 24.7 AND 36.9 AND bbox.ymin BETWEEN 21.9 AND 31.7"

# Imaging keywords route diagnostic_services/laboratory_testing rows to `scan`.
# Both Arabic spellings of أشعة/اشعة appear because Egyptian listings use each
# freely and DuckDB's ILIKE does not fold hamza forms.
IMAGING = [
    "scan", "radiolog", "imaging", "x-ray", "xray", "mri", "tomograph",
    "ultrasound", "sonar", "doppler",
    "أشعة", "اشعة", "أشعه", "اشعه", "سكان", "رنين", "سونار", "مقطعية", "دوبلر", "تصوير",
]

DENTAL = ("dentist", "cosmetic_dentist", "general_dentistry", "orthodontist",
          "pediatric_dentist", "oral_surgeon")

# Wholesalers, research and teaching are not places a patient can walk into.
EXCLUDED = ("pharmaceutical_companies", "medical_research_and_development", "medical_school")


def build_sql(release: str, country: str) -> str:
    img = " OR ".join(f"names.primary ILIKE '%{t}%'" for t in IMAGING)
    dental = ", ".join(f"'{c}'" for c in DENTAL)
    excluded = ", ".join(f"'{c}'" for c in EXCLUDED)
    src = f"s3://overturemaps-us-west-2/release/{release}/theme=places/type=place/*.parquet"
    # The type CASE is wrapped in a CTE because a SELECT alias is not referenceable
    # from its own WHERE clause.
    return f"""
WITH mapped AS (
  SELECT
    id                                                   AS external_id,
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
    END                                                  AS type,
    names.primary                                        AS name,
    CASE WHEN regexp_matches(names.primary, '[؀-ۿ]') THEN 'ar' ELSE 'en' END AS name_script,
    addresses[1].freeform                                AS address,
    ST_Y(geometry)                                       AS lat,
    ST_X(geometry)                                       AS lng,
    phones[1]                                            AS phone,
    confidence,
    brand.names.primary                                  AS brand,
    'overture'                                           AS source
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
    # One scan: take the cursor's description rather than re-running for columns.
    cur = con.execute(build_sql(args.release, args.country))
    cols = [d[0] for d in cur.description]
    rows = cur.fetchall()
    print(f"  {len(rows)} rows", file=sys.stderr)
    if not rows:
        print("refusing to write an empty artifact", file=sys.stderr)
        return 1

    out_dir = pathlib.Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)
    artifact = out_dir / f"care_places.{args.country.lower()}.ndjson.gz"

    # mtime=0 keeps the gzip byte-identical across runs over identical data, so a
    # re-extract that changes nothing produces no diff.
    with gzip.GzipFile(artifact, "wb", mtime=0) as fh:
        for row in rows:
            rec = dict(zip(cols, row))
            fh.write((json.dumps(rec, ensure_ascii=False, sort_keys=True) + "\n").encode("utf-8"))

    digest = hashlib.sha256(artifact.read_bytes()).hexdigest()
    (out_dir / "manifest.json").write_text(json.dumps({
        "overture_release": args.release,
        "country": args.country,
        "row_count": len(rows),
        "artifact": artifact.name,
        "sha256": digest,
        "license": "CDLA-Permissive-2.0",
        "attribution": "© Overture Maps Foundation",
    }, indent=2) + "\n", encoding="utf-8")

    print(f"wrote {artifact} ({len(rows)} rows, sha256 {digest[:12]}…)", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
