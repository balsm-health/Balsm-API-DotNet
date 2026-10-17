# dotnet-skills (community — Aaronontheweb)
Install: `/plugin marketplace add Aaronontheweb/dotnet-skills` then `/plugin install dotnet-skills`. Routing table + precedence rule live in root `CLAUDE.md` (`## .NET Skills Router`). Balsm rules override dotnet-skills recommendations on conflict.

# dotnet/skills (official .NET team marketplace)
Install: `/plugin marketplace add dotnet/skills` then `/plugin install <plugin>@dotnet-agent-skills` per plugin (full install list + routing in root `CLAUDE.md` `## dotnet/skills Router`). Precedence: Balsm rules > `dotnet/skills` (official) > `dotnet-skills` (Aaronontheweb community).

# guidelines
Read [`guidelines/00-INDEX.md`](./guidelines/00-INDEX.md) before non-trivial work in this repo. The index points at:
- `01-api-dev-rules.md` — .NET 10 modular monolith rules
- `02-cross-repo-contracts.md` — OpenAPI / Insomnia / Flutter / website sync
- `03-ai-agent-rules.md` — graphify-first, C4-first, test gates, commit policy, required skills
- `04-phase-workflow.md` — Balsm-Draft phase alignment (read `Balsm-Draft/STATE.md` before quoting status)
- `05-nfr.md` — latency, availability, observability, security floor
- `06-coding-best-practices.md` — Result<T>, async, logging, EF Core, idempotency, forbidden patterns

These deltas extend `Balsm-Core/agents/rules/` — they never weaken Core. When rules collide, regulatory + Core > root `CLAUDE.md` > these guidelines.

# graphify
- **graphify** (`.claude/skills/graphify/SKILL.md`) - any input to knowledge graph. Trigger: `/graphify`
When the user types `/graphify`, invoke the Skill tool with `skill: "graphify"` before doing anything else.
