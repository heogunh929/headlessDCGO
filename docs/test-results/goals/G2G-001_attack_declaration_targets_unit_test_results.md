# G2G-001 Attack declaration target 포팅 테스트 결과

## 실행 일시

- 2026-06-25 15:35:27 +09:00

## 수정/생성 파일

- 수정: `src/HeadlessDCGO.Engine/Headless/Runtime/AttackPermanentAction.cs`
- 생성: `tests/G2G-001.Attack.declaration.target.Tests/G2G-001.Attack.declaration.target.Tests.csproj`
- 생성: `tests/G2G-001.Attack.declaration.target.Tests/Program.cs`
- 생성: `docs/test-results/goals/G2G-001_attack_declaration_targets_unit_test_results.md`

## 읽기 전용으로 확인한 AS-IS 파일

- `DCGO/Assets/Scripts/Script/SelectAttackEffect.cs`
- `DCGO/Assets/Scripts/Script/AttackProcess.cs`
- `DCGO/Assets/Scripts/Script/Permanent.cs`

## 구현 범위

- G2G-001 범위인 `공격 선언과 target 후보 생성`만 구현했다.
- `AttackPermanentAction.GetAttackDeclarations` API를 추가해 attacker별 `AttackDeclaration`과 `AttackTargetCandidate` 목록을 생성한다.
- target 후보는 직접 공격 대상인 상대 player와 공격 가능한 상대 battle area Digimon으로 분리했다.
- 기존 `GetLegalActions`는 새 후보 모델에서 `DeclareAttack` 액션을 생성하도록 연결했다.
- block timing, DP 비교, deletion, security check, end-attack trigger 처리는 G2G-001 범위 밖이라 구현하지 않았다.
- 원본 `DCGO/Assets/...` 파일은 읽기만 했고 수정하지 않았다.

## 테스트 명령

- `.\.dotnet\dotnet.exe run --project .\tests\G2G-001.Attack.declaration.target.Tests\G2G-001.Attack.declaration.target.Tests.csproj`
- `.\.dotnet\dotnet.exe build .\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj`
- `.\.dotnet\dotnet.exe run --project .\tests\G2E-004.Attack.action.Tests\G2E-004.Attack.action.Tests.csproj`

## 전체/통과/실패/스킵 수

| 범위 | 전체 | 통과 | 실패 | 스킵 |
|---|---:|---:|---:|---:|
| G2G-001 전용 테스트 | 10 | 10 | 0 | 0 |
| HeadlessDCGO.Engine 빌드 | 1 | 1 | 0 | 0 |
| G2E-004 회귀 테스트 | 10 | 10 | 0 | 0 |
| 합계 | 21 | 21 | 0 | 0 |

## 실패 상세

- 최종 실행 기준 실패 없음.
- 구현 중 새 record 선언을 compact constructor 형태로 작성해 1차 빌드에서 컴파일 오류가 발생했다.
- 같은 Goal 범위 안에서 명시적 생성자/프로퍼티 형태로 수정했고, 이후 빌드와 테스트가 모두 통과했다.

## 테스트하지 못한 항목과 이유

- block timing, DP battle resolver, security check, end-attack trigger는 상세 지시서에 언급된 후속 공격 처리 흐름이지만 G2G-001 산출물인 `attack declaration`과 CSV 단위테스트 범위 `target candidate 테스트`를 넘어가므로 이번 Goal에서 테스트하지 않았다.

## 미해결 리스크

- 공격 후보 생성은 기존 Headless metadata(`isSuspended`, `enteredThisTurn`, `hasRush`, `cannotAttackPlayer`, `canAttackUnsuspendedDigimon`)를 기준으로 한다.
- 실제 카드 효과로 공격 가능 대상이 동적으로 바뀌는 처리는 이후 effect/rule 포팅 Goal에서 확장되어야 한다.
- 작업 디렉터리에서 `git status --short`는 `fatal: not a git repository`로 실행되지 않았다.

## 완료 판정

- COMPLETE
- 완료 기준 `공격 선언 테스트 통과` 충족.
- 다음 Goal 진행 가능.
