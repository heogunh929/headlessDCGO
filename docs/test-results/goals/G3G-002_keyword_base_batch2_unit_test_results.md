# G3G-002 Keyword Base Batch 2 Unit Test Results

## 실행 일시

- 2026-06-25 19:43:12 +09:00

## Goal 범위

- Goal ID: G3G-002
- 목표: Keyword base batch 2 포팅
- 작업 범위: rush blitz retaliation armor purge base 구현
- 산출물: keyword base batch2
- 완료 기준: keyword base2 테스트 통과
- 선행 Goal 확인: `G3G-001_keyword_base_batch1_unit_test_results.md`에서 COMPLETE 확인

## 수정/생성 파일

- 생성: `src/HeadlessDCGO.Engine/Headless/Effects/KeywordBaseBatch2.cs`
- 수정: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Rush.cs`
- 수정: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Blitz.cs`
- 수정: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Retaliation.cs`
- 수정: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/ArmorPurge.cs`
- 생성: `tests/G3G-002.Keyword.base.batch.2.Tests/G3G-002.Keyword.base.batch.2.Tests.csproj`
- 생성: `tests/G3G-002.Keyword.base.batch.2.Tests/Program.cs`
- 생성: `docs/test-results/goals/G3G-002_keyword_base_batch2_unit_test_results.md`

## 참조한 AS-IS 파일

- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Rush.cs`
- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Blitz.cs`
- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Retaliation.cs`
- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/ArmorPurge.cs`
- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Rush.cs`
- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Blitz.cs`
- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Retaliation.cs`
- 읽기 전용 확인: `DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/ArmorPurge.cs`
- 원본 `DCGO/Assets/...` 파일 수정 없음

## 구현 요약

- `KeywordBaseBatch2Kind`, timing/scope/context key 상수, `KeywordBaseBatch2Effect`, `KeywordBaseBatch2Factory`를 추가했다.
- Rush/Blitz/Retaliation/Armor Purge를 `IHeadlessCardEffect` 기반 public API로 생성할 수 있게 했다.
- keyword binding은 `EffectRegistry.GetKeywordEffects(...)`, role query, scope query로 조회 가능하게 했다.
- AS-IS `ArmorPurge` 명칭과 표시명 `Armor Purge`를 모두 조회할 수 있도록 alias를 등록했다.
- Rush는 battle area 대상에게 `GrantRush` mutation을 낸다.
- Blitz는 trigger reason이 맞고, 대상이 공격 가능하며, 상대 memory가 1 이상이고, 공격 중이 아닐 때 `RequestBlitzAttack` mutation을 낸다.
- Retaliation은 keyword 대상이 battle로 삭제됐고, 상대 battle 대상이 존재할 때 `DeleteRetaliationTarget` mutation을 낸다.
- Armor Purge는 field removal event에서 대상에게 진화원이 있을 때 `ApplyArmorPurge` mutation을 내며 제거될 source id를 결과 값에 기록한다.
- 기존 `src/HeadlessDCGO.Engine/Assets/.../KeyWordEffects`의 네 TODO skeleton 파일을 Headless factory facade로 교체했다.

## 테스트 명령

```powershell
.\.dotnet\dotnet.exe run --project .\tests\G3G-002.Keyword.base.batch.2.Tests\G3G-002.Keyword.base.batch.2.Tests.csproj
```

## 테스트 결과

- 전체: 10
- 통과: 10
- 실패: 0
- 스킵: 0

통과한 테스트:

- G3G-002 goal row and predecessor are satisfied
- AS-IS keyword batch 2 references are recorded
- Factory creates rush blitz retaliation armor purge effects
- Keyword batch 2 effects register deterministic keyword bindings
- Rush resolves by granting immediate attack mutation
- Blitz resolves only when trigger and attack conditions match
- Retaliation resolves from battle-deleted keyword card in trash
- Armor Purge resolves only with digivolution source
- Invalid keyword target fails without mutation
- G3G-002 source files stay inside keyword base batch 2 scope

## 실패 상세

- 최종 실행 실패 없음

## 테스트 중 관찰된 경고

- 최종 실행에는 기존 Runtime 파일의 nullable 경고가 남아 있으나, G3G-002 변경 범위 밖 파일이다.
- 이번 Goal에서 추가한 `KeywordBaseBatch2.cs` 및 네 facade 파일에서는 새 nullable/Unity/Photon 의존성 경고가 발생하지 않았다.

## 테스트하지 못한 항목과 이유

- Phase 4의 완전한 combat keyword 통합(`G4B-002`)은 이번 Goal 범위 밖이다.
- 실제 attack process/security process/field removal 상태 머신 직접 변경은 하지 않았다. 이번 Goal은 keyword base effect API와 mutation 계약 고정까지 수행했다.

## 미해결 리스크

- `GrantRush`, `RequestBlitzAttack`, `DeleteRetaliationTarget`, `ApplyArmorPurge` mutation을 실제 전투/턴/필드 제거 흐름에 연결하는 작업은 후속 combat keyword Goal에서 필요하다.
- G3G-003 이후 키워드 또는 Phase 4 combat keyword 구현은 이번 Goal 범위가 아니므로 구현하지 않았다.

## 다음 Goal 진행 가능 여부

- G3G-002 완료 기준을 충족했으므로 후속 Goal 진행 가능

## 완료 판정

- COMPLETE
