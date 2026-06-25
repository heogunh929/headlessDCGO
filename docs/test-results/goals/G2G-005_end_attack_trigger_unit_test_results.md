# G2G-005 End attack trigger 결과

- 실행 일시: 2026-06-25 18:07:27 +09:00
- Goal ID: `G2G-005`
- 완료 판정: **COMPLETE**

## 수정/생성 파일
- `docs/test-results/goals/G2G-005_end_attack_trigger_unit_test_results.md` (이번 보고서 갱신)
- `tests/G2G-005.End.attack.trigger.Tests/Program.cs` (구현이 이미 완료된 상태로 검증 실행)
- `src/HeadlessDCGO.Engine/Headless/Effects/EndAttackTriggerHook.cs` (구현 내용은 기존 상태 기준으로 계약 검증 완료)

## 테스트 명령
- `.\.dotnet\dotnet.exe run --project .\tests\G2G-005.End.attack.trigger.Tests\G2G-005.End.attack.trigger.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2F-001.Trigger.event.collection.Tests\G2F-001.Trigger.event.collection.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2G-004.Security.check.Tests\G2G-004.Security.check.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과 요약
- 전체: 8
- 통과: 8
- 실패: 0
- 스킵: 0

## 실패 상세
- 없음

## 미해결 리스크
- 없음

## 완료 기준
- `end attack 트리거 테스트 통과`
