#!/usr/bin/env bash
# gen_tobe.sh — TO-BE 파일 인벤토리 기계 생성 (인벤토리 전용, 판정/해석 없음)
#
# 대상: src/HeadlessDCGO.Engine/ 아래 전 .cs (bin/obj 제외)
# 출력: docs/audit/manifest/tobe_files.csv
#   컬럼: relpath,layer,lines,card_state
#     - relpath   : Assets/Scripts/ 아래는 그 기준 상대경로(예: Script/Foo.cs, CardEffect/Bar.cs)
#                   Headless/ 아래(및 그 외 기타)는 src/HeadlessDCGO.Engine/ 기준 상대경로(Headless/ 접두 유지)
#     - layer     : Script | CardEffect | Headless | 기타
#     - lines     : wc -l
#     - card_state: CardEffect만 SHELL(마커 "TODO: Skeleton only" 보유) | IMPL ; 그 외 layer는 n/a
#
# 재실행 가능. 이 스크립트는 저장소를 수정하지 않는다(read-only 스캔).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
ENGINE_DIR="$REPO_ROOT/src/HeadlessDCGO.Engine"
SCRIPTS_DIR="$ENGINE_DIR/Assets/Scripts"
OUT_CSV="$REPO_ROOT/docs/audit/manifest/tobe_files.csv"

if [ ! -d "$ENGINE_DIR" ]; then
  echo "ERROR: $ENGINE_DIR not found" >&2
  exit 1
fi

# CSV 필드 이스케이프: 콤마/큰따옴표/개행 포함 시 큰따옴표로 감싸고 내부 큰따옴표는 두 배로
csv_escape() {
  local s="$1"
  if [[ "$s" == *,* || "$s" == *\"* || "$s" == *$'\n'* ]]; then
    s="${s//\"/\"\"}"
    s="\"$s\""
  fi
  printf '%s' "$s"
}

echo "relpath,layer,lines,card_state" > "$OUT_CSV"

# find로 전 .cs 수집(bin/obj 제외), NUL 구분으로 안전 처리
while IFS= read -r -d '' f; do
  # f는 $ENGINE_DIR 기준 절대경로

  if [[ "$f" == "$SCRIPTS_DIR/Script/"* ]]; then
    layer="Script"
    relpath="${f#"$SCRIPTS_DIR"/}"
    card_state="n/a"
  elif [[ "$f" == "$SCRIPTS_DIR/CardEffect/"* ]]; then
    layer="CardEffect"
    relpath="${f#"$SCRIPTS_DIR"/}"
    if grep -qF --binary-files=text "TODO: Skeleton only" "$f"; then
      card_state="SHELL"
    else
      card_state="IMPL"
    fi
  elif [[ "$f" == "$ENGINE_DIR/Headless/"* ]]; then
    layer="Headless"
    relpath="${f#"$ENGINE_DIR"/}"
    card_state="n/a"
  else
    layer="기타"
    relpath="${f#"$ENGINE_DIR"/}"
    card_state="n/a"
  fi

  lines=$(wc -l < "$f" | tr -d ' ')

  {
    printf '%s,' "$(csv_escape "$relpath")"
    printf '%s,' "$layer"
    printf '%s,' "$lines"
    printf '%s\n' "$card_state"
  } >> "$OUT_CSV"
done < <(find "$ENGINE_DIR" -type f -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' -print0 | sort -z)

# ---- 자가 검산 ----
total_rows=$(($(wc -l < "$OUT_CSV") - 1))
n_script=$(awk -F',' 'NR>1 && $2=="Script"' "$OUT_CSV" | wc -l)
n_cardeffect=$(awk -F',' 'NR>1 && $2=="CardEffect"' "$OUT_CSV" | wc -l)
n_headless=$(awk -F',' 'NR>1 && $2=="Headless"' "$OUT_CSV" | wc -l)
n_other=$(awk -F',' 'NR>1 && $2=="기타"' "$OUT_CSV" | wc -l)
n_shell=$(awk -F',' 'NR>1 && $4=="SHELL"' "$OUT_CSV" | wc -l)
n_impl=$(awk -F',' 'NR>1 && $4=="IMPL"' "$OUT_CSV" | wc -l)

echo "=== gen_tobe.sh 자가 검산 ==="
echo "출력: $OUT_CSV"
echo "총 행수(헤더 제외): $total_rows"
echo "layer별: Script=$n_script CardEffect=$n_cardeffect Headless=$n_headless 기타=$n_other"
echo "CardEffect card_state: SHELL=$n_shell IMPL=$n_impl (합=$((n_shell+n_impl)), CardEffect 총=$n_cardeffect)"
echo ""
echo "기존 실측 대조: Script 377 / CardEffect 4013(SHELL 3574/IMPL 439) / Headless 208"
[ "$n_script" -eq 377 ] && echo "  Script: 일치" || echo "  Script: 불일치 (실측 $n_script)"
[ "$n_cardeffect" -eq 4013 ] && echo "  CardEffect: 일치" || echo "  CardEffect: 불일치 (실측 $n_cardeffect)"
[ "$n_headless" -eq 208 ] && echo "  Headless: 일치" || echo "  Headless: 불일치 (실측 $n_headless)"
[ "$n_shell" -eq 3574 ] && echo "  SHELL: 일치" || echo "  SHELL: 불일치 (실측 $n_shell)"
[ "$n_impl" -eq 439 ] && echo "  IMPL: 일치" || echo "  IMPL: 불일치 (실측 $n_impl)"
