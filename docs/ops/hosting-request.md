# Balsm — Hosting Requirements

Thank you for offering to host Balsm. Balsm is an open-source healthcare platform (a .NET 10 API + patient app). Below is what the backend needs to run, kept deliberately short. Happy to adjust to whatever your platform offers.

## Environments

We run **two** isolated environments — **staging** and **production** — each a fully separate stack (its own container, database, cache, storage bucket, and secrets). Nothing is shared between them. Local development runs on our own machines, so we are **not** asking you to host it.

| Environment | Domain | Size | Data |
|-------------|--------|------|------|
| **Staging** | `api-staging.balsm.health` | ~1 vCPU / 2 GB | test data only |
| **Production** | `api.balsm.health` | ~1 vCPU / 2 GB (room to grow to 2 vCPU / 4 GB) | live |

Everything in "What we need now" is needed **per environment**, except the shared items noted there (DNS/CDN/TLS, secret store mechanism).

## What we need now

| Service | Requirement |
|---------|-------------|
| **App server** | Per environment: one always-on Linux container (builds from a `Dockerfile`, .NET 10), **~1 vCPU / 2 GB RAM**. Listens on HTTP **:5000** behind your TLS. Please **no scale-to-zero** (cold starts break login). Outbound internet access. |
| **PostgreSQL** | Per environment: managed, persistent, TLS. **Production** needs **automated encrypted backups** (point-in-time recovery preferred); **staging** can be a smaller tier (test data only). |
| **Object storage** | Per environment: S3-compatible bucket, ~10 GB to start (file uploads + backup offload). |
| **Redis** | Per environment: a small managed instance (cache + rate limiting). |
| **CDN** | Edge caching in front of static assets and read APIs (shared, one config). |
| **TLS + DNS** | Auto HTTPS certificates and custom domains: `api.balsm.health` and `api-staging.balsm.health` (shared, one CNAME + cert per domain). |
| **Secrets** | A way to set environment variables / secrets securely per environment (DB connection string, signing keys, third-party API keys). |

**In one line:** two always-on stacks (staging + production), each an ~1 vCPU / 2 GB container + managed PostgreSQL + object storage + Redis; plus a shared CDN, custom domains with TLS, a secret store, and outbound internet. Production's PostgreSQL needs backups + PITR.

## Healthcare compliance (please note)

Because this is a patient platform, a few constraints affect the choice of region/tier. These bind the **production** environment (staging holds synthetic test data only):

- **Region:** ability to pin data to an **EU or Middle East** region.
- **Encryption:** AES-256 at rest, TLS in transit (including the database).
- **Backups:** encrypted, stored in a separate location, ideally every ~6 hours with point-in-time recovery.
- **Agreement:** a signed **Data Processing Agreement (DPA)**.

To keep this simple: the cloud holds **no plaintext patient health data** — sensitive data stays encrypted or on the patient's device. We would list your service as a sub-processor in our privacy disclosures.

## We handle these ourselves (no action needed from you)

Email, error monitoring, sign-in (Google/Apple), push notifications, and payments all run on external services. We only need **outbound network access** to reach them — you do not host them.

## Looking ahead (for capacity planning only — not needed now)

As features roll out we may later add: more storage for medical imaging, a message queue, a search index, and real-time/video. Mentioning now only so you can gauge headroom; none are required to launch.

**On your kind offer of AI model support:** thank you — we may take you up on it later. If we do, it would be for **non-patient-data** tasks only (e.g. document/FAQ search, de-identified analytics, clinical-coding lookups). By design **no patient health data is ever sent to a hosted model**, and any AI we add is assistive only — it never replaces clinical judgment. Nothing to provision now.
