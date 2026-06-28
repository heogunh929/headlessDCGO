# 엔진 완성 작업 — PC 이전 핸드오프

- 작성일: 2026-06-28
- 목적: 다른 PC에서 이 작업(헤드리스 DCGO 엔진 완성)을 그대로 이어받기 위한 인수인계.
- **단일 진실 원천(상세 체크리스트)**: [engine_completion_backlog.md](engine_completion_backlog.md) — 모든 항목의 완료/잔여 상태는 이 백로그가 기준. 본 문서는 "이전 절차 + 작업 기준 + 큰 그림"만 담는다.

---

## 0. ⚠️ PC 이전 전에 반드시 (데이터 손실 방지)

1. **지금까지의 작업은 전부 git 미커밋 상태다.** 엔진 수정 17파일 + 신규 소스 6파일 + 신규 테스트 15디렉터리. 이전 전에 **반드시 커밋(또는 stash/패치)** 하거나 작업 트리 전체를 옮겨라. 안 그러면 이번 세션 작업이 사라진다.
   - 커밋 정책상 그동안 사용자가 직접 커밋해 왔으므로, **이전 직전에 한 번 더 커밋**할 것.
2. **`DCGO/` 원본 참조는 git-ignored다.** git으로 안 따라온다. 새 PC로 **수동 복사** 필요(`E:\headlessDCGO_new\DCGO\`). 이게 없으면 AS-IS 미러링/원본 대조가 불가능하다.
3. **`.dotnet/` 로컬 SDK**도 git-ignored일 수 있다. 새 PC에 없으면 동일 버전 .NET SDK를 `.dotnet/`에 두거나 시스템 dotnet을 쓰도록 경로 조정.
4. **메모리 디렉터리**는 사용자 홈 경로(`C:\Users\HG\.claude\projects\E--headlessDCGO-new\memory\`)에 있어 머신 종속이다. 새 PC에서 대응 경로로 `MEMORY.md` + 메모 파일들을 옮길 것. 핵심 메모 2개: `engine-completion-goal.md`, `asis-structure-mirror-rule.md`.

---

## 1. 환경 & 명령

- 작업 루트: `E:\headlessDCGO_new` (Windows, git repo, remote `github.com/heogunh929/headlessDCGO.git`)
- 셸: PowerShell 주력 + Bash 도구 병행
- **엔진 빌드**: `.dotnet/dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj -clp:ErrorsOnly`
- **단일 테스트 실행**: `.dotnet/dotnet run --project tests/<디렉터리>/<csproj>`
- **전체 스위트**: `bash scripts/run-tests.sh` (tests/*.Tests.csproj 자동 스캔, .sln 없음). 마지막 줄 `SUMMARY: PASS=N FAIL=0 TOTAL=N` 확인.
- 현재 상태: **전체 174/174 통과** (세션 시작 134 → 165 → … → 173 → 174).
- ✅ **충실도 마이그레이션 완료(F-6.8)**: 삭제-대체/전환 키워드 9종(Evade/Barrier/Decoy/ArmorPurge/Fragment/Scapegoat/Ascension/Save/Raid)이 자동해소(룰 변경)→**agent 선택**으로 전환됨(효과/전투/post 전 경로). `DeletionReplacementTiming`·`RaidAttackSwitch`·`AttackPhase.DeletionReplacement`. 테스트 `tests/G3.5-F68`(13)·`tests/G3.5-C3`(7). 상세: `docs/audit/{asis_fidelity_audit,f68_deletion_replacement_window_design}.md`. 상세: `docs/audit/{asis_fidelity_audit,f68_deletion_replacement_window_design}.md`.
- ImplicitUsings 켜짐, Nullable 켜짐, net8.0.

---

## 2. 작업 기준 (반드시 준수)

### 2-1. 스켈레톤 ↔ AS-IS 1:1 대응 (firm — 최우선 개발 기준)
**카드-facing 코드(스켈레톤/미러 트리)는 원본 DCGO와 1:1로 대응시킨다. 추가 비용이 들어도 유지보수를 위해 무조건 준수.**

1. **파일 1:1** — 미러의 모든 카드-facing 파일은 AS-IS 원본 파일 **정확히 하나**에 대응한다. **상대 경로·파일명을 동일하게** 둔다.
   - 예: 원본 `DCGO/Assets/Scripts/Script/SelectCardEffect.cs` → 미러 `src/HeadlessDCGO.Engine/Assets/Scripts/Script/SelectCardEffect.cs`.
   - 신규 파일을 만들기 전에 **반드시 원본에서 대응 파일을 먼저 찾는다.** 원본에 없는 새 구조를 카드-facing 트리에 임의로 만들지 않는다.
2. **심볼 1:1** — class/enum/메서드/필드명을 원본과 동일하게 둔다(예: `Mode`·`Root`·`SetUp`·`KeyWordEffects/<Keyword>.cs`·`KeywordBaseBatch2`의 `<Keyword>` kind). 의미/동작도 원본 기준(백로그 한 줄 설명과 다르면 **원본이 정답** — 원본을 읽고 정정).
3. **룰 불변(포팅 ≠ 룰 변경)** — 동작은 원본과 동일해야 한다. 자동해소/단순화로 게임 룰을 바꾸지 않는다(예: optional "you may"는 agent 선택으로 노출). 근거 문서: [asis_fidelity_audit.md](asis_fidelity_audit.md).
4. **facade + impl 패턴** — 미러 파일은 얇은 표면(원본 시그니처·구조)을 유지하고, 결정론적 .NET 구현은 `Headless/*`의 impl/헬퍼에 위임한다(예: 미러 `*HelperFactory` → `Headless` `*Helpers`). 스켈레톤은 "원본 모양", 실제 배관은 Headless.
5. **키워드/카드 추가 시 절차** — ① 원본 대응 파일 확인 → ② 미러 스켈레톤에 동일 심볼로 채움(예: `KeyWordEffects/<Keyword>.cs` 파티얼 + factory wrapper, `KeywordBaseBatch*`에 kind) → ③ 동작 로직은 Headless 게이트/헬퍼로 → ④ 테스트.

**엔진 plumbing(여러 카드가 공유하는 게임 규칙 배관)은 `Headless/{Effects,Runtime,Services,State,Bridge,Choices}/`** 에 둔다.
- 판단 기준: "이 카드 한 장의 텍스트/구조인가?" → **미러(원본 1:1)**. "여러 카드가 공유하는 엔진 메커니즘(수집/해소/게이트)인가?" → **Headless**.
- 미러 트리: `Assets/Scripts/Script/` (+ `/CardEffectCommons/`, `/CardEffectFactory/`, `/KeyWordEffects/`), `Assets/Scripts/CardEffect/`, `Assets/CardBaseEntity/`(JSON 카드데이터).
- 원본 참조: `DCGO/Assets/Scripts/...` (git-ignored, 로컬 전용 — **반드시 이 머신에 복사돼 있어야 1:1 대조 가능**).

### 2-2. 증분 리듬 (매 항목 1세트)
구현 → 단위 테스트 작성 → 단일 테스트 통과 → **전체 스위트 통과** → **백로그 문서 갱신** → 보고. 핫패스(sink/게이트/GameFlowProcessor/페이즈) 수정 시 전체 스위트 필수.

### 2-3. 테스트 컨벤션
- 위치/이름: `tests/G3.5-<ID>.<Name>.Tests/` + 동명 `.csproj` + `Program.cs`(top-level, 직접 작성한 `(name, body)` 배열 러너 + PASS/FAIL 출력 + 실패 시 `Environment.Exit(1)`). 기존 `tests/G3.5-W1b.*` 또는 `tests/G3.5-B5.*`를 템플릿으로 복사.
- csproj는 `<ProjectReference Include="..\..\src\HeadlessDCGO.Engine\HeadlessDCGO.Engine.csproj" />` 1개.

### 2-4. 커밋 정책
- **커밋/푸시는 사용자가 직접 한다.** AI는 요청 없이 커밋하지 않는다.
- `DCGO/`는 절대 커밋 금지(git-ignored, 원본 저작물).

### 2-5. 보류 규율 (중요)
- "올바른 전용 지점이 아직 없는" 기능은 **잘못 배선하지 말고 명시적으로 보류**하고 백로그에 근거를 남긴다. 현재 의도적 보류:
  - **OnStartBattle** (F-6.3): DP 비교 전 동기 해결 윈도우 필요 — 단순 emit 시 부정확.
  - **F-1.7 UntilCalculateFixedCost**: 비용 헬퍼가 read-side(legal-action 열거마다 호출) → 거기서 expire하면 매 쿼리 레지스트리 변형 버그.
  - **PlayForCost / B-8 코스트변형**: D-8 비용 파이프라인 필요.

### 2-6. 패턴 자산 (재사용)
- **선택→연산**: `EffectChoiceHelpers`(요청 생성/해소), `DeferredChoiceProvider`(suspend/resume), `ScriptedChoiceProvider`(테스트), `SelectPermanentEffect`/`SelectCardEffect`(보드/카드존 열거+Mode→뮤테이션).
- **뮤테이션 vocabulary**: `MatchStateMutationSink` (kind: AddDpModifier/Suspend/Unsuspend/SetFlag/ClearFlag/Delete/TrashCard/ReturnToHand/ReturnToDeckTop·Bottom/AddToSecurity/DrawCards/AddMemory/SetMemory/**PlayCard**). `Apply(mutation)` 후 `FlushAsync()`.
- **연속효과/게이트**: `ContinuousEffectEvaluator` + `ContinuousScopeEvaluation.EvaluateForCard`(카드타깃 ∪ player-scope) → `ContinuousDpGate`/`ContinuousModifierGate`(DP/SecAtk/Cost)/`ContinuousRestrictionGate`(cannot-*). duration은 `EffectDuration`+`EffectDurationExpiry`로 자동 만료.
- **타이밍 emit**: 존 이동은 `TriggerTimingMap.Derive`(CardMoved→타이밍 파생) 자동, 비-이동은 `TriggerEventEmitter.Emit(queue, timing, actor, subject)`. 상수는 `TriggerTimings`.
- **once-per-turn**: `OnceFlagController`(EngineContext.OnceFlags) — `CardEffectDefinition.MaxCountPerTurn` 보유 효과를 GameFlowProcessor가 자동 게이트.
- **조건/쿼리 헬퍼**: `MinMaxRequirementHelpers`(DP/Cost/Level min·max), `CardRequirementHelpers`(name/color/trait), `ZoneQueryHelpers`(존 카운트/존재), `TurnOwnershipHelpers`(턴/소유), `InheritedEffectHelpers`(계승 활성).

---

## 3. 진행 현황 (요약 — 상세는 백로그)

### A. 기반 프레임워크 — 핵심 통합 거의 완료
- F-1 EffectDuration ✅ (F-1.7만 보류)
- F-2 선택→연산 ✅ (SelectPermanent/SelectCard, Root 존추상화, Mode 매핑)
- F-3 뮤테이션 vocabulary — 코어 kind ✅, **PlayForFree ✅**; 잔여 F-3.6(Reveal)/F-3.8(Token)/PlayForCost
- F-4 once-per-turn ✅
- F-5 player-scope 연속 ✅ (F-5.3 진화요건무시만)
- F-6 타이밍 emit — 6.2/6.4/6.5/6.6(OnUseOption)/6.7 ✅, 6.3 OnEndBattle ✅; 잔여 OnStartBattle·6.8(would)·6.9(링크)
- F-7 계승효과 활성 ✅
- F-8 조건/쿼리 — 8.2/8.3/8.4 ✅, 8.1 대부분; 잔여 DigivolutionCards 메트릭·8.5 특수

### B. 공통 연산
- B-1 Delete ✅(소재스택만) · B-2 ±DP/SecAtk/Cost ✅ · B-3 바운스/덱복귀 ✅ · B-4 Suspend/Unsuspend ✅ · B-5 Draw/discard/mill ✅ · B-8 무료플레이 ✅
- 잔여: B-6(시큐리티 trash/Recovery) · B-7(reveal&select=F-3.6) · B-9(토큰) · B-10/11(소재·링크 trash)

### C. 키워드 — 실효 6종(Blocker/Jamming/Reboot/Piercing/**Rush**/**Blitz**) 외 ~18종 잔여
- C-그룹1: **C-1 Rush ✅** · **C-2 Blitz ✅**(MemoryPass 윈도우) · **C-3 Raid ✅**(공격 대상 전환=`RaidAttackSwitch`; 시큐리티 직접공격 아님 → D-3 불필요).
- C-그룹2: **4종 전부 엔진 소비 완료**(`DeletionReplacementGate`) — Evade/Barrier(would-be-deleted 코스트 생존), Decoy(적 효과 삭제 리다이렉트), Fortitude(OnDestroyed 트래시 무료 replay). grant 트리거 클래스(KeywordBaseBatch3)만 포팅 시 잔여.
- C-그룹3 완료: **C-8 Retaliation ✅**(전투 상대 삭제) · **C-9 Execute ✅**(언서스펜드 공격+종료 시 자기삭제) · **C-10 Collision ✅**(강제 블록) · **C-21 ArmorPurge ✅**(top shed→소재 승격).
- C-그룹4: 삭제-계열 **C-11 Fragment ✅ · C-17 Ascension ✅ · C-19 Scapegoat ✅**(`DeletionReplacementGate`). 나머지 7종(Iceclad/Decode/Partition/Progress/Overclock/Alliance/Vortex)은 미완 서브시스템 의존 → 보류(백로그에 사유).
- C-그룹5: **C-22 Save ✅**(삭제 후 다른 permanent에 부착) · **C-23 Material Save**/**C-24 Training** 프리미티브 제공(`DigivolutionStackHelpers`, 활성 발동은 포팅).
- 다음: C-그룹4 보류 7종의 선결 서브시스템(소재 스택 분할·효과-구동 공격·trait 조건) 또는 grant 트리거 클래스 일괄(KeywordBaseBatch3+).
- 📌 신규 허브: `DigivolutionStackHelpers`(소재 스택 add-bottom/move/train) — 소재 조작 키워드는 여기에 추가.
- 📌 `DeletionReplacementGate`가 삭제-대체 키워드 허브: Evade/Barrier/Decoy/Fortitude/ArmorPurge/Fragment/Ascension/Scapegoat (+ BattleResolver Retaliation). 신규 삭제계 키워드는 여기에 추가.
- ⚠️ 원본 `DCGO/`가 이 머신엔 **존재**(미러 가능). §0 경고는 새 PC 대비용.

### D. 대형 서브시스템 — 8종 잔여 (D-3 해소)
- D-1 Link · D-2 Appfuse · ~~D-3 Raid~~(C-3에서 공격 대상 전환 키워드로 해소, 서브시스템 아님) · D-4 De-Digivolve · D-5 DNA/DigiXros · D-6 Blast/Arts · D-7 무효화 · D-8 코스트감소 · D-9 Recovery/Token/MindLink/DelayOption

---

## 4. 권장 다음 단계
1. **수직 슬라이스**: 대표 카드 1~2장을 실제 포팅해 위 헬퍼 조합을 end-to-end 검증(템플릿 확정).
2. **C 키워드 그룹1** (Rush 소환턴 공격 완성 등) 또는 **D-1 Link**.
3. 이후 per-card 대량 포팅은 **로컬 LLM**에 인계(엔진/헬퍼는 이미 광범위하게 깔림 — 이게 이번 세션의 목적).
4. 보류 항목(OnStartBattle 동기윈도우, D-8 코스트 파이프라인)은 해당 서브시스템 착수 시 함께 배선.

---

## 5. 이번 세션 신규/수정 파일 (이전 검증용)

### 신규 소스 (엔진 plumbing)
- `Headless/Effects/EffectDuration.cs`, `EffectDurationExpiry.cs`, `OnceFlagController.cs`, `PlayerScopeContinuousHelpers.cs`
- `Headless/Runtime/ContinuousScopeEvaluation.cs`, `ContinuousModifierGate.cs`

### 신규 소스 (미러, 카드-facing)
- `Assets/Scripts/Script/SelectPermanentEffect.cs`, `SelectCardEffect.cs`
- `Assets/Scripts/Script/CardEffectCommons/InheritedEffectHelpers.cs`, `TurnOwnershipHelpers.cs`

### 주요 수정
- `Headless/Effects/TriggerTimings.cs`, `TriggerTimingMap.cs`, `MatchStateMutationSink.cs`, `EffectRegistry.cs`
- `Headless/Bridge/EngineContext.cs`
- `Headless/Runtime/`: `BattleResolver.cs`, `MetadataActionProcessor.cs`, `PassAction.cs`, `DigivolveAction.cs`, `PlayCardAction.cs`, `OptionActivateAction.cs`, `AttackPipeline.cs`, `GameFlowProcessor.cs`, `ContinuousDpGate.cs`, `ContinuousRestrictionGate.cs`

### 신규 테스트 (G3.5-): CVA2·CVA4·CVB1·F62·F64·F65·F67·F15·F4·F5·F7·F84·B2·B5 (전부 green)

> 새 PC에서 첫 작업: `bash scripts/run-tests.sh` 로 165/165 재확인 → 백로그에서 다음 미완료 항목 선택 → 2-2 리듬으로 진행.
