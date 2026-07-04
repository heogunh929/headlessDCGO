---
description: Port one Digimon card 1:1 from DCGO/ into the headless engine, driven by the card's pre-extracted brief (pipeline-v2). Reads docs/porting/briefs/<SET>.<COLOR>/<ID>.md instead of injecting the 90KB catalog.
agent: porter
---
카드 **$ARGUMENTS** 를 그 카드의 **브리프**로 1:1 포팅하세요. `port-brief` 스킬의 절차·규칙을 그대로 따릅니다. **90KB 카탈로그를 읽지 않습니다** — 브리프가 이 카드의 카탈로그 슬라이스입니다.

## 규칙 (필수)
- **AS-IS 1:1 미러**: 원본과 같은 타이밍 분기 · 같은 `CardEffectFactory.<이름>(...)` 호출을 그대로. 로직 변경/단순화 금지.
- **브리프가 조회의 진실원천**: 브리프의 `심볼 조회 결과`·`코루틴 의도→팩토리`·`condition 표현 치환` 3섹션은 **그대로 사용(STOP 금지)**. STOP은 오직 `## 미해결 심볼 (자동조회 실패 — STOP 후보)` 섹션 심볼만.
- **프리미티브 개발 금지 · 불확실하면 STOP.** 발명 금지.
- 쓰기는 화이트리스트(카드 폴더 · `docs/porting/stop/`)만. `tests/` 안 씀(자동바인딩 테스트는 강모델 소유). `DCGO/` 읽기 전용, 커밋 금지.

## 절차
1. **브리프 확보.** `docs/porting/briefs/<SET>.<COLOR>/$ARGUMENTS.md` 읽기(id에서 SET/COLOR 판별). 없으면 먼저 생성: `python3 scripts/make-card-brief.py <SET> <COLOR> $ARGUMENTS`.
2. **원본 읽기.** `DCGO/Assets/Scripts/CardEffect/<SET>/<COLOR>/$ARGUMENTS.cs` 의 모든 `EffectTiming` 분기 확인.
3. **분기별 매핑.** 각 분기를 브리프의 해결된 심볼/의도표/표현치환으로 옮긴다. 인자는 원본 1:1.
4. **미러 작성(스텁 교체).** `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<COLOR>/$ARGUMENTS.cs`. **브리프의 `## 미러 뼈대` 틀을 그대로 사용** — `namespace ...CardEffect.<SET>.<COLOR>;` 선언 필수(없으면 게이트 FAIL), `override IReadOnlyList<ICardEffect>`, `public sealed`. **테스트는 쓰지 않는다.**
5. **부분 포팅.** 미해결 심볼을 쓰는 분기만 `// STOP: <심볼> — 강모델` 처리(그 분기 `cardEffects.Add` 생략), 나머지는 포팅. 카드 통째 STOP 금지.
6. **STOP 기록.** 실제로 STOP 한 분기가 있을 때**만** `docs/porting/stop/<SET>.<COLOR>.md` 에 `$ARGUMENTS | 이유 | 원본심볼` append. STOP 이 없으면 stop 로그에 아무것도 쓰지 않는다.
7. **게이트.** `bash scripts/run-tests.sh CardEffect.Binding.Auto` → `FAIL=0` 확인. green 전까지 완료 선언 금지.

## 참조 (반드시 먼저 읽기)
- 브리프 기반 절차·계약: `port-brief` 스킬(`.opencode/skill/port-brief/SKILL.md`)
- 이 카드의 브리프: `docs/porting/briefs/<SET>.<COLOR>/$ARGUMENTS.md`  ← **이것이 유일한 조회원천. 여기 있는 심볼은 STOP 금지.**
