#!/usr/bin/env bash
# Local full test-suite runner (Git Bash / Linux / macOS).
#
# Runs every tests/*.Tests.csproj and reports a PASS/FAIL summary. Use this before committing Phase 4
# card-porting changes — it is the real regression gate (unlike CI, which only compiles, because the
# AS-IS parity tests need the git-ignored DCGO/ sources that exist locally).
#
# Usage:   scripts/run-tests.sh            # run all
#          scripts/run-tests.sh G3.5       # run only projects matching a substring
set -u
cd "$(dirname "$0")/.."

# Prefer the locally pinned SDK if present.
[ -d ".dotnet" ] && export PATH="$PWD/.dotnet:$PATH"

filter="${1:-}"
pass=0; fail=0; failed=()

run() { timeout 180 dotnet run --project "$1" -c Debug -v q --nologo 2>&1; }

for csproj in $(find tests -name '*.Tests.csproj' | sort); do
  name=$(basename "$csproj" .csproj)
  [ -n "$filter" ] && [[ "$name" != *"$filter"* ]] && continue

  out=$(run "$csproj"); code=$?
  # Retry on a build-server lock (no test output produced) — a known concurrency flake.
  tries=0
  while [ $code -ne 0 ] && ! echo "$out" | grep -q 'test(s)' && [ $tries -lt 3 ]; do
    sleep 2; dotnet build-server shutdown >/dev/null 2>&1
    out=$(run "$csproj"); code=$?; tries=$((tries+1))
  done

  if [ $code -eq 0 ]; then
    pass=$((pass+1)); echo "PASS $name"
  else
    fail=$((fail+1)); failed+=("$name")
    echo "FAIL $name"
    echo "$out" | grep -iE 'FAIL |Exception|expected' | head -3
  fi
done

echo ""
echo "===================================="
echo "SUMMARY: PASS=$pass FAIL=$fail TOTAL=$((pass+fail))"
if [ $fail -gt 0 ]; then
  printf '  - %s\n' "${failed[@]}"
  exit 1
fi
