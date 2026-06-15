# 04 — Phase-Driven Workflow

This API is delivered in phases tracked by **`Balsm-Draft/`**. The order, scope, and dependencies live there — not here. This file tells agents **how** to align work in this repo with that plan.

> Canonical sources (read before planning any non-trivial change):
> - `../../../Balsm-Draft/PHASED_ROADMAP.md` — ordered phase list + ADRs.
> - `../../../Balsm-Draft/STATE.md` — *current* reality vs roadmap (what's done, partial, todo).
> - `../../../Balsm-Draft/PHASE_ORDERING_DRIVERS.md` — why phases land in this order.
> - `../../../Balsm-Draft/BUSINESS_FEATURES.md` — feature-level specs.
> - `../../../Balsm-Draft/COMPLIANCE_REVIEW.md` — open compliance blockers per feature.

---

## 1. What This Repo Owns

`Balsm-API-DotNet/` is the **server tier** for every phase that has a `.NET` line in `STATE.md`. It does **not** own:

- Phase 1 Consumer Patient App → `balsm_app_flutter/` (Flutter + Supabase). Parallel track, independent.
- The Supabase identity layer for patient app → out of scope until the JWT bridge phase.
- The website (`website/`).

Any work item that says "Flutter", "Supabase RLS", "iOS / Android", or "website" is **not** for this repo.

## 2. Current State (verify against `Balsm-Draft/STATE.md` before acting)

Snapshot as of `STATE.md` last-edit (verify timestamps before quoting):

| Phase | Reality | Repo lives here? |
|---|---|---|
| **P0 Local Server Foundation** | ~95% — migrations applied on boot, Workspace/Entity/Branch CRUD, backup service, 98 tests green | Yes |
| **P14 Federation (out of order)** | ~50% — pairing codes, federation auth, tunnel registry, heartbeat, sync handshake | Yes |
| **P7 Offline Sync Queue** | ~40% — `SyncService` loop + inbound channel + heartbeat | Yes |
| **P2 Auth** | ~30% — admin auth done; user/role JWT, device registry, invite codes, Supabase bridge NOT done | Yes |
| **P3 Entity + Egypt L10n** | ~5% — module shell only | Yes |
| **P4 Pharmacy Inventory** | ~5% — DbContext shell only | Yes |
| **P5 Pharmacy POS** | ~5% — DbContext shell only | Yes |
| **P6 Customer + Analytics** | ~5% — DbContext shell only | Yes |
| **P8–P13 (Patient×Clinic → Doctor → Billing)** | 0–5% | Yes (except P1) |
| **P15–P21** | 0% | Yes (where applicable) |

> Always re-read `STATE.md` before quoting numbers in PRs or specs — these decay fast.

## 3. Phase Workflow

For any new feature in this repo:

1. **Identify the phase.** Open `Balsm-Draft/PHASED_ROADMAP.md`, find the phase the feature belongs to. If it doesn't fit a current phase, **stop** and surface the gap to the user.
2. **Check ordering.** Look at `STATE.md` and `PHASE_ORDERING_DRIVERS.md`. If the feature's phase has hard dependencies on incomplete phases, surface that before implementing — work out-of-order is flagged ⚠️ in `STATE.md` and creates rework risk.
3. **Check compliance.** Open `COMPLIANCE_REVIEW.md`. If the feature has a `CR-##` entry, the resolved option (`[x]`) defines the scope. Do not silently exceed it.
4. **Plan with C4.** Per `03-ai-agent-rules.md` §2 and root `CLAUDE.md`.
5. **Build, test, document.** Per `01-api-dev-rules.md` per-module checklist.
6. **Commit per task, not per phase.** Phases are big; commits are small.

## 4. Cross-Repo Phase Coordination

- A phase that requires a Flutter consumer change (most patient-facing phases) cannot be marked "done" by this repo alone. The PR description must link the consumer PR in `balsm_app_flutter/` or `website/`.
- A phase that depends on Supabase identity (P2 bridge, P8 patient×clinic) cannot start until the JWT bridge is wired. Surface dependency before scoping.
- Federation work (P14, P17) must keep `tunnel-registry/` (Cloudflare Worker) in sync — same PR.

## 5. ADRs to Respect (Balsm-Draft)

Verify each ADR against the latest `PHASED_ROADMAP.md` before quoting. Highlights as of last read:

- **ADR-01** Patient app uses Supabase. This API is **local-only** until the JWT bridge phase.
- **ADR-02** Medical records at P1 live in user-owned cloud (Drive/iCloud). This API holds **zero PHI** for patient-app users at that phase.
- **ADR-03** JWT bridge pattern at P16: Supabase issues JWTs → .NET cloud validates. **No auth migration ever needed.**
- **ADR-04** No business logic in Supabase. RLS + DB functions for simple rules only. Complex logic lives here.
- **ADR-08** Sync is a **single** mechanism for device↔server **and** server↔server. `SyncService` + `FederationService` share heartbeat + bounded-channel inbound + pairing-code handshake. Do not fork them into two engines.
- **ADR-09** Egypt-only launch at P1 to dodge HIPAA Supabase tier. Geo-fence by IP at registration.

When a change conflicts with an ADR, open a new ADR in `Balsm-Draft/` first.

## 6. Workflow Tools (used in `Balsm-Draft/`)

`Balsm-Draft/` uses spec-kit + GSD. From this repo's perspective:

```
/speckit.specify    # what to build + acceptance criteria
/speckit.plan       # technical architecture + data models
/speckit.tasks      # ordered task list with dependencies
/speckit.implement  # AI-assisted implementation

/gsd:discuss-phase  # gather context, resolve ambiguities
/gsd:plan-phase     # PLAN.md with tasks + threat model
/gsd:execute-phase  # implement following the plan
/gsd:verify-work    # UAT vs acceptance criteria
/gsd:code-review    # security + quality before merge
```

Use them from this repo when a phase needs structured planning. Do **not** invent a parallel planning system here.

## 7. Out-of-Phase Work — escape hatches

Some work doesn't fit any phase: dependency bumps, security patches, observability fixes, build-system hardening, dev-experience tweaks, doc cleanups. These are fine without a phase ticket, but:

- Keep the change small and isolated.
- If the fix touches a contract (DTO, endpoint, auth), it follows `02-cross-repo-contracts.md` regardless of "it's just a bump".
- If it changes a rule, update the relevant guideline file in the same PR (`03-ai-agent-rules.md` §7).

## 8. When `STATE.md` Disagrees With Reality

`STATE.md` is hand-curated and decays. Trust the code over the doc. If you find a discrepancy:

1. Verify with `dotnet build` + `dotnet test` + a quick `graphify query`.
2. Update `STATE.md` (it lives in `Balsm-Draft/`, not here — open a PR there).
3. Note the correction in this PR's description.
