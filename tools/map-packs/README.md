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

## Publishing

`publish.py` uploads to Cloudflare R2 and writes `build/manifest.published.json`,
which is copied to `data/map-packs/manifest.json` for the API to serve.

```bash
pip install boto3
python3 tools/map-packs/publish.py --dry-run   # needs no credentials
python3 tools/map-packs/publish.py
```

R2 was chosen for one reason: **egress is free**. This workload is almost pure
egress — 27 immutable files downloaded whole by everyone. At ~330 GB/month any
per-GB model costs real money and costs more as adoption grows, which is exactly
backwards for a feature measured in downloads.

### CI

`.github/workflows/map-packs.yml` runs it on a monthly schedule, on demand, and
when `tools/map-packs/**` changes — deliberately **not** on every push, since a
full run moves ~550 MB to produce bytes identical to the last one.

Uploading is automated; flipping the catalogue is not. Pack filenames carry
their version, so publishing a new set cannot disturb an installed one — nothing
changes for users until `data/map-packs/manifest.json` points at it. That step
lands as a pull request.

**No CDN purge step exists and none is needed.** Pack URLs are versioned, so
their bytes never change; the catalogue is served by the API, not the CDN.

### Secrets

| Secret | Value |
|---|---|
| `R2_ACCOUNT_ID` | Cloudflare account id |
| `R2_ACCESS_KEY_ID` / `R2_SECRET_ACCESS_KEY` | R2 API token |
| `R2_BUCKET` | e.g. `balsm-map-packs` |
| `PACKS_BASE_URL` | e.g. `https://cdn.balsm.health/packs` |

Scope the R2 token to **Object Read & Write on that one bucket**. It needs no
cache-purge permission and no account-wide access; a token that can only write
objects to one bucket is the whole blast radius if it leaks.

Without the secrets the workflow still builds and reports sizes, then dry-runs
the publish — a fork gets a useful result instead of an opaque credential error.

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
