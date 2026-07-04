#!/usr/bin/env bash
# port3.sh — 3-role local porting driver (pipeline-v2.5): planner → coder → analyzer → gate.
#
# Each card runs three bounded, single-role opencode sessions in sequence, handing off via files:
#   1. plan-card  (planner  = qwen3.6)        -> porting/data/plans/<SET>.<COLOR>/<ID>.md
#   2. code-card  (coder    = qwen3-coder:30b)-> src/.../CardEffect/<SET>/<COLOR>/<ID>.cs (mirror)
#   3. review-card(analyzer = gemma4:31b)     -> porting/data/reviews/<SET>.<COLOR>/<ID>.md
# Then the deterministic gate (build + binding) runs once at the end. The analyzer's verdict is
# ADVISORY (surfaced in the summary); the build/binding gate is the hard check. Briefs are generated
# first (make-card-brief.py). Never commits.
#
# Usage:
#   porting/scripts/port3.sh <SET> <COLOR> [PER_ROLE_TIMEOUT_SECS] [ID ...]
#   porting/scripts/port3.sh BT1 Blue                 # all not-yet-ported BT1/Blue
#   porting/scripts/port3.sh BT1 Blue 900 BT1_003     # one card, 15-min per-role cap

set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"; cd "$ROOT"

SET="${1:-}"; COLOR="${2:-}"; TIMEOUT="${3:-900}"
if [[ -z "$SET" || -z "$COLOR" ]]; then
  echo "usage: porting/scripts/port3.sh <SET> <COLOR> [PER_ROLE_TIMEOUT] [ID ...]" >&2; exit 2
fi
shift $(( $# < 3 ? $# : 3 )); EXPLICIT_IDS=("$@")

MIRROR_DIR="src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/${SET}/${COLOR}"
ORIG_DIR="DCGO/Assets/Scripts/CardEffect/${SET}/${COLOR}"
BRIEF_DIR="porting/briefs/${SET}.${COLOR}"
[[ -d "$ORIG_DIR" ]] || { echo "port3: no originals at $ORIG_DIR" >&2; exit 2; }
command -v opencode >/dev/null 2>&1 || { echo "port3: opencode not on PATH" >&2; exit 2; }

echo "== port3: $SET $COLOR (planner→coder→analyzer, per-role ${TIMEOUT}s) =="
echo "-- briefs --"; python3 porting/scripts/make-card-brief.py "$SET" "$COLOR"

targets=()
if (( ${#EXPLICIT_IDS[@]} > 0 )); then targets=("${EXPLICIT_IDS[@]}")
else while IFS= read -r f; do targets+=("$(basename "$f" .cs)"); done \
  < <(grep -l "Skeleton only" "$MIRROR_DIR"/*.cs 2>/dev/null | sort); fi
(( ${#targets[@]} )) || { echo "port3: nothing to do (no skeleton stubs)"; exit 0; }
echo "-- ${#targets[@]} card(s): ${targets[*]} --"

run_role() { timeout "${TIMEOUT}s" opencode run --command "$1" "$2" >/dev/null 2>&1; }

ported=(); flagged=(); failed=()
for id in "${targets[@]}"; do
  [[ -f "$BRIEF_DIR/$id.md" ]] || { echo "  [$id] SKIP — no brief"; failed+=("$id"); continue; }
  echo "  [$id] plan ..."   ; run_role plan-card   "$id"
  [[ -f "porting/data/plans/${SET}.${COLOR}/$id.md" ]] || { echo "  [$id] no plan — skip"; failed+=("$id"); continue; }
  echo "  [$id] code ..."   ; run_role code-card   "$id"
  if grep -q "Skeleton only" "$MIRROR_DIR/$id.cs" 2>/dev/null; then echo "  [$id] still stub — coder failed"; failed+=("$id"); continue; fi
  echo "  [$id] review ..." ; run_role review-card "$id"
  if grep -qi 'verdict:.*FLAG' "porting/data/reviews/${SET}.${COLOR}/$id.md" 2>/dev/null; then
    echo "  [$id] analyzer FLAGGED (see review)"; flagged+=("$id")
  else
    echo "  [$id] ported (analyzer PASS)"; ported+=("$id")
  fi
done

echo "-- gate: run-tests.sh CardEffect.Binding.Auto --"
bash scripts/run-tests.sh CardEffect.Binding.Auto || echo "!! gate FAIL — re-diff mirrors vs originals"

echo; echo "== summary: $SET $COLOR =="
echo "  ported(PASS): ${#ported[@]}  ${ported[*]:-}"
echo "  flagged     : ${#flagged[@]}  ${flagged[*]:-}   (porting/data/reviews/${SET}.${COLOR}/)"
echo "  failed      : ${#failed[@]}  ${failed[*]:-}"
