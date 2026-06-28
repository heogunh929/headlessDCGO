# C-그룹4 보류 7종 — 선결 서브시스템 분석

- 작성일: 2026-06-28
- 대상: C-12 Iceclad · C-13 Decode · C-14 Partition · C-15 Progress · C-16 Overclock · C-18 Alliance · C-20 Vortex
- 방법: 원본 DCGO 소스(`DCGO/.../KeyWordEffects/`, `CardController.cs`, `Permanent.cs`) 정독 + 헤드리스 기존 인프라 대조.
- 결론 요약: **7종 중 3종은 신규 서브시스템 없이 구현 가능**(오분류였음). 나머지 4종은 4개의 구분되는 선결 서브시스템에 의존하며, 그중 **효과-구동 공격**이 최고 레버리지.

---

## 1. 키워드별 동작 & 선결 요건

| ID | 키워드 | 원본 동작(정확) | 필요 인프라 | 헤드리스 보유 | 판정 |
|----|--------|----------------|-------------|--------------|------|
| C-12 | **Iceclad** | 전투 시 **둘 중 하나라도 Iceclad면 DP 대신 디지볼루션 소재 수로 승부 비교**(`CardController.CompareStats`) | BattleResolver 비교 분기 + `sourceIds` 카운트 | 둘 다 ✅ | **즉시 가능** |
| C-13 | **Decode** | 필드를 떠날 때(WhenRemoveField), 조건 맞는 **디지볼루션 소재 1장을 무료 플레이** | 소재→배틀존 PlayForFree + 삭제-후 훅 | PlayCardKind✅(B-8)·소재모델✅·삭제후훅✅(Fortitude류) | **소규모 가능** |
| C-18 | **Alliance** | 다른 아군 1장 **서스펜드**(코스트) → 이 디지몬 +DP(=그 아군 DP)·+1 SecAtk(UntilEndAttack) | select + suspend-cost + DP/SecAtk 모디파이어 | SelectPermanent✅·suspend✅·`ContinuousModifierGate`✅ | **가능**(활성 효과; 발동은 포팅) |
| C-14 | **Partition** | 필드를 떠날 때(비전투·비자기효과), 소재≥2를 **둘로 분할해 각각 별도 permanent로 플레이** | 🔴 **스택 분할/소재 materialize** | ❌ 소재 스택 분할 미완 | **블록** |
| C-15 | **Progress** | 공격 중, **상대 효과의 영향을 받지 않음**(UntilEndAttack) | 🔴 **효과-무효(CanNotAffected) 프레임워크** | ❌ 일반 무효 프레임워크 없음 | **블록** |
| C-16 | **Overclock** | trait 일치 아군 1장 삭제 → 그 후 **언탭 상태로 플레이어 공격** | 🔴 **trait 시스템** + 🔴 **효과-구동 공격** | ❌ 둘 다 없음 | **블록** |
| C-20 | **Vortex** | **효과로 공격 개시**(플레이어 공격 가능/대상 전환) | 🔴 **효과-구동 공격** | ❌ | **블록** |

---

## 2. 선결 서브시스템 (공유 블로커)

### S1. 효과-구동 공격 (`SelectAttackEffect` 대응) — 🔴 최고 레버리지
- 원본: 효과가 특정 Digimon으로 **공격을 개시**(대상 선택, 플레이어 공격 허용, `withoutTap`/`isVortex` 플래그).
- 의존: **C-16 Overclock, C-20 Vortex** (+ C-9 Execute는 현재 flag로 근사, D-3 Raid는 전환만으로 해결됨).
- 헤드리스 갭: 공격은 agent legal-action으로만 선언됨. "효과가 공격을 강제 개시"하는 경로 없음.
- 구축 방향: `AttackController.DeclareAttack` + `AttackPipeline`을 효과 해소 중 호출하는 진입점(또는 agent에게 "이 효과로 공격" legal-action 노출). 플레이어 공격/withoutTap 옵션 반영.

### S2. 효과-무효 (CanNotAffected) 프레임워크 — 🔴 광범위 영향
- 원본: `CardSource.CanNotBeAffected(cardEffect)` — 특정 Digimon이 (상대) 효과의 영향을 받지 않음. 카드 전반에서 광범위 사용.
- 의존: **C-15 Progress** (+ 수많은 카드 효과의 게이팅, D-7 무효화와 인접).
- 헤드리스 갭: 일반 무효 프레임워크 없음(`cannotBeAffectedByCollision` 같은 단발 플래그만).
- 구축 방향: 효과 적용 직전 대상이 무효 대상인지 검사하는 연속-효과 게이트(`ContinuousRestrictionGate` 패턴 확장). 뮤테이션 sink/게이트가 적용 전 조회.

### S3. Trait 시스템 (`ContainsTraits`) — 🟡 데이터+헬퍼
- 원본: 카드의 trait(종족/형태 등) 보유 + 매칭(`TopCard.ContainsTraits(trait)`).
- 의존: **C-16 Overclock** (+ 다수 카드 조건 — 이미 `CardRequirementHelpers.HasTrait`가 일부 존재).
- 헤드리스 갭: trait 데이터가 카드 레코드/메타데이터에 일관 적재되는지 확인 필요(`HasTrait`는 있으나 데이터 소스 점검).
- 구축 방향: 카드 데이터(JSON)에 traits 적재 확인 + `CardRequirementHelpers` 재사용.

### S4. 소재 스택 분할/materialize — 🔴 (D-4 De-Digivolve와 공유)
- 원본: 소재 묶음을 **독립 permanent로 승격/분할**(Partition은 2분할, ArmorPurge는 top-shed로 일부만 다룸).
- 의존: **C-14 Partition** (+ **D-4 De-Digivolve**, B-1 소재처리 🔴, B-10).
- 헤드리스 갭: 소재 인스턴스는 `ChoiceZone.None`에 보존되나 "여러 장을 새 permanent로 동시 승격"은 미구현(ArmorPurge는 1장 승격만).
- 구축 방향: `DigivolutionStackHelpers`에 "소재 N장을 새 permanent로 승격(스택 재구성)" 추가 + D-4와 공동 설계.

---

## 3. 재분류 결과

- **신규 서브시스템 없이 구현 가능 (오분류였음)**: **C-12 Iceclad, C-13 Decode, C-18 Alliance** → 다음 증분에서 바로 처리 가능.
- **블록(서브시스템 선결 필요)**:
  - S1 효과-구동 공격 → C-16(부분), C-20
  - S2 효과-무효 → C-15
  - S3 trait → C-16(부분)
  - S4 스택 분할 → C-14

## 4. 권장 착수 순서
1. **즉시**: C-12 Iceclad → C-13 Decode → C-18 Alliance (서브시스템 불필요, 기존 헬퍼 재사용).
2. **S1 효과-구동 공격** 구축 → C-20 Vortex, C-16 Overclock(공격부) 해금. (D 단계 다수와도 인접)
3. **S4 스택 분할** 구축(= D-4 De-Digivolve와 공동) → C-14 Partition.
4. **S3 trait** 점검/적재 → C-16 Overclock 완성.
5. **S2 효과-무효** 구축(= D-7 무효화 인접) → C-15 Progress. (가장 광범위, 신중 설계)
