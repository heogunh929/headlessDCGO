# G2F-003 Optional prompt queue 포팅

## 실행 일시

- 2026-06-25 15:21:41 +09:00
- 환경: Windows PowerShell, .NET SDK via `.\.dotnet\dotnet.exe`

## Goal 범위

- Goal ID: G2F-003
- 목표: Optional prompt queue 포팅
- 작업 범위: optional effect 선택 요청 연결
- 산출물: optional prompt queue
- 선행 Goal: G2F-002; G1E-005
- 완료 기준: optional prompt 테스트 통과

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Choices/ChoiceType.cs`
- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/OptionalPromptQueue.cs`
- 생성: `tests/G2F-003.Optional.prompt.queue.Tests/G2F-003.Optional.prompt.queue.Tests.csproj`
- 생성: `tests/G2F-003.Optional.prompt.queue.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2F-003_optional_prompt_queue_unit_test_results.md`

## 읽기 전용 AS-IS 참조 파일

- `DCGO/Assets/Scripts/Script/MultipleSkills.cs`
- `DCGO/Assets/Scripts/Script/AutoProcessing.cs`
- `DCGO/Assets/Scripts/Script/SelectCardEffect.cs`

원본 `DCGO/Assets/...` 파일은 수정하지 않았다.

## 구현 요약

- AS-IS `MultipleSkills`가 optional effect 후보를 `OpenSelectCardPanel`로 선택 요청하고, 모두 skippable이면 "Don't activate"를 허용하는 의미를 Headless `ChoiceRequest` 기반 prompt queue로 분리했다.
- `ChoiceType.OptionalEffect`를 추가해 optional effect 선택 요청을 기존 카드/액션 선택과 구분했다.
- `OptionalPromptQueue`를 추가해 optional trigger 묶음을 FIFO prompt로 보관한다.
- `RequestNextChoice`는 다음 prompt를 `IHeadlessChoiceController.RequestChoice`로 연결해 pending choice 상태를 만든다.
- `ResolveChoice`는 skip 결과면 scheduler에 아무 것도 넣지 않고 prompt를 제거한다.
- `ResolveChoice`는 선택 결과면 선택된 optional effect만 `EffectScheduler`에 enqueue한다.
- mandatory trigger, 다른 플레이어 trigger, null/empty 입력, 이미 pending choice가 있는 상태는 명시적 실패 결과를 반환하고 기존 상태를 덮지 않는다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2F-003.Optional.prompt.queue.Tests\G2F-003.Optional.prompt.queue.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2F-002.Mandatory.effect.ordering.Tests\G2F-002.Mandatory.effect.ordering.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1E-005.Choice.pause.resume.contract.Tests\G1E-005.Choice.pause.resume.contract.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2F-001.Trigger.event.collection.Tests\G2F-001.Trigger.event.collection.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1E-003.ScriptedChoiceProvider.Tests\G1E-003.ScriptedChoiceProvider.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1E-004.PolicyChoiceProvider.Tests\G1E-004.PolicyChoiceProvider.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G1E-002.ChoiceResult.validation.Tests\G1E-002.ChoiceResult.validation.Tests.csproj`

## 테스트 결과

| 구분 | 전체 | 통과 | 실패 | 스킵 |
| --- | ---: | ---: | ---: | ---: |
| G2F-003 전용 테스트 | 10 | 10 | 0 | 0 |
| 선행/인접 회귀 테스트 합계 | 60 | 60 | 0 | 0 |
| 전체 실행 테스트 합계 | 70 | 70 | 0 | 0 |

빌드 결과:

- `HeadlessDCGO.Engine.csproj`: 성공, 경고 9개, 오류 0개
- 경고는 기존 `HeadlessGameLoop.cs`, `MetadataActionProcessor.cs` nullable 경고이며, G2F-003 새 파일의 nullable 경고는 수정 후 재실행했다.

## G2F-003 전용 테스트 상세

- G2F-003 goal row and predecessors are satisfied: PASS
- AS-IS optional prompt references are recorded: PASS
- Optional prompt queue requests a skippable choice from optional triggers: PASS
- Skipping optional prompt leaves scheduler unchanged and dequeues prompt: PASS
- Selecting optional prompt enqueues selected effect only: PASS
- Prompt queue preserves multiple prompts in FIFO order: PASS
- Pending choice prevents optional prompt overwrite: PASS
- Invalid optional prompt input returns explicit failure: PASS
- Optional prompt queue is deterministic for repeated equivalent input: PASS
- G2F-003 source files contain no placeholder or Unity dependency: PASS

## 실패 상세와 수정 여부

- 최종 실패 없음.
- 최초 G2F-003 전용 테스트 통과 후 새 파일에서 nullable 경고 1개가 확인되어 `buildResult.Prompt!`로 명시 수정했다.

## 테스트하지 못한 항목과 이유

- 실제 카드 효과 본문 실행과 optional effect의 카드별 `CanActivate` 세부 판정은 후속 카드 효과 포팅 범위라 수행하지 않았다.
- Unity `SelectCardPanel` UI, command text, 보안 글래스 연출은 Headless 엔진 범위 밖이라 수행하지 않았다.
- security/delayed trigger hook은 후속 G2F-004 범위라 수행하지 않았다.

## 미해결 리스크

- `git status --short`는 현재 작업 디렉터리가 git repository가 아니어서 `fatal: not a git repository`로 확인되지 않았다.
- 엔진 빌드에서 기존 Runtime nullable 경고 9개가 출력된다. 이번 G2F-003 산출물 자체의 경고는 제거했다.
- optional prompt queue는 선택 요청과 scheduler 연결 계약까지 구현했다. 실제 선택된 효과의 실행 본문은 후속 자동처리/카드 효과 Goal에서 확장되어야 한다.

## 완료 기준 충족 근거

- CSV의 단위테스트 범위 `optional choice 테스트`를 G2F-003 전용 테스트에서 직접 검증했다.
- 전용 테스트가 choice request 생성, skip, 선택 enqueue, FIFO, pending overwrite 방지, invalid input 실패, 결정성을 포함한다.
- 선행 Goal `G2F-002`, `G1E-005` 결과 문서의 COMPLETE를 확인했다.
- 원본 `DCGO/Assets/...` 파일을 수정하지 않았다.

## 다음 Goal 진행 가능 여부

- 가능. G2F-003 산출물 `optional prompt queue`와 결과 문서가 준비되었고, 완료 기준 `optional prompt 테스트 통과`를 충족했다.

## 완료 판정

COMPLETE
