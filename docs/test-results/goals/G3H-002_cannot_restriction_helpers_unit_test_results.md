# G3H-002 Cannot restriction helper porting

- 실행 일시: 2026-06-25 19:56:50 +09:00
- Goal ID: G3H-002
- 완료 기준: 제한 테스트 통과
- 완료 판정: COMPLETE

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/RestrictionHelpers.cs`
- 생성: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/RestrictionHelpers.cs`
- 생성: `tests/G3H-002.Cannot.restriction.helper.Tests/G3H-002.Cannot.restriction.helper.Tests.csproj`
- 생성: `tests/G3H-002.Cannot.restriction.helper.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3H-002_cannot_restriction_helpers_unit_test_results.md`

## 읽기 전용 AS-IS 확인 파일

- `DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotAttack.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotBlock.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotBeDeleted.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotBeDeletedByEffect.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotBeDeletedByBattle.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotReturnToHand.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/CanNoReturnToDeck.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotSuspend.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotAttack.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotBlock.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPermanent/CanNotReturnToHand.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/GiveEffect/GiveEffectToPlayer/CanNotSuspend.cs`

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G3H-002.Cannot.restriction.helper.Tests\G3H-002.Cannot.restriction.helper.Tests.csproj
```

## 테스트 결과

- 전체: 11
- 통과: 11
- 실패: 0
- 스킵: 0

통과 항목:

- G3H-002 goal row and predecessor are satisfied
- AS-IS cannot restriction references are recorded
- Attack block delete return suspend restrictions resolve
- Restriction target and source filters skip non matching restrictions
- Metadata boolean restrictions are read from card and instance
- CardInstanceState modifiers and flags are read as restrictions
- Effect query restriction requests are read from context values
- Restriction result values are deterministic
- Invalid restriction input fails with explicit exception
- CardEffectCommons factory creates headless restrictions
- G3H-002 source files stay inside restriction helper scope

## 실패 상세

- 없음

## 테스트하지 못한 항목과 이유

- 실제 카드별 효과 해석기는 후속 카드 효과 포팅 범위이므로 구현하지 않았다.
- 원본 Unity coroutine, visual buff/debuff, `CanNotBeAffected` UI/연출 경로는 Headless 제한 helper 계약 밖이므로 포팅하지 않았다.

## 미해결 리스크

- `CannotRestrictionKind.Delete`는 삭제 일반 제한을 고정한다. AS-IS의 battle/effect별 세부 삭제 제한은 `reason`, `sourceEntityId`, 이후 효과 해석기의 query scope로 세분화할 수 있게 열어 두었다.
- 기존 엔진 파일의 nullable 경고가 테스트 출력에 함께 표시된다. 이번 Goal 범위 밖 기존 경고라 수정하지 않았다.

## 완료 근거

- 선행 Goal `G3H-001` 결과 문서의 `COMPLETE` 판정을 확인했다.
- `cannot attack/block/delete/return to hand/return to deck/suspend` 계열을 public `CannotRestriction` 모델과 `RestrictionHelpers` API로 고정했다.
- metadata, `CardInstanceState`, `IEffectQueryService.GetRestrictionEffects` 경로에서 restriction을 읽는 테스트를 통과했다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용으로만 확인했고 수정하지 않았다.
- 다음 Goal 진행 가능 여부: 가능
