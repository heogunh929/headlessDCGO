# Memory Mirror (auto-memory → repo)

> 이 문서는 Claude Code 자동-메모리(`C:\Users\HG\.claude\projects\E--headlessDCGO-new\memory\`)의 **저장소 내 미러**다. 메모리 폴더는 git 밖(머신 로컬)이라 PC 이동 시 사라지므로, 그 내용을 여기에 보존한다. **새 PC에서**: 아래 각 절을 같은 파일명으로 메모리 폴더에 복원하면 컨텍스트가 이어진다(또는 이 문서만 읽어도 됨). 메모리가 갱신되면 이 미러도 갱신할 것.

마지막 동기화 시점 HEAD: `63a462d7`.

---

## MEMORY.md (인덱스)

```
# Memory Index

- [Engine completion goal](engine-completion-goal.md) — complete base headless engine to enable (local-LLM) card porting; enumerate missing subsystems first
- [AS-IS structure mirror rule](asis-structure-mirror-rule.md) — card-facing logic must mirror original DCGO file structure 1:1 (even at extra cost); engine plumbing stays in Headless/
- [Engine completion progress](engine-completion-progress.md) — current state + read docs/audit/engine_completion_handoff.md first (PC-move handoff)
- [Fidelity debt](fidelity-debt.md) — ST1/2/3 부채 전부 상환(G10): 35/35 진짜 1:1; 엄격 PASS 기준(빈도·추측 금지)
- [Porting standard](porting-standard.md) — 포팅 표준: 원본구조 동일(파일·팩토리 이름 1:1, 행동만 아님); docs/audit/card_porting_standard.md
```

---

## engine-completion-goal.md (type: project)

현재 목표(2026-06-27 기준): **복잡한 카드 효과를 포팅할 수 있도록 기반 헤드리스 DCGO 엔진을 완성**(Phase 4). 카드 포팅 자체는 나중에 **로컬 LLM**이 수행하므로, 지금 강모델 작업은 넓은/교차 엔진 서브시스템(소비측 + authoring 프레임워크)을 먼저 만들어, 로컬 LLM에는 좁고 기계적인 per-card 패턴 채우기만 남긴다. 갭 문서는 `docs/audit/`. 빌드는 로컬 `.dotnet`, 전체 스위트 `bash scripts/run-tests.sh`. 커밋은 사용자 지시 시.

## asis-structure-mirror-rule.md (type: feedback)

헤드리스 포팅은 **카드-facing 로직에 대해 원본 DCGO(AS-IS)와 동일한 파일/디렉토리 구조를 유지**한다(clean re-arch보다 일이 많아도). 1:1 기준: (1) 파일 1:1(미러 카드-facing 파일 = AS-IS 원본 정확히 하나, 새 파일 전에 원본 대응 먼저 찾기), (2) 심볼 1:1(class/enum/메서드/필드명 동일; 의미 충돌 시 원본이 정답), (3) 룰 불변(동작 동일, 자동해소/단순화로 룰 변경 금지, optional은 agent 선택), (4) 키워드/카드 추가 절차(원본 대응 파일 → 미러 스켈레톤 동일 심볼 → 동작은 Headless 게이트/헬퍼 위임 → 테스트). 엔진-invented 배관(EffectScheduler, 게이트, MatchStateMutationSink, RL 등 원본 대응 없음)은 `Headless/`에 둔다. 파일맵: docs/audit/engine_completion_file_map.md.

## engine-completion-progress.md (type: project)

헤드리스 엔진 완성 + 인계. **먼저 읽을 문서**: `docs/audit/engine_completion_handoff.md`. 항목별 단일 진실: `docs/audit/engine_completion_backlog.md`. A·B·C·D 엔진 골격 코어 완료 + Phase 1 카드 포팅 레시피 확립(`Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs`). ST7_10 + ST1 12장 포팅, 효과 서브시스템(연속[상속/조건/동적/광역/존]·트리거-메모리·활성선택+삭제·지속버프). G6 라이브통합(자동등록 `CardEffectDispatch`/`CardEffectRegistrar`, `ActivatedEffectResolver`, 카드데이터 로더 cards.json, SpecialPlayAction, emit-only 타이밍) 완료. G7 통합정밀화 7건·G8 대량포팅 선결 8건 완료. G9-001 effectClass 별칭 dispatch. CARDS-ST2-Blue·CARDS-ST3-Yellow 포팅. ⚠️ "테스트 green ≠ 1:1" — fidelity-debt 참조. run-tests.sh 2단계(빌드 6병렬/실행 --no-build 20병렬, Win FS 손상 회피). 그룹기준 `CARDS-<Set>-<Color>`. **새 PC 첫 작업**: handoff 0절(clone/pull·`DCGO/` 수동복사(git-ignored)·`.dotnet/`·memory) 후 `bash scripts/run-tests.sh` 재확인.

## fidelity-debt.md (type: project)

ST1/Red·ST2/Blue·ST3/Yellow + ST7_10 = 35장. 최초 감사: 진짜 1:1은 24/35(69%)뿐("테스트 green=완료" 부풀림). **G10(7 goal)으로 부채 11장 전부 상환 → 35/35 진짜 1:1.** 복원: G10-001 once-per-turn(`maxCountPerTurn`+hash→OnceFlagController), G10-002 0DP격파(`IsDPZeroDelete`/`CanTriggerOnPermanentDeleted`), G10-003 테이머[Security]플레이(`PlayThisCardToBattleEffect`), G10-004 protected-source, G10-005 동적삭제임계(`MaxDpDeleteThreshold`), G10-006 배틀페어링(`CurrentBattleOpponent`), G10-007 play-from-under. **엄격 PASS 기준(사용자 확정)**: 원본 가드를 뺐으면 발동빈도·중복성·"희귀 엣지"는 PASS 근거 아님 = FAIL; 동일 동작을 엔진이 다른 방식으로 강제함을 **코드로 검증**한 경우만 PASS(추측 금지). "활성효과 실루프 자동발동 안 됨"은 엔진 통합 갭(별도 트랙) — **단, LA-1/LA-3로 [When Digivolving]/[All Turns]는 라이브화됨(아래 porting-standard 참조)**. 전체 레저: docs/audit/fidelity_debt.md.

## porting-standard.md (type: project) — 최신

지배 원칙(사용자 지시): **"원본구조랑 같게하는게 중요하다."** 행동만이 아니라 원본 `Script/`의 구조(파일 위치·팩토리/메서드 이름·시그니처·논리 분해)까지 1:1. 전체 표준: `docs/audit/card_porting_standard.md`(두 레이어 모델, PASS 기준, §2 워크플로우, 함정).

판별식: 원본 `CardEffectFactory.<X>`가 있는데 헤드리스에 같은 이름 없으면 = 빠진 프리미티브. → 이름·시그니처 그대로 미러 → 실 카드 경로로 라이브 검증. **핵심 교훈**: 새 프리미티브 전 엔진에 이미 있는지 probe(엔진 있음/등록 경로 없음 패턴).

**완료(커밋·푸시):** self-static 키워드 팩토리 4종(`fcea38cf`), play-cost 팩토리 2종(`fcea38cf`), EX8_074 BeforePayCost Stage1-3(`dd3001c8`/`ef95d1d4`/`237466eb`/`7955d03f`), **EX8-074 묶음(포팅+프리미티브) `3b061f61`**, **LA 라이브 활성화 `63a462d7`**. HEAD=`63a462d7`, 244/244 green, RuleAudit 0.

**EX8_074 6 region 전부 LIVE**: #1 BeforePayCost·#3 Alliance·#4 Vortex + #5 WhenDigivolving(LA-1)·#6 [All Turns](LA-3).

**확립 사실:** 활성화 효과 라이브 구동의 정답 패턴 = **트리거 시점 `ActivatedEffectResolver` 직접 호출 라이브 윈도우**(EngineContext 완비, coordinator 중첩 회피). 스케줄러 경로는 `CardEffectResolveContext`에 EngineContext/ChoiceProvider 없어 choice 못 구동.

**신규 재사용 프리미티브**: `EffectTiming.BeforePayCost`/`OnEndTurn`, `SuspendCostReductionEffect`, BeforePayCost 윈도우+availability, `ReuseWhenDigivolvingEffect`, `IsBattleAreaDigimon`/`IsExistOnHand`/`MatchConditionPermanentCount`/`IsSuspended`, 동적임계 삭제, `OnPlayReactivation`. 커맨드: `/goal`·`/ex8-074`(`.claude/commands/`). 픽스처: `TestFixtures/Tfx*.cs`(클래스명=카드번호, 리플렉션 dispatch).

**미진행(선택):** LA-2(자기 On-Play 활성화), LA-4(인터랙티브 deferred resume across 새 윈도우). 둘 다 `docs/audit/live_activation_goals.md`.
