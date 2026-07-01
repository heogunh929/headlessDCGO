# BT-PRE-A — 액션형 선결 프리미티브 (BT1–3 공유)

> **목표:** BT1–3 카드 포팅에 공통으로 쓰이는 **액션형 카드-facing 프리미티브 5종**을 원본 이름·시그니처 1:1로 신설. 엔진 메커니즘은 대부분 이미 존재(아래 "엔진 seam") → 카드-facing 등록 경로만 만든다. 상위 묶음: `docs/audit/bt1_3_prereq_primitives_goals.md`(BT-PRE-A/B/C 중 A).
>
> **순서(빈도·의존):** A1 Draw(27) → A2 SimplifiedSelect(20) → A3 Destroy(6) → A4 HatchDigiEgg(1) → A5 PlayCard(1). **각 항목 독립 green 게이트 후 다음.** (A5 PlayCard는 jogress/burst/appfusion 분기가 무거우므로 **BT 사용 범위(단순 play)만** 미러하고 나머지는 명시 미구현으로 남긴다.)

## 공통 종료 기준 (각 항목)
- `bash scripts/run-tests.sh` 전체 green(FAIL=0) + **격리/픽스처 테스트로 동작 단언** + `tools/RuleAudit` 0.
- **AS-IS 미러**: 원본 클래스명·생성자 시그니처·가드 1:1. 카드-facing 술어는 헤드리스 엔티티-id 관용. 가드 완화·추측 = FAIL.
- **probe-first**: 신설 전 엔진 seam 확인(아래). `Headless/**` change-control.
- 신설 위치: 카드-facing 프리미티브는 `Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs`(또는 원본 대응 파일), 해소는 `ActivatedEffectResolver.ResolveListAsync`에 case 추가(기존 `ActivatedSelectEffect`/`SuspendCostReductionEffect` 패턴과 동형). 픽스처 `TestFixtures/Tfx*.cs`.

## 기준 / 현황
- 기준 HEAD `d8b9daba`(+ 미커밋 EX8_074 fix·brick 2b·문서). 전체 **246 green, RuleAudit 0**.

---

## A1 — `DrawClass` → 카드-facing 드로우 (사용 27)

**AS-IS** (`DCGO/Assets/Scripts/Script/CardController.cs`):
```
DrawClass(Player player, int drawCount, ICardEffect cardEffect)
IEnumerator Draw():
  if drawCount <= 0: 종료
  if player.LibraryCards.Count <= 0: 종료
  for i in drawCount: if 남으면 라이브러리 top 1장 → 손으로 (min(drawCount, available))
```
**가드**: drawCount>0, 라이브러리 비면 no-op, 가능한 만큼만(min).
**엔진 seam**: `MetadataActionProcessor.DrawCardsAsync` / `NormalizedDrawCards` + `ZoneMover`(Deck→Hand). 덱 고갈→패배 규칙은 **기존 엔진 처리 그대로**(추측 금지, 원본 Draw는 패배 트리거 안 함 — 별도).
**미러**: `DrawEffect`(원본 `DrawClass` 대응) — `IActivatedCardEffect`, `Apply`가 라이브러리 top N을 ZoneMover로 손으로 이동(엔진 draw 경로 재사용). `ActivatedEffectResolver`에 case 추가.
**테스트**: 라이브러리 5장 상태 → N=2 드로우 시 손 +2/덱 −2 / 라이브러리 1장에 N=3 → 1장만 / 빈 라이브러리 no-op.

## A2 — `SimplifiedSelectCardConditionClass` → 조건부 카드 선택 (사용 20)

**AS-IS** (`CardEffectCommons/RevealLibrary.cs`):
```
SimplifiedSelectCardConditionClass(Func<CardSource,bool> canTargetCondition, string message,
    SelectCardEffect.Mode mode, int maxCount, Func<CardSource,IEnumerator> selectCardCoroutine)
```
순수 descriptor(조건·메시지·mode·maxCount·선택후 코루틴). 실제 해소는 상위 select 흐름.
**엔진 seam**: `SelectPermanentEffect`(BuildRequest/Apply) + `ChoiceProvider`(동기/deferred). 기존 `ActivatedSelectEffect`와 동형이나 **존(Hand/Trash/Library 등) + 선택후 액션(mode)** 일반화.
**미러**: `SimplifiedSelectCardConditionClass` descriptor + 해소를 `ActivatedEffectResolver`에 case 추가(mode별 Apply). probe: 기존 `RevealAndSelect`/`SelectPermanentEffect`가 비-BattleArea 존을 다루는지 먼저 확인 — 가능하면 재사용, 부족분만 신설.
**테스트**: 조건 만족 후보만 제시 / maxCount 준수 / mode(예: trash/draw)별 결과 단언.

## A3 — `DestroyPermanentsClass` → 직접 삭제 (사용 6)

**AS-IS** (`CardController.cs`):
```
DestroyPermanentsClass(List<Permanent> destroyTargetPermanents, Hashtable hashtable, bool notShowCards=false)
IEnumerator Destroy():
  targets = targets.Filter(p => p.TopCard != null
            && (cardEffect==null || (!p.TopCard.CanNotBeAffected(cardEffect) && p.CanBeDestroyedBySkill(cardEffect))))
  if 0: 종료
  ... IsDPZeroDelete 플래그 반영
```
**가드(중요)**: 대상 필터에 **면역(`CanNotBeAffected`) + 스킬-파괴면역(`CanBeDestroyedBySkill`)** 적용. (이미 선택된 리스트를 직접 삭제하는 비-select 변형.)
**엔진 seam**: `MatchStateMutationSink.DeleteKind`(= `SelectPermanentEffect.Mode.Destroy`가 쓰는 경로). 면역은 `ContinuousImmunityGate`가 **sink에서 중앙 처리** → 미러 술어에 `CanNotBeAffected` 재구현 금지(EX8_074 교훈). `CanBeDestroyedBySkill`(스킬-파괴면역)은 헤드리스 대응 게이트 probe 후 위임(`CanNotBeDestroyedBySkillClass` 계열).
**미러**: `DestroyPermanentsEffect` — 주어진 id 리스트를 sink DeleteKind로 삭제. `ActivatedEffectResolver` case.
**테스트**: 대상 2체 직접 삭제(Trash 이동) / 스킬-파괴면역 보유 대상은 제외 / 빈 리스트 no-op.

## A4 — `HatchDigiEggClass` → 디지타마 부화 (사용 1)

**AS-IS** (`CardController.cs`):
```
HatchDigiEggClass(Player player, Hashtable hashtable)
IEnumerator Hatch():
  if !player.CanHatch: 종료
  top 디지타마 → PlayPermanentClass(root=Library, isBreedingArea, isHatching, ActivateETB) → 육성존
  부화 퍼머넌트 EnterFieldTurnCount = -1
```
**가드**: `CanHatch`(육성존 비어있음 등 기존 규칙).
**엔진 seam**: `MetadataActionProcessor.HatchDigitamaAsync` / `NormalizedHatchDigitama`(BreedingController.HatchDigitamaAsync). **이미 존재** → 카드-facing 효과가 그 경로 호출.
**미러**: `HatchDigiEggEffect` — `Apply`가 부화 경로 호출(CanHatch 가드). `ActivatedEffectResolver` case.
**테스트**: 육성존 빈 상태 → 부화 시 top 디지타마가 육성존에 등장 / CanHatch 불가 시 no-op.

## A5 — `PlayCardClass` → 카드 플레이 (사용 1, 범위 한정)

**AS-IS** (`CardController.cs`): 무거움(jogress/burst/appfusion/targetPermanent/isTapped 분기). **BT 사용은 단순 play 1건** → 그 경로만 미러.
**엔진 seam**: `PlayCardAction`(코스트/이동/등록/ETB). `payCost` 플래그·root(어느 존에서) 반영.
**미러**: `PlayCardEffect`(단순 play: cardSource, payCost, root) — `PlayCardAction` 위임. **jogress/burst/appfusion은 명시 NotSupported**(원본 대응 없을 때 신설 금지, BT 범위 밖). 주석으로 미구현 범위 명시.
**테스트**: 지정 카드를 해당 존에서 배틀존으로 play(payCost true/false 각각) 단언.

---

## 진행 요약
- [x] **A1** DrawEffect (27) — ✅ `DrawEffect`(원본 `DrawClass` 미러) + ResolveListAsync case + `TfxDraw`/G9-015 4/4. sink `DrawCardsKind` stage(re-run 안전). 247 green, RuleAudit 0.
- [x] **A2** SimplifiedSelectCardCondition (20) — ✅ `SimplifiedSelectCardConditionClass` + `SimplifiedRevealAndSelectEffect`(reveal top N→조건선택→Hand/분배, sink stage·ChoiceType.Card로 RevealSelect 가로채기 회피) + resolver case + `TfxRevealSelect`/G9-016 3/3. Custom mode는 per-card 후속. 248 green, RuleAudit 0.
- [x] **A3** DestroyPermanentsEffect (6) — ✅ 사전계산 리스트 DeleteKind stage(면역·삭제방지 sink 중앙처리로 subsume, 스킬파괴면역=엔진 미모델링 문서화) + resolver case + `TfxDestroy`/G9-017 2/2. 249 green, RuleAudit 0.
- [x] **A4** HatchDigiEggEffect (1) — ✅ CanHatch(육성존 빔+에그) 명시 미러 + `ZoneMover.HatchDigitamaAsync` 위임 + `TfxHatch`/G9-018 3/3.
- [x] **A5** PlayCardEffect (1, 범위 한정) — ✅ 코스트무료 play(sink `PlayCardKind`, PlayThisCardToBattle 일반화; jogress/burst/appfusion/payCost:true 미구현 명시) + `TfxPlayCard`/G9-019 2/2.
- ✅ **BT-PRE-A 완료** — 251 green, RuleAudit 0. → BT-PRE-B(키워드)로. 전 배치 종료 후 BT1–3 색상 포팅 진입.

## /goal·/bt-pre-a 연동
`/bt-pre-a`(전용 커맨드) 또는 `/goal BT-PRE-A`. 각 항목: 원본 1:1 확인 → probe(엔진 seam 재사용) → 미러 → green + 격리 테스트 + RuleAudit 0. AS-IS 불명확하면 중단·확인. 커밋은 사용자 지시 시(항목별 커밋 권장).
