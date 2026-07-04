---
description: PLAN one card's headless mapping (planner agent). Reads DCGO original + brief, writes porting/data/plans/<SET>.<COLOR>/<ID>.md. No code.
agent: planner
---
카드 **$ARGUMENTS** 의 헤드리스 매핑을 계획하세요. `planner` 에이전트 규칙을 그대로 따릅니다.

1. 원본 읽기: `DCGO/Assets/Scripts/CardEffect/<SET>/<COLOR>/$ARGUMENTS.cs` (id에서 SET/COLOR 판별).
2. 브리프 읽기: `porting/briefs/<SET>.<COLOR>/$ARGUMENTS.md` (심볼·의도표·표현표 — 유일한 조회원천).
3. 계획 작성: `porting/data/plans/<SET>.<COLOR>/$ARGUMENTS.md` — 원본의 각 EffectTiming 분기마다
   (intent / headless 심볼 / args 1:1 / condition / STOP) 한 항목. 브리프에 없는 심볼 발명 금지, 불확실하면 STOP.
**C#은 쓰지 않습니다.** 계획만.
