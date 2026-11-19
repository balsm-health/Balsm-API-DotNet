# Systems Directory — Level 4: Dynamic — submit and publish a listing

```mermaid
C4Dynamic
  title Submit a listing → review → published

  Person(pub, "Publisher (browser)")
  Person(rev, "Balsm reviewer")
  Container(ctl, "ListingsController / UploadsController / ReviewController", "Balsm.SystemsDirectory.Api")
  Container(med, "MediatR", "Application")
  Container(h, "Handlers", "Infrastructure")
  ContainerDb(db, "SystemsDirectoryDbContext", "PostgreSQL")
  Container_Ext(r2, "IObjectStore → R2 public bucket")
  Container_Ext(mail, "IEmailSender → Resend")

  Rel(pub, ctl, "1. POST /systems/uploads {type, bytes} ×N", "JWT")
  Rel(ctl, med, "2. CreateListingUploadCommand")
  Rel(med, h, "3. role check (editor+), validate PNG/JPG/WebP ≤5 MB, ≤8")
  Rel(h, r2, "4. presign PUT (5 min, exact type + length)")
  Rel(pub, r2, "5. PUT image bytes directly")
  Rel(pub, ctl, "6. POST /systems {fields, imageKeys, publisherOrgId} + Idempotency-Key", "JWT")
  Rel(ctl, med, "7. CreateSystemListingCommand (FluentValidation: lengths, ≤20 customers, YouTube id)")
  Rel(h, r2, "8. HEAD each key: exists, size, type")
  Rel(h, db, "9. SystemListing.Submit(): publisher is software org in caller's workspace → status pending; audit row; commit")
  Rel(rev, ctl, "10. POST /admin/systems/{id}/approve", "JWT + DirectoryReviewer")
  Rel(h, db, "11. SystemListing.Approve() pending → published; audit; raise SystemListingPublished")
  Rel(h, mail, "12. notify workspace owner/admins (queued, retried)")
```

## Notes

- Step 9 is one transaction. If any image fails the HEAD check the command
  returns `422` listing the bad keys and nothing is written.
- Re-sending step 6 with the same `Idempotency-Key` returns the first result.
- `GET /systems` only ever reads `status = published AND NOT is_deleted`.
- Reject (`/reject` with a required note) moves `pending → rejected`; the
  publisher edits and resubmits, which returns it to `pending`.
- Editing a published listing (`PATCH`) moves it back to `pending`; the last
  published version stays hidden until re-approved (no draft/published split in
  v1 — call out if you want the live version kept visible during review).
