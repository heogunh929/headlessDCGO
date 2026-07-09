# 프리미티브 진행 예정 대상 (워크리스트)

- 작성: 2026-07-09. 전체 대조·상환 현황은 [asis_tobe_primitive_mapping.md](asis_tobe_primitive_mapping.md).
- 원칙: **AS-IS 1:1 미러**. 결과-등가라도 구조 이탈 불허. AS-IS가 전 필드 효과를 순회 스캔하며 joint predicate를 평가하면 **진짜 순회-스캔 구조**로 미러(per-card scope로 술어 분리 금지). 단, 코어 restriction 인프라(`IsRestrictedFromCause`/`ContinuousRestrictionGate`)를 쓰는 항목은 그 시스템의 관례를 따른다.

## ✅ 완료 (2026-07-08~09, 참고)

- **(a) 틀린 결과 15건** 전량 · **(b) dead/inert**(실 dead 2 상환 + 오분류 5 검증 + 딥 잔여 1) · **(c) 트리거 미발화 1건** · **(d) MISSING 8건**(CanNotSelectBySkill·CanNotBeRemoved·CanNotMove·DontHaveDP·DontBattleSecurityDigimon·CannotIgnoreDigivolutionCondition·ChangeEndTurnMinMemory·ChangeBaseCardName). 회귀 370/370, RuleAudit 0. 테스트 FAILa-01~13·FAILa-PPS·FAILb-01/02·FAILc-01·FAILd-01~08.
- restriction-scan 4종(Select·Ignore·Remove·Move)은 **진짜 순회-스캔 구조**로 통일.

---

## 진행 예정 대상

### A. P0 — 실동작 버그 (지금 잘못된 출력, 최우선)

| 항목 | AS-IS | 갭 | 상환 방향 |
|---|---|---|---|
| **RevealLibraryClass** | `IsBeingRevealed` | reveal 미모델로 **WhenDiscardLibrary 과발화**. 풀정보 모델이라 reveal 자체는 무의미하나 discard-window 제외 가드 필요 | reveal 중 discard 트리거 억제 플래그 |
| **TrainingClass** | suspend-cost | **OnTappedAnyone 미발화** + isFacedown 미전달 | sink suspend 경로에 OnTapped 이벤트 + facedown 배선 |
| **SuspendPermanentsClass** | | **DPWhenSuspended 스냅샷 누락** + CanNotBeAffected 드롭 + already-suspended 미필터→OnTapped 재발화 | suspend 경로에 DP 스냅샷 + 가드 + 중복필터 |
| **DestroyPermanentsClass 외 가드 드롭** | CanBeDestroyedBySkill·CanAddSecurity·CanAddMemory | 가드 드롭. ⚠️**재대조 필요**: 엔진 clamp(MemoryController.Clamp 등)로 등가 강제될 수 있음 | 재대조 후 진짜 갭만 상환 |

### B. (d) MISSING — 미포팅 (실카드 수요, 신규 프리미티브)

| 항목 | AS-IS | 난이도 | 상환 패턴 |
|---|---|---|---|
| **CanNotPlayClass** | `CanNotPlay(cardSource)` | 딥 | 플레이 대상이 **필드 밖**(hand) → 플레이 검증(`CanPlayAsNewPermanent`/play action)서 전역 스캔 + 카드 술어 |
| **CanNotPutFieldClass** | `CanNotPutField(cardSource, cardEffect)` | 딥 | 위와 동일(효과-구동 필드 배치 chokepoint) |
| **CanSuspendByDigisorptionClass** | | 딥 | G11 Digisorption 연계 |
| **ChangeCardLevelForAssemblyClass** | 레벨 override(Assembly용) | 중 | `CardSource.CardLevel`에 base-fold 단계 추가(ChangeBaseCardName 패턴 미러) |
| **ChangeCardNamesForDigiXrosClass** | DigiXros용 이름 추가 | 중 | ChangeCardNames(ADD) 패턴 + DigiXros 매칭 경로 배선 |
| **CanSelectAssemblyClass** | Assembly 선택 허용 | 중 | Assembly(합체) 선택 경로 |
| **CanSelectDigiXrosClass** | DigiXros 선택 허용 | 중 | DigiXros 선택 경로 |
| **CanAttackTargetDefendingPermanentClass** | 공격 대상 지정 | 중 | 공격 타겟 선택 게이트 |
| **DeckTopBounceClass** | | 저 | 직접-리스트 미러 없음(select 경로만) — 리스트 버전 배선 |

### C. 구조/술어 평면화 FAIL — 통합 프리미티브 필요

| 항목 | 갭 | 상환 방향 |
|---|---|---|
| **AddSkillClass** | 범용 getEffects 스플라이스 + limitedTiming 게이트 미포팅. **라이브 STOP(BT1_104)** | AddSkill 중앙 프리미티브(가장 큰 조합성 갭) |
| **ChangeSAttack/ChangeCost/ChangeDP/ChangeLinkMaxClass** | Func-transform + IsUpDown/IsMinusDP 드롭 | 통합 Func-transform 프리미티브(list-transform fold 패턴 미러) |
| **DisableEffectClass** | per-effect 술어 → whole-card boolean 축소 | 효과-단위 disable 술어 복원 |
| **CanNotDigivolveClass** | into-card 술어 드롭 | 순회-스캔 + into-card 술어 |
| **CollisionClass** | predicate-form 미포팅(self-keyword만) | predicate-form 오버로드 |
| **SelectCardCondition/SelectDigiXrosClass** | 고급 술어 저장만·미평가 / substitution 드롭 | 술어 실평가 + 치환 분기 |
| **AddDigivolutionRequirement/AddDetailClass** | 범용 게이트 / permanentCondition+triggerEffect 드롭 | 범용 배선 |

### D. 딥 — 아키텍처 확장

| 항목 | 갭 | 필요 인프라 |
|---|---|---|
| **CanNotTrashFromDigivolutionCardsClass** | metadata-stamp로 축소, 술어 미평가 | **전역 source-protection 스캔** — 트래시 경로(repository-only)에 registry 관통 |

### E. 저우선 — 결과-동일 (지금 틀리지 않음, 구조 복원)

- **결과동일 FAIL (~23)**: 리네임·폴딩·delta 재구현. 현재 도달가능 입력서 결과 동일 → 급하지 않음.
- **PARTIAL 구조 평면화 (~30)**: 타이밍-클래스 계열(TurnTiming/When*/StartOf*/EndOf* 2단계 게이트 중앙화), ActivateClassesForSharedEffects(공유 hashValue once-per-turn), Play*/Trash*ProcessAccordingToResult success 콜백에 카드 리스트 복원 등.

---

## 재사용 가능 프리미티브 패턴 (확립됨)

1. **연속 제약 + 소비 chokepoint** — 필드-내 대상, causingEffectPredicate·condition 지원.
2. **상태 override** — DontHaveDP → `ResolveDp` -1 sentinel(모디파이어 override).
3. **intrinsic-마커 dispatch** — DontBattleSecurityDigimon → 카드 자신 효과를 dispatch해 마커 검사(연속 레지스트리 아님).
4. **list-transform fold** — ChangeBaseCardName/Color → `FoldListTransforms(printed, key)`(REPLACE) + ADD 2단계.
5. **진짜 순회-스캔 + joint predicate** — CanNotSelectBySkill/Ignore/Remove/Move → 마커가 AS-IS 술어를 원 시그니처 Func로 보유, 소비부가 `GetContinuousEffects(Scope)` 전역 순회 평가. ⚠️ 마커 발견 시에만 CardSource lazy 생성 + owner 가드.
6. **글로벌 lock via 임계값/그랜트 무효화** — CannotIgnore(그랜트 부정) · ChangeEndTurnMinMemory(턴패스 임계값).
