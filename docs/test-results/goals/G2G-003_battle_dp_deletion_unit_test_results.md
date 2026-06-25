# G2G-003 Battle DP deletion 포팅 테스트 결과

- 실행 일시: 2026-06-25 15:51:35 +09:00
- Goal ID: G2G-003
- 목표: Battle DP deletion 포팅
- 완료 기준: battle 테스트 통과

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/BattleResolver.cs`
- 생성: `tests/G2G-003.Battle.DP.deletion.Tests/G2G-003.Battle.DP.deletion.Tests.csproj`
- 생성: `tests/G2G-003.Battle.DP.deletion.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2G-003_battle_dp_deletion_unit_test_results.md`

## 구현 요약

- AS-IS `IBattle.Battle()`의 DP 비교 의미를 Headless 기준 `BattleResolver.ResolveAsync`로 포팅했다.
- pending attack의 attacker와 현재 target을 battle area Digimon으로 검증하고, 양쪽 `dp` metadata를 비교한다.
- 공격자 DP가 높으면 방어자, 방어자 DP가 높으면 공격자, 동률이면 양쪽을 `ChoiceZone.Trash`로 이동한다.
- 삭제된 카드 instance metadata에 `deletedByBattle=true`, `dpBeforeBattle=<전투 직전 DP>`를 기록한다.
- battle 처리 완료 시 attack state를 resolved 상태로 전환한다.
- 직접공격, DP 없음, 비 Digimon participant는 실패 결과를 반환하고 zone/attack 상태를 변경하지 않는다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2G-003.Battle.DP.deletion.Tests\G2G-003.Battle.DP.deletion.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2G-002.Block.timing.Tests\G2G-002.Block.timing.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2G-001.Attack.declaration.target.Tests\G2G-001.Attack.declaration.target.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-004.Attack.action.Tests\G2E-004.Attack.action.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과 수치

- G2G-003 전용 테스트: 전체 10, 통과 10, 실패 0, 스킵 0
- G2G-002 회귀 테스트: 전체 10, 통과 10, 실패 0, 스킵 0
- G2G-001 회귀 테스트: 전체 10, 통과 10, 실패 0, 스킵 0
- G2E-004 회귀 테스트: 전체 10, 통과 10, 실패 0, 스킵 0
- 엔진 빌드: 경고 0, 오류 0

## 실패 상세

- 최종 실패 없음.
- 중간 실패:
  - `BattleResolutionResult` named argument 대소문자 불일치 컴파일 오류를 수정했다.
  - 테스트의 attack target parameter key를 실제 계약인 `AttackTargetId`로 수정했다.
  - AS-IS 원본 호출 검증 문자열을 실제 named-argument 형태에 맞게 수정했다.

## 미해결 리스크

- 이 Goal은 DP 비교와 battle deletion 처리만 포함한다.
- AS-IS의 전투 시작/종료 트리거, battle effect, security battle, deletion replacement/immunity 세부 효과는 이번 Goal 범위 밖이다.
- 작업공간 루트에서 `git status --short`는 `fatal: not a git repository`로 실패하여 Git 기준 변경 목록은 확인하지 못했다.

## 완료 판정

COMPLETE
