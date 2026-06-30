# Session Handoff — 다른 PC에서 이어서

> 이 문서 하나로 새 PC(또는 새 대화)에서 작업을 이어받을 수 있다. 저장소: **https://github.com/heogunh929/headlessDCGO** (branch `main`).
> 메모리 내용은 `docs/audit/memory_mirror.md`에 미러돼 있다(메모리 폴더는 git 밖).

## 0. 새 PC 셋업 체크리스트

공통: 1) clone, 2) `DCGO/` 복사(필수, git-ignored — AS-IS 1:1 대조 기준; C# 소스+.meta라 크로스플랫폼 복사 OK), 3) .NET 8 SDK, 4) (선택) 메모리 복원(`memory_mirror.md` 각 절을 `~/.claude/projects/<project>/memory/<name>.md`로; 또는 새 대화에서 "session_handoff.md + memory_mirror.md 읽고 이어서"), 5) 검증.

### Ubuntu / Linux (권장 경로)
```bash
# .NET 8 SDK — 둘 중 하나
sudo apt-get install -y dotnet-sdk-8.0                                   # MS 저장소 설정 시
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0  # 또는 격리 설치

git clone https://github.com/heogunh929/headlessDCGO.git && cd headlessDCGO
# DCGO/ 를 기존 PC에서 루트로 복사 (scp/USB/클라우드)
bash scripts/run-tests.sh            # SUMMARY: PASS=244 FAIL=0
dotnet run --project tools/RuleAudit # No rule-invariant violations
```
- **`.dotnet/` 복사 금지** — Windows 바이너리. `run-tests.sh`는 `.dotnet/`가 없으면 시스템 `dotnet`을 쓴다(`[ -d ".dotnet" ] && export PATH=...`). 격리하고 싶으면 `dotnet-install.sh --install-dir ./.dotnet`로 **Linux** SDK를 `.dotnet/`에 깔면 된다.
- 줄바꿈: `.gitattributes`가 `*.sh eol=lf` 강제 → 우분투에서 스크립트 정상. (Windows의 CRLF 경고는 무관)
- 명령은 시스템 `dotnet`(아래 §1의 `.dotnet/dotnet` 대신 `dotnet`).

### Windows (기존 PC)
- `.dotnet/`(Windows SDK) 복사 또는 .NET 8 설치. 명령은 `.dotnet/dotnet ...`. 빌드는 throttle됨(20-way 동시빌드가 Win32 1392 산출물 손상 유발 → run-tests.sh가 빌드/실행 분리).

⚠️ **안 넘어가는 것**: 활성 `/goal` Stop 훅, 백그라운드 작업, 이 대화 컨텍스트. 코드/문서/`.claude/commands/`는 git으로 인계됨.

## 1. 명령 (Ubuntu는 `dotnet`, Windows는 `.dotnet/dotnet`)

- 빌드: `dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj -clp:ErrorsOnly`
- 전체 테스트: `bash scripts/run-tests.sh` (2단계: 빌드 병렬 → 실행 병렬; `SUMMARY: PASS=N FAIL=0`)
- 룰 감사: `dotnet run --project tools/RuleAudit` ("No rule-invariant violations detected" + 위반 0)
- 커밋은 **사용자 지시 시에만**. 금지경로 절대 미커밋: `DCGO/` · `.dotnet/` · `**/bin/` · `**/obj/` · `.claude/settings.local.json`. 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

## 2. 표준 (작업 규율)

- **포팅 표준**: `docs/audit/card_porting_standard.md` — "원본구조 동일"(파일 위치 + 팩토리/메서드 이름·시그니처 1:1, 행동만 아님). 카드-facing은 엔티티-id 술어 관용.
- **AS-IS 미러 규칙** + **엄격 PASS**(가드 완화·추측 = FAIL, 빈도/희귀엣지는 PASS 근거 아님): `memory_mirror.md` 참조.
- **probe-first**: 새 프리미티브 전 엔진에 이미 메커니즘 있는지 확인(반복 교훈: "엔진 있음 / 등록 경로 없음").
- `/goal <ID>` 커맨드(`.claude/commands/goal.md`)로 골 실행; `/ex8-074`로 EX8-074 묶음.

## 3. 현재 상태 (이 세션 성과)

전체 **244/244 green, RuleAudit 위반 0**. HEAD `63a462d7`.

**EX8_074 하드카드 6 region 전부 LIVE 완성** (forcing function으로 포팅 표준 확립):
- #1 [BeforePayCost] 디지몬 2체 서스펜드→코스트-4 (라이브, availability/payment 분리 모델링)
- #3 [Alliance] / #4 [Vortex] (라이브 키워드)
- #5 [When Digivolving] 1체 서스펜드→상대 ≤8000 삭제(+서스펜드당 3000 동적임계) (LA-1로 라이브)
- #6 [All Turns] 1회/턴 자기 [When Digivolving] 재발동 (LA-3로 라이브)

**핵심 부수효과**: LA-1로 **모든 [When Digivolving] 카드(ST1_08 등)가 함께 라이브화**.

커밋: `3b061f61`(EX8_074 포팅+프리미티브) · `63a462d7`(LA 라이브 활성화). 이전 단계: self-static/play-cost 팩토리(`fcea38cf`), BeforePayCost Stage1-3(`dd3001c8`/`ef95d1d4`/`237466eb`/`7955d03f`).

**신규 재사용 프리미티브**: `EffectTiming.BeforePayCost`/`OnEndTurn`; `SuspendCostReductionEffect`; BeforePayCost 지불-전 윈도우 + availability 감소(PlayCardAction); `ReuseWhenDigivolvingEffect`; `OnPlayReactivation`(LA-3 윈도우); CardEffectCommons `IsBattleAreaDigimon`/`IsExistOnHand`/`MatchConditionPermanentCount`/`IsSuspended`; 동적임계 삭제 패턴. **정답 패턴(활성화 라이브화)**: 트리거 시점 `ActivatedEffectResolver` 직접 호출 윈도우(EngineContext 완비, coordinator 중첩 회피 — 스케줄러 경로는 `CardEffectResolveContext`에 ChoiceProvider 없음).

## 3-b. 후속 (LA-4 완료)

**LA-4 인터랙티브 deferred resume 완료** (미커밋, HEAD 위 working tree): `tests/G9-013.DeferredActivationResume` 2/2. 핵심 — resume 경로(`MetadataActionProcessor:604`)가 **이미 timing-agnostic**이라 production 변경 0, **검증 골**. WhenDigivolving(LA-1)·[All Turns](LA-3) 윈도우가 `deferredChoice:true`에서 processor를 직접 구동하는 ResolveChoice 2라운드로 안전하게 suspend/resume(재-digivolve/재-play 없음, commit-once: Tap 즉시·Destroy staged). 전체 **245 프로젝트 green, RuleAudit 0**.

## 4. 다음 작업 후보

- **LA-2** (자기 On-Play 활성화) — `docs/audit/live_activation_goals.md`. 선택(EX8_074 무관, 해당 카드 포팅 시 LA-1 패턴 적용).
- **추가 카드/그룹 포팅** — `CARDS-<Set>-<Color>` 단위, 표준 §2 워크플로우 + 이번에 만든 프리미티브 재사용. 다른 하드카드도 같은 방식.
- **fidelity-debt** 기준 유지(보고는 "진짜 1:1 몇 장 / 부채 몇 장"). 레저: `docs/audit/fidelity_debt.md`.

## 5. 핵심 문서

- 포팅 표준: `docs/audit/card_porting_standard.md`
- EX8-074 묶음 골(완료): `docs/audit/ex8_074_remaining_goals.md`
- LA 라이브 활성화 골: `docs/audit/live_activation_goals.md`
- 메모리 미러: `docs/audit/memory_mirror.md`
- 엔진 백로그/이전 핸드오프: `docs/audit/engine_completion_backlog.md`, `docs/audit/engine_completion_handoff.md`
