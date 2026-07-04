---
description: Port one Digimon card 1:1 from DCGO/ into the headless engine following the recipe
---
당신은 headlessDCGO 카드 포팅 작업자입니다. 카드 **$ARGUMENTS** 를 아래 절차대로 1:1 포팅하세요.

## 규칙 (필수)
- **AS-IS 1:1 미러**: 원본과 같은 타이밍 분기 · 같은 `CardEffectFactory.<이름>(...)` 호출을 그대로 옮긴다. 로직 변경/단순화 금지.
- **프리미티브 개발 금지**: 원본이 부르는 팩토리는 카탈로그에 이미 있다. 없으면 **STOP**(강모델에 넘김).
- `DCGO/` 는 읽기 전용. `bin/`·`obj/` 건드리지 않는다.
- 커밋하지 않는다(사용자가 직접).

## 절차
1. 원본 읽기: `DCGO/Assets/Scripts/CardEffect/<SET>/<COLOR>/$ARGUMENTS.cs` (id에서 SET/COLOR 판별. 예: `ST1_06` → set `ST1`, color는 원본 경로에서 확인).
2. 각 `CardEffectFactory.<Method>` 호출을 카탈로그에서 조회해 헤드리스 시그니처로 인자를 맞춘다.
3. 미러 파일 작성/수정: `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<COLOR>/$ARGUMENTS.cs`.
4. 그룹 테스트 `tests/CardEffect.<SET>.<COLOR>.Tests/` 에 이 카드 sub-test 추가(효과가 라이브인지 단언).
5. 게이트: `bash scripts/run-tests.sh` → `PASS=N FAIL=0` 확인.
6. STOP 조건(팩토리/타이밍 부재, nested 커스텀 로직, 특수플레이 레시피 필요)이면 `<ID> | 이유 | 심볼` 로 기록하고 넘어간다.

## 참조 (반드시 먼저 읽기)
- 상세 절차·템플릿·예시: @docs/porting/PORTING-RECIPE.md
- 사용 가능한 프리미티브 전수(시그니처): @docs/porting/PRIMITIVE-CATALOG.md

먼저 위 두 문서를 읽고, 원본을 읽은 뒤, 미러 파일과 테스트를 작성하고, 테스트를 돌려 green을 확인하세요.
