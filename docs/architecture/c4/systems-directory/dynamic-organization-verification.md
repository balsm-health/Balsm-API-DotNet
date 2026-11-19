# Systems Directory — Level 4: Dynamic — organisation email confirmation and legal verification

```mermaid
C4Dynamic
  title Add organisation → confirm work email → (optional) legal verification

  Person(pub, "Publisher admin (browser)")
  Person(rev, "Balsm reviewer")
  Container(ctl, "OrganizationsController / ReviewController", "Balsm.SystemsDirectory.Api")
  Container(h, "Handlers (via MediatR)", "Infrastructure")
  ContainerDb(db, "SystemsDirectoryDbContext", "PostgreSQL")
  Container_Ext(mail, "IEmailSender → Resend")
  Container_Ext(r2p, "IObjectStore → R2 private bucket")

  Rel(pub, ctl, "1. POST /publisher-workspaces/{id}/organizations {type, name, country, site, email}", "JWT")
  Rel(h, db, "2. DirectoryOrganization.Create(): email domain = site domain, not a personal provider; status unverified")
  Rel(pub, ctl, "3. POST /directory-organizations/{id}/email-confirmation", "JWT")
  Rel(h, db, "4. store hashed code, 10-min expiry; rate limit 3/hour")
  Rel(h, mail, "5. send 6-digit code to the work email")
  Rel(pub, ctl, "6. POST …/email-confirmation/verify {code}", "JWT")
  Rel(h, db, "7. ConfirmEmail(): ≤5 attempts → emailConfirmed")
  Rel(pub, ctl, "8. POST /systems/uploads {purpose: legal, pdf|image ≤10 MB} → presigned PUT (private)", "JWT")
  Rel(pub, ctl, "9. POST …/legal-verification {registryNo, taxNo, documentKey}", "JWT")
  Rel(h, db, "10. RequestVerification(): unverified → pending; audit")
  Rel(rev, ctl, "11. GET /admin/directory-organizations/{id}/document → 60 s presigned GET", "JWT + DirectoryReviewer")
  Rel(rev, ctl, "12. POST /admin/directory-organizations/{id}/verify", "JWT + DirectoryReviewer")
  Rel(h, db, "13. Verify(): pending → verified; audit; DirectoryOrganizationVerified")
  Rel(h, mail, "14. notify workspace admins (queued, retried)")
```

## Failure modes and trust

- Email confirmation is optional for listing (as in the design) but shown on the
  organisation card; legal verification is optional and drives the "Verified"
  badge only.
- The document never leaves the private bucket except through the reviewer's
  60-second presigned GET; the publisher cannot read it back after submitting.
- Wrong code: `422` with remaining attempts; after 5, the code is burned and a
  new one must be sent. Resend failure at step 5: `503`, no code stored as sent.
- Reject verification needs a note; status returns to `unverified` and the
  publisher may resubmit.
