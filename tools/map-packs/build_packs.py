#!/usr/bin/env python3
"""Build one offline map pack per Egyptian governorate.

Pipeline:
    protomaps planet (remote)
      │  pmtiles extract --region=egypt              ← ONE remote read
      ▼
    build/egypt.pmtiles
      │  pmtiles extract --region=<governorate>      ← x27, local
      ▼
    build/packs/<id>-<version>.pmtiles  + manifest.json

Protomaps asks that builds not be hotlinked, so Egypt is pulled once and every
governorate is cut from that local file. Re-running with build/egypt.pmtiles
already present skips the remote step entirely.

Tiles are an ODbL Produced Work of OpenStreetMap: redistributable, attribution
required. "© OpenStreetMap contributors" must be visible wherever these render.

Usage:   python3 tools/map-packs/build_packs.py [--maxzoom 15] [--only cairo,giza]
Requires: the `pmtiles` CLI (brew install pmtiles)
"""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import time

HERE = pathlib.Path(__file__).parent
BOUNDARIES = HERE / "egypt-governorates.geojson"

# Protomaps' daily planet basemap. The date is the archive's own name; the data
# date comes from the archive metadata (see `_source_date`), which is NOT the
# same thing.
PLANET = "https://build.protomaps.com/{date}.pmtiles"


def run(cmd: list[str], quiet: bool = True) -> str:
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.returncode != 0:
        sys.exit(f"failed: {' '.join(cmd[:3])}…\n{proc.stderr[-800:]}")
    return proc.stdout


def _source_date(archive: pathlib.Path) -> str:
    """The date of the OSM data inside [archive], as YYYYMMDD.

    Read from `planetiler:osm:osmosisreplicationtime`, NOT from
    `planetiler:buildtime`: the latter is inherited from the build image and
    can be months older than the data, which would have the app tell people
    their map is stale when it is hours old.
    """
    meta = json.loads(run(["pmtiles", "show", str(archive), "--metadata"]))
    stamp = meta.get("planetiler:osm:osmosisreplicationtime")
    if not stamp:
        sys.exit("archive has no osmosisreplicationtime — cannot date the pack honestly")
    return stamp[:10].replace("-", "")


def _bounds(archive: pathlib.Path) -> list[float]:
    """[w, s, e, n] — lets the app tell whether a pack covers the viewport
    without opening it."""
    out = run(["pmtiles", "show", str(archive)])
    for line in out.splitlines():
        if line.startswith("bounds:"):
            nums = [float(t.strip("(),")) for t in line.replace("long:", "").replace("lat:", "").split()
                    if t.strip("(),").replace(".", "").replace("-", "").isdigit()]
            if len(nums) == 4:
                return [nums[0], nums[1], nums[2], nums[3]]
    sys.exit(f"no bounds in {archive}")


def sha256(path: pathlib.Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--date", default=time.strftime("%Y%m%d"),
                    help="which daily planet build to pull (default: today)")
    ap.add_argument("--maxzoom", type=int, default=15,
                    help="15 is the source maximum. 14 roughly halves every pack; vector "
                         "tiles over-zoom, so 14 still renders past z14 with less detail.")
    ap.add_argument("--only", help="comma-separated governorate ids, for a quick run")
    ap.add_argument("--out", type=pathlib.Path, default=HERE / "build")
    args = ap.parse_args()

    if not shutil.which("pmtiles"):
        sys.exit("pmtiles CLI not found — brew install pmtiles")
    if not BOUNDARIES.exists():
        sys.exit(f"no {BOUNDARIES.name} — run boundaries.py first")

    out = args.out
    packs_dir = out / "packs"
    packs_dir.mkdir(parents=True, exist_ok=True)
    egypt = out / "egypt.pmtiles"

    # ── one remote read ───────────────────────────────────────────────────
    if egypt.exists():
        print(f"reusing {egypt} ({egypt.stat().st_size >> 20} MB)")
    else:
        print(f"pulling Egypt from the {args.date} planet build (one remote read)…")
        run(["pmtiles", "extract", PLANET.format(date=args.date), str(egypt),
             f"--region={BOUNDARIES}", f"--maxzoom={args.maxzoom}"], quiet=False)
        print(f"  {egypt.stat().st_size >> 20} MB")

    version = _source_date(egypt)
    print(f"OSM data date: {version}")

    features = json.loads(BOUNDARIES.read_text())["features"]
    wanted = set(args.only.split(",")) if args.only else None

    manifest = []
    for feat in features:
        gid = feat["properties"]["id"]
        if wanted and gid not in wanted:
            continue

        region = out / f"{gid}.geojson"
        region.write_text(json.dumps({"type": "FeatureCollection", "features": [feat]}))
        pack = packs_dir / f"{gid}-{version}.pmtiles"

        if pack.exists():
            print(f"  {gid}: already built")
        else:
            run(["pmtiles", "extract", str(egypt), str(pack),
                 f"--region={region}", f"--maxzoom={args.maxzoom}"])
        region.unlink()

        size = pack.stat().st_size
        manifest.append({
            "id": gid,
            "name_en": feat["properties"]["name_en"],
            "name_ar": feat["properties"]["name_ar"],
            "version": version,
            "size_bytes": size,
            "sha256": sha256(pack),
            "bounds": _bounds(pack),
            "file": pack.name,
        })
        print(f"  {gid}: {size >> 20} MB")

    manifest.sort(key=lambda m: m["id"])
    (out / "manifest.json").write_text(json.dumps({"packs": manifest}, ensure_ascii=False, indent=2))

    total = sum(m["size_bytes"] for m in manifest)
    print(f"\n{len(manifest)} packs, {total >> 20} MB total → {packs_dir}")
    print(f"manifest → {out / 'manifest.json'}")


if __name__ == "__main__":
    main()
