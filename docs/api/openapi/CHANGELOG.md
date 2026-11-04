# API changelog

Contract changes to the published OpenAPI documents (`docs/api/openapi/v1/`).
Newest first. Breaking entries carry `x-balsm-breaking: true` on the operation
and a migration note.

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
