# 02 — Cross-Repo Contracts

This API is the **source of truth** for HTTP endpoints, DTOs, and auth contracts. Two consumers depend on this contract:

- `balsm_app_flutter/` — mobile/desktop app (Flutter).
- `website/` — public web frontend.

Both can break silently. This file defines the rules that prevent that.

> Cross-repo rule from `Balsm-AI/canonical/rules/00-workspace.md`: *When you change an API endpoint, DTO, or auth contract in Balsm-API-DotNet, check whether balsm_app_flutter and website consume it and would break. Flag drift.*

---

## 1. Contract Artifacts (the only sources of truth)

| Artifact | Path | Audience | Generated? |
|---|---|---|---|
| OpenAPI 3.1 spec, per-module | `docs/api/openapi/v1/{module}.json` | Flutter codegen, website fetch client, Scalar UI | Yes — from controller annotations |
| OpenAPI 3.1 aggregate | `docs/api/openapi/v1/balsm.json` | Single-file consumers, docs site | Yes — built from per-module |
| Insomnia collection, per-module | `docs/api/insomnia/{module}.yaml` | Manual QA, admin UI examples, support | No — hand-authored, validated by `inso` |
| API changelog | `docs/api/openapi/CHANGELOG.md` | Mobile + web release planning | No — hand-curated |
| Auth contract | controller `[Authorize]` policies + `<remarks>` permission list | Mobile + web auth flows | No — code is the source |

Anything else (a PDF spec, a Notion page, a Slack message) is **not** a contract. Do not treat it as one.

## 2. Breaking-Change Gate

A change is **breaking** if it:

- Renames or removes an endpoint, a route param, or a query param.
- Renames or removes a DTO field.
- Tightens validation (longer min length, stricter format, new required field).
- Changes a field's type or nullability.
- Changes auth policy or required permission.
- Changes a status code semantically (e.g., 200 → 201, or a 4xx code shift).

For every breaking change:

1. Mark the operation with `x-balsm-breaking: true` in OpenAPI.
2. Add an entry to `docs/api/openapi/CHANGELOG.md` with: version, date, what changed, migration note.
3. Run `npx oasdiff breaking docs/api/openapi/v1/balsm.{previous}.json docs/api/openapi/v1/balsm.json` — must list the change and exit non-zero, then CHANGELOG must explain it.
4. Notify the Flutter + website maintainers in the PR description (link the consumer files that need updating — see §4).
5. If feasible, ship a **deprecation window**: keep the old endpoint with `deprecated: true` and `sunset:` date for one release, then delete in the next.

Non-breaking additions (new endpoint, new optional field, looser validation) skip steps 1, 3, 5 but still need a CHANGELOG entry.

## 3. Per-Change Checklist (PR blocked if any unchecked)

- [ ] OpenAPI per-module spec regenerated and committed (`docs/api/openapi/v1/{module}.json`).
- [ ] Aggregate `balsm.json` rebuilt; `npx @redocly/cli lint docs/api/openapi/v1/balsm.json --max-problems 0` passes.
- [ ] `npx oasdiff breaking ... balsm.json` exit code reviewed — breaking findings explained in CHANGELOG.
- [ ] Insomnia collection updated (`docs/api/insomnia/{module}.yaml`) — request, headers, example body, example response, negative case if validation changed.
- [ ] Insomnia validation passes locally: `npx insomnia-inso run collection --src docs/api/insomnia/{module}.yaml --env local`.
- [ ] DTO XML doc comments updated (drive the spec; bad comments = bad spec).
- [ ] Auth changes documented in action `<remarks>` (required permission appears in Scalar).
- [ ] If breaking: CHANGELOG entry added, `x-balsm-breaking: true` set, consumer impact noted in PR description.

## 4. Consumer Touch Points to Check

When you change a contract, search consumers for usages **before** opening the PR. Minimum sweep:

- `../balsm_app_flutter/` — search the path string (e.g. `api/v1/patients`) and any generated client types. Note the file paths in the PR description.
- `../website/` — same: search the path string and any TypeScript client.
- `Balsm-API-DotNet/admin-ui/src/api.ts` — local admin UI consumer. Update in the same PR (it lives in this repo).

If any consumer would break, either:

- Fix the consumer in its own PR landed **before** this one merges, or
- Ship the deprecation window described in §2.

Do **not** merge an API breaking change with no consumer-side plan.

## 5. Determinism in Spec Examples

Per root `CLAUDE.md` — never drift from these:

- IDs in examples: fixed GUIDs like `00000000-0000-0000-0000-000000000001`. Never `Guid.NewGuid()`.
- Timestamps: fixed ISO-8601 (`2025-01-01T00:00:00Z`). Never "now".
- No PHI. Use synthetic patients (`Jane Doe`, DOB `1990-01-01`, MRN `MRN-000001`).
- Insomnia `metaSortKey` is set explicitly so diffs stay stable.

## 6. Auth Contract Specifics

- Tokens, claims, and permission strings are part of the contract. Adding a new required permission to an endpoint **is** breaking — follow §2.
- Standalone-mode admin tokens, federation tokens, and user tokens are **different audiences**. Never let an admin path accept a federation token or vice versa.
- The `Authorization` header is the only supported transport. Do not introduce custom headers for auth.

## 7. Versioning

- URI versioning: `/api/v1/...`. A new major version means a new path prefix (`/api/v2/...`), a new module folder, and a new OpenAPI file.
- Within `v1`, additive changes only (per §2). When `v1` cannot absorb a needed breaking change, open an ADR before promoting to `v2`.

## 8. Generated vs. Hand-Authored — never invert

- Controllers + DTOs + `[ProducesResponseType]` annotations are the source of truth. OpenAPI is generated from them.
- **Never hand-patch the JSON to "fix" a diff.** Fix the annotation that produced it.
- Insomnia is hand-authored, validated against the running API by CI. When Insomnia drifts from OpenAPI, the spec is usually right and the collection wasn't refreshed.
