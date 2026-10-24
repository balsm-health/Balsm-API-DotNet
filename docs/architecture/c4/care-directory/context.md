# Care Directory — Level 1: System Context

The care directory is the patient app's "nearby health places" map: hospitals,
clinics, dentists, pharmacies, labs, scan centres and medical stores. It is
**public reference data, not PHI**, and it is the only Balsm dataset sourced from
a third party rather than produced by users or providers.

```mermaid
C4Context
  title Care Directory — System Context

  Person(patient, "Patient", "Looks for care near their location or near an area they are planning to visit")

  System_Boundary(balsm, "Balsm Platform") {
    System(api, "Balsm API", ".NET modular monolith. Serves GET /care/entities from the local care_place table")
    System(app, "Balsm Patient App", "Flutter. Renders the directory on an OpenStreetMap basemap with clustered pins")
  }

  System_Ext(overture, "Overture Maps Foundation", "GeoParquet place data on public S3. CDLA-Permissive-2.0 — storage and redistribution permitted")
  System_Ext(osm_tiles, "OpenStreetMap tile servers", "Raster basemap tiles. ODbL — attribution required")
  System_Ext(osm_data, "OpenStreetMap / Overpass", "Arabic names and opening hours. ODbL. PHASE 2 — not yet integrated")

  Rel(patient, app, "Searches by text, type and area")
  Rel(app, api, "GET /care/entities?lat&lng&radius_km&type&q&limit", "HTTPS, anonymous")
  Rel(app, osm_tiles, "Fetches basemap tiles", "HTTPS")
  Rel(overture, api, "Extracted OFFLINE into a versioned artifact, committed to the repo", "not a runtime dependency")
  Rel(osm_data, api, "Phase 2", "dashed")

  UpdateRelStyle(overture, api, $offsetY="-30", $offsetX="-90")
  UpdateRelStyle(app, api, $offsetY="-20")
```

## Trust boundaries and why the extract is offline

**The API never talks to S3 or Overpass at runtime.** Extraction happens by hand
into `data/care-directory/care_places.eg.ndjson.gz`, which is committed. Three
consequences, all deliberate:

- CI and the build need no network access to a third-party host.
- The exact bytes that populated production are auditable and re-playable — the
  manifest records the Overture release, row count and sha256.
- A third-party outage or a schema change upstream cannot break a deploy.

**Anonymous by design.** `GET /care/entities` is the one care endpoint marked
`[AllowAnonymous]` alongside the module health probe. The directory is public
business information; requiring a session would leak *who is looking for what
kind of care*, which is the PHI-adjacent signal worth protecting here.

**No PHI crosses this boundary in either direction.** The query carries a
location, which is why the endpoint takes coordinates as parameters rather than
resolving them from a session.

## Why Google Maps is not on this diagram

Google Maps Platform Terms §3.2.4(a) bar extraction, §3.2.3(b) bar storing Places
content beyond `place_id`, and §3.2.4(e) bar displaying Google content on a
non-Google map — which the OpenStreetMap basemap is. Separately, scraped rows
carry no provenance, and "where did your provider directory come from?" is a
question partner and regulatory due diligence will ask.

Overture carries per-row `sources` and a `confidence` score. That is the answer.

## Licence obligations this creates

| source | licence | obligation |
|---|---|---|
| Overture Places | CDLA-Permissive-2.0 | Attribution: © Overture Maps Foundation. Storage and redistribution permitted, no share-alike. |
| OSM tiles | ODbL | Attribution: © OpenStreetMap contributors, on the map surface. |
| OSM place data (Phase 2) | ODbL | Merging makes `care_place` a Derived Database. Serving query results is a *Produced Work* — attribution suffices; distributing the table would trigger share-alike. |
