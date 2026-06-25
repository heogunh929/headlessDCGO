# G2G-004 Security check 포팅 테스트 결과

- 실행 일시: 2026-06-25 15:57:07 +09:00
- Goal ID: G2G-004
- 목표: Security check 포팅
- 완료 기준: 시큐리티 체크 테스트 통과

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Runtime/SecurityResolver.cs`
- 생성: `tests/G2G-004.Security.check.Tests/G2G-004.Security.check.Tests.csproj`
- 생성: `tests/G2G-004.Security.check.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2G-004_security_check_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/AttackProcess.cs`
- `DCGO/Assets/Scripts/Script/CardController.cs`

## 구현 요약

- AS-IS `AttackProcess`의 direct attack 후 `ISecurityCheck` 호출 흐름을 Headless 기준 `SecurityResolver.ResolveAsync`로 분리했다.
- pending direct attack의 attacker와 defender를 검증하고, attacker의 `strike` metadata를 기준으로 security top card를 체크한다.
- 체크된 security card는 `ChoiceZone.Security`에서 `ChoiceZone.Trash`로 이동한다.
- 체크된 card instance metadata에 `checkedBySecurityCheck`, `securityCheckOrder`, `securityCheckedPlayerId`, `securityCheckAttackerId`를 기록한다.
- strike가 0이면 zone 이동 없이 attack을 resolved 처리한다.
- security가 없거나 direct attack이 아니거나 attacker가 Digimon이 아닌 경우 실패 결과를 반환하고 zone/attack 상태를 변경하지 않는다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2G-004.Security.check.Tests\G2G-004.Security.check.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2G-003.Battle.DP.deletion.Tests\G2G-003.Battle.DP.deletion.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2G-002.Block.timing.Tests\G2G-002.Block.timing.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2G-001.Attack.declaration.target.Tests\G2G-001.Attack.declaration.target.Tests.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-004.Attack.action.Tests\G2E-004.Attack.action.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`

## 테스트 결과 수치

- G2G-004 전용 테스트: 전체 9, 통과 9, 실패 0, 스킵 0
- G2G-003 회귀 테스트: 전체 10, 통과 10, 실패 0, 스킵 0
- G2G-002 회귀 테스트: 전체 10, 통과 10, 실패 0, 스킵 0
- G2G-001 회귀 테스트: 전체 10, 통과 10, 실패 0, 스킵 0
- G2E-004 회귀 테스트: 전체 10, 통과 10, 실패 0, 스킵 0
- 엔진 빌드: 경고 0, 오류 0

## 실패 상세

- 최종 실패 없음.
- 중간 실패:
  - 테스트 fixture에서 security card가 hand에 있다고 가정해 `Hand -> Security` 이동이 실패했다.
  - security fixture 배치는 테스트 준비용 삽입인 `None -> Security`로 수정했다.

## 테스트하지 않은 항목과 이유

- security skill 실제 효과 발동은 이번 Goal 범위 밖이다.
- security Digimon과 attacker의 battle 처리는 AS-IS에 존재하지만, 현재 Goal 산출물은 `security check와 security zone 이동`으로 제한된다.
- end attack trigger 연결은 다음 Goal `G2G-005` 범위라 앞당기지 않았다.

## 미해결 리스크

- `SecurityResolver`는 security check card 공개/이동 계약을 고정하지만, AS-IS의 security skill 선택 UI, 실행 카드 영역, security Digimon battle, delayed trigger 세부 연결은 후속 Goal에서 별도 검증이 필요하다.
- 작업공간 루트에서 `git status --short`는 `fatal: not a git repository`로 실패하여 Git 기준 변경 목록은 확인하지 못했다.

## 완료 기준 충족 근거

- `G2G-004.Security.check.Tests`가 security check 정상/복수 strike/zero strike/실패 입력/결정성 검증을 포함하며 전체 통과했다.
- `SecurityResolver.cs`는 `UnityEngine`, `MonoBehaviour`, `TODO` 문자열을 포함하지 않는다.
- 원본 `DCGO/Assets/...` 파일은 수정하지 않았다.

## 다음 Goal 진행 가능 여부

- G2G-004 완료 기준을 충족했으므로 다음 Goal 진행 가능.

## 완료 판정

COMPLETE
