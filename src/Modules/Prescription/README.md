---
context: Prescriptions
plane: provider
features:
  - "P013: lifecycle FSM draft → issued → (cancelled | superseded | dispensed | expired), audit-logged"
  - "P013: prescription created within encounter (medications, dosage, duration, refills)"
  - "P013: QR published language (one-time or reusable per doctor's choice); superseded auto-redirect"
  - "P013: dispense blocking for cancelled/superseded/expired (status shown to pharmacy)"
  - "P013: rule-based drug interaction warnings"
  - "P013: recurring prescriptions for chronic medications"
  - "P020: AI CDSS augmentation (BYOK, advisory-only, CR-03 gate) — future"
---

# Balsm.Prescription

Prescription lifecycle module — Core Domain. Owns validity and the QR/status published language Pharmacy dispenses against; marks `dispensed` only on `DispensationCompleted`. Constitutional: exhaustive edge-case coverage on validation/interaction/dosage logic. Maps to the **Prescriptions** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/prescriptions.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations). Currently a DbContext shell — domain lands with P013.
