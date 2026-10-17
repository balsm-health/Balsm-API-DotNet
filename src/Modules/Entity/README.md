---
context: Entity Management
plane: provider
features:
  - "P000: workspace creation (one per server, enforced) — done"
  - "P000: entity + branch + entity-type CRUD, soft-delete + reactivate (MediatR CQRS, 24 integration tests) — done"
  - "P005: pharmacy entity setup (license, branches) + entity settings (EGP, 14% VAT, Africa/Cairo)"
  - "P014: departments, rooms, beds; staff org structure; owner transfer; multi-branch access — future"
---

# Balsm.Entity

Organizational-structure module: workspace (single tenancy root), entities, branches, and (P014) the physical hierarchy. Structure only — occupancy is Care Delivery, people/roles are Identity & Access. Maps to the **Entity Management** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/entity-management.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations).
