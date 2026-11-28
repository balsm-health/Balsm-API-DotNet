# API changelog

Contract changes to the published OpenAPI documents (`docs/api/openapi/v1/`).
Newest first. Breaking entries carry `x-balsm-breaking: true` on the operation
and a migration note.

## 2026-11-28 — Auth: Apple server-to-server notifications (non-breaking, additive)

New operation `Auth_AppleServerNotification` — `POST /auth/apple/notifications`
(`all.json`). Called by Apple, not by the app: anonymous, authenticated by the
Apple-signed `payload` JWT. `200` for a valid payload (unknown `sub` or event
type included), `400` when the JWT fails validation, `404` while
`SocialSignIn:Enabled` is false, `429` per-IP rate limit. Consumer: Apple
(URL registered in the Apple Developer portal). Contract:
`docs/architecture/c4/apple-sign-in/`.

## 2026-11-28 — Auth: Apple sign-in nonce + authorization code (behavioral)

`POST /auth/apple` (`all.json`): `nonce` is now required — the
raw value whose SHA-256 hex must equal the ID token's `nonce` claim; missing or
mismatched returns `401` (now declared). `authorization_code` is exchanged with
Apple for a refresh token kept encrypted for revocation at account deletion; a
failed exchange never fails sign-in. No schema change (both fields already
existed). The endpoint is off (`SocialSignIn:Enabled`), so no client is
affected yet. Consumer: `balsm_app` `AuthApi.signInWithApple`.

