# Systems Directory — Level 1: System Context

The website's Marketplace page (`balsm.health/marketplace`, سوق الأنظمة) is a
public directory of health **software** systems. Software companies sign in
with their Balsm account, set up a publisher workspace, add their organisations,
and submit listings; Balsm staff review listings and legal verification before
anything shows publicly. Facilities browse anonymously.

The UI is already built and currently keeps its state in the visitor's browser
(`website/src/components/cloud/marketplace/store.ts`). This module replaces that
with server-side persistence.

Bounded context: **Systems Directory** (Platform plane, Supporting, no PHI) —
`Balsm-Core/architecture/bounded-contexts/systems-directory.md`, added by
constitution v1.9.0. Not the P022 add-on Marketplace and not the Provider
Directory.

```mermaid
C4Context
  title Systems Directory — System Context

  Person(visitor, "Facility visitor", "Browses and filters published systems; anonymous")
  Person(publisher, "Publisher", "Software-company staff: owner / admin / editor of a publisher workspace")
  Person(reviewer, "Balsm reviewer", "Balsm staff: approves listings and legal verification")

  System_Boundary(balsm, "Balsm Platform") {
    System(web, "balsm.health website", "Next.js on Cloudflare. /marketplace UI; calls the API from the browser")
    System(api, "Balsm API — SystemsDirectory module", ".NET 10 modular monolith, cloud deployment. Workspaces, organisations, listings, review")
    System(auth, "Balsm API — Auth module", "Existing Balsm account: email-code sign-up, password sign-in, JWT access/refresh")
  }

  System_Ext(r2pub, "Cloudflare R2 — public bucket", "Listing images, served via public URL / CDN")
  System_Ext(r2priv, "Cloudflare R2 — private bucket", "Legal-verification documents; never publicly addressable")
  System_Ext(resend, "Resend", "Transactional email: confirmation codes, invites, review outcomes")
  System_Ext(yt, "YouTube (nocookie)", "Listing videos, embedded client-side by id only")

  Rel(visitor, web, "Browses", "HTTPS")
  Rel(publisher, web, "Signs in, manages workspace, submits listings", "HTTPS")
  Rel(reviewer, api, "Approves / rejects (Insomnia or a later admin UI)", "HTTPS + JWT")
  Rel(web, auth, "Sign-up / sign-in / refresh", "HTTPS, JSON")
  Rel(web, api, "Public reads (anonymous) + publisher writes (Bearer JWT)", "HTTPS, JSON, CORS")
  Rel(web, r2pub, "PUT image via presigned URL", "HTTPS")
  Rel(web, r2priv, "PUT legal document via presigned URL", "HTTPS")
  Rel(api, r2pub, "Presign PUT, verify object, delete", "S3 API")
  Rel(api, r2priv, "Presign PUT / short-lived GET for reviewers", "S3 API")
  Rel(api, resend, "Send email", "HTTPS")
  Rel(web, yt, "Embed video by id", "iframe")
```

## Trust boundaries

| Boundary | Control |
|---|---|
| Browser → API (public reads) | Anonymous `GET` of **published** listings only; rate-limited per IP like the other anonymous endpoints (`/auth/*`, emergency QR); response cached at the edge. |
| Browser → API (writes) | Bearer JWT from the Auth module; every handler checks workspace role (owner / admin / editor) server-side. CORS allows the website origins via `Cors:AllowedOrigins`. |
| Browser → R2 | Short-lived (5 min) presigned `PUT` scoped to one object key, content type and max length. The API verifies the object (HEAD: size, type) before attaching it to a listing; unattached uploads are swept after 24 h. Keeps 5–10 MB files off the API, whose request body cap is 2 MB. |
| Reviewer → API | `DirectoryReviewer` authorization policy (see container.md, open decision). |
| API → Resend / R2 | Credentials from environment (`Resend:*`, `SystemsDirectory:R2:*`); timeouts + circuit breaker; failures surface as retryable errors, never partial state. |

## Failure modes

- **R2 unavailable:** presign fails → `503` with retry hint; nothing is written.
- **Upload never completes / wrong type or size:** listing submit rejects the image key (`422`); orphan swept later.
- **Resend down:** confirmation/invite returns `503`; no code is stored as "sent" so the user can retry. Review-outcome emails are queued and retried by a background job — the decision itself is already committed.
- **Token expired mid-flow:** `401` → website refreshes via `/auth/refresh` and retries once.
