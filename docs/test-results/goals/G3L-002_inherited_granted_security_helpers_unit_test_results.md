# G3L-002 Inherited Granted Security Helper Unit Test Results

## 실행 일시

- 2026-06-25 20:44:18 +09:00

## Goal

- Goal ID: G3L-002
- 목표: Inherited granted security helper 포팅
- 작업 범위: inherited granted security effect helper 포팅
- 완료 기준: inherited 헬퍼 테스트 통과

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/InheritedGrantedSecurityHelpers.cs`
- 생성: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/InheritedGrantedSecurityHelpers.cs`
- 생성: `tests/G3L-002.Inherited.granted.security.helper.Tests/G3L-002.Inherited.granted.security.helper.Tests.csproj`
- 생성: `tests/G3L-002.Inherited.granted.security.helper.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3L-002_inherited_granted_security_helpers_unit_test_results.md`

## 참조한 AS-IS 원본

- 읽기 전용 참조: `DCGO/Assets/Scripts/Script/CEntity_EffectController.cs`
- 읽기 전용 참조: `DCGO/Assets/Scripts/Script/ICardEffect.cs`
- 읽기 전용 참조: `DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/SecurityEffect.cs`
- 읽기 전용 참조: `DCGO/Assets/Scripts/Script/AttackProcess.cs`

원본 `DCGO/Assets/...` 파일은 수정하지 않았다.

## 테스트 의도

- G3L-001 선행 Goal 완료 증빙이 존재하고 COMPLETE인지 검증한다.
- inherited/granted/security 효과 출처가 `EffectBinding` metadata에 고정되는지 검증한다.
- inherited 효과는 host entity, granted 효과는 target entity, security 효과는 owner 정보를 보존하는지 검증한다.
- `IEffectQueryService` 기반 조회가 role/scope/source kind 기준으로 필터링되는지 검증한다.
- 조회 결과가 effect id 기준으로 결정적인 순서를 유지하는지 검증한다.
- 잘못된 입력은 예외 대신 명시적인 실패 결과로 반환되는지 검증한다.
- 기존 bool metadata(`isInherited`, `isGranted`, `isSecurity`)도 source kind 판별에 사용할 수 있는지 검증한다.
- Assets facade가 Headless helper에 위임하며 G3L 범위 밖 파일을 수정하지 않았는지 검증한다.

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G3L-002.Inherited.granted.security.helper.Tests\G3L-002.Inherited.granted.security.helper.Tests.csproj
```

## 테스트 결과

- 전체: 10
- 통과: 10
- 실패: 0
- 스킵: 0

통과 테스트:

- PASS G3L-002 goal row and predecessor are satisfied
- PASS AS-IS inherited granted security references are recorded
- PASS Inherited binding records source kind and host
- PASS Granted binding records target and query metadata
- PASS Security binding records security owner
- PASS Query filters inherited granted and security effects by role and scope
- PASS Query returns deterministic ordered effect ids
- PASS Invalid query input returns explicit failure
- PASS Source kind detection falls back to legacy boolean flags
- PASS Assets facade delegates and source files stay inside G3L scope

## 실패 상세

- 없음

## 빌드/테스트 경고

- 테스트 실행 중 기존 런타임 파일의 nullable 관련 컴파일 경고가 출력되었으나, G3L-002 테스트 실패는 없었다.
- G3L-002 범위 밖 경고 수정은 수행하지 않았다.

## 미해결 리스크

- 실제 카드별 inherited/granted/security 효과 포팅은 후속 Goal 범위이며 이번 Goal에서 구현하지 않았다.
- 이번 Goal은 helper 계약과 query/filter 동작을 고정한다. 실제 전투/시큐리티 처리 루프에 연결하는 작업은 별도 Goal에서 수행해야 한다.

## 완료 판정

- COMPLETE
