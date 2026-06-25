# G3I-001 Replacement prevention helper porting

- 실행 일시: 2026-06-25 20:02:31 +09:00
- Goal ID: G3I-001
- 완료 기준: 대체 효과 테스트 통과
- 완료 판정: COMPLETE

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/ReplacementHelpers.cs`
- 생성: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/ReplacementHelpers.cs`
- 생성: `tests/G3I-001.Replacement.prevention.helper.Tests/G3I-001.Replacement.prevention.helper.Tests.csproj`
- 생성: `tests/G3I-001.Replacement.prevention.helper.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3I-001_replacement_prevention_helpers_unit_test_results.md`

## 읽기 전용 AS-IS 확인 파일

- `DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/ArmorPurge.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Evade.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Decoy.cs`
- `DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Scapegoat.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Evade.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Decoy.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Scapegoat.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Progress.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/ImmuneFromDPMinus.cs`

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G3I-001.Replacement.prevention.helper.Tests\G3I-001.Replacement.prevention.helper.Tests.csproj
```

## 테스트 결과

- 전체: 12
- 통과: 12
- 실패: 0
- 스킵: 0

통과 항목:

- G3I-001 goal row and predecessor are satisfied
- AS-IS replacement prevention references are recorded
- Prevent replacement cancels field removal
- Redirect replacement returns substitute target
- Immune replacement filters source and mutation kind
- Metadata replacements are read from card and instance
- CardInstanceState replacements are read from modifiers and flags
- Effect query replacement requests are read from context values
- Replacement result values are deterministic
- Invalid redirect input fails with explicit exception
- CardEffectCommons factory creates headless replacements
- G3I-001 source files stay inside replacement helper scope

## 실패 상세

- 없음

## 테스트하지 못한 항목과 이유

- 실제 카드별 replacement 효과 해석과 선택 UI는 후속 카드 효과 포팅 범위이므로 구현하지 않았다.
- Unity coroutine, visual buff/debuff, card object 이동 연출은 Headless replacement helper 계약 밖이므로 포팅하지 않았다.

## 미해결 리스크

- `ReplacementActionKind.Redirect`는 대체 대상 id를 결정해 반환하는 계약만 고정한다. 실제 대체 대상 삭제/이동 mutation 실행은 이후 효과 해석기나 런타임 mutation 단계가 소비해야 한다.
- 기존 엔진 파일의 nullable 경고가 첫 테스트 출력에 함께 표시됐다. 이번 Goal 범위 밖 기존 경고라 수정하지 않았다.

## 완료 근거

- 선행 Goal `G3H-002` 결과 문서의 `COMPLETE` 판정을 확인했다.
- `prevent / redirect / immune` replacement 효과를 public `ReplacementEffect`, `ReplacementRequest`, `ReplacementResult`, `ReplacementHelpers` API로 고정했다.
- metadata, `CardInstanceState`, `IEffectQueryService.GetReplacementEffects` 경로에서 replacement를 읽는 테스트를 통과했다.
- 원본 `DCGO/Assets/...` 파일은 읽기 전용으로만 확인했고 수정하지 않았다.
- 다음 Goal 진행 가능 여부: 가능
