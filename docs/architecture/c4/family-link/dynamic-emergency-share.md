# Family link — Level 4: Dynamic (sealed emergency card share)

A linked member's **emergency card** (blood type, allergies, conditions,
emergency contacts, name, date of birth) is shown in the other person's app only
if its owner opts in, and only as ciphertext the server cannot read.

- Each app holds a device **X25519** key pair (private key in the platform secure
  store, never leaves the device). The public key goes up with the link request
  (A) and with the approval (B).
- The owner seals the card for the viewer: ephemeral X25519 → shared secret →
  HKDF-SHA256 (info `balsm-family-card-v1`, salt = link id as UTF-8) →
  AES-256-GCM, AAD = UTF-8 `"{linkId}|{ownerAccountId}|{version}"`. Server stores
  `{version, ephemeral public key, nonce, ciphertext}` only.
- Wire format: standard base64 with padding; `ephemeral_public_key` 32 bytes,
  `nonce` 12 bytes, `ciphertext` = AES-GCM output followed by its 16-byte tag
  (≤ 16 KB decoded). `version` is **int64**, strictly increasing (the app uses its
  clock in ms); the server refuses `version ≤ stored` with `409`, and answers `204`
  to a byte-identical replay of the stored version.
- Opt-in is per link, off by default, revocable at any time. Revoking or unlinking
  **purges the ciphertext** (it's the owner's PHI copy, not a clinical record); the
  audit row stays.
- The owner's app re-seals and uploads a new version whenever the card's fields
  change (or on next app start if offline). The viewer always shows the newest
  version with its "updated" date.
- A viewer key change (new phone) leaves the old ciphertext undecryptable; the
  viewer's app uploads its new public key and the owner's app re-seals on next
  pull. Until then the viewer sees "Ask them to open Balsm to refresh".

```mermaid
C4Dynamic
  title Family link — sealed emergency card share

  Person(b, "Owner (B)")
  Container(appB, "Balsm app (B)", "Flutter — X25519 key pair in secure store")
  Container(ctl, "FamilyLinksController", ".NET")
  ContainerDb(db, "family_link_card_share", "IdentityDbContext — opaque ciphertext")
  Container(appA, "Balsm app (A)", "Flutter — X25519 key pair in secure store")

  Rel(b, appB, "1. Turns on 'Share my emergency card with A'")
  Rel(appB, ctl, "2. GET /family-links/{id} → A's public key")
  Rel(appB, appB, "3. Seal card for A's key (ECIES: X25519 + HKDF-SHA256 + AES-256-GCM)")
  Rel(appB, ctl, "4. PUT /family-links/{id}/card {version, epk, nonce, ct}")
  Rel(ctl, db, "5. Owner check (else 404); upsert newer version only; audit")
  Rel(appA, ctl, "6. GET /family-links/{id}/card")
  Rel(appA, appA, "7. Decrypt with A's private key; show read-only card")
  Rel(b, appB, "8. Turns sharing off → DELETE /family-links/{id}/card (ciphertext purged)")
```

## Extra endpoints

| Verb + route | Who | Result |
|---|---|---|
| `PUT /api/v1/family-links/{id}/keys` `{ public_key }` | either party of a pending or linked link | `204` · `404` · `409` `FamilyLink.Ended` · `422` bad key |
| `GET /api/v1/family-links/{id}` | either party | `200` link object incl. `other_public_key` and `other_account_id` while linked |
| `PUT /api/v1/family-links/{id}/card` `{ version, ephemeral_public_key, nonce, ciphertext }` | the card's owner, link `linked` | `204` · `404` · `409` not linked / stale version · `413` > 16 KB · `422` malformed envelope |
| `GET /api/v1/family-links/{id}/card` | the other party | `200` `{ family_link_id, owner_user_id, version, ephemeral_public_key, nonce, ciphertext, updated_at }` · `404` none shared / not linked |
| `DELETE /api/v1/family-links/{id}/card` | the card's owner | `204` (purges ciphertext; idempotent) |

The first share records the owner's `EmergencyCardShare` consent; stopping withdraws
it. Every share, revoke and **read** writes a `family_link_audit` row.

Both directions are independent: A may share with B and B with A.

## Threats addressed

Server compromise yields only ciphertext (no key on the server). Wrong-recipient /
replay: AAD binds link, owner and version; the server refuses older versions.
IDOR (E10): every route 404s for non-parties. Size cap 16 KB. Telemetry never
carries card fields (existing allowlist).
