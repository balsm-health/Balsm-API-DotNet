# API changelog

Contract changes to the published OpenAPI documents (`docs/api/openapi/v1/`).
Newest first. Breaking entries carry `x-balsm-breaking: true` on the operation
and a migration note.

## 2026-11-24 — Platform: pregnancy guidance gains sources, sizes and labels (non-breaking, additive)

`Platform_GetPregnancyWeeks` now serves sourced copy (`version`
`who-egmohp-2026-11-24`) and adds fields: top-level `sources`
(`ContentSourceDto`), `danger_sign_labels`, `test_labels`; per week
`fetal_size` (`FetalSizeDto`, nullable figures), `tests_due`, `danger_signs`,
`topics`, `illustration`, `sources`. Existing fields are unchanged; clients
that ignore unknown fields (`balsm_app` `ContentApi`) keep working.

## 2026-11-04 — Platform: pregnancy guidance content (non-breaking, additive)

New operation `Platform_GetPregnancyWeeks` — `GET /api/v1/content/pregnancy-weeks`
(`all.json`; host controller, no module document). Authenticated; `ETag` +
`If-None-Match` → `304`. Placeholder copy from a static file for now. Consumer:
`balsm_app` `ContentApi`. Contract: `docs/architecture/c4/pregnancy-guidance/`.

## 2026-11-04 — Identity: family links (non-breaking, additive)

New operations under `/api/v1/family-links` (`identity.json`):
`Identity_CreateFamilyLink`, `Identity_ListIncomingFamilyLinks`,
`Identity_ListOutgoingFamilyLinks`, `Identity_GetFamilyLink`,
`Identity_ApproveFamilyLink`, `Identity_DeclineFamilyLink`,
`Identity_DeleteFamilyLink`, `Identity_UpdateFamilyLinkKey`,
`Identity_UpsertFamilyLinkCard`, `Identity_GetFamilyLinkCard`,
`Identity_DeleteFamilyLinkCard`. Consumer: `balsm_app` `FamilyLinksApi`.
Contract: `docs/architecture/c4/family-link/dynamic-link.md` and
`dynamic-emergency-share.md`.
