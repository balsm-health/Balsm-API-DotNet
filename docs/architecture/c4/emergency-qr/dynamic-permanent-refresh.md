# Emergency QR — Level 4: Dynamic, permanent-QR lifetime

The property being preserved: **the QR image printed on day 1 must resolve to
the profile as it is on day N**, without the key ever leaving the device.

## Mint (ttl = 0)

```mermaid
sequenceDiagram
  autonumber
  participant Sheet as _QrShareSheet (app)
  participant UC as MintEmergencyQrTokenUseCase
  participant KS as PermanentQrStore (keystore)
  participant API as EmergencyQrController
  participant Db as emergency_qr_token

  Sheet->>UC: call(ttlSeconds: 0, preferredLanguage)
  UC->>UC: read on-device snapshot; generate AES-256-GCM key
  UC->>UC: encrypt snapshot → nonce‖ciphertext‖mac; etag = sha256(snapshot)[0..8]
  UC->>API: POST /emergency-qr/mint {ciphertext, profile_etag, preferred_language, ttl_seconds: 0}
  API->>Db: revoke prior active tokens; insert token (expires_at = NULL)
  API-->>UC: {token_id, expires_at: null}
  UC->>KS: write {jti, key(b64url), etag, qrUrl}
  UC-->>Sheet: MintResult(token, qrUrl = …/emergency/{jti}#k={key})
```

## Silent refresh (app start · sheet open)

```mermaid
sequenceDiagram
  autonumber
  participant Trigger as app start / sheet open
  participant UC as RefreshPermanentQrUseCase
  participant KS as PermanentQrStore
  participant API as EmergencyQrController
  participant Db as emergency_qr_token

  Trigger->>UC: call() — fire-and-forget
  UC->>KS: read record
  alt no record
    UC-->>Trigger: success(null) — nothing to do
  else record exists
    UC->>UC: etag' = sha256(current snapshot)[0..8]
    alt etag' == record.etag
      UC-->>Trigger: success — ciphertext already current, no network
    else profile changed
      UC->>UC: re-encrypt snapshot with the SAME stored key
      UC->>API: PUT /emergency-qr/{jti}/ciphertext {ciphertext, profile_etag: etag', preferred_language}
      alt 200
        API->>Db: replace ciphertext + etag (owner-only, active-only)
        UC->>KS: record.etag = etag'
      else 404 / 409 / 410 — token gone server-side
        UC->>KS: clear record (stale key is useless)
      else offline / transient
        UC-->>Trigger: failure — record kept, retried on next trigger
      end
    end
  end
```

## Failure modes considered

| Failure | Behaviour |
|---|---|
| Device offline at refresh | Old ciphertext keeps resolving (stale but valid); retry on next trigger. |
| Token revoked from another device | `PUT` returns 410 → local record cleared; sheet falls back to the mint affordance. |
| Keystore record corrupted | `PermanentQrStore.read()` drops it and returns null — never crashes the sheet. |
| Temporary token minted afterwards | Server revokes the permanent token; the app clears the stored key in the same flow. |
| Profile emptied | Refresh is a no-op — leaving vs. revoking an emptied profile is the patient's decision, not the sync job's. |
