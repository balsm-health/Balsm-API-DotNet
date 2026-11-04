# Family link — Level 3: Components

Three modules take part. None references another's projects: Identity reaches
the others only through **SharedKernel contracts**, implemented in their own
Infrastructure and wired in `Balsm.API/Program.cs`.

```mermaid
C4Component
  title Family link — components across Identity, EmergencyQr, Account

  Container_Boundary(identity, "Identity module") {
    Component(ctl, "FamilyLinksController", "Api", "POST/GET/approve/decline/DELETE; [Authorize]; user from sub claim")
    Component(cmds, "Create / Approve / Decline / Cancel / Unlink handlers", "Infrastructure (MediatR)", "Touch IdentityDbContext")
    Component(qry, "ListIncoming / ListOutgoing handlers", "Infrastructure (MediatR)", "AsNoTracking projections")
    Component(agg, "FamilyLink aggregate", "Domain", "State machine + invariants; raises FamilyLinkApproved")
    ComponentDb(db, "IdentityDbContext", "EF Core", "family_link, family_link_consent, family_link_card_share, family_link_audit")
    Component(eraser, "FamilyLinkAccountEraser", "Infrastructure", "Ends the erased account's links, purges cards")
  }
  Container_Boundary(sk, "SharedKernel") {
    Component(c1, "IProfileQrOwnerResolver", "Contract", "jti → UserId? for an active profile token")
    Component(c2, "IAccountDisplayNameReader", "Contract", "UserId[] → display name / @handle")
    Component(c3, "IAccountFamilyLinkEraser", "Contract", "Account erasure → end links, purge cards")
  }
  Container_Boundary(eqr, "EmergencyQr module") {
    Component(r1, "ProfileQrOwnerResolver", "Infrastructure", "Reads emergency_qr_token; revoked/expired → null")
  }
  Container_Boundary(del, "Deletion module") {
    Component(purge, "DeletionPurgeJob", "Infrastructure", "Nightly account purge")
  }
  Container_Boundary(acc, "Account module") {
    Component(r2, "AccountDisplayNameReader", "Infrastructure", "Reads user_account handle + names (non-PHI)")
  }

  Rel(ctl, cmds, "Send command")
  Rel(ctl, qry, "Send query")
  Rel(cmds, agg, "Load / mutate")
  Rel(cmds, db, "SaveChanges (one transaction: link + consent + audit)")
  Rel(qry, db, "Read")
  Rel(cmds, c1, "Resolve jti on create")
  Rel(qry, c2, "Names for the list")
  Rel(r1, c1, "implements")
  Rel(r2, c2, "implements")
  Rel(eraser, c3, "implements")
  Rel(purge, c3, "Erase on account purge")
```
