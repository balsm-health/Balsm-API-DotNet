# Governorate registry

`governorates.json` is the reference list the nightly map-pack export job
(`Balsm.CareDirectory.Infrastructure.MapPacks`) iterates: one entry per
Egyptian governorate, with the id, bilingual name, and bounding box it needs
to export a places snapshot and to match basemap object keys in the R2
bucket back to a governorate.

It is derived data, not hand-written — the ids, names, and bounds come from
`tools/map-packs/egypt-governorates.geojson` (OSM `admin_level=4` boundaries,
ODbL) by way of `tools/map-packs/build_packs.py`, which computes each
governorate's bounding box while cutting its basemap tiles.

Regenerate it after a boundary refresh:

```bash
python3 tools/map-packs/build_packs.py   # writes tools/map-packs/build/manifest.json
python3 - <<'PY'
import json
d = json.load(open("tools/map-packs/build/manifest.json"))
rows = sorted(
    ({"id": p["id"], "name_en": p["name_en"], "name_ar": p["name_ar"],
      "west": p["bounds"][0], "south": p["bounds"][1],
      "east": p["bounds"][2], "north": p["bounds"][3]} for p in d["packs"]),
    key=lambda r: r["id"])
json.dump(rows, open("data/map-packs/governorates.json", "w", encoding="utf-8"),
           ensure_ascii=False, indent=2)
PY
```

Boundaries move on the order of years (see `boundaries.py`'s own docstring),
so this is a rare, reviewed regeneration — not something either pipeline
does automatically.
