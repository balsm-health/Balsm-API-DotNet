# OTP email restricted to registration + password-reset

- **Date:** 2026-08-02
- **Repos:** `Balsm-API-DotNet` (Auth module), `balsm_app` (Flutter)
- **Driver:** Limited Resend email quota. Email-OTP is currently the passwordless
  login path, so every login spends an email. That is the volume to remove.

## Goal

Stop spending email quota on **login**. OTP emails are sent only for:

1. **Registration** — a brand-new email verifying ownership at signup.
2. **Password reset** — an existing user recovering a forgotten password.

Existing users log in with **password** or **Google/Apple OIDC**. Email-OTP
login is removed.

## Current state (why the change is non-trivial)

There is exactly **one** OTP email send site — `RequestOtpHandler` reached via
`POST /auth/otp/request` — and it serves four flows through that one endpoint:
registration, login, resend, and password-reset. Registration vs login is
decided at *verify* time (`VerifyOtpHandler` creates the account when no
identity exists), not at request time. So "OTP for registration only" cannot be
a client-only change: the server must know the caller's **intent**.

Verification paths:
- `POST /auth/otp/verify` (`VerifyOtpHandler`) — creates account if new, else logs in.
- `POST /auth/otp/verify-link` (`VerifyLinkHandler`) — magic link, mirrors the above.
- `POST /auth/password/reset` (`ResetPasswordHandler`) — sets password from the emailed code.

## Design

### 1. `purpose` on request-otp

Add required `purpose: "register" | "reset"` to the request. `RequestOtpHandler`
branches on whether an email identity already exists
(`authDb.UserIdentities`, provider `email`):

| purpose  | email unknown                          | email known                                    |
|----------|----------------------------------------|------------------------------------------------|
| register | send OTP                               | `409 EmailAlreadyRegistered` (no email)         |
| reset    | `200` generic accepted, **no email** (anti-enumeration) | send OTP                       |

There is no `login` purpose, so login never triggers an email. Rate limits
(3/email/10 min, 10/ip/60 min, global) are unchanged and now cover
register + reset only. The rate-limit check runs *before* the existence branch,
so enumeration probing is still rate-limited.

### 2. Verify is registration-only

`VerifyOtpHandler` and `VerifyLinkHandler` reject an **existing** identity with
`409 AccountAlreadyExists` ("sign in with password or social") and only complete
**new-user** registration. This closes the residual OTP-login path (otherwise a
reset code could be replayed at `/auth/otp/verify` to log in). `is_new_user` is
always `true` from these endpoints now.

`POST /auth/password/reset` is unchanged — it consumes the emailed reset code and
sets the password. It is a separate endpoint, so verify-tightening does not touch it.

### 3. Migration / lockout resolution

Existing **passwordless** users (OTP-only, no OIDC) are not locked out: the
**forgot-password** flow is their bootstrap. It emails them a reset code (allowed),
they set a password via `/auth/password/reset`, and thereafter sign in with the
password. Reset is **not** gated on "already has a password" precisely so it can
serve this migration. The (low-volume) reset email is the sanctioned recovery cost.

### 4. Flutter (`balsm_app`)

- `packages/balsm_api`: `RequestOtpRequest` gains `purpose`; `toJson` adds `"purpose"`.
- Thread `purpose` through `BalsmAuthAdapter.requestOtp` →
  `SignUpUseCase`/`SignInUseCase.requestEmailOtp`.
- Active UI (`app/lib/patient_app/screens/auth_flow.dart`): the sign-in email
  sub-mode drops "use code" (email sign-in = password only); the register path
  sends `purpose: register`; the forgot-password sheet sends `purpose: reset`.
- Legacy, unwired module screens (`modules/auth/.../presentation/screens/*`) are
  updated only enough to compile against the new signatures.

## Error contract (matches existing `{ error: { code } }` envelope)

- `POST /auth/otp/request`: `400 InvalidPurpose`, `409 EmailAlreadyRegistered`,
  plus existing `429 RateLimitExceeded` / `423 AccountLocked` / `403 CountryDenied`.
- `POST /auth/otp/verify` and `/auth/otp/verify-link`: `409 AccountAlreadyExists`,
  plus existing `401` for a bad/expired code.

## Decisions (confirmed)

- **A. Verify tightening: yes** — fully removes OTP login rather than only gating the send.
- **B. Register on existing email: explicit `409`** — clearer UX; email enumeration is
  already observable via `is_new_user` elsewhere, so no new exposure of note.

## Testing

- **Unit** (`Balsm.Auth.UnitTests`): `RequestOtpHandler` branch matrix
  (register×{new,exists}, reset×{new,exists}); `VerifyOtpHandler`/`VerifyLinkHandler`
  reject-existing; purpose parsing.
- **Integration** (`tests/Balsm.API.Tests/Controllers/Auth/`): `/auth/otp/request`
  status matrix, `/auth/otp/verify` rejects existing, reset still works end-to-end.
- **Flutter**: `dio_auth_api_test` body assertion includes `purpose`;
  `sign_up`/`sign_in` use-case tests per purpose.

## Contracts

- OpenAPI `auth` document regenerated; `docs/api/insomnia/auth.yaml` updated
  (purpose field, the two new 409s, reset example).

## Out of scope

- `website` auth (does not use this endpoint today).
- Binding a `purpose` onto the persisted `OtpChallenge` (a reset code can still be
  used to verify — acceptable: the holder owns the email, and no extra email is spent).
