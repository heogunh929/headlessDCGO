---
description: Strong-model review of a completed SET — audit every ported card 1:1 against DCGO/ (per card) and develop the primitives the STOP queue needs.
agent: reviewer
---
SET **$ARGUMENTS** (예: `ST1`) 를 검수하세요. 이 SET의 포팅이 끝난 뒤 발동합니다.

## 발동 전 완료 확인 ("완료" = 라이브 100%가 아님)
1. 이 SET의 각 색상 폴더에서, 원본 `DCGO/.../<SET>/<COLOR>/*.cs` 의 모든 카드가 **(미러 존재) 이거나 (`porting/stop/<SET>.<COLOR>.md`에 기록)** 인지 확인. 둘 다 아닌 카드가 있으면 아직 미완 → 그 색상은 `port-set <SET> <COLOR>` 로 먼저 돌리라고 보고하고 중단.
2. `bash scripts/run-tests.sh CardEffect.Binding.Auto` → `FAIL=0` 확인(이 SET의 라이브 미러가 바인딩 등록·빌드 green). FAIL이면 검수 전에 그 실패부터 보고.

## (A) 충실도 감사 — 원본 대비 1:1 (장당)
**색상별로 순회**하고, 색상 안에서 **카드 한 장씩**. 100장을 한 번에 훑고 "괜찮음" 금지 — 충실도는 장당 작업.
각 카드의 미러를 `DCGO/Assets/Scripts/CardEffect/<SET>/<COLOR>/<ID>.cs` 와 대조:
- 모든 `EffectTiming` 분기가 있는가? (누락 = 결함)
- 같은 `CardEffectFactory.<이름>(...)`, 같은 인자 순서·값인가?
- 술어/조건을 뭉개거나 "드무니까" 빠뜨린 게 있는가? (충실도 위반)

결함은 AS-IS 1:1을 지키며 수정(또는 정확한 수정 지시).

## (B) STOP 큐 소비 — 프리미티브 선행개발
`porting/stop/<SET>.*.md` 를 읽어 각 STOP에 대해:
- 부족한 프리미티브/타이밍을 엔진에 개발.
- `porting/docs/PRIMITIVE-CATALOG.md` 에 정확한 시그니처로 추가.
- 이제 회수 가능한 카드를 표시 → `port-set` 재실행 대상.

## 보고
색상별 감사 결과(결함/수정), 개발한 프리미티브 목록, 재-port-set 회수 대상 카드를 요약.
