# PRIM-W2 — 고빈도 프리미티브 (선행개발 웨이브 2, 사용 20+)

> **위치:** `primitive_backlog.md`의 W2. 진화 기반(W1) 외 **사용 20회 이상**의 키워드·특수진화·링크·메모리·시큐리티·프레임워크. W1 완료 후 진행.
>
> **공통 종료 기준·규율:** W1과 동일 — green + 격리 테스트 + RuleAudit 0, AS-IS 미러, probe-first, 중앙화 게이트 술어 재구현 금지, 없는 메커니즘은 실 수요 범위만(나머지 NotSupported). 각 항목 독립 green 게이트.
> **재사용 힌트:** 키워드 self-static은 기존 팩토리 관용(`PierceSelfEffect`/`BlockerSelfStaticEffect`/`JammingSelfStaticEffect`/`RebootSelfStaticEffect`/`AllianceSelfEffect`/`VortexSelfEffect` 이미 있음)을 그대로 따른다. 메모리는 sink `AddMemory`/`SetMemory`, 바운스는 sink `ReturnToDeckBottom`, 시큐리티는 `SecurityResolver`/딜레이 옵션 경로.

## 서브goal (사용빈도순)
| # | 프리미티브 (사용) | 원본 위치 | 계열 |
|---|---|---|---|
| W2-1 | ActivateClassesForSharedEffects (88) | `CardEffectFactory` (probe) | 프레임워크(공유효과 묶음 발동) |
| W2-2 | SetMemoryTo3TamerEffect (83) | `CardEffectFactory` (probe) | 메모리(테이머 메모리 3으로) |
| W2-3 | BlastDigivolveEffect (75) | `KeyWordEffects/BlastDigivolution.cs` | 특수진화 |
| W2-4 | RetaliationSelfEffect (74) | `KeyWordEffects/Retaliation.cs` | 키워드 |
| W2-5 | LinkEffect (70) | `KeyWordEffects/Link.cs` | 링크 |
| W2-6 | RaidSelfEffect (64) | `KeyWordEffects/Raid.cs` | 키워드 |
| W2-7 | BarrierSelfEffect (61) | `KeyWordEffects/Barrier.cs` | 키워드 |
| W2-8 | PlaceSelfDelayOptionSecurityEffect (54) | `CardEffectFactory` (probe) | 시큐리티(딜레이 옵션 배치) |
| W2-9 | ArmorPurgeEffect (45) | `KeyWordEffects/ArmorPurge.cs` | 효과 |
| W2-10 | RushSelfStaticEffect (43) | `KeyWordEffects/Rush.cs` | 키워드 |
| W2-11 | SaveEffect (39) | `KeyWordEffects/Save.cs` | 효과 |
| W2-12 | DeckBottomBounceClass (36) | `Script/CardController.cs` | 바운스 |
| W2-13 | PlaySelfDigimonAfterBattleSecurityEffect (35) | `CardEffectFactory` (probe) | 시큐리티 |
| W2-14 | CollisionSelfStaticEffect (32) | `KeyWordEffects/Collision.cs` | 키워드 |
| W2-15 | SelectCardConditionClass (30) | `CardEffectCommons/RevealLibrary.cs` | 리치 select(비-Simplified) |
| W2-16 | FortitudeSelfEffect (27) | `KeyWordEffects/Fortitude.cs` | 키워드 |
| W2-17 | AddAppfuseMethodByName (26) | `CardEffectFactory/AddAppfusionMethod.cs` | 앱퓨즈 |
| W2-18 | BlockerStaticEffect (25) | `KeyWordEffects/Blocker.cs` | 키워드(대상, 비-self) |
| W2-19 | EvadeSelfEffect (20) | `KeyWordEffects/Evade.cs` | 키워드(would-be-deleted 대체) |
| W2-20 | CanNotAttackSelfStaticEffect (20) | `CardEffectFactory/CanNotAttack.cs` | 제약 |

> 권장 소순서: **키워드류(W2-4/6/7/10/14/16/19)** 먼저(기존 팩토리 관용 복제로 저비용) → 메모리(W2-2) → 시큐리티(W2-8/13) → 특수진화(W2-3)·링크(W2-5)·앱퓨즈(W2-17) → 프레임워크(W2-1)·리치select(W2-15) → 나머지.

## 진행 요약
- [x] **키워드 7종 — 완료** (G9-025 8/8, 257 green):
  - Rush·Retaliation·(ArmorPurge 등): Batch2 enum → `SelfKeywordBatch2Effect` 1줄.
  - Raid·Barrier·Collision·Fortitude·Evade: 신설 `SelfKeywordByNameEffect`(이름별 keyword 바인딩) + `ContinuousKeywordGate` 상수 5개 추가 → `HasKeyword` grant live.
  - **바(bar)**: Alliance/Overclock와 동일 = grant가 게이트에 live(HasKeyword). behavior 소비자(메타 플래그 hasRaid 등)를 HasKeyword로 마이그레이션하는 건 per-gate 후속(behavior, grant 프리미티브 아님 — 코드베이스 "preemptive seal" 관용).
  - 재사용: `SelfKeywordByNameEffect`는 이후 이름-기반 키워드 grant에 재사용.
- [x] **SetMemoryTo3Tamer** — 신설 `TriggeredSetMemoryEffect`(OnStartTurn 트리거, IsOwnerTurn+메모리≤2 가드 → sink SetMemory). G9-026 4/4. 258 green.
- [x] **ArmorPurge** (Batch2 enum) · **CanNotAttackSelf** (`ContinuousSelfRestrictionEffect` 재사용) · **DeckBottomBounce** (`DeckBottomBounceEffect` = ReturnToDeckBottom sink, Destroy 미러) — G9-027 3/3. 259 green.
- [x] **BlastDigivolve — SUBSUMED**: `SpecialPlayKind.Blast` 이미 존재(SpecialPlayAction + FreeDigivolveHelpers, 레시피 데이터 구동). 카드-facing 프리미티브 불필요(jogress 패턴).
- [x] **ActivateClassesForSharedEffects — SUBSUMED**: 원본은 `List<ICardEffect>` 병합 authoring 헬퍼. 헤드리스는 카드가 효과 리스트 직접 반환 → 런타임 프리미티브 아님.
- [x] **Save** — keyword grant(`SelfKeywordByNameEffect(Save)`, ContinuousKeywordGate.Save). G9-025 10/10.
- [x] **BlockerStatic**(비-self) — 신설 `ContinuousPlayerScopeKeywordEffect`(player-scope 키워드) + `HasKeyword` player-scope 확장(additive, BlockTiming이 읽는 seam). G9-028 3/3. 260 green. **재사용 기반**.
- [x] **SelectCardConditionClass** — 신설 디스크립터 + `ToSimplified()`로 reveal-select 메커니즘 연결. `TfxSelectCardCond`/G9-029.
- [x] **시큐리티 2**(PlaceSelfDelayOption·PlayAfterBattleSecurity) — `PlayThisCardToBattleEffect` 재사용(시큐리티→배틀). G9-031.
- [x] **AddAppfuse** — `SpecialPlayKind.AppFusion` 추가(DigiXros fusion 경로, 레시피 데이터-구동). G9-030.
- [x] **Link** — 신설 `LinkSelfEffect`(host 선택 + 링크코스트 지불 + `LinkHelpers.AddLinkCardAsync` attach) + resolver case. G9-031.

## ✅ PRIM-W2 전부 완료 (20/20)
263 green, RuleAudit 0. 신설 재사용 인프라: `SelfKeywordByNameEffect`·`TriggeredSetMemoryEffect`·`DeckBottomBounceEffect`·`ContinuousPlayerScopeKeywordEffect`(+HasKeyword player-scope)·`SelectCardConditionClass`·`LinkSelfEffect`·`SpecialPlayKind.AppFusion`. subsumed: BlastDigivolve·ActivateClassesForShared. 테스트 G9-025~031.

## W2 누적 (15/20 해소: 13 built + 2 subsumed)
키워드 7·SetMemory·ArmorPurge·CanNotAttackSelf·DeckBottomBounce·Save·BlockerStatic + Blast/ActivateClassesForShared(subsumed). 신설 재사용 인프라: `SelfKeywordByNameEffect`·`TriggeredSetMemoryEffect`·`DeckBottomBounceEffect`·`ContinuousPlayerScopeKeywordEffect`+HasKeyword player-scope. 테스트 G9-025~028.

## W2 누적 완료 (11/20)
키워드 7 · SetMemoryTo3Tamer · ArmorPurge · CanNotAttackSelf · DeckBottomBounce. 신설 재사용 인프라: `SelfKeywordByNameEffect`·`TriggeredSetMemoryEffect`·`DeckBottomBounceEffect`. 테스트 G9-025~027.
- [ ] 메모리(SetMemoryTo3Tamer) · 시큐리티(PlaceSelfDelayOption·PlayAfterBattle)
- [ ] 특수진화(Blast) · 링크(Link) · 앱퓨즈(AddAppfuse) · 바운스(DeckBottomBounce)
- [ ] 프레임워크(ActivateClassesForShared) · 리치select(SelectCardCondition) · ArmorPurge · Save · BlockerStatic · CanNotAttackSelf
- 완료 → PRIM-W3.

---

## 실행 대화문 (복붙용)
```
PRIM-W2 진행. docs/audit/primitive_w2_goals.md 스펙대로, 아래 소순서로 순차 실행:
키워드 7종(Retaliation·Raid·Barrier·Rush·Collision·Fortitude·Evade) → SetMemoryTo3Tamer → 시큐리티(PlaceSelfDelayOptionSecurity·PlaySelfDigimonAfterBattleSecurity) → BlastDigivolve → Link → AddAppfuseMethodByName → DeckBottomBounce → ActivateClassesForSharedEffects → SelectCardConditionClass → ArmorPurge → Save → BlockerStaticEffect → CanNotAttackSelfStaticEffect.

각 항목: 구현 전 원본(위 표 위치)에서 1:1 확인(추측 금지) → probe(키워드는 기존 self-static 팩토리 관용 복제, 메모리=sink AddMemory/SetMemory, 바운스=sink ReturnToDeckBottom, 시큐리티=SecurityResolver 경로 재사용) → 원본 이름·시그니처 미러 → bash scripts/run-tests.sh 전체 green + 격리/픽스처 테스트 + tools/RuleAudit 0.
이전 항목 green 후 다음. 중앙화 게이트(면역 등) 술어 재구현 금지. 없는 메커니즘은 실 수요 범위만·나머지 NotSupported 명시. AS-IS 불명확하면 중단·확인. 커밋은 내가 지시할 때.
```
