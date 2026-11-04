# Family link — Level 4: Dynamic (scan → request → approve)

```mermaid
C4Dynamic
  title Family link — request and approval

  Person(a, "Requester (A)")
  Person(b, "Recipient (B)")
  Container(appA, "Balsm app (A)", "Flutter")
  Container(appB, "Balsm app (B)", "Flutter")
  Container(ctl, "FamilyLinksController", ".NET")
  Container(h, "MediatR handlers", ".NET")
  Container(res, "IProfileQrOwnerResolver", "SharedKernel → EmergencyQr")
  ContainerDb(db, "family_link + consent + audit", "IdentityDbContext")

  Rel(a, appA, "1. Scans B's profile QR (/t/{jti}#k=…); app decrypts name + DOB locally")
  Rel(appA, ctl, "2. POST /api/v1/family-links {id, jti, relation}")
  Rel(ctl, h, "3. CreateFamilyLinkCommand(sub = A)")
  Rel(h, res, "4. Resolve jti → B (null → 404)")
  Rel(h, db, "5. Refuse self / existing pair (409); insert pending + A's consent + audit")
  Rel(appB, ctl, "6. GET /api/v1/family-links/incoming (on start / resume / Family)")
  Rel(b, appB, "7. Approves")
  Rel(appB, ctl, "8. POST /api/v1/family-links/{id}/approve")
  Rel(h, db, "9. Recipient check (else 404); pending → linked + B's consent + audit")
  Rel(appA, ctl, "10. GET /api/v1/family-links/outgoing → linked")
```

## API (Identity module, `/api/v1/family-links`)

| Verb + route | Who | Result |
|---|---|---|
| `POST /api/v1/family-links` `{ id, jti, relation }` | requester | `201` pending · `200` same id replayed (idempotent on client id) · `404` unknown/revoked/expired jti · `409` self-link or open request/link for the pair · `422` relation invalid · `429` rate limit |
| `GET /api/v1/family-links/incoming?status=pending` | recipient | `200` paged list: id, requester display name/@handle, relation, createdAt |
| `GET /api/v1/family-links/outgoing` | requester | `200` paged list: id, recipient display name/@handle (after approval only), relation, status, dates |
| `POST /api/v1/family-links/{id}/approve` | recipient | `200` linked · `404` not theirs · `409` not pending |
| `POST /api/v1/family-links/{id}/decline` | recipient | `200` declined · `404` · `409` |
| `DELETE /api/v1/family-links/{id}` | requester (pending → cancelled) or either side (linked → unlinked) | `204` · `404` |
| `GET /identity/health` | anonymous | liveness (already required per module) |

Statuses: `pending → linked | declined | cancelled`, `linked → unlinked`. Rows are
never hard-deleted. The recipient's name is not shown to the requester until
approval — before that, the requester sees only what their own scan showed them,
kept on their own device.

## App side

- `packages/balsm_api`: `FamilyLinksApi` + dio impl + DTOs; core `Provider<FamilyLinksApi>`.
- New module `modules/family_link` (context **Identity & Access**, plane cross-plane):
  replaces the shell's `FamilyMemberPreview` / `FamilyLinkRequest` preview.
- Mother & Child "Add family member" sheet gets back its **Scan their QR code** row;
  incoming requests show in the Family screen and the profile picker with a badge;
  a linked member appears in Family as **Read-only** (design `m.readOnly`) with
  name, relation and — when they share it — their sealed emergency card
  (`dynamic-emergency-share.md`); no other health tiles.
- Behind a new flag `family_link` (fallback **off** — it sends data to the server).

## Tests

Unit (aggregate transitions, every invariant), integration per endpoint incl. an
IDOR test (third account gets 404 on every route), E2E A-requests/B-approves over
HTTP, app widget + API-client tests. Insomnia `docs/api/insomnia/identity.yaml`
(new) and OpenAPI `docs/api/openapi/v1/identity.json` regenerated.
