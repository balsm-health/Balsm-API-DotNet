---
context: Point of Sale
plane: provider
features:
  - "P007: walk-in sale (search/scan → basket → confirm) with atomic stock deduction, no partial sales"
  - "P007: immutable invoices; corrections via linked sale returns only"
  - "P007: payment methods (cash/card/credit-on-account, per-entity config); VAT 14% configurable"
  - "P007: cash drawer sessions (opening, mid-day counts, closing) + end-of-day reconciliation"
  - "P007: receipts (digital/printed, QR link to sale); thermal printing"
  - "P007: controlled-substance checkout flow (warning banner, pharmacist-only, prescription attachment per DR-02)"
  - "P007: paper prescription photo attachment on sale"
---

# Balsm.POS

Counter-trade module: sale, basket, returns, cash drawer, reconciliation, receipts. Partners with Pharmacy for dispensation validation at checkout; consumes stock via Inventory's `DeductStock` published interface (sanctioned sync command, constitution v1.8.0). Maps to the **Point of Sale** bounded context — canvas: `Balsm-Core/architecture/bounded-contexts/point-of-sale.md`.

Layers: `Api` → `Application` → `Domain` → `Infrastructure` (+ SQLite migrations). Currently a DbContext shell — domain lands with P007.
