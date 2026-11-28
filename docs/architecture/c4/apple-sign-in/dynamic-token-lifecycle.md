# Sign in with Apple — Level 4: Dynamic (Apple token lifecycle)

The life of an Apple grant inside Balsm: captured at sign-in, revoked at
account purge, and cut off when Apple tells us the user withdrew. All three
paths run in the Auth module (`AuthDbContext`); the purge is driven by the
Deletion module through SharedKernel contracts (`IAppleGrantRevoker`,
`IAccountAuthEraser`), never by a cross-module project reference.

```mermaid
C4Dynamic
  title Sign in with Apple — token lifecycle

  Container(app, "Balsm app", "Flutter")
  Container(ctl, "AuthController", "Balsm.Auth.Api")
  Container(validator, "AppleOidcValidator", "Balsm.Infrastructure — JWKS cached 6h")
  Container(exchange, "ExchangeAppleTokenHandler", "Balsm.Auth.Infrastructure (MediatR)")
  Container(tokens, "AppleTokenClient", "Balsm.Infrastructure — /auth/token, /auth/revoke")
  Container(notify, "HandleAppleServerEventHandler", "Balsm.Auth.Infrastructure (MediatR)")
  Container(purge, "DeletionPurgeJob", "Balsm.Deletion.Infrastructure — daily")
  Container(revoker, "AppleGrantRevoker / AuthEraser", "Balsm.Auth.Infrastructure — SharedKernel contracts")
  ContainerDb(db, "AuthDbContext", "user_identities (encrypted Apple refresh token), user_refresh_tokens")
  System_Ext(apple, "Apple ID servers", "appleid.apple.com")

  Rel(app, ctl, "1. POST /auth/apple {id_token, authorization_code, nonce}")
  Rel(ctl, exchange, "2. ExchangeAppleTokenCommand")
  Rel(exchange, validator, "3. Verify ID token (RS256, JWKS, iss, aud, exp), then nonce: SHA-256(raw) == nonce claim — either fails → 401")
  Rel(exchange, tokens, "4. Exchange code → refresh token (failure logged; sign-in continues)")
  Rel(exchange, db, "5. Upsert identity by sub; store AES-GCM refresh token; issue Balsm session")
  Rel(purge, revoker, "6. Purge: RevokeForAccountAsync(userId)")
  Rel(revoker, apple, "7. POST /auth/revoke (failure recorded; purge continues)")
  Rel(purge, revoker, "8. EraseForAccountAsync — fails → account kept for retry")
  Rel(apple, ctl, "9. POST /auth/apple/notifications {payload JWT}")
  Rel(ctl, validator, "10. ValidateServerNotificationAsync (RS256, iss, aud, iat ≤ 7 days old) — invalid → 400")
  Rel(ctl, notify, "11. HandleAppleServerEventCommand → 200")
  Rel(notify, db, "12. consent-revoked / account-delete: clear Apple token, revoke all refresh tokens; email-*: refresh address")
```

## Failure modes

| Step | Failure | Result |
|---|---|---|
| 3 | ID token invalid, nonce missing or mismatched | `401`; nothing stored |
| 4 | Code exchange fails, Apple down, or client secret not configured | Logged with user id only; sign-in succeeds without a stored refresh token |
| 7 | Apple revoke fails or config missing | Status recorded on the deletion log; purge goes on to the auth erase |
| 8 | Auth erase fails | Account row kept, retried on the next run |
| 10 | Notification JWT missing, not RS256-signed by Apple, wrong audience/issuer, `iat` missing / older than 7 days / in the future, passed `exp`, or malformed `events` | `400` |
| 11–12 | Unknown `sub` or unknown event type | `200`, no-op (logged: event type only) |

`consent-revoked` and `account-delete` sign the user out on every device by
revoking every Balsm refresh token; an access token already issued keeps
working until its 15-minute expiry, after which the app cannot refresh. The
Balsm account and on-device data are kept. The user can sign in again with
Apple, which re-grants consent and stores a fresh refresh token.
