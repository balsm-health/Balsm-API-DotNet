---
context: Customer Relations
plane: provider
features:
  - "P008: customer record (name, phones, email, national ID, DOB, gender, address)"
  - "P008: purchase history projection from SaleCompleted events"
  - "P008: manual paper-prescription recording (doctor, medication, dosage, date)"
  - "P008: search by name / phone / national ID (instant)"
  - "P008: unclaimed profiles (pharmacy-created, claimable later)"
  - "P010: claim flow — bind supabase_user_id on phone/email/national-ID match"
---

# Balsm.Customer

The pharmacy's model of a person — commercial relationship, not platform patient identity. Owns the unclaimed→claim bridge to Supabase identity (P010). Maps to the **Customer Relations** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/customer-relations.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations). Currently a DbContext shell — domain lands with P008.
