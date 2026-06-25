# G3J-002 PermanentEffectFactory binding 포팅 단위테스트 결과

- 실행 일시: 2026-06-25 20:20:39 +09:00
- Goal ID: G3J-002
- 완료 기준: PermanentEffectFactory 테스트 통과
- 완료 판정: COMPLETE

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/PermanentEffectFactoryBinding.cs`
- 수정: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/PermanentEffectFactory.cs`
- 생성: `tests/G3J-002.PermanentEffectFactory.binding.Tests/G3J-002.PermanentEffectFactory.binding.Tests.csproj`
- 생성: `tests/G3J-002.PermanentEffectFactory.binding.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3J-002_permanent_effect_factory_binding_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/PermanentEffectFactory.cs`
- `DCGO/Assets/Scripts/Script/Permanent.cs`
- `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Collision.cs`

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G3J-002.PermanentEffectFactory.binding.Tests\G3J-002.PermanentEffectFactory.binding.Tests.csproj
```

## 테스트 결과

- 전체: 10
- 통과: 10
- 실패: 0
- 스킵: 0

통과 테스트:

- G3J-002 goal row and predecessor are satisfied
- AS-IS PermanentEffectFactory references are recorded
- Permanent id and top card lookup create bindings
- Permanent bindings register into EffectRegistry
- Trigger mismatch returns explicit permanent failure
- Duplicate permanent effect ids fail without registry mutation
- Repeated permanent lookup is deterministic
- Invalid permanent binding inputs fail explicitly
- Assets PermanentEffectFactory facade creates permanent rules
- G3J-002 source files stay inside permanent binding scope

## 실패 상세 및 수정 여부

- 최초 실행에서 invalid top card fixture가 잘못되어 1개 테스트가 실패했다.
- `CreateMismatchedTopCard()` fixture를 추가해 `CardRecord.Id`가 permanent definition id와 다르게 들어가도록 테스트를 수정했다.
- 재실행 결과 전체 10개 테스트가 통과했다.

## 실행 중 경고

- 첫 빌드 때 `HeadlessGameLoop.cs`, `MetadataActionProcessor.cs`의 기존 nullable warning이 함께 출력되었다.
- 이번 Goal 범위 밖 Runtime 기존 경고이므로 수정하지 않았다.

## 테스트하지 못한 항목과 이유

- 실제 카드별 permanent 효과 본문 전체 포팅은 G3J-002 범위가 아니므로 수행하지 않았다.
- 후속 `Effect selection helper` 작업은 G3K-001 범위이므로 앞당기지 않았다.

## 완료 기준 충족 근거

- permanent instance id, definition id, top card card number, top card `EffectBindingKey` 기반 lookup key 계약을 고정했다.
- trigger가 일치하는 permanent rule만 `EffectBinding`을 생성하도록 검증했다.
- 생성된 binding을 `EffectRegistry`에 등록하고 timing, keyword alias, query role/scope lookup으로 다시 조회했다.
- 실패 케이스는 명시적 failure result를 반환하며 registry를 변경하지 않음을 검증했다.
- 동일 입력 반복 결과가 결정적임을 검증했다.

## 다음 Goal 진행 가능 여부

- 가능

## 완료 판정

COMPLETE
