# G2E-002 Digivolve action 포팅 테스트 결과

## 실행 일시

- 2026-06-25 14:38:59 +09:00

## 실행 환경

- Workspace: `E:\headlessDCGO_new`
- Runtime: `.NET 8.0`

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionTypes.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionParameterKeys.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessActionFactory.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/MetadataActionProcessor.cs`
- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/HeadlessLegalActionDispatcher.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/DigivolveAction.cs`
- 생성: `tests/G2E-002.Digivolve.action.Tests/G2E-002.Digivolve.action.Tests.csproj`
- 생성: `tests/G2E-002.Digivolve.action.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2E-002_digivolve_action_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/TurnStateMachine.cs`
- `DCGO/Assets/Scripts/Script/CardController.cs`
- `DCGO/Assets/Scripts/Script/Permanent.cs`
- `DCGO/Assets/Scripts/Script/MainPhaseAction/PlayCardAction.cs`

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2E-002.Digivolve.action.Tests\G2E-002.Digivolve.action.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-001.PlayCardAction.Tests\G2E-001.PlayCardAction.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2D-004.Digivolution.source.attach.Tests\G2D-004.Digivolution.source.attach.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2A-006.Legal.action.dispatch.hook.Tests\G2A-006.Legal.action.dispatch.hook.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2E-002 Digivolve.action.Tests | 9 | 9 | 0 | 0 |
| HeadlessDCGO.Engine build | 1 | 1 | 0 | 0 |
| G2E-001 PlayCardAction.Tests | 9 | 9 | 0 | 0 |
| G2D-004 Digivolution source attach.Tests | 10 | 10 | 0 | 0 |
| G2A-006 Legal action dispatch hook.Tests | 10 | 10 | 0 | 0 |

## 실패 상세 및 수정 여부

- 최초 전용 테스트 작성 중 setup zone move 이벤트를 Digivolve 증분 이벤트와 함께 세는 테스트 계측 오류가 있었다.
- Digivolve 직전 이벤트 수를 기준으로 증분 2개를 검증하도록 수정했다.
- 최종 실패 없음.

## 확인한 계약

- `DigivolveAction`은 Main phase legal action 조회에서 손패 카드, BattleArea 대상, `CardRecord.EvolutionCost`, `CardRecord.EvolutionCondition`, memory 지불 가능 여부를 같은 검증 경로로 평가한다.
- legal apply는 동일 검증을 다시 수행한 뒤 대상 카드를 BattleArea에서 제거하고 진화 카드를 Hand에서 BattleArea로 이동한다.
- 진화원 정보는 서비스 기반 런타임 상태에서 `CardInstanceRecord.Metadata["sourceIds"]`로 기록한다. 첫 source는 대상 카드 id이며 대상 카드의 기존 source metadata가 있으면 뒤에 보존한다.
- 비용 불일치, 진화 조건 불일치, 손패 밖 카드, memory 부족은 `ActionProcessResult.Illegal`로 반환하고 memory/zone/source 상태를 바꾸지 않는다.
- AS-IS의 `CanEvolve`, `Digivolution()`, `PlayCardAction` 패킷, `AddDigivolutionCardsTop` 의미를 Headless API에 맞춰 분리했다.

## 테스트하지 않은 항목과 이유

- 조그레스, 버스트, 앱퓨전, option activation, attack, pass는 이번 Goal 범위가 아니므로 구현/테스트하지 않았다.
- 실제 카드 효과 발동, 드로우 보너스, OnDigivolve/WhenWouldDigivolve trigger enqueue는 후속 효과 포팅 범위로 남겼다.
- Unity 시각 효과와 Photon/RPC 동기화는 Headless runtime 범위 밖이다.

## 미해결 리스크

- `EvolutionCondition` 파서는 이번 Goal에서 단순 문자열 조건으로 고정했다. 실제 카드별 복합 진화 조건은 후속 포팅에서 확장해야 한다.
- 서비스 기반 `CardInstanceRecord`에는 아직 강타입 source stack 필드가 없어 `Metadata["sourceIds"]`를 사용한다. 이후 상태 모델 확장이 필요할 수 있다.
- AS-IS의 비용 변경/감소 효과는 아직 적용하지 않았다.

## 완료 기준 충족 근거

- 전용 테스트 `G2E-002.Digivolve.action.Tests` 9/9 통과.
- 엔진 빌드 통과.
- 선행/연결 회귀 `G2E-001`, `G2D-004`, `G2A-006` 모두 통과.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않았다.

## 다음 Goal 진행 가능 여부

- 가능. G2E-002 완료 기준 `digivolve action 테스트 통과`를 충족했다.

## 완료 판정

- COMPLETE
