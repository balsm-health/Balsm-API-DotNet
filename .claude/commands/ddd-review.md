# Domain-Driven Design Review

Review all changed or staged files for DDD and modular monolith violations in the Balsm platform.

## Instructions

1. Run `git diff HEAD` (or `git diff --cached`) to identify all changed files.
2. Read each changed file and analyze for the following:

### Bounded Context Boundaries
The system has 13 bounded contexts — verify no boundary violations:
- **Identity & Access** — User, Account, Permission, PermissionGroup, Team, Session
- **Entity Management** — Entity, Branch, Department, Room, Bed
- **Appointment** — Appointment, Slot, Schedule, WaitingList, HouseVisit
- **Clinical Records** — PatientRecord, Entry, CareTeam, Referral, Consent
- **Prescriptions** — Prescription, Medication, DrugInteraction
- **Pharmacy** — Pharmacy, PharmacyInventory, Dispensation
- **Labs** — LabOrder, Specimen, TestResult, QualityControl
- **Radiology** — ImagingOrder, Study, RadiologyReport, PACS
- **Billing & Finance** — Bill, Claim, Payment, InsurancePolicy
- **Inventory** — Item, PurchaseOrder, Vendor, StockLevel
- **Messaging & Notifications** — Conversation, Message, Notification
- **Charitable Donations** — Charity, DonationCase, Donation
- **Marketplace** — AddOn, AddOnDeveloper, AddOnReview

### Module Structure Violations
- Cross-module project references (modules must depend on shared contracts only)
- Direct access to another module's database tables or internal classes
- Code in one module importing types from another module's Domain/Application layer
- Shared database tables between contexts (each context owns its schema)

### Communication Violations
- Direct method calls between bounded contexts (must use domain events)
- Missing anti-corruption layers for external system integrations (HL7/FHIR, DICOM, insurance APIs)
- Tight coupling through shared entities across contexts

### Ubiquitous Language
- Wrong terminology: "reservation" instead of "appointment", "sample" instead of "specimen", "fulfillment" instead of "dispensation"
- Inconsistent naming between code, API endpoints, database schemas, and documentation
- Invented abbreviations or synonyms for domain terms

### Tactical Patterns
- Complex contexts (Clinical Records, Prescriptions, Billing, Labs, Radiology) missing DDD patterns: aggregates, domain events, value objects, repositories, domain services
- Simple CRUD contexts over-engineered with unnecessary DDD patterns
- Domain logic in application services, handlers, or controllers instead of domain models
- Anemic domain models (entities with only getters/setters and no behavior)

### Code Organization (.NET)
- Module not under `src/Modules/{ContextName}/` with proper layer split (Domain/Application/Infrastructure/Api)
- Shared types not in `Balsm.SharedKernel`
- Missing module registration class for DI

3. For each violation, report:
   - File path and line number
   - Violation type and severity
   - Which bounded contexts are involved
   - Suggested fix

4. If no issues are found, confirm the code follows DDD conventions.
