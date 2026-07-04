#!/usr/bin/env bash
# port-batch.sh — brief-wired external driver for local-model porting (pipeline-v2).
#
# Replaces the model-self-managed `port-set` batch (which looped: the small model
# re-enumerated remaining cards via ls/grep/echo/wc every turn and never converged)
# with an EXTERNAL shell loop: ONE card = ONE bounded `opencode run` session. The
# model never orchestrates a batch, so there is nothing to loop on; a per-card
# timeout caps the blast radius of any single stuck card to that card.
#
# Steps:
#   1. Generate per-card briefs for the SET+COLOR (make-card-brief.py). The porter
#      reads the 3-6KB brief, never the 90KB catalog — no truncation, no lookup.
#   2. Enumerate not-yet-ported cards (skeleton stubs only; live cards untouched).
#   3. For each, run `opencode run --command port-card-brief "<ID>"` under a hard
#      wall-clock timeout. Record timeouts/failures; keep going.
#   4. Run the binding gate once at the end and print a summary.
#
# Usage:
#   porting/scripts/port-batch.sh <SET> <COLOR> [PER_CARD_TIMEOUT_SECS] [ID ...]
#   porting/scripts/port-batch.sh BT1 Red                 # whole not-yet-ported BT1/Red
#   porting/scripts/port-batch.sh BT1 Red 600             # with a 10-min per-card cap
#   porting/scripts/port-batch.sh BT1 Red 600 BT1_011 BT1_012   # only these ids
#
# NOTE: this only DRIVES the porter; it never commits. The user commits.

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"   # repo root (porting/scripts/ -> ../..)
cd "$ROOT"

SET="${1:-}"
COLOR="${2:-}"
TIMEOUT="${3:-900}"
if [[ -z "$SET" || -z "$COLOR" ]]; then
  echo "usage: porting/scripts/port-batch.sh <SET> <COLOR> [PER_CARD_TIMEOUT_SECS] [ID ...]" >&2
  exit 2
fi
shift $(( $# < 3 ? $# : 3 ))   # drop SET COLOR [TIMEOUT]; remaining args = explicit ids
EXPLICIT_IDS=("$@")

MIRROR_DIR="src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/${SET}/${COLOR}"
ORIG_DIR="DCGO/Assets/Scripts/CardEffect/${SET}/${COLOR}"
BRIEF_DIR="porting/briefs/${SET}.${COLOR}"

if [[ ! -d "$ORIG_DIR" ]]; then
  echo "port-batch: no originals at $ORIG_DIR" >&2
  exit 2
fi
if ! command -v opencode >/dev/null 2>&1; then
  echo "port-batch: opencode CLI not on PATH" >&2
  exit 2
fi

echo "== port-batch: $SET $COLOR (per-card timeout ${TIMEOUT}s) =="

# 1. Briefs — pre-resolve every card's symbols so the porter only translates.
echo "-- generating briefs --"
python3 porting/scripts/make-card-brief.py "$SET" "$COLOR"

# 2. Target selection — not-yet-ported = skeleton stub. Live cards are never touched.
targets=()
if (( ${#EXPLICIT_IDS[@]} > 0 )); then
  targets=("${EXPLICIT_IDS[@]}")
else
  while IFS= read -r f; do
    targets+=("$(basename "$f" .cs)")
  done < <(grep -l "Skeleton only" "$MIRROR_DIR"/*.cs 2>/dev/null | sort)
fi

if (( ${#targets[@]} == 0 )); then
  echo "port-batch: nothing to do — no skeleton stubs in $MIRROR_DIR"
  exit 0
fi
echo "-- ${#targets[@]} card(s) to port: ${targets[*]} --"

# 3. Per-card bounded sessions.
ported=(); timedout=(); failed=(); skipped=()
for id in "${targets[@]}"; do
  if [[ ! -f "$BRIEF_DIR/$id.md" ]]; then
    echo "  [$id] SKIP — no brief (make-card-brief did not emit it)"; skipped+=("$id"); continue
  fi
  echo "  [$id] porting (timeout ${TIMEOUT}s) ..."
  timeout "${TIMEOUT}s" opencode run --command port-card-brief "$id"
  rc=$?
  if (( rc == 124 )); then
    echo "  [$id] TIMEOUT — left as-is, needs 강모델 look"; timedout+=("$id")
  elif (( rc != 0 )); then
    echo "  [$id] opencode exit $rc"; failed+=("$id")
  elif grep -q "Skeleton only" "$MIRROR_DIR/$id.cs" 2>/dev/null; then
    echo "  [$id] still a stub after run — no mirror written"; failed+=("$id")
  else
    echo "  [$id] mirror written"; ported+=("$id")
  fi
done

# 4. Gate once.
echo "-- gate: run-tests.sh CardEffect.Binding.Auto --"
bash scripts/run-tests.sh CardEffect.Binding.Auto || echo "!! gate FAIL — re-diff the ported mirrors against originals"

echo
echo "== summary: $SET $COLOR =="
echo "  ported   : ${#ported[@]}  ${ported[*]:-}"
echo "  timedout : ${#timedout[@]}  ${timedout[*]:-}"
echo "  failed   : ${#failed[@]}  ${failed[*]:-}"
echo "  skipped  : ${#skipped[@]}  ${skipped[*]:-}"
echo "  STOP log : porting/stop/${SET}.${COLOR}.md"
