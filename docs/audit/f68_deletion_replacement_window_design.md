# F-6.8 — 재진입 삭제-대체 윈도우 (WhenPermanentWouldBeDeleted) 설계

- 작성일: 2026-06-28
- 목적: 삭제가 확정되기 직전 **agent 선택을 받는 재진입 윈도우**를 열어, optional 삭제-대체 키워드를 **룰-충실**하게 만든다. 자동해소(룰 변경) 제거.
- 짝 문서: [asis_fidelity_audit.md](asis_fidelity_audit.md)
- 대상 키워드(9): Evade·Barrier·Decoy·ArmorPurge·Fragment·Scapegoat (would-be-deleted 대체) / Ascension·Save (post-deletion optional) / Raid (OnUseAttack 대상전환, 별 윈도우).
- 상태: **✅ 완료** — 9개 키워드 전부 agent 선택. 자동해소(룰 변경) 제거. `DeletionReplacementTiming`(pre+post 윈도우), `RaidAttackSwitch`(공격 전환 선택), `ChoiceType.DeletionReplacement`/`AttackTarget`, `AttackPhase.DeletionReplacement`(전투 분할), GameFlowProcessor 윈도우-오픈+스윕 가드, MetadataActionProcessor 라우팅.
  - ✅ 증분 1: **Evade**(pre, 효과경로 지연화).
  - ✅ 증분 2a: **Ascension**(post-deletion, 전투+효과 — post 윈도우는 trash 도착 후 처리라 양 경로 동시 커버, AttackPhase 불요).
  - ✅ 증분 2b: **Scapegoat**(pre, **2단계 sub-선택** 확립: step1 키워드 활성→`pendingReplacementOption`→step2 대상 선택).
  - ✅ 증분 2c: **Fragment**(pre, 2단계; cost N은 단일선택 N회 반복=`fragmentRemaining`).
  - ✅ 증분 2d: **Decoy**(pre, enemy-gated 마커 + 2단계) · **ArmorPurge**(post, no-sub).
  - ✅ 증분 2e: **Save**(post, 2단계 — sub-선택 스캔이 trash 카드 포함).
  - ✅ 증분 3: **전투 PRE경로**(Barrier·Evade·Fragment·Scapegoat in battle) — BattleResolver 분할(Part A 지연/park · Part B 윈도우 후 확정), `AttackPhase.DeletionReplacement`, Piercing/OnEndBattle를 윈도우 이후로 재배치. 옵션 없는 일반 전투는 동기 유지.
  - ✅ 증분 4: **Raid**(OnUseAttack 선택화 — `RaidAttackSwitch` 자동→선택; max-DP 후보 중 택1 or 스킵).
  - ✅ 증분 5: **Retaliation 윈도우 재오픈**(구 LIMITATION #1 해소) — BattleResolver를 반복 라운드(`ResolveRoundAsync`)로 통합: retaliation은 holder 사망 **확정 후**(IsConfirmedDoomed) 발동하고 상대를 `pendingDeletion`으로 플래그해 **would-be-deleted 윈도우**로 보냄(상대가 Evade/Barrier 가능). `retaliationFired` 마커로 1회 보장, 수렴.
  - ✅ 증분 6: **end-attack optional 트리거 선택화**(구 LIMITATION #6 해소) — `EndAttackTriggerHook`이 수집된 트리거를 바인딩된 effect의 `Definition.IsOptional`로 **재분류**(`Reclassify`, GameFlowProcessor.ReclassifyKind 미러)해 "you may" end-attack 효과를 Optional로 만들고, `AttackPipeline.AdvanceEndAttackAsync`가 이를 `OptionalPromptQueue`(턴플레이어 우선)로 라우팅 → 자동해소 대신 agent 결정. registry 없는 호출/바디 없는 바인딩은 수집 kind 유지(무변경 — G2G-005 보존). 테스트 `tests/G3.5-F68B`(4/4).
  - ✅ 증분 7: **카드별 후보 조건 훅 선행 배선**(구 LIMITATION #3 해소) — AS-IS 키워드 효과의 `Func<Permanent,bool> permanentCondition`(카드별 색/레벨/타머 등; null=제네릭)을 헤드리스로 미러. Gate 후보 열거자(`FindScapegoatSacrificeCandidates`/`FindDecoyRedirectCandidates` + 단일-pick 변형)에 optional `Func<CardInstanceRecord,bool>`(null=제네릭, 무변경) 추가, `SaveTargets`도 동일. `IDeletionReplacementCandidateConditions` 리졸버(기본 `NoDeletionReplacementCandidateConditions`=null, `Delegate…` 편의 구현)를 `EngineContext` 서비스로 조회 → `DeletionReplacementTiming`이 context-인식 `PreOptions`/`GetTargets`에서 카드별 조건 적용(옵션 제공·sub-선택 모두). 정적 `PreOptions`(sink defer)는 제네릭 유지(안전 상위집합 — sweep가 마무리). **현재 조건 쓰는 카드 없음 → 기본 리졸버로 거동 동일**; 카드 포팅 시 리졸버 등록만으로 주입(엔진 리팩터 불요). 테스트 `tests/G3.5-F68D`(8/8).
  - **완료**: 9개 키워드 전부 agent 선택 + end-attack optional 선택화 + 카드별 후보 조건 선행 훅. 테스트 `tests/G3.5-F68`(14/14)·`tests/G3.5-F68B`(4/4)·`tests/G3.5-F68D`(8/8)·`tests/G3.5-C3`(7/7).
  - 잔여 LIMITATION(경미): Fragment cost>1은 단일선택 N회(결과 동일); grant 트리거 클래스는 포팅 단계; 동시 다중삭제 Decoy 모델 차이(광역삭제 카드 포팅 시 재검토).

---

## 1. 원칙 & 핵심 아이디어
- **포팅 = 룰 불변**: optional은 agent가 발동/스킵 결정, sub-selection도 agent 선택.
- **삭제를 즉시 적용하지 말고 "지연(deferred)"**: 삭제 후보를 `pendingDeletion` 플래그만 세우고, 윈도우에서 대체-키워드 선택을 받은 뒤, **살아남지 못한 카드만** state-based 스윕으로 trash.

## 2. 재사용 자산 (이미 존재 — 신규 최소화)
- `GameFlowProcessor.PendingDeletionKey` + `RuleProcessAsync`: 플래그된 field 카드를 trash로 스윕(step1). → **지연 삭제의 실행기**.
- `BlockTiming` 패턴: 핫패스에서 `ChoiceController.RequestChoice`로 선택 열고 `AttackPhase`에 park → `ResolveChoice`로 재개. → **삭제-대체 선택의 템플릿**.
- `ChoiceController` / `DeferredChoiceProvider`(suspend/resume) / `OptionalPromptQueue`: 선택·sub-선택 인프라.
- 기존 `DeletionReplacementGate`의 **코스트 로직**(서스펜드/시큐리티trash/소재trash/희생/승격) → 폐기하지 않고 **"선택 후 적용" 본체로 재사용**.

## 3. 삭제 종류 3분류 (윈도우 시점이 다름)
| 분류 | 키워드 | 시점 | 효과 |
|------|--------|------|------|
| **A. would-be-deleted 대체** | Evade·Barrier·Decoy·ArmorPurge·Fragment·Scapegoat | 삭제 **직전** | 코스트 지불→`pendingDeletion` 해제(생존)/리다이렉트 |
| **B. post-deletion optional** | Ascension·Save | 삭제 **후**(trash 도착) | 카드를 시큐리티/스택으로(옵션) |
| **C. 공격 대상전환** | Raid | OnUseAttack(블록 전) | 대상 전환(별 윈도우, 기존 RaidAttackSwitch를 선택화) |
| (참고) mandatory | Fortitude·Retaliation | 자동 OK | 변경 불필요 |

## 4. 지연-삭제 모델 (공통)
1. 삭제 경로가 **즉시 trash 이동 대신** 후보에 `pendingDeletion=true` + `WhenPermanentWouldBeDeleted`(카드 스코프) emit.
2. 윈도우: 후보 중 **대체-키워드 보유분**에 대해 agent 선택을 연다(분류 A). 선택지 = {각 대체 키워드 활성 / 스킵} (+ sub-target).
3. **활성** 시: 해당 코스트 로직 적용(기존 gate 로직) + `pendingDeletion` 해제(생존) 또는 리다이렉트(대상 카드에 `pendingDeletion` 세움).
4. **스킵** 시: 플래그 유지.
5. `RuleProcessAsync` 스윕이 **여전히 플래그된 카드만** trash → 이때 분류 B(post-deletion) optional 윈도우를 연다(trash 도착 후 Ascension/Save 선택).

## 5. 경로별 시퀀싱

### 5-1. 효과-삭제 (`MatchStateMutationSink.ApplyDelete`) — **쉬움**
- 현재: 정적 prevent → (자동)Evade/Decoy/... → trash 이동 + (자동)post.
- 변경: prevent(`cannotBeDeleted`/연속) 후 → **`pendingDeletion=true` + `WhenPermanentWouldBeDeleted` emit**, 끝(즉시 이동 안 함).
- 이후 **GameFlowProcessor 루프**가: 윈도우 트리거 수집 → 선택 open(loop pause) → agent resolve → 대체 적용/스킵 → `RuleProcess` 스윕(post 윈도우 포함).
- sink는 동기 유지(파이프라인 pause 불필요) — 루프가 재진입을 담당.

### 5-2. 전투-삭제 (`BattleResolver` + `AttackPipeline`) — **어려움(파이프라인 분할)**
- 현재: DP비교→deleted→(자동)대체→trash이동→Piercing→OnEndBattle→Resolved.
- 변경: **AttackPhase에 `DeletionReplacement` 신규**(BlockTiming의 `Blocking`과 동형):
  - **Combat 단계**: DP비교 → 후보에 `pendingDeletion` + `WhenWouldBeDeleted` emit → 대체-키워드 보유 시 선택 open, `AttackPhase.DeletionReplacement`로 park(미보유면 바로 통과).
  - **루프**: 선택 resolve → 대체 적용/스킵.
  - **DeletionReplacement 단계(재개)**: 스윕(살아남지 못한 후보 trash) → **Piercing은 여기서**(최종 생존 상태 기준) → OnEndBattle emit → Resolved.
- 핵심: **Piercing/OnEndBattle를 대체 윈도우 이후로** 이동(현재는 이전). AS-IS 순서와 일치(대체로 방어자가 살면 Piercing 없음).

## 6. 선택 표면화 메커니즘 (택1)
- **(권장) BlockTiming式 직접 ChoiceRequest**: 삭제-대체 전용 `ChoiceController.RequestChoice`(후보=활성 가능 키워드, canSkip=optional). 등록된 effect 불필요 → KeywordBaseBatch3(포팅) 선행 불요. sub-target은 후속 ChoiceRequest(DeferredChoice).
- (대안) OptionalPromptQueue 경유: 키워드를 등록 effect로 만들어 트리거 수집(=KeywordBaseBatch3 grant 클래스 선행 필요). 더 "정통"이나 포팅 작업을 끌어옴.
- → **권장안 채택 시** 기존 `DeletionReplacementGate` 코스트 로직을 "선택 후 호출"로 재사용, 룰-충실 + 최소 신규.

## 7. 키워드 매핑 (재배선)
- Evade/Barrier/Fragment/ArmorPurge: 자신-대상 대체. 선택 open(self) → 활성 시 기존 `TryEvade`/`TryBarrier`/`Fragment`/`ArmorPurge` 로직 호출(단 sub-선택은 ChoiceRequest로: Fragment "어느 소재", Barrier 비용 등).
- Decoy/Scapegoat: 리다이렉트. 선택 open + **어느 아군 희생**(sub ChoiceRequest) → `SacrificeAsync`.
- Ascension/Save: post-deletion. 스윕 후 Yes/No(+Save 대상 선택) → 시큐리티/스택行.
- Raid: 기존 `RaidAttackSwitch`를 OnUseAttack 선택(활성/스킵 + 어느 최고DP)으로 전환.

## 8. 마이그레이션 순서 (증분)
1. **효과-삭제 경로(5-1)** 먼저 — 파이프라인 분할 불필요, 위험 낮음. Evade/Decoy/Fragment/Scapegoat(효과) + Ascension/Save(post).
2. **전투-삭제 경로(5-2)** — `AttackPhase.DeletionReplacement` 추가 + Piercing/OnEndBattle 재배치. Barrier/ArmorPurge 등 전투분.
3. **Raid** OnUseAttack 선택화.
4. 누락 타이밍(ArmorPurge `WhenTopCardTrashed`) 발화.
5. `DeletionReplacementGate` 자동 호출 제거(코스트 로직만 잔존, 선택 후 호출).

## 9. 리스크
- **핫패스**(전투/효과 삭제) 대규모 변경 + 파이프라인 신규 phase → 회귀 위험 높음. 전체 스위트 필수, 증분별.
- **재진입 정확성**: 다중 후보 동시 삭제(전투 양쪽, 광역 삭제) 시 윈도우/선택 순서(턴플레이어 우선)·중첩 선택 처리.
- **Piercing/OnEndBattle 순서 이동**이 기존 테스트(W5/F-1.5/Piercing) 거동을 바꿀 수 있음 → 해당 테스트 재검토.
- 기존 9키워드 테스트(C57/C46/C821/C4D/C5/C3)는 "자동 적용" 전제 → **선택 기반으로 재작성** 필요(ScriptedChoiceProvider로 활성/스킵 시나리오).

## 10. 테스트 계획
- 효과-삭제: 대체 키워드 보유+활성 선택→생존 / 스킵 선택→삭제 / sub-선택(Fragment 소재) / 미보유→즉시 삭제.
- 전투-삭제: `DeletionReplacement` phase park→resolve→Piercing 순서 / 방어자 Barrier 생존 시 Piercing 없음.
- post: Ascension/Save Yes/No.
- 회귀: 전체 스위트(특히 W5 시큐리티, Piercing, 종료 트리거).

## 11. 산출
- 신규: `AttackPhase.DeletionReplacement`, 삭제-대체 ChoiceRequest 핸들러(`DeletionReplacementTiming` — BlockTiming 형제), `WhenPermanentWouldBeDeleted` 타이밍 상수.
- 변경: `BattleResolver`(분할), `AttackPipeline`(phase), `MatchStateMutationSink.ApplyDelete`(flag+emit), `GameFlowProcessor`(윈도우 open 훅, 스윕 시 post 윈도우).
- 재사용: `DeletionReplacementGate` 코스트 로직(선택 후 호출), `pendingDeletion`+`RuleProcess` 스윕, `ChoiceController`/`DeferredChoiceProvider`.
