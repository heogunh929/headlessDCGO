# 엔진 결함 원장 — 2026-07-24 (HEAD e5ea69d7)

**지위**: 사용자 판정(2026-07-24) — 카드층 전량 부적합·기존 포팅 무효·**엔진 수리 선행 후 재포팅**.
이전 "소프트 동결"·"미러 100%"·categorized "무결" 선언 전부 무효.
**대상=엔진 전체**(미러 Script BOTH 344 + substrate Headless 208, 렌즈-없는 감사 완료). **카드층은 감사 대상 아님 — 사용자 판정으로 전량 무효·재포팅 대기(개별 검증 불요, 폐기)**. 하한선인 이유는 카드가 아니라 R1(RD-J-01 상환분)·R2(C5 빈-union) 엔진-내부 재검증 미완.
상환 기준: **AS-IS 행동**(다이제스트는 결함난 엔진의 것이라 기준 아님). 각 항목 상환 시 AS-IS 원문 대조·행동 witness.

출처: `LENS_FREE_FINDINGS_2026-07-24.md`, `match_check_00~12.md`.

---

## A. 행동 결함·로직 불일치 (15)

### A-live: 현재 오작동 (잠복 아님) — 최우선

| ID | 파일 | 결함 | AS-IS 앵커 | 상환 |
|---|---|---|---|---|
| DEF-A1 | `Script/CardSource.cs:1638` | `HasCardColor(string)`가 `CardColors`만 검사. AS-IS는 `AllCardColors=CardColors∪DualCardColors`(:1577). 듀얼컬러 카드 색판정 탈락 | CardSource.cs:1577 | union 복원. ~590 호출부 영향 |
| DEF-A2 | `Script/CardEffectFactory/PermanentEffectFactory.cs` CollisionEffect | 면역게이트 `!TopCard.CanNotBeAffected(activateClass)` 삭제. "전 self-grant" 전제 거짓 | AS-IS CollisionEffect | 가드 복원. 소비 EX8_070·BT21_077·EX11_063·EX10_032·EX10_008 |
| DEF-A8 | `Script/CardController.cs:491` IRecovery | `SecurityRuleGateSeam.CanAddSecurity`(=>true 스텁) 호출. 충실 미러 `Player.CanAddSecurity`(Player.cs:477)가 실 스캔 | Player.cs:477 | 스텁→실 게이트 재배선 |

### A-발산: 조건부 발현

| ID | 파일 | 결함 | 상환 |
|---|---|---|---|
| DEF-A3 | `Script/CardEffectCommons/KeyWordEffects/Decode.cs` | `PlayPermanentCards(sourceCard:)` 저수준 호출로 CanEnterField(ICanNotPutFieldEffect) 미검사. Partition/Blast는 올바른 오버로드 | activateClass 오버로드로 전환 |
| DEF-A4 | `Script/SelectDigiXrosClass.cs` | AS-IS `EndSelectDigiXros()`가 StartCoroutine 없이 불려 미실행(버그), TO-BE 동기 Task가 실행. 4 호출부 | AS-IS no-op 의미 재현 여부 판정(버그 보존 vs 수정) |
| DEF-A5 | `Script/CardEffectCommons/KeyWordEffects/MindLink.cs` | `Count(IsTamer&&!IsFlipped)`→`Count(IsTamer)` 협착 소실. IsFlipped는 live 플래그(CardSource.cs:1180) | `!IsFlipped` 복원 |
| DEF-A6 | `Script/CardEffectCommons/TrashDigivolutionCards.cs` | 선택집합 협착: 소스없는 host 배제·host pick 후 0-trash 금지(vs AS-IS canNoSelect) | AS-IS 선택집합 복원 |
| DEF-A7 | `Script/CardController.cs:2090` IUnsuspendPermanents | 컷인 후 재필터를 1차생존자(untappedPermanets:5723) 대신 `_permanents`(프리필터 전체)로 | untappedPermanets로 |
| DEF-A9 | `Script/CardController.cs` 바운스3종(DeckBottom/DeckTop/HandBounce) | pre-move "would-return/would-remove-field" 컷인 창(취소) 누락. Destroy엔 있음. BT5_086 prevent-removal 무력화 | pre-move 창 추가. HandBounce IsDigiEgg→덱밑·DiscardEvoRoots도 미발견 |
| DEF-A10 | `Script/CardController.cs` IPutSecurityPermanent | 면역·PRE WhenRemoveField 창·DiscardEvoRoots·DigiEgg→라이브러리밑 분기·토큰게이트 누락(happy-path만) | 누락 분기 복원 |
| DEF-A11 | `Script/CardController.cs` ISecurityCheck→SecurityResolver | 해결순서 스왑([Security] activated vs OnSecurityCheck/OnLoseSecurity)·다중[Security] select 루프 미재현·Execution 스테이징 발산 | 순서·루프·스테이징 AS-IS 정렬 |
| DEF-A12 | `Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/ChangePlayCost.cs` | 면역검사 `CanNotBeAffected(changeCostClass)`(신규 익명) vs AS-IS `(activateClass)`(원 효과). SkillCondition이 타입 검사 | activateClass 스레딩(DP 형제 방식) |
| DEF-A13 | `Script/Permanent.cs` Level | AS-IS `if(!TopCard.HasLevel)Level=1145140` 강제폐기 미이식 → folded 값 유지 가능 | 강제폐기 재현(TopCard.HasLevel 재검증) |
| DEF-A14 | `Script/Permanent.cs` cardSources | 링크카드 항상 맨뒤. AS-IS는 index1 interleaved 삽입 | 삽입 순서 AS-IS 정렬 |
| DEF-A15 | `Script/GameRandom.cs`→`Headless/Services/GameRandomSource.cs` | NextUInt32(상위32) vs NextUInt64(전체64)·Range 폭·Probability 소실·seed long→int → 결정론 계약 파손 | AS-IS PRNG 계약 복원(재현성) |
| DEF-A16 | `Script/PermanentEffectFactory.cs` CanNotSwitchAttackTargetEffect | `_ = activateClass;`로 AS-IS 라이브 면역가드(:120) 침묵 삭제. 유일 호출부 AD1_011 self-only라 현재 무증상이나 구조적 오삭제(CollisionEffect 동일 패턴) | 가드 복원 |

## B. 미포팅 로직 갭 (AS-IS 실 로직·TO-BE 스텁/부재)

| ID | 대상 | 내용 |
|---|---|---|
| DEF-B1 | `Script/DeckData.cs` (CoreRule/HIGH) | base-256 덱코드 코덱·검증·GetDeckCode — 8줄 스텁 |
| DEF-B2 | `Script/DeckBuildingRule.cs` (CoreRule/HIGH) | 덱 합법성(매수제한·밴리스트·밴페어) — 스텁 |
| DEF-B3 | `Script/DeckCodeUtility.cs` (CoreRule/HIGH) | TTS/DeckBuilder 덱코드 파서 — 스텁 |
| DEF-B4 | `Script/CreateNewDeckButton.cs` | 덱코드 import 오케스트레이션 — 스텁 |
| DEF-B5 | `Script/ShuffleDeckCode.cs` | 덱코드 암호(ConvFactor) — 스텁 |
| DEF-B6 | `Script/Combinations.cs` | 색/이름 조합 열거(GetCombinations·GetDifferenetColorCardCount 등). 소비 10+장 |
| DEF-B7 | `Script/ConvertBinaryNumber.cs` | 260-glyph 진법변환 — 스텁 |
| DEF-B8 | `Script/SpellRestoration.cs` | Base64/JsonUtility 스펠코드 — 스텁 |
| DEF-B9 | `Script/JsonSerializedClass.cs` | AS-IS CardData(35필드)를 다른 스키마 CardJsonDto로 재발명(1:1 아님) |
| DEF-B10 | `Script/AutomaticOrder/StartTurnTamerMemory.cs` | GetSkillIndexAutomaticOrder(Set Memory to 호이스팅) — 스텁 |
| DEF-B11 | `Script/CardObjectController.cs` | AddLibraryTopCards/AddLibraryBottomCards(호출 50)·MovePermanent(10) 정본구현 부재 |
| DEF-B12 | `Script/ContinuousController.cs` | RandomUtility.IsSucceedProbability 미러 부재 |
| DEF-B13 | `Script/Permanent.cs` | HandBounceEffect·LibraryBounceEffect·DPWhenSuspended(write-only)·DigivolutionOrLinkCards 부재 |
| DEF-B14 | `Script/CardSource.cs` | HasInheritedEffect·HasUseCost·~50 trait 술어·~10 keyword flag 부재(카드 인라인 재구현 중) |
| DEF-B15 | ChangeSAttack | Invert 3종(GiveEffectToPermanent InverteDigimonSAttack·InvertDigimonSAttack / GiveEffectToPlayer InvertDigimonSAttackPlayerEffect) 미포팅 |
| DEF-B16 | `Script/CardEffectCommons/GameContextDeterminarion.cs` | OwnerHas1OrLessTamers 부재(라이브 소비 4장) |
| DEF-B17 | `Script/CardEffectCommons.cs` | OptionSecurityEffect 브릿지 부재(BT18_098·BT15_092 인라인) |
| DEF-B18 | `Script/CardEffectCommons/KeyWordEffects/Progress.cs` | GainProgress grant 부재(형제 ProgressProcess는 포팅) |

## C. 발명 (AS-IS 무대응)

| ID | 대상 | 내용 |
|---|---|---|
| DEF-C1 | `Script/ICardEffect.cs` EffectTiming | `WhenDigivolving` 발명 멤버(AS-IS enum 60값에 없음). 코퍼스 방언 |
| DEF-C2 | `Script/CardController.cs` PlayPermanentClass | isEvolution 시 WhenDigivolving 2번째 창+STOP 가드(DISPATCH-REMAP BRIDGE) |
| DEF-C3 | `Script/CardEffectFactory.cs` | SpecialPlayRecipeRegistry(BurstDigivolve 등 등록) — 레지스트리 청산 규약 대조 요망 |
| DEF-C4 | `Script/SelectCardEffect.cs:52-200`·`SelectPermanentEffect.cs:117-160` | 사문 레거시 API 블록(id-flip 잔존, 호출 0) |

## R. 재검증 필요 (과거 상환의 과잉적용 의심)

| ID | 내용 |
|---|---|
| DEF-R1 | **RD-J-01 재검증 완료(recheck_R1_immunity.md)**: 37 판정단위 전수 → 오삭제 **2건만**(DEF-A2 CollisionEffect + DEF-A16 CanNotSwitchAttackTarget). 정당 35건(대부분 상환은 옳았음). 계통적 과잉적용 아님 — 종결 |
| DEF-R2 | **C5-1 빈-union 재검증 완료(recheck_R2_emptyunion.md)**: 빈-스텁 소비 4곳 전수 → 죽은 소비경로 **1건만**(DEF-S4 DigivolveAction, 기등재·latent). 나머지 union 존재/완전이관=정상. 신규 live 결함 0 — C5 은퇴는 대부분 옳았음. + 고아 죽은코드 5건(DEF-S21). 종결 |

---

## 확정 순서 (사용자)
1. 엔진 결함 전량 확정 = 본 원장 + **Headless substrate 208 감사**(진행 중) 합집합
2. 엔진 수리 (AS-IS 행동 기준·항목별 witness)
3. 엔진 결함 0 재검증
4. 카드 **전량 재포팅** (기존 IMPL 439 전부 무효·폐기 — 개별 감사 없이 깨끗한 엔진 위에 재작성)

---

# S. Headless substrate 결함 (208파일 렌즈-없는 감사 8파트)

**전체 판정**: 대부분 정당 substrate(게임규칙을 미러층에 위임). live 행동결함은 미러층보다 적으나 실재. `=>true` 게이트 스텁은 배선된 것 거의 없음(대부분 dormant).

## S-live: substrate가 낸 실 행동결함

| ID | 파일 | 결함 | 연관 |
|---|---|---|---|
| DEF-S1 | `Headless/Runtime/SecurityResolver.cs` | **해결순서 오류(모든 시큐리티 체크 디폴트 경로)**: AS-IS는 공개 카드 `[Security]` 활성스킬을 OnSecurityCheck/OnLoseSecurity 반응자 **이전** 해결, TO-BE는 역순 무조건 | DEF-A11 확증(edge 아닌 전경로) |
| DEF-S2 | `Headless/Runtime/SecurityResolver.cs` | checkCount 캡: 루프 진입 시 1회 `Math.Min(strike,available)` vs AS-IS 매 반복 재평가 → 중간 시큐리티 추가 시 AS-IS가 더 검사 | — |
| DEF-S3 | `Headless/State/VisibilityView.cs` | **소유자 본인 Security/Library 식별자 무조건 공개** — 실규칙상 본인도 확인 전 미지. AS-IS 근거 없는 게임규칙 임의결정. G2B-002 문서가 "미해결 리스크" 자인 | **RL info-set 정보비대칭** |
| DEF-S4 | `Headless/Runtime/DigivolveAction.cs` | "진화요구조건 전체 무시" 소비 배선이 영구 빈-스텁(ContinuousScopeEvaluation)에만 연결, union 파트너 없어 3소비 구조적 영구무효. 프로듀서 살아나도 반영 안 됨 | DEF-R2(C5 빈-union 잔재) |
| DEF-S5 | `Headless/Runtime/DeDigivolveHelpers.cs` | 루키-플로어 정지가 라이브 `Permanent.Level`(folded) 대신 정적 printed-level 읽음 + AS-IS `==3` vs `<=3`. 레벨변경 연속효과 시 발산 | DEF-A13 동뿌리 |
| DEF-S6 | `Headless/Runtime/EffectDrivenAttack.cs` | `RequestChoice` 항상 `canSkip:true`. AS-IS 강제-공격 모드(`_canNoSelect`) 대응 플래그 없어 **강제공격 표현 불가** | — |
| DEF-S7 | `Headless/Runtime/ContinuousFieldMembership.cs` | AS-IS 4번째 arm(`IsLinkedEffect && cardSource.IsLinked`) 전면 부재, 2소비자 커버리지 갭 | — |
| DEF-S8 | `Headless/Services/DeckValidator.cs` | main 0-60/digitama 0-10 허용 vs AS-IS **정확히 50/≤5**·BannedPair 미지원(StarterDecks 우연 통과) | DEF-B2 |
| DEF-S9 | `Headless/Runtime/OptionColorRequirement.cs` | 옵션 색요건이 substrate·미러(CardSource.MatchColorRequirement:313) **이중 라이브 구현** — 표류 가능. 미러 단일화 필요 | — |
| DEF-S10 | `Headless/Runtime/LinkHelpers.cs:164` | LinkedMax>1 오버플로 트림을 AS-IS는 소유자 SELECTION인데 substrate는 자동 oldest-first. 추적됨 MIG2-ADDLINK-SELECT, max>1 witness 없음 | — |

## S-latent/dormant: 현재 무증상(카드 재포팅·재배선 시 함정)

| ID | 파일 | 상태 |
|---|---|---|
| DEF-S11 | `Headless/State/PlayerRuleAdapter.cs` | CanAddSecurity/CanDraw/CanPayMemoryCost 등이 AS-IS 연속제한 스캔 생략(간소화). 프로덕션 호출 0(테스트만) | DEF-A8 계열 |
| DEF-S12 | `Headless/Effects/ActivatedHashtableBridge.cs` | SecuritySkill hashtable `isFaceDown:true` 하드코딩(SecurityFaceState 미참조). 현재 소비 카드 0 |
| DEF-S13 | `Headless/Runtime/PlayCardAction.cs:62` | `CreateAssemblyActionIfPlayable`가 `reduceCost<=0`시 null(AS-IS는 HasAssembly&&!isEvolution만). reduceCost==0 어셈블리 카드 존재 미확인(저확신) |

## S-dead: 죽은 재구현/스텁 (정리 후보 — [Obsolete]+가드 또는 삭제)

| ID | 파일 | 상태 |
|---|---|---|
| DEF-S14 | `Headless/Runtime/RevealAndSelect.cs` (602줄) | RevealLibrary 전체 재구현, 호출자 0(빅뱅 컷오버 시 선언효과 삭제), 미러에 충실 브릿지 별존. [Obsolete] 없음 |
| DEF-S15 | `Headless/Runtime/DigivolutionSourceStackPort.cs` | 라이브와 병렬 disconnected 모델, 프로덕션 호출 0, AS-IS 인용 0의 게임규칙 |
| DEF-S16 | `Headless/Effects/PlayCostHelpers.cs` | 발명 코스트-모디파이어 엔진이 미기록 메타키 읽어 pass-through 퇴화. 라이브 호출되나 inert. cost-fold 은퇴 잔재 |
| DEF-S17 | `Headless/Services/InMemoryRuleQueryService.cs` | `CanPayCost=>cost>=0` 무조건-통과 스텁, 호출 0 |
| DEF-S18 | `Headless/Effects/MandatoryEffectOrdering.cs` | 프로덕션 dead(창 컷오버로 대체), 테스트-핀 |
| DEF-S19 | `Headless/Effects/DpZeroDeletionHelpers.cs:22` | `SweepAsync` 호출 0, 재배선 시 CanBeDestroyed 게이트 없어 보호 0-DP 선택 위험 |
| DEF-S20 | `Headless/Runtime/BattleResolver.cs` ResolveKnockOutWindow | 발명 latent no-op(OnKnockOut AS-IS 대응 없음), 반응자 0 |
| DEF-S21 | C5 잔재 고아 5종 | `ContinuousFieldMembership.GranterMembershipHolds`·`MatchStateMutationSink.HasSelfFlag`·`CardSource.EffectConditionPasses`·`EffectQueryContext(.Matches)`·`ContinuousScopeEvaluation.DynamicValue/Metric/InheritedEffectKey` — 라이터/호출 0, 오도 주석. 정리(삭제 or [Obsolete]) |

---

# 최종 결산 (엔진 결함 하한선)

- **미러층(Assets/Scripts)**: 행동결함/로직불일치 15(A) + 미포팅갭 18(B) + 발명 4(C) + 재검증 2(R)
- **substrate(Headless)**: live 행동결함 10(S1~S10) + 잠복 3(S11~S13) + 죽은코드 7(S14~S20)
- **하한선인 이유**: RD-J-01 면역가드 상환분(R1)·C5 빈-union(R2·S4로 실증) 엔진-내부 재검증 미완. **카드층은 하한선 요소 아님 — 전량 무효·엔진 수리 후 재포팅.**
- **수리 우선순위**: live 행동결함(A-live 3·S1·S3·S4·S5·S6 등) → 미포팅 코어갭(덱코덱·B계열) → 발명/죽은코드 정리 → RD-J-01/C5 재검증.
