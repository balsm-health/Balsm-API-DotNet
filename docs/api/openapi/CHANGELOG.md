# API changelog

Contract changes to the published OpenAPI documents (`docs/api/openapi/v1/`).
Newest first. Breaking entries carry `x-balsm-breaking: true` on the operation
and a migration note.

## 2026-11-25 — Platform: pregnancy week artwork moves to the shared media contract (BREAKING)

`BabyDto.illustration` (`IllustrationDto`: `key`, `credit`, `source_url`,
`image_url`, `image_url_dark`) is removed and replaced by `BabyDto.media`
(`MediaDto`, `Balsm.SharedKernel.Media`): `kind` (`image`), `alt` (in the
request's language), `credit` (still a key of `illustration_credits`),
`source_url`, `axes` (`MediaAxisDto`: `id`, `source` `app`|`user`, and for user
axes `label`, `options` (`MediaOptionDto`), `default`) and `renditions`
(`MediaRenditionDto`: `when`, `url`, `mime`, `width`, `height`, `bytes`,
`thumbnail` (`MediaFileDto`) or null). Weeks 6, 7, 8, 10 and 12 carry one
`theme` app axis with a `{theme: light}` and a `{theme: dark}` JPEG rendition;
renditions for another `locale` are dropped per `lang`. Content version
`who-egmohp-2026-11-25.6`. Contract:
`docs/superpowers/specs/2026-11-25-media-variants.md`.

Migration: read `baby.media` instead of `baby.illustration`; the light image is
the rendition whose `when.theme` is `light` (was `image_url`), the dark one
`when.theme` `dark` (was `image_url_dark`); `credit` and `source_url` keep their
meaning; `key` is gone. A client of the old shape gets `illustration` missing
(no artwork) rather than an error, and the new content version invalidates any
stored copy. Consumer: `balsm_app` `ContentApi` (bumps its storage key with
it).

## 2026-11-25 — Platform: pregnancy week artwork comes from the CDN (non-breaking, additive)

`IllustrationDto` gains `image_url` and `image_url_dark`: the week's artwork on
`cdn.balsm.health` (light and dark background), so the app no longer has to
ship the images. Content version `who-egmohp-2026-11-24.5`; weeks 6, 7, 8, 10
and 12 carry artwork.

## 2026-11-24 — Platform: pregnancy guidance is served one language at a time, with sources, sizes, labels, topics and artwork

`Platform_GetPregnancyWeeks` takes `lang` (`en` or `ar`; anything else gets
English) and returns every text as a plain string in that language instead of
an `{en, ar}` pair, with Arabic written as UTF-8 rather than `\u` escapes —
about 25 KB in English and 31 KB in Arabic against 70 KB for both;
`data.language` names it, and
each language has its own `ETag`. The copy is sourced (`version`
`who-egmohp-2026-11-24.4`) and the document adds: `symptom_guide`
(`SymptomDto`: label, self-care, when to call the doctor); `labels` (the guide's own
wording, with `{n}`/`{thing}`/`{value}`/`{week}` placeholders), `sources`
(`ContentSourceDto`), `danger_sign_labels`, `test_labels`, `topic_labels`,
`illustration_credits` (`IllustrationCreditDto`); per week `summary`, `symptoms`, `feelings`, `tips`, `tests_due`,
`danger_signs`, `topics`, `sources` and `baby` (`BabyDto`: `fact`,
`fetal_size` with nullable figures, `illustration` or null, `sources`).

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
