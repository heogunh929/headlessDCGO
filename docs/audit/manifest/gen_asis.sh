#!/usr/bin/env bash
# Generates docs/audit/manifest/asis_files.csv — machine inventory of all .cs files
# under DCGO/Assets/Scripts/. Re-runnable; overwrites its own output only.
#
# Columns: relpath(relative to Assets/Scripts/), layer(Script|CardEffect|기타), lines
#
# Notes:
# - AS-IS files may be non-UTF8; `wc -l` counts newline bytes regardless of encoding,
#   so it is safe here (unlike `grep`, which silently skips binary-looking files —
#   see memory note "Grep binary-skip pitfall"). We intentionally do NOT use grep.
# - relpath is CSV-escaped (quoted, embedded quotes doubled) in case any path
#   contains a comma.
# - Layer/line counters are accumulated in-loop (not by re-parsing the CSV
#   afterward with a naive comma split), since relpath could itself contain a
#   comma inside quotes and break a naive `awk -F','` re-parse.

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
SCRIPTS_ROOT="$REPO_ROOT/DCGO/Assets/Scripts"
OUT_CSV="$REPO_ROOT/docs/audit/manifest/asis_files.csv"

if [[ ! -d "$SCRIPTS_ROOT" ]]; then
  echo "ERROR: $SCRIPTS_ROOT not found" >&2
  exit 1
fi

echo "relpath,layer,lines" > "$OUT_CSV"

total_rows=0
count_script=0
count_cardeffect=0
count_other=0
lines_script=0
lines_cardeffect=0
lines_other=0

# Use process substitution (not a pipe) so counter variables survive the loop.
while IFS= read -r -d '' f; do
  relpath="${f#"$SCRIPTS_ROOT"/}"

  case "$relpath" in
    Script/*)
      layer="Script"
      ;;
    CardEffect/*)
      layer="CardEffect"
      ;;
    *)
      layer="기타"
      ;;
  esac

  lines="$(wc -l < "$f" | tr -d ' ')"

  # CSV-escape relpath: wrap in quotes, double any embedded quotes.
  esc_relpath="${relpath//\"/\"\"}"
  echo "\"${esc_relpath}\",${layer},${lines}" >> "$OUT_CSV"

  total_rows=$((total_rows + 1))
  case "$layer" in
    Script) count_script=$((count_script + 1)); lines_script=$((lines_script + lines)) ;;
    CardEffect) count_cardeffect=$((count_cardeffect + 1)); lines_cardeffect=$((lines_cardeffect + lines)) ;;
    *) count_other=$((count_other + 1)); lines_other=$((lines_other + lines)) ;;
  esac
done < <(find "$SCRIPTS_ROOT" -type f -name '*.cs' -print0 | sort -z)

echo "Wrote: $OUT_CSV"
echo "Total data rows: $total_rows"
echo "Layer counts (files / total lines):"
echo "  Script:     $count_script / $lines_script"
echo "  CardEffect: $count_cardeffect / $lines_cardeffect"
echo "  기타:        $count_other / $lines_other"
