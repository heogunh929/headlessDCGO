# C5 "빈-union 은퇴" 재검증 — 죽은 소비 경로 전수

날짜: 2026-07-24
대상: `ContinuousScopeEvaluation.ApplicableEffects`(Headless/Runtime/ContinuousScopeEvaluation.cs:49-64) 및 C5-1에서
삭제된 것(ContinuousEffectEvaluator/ModifierHelpers 구 경로/게이트 축소)이 남긴 잔존 배선.

## 0. 실독 확인

`ApplicableEffects`(:49-64)는 인자를 전부 버리고(`_ = cardId; _ = digivolveTargetPermanentId;`)
무조건 `Array.Empty<EffectRequest>()`를 반환한다 — 조건분기 없음. 확인됨.

`EvaluateForCard`/`ResolveCard`(구 C5-1 삭제 대상)는 소스에 존재하지 않음(grep 0) — 완전 삭제 확인.

## 1. `ApplicableEffects` 실제 호출부 전수 (grep, 주석 제외)

호출은 정확히 4곳뿐이다:

| # | 호출부 | 상위 소비자 | union 파트너 | 판정 |
|---|---|---|---|---|
| 1 | `DigivolveAction.cs:748` (`HasContinuousFlag`) | `CanIgnoreDigivolutionRequirement`(:701-702) | **없음** — 이 헬퍼가 유일한 판정원 | **죽은 경로 (결함)** |
| 2 | `DigivolveAction.cs:748` (`HasContinuousFlag`, 동일 호출) | `CanIgnoreColorRequirement`(:704-706) | 있음 — `NewModelIgnoreColorActive`(AS-IS `CardSource.IgnoreColorConditionActive` 라이브 스캔)와 `\|\|` union | 정상 (빈-스텁은 잉여) |
| 3 | `DigivolveAction.cs:791` (`TryGetAddedDigivolutionCost`) | 추가-진화조건 비용 판정 | 있음 — `NewModelAddedDigivolutionCosts`(AS-IS `CardSource.AddedDigivolutionCosts`/`CostList` 라이브 스캔)와 union(:812-821) | 정상 |
| 4 | `DigivolveAction.cs:856` (`MatchesAddedDigivolutionRequirement`) | 추가-진화조건 매치 판정 | 있음 — 동일 `NewModelAddedDigivolutionCosts` union(:864-866) | 정상 |
| 5 | `MatchStateMutationSink.cs:1763` (`ScopedEffects`) | `HasValueFlag`→`HasSelfFlag`(:1954) | N/A — **`HasSelfFlag` 자체가 호출부 0**(전 소스+테스트 grep 0건); 실행되지 않는 사문(死文) | 미실행 사문(잠재 결함 아님, 하단 §3 참고) |
| 5b | `MatchStateMutationSink.cs:1763` (`ScopedEffects`) | `IsRestrictedFromCause`의 registry-only fallback(:1901-1913, `_context is null`일 때만 도달) | 없음(그 분기 자체가) — 단 프로덕션 싱크는 항상 `_context`를 주입(코드 주석 확정, 생성자 기본 `context=null`은 "bare unit test"용) | 구조적으로는 죽은 분기이나 **프로덕션 도달 불가**(테스트 전용 컨텍스트-리스 싱크만 진입) — §3 참고 |

## 2. 판정 상세

### 2-1. 죽은 경로 (결함) — `DigivolveAction.CanIgnoreDigivolutionRequirement`

- **이미 알려진 사례**(과제 전제와 동일 건). `IgnoreDigivolutionRequirementKey`("ignoreDigivolutionRequirement")를
  오직 `HasContinuousFlag`→`ApplicableEffects`(항상 빈 배열)로만 읽는다. union 파트너 없음.
- 소비 지점 2곳, 둘 다 영향:
  - `Validate()`:488 — 인쇄된 진화조건이 불일치할 때 "무시" 관용을 여는 조건의 절반.
  - `TryGetDeclaredEvolutionCost()`:646 — 같은 무시-관용으로 비용 재해석을 여는 조건.
- **AS-IS 프로듀서**: `GiveEffect/GiveEffectToPlayer/IgnoreDigivolutionRequirement.cs`의
  `GainIgnoreDigivolutionRequirementPlayerEffect`(AS-IS 1:1, `AddDigivolutionRequirementStaticEffect`를
  `ignoreDigivolutionRequirement:true`로 구성해 플레이어 버킷에 등록) — 그러나 이 팩토리 자체가 헤들리스에서
  **호출부 0**("Latent (0 callers)"라고 파일 자체가 명시)이라 오늘 당장 카드가 이 일반-무시 그랜트를 실제로
  발동시키는 경로는 없다. 대조로, 목격 카드 BT13_028("IgnoreDigivolutionRequirement witness")은 이 일반 플래그가
  아니라 **별개의 "added-requirement" 경로**(자신만의 `AddDigivolutionRequirementClass`를
  `UntilCalculateFixedCostEffect` 버킷에 직접 등록, `TryGetAddedDigivolutionCost`의 union 쪽에서 라이브로 소비됨,
  #3 정상 항목)로 우회하고 있어 무증상이다.
- **결론**: 구조적으로 영구 무효(카드가 `GainIgnoreDigivolutionRequirementPlayerEffect` 패턴 — 대상 한정 없는
  일반 "진화조건 전체 무시" 그랜트 — 을 통해 포팅되는 순간, `Validate`/`TryGetDeclaredEvolutionCost`는 그 그랜트를
  절대 보지 못한다). 오늘은 그 팩토리에 실 호출자가 없어 무증상이지만, 배선 자체는 죽어 있다 — latent 결함.

### 2-2. 정상 (union 존재) — CanIgnoreColorRequirement / TryGetAddedDigivolutionCost / MatchesAddedDigivolutionRequirement

세 곳 모두 `ApplicableEffects`(항상 빈 루프, 무해)에 이어 AS-IS 라이브 인터페이스 스캔(`NewModelIgnoreColorActive`
/ `NewModelAddedDigivolutionCosts`)과 `||`/카운트 union으로 실제 판정을 낸다. 빈-스텁 쪽 루프 안에서만 참조되는
`ConditionKey`(`AddedConditionActive`/`AddedPredicateActive`, :887·950)도 같은 이유로 무해 — 그 루프 자체가 절대
실행되지 않으므로 도달하지 않는다.

### 2-3. `BattleDeletionGate` / `ContinuousRestrictionGate` / `LinkHelpers` / `OptionColorRequirement`

주석에 `ContinuousScopeEvaluation`/`ApplicableEffects` 언급이 남아 있으나, 실제 호출은 전부 제거되고
`NewModelContinuousScan.*`(`HasCanNotBeDestroyed`, `IsRestrictedNewModel`, `FoldLinkedMax`, `FoldLinkCost`,
`IgnoreColorConditionActive` 등) 라이브 스캔으로 완전 대체됨(§1 목록에 호출부로 잡히지 않음 — 실제 grep으로
확인). C5-1에서 이미 완결된 정상 이관. 결함 아님.

## 3. C5 삭제가 남긴 그 외의 죽은 배선 (호출부 0 — 순수 사문)

`ApplicableEffects`의 빈 반환을 "소비"하는 건 아니지만, 같은 C5-1/이후 리하우징이 만든 **부착점 없는 고아
스캐폴드**다. 게임 판정에 아직 아무 영향을 주지 않는다(호출부가 아예 없어 결코 실행되지 않음)는 점에서
"죽은 소비 경로"와는 다른 범주지만, §5 절차 5(다른 죽은 배선 확인)에 해당하여 기록한다.

| 심볼 | 위치 | 상태 |
|---|---|---|
| `ContinuousFieldMembership.GranterMembershipHolds` | Headless/Runtime/ContinuousFieldMembership.cs:34 | 전 소스+테스트 호출부 0. 클래스 XML 주석은 `CanNotPlayOptionScan`(CardSource.CanNotPlayThisOption)과 `CardSource.CanNotTrashFromDigivolutionCards`가 이걸 쓴다고 주장하지만, 두 실제 구현(`CanNotPlayOptionScan.CanNotPlay`:49-107, `CardSource.CanNotTrashFromDigivolutionCards`:1795-1840)을 직접 읽어보면 **자체 인라인 필드-멤버십 스캔**을 쓰고 이 헬퍼를 호출하지 않는다 — 주석이 실제와 어긋남(stale). |
| `MatchStateMutationSink.HasSelfFlag` | Headless/Effects/MatchStateMutationSink.cs:1954 | `private bool HasSelfFlag(...) => HasValueFlag(...)` — 전 소스+테스트 호출부 0. |
| `CardSource.EffectConditionPasses` | Assets/Scripts/Script/CardSource.cs:960-963 | `internal static` — 전 소스+테스트 호출부 0. |
| `EffectQueryContext`(record) + `.Matches()` | Headless/Services/IEffectQueryService.cs:10-75 | 생성자 호출부 0(전 소스 grep). 같은 파일 헤더 주석은 "the continuous-scope query key still used by ContinuousScopeEvaluation"라 주장하나 `ContinuousScopeEvaluation.cs` 전문을 읽어도 `EffectQueryContext`를 참조하지 않음 — 주석이 실제와 어긋남(stale, false). |
| `ContinuousScopeEvaluation.DynamicValueKey` / `DynamicMetricKey` | ContinuousScopeEvaluation.cs:31,34 | 전 소스 grep: 자기 선언 외 참조 0 — 읽는 곳도 쓰는 곳도 없음. |
| `ContinuousScopeEvaluation.InheritedEffectKey` | ContinuousScopeEvaluation.cs:23 | 유일한 리더가 `GranterMembershipHolds`(위 항목, 호출부 0) — 쓰는 곳은 전 소스에 0. 리더-라이터 둘 다 죽음. |
| `ContinuousScopeEvaluation.ConditionKey` | ContinuousScopeEvaluation.cs:27 | 리더는 있음(`DigivolveAction`:887·950, `CardSource.EffectConditionPasses`:961, `CanNotPlayOptionScan` 주석) — 그러나 **전 소스 grep으로 라이터(`... ConditionKey] = ...`) 0건**: 아무도 이 키에 값을 쓰지 않는다. 리더 쪽 도달 여부는 §1/§2-2 참고(대부분 이미 죽은 루프 안). |

부수 발견(기능에는 영향 없는 문서 정합성 문제, 참고용): `ContinuousModifierGate.cs`의 클래스 XML 주석은 "Security
Attack continuous modifiers still fold through the registry-sourced ContinuousScopeEvaluation path"라고 하지만
그런 코드는 파일 본문에 존재하지 않는다(`ResolvePlayCost`/`ResolveDigivolutionCost`는 순수 `GetPayingCostWithBaseCost`
위임). `CardEffectCommons.cs:3407-3410`의 `RegisterDigivolutionCostDeltaForPlayer` 요약 주석도 "ContinuousModifierGate.ResolveDigivolutionCost
-> ContinuousScopeEvaluation"이라 쓰지만 같은 함수 본문의 최신 인라인 주석(R3-W3b, :3428-3435)은 실제 경로가
`NewModelContinuousScan.FoldPlayCost` union임을 명시 — 둘 다 사문화된 낡은 설명일 뿐, 코드 자체는 라이브 경로로
이미 옮겨져 있다(결함 아님, §2-3과 동일 부류).

## 4. 요약

- **죽은 소비 경로(결함)**: 1건 — `DigivolveAction.CanIgnoreDigivolutionRequirement`(printed-condition 일반
  진화조건-전체-무시 그랜트). AS-IS 프로듀서 존재(`GainIgnoreDigivolutionRequirementPlayerEffect`)하나 그 프로듀서
  자체가 헤들리스에 호출부 0 → 오늘은 무증상, **latent 결함**(그 프로듀서가 어느 카드에든 배선되는 순간 발현).
  (과제 전제에서 이미 지목된 건과 동일.)
- **정상(union 존재, 빈-스텁은 잉여)**: 3건 — `CanIgnoreColorRequirement`, `TryGetAddedDigivolutionCost`,
  `MatchesAddedDigivolutionRequirement`(모두 `DigivolveAction.cs`).
- **완전 이관 완료(호출 자체가 이미 사라짐)**: `ContinuousRestrictionGate`/`BattleDeletionGate`/`LinkHelpers`/
  `OptionColorRequirement` — 결함 아님.
- **C5 잔존 사문(호출부 0, 다른 범주)**: 5건 — `ContinuousFieldMembership.GranterMembershipHolds`,
  `MatchStateMutationSink.HasSelfFlag`, `CardSource.EffectConditionPasses`, `EffectQueryContext`(+`.Matches`),
  `ContinuousScopeEvaluation.DynamicValueKey`/`DynamicMetricKey`/`InheritedEffectKey`(라이터 0). 이들은 아직 아무
  것도 호출하지 않으므로 오늘 게임 판정을 그르치진 않지만, 부착점 없는 고아 코드 + 실제와 어긋난 클래스/헤더
  주석(사용 중이라고 주장)이 남아 있어 향후 오인·재사용 위험이 있다.
- **문서 정합성만 어긋난 곳(코드는 이미 라이브)**: `ContinuousModifierGate.cs` 클래스 주석,
  `CardEffectCommons.cs:3407-3410` 요약 주석 — 기능 결함 아님, stale 문서.

누락 없음: `ApplicableEffects`/`EvaluateForCard`/`ContinuousScopeEvaluation` 전 참조(호출·상수·주석)를 grep
전수 대조했으며, 실제 호출부는 §1의 4곳(5개 소비자)이 전부다.
