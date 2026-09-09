#!/usr/bin/env bash
# Cursor/Claude graphify reminder: when the agent greps/finds in a repo that
# has graphify-out/graph.json, inject the graphify-first instruction.
set -euo pipefail

input="$(cat)"
cmd="$(printf '%s' "$input" | python3 -c 'import json,sys
try:
    d=json.load(sys.stdin)
except Exception:
    raise SystemExit(0)
print(d.get("tool_input", d).get("command") if isinstance(d.get("tool_input"), dict) else d.get("command") or "")
' 2>/dev/null || true)"

printf '%s' "$cmd" | grep -Eq '(^|[;&| ]|\b)(grep|rg|ripgrep|find|fd|ack|ag)( |$)' || exit 0

root="${CURSOR_PROJECT_DIR:-${CLAUDE_PROJECT_DIR:-$(pwd)}}"
[ -f "$root/graphify-out/graph.json" ] || exit 0

msg='graphify: knowledge graph at graphify-out/. For focused questions, run `graphify query "<question>"` (scoped subgraph, usually much smaller than GRAPH_REPORT.md) instead of grepping raw files. Read GRAPH_REPORT.md only for broad architecture context.'
python3 -c 'import json,sys; t=sys.stdin.read(); print(json.dumps({"permission":"allow","additional_context":t,"agent_message":t,"hookSpecificOutput":{"hookEventName":"PreToolUse","additionalContext":t}}))' <<<"$msg"
exit 0
