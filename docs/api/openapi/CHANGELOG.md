# API changelog

Contract changes to the published OpenAPI documents (`docs/api/openapi/v1/`).
Newest first. Breaking entries carry `x-balsm-breaking: true` on the operation
and a migration note.

## 2026-11-24 — Platform: pregnancy guidance is served one language at a time, with sources, sizes, labels, topics and artwork

`Platform_GetPregnancyWeeks` takes `lang` (`en` or `ar`; anything else gets
English) and returns every text as a plain string in that language instead of
an `{en, ar}` pair, with Arabic written as UTF-8 rather than `\u` escapes —
about 25 KB in English and 31 KB in Arabic against 70 KB for both;
`data.language` names it, and
each language has its own `ETag`. The copy is sourced (`version`
`who-egmohp-2026-11-24.3`) and the document adds: `labels` (the guide's own
wording, with `{n}`/`{thing}`/`{value}`/`{week}` placeholders), `sources`
(`ContentSourceDto`), `danger_sign_labels`, `test_labels`, `topic_labels`,
`illustration_credits` (`IllustrationCreditDto`); per week `fetal_size`
(`FetalSizeDto`, nullable figures), `tests_due`, `danger_signs`, `topics`,
`illustration` (`IllustrationDto` or null) and `sources`.

The text fields changing from `{en, ar}` to strings would break a client of
the 2026-11-04 shape; none shipped (the only consumer, `balsm_app`
`ContentApi`, changes with it, behind `mother_child`), so no migration is
needed.

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
