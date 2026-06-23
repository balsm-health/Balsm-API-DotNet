# Balsm API — Hosting Service Requirements

> What to request from a hosting provider, across **all delivery phases**.
> Verified against the codebase (`Dockerfile`, `appsettings*.json`, module wiring) and the platform specs (`Balsm-Core/NON_FUNCTIONAL_REQUIREMENTS.md`, `MIGRATION_INTEGRATION_GUIDE.md`, 13 bounded contexts).
>
> Legend: ✅ **Now** (P001 MVP, cloud deploy) · 🔭 **Future** (later phase — provision when the triggering feature lands).

Cloud deploy = `DeploymentMode=Cloud`: PostgreSQL, Dockerfile build (container image, HTTP port 5000). Standalone-only features (Supervisor, mDNS, self-update, admin SPA, HTTPS-on-port+1, SQLite) are **excluded** in cloud — not part of any hosting ask.

---

## ✅ Now — required for MVP boot

| # | Service | Detail | Why | Hint (free-tier pick / gotcha) |
|---|---------|--------|-----|--------------------------------|
| 1 | **Container runtime** (Linux x64) | Builds from `Dockerfile` (`mcr.microsoft.com/dotnet/aspnet:10.0`), exposes HTTP **:5000**, always-on, restart-on-failure. Outbound egress. | API host. No serverless (cold starts). | Fly.io / Render / Koyeb free web service. **Disable scale-to-zero** (set min 1 instance) or OTP/latency suffers. |
| 2 | **PostgreSQL** (managed) | Persistent + automated backups (PITR ideal), TLS. | Cloud DB (`DeploymentMode=Cloud` → Npgsql). Non-PHI only; `date_of_birth` AES-256-GCM at app layer. | Neon / Supabase free. Pick region at create — **can't move later**. Free tiers often daily-backup only → verify PITR before go-live. |
| 3 | **TLS termination + DNS** | Auto cert. Domains: `api.balsm.health`, `api-dev.*`, `api-staging.*`. | HTTPS redirect on in cloud mode. | Cloudflare DNS (free) + provider auto-TLS (Let's Encrypt). Add one CNAME per env. |
| 4 | **Transactional email** | **Resend** — email OTP. API key + verified sending domain (SPF/DKIM/DMARC on the DNS above). | Auth (OTP login). | Resend free = 3k emails/mo, 1 domain. Verify DKIM day 1 or OTP lands in spam. |
| 5 | **Secret/env store** (encrypted at rest) | DB conn string, JWT HS256 signing key, AES-256-GCM key (`date_of_birth`, FR-048), Resend key, Google/Apple OIDC client id+secret, reCAPTCHA Enterprise key, `Sentry__DSN`. | Config injection. | Use the provider's secret manager — never `.env` in repo. JWT + AES keys ≥32 bytes; plan rotation. |
| 6 | **Error + ops logging** | **Sentry** (PHI-scrubbed) — **already provisioned** (existing premium SaaS subscription). Host only needs `Sentry__DSN` env var (see row 5). No compute/storage from provider. | Crash/error visibility + app/ops log capture (Sentry Logs). | Zero new cost — reuse existing sub, set `Sentry__DSN`, dual-write Serilog → Sentry. **Audit logs (7yr) go to cold storage, not Sentry** — see healthcare table. |
| 7 | **Redis** (managed) | Provision **upfront** so the free tier reserves capacity. Code wires it when API runs 2+ replicas / adds L2 cache. | Shared rate-limit + cache + SignalR backplane. Today `OtpRateLimitPolicies.cs:8` uses single-host `IMemoryCache`. | Upstash Redis free (10k cmd/day), serverless pay-per-use. Skip wiring until 2nd replica exists. |
| 8 | **Object storage (S3-class)** | Provision **upfront** (bucket + keys). Wired when first server-side upload / backup-offload lands. | Durable blobs: uploads, backup/log offload. Today backups → local `/data` volume. | Cloudflare R2 free 10GB, **zero egress fee** — pairs with row-9 CDN. S3-compatible API. |
| 9 | **CDN** | Edge cache in front of website + static assets + API read responses / image & QR delivery. | Geo latency, offload, caching headers (NFR §11.2). | Cloudflare free CDN over everything. Set `Cache-Control` per endpoint; **never cache** PHI/auth responses. |

> Rows 7–9 are **requested now, wired in code later**. Not yet referenced in the codebase — ask the provider upfront so the free tier reserves them; flip on as each feature phase lands.

**External integrations (egress + secrets only — host does not provide):** Google OIDC, Apple OIDC, reCAPTCHA Enterprise, **push — FCM (Android) + APNs (iOS)** (host needs outbound egress + secret store only; NFR ≥ 5,000 push/min is app-side throughput), **payment — Stripe / Paymob / Fawry** (egress + secret + inbound webhook URL only; Billing & Finance context), iCloud Drive + Google Drive (client-side user PHI backup blobs — no server storage).

**Minimal free-tier ask:** 1 always-on container (2 GB RAM) + 1 Postgres (persistent+backup) + 1 Redis + 1 object-storage bucket + CDN + TLS custom domain + outbound egress + secret store. Resend + Sentry external (Sentry already provisioned).

---

## 🔭 Future — provision when the phase lands

Each row names the **trigger** (the feature/bounded context that forces it) so nothing is bought early.

> Redis, object storage, and CDN are in the **Now** table (rows 7–9) — requested upfront even though wired later.

| # | Service | Trigger (phase / context) | Why | Hint (free-tier pick / gotcha) |
|---|---------|---------------------------|-----|--------------------------------|
| F1 | **Realtime (WebSocket / SignalR)** | Real-time chat / messaging + live updates (Messaging, Sessions). | Push live data to clients without polling. | **First future need** — SignalR self-hosts on the container; backplane = row-7 Redis only when multi-node. No new paid service. |
| F2 | **Large object storage + DICOM server / PACS** | **Radiology** context (ImagingOrder, Study, PACS); DICOM migration. | Imaging archives are large, long-retention. Sized separately from the row-8 bucket. | Orthanc (open-source DICOM) over R2/S3 backend. Volume kills free tiers — budget paid storage. |
| F3 | **Message queue** (RabbitMQ / SQS / Kafka-class) | Async email/Resend send with retry; cross-context domain events; in-app messaging fan-out. NFR: "persist undelivered, retry exponential backoff". | Decouple bounded contexts; durable delivery + dedup; OTP request returns fast, survives Resend outage. | Today OTP send is a direct inline call (`OtpService.cs:51`). Start with a **Postgres outbox** — no new infra; CloudAMQP free (1M msg/mo) when volume demands. |
| F4 | **Search index** (OpenSearch / Elasticsearch) | Clinical Records search; avoid `LIKE '%term%'` on large tables (NFR §11.1). | Full-text + faceted search over records. | Use **Postgres FTS / `pg_trgm`** first — free, already have the DB. Dedicated index only when it outgrows. |
| F5 | **HL7 / FHIR interface engine** | External healthcare system integration (`MIGRATION_INTEGRATION_GUIDE.md`); `/metadata` CapabilityStatement (R4). | Anti-corruption boundary; HL7 v2/v3 ↔ FHIR ↔ domain. Egress to insurance/pharmacy networks. | Firely SDK or HAPI FHIR facade. Heavy — its own service, not a free-tier fit. |

---

## ⚕️ Healthcare / regulatory constraints on the host

Compliance targets: **Egypt PDPL** (primary), GDPR, HIPAA-design, DPG. Source: `Balsm-Core/CERTIFICATIONS.md`, `NON_FUNCTIONAL_REQUIREMENTS.md`. These constrain **which** provider/tier is acceptable — not just what to switch on.

**Design that lowers the bar:** PHI lives **on-device** (SQLite/SQLCipher). Cloud holds **non-PHI only**; the single cloud PHI field `date_of_birth` is **AES-256-GCM encrypted at the app layer** (FR-048), audited on every write. So the provider never sees plaintext PHI — this is what makes a free tier even thinkable.

| Requirement | What the host must offer | Notes | Hint (how to satisfy cheaply) |
|-------------|--------------------------|-------|-------------------------------|
| **Data residency** | Region pinning; keep data in the entity's region. | PDPL bars cross-border PHI transfer without consent + legal basis. Egypt/GCC region ideal. Verify free tier's region before committing. | Pick EU/ME region at project create on Neon/Supabase — irreversible. No ME region? Frankfurt is the usual fallback. |
| **Encryption at rest** | AES-256 on disk + DB volume. | NFR: "all data at rest must be encrypted using AES-256." App-layer AES-256-GCM on `date_of_birth` is on top of this. | Managed Postgres + R2 = AES-256 by default; just cite their docs in the compliance file. |
| **Encryption in transit** | TLS everywhere (row 3) incl. DB connection. | | Force `Ssl Mode=Require` (Npgsql) in the conn string — don't trust the default. |
| **Backups** | Automated **≤ every 6h**, PITR, **encrypted**, stored **geographically separate** from primary. Quarterly restore test. | Most free Postgres tiers do daily-only / no PITR — check, may force a paid DB. | **Likeliest paid line item.** If free DB lacks PITR, add a 6h `pg_dump` → R2 cron as a stopgap. |
| **Audit-log retention** | Durable storage: **7 years** audit logs, **90 days** operational logs. | Drives log-sink + object-storage (row 8) sizing. | Hot DB for 90d, then ship audit rows to cold R2/Glacier-class. Don't keep 7yr in Postgres. |
| **BAA / DPA** | A signed Data Processing Agreement (PDPL/GDPR). HIPAA **BAA** only if any PHI reaches cloud. | Free tiers usually **won't sign a BAA** → acceptable *only because* cloud carries no plaintext PHI. If that ever changes, the free tier is disqualified. | Click-through **DPA** is common on free tiers (GDPR); grab it. BAA is paid-only — fine while no plaintext PHI. |
| **Sub-processor disclosure** | Provider is itself a sub-processor; must be listed in the data-safety filing. | Alongside Resend, iCloud/Google Drive, reCAPTCHA, Sentry. | Keep a `SUBPROCESSORS.md`; add a row on every new vendor — gate it in PR review. |
| **Right to erasure** | Ability to hard-purge on request within 30 days (or anonymize where retention is legally required). | PDPL + GDPR. Backups must age out too. | Build the purge job early; set backup retention **< 30d** so erased data ages out without manual scrub. |
| **Tenancy isolation** | No cross-tenant data bleed (cache keys, backups, logs include tenant id). | NFR caching rules. | Prefix every cache key + storage path with `tenantId` from day 1 — retrofitting is painful. |

> **Decision gate before accepting the free hosting:** (1) does it sign a DPA, (2) can it pin region, (3) AES-256 at rest, (4) ≤6h encrypted off-site backups + PITR. If any is "no", that workload (esp. Postgres) may need a paid/compliant tier even if compute stays free.

### HIPAA — conditional, not required for MVP

HIPAA is **US** law. NFR mandate is *"designed for HIPAA with BAA support"* — **design-for**, not certify-now. Primary market is the Arab world / **Egypt (PDPL)**, so HIPAA does **not** attach to the MVP.

**Why MVP is clear:** cloud holds **no plaintext PHI** — PHI is on-device, and the one cloud PHI field (`date_of_birth`) is AES-256-GCM encrypted at the app layer. The provider sees ciphertext only, so HIPAA's infrastructure obligations don't bind the host today. **A free tier is acceptable for the MVP.**

**HIPAA triggers when** either: (a) a **US covered-entity** customer is onboarded, or (b) **plaintext PHI** ever lands in the cloud. At that point the host must satisfy:

| HIPAA Security Rule | Infrastructure requirement | MVP status | Hint (what to do if it triggers) |
|---------------------|----------------------------|------------|----------------------------------|
| §164.314 — BAA | **Signed BAA** with the host **and every** PHI-touching sub-processor (Resend, etc.) | ❌ free tiers won't sign — **hard blocker** | AWS/Azure/GCP sign BAA free, but only over their HIPAA-eligible services. Migrate the PHI workload there, not the whole stack. |
| Physical safeguards | Host on its **HIPAA-eligible service** list + **SOC 2 Type II / ISO 27001 / HITRUST** attestation | ❌ verify provider | Request the provider's SOC 2 report (usually under NDA) before signing anything. |
| §164.312 — audit controls | ePHI-access audit logs, **≥ 6yr** retention | ✅ doc already requires 7yr | Reuse the healthcare-table audit pipeline; HIPAA's 6yr ≤ our 7yr. |
| §164.312 — encryption | AES-256 at rest + TLS in transit | ✅ already (rows 2–3 + healthcare table) | No change needed — already meets the addressable spec. |
| §164.308 — contingency | Backup + tested **disaster-recovery** plan | ✅ ≤6h / PITR / quarterly restore | Write the DR runbook now; HIPAA wants it documented + tested, not just configured. |
| Access control / auth | RBAC, unique user IDs, auto-logoff | ✅ app (permissions + JWT) | App-layer, not a host ask — no provider action. |
| Integrity | soft-delete + immutable audit + optimistic concurrency | ✅ app rules | App-layer, not a host ask — no provider action. |

> **Action if HIPAA triggers:** move PHI-touching workloads (Postgres, any plaintext-PHI store) to a **HIPAA-eligible paid tier** (AWS / Azure / GCP HIPAA programs) under a signed BAA. Ciphertext-only compute can stay on the cheaper tier. Do **not** accept a free host that cannot sign a BAA once any plaintext PHI is in scope.

---

## Provisioning cheat-sheet

- **Phase 1 (now):** rows 1–9. Rows 1–6 boot the MVP; rows 7–9 (Redis, object storage, CDN) requested upfront, wired as features land.
- **Engagement:** F1 (realtime), F3 (queue).
- **Clinical depth:** F2 (radiology/PACS), F4 (search), F5 (FHIR/HL7).
