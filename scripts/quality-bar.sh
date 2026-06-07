#!/usr/bin/env bash
# Enforces the CLAUDE.md engineering quality bar in CI.
#
#   1. async-only      — no sync-over-async (.Result / .Wait() / .GetAwaiter().GetResult())  [FAIL]
#   2. no-PHI-in-logs  — no patient/PII field names inside logging calls                     [FAIL]
#   3. Result<T>       — MediatR handlers should return Result/Result<T>                     [WARN]
#
# Scope: src/ only (production code). Excludes obj/, bin/, Migrations/ (generated),
# and *.Designer.cs. Add a trailing `// sync-ok` or `// phi-ok` comment on a line to
# acknowledge a reviewed exception. Portable to bash 3.2 (macOS) and 4+ (CI).
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SRC="$ROOT/src"
fail=0

# grep across production .cs files. Args are passed through to grep.
src_grep() {
  find "$SRC" -name '*.cs' \
    -not -path '*/obj/*' -not -path '*/bin/*' \
    -not -path '*/Migrations/*' -not -name '*.Designer.cs' -print0 \
    | xargs -0 grep "$@" 2>/dev/null || true
}

echo "── 1. async-only (no sync-over-async) ──────────────────────────"
ASYNC_HITS="$(src_grep -nE '\.Result\b|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)' \
  | grep -v '// sync-ok' \
  | grep -vE 'Result<|\bResult\.|IResult|ActionResult|\.Value\b' || true)"
if [ -n "$ASYNC_HITS" ]; then
  echo "✗ sync-over-async found (use await; annotate reviewed cases with // sync-ok):"
  echo "$ASYNC_HITS"
  fail=1
else
  echo "✓ none"
fi

echo
echo "── 2. no-PHI-in-logs ───────────────────────────────────────────"
# PHI/PII field names that must never be interpolated into log output.
PHI='NationalId|National_Id|Ssn\b|DateOfBirth|\bDob\b|Diagnosis|BloodType|MedicalRecord|PatientName|HomeAddress|Passport'
PHI_HITS="$(src_grep -nE '_?[Ll]og(ger)?\.(Log)?(Trace|Debug|Information|Warning|Error|Critical)' \
  | grep -v '// phi-ok' | grep -E "$PHI" || true)"
if [ -n "$PHI_HITS" ]; then
  echo "✗ possible PHI/PII in log statements (remove or annotate with // phi-ok):"
  echo "$PHI_HITS"
  fail=1
else
  echo "✓ none"
fi

echo
echo "── 3. Result<T> on MediatR handlers (advisory) ─────────────────"
HANDLER_WARN="$(src_grep -nE 'IRequestHandler<' | grep -v 'Result' || true)"
if [ -n "$HANDLER_WARN" ]; then
  echo "⚠ handlers whose IRequestHandler signature does not mention Result (review):"
  echo "$HANDLER_WARN"
else
  echo "✓ all handlers return Result"
fi

echo
if [ "$fail" -ne 0 ]; then
  echo "Quality bar: FAILED"
  exit 1
fi
echo "Quality bar: PASSED"
