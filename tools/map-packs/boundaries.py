#!/usr/bin/env python3
"""Fetch Egypt's 27 governorate boundaries from OSM and write them as GeoJSON.

Run rarely and commit the result. Boundaries change on the order of years, an
Overpass outage should not break a map-pack build, and a boundary silently
changing between builds would silently change what a pack covers.

OSM data is ODbL — the same licence as the tiles these boundaries cut, so one
attribution covers both: "© OpenStreetMap contributors".

Usage:   python3 tools/map-packs/boundaries.py [--out egypt-governorates.geojson]
Requires: nothing beyond the standard library.
"""
import argparse
import json
import pathlib
import subprocess
import sys
import time

from _rings import to_multipolygon

OVERPASS = "https://overpass-api.de/api/interpreter"

# Egypt's governorates are admin_level=4 relations inside the admin_level=2
# country area. There are 27; a run returning any other count means the query
# or the data moved, and is treated as a failure rather than silently shipping
# a pack set with a hole in it.
EXPECTED = 27

META_QUERY = """
[out:json][timeout:180];
area["ISO3166-1"="EG"]["admin_level"="2"]->.eg;
relation(area.eg)["boundary"="administrative"]["admin_level"="4"];
out tags;
"""

# Overpass names are inconsistent for several governorates ("Lake" for
# Beheira, "Aj Jiza" for Giza). The Arabic names are correct throughout, so
# only English is overridden, keyed by OSM relation id so a name edit upstream
# cannot silently re-point one of these.
ENGLISH_OVERRIDES = {
    3824513: "Beheira",
    3824206: "Giza",
    3726170: "Beni Suef",
    3726184: "Asyut",
    3726186: "Sohag",
    3726175: "Minya",
    3062184: "Ismailia",
    4103337: "Qalyubia",
    3584607: "Gharbia",
    3824207: "Monufia",
    4103403: "Dakahlia",
    4103407: "Sharqia",
    3061826: "Matrouh",
    3061827: "New Valley",
}

# A stable, url-safe id per governorate; the app and the manifest key on this
# rather than on a name, which is translated and may be re-spelled upstream.
def slug(name: str) -> str:
    return "".join(c if c.isalnum() else "-" for c in name.lower()).strip("-")


def overpass(query: str, attempts: int = 4) -> dict:
    """POST to Overpass, retrying: the public instance 504s under load, and a
    map-pack build should not fail because someone else was running a big
    query at the time."""
    for attempt in range(1, attempts + 1):
        proc = subprocess.run(
            ["curl", "-s", "-X", "POST", "--data-binary", query, OVERPASS],
            capture_output=True, text=True,
        )
        try:
            return json.loads(proc.stdout)
        except json.JSONDecodeError:
            if attempt == attempts:
                sys.exit(f"overpass failed after {attempts} attempts: {proc.stdout[:200]}")
            wait = 8 * attempt
            print(f"  overpass retry {attempt}/{attempts - 1} in {wait}s", file=sys.stderr)
            time.sleep(wait)
    raise AssertionError("unreachable")


def main() -> None:
    ap = argparse.ArgumentParser()
    here = pathlib.Path(__file__).parent
    ap.add_argument("--out", type=pathlib.Path, default=here / "egypt-governorates.geojson")
    ap.add_argument("--elements", type=pathlib.Path,
                    help="assemble from a saved Overpass `out geom` response instead of "
                         "querying. The public instance throttles hard, and a half-fetched "
                         "run should not mean starting over.")
    ap.add_argument("--save-elements", type=pathlib.Path,
                    help="write the raw Overpass response alongside the GeoJSON")
    args = ap.parse_args()

    if args.elements:
        elements = json.loads(args.elements.read_text())["elements"]
        print(f"assembling from {args.elements} — {len(elements)} relations")
    else:
        print("fetching governorate list…")
        meta = overpass(META_QUERY)
        ids = [e["id"] for e in meta["elements"]]
        if len(ids) != EXPECTED:
            sys.exit(f"expected {EXPECTED} governorates, got {len(ids)} — "
                     "check the Overpass query before shipping packs")

        # Batched: `out geom` for all 27 at once reliably times out on the
        # public instance. Five is comfortably under the limit.
        elements = []
        for i in range(0, len(ids), 5):
            batch = ids[i:i + 5]
            print(f"  geometry {i + 1}-{i + len(batch)} of {len(ids)}")
            got = overpass(f"[out:json][timeout:180];relation(id:{','.join(map(str, batch))});out geom;")
            elements.extend(got["elements"])
            time.sleep(3)
        if args.save_elements:
            args.save_elements.write_text(json.dumps({"elements": elements}))

    features, problems = [], []
    for rel in elements:
        tags = rel.get("tags", {})
        polys, unclosed = to_multipolygon(rel)
        name_en = ENGLISH_OVERRIDES.get(rel["id"]) or tags.get("name:en") or str(rel["id"])
        if polys is None:
            problems.append(f"{name_en}: no closed outer ring")
            continue
        if unclosed:
            problems.append(f"{name_en}: {unclosed} unclosed ring(s) dropped")
        features.append({
            "type": "Feature",
            "properties": {
                "id": slug(name_en),
                "osm_id": rel["id"],
                "name_en": name_en,
                "name_ar": tags.get("name", ""),
            },
            "geometry": {"type": "MultiPolygon", "coordinates": polys},
        })

    if problems:
        for p in problems:
            print(f"  ! {p}", file=sys.stderr)
    if len(features) != EXPECTED:
        sys.exit(f"assembled {len(features)} of {EXPECTED} governorates — refusing to write")

    features.sort(key=lambda f: f["properties"]["id"])
    args.out.write_text(json.dumps({"type": "FeatureCollection", "features": features}))
    print(f"wrote {args.out} — {len(features)} governorates")


if __name__ == "__main__":
    main()
