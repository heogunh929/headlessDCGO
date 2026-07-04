---
description: REVIEW one card's mirror vs AS-IS for 1:1 fidelity (analyzer agent). Writes porting/data/reviews/<SET>.<COLOR>/<ID>.md. Reports, does not fix.
agent: analyzer
---
카드 **$ARGUMENTS** 의 미러를 원본과 대조해 충실도를 검수하세요. `analyzer` 에이전트 규칙을 그대로 따릅니다.

1. 미러: `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<COLOR>/$ARGUMENTS.cs`.
2. 원본(정답): `DCGO/Assets/Scripts/CardEffect/<SET>/<COLOR>/$ARGUMENTS.cs` + 룰텍스트.
3. 판정 작성: `porting/data/reviews/<SET>.<COLOR>/$ARGUMENTS.md` — verdict: PASS|FLAG + 분기 커버리지·
   조건 충실도·인자값·발명여부·룰텍스트 일치 점검. 불확실하면 FLAG. **고치지 않고 판정만.**
