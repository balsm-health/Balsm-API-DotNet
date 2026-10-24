---
context: Inventory
plane: provider
features:
  - "P006: medication catalog (name, generic, brand, category, form, strength, unit) + multi-barcode"
  - "P006: expiry tracking + configurable alerts (30/60/90d); low-stock thresholds per item per branch"
  - "P006: stock never below zero (DB-enforced), FIFO by expiry"
  - "P006: purchase entry + purchase return (linked to original)"
  - "P006: controlled-substance classification (Egypt Law 182/1960 pre-tagged, irremovable)"
  - "P006: dead-stock identification"
  - "P007: DeductStock/RestoreStock published interface for POS (sync command, single local transaction — sanctioned exception, constitution v1.8.0)"
---

# Balsm.Inventory

Stock + catalog module. Owns the never-below-zero invariant (`StockLevel` aggregate). Point of Sale consumes stock exclusively through the `DeductStock`/`RestoreStock` published interface — the sole sanctioned synchronous cross-context call. Maps to the **Inventory** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/inventory.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations). Currently a DbContext shell — domain lands with P006.
