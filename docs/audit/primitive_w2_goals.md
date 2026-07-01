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
- [ ] 키워드 7종 (Retaliation·Raid·Barrier·Rush·Collision·Fortitude·Evade)
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
