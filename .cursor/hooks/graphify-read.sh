#!/usr/bin/env bash
# Cursor/Claude graphify reminder: when the agent reads source files in a repo
# that has graphify-out/graph.json, inject the graphify-first instruction.
set -euo pipefail

input="$(cat)"
hit="$(printf '%s' "$input" | python3 -c '
import json, sys
try:
    d = json.load(sys.stdin)
except Exception:
    raise SystemExit(0)
t = d.get("tool_input", d) if isinstance(d.get("tool_input"), dict) else d
s = " ".join(str(t.get(k) or d.get(k) or "") for k in ("file_path", "path", "pattern", "target_file")).lower().replace("\\\\", "/")
exts = (".py",".js",".ts",".tsx",".jsx",".go",".rs",".java",".rb",".c",".h",".cpp",".hpp",".cc",".cs",".kt",".swift",".php",".scala",".lua",".sh",".md",".rst",".txt",".mdx",".dart")
sys.stdout.write("1" if "graphify-out/" not in s and any(e in s for e in exts) else "")
' 2>/dev/null || true)"

[ "$hit" = 1 ] || exit 0
root="${CURSOR_PROJECT_DIR:-${CLAUDE_PROJECT_DIR:-$(pwd)}}"
[ -f "$root/graphify-out/graph.json" ] || exit 0

msg='MANDATORY: graphify-out/graph.json exists. You MUST run graphify before reading source files. Use: `graphify query "<question>"` (scoped subgraph), `graphify explain "<concept>"`, or `graphify path "<A>" "<B>"`. Only read raw files after graphify has oriented you, or to modify/debug specific lines. This rule applies to subagents too — include it in every subagent prompt involving code exploration.'
python3 -c 'import json,sys; t=sys.stdin.read(); print(json.dumps({"permission":"allow","additional_context":t,"agent_message":t,"hookSpecificOutput":{"hookEventName":"PreToolUse","additionalContext":t}}))' <<<"$msg"
exit 0
