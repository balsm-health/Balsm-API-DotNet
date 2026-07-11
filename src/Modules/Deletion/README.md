---
context: Identity & Access
plane: cross-plane
features:
  - "P001: account deletion FSM (request → re-auth → 7-day grace → purge | cancel-via-login)"
  - "P001: Sign in with Apple token revoke on confirm (Apple 5.1.1(v))"
  - "P001: final purge (auth.users, profiles, emergency tokens, avatar; username released)"
  - "P001: deletion_log retention record (hashed user_id + reason, 2-year, disclosed pre-confirm)"
  - "P001: web deletion entry balsm.health/account/delete (Google Play requirement)"
---

# Balsm.Deletion

Account-deletion lifecycle module: the compliance-driven deletion state machine (Apple 5.1.1(v), Google Play User Data, Egypt PDPL 151/2020; purge SLA ≤30 days). Maps to the **Identity & Access** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/identity-access.md`.

Publishes `AccountDeletionConfirmed` (triggers immediate on-device PHI wipe in Personal Health) and `AccountPurged`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations).
