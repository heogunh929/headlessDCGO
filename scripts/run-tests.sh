#!/usr/bin/env bash
# Local full test-suite runner (Git Bash / Linux / macOS).
#
# Runs every tests/*.Tests.csproj and reports a PASS/FAIL summary. Use this before committing
# card-porting changes — it is the real regression gate (unlike CI, which only compiles, because the
# AS-IS parity tests need the git-ignored DCGO/ sources that exist locally).
#
# Two-phase to stay fast AND safe:
#   1. BUILD phase — build the engine, then each test project, at moderate parallelism (BUILD_JOBS).
#      Heavy concurrent MSBuild was observed to corrupt output assemblies on Windows (Win32 1392), so
#      builds are throttled.
#   2. RUN phase — run each prebuilt test with `--no-build` at high parallelism (JOBS). Launching
#      prebuilt executables concurrently is safe and fast.
#
# Usage:   scripts/run-tests.sh                 # all
#          scripts/run-tests.sh G3.5            # only projects whose name matches a substring
#          JOBS=8 BUILD_JOBS=2 scripts/run-tests.sh
set -u
cd "$(dirname "$0")/.."

[ -d ".dotnet" ] && export PATH="$PWD/.dotnet:$PATH"

filter="${1:-}"
cpu=$(nproc 2>/dev/null || echo 4)
jobs="${JOBS:-$cpu}"
# Builds are the corruption-prone step → keep their parallelism modest regardless of core count.
build_jobs="${BUILD_JOBS:-$(( cpu < 6 ? cpu : 6 ))}"
[ "$jobs" -lt 1 ] 2>/dev/null && jobs=1
[ "$build_jobs" -lt 1 ] 2>/dev/null && build_jobs=1

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

projects=()
for csproj in $(find tests -name '*.Tests.csproj' | sort); do
  name=$(basename "$csproj" .csproj)
  [ -n "$filter" ] && [[ "$name" != *"$filter"* ]] && continue
  projects+=("$csproj")
done

throttle() { local n="$1"; while [ "$(jobs -r -p | wc -l)" -ge "$n" ]; do sleep 0.2; done; }

echo "Building engine ..."
dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj -c Debug -v q --nologo >/dev/null 2>&1

echo "Building ${#projects[@]} test project(s) (build_jobs=$build_jobs) ..."
for csproj in "${projects[@]}"; do
  name=$(basename "$csproj" .csproj)
  throttle "$build_jobs"
  ( dotnet build "$csproj" -c Debug -v q --nologo >"$tmp/$name.build" 2>&1; echo $? >"$tmp/$name.buildcode" ) &
done
wait

run() { timeout 180 dotnet run --project "$1" -c Debug --no-build -v q --nologo 2>&1; }

worker() {
  local csproj="$1" name="$2"
  if [ "$(cat "$tmp/$name.buildcode" 2>/dev/null)" != "0" ]; then
    { echo "FAIL"; echo "BUILD FAILED"; tail -3 "$tmp/$name.build" 2>/dev/null; } >"$tmp/$name.status"
    echo "FAIL $name (build)"
    return
  fi

  local out code tries=0
  out=$(run "$csproj"); code=$?
  # Retry once with a full rebuild if the prebuilt run produced no test output (stale/corrupt artifact).
  while [ $code -ne 0 ] && ! echo "$out" | grep -q 'test(s)' && [ $tries -lt 2 ]; do
    sleep 1; dotnet build "$csproj" -c Debug -v q --nologo >/dev/null 2>&1
    out=$(timeout 180 dotnet run --project "$csproj" -c Debug --no-build -v q --nologo 2>&1); code=$?; tries=$((tries+1))
  done

  if [ $code -eq 0 ]; then
    echo "PASS" >"$tmp/$name.status"; echo "PASS $name"
  else
    { echo "FAIL"; echo "$out" | grep -iE 'FAIL |Exception|expected' | head -3; } >"$tmp/$name.status"
    echo "FAIL $name"
  fi
}

echo "Running tests (jobs=$jobs)${filter:+, filter='$filter'} ..."
for csproj in "${projects[@]}"; do
  name=$(basename "$csproj" .csproj)
  throttle "$jobs"
  worker "$csproj" "$name" &
done
wait

pass=0; fail=0; failed=()
for f in "$tmp"/*.status; do
  [ -e "$f" ] || continue
  if head -1 "$f" | grep -q PASS; then pass=$((pass+1)); else fail=$((fail+1)); failed+=("$(basename "$f" .status)"); fi
done

echo ""
echo "===================================="
echo "SUMMARY: PASS=$pass FAIL=$fail TOTAL=$((pass+fail))  (jobs=$jobs build_jobs=$build_jobs)"
if [ $fail -gt 0 ]; then
  printf '  - %s\n' "${failed[@]}"
  exit 1
fi
