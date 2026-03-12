# Claude Code Instructions

Read and follow the agent instructions in the Roadmap repo: [AGENTS.md](../Balsam-Roadmap/agents/rules/AGENTS.md).

## API-Specific Notes

- .NET modular monolith — respect module and layer boundaries (API → Business → Data)
- All endpoints require authentication and permission checks
- Use parameterized queries — never concatenate user input
- Soft-delete only — never hard-delete clinical data
- Every new endpoint needs integration tests
