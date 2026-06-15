# Balsm API — Guidelines & Rules (Index)

Authoritative rule index for **Balsm-API-DotNet**. Read top-down; each file owns one concern.

> Canonical cross-repo rules live in `../../../Balsm-Core/agents/rules/`. Files here are **API-specific deltas** — they **never** restate Core; they extend, override, or operationalize it for this repo.

## Read order

| # | File | Owns |
|---|------|------|
| 1 | [`01-api-dev-rules.md`](./01-api-dev-rules.md) | .NET 10 modular monolith rules — modules, layering, MediatR, EF Core, controllers, migrations, Standalone vs Cloud |
| 2 | [`02-cross-repo-contracts.md`](./02-cross-repo-contracts.md) | API ↔ Flutter ↔ website contract hygiene — OpenAPI, Insomnia, DTO drift, breaking-change gates |
| 3 | [`03-ai-agent-rules.md`](./03-ai-agent-rules.md) | How AI agents must operate in this repo — graphify-first, C4-first, test gates, commit policy, skills to use |
| 4 | [`04-phase-workflow.md`](./04-phase-workflow.md) | Phase-driven delivery — current `Balsm-Draft` state, what this repo owes per phase, ordering rules |
| 5 | [`05-nfr.md`](./05-nfr.md) | Non-functional requirements (latency, availability, observability, security posture) for this API |
| 6 | [`06-coding-best-practices.md`](./06-coding-best-practices.md) | Repo-specific coding deltas — Result<T>, async, structured logging, exceptions, validators, idempotency |

## Source-of-truth precedence

When rules collide, the higher tier wins:

1. **Regulatory + clinical safety** — `Balsm-Core/CERTIFICATIONS.md`, `Balsm-Core/AI_GOVERNANCE.md`, `Balsm-Core/SYSTEM_THREAT_MODEL.md`
2. **Universal agent rules** — `Balsm-Core/agents/rules/AGENTS.md`, `Balsm-Core/agents/rules/CODING_STANDARDS.md`
3. **This repo's CLAUDE.md** (`/Volumes/Dev/Balsm/Balsm-API-DotNet/CLAUDE.md`)
4. **These guidelines** (`.claude/guidelines/*.md`)

Lower tiers may **add** detail; they may not **weaken** higher tiers.

## Sibling repos referenced

- `Balsm-Core/` — canonical rules, certifications, threat model, glossary, NFR baseline.
- `Balsm-Draft/` — `PHASED_ROADMAP.md`, `STATE.md`, `BUSINESS_FEATURES.md`, `COMPLIANCE_REVIEW.md`. Mined for *what* this API must deliver per phase.
- `Balsm-AI/canonical/` — shared security/forensics/hardening skills. Used as the **required tooling layer** for security-sensitive changes (see `03-ai-agent-rules.md`).

## When to update these files

Update the relevant guideline in the **same PR** that introduces a rule change. A rule that lives only in conversation or commit messages is not a rule.

Skip updates for: typo fixes, single-method refactors, dependency bumps, doc-only edits.
