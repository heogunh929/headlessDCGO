# S1 — 효과-구동 공격 (Effect-Driven Attack) 설계

- 작성일: 2026-06-28
- 목적: 효과가 특정 Digimon으로 **공격을 개시**하는 엔진 메커니즘. AS-IS `SelectAttackEffect` 대응.
- 해금: **C-20 Vortex**, **C-16 Overclock**(공격부). 인접: C-9 Execute(현재 flag 근사 대체 가능), D 단계 다수.
- 상태: 설계(미구현). 승인 후 구현.

---

## 1. 핵심 통찰 — 새 파이프라인 불필요

`GameFlowProcessor.RunToStableAsync` 루프의 step 3:
```
if (context.AttackController.Current.Phase != AttackPhase.None)
    await _attackPipeline.AdvanceAsync(context, ct);
```
→ **`AttackController`에 공격이 선언(Phase=Declared)되기만 하면** 기존 파이프라인이 블록 타이밍→전투/시큐리티→종료까지 구동하고, 블록 선택 시 자동 일시정지/재개한다. (`AttackPermanentAction.Process`도 결국 `AttackController.DeclareAttack`을 호출할 뿐.)

따라서 S1 = **효과 해소 중 `DeclareAttack`을 호출하는 얇은 개시기** + 효과별 옵션 적용. 새 상태기계·재진입 처리 불필요.

## 2. 일반 공격 vs 효과-구동 공격 차이

| 항목 | 일반(AttackPermanentAction) | 효과-구동 |
|------|----------------------------|-----------|
| 게이트 | Main 페이즈 + 턴 플레이어 + 소환멀미 + cannot-attack | **우회**(효과가 부여) |
| 공격자 서스펜드 | 항상(SuspendAttacker) | 옵션 `WithoutTap`(Overclock=언탭 공격) |
| 대상 | 합법 대상 열거 | 효과가 제한(Overclock=플레이어만, Vortex=디지몬+플레이어) |
| 언서스펜드 디지몬 공격 | 불가(없으면) | 옵션(`canAttackUnsuspendedDigimon` 기존 플래그 재사용) |
| 누가 선택 | agent legal-action | 효과(자동해소) 또는 agent 선택(후속) |

## 3. 컴포넌트 (신규 `Headless/Runtime/EffectDrivenAttack.cs`)

### 3-1. 옵션 레코드
```csharp
public sealed record EffectAttackOptions(
    bool WithoutTap = false,          // Overclock: 공격자 서스펜드 안 함
    bool AllowPlayerTarget = true,    // 플레이어(시큐리티) 직접 공격 허용
    bool AllowDigimonTarget = true,   // 상대 디지몬 공격 허용 (Overclock=false)
    bool TargetUnsuspended = true);   // 언서스펜드 디지몬도 대상 가능
```

### 3-2. 대상 열거기
```csharp
public static IReadOnlyList<AttackTargetCandidate> GetTargets(
    EngineContext context, HeadlessEntityId attackerId, EffectAttackOptions options)
```
- 공격자 소유자=공격측, NonTurnPlayer=방어측(효과-구동도 현 턴 컨텍스트 사용).
- `AllowDigimonTarget`이면 방어측 BattleArea 디지몬(옵션 따라 서스펜드 필터) → Digimon 후보.
- `AllowPlayerTarget`이면 Direct(player) 후보.
- 기존 `AttackTargetCandidate`(AttackPermanentAction) 재사용.

### 3-3. 개시기
```csharp
public static bool Initiate(
    EngineContext context, HeadlessEntityId attackerId,
    AttackTargetCandidate target, EffectAttackOptions options)
```
1. 공격 진행 중이면(`AttackController.Current.IsPending`) false(중첩 금지).
2. `!WithoutTap`이고 공격자 언서스펜드면 서스펜드(메타 `isSuspended`).
3. `TargetUnsuspended`이면 공격자에 `canAttackUnsuspendedDigimon` 임시 부여(또는 검증 우회 — 효과-구동은 BattleResolver가 대상 서스펜드 검사 안 하므로 사실상 불필요; 직접 `DeclareAttack`은 합법성 미검사).
4. `context.AttackController.DeclareAttack(attackingPlayer, attackerId, defendingPlayer, target.TargetId, target.IsDirectAttack)`.
5. return true. → 이후 `GameFlowProcessor` step3가 구동.

> 주: `AttackController.DeclareAttack`은 합법성 검사를 하지 않으므로(상태만 설정) 효과-구동의 게이트 우회가 자연스럽다. C-3 Raid가 추가한 `SwitchDefender`도 같은 계열.

## 4. 자동해소 vs agent 선택
- 현행 정책(optional 자동해소/첫 후보)과 일관: 효과-구동 공격 대상은 **자동 = `GetTargets` 첫 후보**(결정적). LIMITATION 주석.
- 후속: `ChoiceController`/`OptionalPromptQueue`로 대상 선택 노출(공격은 agent 결정 가치가 큼 → 별도 작업으로 명시).

## 5. 키워드 매핑
- **Vortex**: `Initiate(opts{ AllowPlayerTarget = canAttackPlayers, AllowDigimonTarget = true })`. (자동: 첫 후보)
- **Overclock**(공격부): trait 아군 삭제 성공 후 `Initiate(opts{ WithoutTap = true, AllowPlayerTarget = true, AllowDigimonTarget = false })`. (삭제부는 S3 trait 선결, `DeletePeremanentAndProcessAccordingToResult`=기존 Delete 뮤테이션)
- **Execute**: 현재 `canAttackUnsuspendedDigimon`+`deleteSelfAtEndOfAttack` 플래그로 근사 완료. S1 도입 후 "효과가 공격 개시"로 정합화 가능(선택).

## 6. 엣지 케이스
- **블록 재진입**: 기존 파이프라인이 처리(설계상 무처리). 효과-구동 공격이 블록 선택을 열면 루프가 일시정지→ResolveChoice→재개.
- **withoutTap + 종료 후**: 언탭 공격자는 종료 시 서스펜드 안 됨(서스펜드 안 했으므로 자연 정합).
- **중첩 공격**: step1 가드(이미 pending이면 거부). 효과-구동 공격은 다른 공격 해소 중 개시 불가.
- **once-per-turn / 소환멀미**: 효과-구동은 우회(효과가 책임). 단 효과 자체의 MaxCountPerTurn은 기존 F-4가 게이트.
- **공격자 부재/비-Digimon**: `GetTargets`/`Initiate`가 공격자 유효성 점검 후 no-op.

## 7. 테스트 계획 (`tests/G3.5-S1.EffectDrivenAttack`)
1. Initiate → AttackController에 공격 선언(Phase=Declared, target/direct 일치).
2. GameFlowProcessor 1스텝 구동 → 공격 진행(블록 없으면 전투/시큐리티까지).
3. `WithoutTap` → 공격자 미서스펜드.
4. Overclock 옵션(AllowDigimonTarget=false) → 플레이어 대상만 후보.
5. Vortex 옵션 → 디지몬+플레이어 후보.
6. 중첩 거부(이미 pending).

## 8. 산출/허브
- 신규 `EffectDrivenAttack`(개시기+대상열거) = 효과-구동 공격 허브. Vortex/Overclock(+선택적 Execute 정합화)이 사용.
- D 단계(효과로 공격을 거는 카드 다수)에서 재사용.

---

## 다른 선결 서브시스템 (요약 — 상세는 [cgroup4_subsystem_analysis.md](cgroup4_subsystem_analysis.md))
- **S2 효과-무효(CanNotAffected)**: 뮤테이션/효과 적용 직전 "이 대상이 무효 대상인가" 연속-게이트(`ContinuousRestrictionGate` 확장). 광범위·신중. → C-15 Progress, D-7 인접.
- **S3 trait**: 카드 데이터(JSON) traits 적재 점검 + `CardRequirementHelpers.HasTrait` 재사용. → C-16 Overclock(조건부).
- **S4 소재 스택 분할/승격**: `DigivolutionStackHelpers`에 "소재 N장→새 permanent 승격" 추가, D-4 De-Digivolve와 공동 설계. → C-14 Partition.
