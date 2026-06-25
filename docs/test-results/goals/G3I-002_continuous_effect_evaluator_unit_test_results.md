# G3I-002 Continuous effect evaluator 단위테스트 결과

- 실행 일시: 2026-06-25 20:09:29 +09:00
- Goal ID: G3I-002
- 완료 기준: 상시 효과 테스트 통과
- 완료 판정: COMPLETE

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/ContinuousEffectEvaluator.cs`
- 생성: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/ContinuousEffectEvaluator.cs`
- 생성: `tests/G3I-002.Continuous.effect.evaluator.Tests/G3I-002.Continuous.effect.evaluator.Tests.csproj`
- 생성: `tests/G3I-002.Continuous.effect.evaluator.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3I-002_continuous_effect_evaluator_unit_test_results.md`

## 참조한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/ContinuousController.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/ChangeDP.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/ChangePlayCost.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotAttack.cs`

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G3I-002.Continuous.effect.evaluator.Tests\G3I-002.Continuous.effect.evaluator.Tests.csproj
```

## 테스트 결과

- 전체: 10
- 통과: 10
- 실패: 0
- 스킵: 0

통과 테스트:

- G3I-002 goal row and predecessor are satisfied
- AS-IS continuous effect references are recorded
- Registry continuous effects are collected by query scope
- Card instance and state metadata are recalculated together
- State mutation changes recalculated modifier result
- Continuous restrictions and replacements are exposed together
- Evaluation result values are deterministic
- Invalid continuous evaluation input fails explicitly
- CardEffectCommons facade delegates to evaluator
- G3I-002 source files stay inside continuous evaluator scope

## 실패 상세

- 없음

## 실행 중 경고

- `HeadlessGameLoop.cs`, `MetadataActionProcessor.cs`의 기존 nullable warning이 함께 출력되었다.
- 이번 Goal 범위 밖 Runtime 기존 경고이므로 수정하지 않았다.

## 미해결 리스크

- G3I-002는 기존 `ModifierHelpers`, `RestrictionHelpers`, `ReplacementHelpers`의 메타데이터 해석 계약을 조합해 상시 효과 재평가 surface를 고정했다.
- 실제 카드별 상시 효과 본문 전체 포팅은 후속 카드/효과 포팅 Goal 범위로 남아 있다.

## 완료 판정

COMPLETE
