# S2 · S3 · S4 — 선결 서브시스템 상세 설계

- 작성일: 2026-06-28
- 짝 문서: [s1_effect_driven_attack_design.md](s1_effect_driven_attack_design.md), [cgroup4_subsystem_analysis.md](cgroup4_subsystem_analysis.md)
- 상태: 설계(미구현). 승인 후 구현.

---

# S2 — 효과-무효 (CanNotAffected) 프레임워크 🔴 광범위

## 목적
특정 Digimon이 (상대) 효과의 영향을 받지 않게 한다. 해금: **C-15 Progress**. 인접: **D-7 무효화**, 그리고 수많은 카드 효과의 게이팅(원본 `CardSource.CanNotBeAffected`는 ~20개 파일에서 효과 적용 직전 검사).

## 원본 모델
- `CardSource.CanNotBeAffected(ICardEffect)` (CardSource.cs:1060) — 대상이 그 효과의 영향을 받지 않는지.
- `Permanent.cs` 전반: 효과가 대상에 작용하기 직전 `if (!TopCard.CanNotBeAffected(cardEffect)) { ...apply... }`.
- 무효는 **효과 출처 기준 상대성**을 가짐(예: Progress = "상대 디지몬의 효과"만 무효; SkillCondition=`IsOpponentEffect`).

## 헤드리스 게이트 지점
효과가 대상에 작용하는 단일 통로 = **`MatchStateMutationSink.Apply`**(뮤테이션이 targetId에 적용되는 지점) + 연속 게이트(`ContinuousDpGate`/`ContinuousModifierGate`/`ContinuousRestrictionGate`).
- 뮤테이션은 `SourceEntityId`(효과 출처 카드)와 `targetId`를 가짐 → 출처 소유자 vs 대상 소유자 판정 가능.

## 설계
### S2-1. 연속 immunity 게이트 (신규 `Headless/Runtime/ContinuousImmunityGate.cs`)
```csharp
public static bool IsImmune(EngineContext context, HeadlessEntityId targetId, HeadlessEntityId effectSourceId)
```
- 대상 스코프 연속효과 조회(`ContinuousScopeEvaluation.EvaluateForCard(context, ImmunityScope, targetId)`).
- 각 immunity 효과의 values로 **무효 범위** 판정:
  - `immunityFromOpponentOnly`(기본 true) → 출처 소유자가 대상 소유자의 적일 때만 무효.
  - (확장) `immunityFromAll` → 무조건.
- 출처 소유자 = `context.CardInstanceRepository`에서 effectSourceId 조회.

### S2-2. 뮤테이션 sink 게이팅
`MatchStateMutationSink.Apply`에서 targetId·mutation.SourceEntityId 확보 후:
```csharp
if (ContinuousImmunityGate.IsImmune(context, targetId, mutation.SourceEntityId)) { _skipped.Add(mutation); return; }
```
- **단, "자기 효과/유익 효과"는 무효 대상 아님** → 출처-상대성으로 자연 처리(자기 카드 효과는 immunityFromOpponentOnly에 안 걸림).
- 적용 범위: DP/SecAtk/Cost 모디파이어, Suspend, Delete, Bounce 등 대상-작용 kind. (Draw/Memory 등 비-대상 kind 제외)
- **주의**: sink는 EngineContext 미보유(현재 `_repository`/`_effectRegistry`만). → ① sink 생성 시 출처-소유자 조회용 repository 보유(있음), ② immunity 게이트를 `_effectRegistry`+`_repository`만으로 동작하도록 설계(EngineContext 불필요하게).

### S2-3. Progress 매핑
- Progress = "공격 중, 상대 효과 무효(UntilEndAttack)". → 공격 개시 시 대상에 `immunityFromOpponentOnly`+`EffectDuration.UntilEndAttack` 연속효과 등록. 소비는 S2-1/2 게이트.

## 리스크 & 범위
- **광범위**: 모든 대상-작용 뮤테이션 경로에 게이트 추가 → 회귀 위험 높음. 전체 스위트 필수.
- **권장**: 가장 늦게 착수. D-7 무효화와 공동 설계(무효화=효과 자체를 끄기 vs 무효=대상이 안 받기 — 구분).
- 테스트: 상대 효과 -DP가 무효 대상에 무적용 / 자기 효과는 적용 / duration 만료 후 재적용.

---

# S3 — Trait 시스템 🟡 데이터+헬퍼 (경량)

## 목적
카드의 trait(종족/형태/속성) 보유·매칭. 해금: **C-16 Overclock**(trait 일치 아군). 인접: 다수 카드 조건.

## 현황 (대부분 보유)
- `CardRequirementHelpers.HasTrait` + 키 `trait`/`traits`/`cardTraits` **이미 존재**(메타데이터에서 읽음).
- 갭: **카드 데이터 로딩이 traits를 메타데이터에 적재하는지 미확인**(`Headless/DataLoading`에 traits 매핑 없음).

## 설계
### S3-1. 데이터 적재 점검/배선
- 카드 원천 데이터(JSON, `Assets/CardBaseEntity`)에 trait 필드 확인.
- `DataLoading` 로더가 trait → `CardRecord.Metadata["traits"]`(string[])로 적재하도록 배선(누락 시).
### S3-2. 매칭 헬퍼 재사용
- `CardRequirementHelpers.HasTrait(card, trait)` 그대로 사용. 신규 코드 최소.
### S3-3. Overclock 매핑
- `CanSelectPermanentCondition`: 아군 디지몬 && (토큰 || `HasTrait(top, trait)`) && != 자신. → 기존 헬퍼 조합.

## 범위
- 경량(데이터 배선 + 기존 헬퍼). 단 trait 데이터가 원천에 없으면 적재 작업 추가.
- 테스트: 카드에 trait 적재 확인 / HasTrait 매칭 / Overclock 후보 필터.

---

# S4 — 소재 스택 분할·승격 🔴 (D-4 De-Digivolve와 공유)

## 목적
디지볼루션 소재 묶음을 **독립 permanent로 승격/분할**. 해금: **C-14 Partition**(2분할). 공유: **D-4 De-Digivolve**, B-1/B-10(소재 처리 🔴).

## 헤드리스 모델 (현황)
- 소재 인스턴스 = `ChoiceZone.None` 보존, top 카드 `sourceIds` 메타데이터(top→bottom 순)로 추적.
- `DigivolutionStackReader.Read` → `DigivolutionStack`(Depth, `StackedCard`+roles).
- 기존 승격 사례: **ArmorPurge**(top 1장 trash + 직속 소재 1장 승격) — `DeletionReplacementGate.TryArmorPurgeAsync`.
- 미구현: **여러 장을 새 permanent로 동시 승격 / 스택을 둘로 분할**.

## 설계
### S4-1. 세그먼트 승격 프리미티브 (`DigivolutionStackHelpers` 확장)
```csharp
// 주어진 소재 id들(상위→하위 순)을 하나의 새 permanent로 materialize.
// 세그먼트의 첫(상위) 카드가 새 top, 나머지가 그 sourceIds가 된다.
public static async Task<HeadlessEntityId?> MaterializeSegmentAsPermanentAsync(
    ICardInstanceRepository repository, IZoneMover zoneMover,
    HeadlessPlayerId owner, IReadOnlyList<HeadlessEntityId> segmentTopToBottom, CancellationToken ct)
```
- segment[0] → `ChoiceZone.None → BattleArea`(새 top), `sourceIds = segment[1..]`.
- 반환: 새 permanent의 top id.

### S4-2. 스택 분할 (Partition)
- 원본: 원 permanent의 소재를 두 그룹으로 분할 → 각각 새 permanent. (원 top은? Partition은 top이 떠날 때 발동 → 원 카드는 떠나고 소재들이 두 permanent로 재구성)
- 절차: 원 permanent 제거(top 떠남) → 소재 리스트를 둘로 나눠 각각 `MaterializeSegmentAsPermanentAsync`.
- 분할 기준: 원본 `PartitionCondition`(카드별). 자동해소 = 결정적 분할(예: 균등/조건순).

### S4-3. De-Digivolve (D-4) 공유
- De-Digivolve = top에서 N단계 소재 제거(상위 N장을 trash) → 남은 최상위 소재가 새 top. ArmorPurge의 일반화(N장).
- `DigivolutionStackHelpers`에 `RemoveTopSourcesAsync(permanentId, count)`(상위 N 소재 제거+다음 승격) 추가 → ArmorPurge/De-Digivolve/(부분)Partition 공유.

## 리스크 & 범위
- 소재 스택은 미완 영역(B-1 🔴). 승격 시 top의 상태(서스펜드/DP모디파이어/소환멀미) 이관 규칙 필요(ArmorPurge는 isSuspended만 이관).
- **권장**: D-4 De-Digivolve와 **공동 설계**(같은 프리미티브). Partition 단독보다 D-4 착수 시 함께.
- 테스트: 세그먼트 승격(새 top+sourceIds) / 2분할(두 permanent) / 상태 이관.

---

# 종합 — 착수 우선순위 (재확인)
1. **S1 효과-구동 공격** (가장 가볍고 레버리지 큼; 설계 완료) → Vortex, Overclock(공격부).
2. **S3 trait** (경량, 데이터 점검) → Overclock(조건부) 완성.
3. **S4 스택 분할** (D-4와 공동) → Partition.
4. **S2 효과-무효** (최광범위·고위험; D-7와 공동, 최후) → Progress.

> 재분류 3종(C-12 Iceclad·C-13 Decode·C-18 Alliance)은 어느 서브시스템에도 의존하지 않으므로 위 순서와 무관하게 선처리 가능.
