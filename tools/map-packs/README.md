# Map packs

Offline basemap packs, one per Egyptian governorate. A user downloads the
governorates they care about and the map works inside them with no network.

## Licence and attribution

Tiles are cut from [Protomaps' daily planet basemap](https://maps.protomaps.com/builds),
itself an ODbL **Produced Work** derived from OpenStreetMap. Produced Works may
be redistributed with attribution and carry no share-alike obligation on the
output.

**"© OpenStreetMap contributors" must be visible wherever these tiles render.**
Not on a licence screen — on the map.

Boundaries come from OSM (`admin_level=4`), also ODbL, so one attribution
covers both.

Protomaps asks that their builds not be hotlinked. The pipeline honours this:
Egypt is pulled **once** per build and all 27 governorates are cut from that
local file. Re-running with `build/egypt.pmtiles` present skips the remote read
entirely.

## Running it

```bash
brew install pmtiles

# Rarely — boundaries change on the order of years. Commit the result.
python3 tools/map-packs/boundaries.py

# Each release.
python3 tools/map-packs/build_packs.py                 # all 27, z0-15
python3 tools/map-packs/build_packs.py --maxzoom 14    # roughly half the size
python3 tools/map-packs/build_packs.py --only cairo    # one, for a quick check
```

Outputs land in `build/` (git-ignored): `packs/<id>-<version>.pmtiles` plus a
`manifest.json` the API serves.

## Measured, 2026-09-13, z0–15

| | |
|---|---|
| Egypt, one remote pull | **254 MB**, 4m48s |
| All 27 packs, cut locally | **297 MB**, 2.4s |
| Largest | Giza 26 MB, Cairo 25 MB |
| Smallest | Port Said 2 MB, Luxor 3 MB |

`--maxzoom 14` roughly halves each pack (Cairo 25 MB → 12 MB). Vector tiles
over-zoom, so z14 data still renders past z14 with less detail — worth it if
pack size becomes the complaint.

## Versioning

Packs are versioned by the **date of the OSM data**, read from the archive's
`planetiler:osm:osmosisreplicationtime`.

Deliberately not `planetiler:buildtime`: that field is inherited from the
Protomaps build image and on the 2026-09-13 archive reads `2026-03-28`, six
months before the data it describes. Versioning on it would have the app tell
people their map is half a year old when it is hours old.

## Boundary assembly

`boundaries.py` does more than download. A relation's `outer`/`inner` members
arrive as ways, split and in arbitrary direction; chaining them back into
closed rings is in `_rings.py`. Skipping that step yields open linestrings that
no polygon clipper accepts. Inner rings are assigned to the smallest outer ring
containing them, so lakes and enclaves cut correctly.

The script refuses to write unless all 27 governorates assemble — a pack set
with a silent hole is worse than a failed build.

Overpass throttles aggressively. The fetch retries with backoff, and
`--save-elements` / `--elements` let a throttled run be resumed from the raw
response instead of started over.
