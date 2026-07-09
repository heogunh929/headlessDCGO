# 라이브 룰 결손 상환 설계 (RD-1~17)

입력: `rule_deficiency_2026-07-09.md`(룰 결손 명세) · `fidelity_todo_2026-07-09.md`(TODO 113). 원칙: [[check-asis-before-implementing]](착수 시 AS-IS 지점 재확인 후 미러), [[result-equivalence-not-completion]](구조 1:1, substrate 번역만 허용), 게이트=green+동작단언+RuleAudit 0. 각 항목은 독립 커밋 단위로 설계했고, 단계 간 의존성은 §7에 집약.

## ⚠️ 스코프 한계·이연 목록 (1~3단계(RD-4 부분) 구현 완료분 기준, 2026-07-10 / 적대 점검 반영 최신화)

구현 중 의도적으로 **보류/부분 처리**한 항목. 각 이연은 근거·의존 선행조건·트리거(어느 카드/단계 착수 시 승격)를 명시. ~~전부 현재 회귀·RuleAudit 무영향(latent 또는 구조-등가)~~ **[정정 2026-07-10 적대 점검]** 이 무영향 주장은 과장이었음: L8(RD-6)은 **라이브 결손**(BT1_021 EoTLose3Memory가 새-턴 프레임에서 오해소 — 테스트 미고정이라 회귀에 안 보일 뿐), L4·L6도 라이브 발산 포함(아래 ⚔️ 레지스터 참조). "회귀 green"은 무영향의 증거가 아님.

| # | 항목 | 상태 | 근거·선행조건 | 트리거(승격 시점) |
|---|------|------|--------------|------------------|
| L1 | **RD-1 효과-구동 free-digivolve 드로우** | 보류 | 헤드리스 reveal이 library를 peek만(RevealAndSelect:74) → 드로우가 아직-미이동 revealed 카드를 뽑는 발산(BT1_078 실증). AS-IS는 revealed 3장을 execution limbo로 빼낸 뒤 그 아래서 드로우 | Executing-존/reveal-제거 모델(TODO-68/83) 랜딩 시 |
| L2 | **RD-3 버스트 재-진화 엣지** | 미확인 | AS-IS `AddTrashTopCardAtTurnEnd` **정의가 DCGO export에 없음**(호출부만). 버스트 후 같은 턴 재-진화 시 마커가 buried source로 밀리는 경우 처리 불명 | AS-IS 정의 확보 시 / 재-진화-후-버스트 카드 포팅 시 |
| L3 | **RD-2 ICanNotPlayCardEffect 연속 스캔** | 이연 | "이 옵션 플레이 불가" 연속 제한 인프라 = 스켈레톤, producer 0. RD-2 핵심(색 요건)은 완료 | =TODO-49; CanNotPlay/PutField producer 카드 포팅 前 |
| L4 | **RD-12 트리거 수집경로 collection-소모** | 부분 이연 → **라이브 발산 확인** | ActivatedEffectResolver 경로는 완료. 트리거 수집(GameFlowProcessor :495 TryActivate)의 collection-소모는 **주 파이프라인**이며 실피해 3종 확인(⚔️ P1-2): ①declined optional 캡 소진(OptionalPromptQueue 환불 없음) ②수집-후-미해소(게임종료/Clear/wedge) 캡 소진 ③한 pass 2이벤트 시 1번이 fizzle해도 캡 소진+2번 차단. AS-IS는 스택 시 CanTrigger 체크만, 소모는 실행 시 OnProcess 콜백 | 5단계 WindowResolver(재진입 구조)서 자연 해소 |
| L5 | **RD-13 트리거 경로 optional** | 설계상 분리 | OptionalPromptQueue 경로는 기존 유지(이중 질문 방지). 직접-해소 경로만 게이트 | 5단계서 창-루프로 통합 |
| L6 | **RD-4 소스-소비 키워드 카드의 진화원 트래시** | ~~부분 이연~~ → **P0 상환 대상(게이트=AS-IS 위반 확인)** | **[정정 2026-07-10 ⚔️ P0-3]** 게이트 근거가 오류: AS-IS `DiscardEvoRoots`는 **무조건** 실행(Permanent.cs:117-128 키워드 체크 없음). Save는 top만 Tamer 밑 이동(Save.cs:61, 소스는 이미 트래시), Fortitude는 삭제-前 hashtable **스냅샷** 판독(Fortitude.cs:29-35, 소스 트래시돼도 발화). "소스를 남겨야 POST 동작"은 AS-IS 미러가 아니라 헤드리스 POST-창 자체 구조의 워크어라운드. 결과: 4키워드 카드는 수락/거절 불문 트래시 카운트 영구 부족(Save N·Decode N-1·Partition N-2장) + Fortitude 재생 시 `sourceIds` 삭제로 소스 **도달불가 고아화**(DeletionReplacementGate.cs:221) | **게이트 제거 + Save/Fortitude 스냅샷화**(무조건 트래시)로 즉시 상환 — TODO-96 전체 재설계 대기 불요 |
| L7 | **RD-4 ACE-소스 Overflow · LinkedCards 트래시** | 이연 | `DiscardEvoRoots`의 소스 AceOverflow(TODO-98)·LinkedCards 경로 미미러. 현재 ACE-소스/Link 카드 미포팅 | TODO-98 / Link 메커니즘 포팅 時 |
| L8 | **RD-6 턴 종료 시퀀스 전체** | 이연 — ~~안전 서브셋 부재 실증~~ → **[정정] emit-only는 무해하나 무익(오진 정정)** | **[정정 2026-07-10 ⚔️ D-1]** "6-test 회귀 실증"은 **오진**: 실인은 시험 주석의 "TODO-67" 문자열이 `MetadataActionProcessor.cs` 소스를 `Contains("TODO")`로 스캔하는 6개 테스트의 린트 가드에 걸린 것(행동 회귀 0건 — 통제 재실험으로 TODO-무포함 주석 시 384/384 확인). emit은 **프레임-독립 맞음**(EndTurn 완전 동기·드레인은 액션 반환 後 유일) → 리포지션은 무해하지만 **AS-IS 수렴 0**(해소가 항상 플립 後)이라 무익. 이연의 올바른 근거: 플립 前 해소=in-action 미니 창-루프=Stage 5. **추가 발견**: AS-IS는 [End of Turn] 창(:699)이 어택 루프(:705)보다 **先**인데 헤드리스는 역순(TryOpen이 먼저) — Stage 5 상환 시 함께 교정. 공격 서브케이스 자체는 EndOfTurnEffectAttack가 프레임-정확. step4 지속-분기=TODO-67. **라이브**: BT1_021이 새-턴 프레임에서 오해소 중(테스트 미고정) | 5단계 WindowResolver(드레인-전-플립) + TODO-67 |

**공통 원칙**: 위 이연은 [[strong-model-prebuild-latent-infra]] 기준으로 "해당 카드/단계 착수 前 강모델 선행 구축" 대상. 로컬 LLM에 맡기면 안 됨(엔진 내부 발산, silent-wrong 위험). "현재 무영향"은 skip 사유 아님([[no-callsite-not-skip-reason]]).

---

## ⚔️ 적대적 점검 결과 레지스터 (2026-07-10, 2·3단계 구현분 대상)

독립 적대 reviewer 4기(RD-10/11·RD-12/13·RD-4·RD-6) 투입, "완료" 주장을 반증 관점으로 검증. **생존 주장**: RD-13 핵심 순서(confirm→consume→body)·질문 시점·이중질문 없음, RD-5 전체, RD-4 배선 2곳 내 순서·트리거 미발화, RD-12 Consume-before-body 자체(AS-IS 동일), RD-6 AS-IS 순서 파악. 이하는 격파된 항목.

### P0 — 즉시 상환 (죽은 수정 / 라이브 오동작)

| # | 발견 | 증거 | 상환 |
|---|------|------|------|
| **P0-1** | **RD-10 수정 사망**: `CardEffectSchedulerResolver.WithSinkMetadata`(:133)가 `new EffectResult(Resolved, Message, values)`로 **Status 미전달** 재구성 → ctor 기본값(EffectResult.cs:33)이 `Resolved=false→Failed`. 라이브 경로(MatchStateMutationSink 사용 시 extra 항상 non-null)에서 Skipped가 **반드시 Failed로 격하** → fizzle 큐-wedge **그대로 잔존**. Skipped 생산자는 엔진 전체에 HeadlessCardEffectContract:282 단 1곳이고 정확히 이 격하 경로를 통과 | 본선 직접 재확인 완료 | Status 전달 1줄 + **라이브-경로 관통** 테스트(커스텀 람다 주입 아닌 실제 resolver 체인) |
| **P0-2** | **RD-11 동시-배치 과발화**: AS-IS는 동시 삭제(DP-0 스윕 등)를 `DestroyPermanentsClass` **배치당 1회** `StackSkillInfos`(hashtable에 per-permanent 리스트 팩킹, CardController.cs:3736-3743)→리스너당 **1 SkillInfo**·Some-over-batch 술어·1회 실행(+1). 헤드리스는 카드당 CardMoved 이벤트로 N회 발화(+2). 순차 삭제는 별도 call=별도 SkillInfo라 per-event가 맞음 — 즉 수정이 **순차엔 옳고 동시엔 과교정**. `RD11-PerEventFire` 테스트가 정확히 동시-배치 시나리오를 +2로 고정(反AS-IS) | AutoProcessing.cs:469-484 / HashtableSetting.cs:85-131 / OnDeletion.cs:20-38 | 삭제 이벤트를 **배치 단위**로 방출(batched CardMoved 또는 deletion-batch 이벤트)+테스트 기대값 +1로 교정 |
| **P0-3** | ✅**상환(2026-07-10, 7780f5a0 후속)** — Save/Fortitude 게이트 제거→무조건 트래시. Fortitude 적격성=삭제-시점 count 스냅샷(`SourceCountAtDeletionKey`, `SnapshotPostReplacementKeywords`서 freeze), 트래시로 고아도 해소. Decode/Partition만 게이트 유지(POST가 None 소스 플레이=PRE 이동 TODO-96 결합). | Permanent.cs:117-128 / Save.cs:61 / Fortitude.cs:29-35 | **잔여**=Decode/Partition PRE 이동(TODO-96) 시 전체 정합 |
| **P0-4** | ✅**상환(2026-07-10)** — 두 경로 배선(각각 OnDeleted→소스트래시→top 이동→Fortitude 재생, battle 경로 동형): ①`RuleProcessAsync:217`(PRE-거절 마무리) ②`SecurityResolver:389`(시큐리티-배틀 패자). | reviewer 전수 스캔 | (②는 RD-7 공용화 시 재확인) |

### P1 — 구조 발산 (해당 단계/카드 착수 前 상환)

| # | 발견 | 상환 시점 |
|---|------|-----------|
| P1-1 | **RD-10 skip-and-dequeue가 AS-IS 같은-창 재평가 소실**: AS-IS MultipleSkills(:76-165)는 매 pass 전 스택 `CanActivate` 재평가·재제공(제거는 실제 활성화 시만) — 나중 해소로 조건 충족된 스킬이 같은 창에서 발화. 헤드리스는 수집 시(:488 continue=미enqueue)+해소 시(dequeue) **이중 영구 탈락**. 플레이어 순서선택으로 조건 성립 유도 가능성도 소실 | Stage 5 창-루프의 **핵심 요건**으로 명시(§5.1 재평가 루프가 정확히 이것) |
| P1-2 | **RD-12 수집-소모 실피해 3종**(L4 정정 참조) | Stage 5 |
| P1-3 | **RD-12 Consume의 재실행 계약 위반(latent P0)**: `DeferredChoiceProvider` 계약="효과는 답변마다 처음부터 재실행, 변이는 choice 요청 後" — `Consume`(ActivatedEffectResolver:492)은 sink 밖 비-스테이징·비-롤백 변이. capped **인터랙티브** body가 live에서: 1회차 소모→body 내 suspend→재실행 시 CanActivate=false→**효과 증발+use 소진**. 현재 capped 카드 전원 비-인터랙티브라 불가시 | 첫 "[Once Per Turn] choose…" 카드 포팅 **前** 필수(Consume을 body 완주 후로 이동 또는 staged화) |
| P1-4 | **RD-11 cut-in 창 same-effect dedup 부재**: AS-IS `HasExecutedSameEffect` skipCondition(AutoProcessing.cs:623-627, 컷인 창들 CardController.cs:727·988·5189·5301·5709)의 창-내 중복 억제 미러 없음. (부기: AS-IS `IsCutinEffectUsedMaxCount`(:1095-1098)에 부호 역전 의심 — 포팅 시 명시 결정 필요) | Stage 5 (§5.2 skip-집합에 이미 계획됨 — 본 발견으로 근거 보강) |
| P1-5 | **RD-12 AS-IS 소모 지점 3곳 중 1곳만 미러**: ①트리거-스택 OnProcess(미러함) ②**선언형 메인 활성화=선언 시점 소모**(TurnStateMachine.cs:1183-1186, optional 질문·코스트보다 先) ③백그라운드=수집 시(AutoProcessing.cs:902 등). 선언-경로 포팅 시 ②를 적용해야 함(①로 통일하면 발산) | 선언형 메인-액션(UseCardEffect 상당) 포팅 時 |
| P1-6 | **RD-12 재-스택 use 리셋 부재**: AS-IS는 `CardSource.Init`(:345-350, 진화 재료 스택 시 CardController.cs:3093·3393·1511)에도 use 카운트 리셋; 헤드리스는 턴 경계만(instanceId 키 영속) | 같은 턴 재-스택/재-플레이 시나리오 카드 포팅 時 |
| P1-7 | **RemoveUse 환불 프리미티브 부재**: AS-IS 카드 10+장(AD1_024:265·BT14_029:114 등)이 body 미실행 시 캡 환불 호출 — 헤드리스 대응물 없음(해당 세트 미포팅=latent) | 해당 카드 포팅 前 선행 구축 |
| P1-8 | **RD-13 per-shape optional/cap 우회**: IsOptional/MaxCountPerTurn은 uniform ActivatedEffect 전용 — resolver ~30개 per-shape 케이스는 캡·yes/no 없음(BT1_084는 decline을 pick-skip으로 모델=결과 등가나 결정 구조 상이) | uniform 프리미티브 이관([[asis-uniform-activateclass]]) 진행이 곧 상환 |
| P1-9 | **RD-4 보호필터 밀수(latent)**: `TrashSourcesAsync` 재사용으로 `CanNotTrashFromDigivolutionCards` 필터(ITrashDigivolutionCards 전용)가 DiscardEvoRoots 미러에 혼입 — AS-IS 삭제는 보호 무시하고 트래시(Permanent.cs:121 직접 AddTrashCard). 현재 키워드 producer 0이라 inert | 보호 키워드 포팅 前(전용 무필터 경로로 분리) |
| P1-10 | **battle 경로 knock-out 창이 트래시 前 해소**: AS-IS는 [On Deletion] 스킬을 소스+톱 트래시 **後** 해소(스택만 :3736에서) — 헤드리스 F-6.3 2-phase는 창을 트래시 前 해소(트래시 카운트 판독 효과가 pre-trash 상태 관측). RD-4 이전부터의 기존 발산(RD-4 배선이 상속) | RD-4 전체 시퀀스 재설계 時 |

### 문서·주석·테스트 거짓 (D — 정정 자체가 상환)

| # | 항목 | 조치 |
|---|------|------|
| D-1 | RD-6 "6-test 회귀 실증" 허위(오진) — 본 문서 L8·§D-RD6은 정정 완료; **`MetadataActionProcessor.cs:812-817` 주석의 동일 허위 잔존** | 코드 주석 정정(P0-1 커밋에 동승; 주석에 "TODO" 문자열 금지 — 린트 가드) |
| D-2 | RD-10/11 테스트 결함: RD10=커스텀 resolver 람다 주입으로 격하 계층 우회(tautology)+Failure-wedge를 소망 동작으로 고정; RD11=反AS-IS 기대값(+2) 고정 | P0-1/P0-2 상환 시 테스트 재작성 |
| D-3 | RD-4 테스트가 배선 미검증(헬퍼 직접 호출만) — sink/BattleResolver 관통·소스先톱後 순서·게이트 결과 미고정; check 2~5는 발산 동작을 PASS로 고정 | P0-3/P0-4 상환 시 통합 테스트(삭제→트래시 census) 추가 |
| D-4 | RD-12 주석·§D-RD12의 AS-IS 소모 지점 과일반화(1곳으로 기술, 실제 3곳) | 본 문서 정정 완료(P1-5); 코드 주석은 P1-5 상환 시 |
| D-5 | RD-13 부기: AS-IS는 **Owner**에 질문(OptionalSkill.cs:18)+대상 프리뷰 제공(:24-33) — 헤드리스는 Controller에 질문·설명만(현재 controller==owner라 등가) | controller≠owner 분리(빼앗기 효과) 포팅 時 |

### 권고 상환 순서
1. **P0-1**(Status 전달 1줄+관통 테스트) + **D-1**(허위 주석 정정) — 최소 diff·최대 효과
2. **P0-3/P0-4** RD-4 재설계(게이트 제거+스냅샷화+2경로 배선) + **D-3** 통합 테스트
3. **P0-2** RD-11 배치-이벤트 모델 + **D-2** 테스트 교정
4. P1군은 각 명시 시점에 상환(P1-1·2·4=Stage 5 설계 입력, P1-3=첫 capped-인터랙티브 카드 前, 나머지=해당 카드/기능 포팅 前)

---

## 1단계 — 국소 룰 패치 (독립, 즉시)

### D-RD1. 진화 시 1드로우
**신규**: `Runtime/DigivolveCommons.cs`
```csharp
// AS-IS CardController.cs:1526-1529 — isEvolution 공통 후처리(카운터+드로우) 단일 지점 미러.
public static async Task OnDigivolveCompletedAsync(EngineContext ctx, HeadlessPlayerId player, CancellationToken ct)
{
    ctx.PlayerTurnCounters.Increment(player, DigivolveCountKey);   // 기존 위치에서 이동
    await ctx.ZoneMover.DrawAsync(player, 1, ct);                  // AS-IS DrawClass(owner,1,null)
    TriggerEventEmitter.Emit(ctx.GameEventQueue, TriggerTimings.OnDraw, actor: player); // TODO-40 동시 상환
}
```
**호출 지점(구현 결과)**: ①`DigivolveAction`(정상+앱퓨전 — 인라인 카운터 대체) ②`SpecialPlayAction`(Burst/Blast/DnaDigivolve; **DigiXros/Assembly는 !isEvolution=제외**, AS-IS :626/755로 확인). **③효과-구동 free-digivolve(CPF:5240 DigivolveOntoSelf)는 보류** — AS-IS도 isEvolution이나(BT1_078 PlayCard 경유 확인), 헤드리스 reveal이 library를 peek만 하고(RevealAndSelect:74) revealed 카드를 물리 제거하지 않아, 이 지점 드로우가 아직 library에 있는 revealed 카드를 뽑는 발산 발생(BT1_078 실증). AS-IS는 revealed 3장을 execution limbo로 빼낸 뒤 그 아래에서 드로우 → **reveal/Executing-존 모델(TODO-68/83) 선행 필요**. 덱아웃은 DrawAsync 내 기존 판정 경유.
**구현/검증**: `Runtime/DigivolveCommons.OnDigivolveCompletedAsync`(counter+draw+OnDraw). 테스트 RD1-DigivolveDraw(6검). 회귀: 진화가 이제 이동이벤트+1·드로우하므로 G2E-002 단언 `+2→+3` 갱신. 전체 377/377·RuleAudit 0(승패분포·게임길이 변화는 드로우 도입 정상).
**검수 수정(2026-07-10)**: DigivolveAction의 draw/counter 호출이 WhenDigivolving·OnAddDigivolutionCards **방출 뒤**에 있어 이벤트 큐에서 OnDraw가 digivolve 창들 뒤로 가는 역전 발견 → 방출 **전**으로 이동(AS-IS :1526 draw < :1691 창 스택 순서 미러). SpecialPlayAction은 원래 정상. 회귀 380/380 유지.
**주의**: 드로우 위치는 AS-IS와 동일하게 **스택 이동·RegisterCard 완료 후, WhenDigivolving 창 해소 전이 아님**(AS-IS는 PlayCard 말미 :1526 — WhenDigivolving 스택보다 뒤). 각 호출부에서 AS-IS 순서 재확인 후 삽입.
**테스트** `RD1-DigivolveDraw`: 일반/조그레스/버스트/효과-구동 4경로 손패+1, 덱−1; 덱 0장 진화 시 덱아웃.

### D-RD2. 옵션 색 요건 + CanNotPlay 스캔
**신규**: `Runtime/OptionColorRequirement.cs`
```csharp
// AS-IS CardSource.cs:255-321 MatchColorRequirement 미러.
public static bool Matches(EngineContext ctx, HeadlessPlayerId owner, CardRecord option)
{
    // (1) ignore-color: IIgnoreColorConditionEffect 상당 — 연속 스캔(negation 게이트 없음, AS-IS 그대로)
    if (HasIgnoreColorCondition(ctx, owner, option)) return true;
    // (2) population: 소유자 BattleArea + BreedingArea의 톱카드 색 합집합 (AS-IS GetFieldPermanents=16슬롯 전체)
    var fieldColors = CollectTopCardColors(ctx, owner /* BattleArea ∪ BreedingArea */);
    // (3) 옵션의 모든 색 ⊆ fieldColors
    return OptionColors(option).All(fieldColors.Contains);
}
```
- **구현 결과**: `Runtime/OptionColorRequirement.Matches(ctx, owner, optionCardId)` — ①ignore-color(ApplicableEffects서 `IgnoreColorRequirementKey`=AS-IS IgnoreColorConditionClass, self+player-scope+field 조건-aware 스캔), ②옵션 CardColors(2-stage fold=색변경 반영) 전부가 owner BattleArea+**BreedingArea** permanent top-card CardColors 합집합에 존재. `OptionActivateAction.Validate`(:216 IsOptionLocked 뒤)에 게이트. 색-없는 옵션=요건 없음.
- **ICanNotPlayCardEffect 스캔 보류**: producer 0(스켈레톤)이라 latent = **TODO-49**로 분리(색 요건이 live P0). 착수 시 `RestrictionScan` joint 규약 재사용.
- **완료**: RD2-OptionColor(7검: 색없음/일치/불일치/타색/2색부분/2색완전/브리딩). 회귀 380/380·RuleAudit 0(20/20 terminal). 기존 옵션 테스트는 colorless라 무영향.
- **검수 잔여 3건 → ✅선행 구축(2026-07-10)**:
  - **latent-1 dual 카드 색**: AS-IS `IsDigimon ? DualCardColors : CardColors`(CardSource.cs:307). CardSource(CPF)에 `BaseDualCardColors`/`DualCardColors` 미러(seed=`optionColorRequirements` 메타=OptionCardColorRequirements, CardColors와 동일 2-stage fold) + `OptionColorRequirement`가 dual 카드(IsCardType Digimon)면 DualCardColors 사용. (색 데이터는 dual 카드 포팅 시 로더가 메타 채움.)
  - **latent-2 IsPermanent 가드**: 색 공급원을 `IsCardType(Digimon|Tamer|DigiEgg)` permanent로 제한(CEntity_Base.cs:238 확인 — 순수 필드 옵션 제외).
  - **latent-3 self ignore-color**: 손패 옵션의 자기 ignore-color를 `CardEffectRegistrar.BuildContinuousRequests`(faceup 시큐리티와 동형 dispatch-빌드)로 스캔, `CardSource.EffectConditionPasses`로 조건 honor.
  - 테스트 RD2-OptionColor 확장(11검: 기존 7 + 필드-옵션 제외·dual Red요건·dual Green불충족·self ignore-color) + 픽스처 TfxOptionIgnoreColor. 회귀 380/380·RuleAudit 0.

### D-RD3. 버스트 진화 임시성 — ✅완료
- `SpecialPlayAction`(kind==Burst) 성공 지점에서 burst 톱에 `GameFlowProcessor.BurstTrashAtTurnEndKey` 스탬프.
- `HeadlessEndTurnCleanupFlow`: owner(버스트 플레이어)의 턴 종료 시 마커를 `BurstTrashAtTurnEndDueKey`로 승격(DeleteAtTurnEnd 패턴 미러 — Cleanup은 동기라 마킹만).
- `GameFlowProcessor.SweepDueTurnEndDeletionsAsync`(async, RunToStable 경유)에 burst 분기 추가: due 카드에 `DeDigivolveHelpers.ArmorPurgeTopAsync`(톱카드만 트래시+차상위 승격+permanent 생존, 토큰 처리·WhenTopCardTrashed 방출 포함 = generic top-trash). full-deletion 경로보다 먼저 처리.
- **⚠️AS-IS 소스 갭**: `AddTrashTopCardAtTurnEnd` **정의가 DCGO export에 없음**(호출부 CardController.cs:1531-1538만). 버스트 룰(턴종료 톱 트래시)+AS-IS ArmorPurge 선례로 common-case 구현. **미확인 엣지**: 버스트 후 같은 턴 재-진화 시 마커가 buried source로 밀림(AS-IS는 permanent 단위 IsBurstDigivolved이라 재-진화 후 top 처리가 상이할 수 있음) — 버스트가 통상 턴-종료 플레이라 rare, 정의 미확인이라 보류.
- **완료**: RD3-BurstTemporary(5검: cleanup 승격·base 소거·톱 트래시·차상위 승격). 회귀 379/379·RuleAudit 0.

### D-RD5. Scapegoat 디지몬 한정 (+자기효과 게이트) — ✅완료
- `FindScapegoatSacrificeCandidates`에 optional `EngineContext` 추가 + `ContinuousKeywordGate.IsDigimon(ctx, candidateId)` 필터(context-less 사전체크는 superset 유지, context-aware 호출부 :154/:413에 context 스레딩). AS-IS IsPermanentExistsOnOwnerBattleAreaDigimon(TreatAsDigimon 포괄 chokepoint).
- 자기효과 게이트(TODO-102): `DeletionReplacementTiming` PreOptions(context)의 ScapegoatOption 추가에 `!ReadOwnEffectDeletion(record)` 게이트 — sink가 by-effect defer 시 기록한 `DeletedByOwnEffectKey`(MatchStateMutationSink.cs:734) 판독(그 truth가 이미 by-effect∧own 함의). AS-IS Scapegoat.cs:65-73.
- **완료**: RD5-ScapegoatGuards(6검: Digimon 포함/Tamer 제외/holder 제외; own-effect 억제/비-own 제공). 회귀 378/378·RuleAudit 0. context-less 기존 호출부 무영향.

---

## 2단계 — 트리거 hotfix (창-루프 재설계 前 선행 가능) — ⚠️적대 점검으로 "완료" 주장 격하(2026-07-10)

**요약(원 주장)**: RD-10(fizzle=Skipped dequeue)·RD-11(이벤트별 dedupe)·RD-13(optional yes/no 게이트) 완전 구현. RD-12는 ActivatedEffectResolver 경로 완전 구현(cap을 CanActivate 비소모 체크→optional yes/no→수락 시 Consume), **트리거 수집 경로(:495)의 collection-소모는 OptionalPromptQueue/창 흐름 결합이라 5단계로 이연**. 회귀 383/383·RuleAudit 0. 신규 테스트 RD10/RD11/RD13(+RD-12 동일 테스트) + 픽스처 TfxOnDeleteGainMemory·TfxOptionalMemory.

**⚔️점검 후 실상**: RD-10 **미완**(수정이 라이브 경로에서 사망 — P0-1), RD-11 **부분 오구현**(순차=옳음, 동시-배치=과발화 — P0-2), RD-12 부분 완료 유지(단 소모 지점 과일반화 D-4·재실행 계약 위반 P1-3 잠복), RD-13 **완료 유지**(핵심 주장 전부 생존, per-shape 스코프 갭 P1-8은 uniform 이관 계획과 일치). 상세는 각 섹션 점검 블록·⚔️ 레지스터.

### D-RD10. fizzle = skip (wedge 제거) — ❌점검 결과 수정 사망(P0-1)
- `EffectScheduler.ResolveNextAsync`: 결과 분류 3종으로 — ①`Resolved` → dequeue ②**`GateFailed`(CanResolve-실패)** → **dequeue + skip 기록**(AS-IS MultipleSkills.cs:122-126 continue 미러) ③`Error`(리졸버 예외/불변 위반) → 현행 유지(비-dequeue, 진단 보존).
- 구현: `EffectResult`에 `FailureKind { Gate, Error }` 추가; `HeadlessCardEffectContract`(:274-282)의 CanResolve-실패 반환을 Gate로 태깅. `ResolveAllAsync`는 Gate면 continue, Error면 break.
**테스트** `RD10-FizzleSkip`: 선행 해소가 후행 조건을 무너뜨리는 2-트리거 배치 — 후행 skip + 큐 소진; Error는 여전히 블록.
**⚔️점검(2026-07-10)**:
- **P0-1 수정 사망**: `CardEffectSchedulerResolver.WithSinkMetadata`(:133)가 sink 메타 병합 시 Status를 안 넘겨 재구성 → ctor 기본값으로 `Skipped(Resolved=false)→Failed` 격하. 라이브 배선(EngineContext:265-276, MatchStateMutationSink)에서는 extra가 항상 non-null이라 **격하가 무조건 발생** → fizzle wedge 잔존. Skipped 생산자는 HeadlessCardEffectContract:282 단 1곳=정확히 이 경로.
- **D-2 테스트 tautology**: RD10 테스트는 커스텀 resolver 람다를 스케줄러에 직접 주입 — 격하가 일어나는 `CardEffectSchedulerResolver` 계층을 통째로 우회해서 green. 스케줄러 배관만 증명, 라이브 경로 무증명. 또 test 2가 Failure-wedge(비-dequeue 파킹)를 소망 동작으로 고정 — AS-IS엔 "파킹된 실패 head" 상태 자체가 없음(유일한 해소-시 실패=CanActivate false=skip).
- **P1-1 재평가 소실**: dequeue-on-skip은 AS-IS 같은-창 재평가 루프(매 pass 전 스택 CanActivate 재평가, 제거는 활성화 시만)를 영구 소실 — 나중 해소로 조건 충족된 스킬이 AS-IS선 발화, 헤드리스선 이미 탈락. 수집 시(:488 continue)에도 같은 소실 존재(이벤트가 드레인돼 재수집 불가). Stage 5 창-루프 핵심 요건.
- **부기**: 재실행 위험 — 변이 적용 후 예외로 Failure 반환 시 head 잔존→다음 pass 재실행→**변이 이중 적용** 가능(AS-IS 코루틴엔 재실행 의미론 없음). Error 처리 재설계 시 고려.

### D-RD11. 이벤트별 발화 (dedupe 키 확장) — ⚠️점검 결과 동시-배치 과발화(P0-2)
- `GameFlowProcessor.AutoProcessAsync`(:438-451): `seen`을 `HashSet<(HeadlessEntityId effectId, long eventSeq)>`로 — **같은 이벤트 내** 중복 수집만 차단, 이벤트별로는 각각 enqueue(각자의 enriched subject 유지 = AS-IS 이벤트별 SkillInfo/hashtable).
- once-cap과의 상호작용: 캡 소비는 RD-12에 따라 실행 시점이므로 여기서 N회 enqueue돼도 캡이 M<N이면 실행 시 잘림(AS-IS 동일).
**테스트** `RD11-PerEventFire`: 한 pass 2건 삭제 → "삭제될 때마다" 효과 2회 발화, 각 발화의 subject가 해당 삭제 카드.
**⚔️점검(2026-07-10)**:
- **P0-2 과교정**: AS-IS의 "이벤트" 단위는 **delete-process 배치**(카드 아님). 동시 삭제(DP-0 스윕 AutoProcessing.cs:469-484, 보드 와이프)는 `DestroyPermanentsClass` 1회 → `StackSkillInfos` **1회**(hashtable "hashtables" 키에 per-permanent 리스트 팩킹, CardController.cs:3736-3743/HashtableSetting.cs:85-131) → 리스너당 **1 SkillInfo**·Some-over-batch 술어(OnDeletion.cs:20-38)·1회 실행. 순차 삭제는 별도 call=별도 SkillInfo → per-event가 맞음. 헤드리스는 카드당 CardMoved라 동시 2건에 2회 발화(+2 vs AS-IS +1). **D-2**: RD11 테스트가 정확히 동시-배치 케이스를 +2로 고정(反AS-IS) — 상환 시 +1로 교정.
- **P1-4 컷인 dedup 부재**: AS-IS `HasExecutedSameEffect` skipCondition(AutoProcessing.cs:623-627; 컷인 창 CardController.cs:727·988·5189·5301·5709)의 창-내 같은-효과 중복 억제 미러 없음. (AS-IS `IsCutinEffectUsedMaxCount`:1095-1098 부호 역전 의심 — 1:1 포팅 시 명시 결정.)
- 상환 방향: 삭제를 **배치 이벤트**로 방출(멀티-subject CardMoved 또는 deletion-batch GameEvent, 술어는 Some-over-batch) — sink FlushAsync/스윕이 배치 경계를 소유.

### D-RD12. once-per-turn 소모를 실행 시점으로 — ✅부분 완료(ActivatedEffectResolver 경로) — ⚔️점검 주석 첨부
- **구현**: `OnceFlagController`에 `CanActivate`(비소모 체크)+`Consume`(소모) 분리, `TryActivate`는 둘의 합성으로 유지. `ActivatedEffectResolver` uniform 케이스: CanResolve → `CanActivate`(capped-out면 미제시) → optional yes/no(RD-13) → 수락 시 `Consume`+ResolveBody. 거절/capped 시 미소모. AS-IS OnProcess 소모 시점 미러(ICardEffect.cs:1118-1121).
- **이연(5단계)**: **트리거 수집 경로**(GameFlowProcessor :495 `TryActivate`)의 collection-소모 — declined trigger-optional(OptionalPromptQueue 경유) 과소모는 창-루프(WindowResolver) 재진입 구조서 자연 해소. ~~현재 이 경로 optional의 캡은 수집 시 소모(과소모는 declined 시만, rare)~~ **[정정 L4/P1-2]** 과소모는 declined 외 2종 더(미해소 소진·fizzle 후 2번째 이벤트 차단) — rare 아님, 주 파이프라인.
**테스트** `RD13-OptionalGate`(RD-12 겸): 캡1 optional 거절→같은 턴 재발화 가능(미소모); 수락→+1·재수락 no-op(소모).
**⚔️점검(2026-07-10)**:
- **D-4 소모 지점 과일반화**: "AS-IS는 OnProcess에서 소모"는 **트리거-스택 경로만**의 사실. AS-IS 소모 지점은 3곳 — ①트리거-스택=OnProcess 콜백(`UseOptional||!IsOptional` 게이트, ICardEffect.cs:1117-1121; 미러함) ②**선언형 메인 활성화=선언 시점**(TurnStateMachine.cs:1183-1186, `RegisterUseEffectThisTurn(UseCardEffect)`가 optional 질문·코스트·body보다 先; `MaxCountPerTurn<100` 게이트) ③백그라운드=수집 시(AutoProcessing.cs:902·925·948·971·1039). **P1-5**: 선언형 메인-액션 포팅 시 ②를 적용할 것(①로 통일하면 발산).
- **P1-3 재실행 계약 위반(latent P0)**: `Consume`은 sink 밖 비-스테이징·비-롤백 변이인데 `DeferredChoiceProvider` 계약은 "답변마다 효과 처음부터 재실행" — capped **인터랙티브** body는 1회차 Consume→body 내 suspend→재실행 시 CanActivate=false→**효과 증발+use 소진**(캡≥2면 이중 소모). 현 capped 카드(ST4_11·BT2_002·BT2_073·ST3_01·ST3_04·ST2_11) 전원 비-인터랙티브+ScriptedChoiceProvider 비-suspend라 전 테스트 green인 채 잠복. **첫 "[Once Per Turn] choose…" 카드 포팅 前 Consume을 body 완주 후로 이동 또는 staged화 필수.**
- **P1-6 재-스택 리셋 부재**: AS-IS는 `CardSource.Init`(:345-350; 진화 재료 스택 CardController.cs:3093·3393·1511·DigiXros)에도 use 리셋 — 헤드리스는 턴 경계만+영속 instanceId 키라 같은 턴 재-스택 카드가 계속 capped. **P1-7**: AS-IS `RemoveUse()` 환불(ICardEffect.cs:1242-1245, 사용 카드 10+장) 대응물 부재(미포팅 세트=latent).

### D-RD13. IsOptional yes/no 게이트 — ✅완료(핵심 주장 점검 생존) — ⚔️부기 첨부
- **구현**: `ActivatedEffectResolver` uniform 케이스에 `ConfirmOptionalAsync`(ChoiceType.OptionalEffect 단일 "use" 후보, canSkip=true → 선택=yes/skip=no) — cap 소모 前. AS-IS OptionalSkill "Will you use ~?" 미러. 비대화형 body의 강제 실행 해소.
- 트리거 경로 optional(OptionalPromptQueue)은 기존대로 유지(이중 질문 없음 — 이 게이트는 직접-해소 경로 전용). ScriptedChoiceProvider 미스크립트 fallback=skip이라 기존 테스트 무영향(383/383).
**테스트** `RD13-OptionalGate`: 비대화형 optional(메모리+1) — 거절 시 무변화, 수락 시 +1, 캡 1회.
**⚔️점검(2026-07-10)**: 순서(confirm→consume→body=AS-IS Activate_Optional→Execute 미러)·질문 시점(코스트/대상 前 양쪽 일치)·이중질문 없음(파이프라인 분리, ToBinding throw) 전부 생존. 테스트도 behavioral(비-tautology). 부기:
- **D-5**: AS-IS는 카드 **Owner**에 질문(OptionalSkill.cs:18)+대상 프리뷰(:24-33) — 헤드리스는 Controller에 질문·설명만. 현재 전 호출부 controller==owner라 등가; 컨트롤 탈취 효과 포팅 時 정정.
- **P1-8 per-shape 우회**: IsOptional/MaxCountPerTurn은 uniform 전용 — per-shape ~30케이스는 캡·yes/no 없음(BT1_084 decline=pick-skip 모델, 결과 등가·결정 구조 상이). uniform 이관([[asis-uniform-activateclass]])이 곧 상환.
- **커버리지 갭**: 실 optional 카드 5장(BT2_085·ST4_14·BT1_039·BT1_081·BT1_086) 테스트 0건 — fallback=Skip 전환으로 이전 강제-실행이 침묵-거절로 뒤집혔는데 미고정. accept-경로 고정 테스트 추가 권장. (AS-IS 기본: 인간=대기, AI=90% 수락 — 스크립트 무응답=100% 거절은 시뮬레이션 의미론 차이로 인지.)
- 트리거-경로 optional은 AS-IS 스택-순서 창(MultipleSkills:280-334, 선택=수락 함의 `isCheckOptional=false` 포함) 없이 스캔 순서 즉답 — Stage 5 창-루프 통합 시 해소(기존 L5와 동일).

---

## 3단계 — 시퀀스 재설계

### D-RD6. 턴 종료 시퀀스 (MetadataActionProcessor.EndTurn 재배열) — ⏸️이연(근거 정정: emit-only는 무해하나 무익, 2026-07-10)
**시도·결과(원 기록)**: step 3의 `OnEndTurn` emit을 cleanup·`TurnController.EndTurn()` 플립 **前**(ending 프레임)으로 이동하는 "무해 구조 재배열"을 시도 → 6개 턴-경계 테스트 회귀 → "emit이 프레임-독립이 아님, 안전 서브셋 불성립" 결론·코드 원복.
**⚔️점검 정정(2026-07-10, D-1)**: 위 결론은 **오진**. 통제 재실험(동일 리포지션을 "TODO" 문자열 없는 주석으로 재적용) 결과 **384/384 전부 통과** — 깨졌던 6개는 정확히 `MetadataActionProcessor.cs` 소스 텍스트를 `Contains("TODO", OrdinalIgnoreCase)`로 스캔하는 린트-가드 테스트 집합이었고, 시험 주석의 "TODO-67" 참조가 걸린 것(그중 G2E-003·G2E-005는 턴-경계 행동과 무관 — 이 사실만으로 진단을 의심했어야 함). 행동 회귀 **0건**.
- **올바른 사실관계**: emit은 **프레임-독립 맞음** — EndTurn은 완전 동기(TriggerEventEmitter=enqueue만), 큐 드레인은 액션 반환 後 `AutoProcessAsync` 유일, Cleanup/CompleteMemoryPassTurn은 큐 미접근. 따라서 리포지션은 **무해하지만 무익**(해소가 항상 플립 後라 AS-IS 수렴 0). 이연은 유지하되 근거를 교체: RD-6의 실체는 emit 위치가 아니라 **플립 前 in-action 창 해소**(미니 창-루프)이며 이는 Stage 5(WindowResolver, RD-14~17)와 구조적 결합.
- **추가 발견(점검)**: AS-IS는 [End of Turn] 창 해소(:699-702)가 어택 루프(:705-712)보다 **先**인데, 헤드리스는 `EndOfTurnEffectAttack.TryOpen`이 먼저(공격창)→EoT activated는 플립 後 — 서브케이스 간 상대 순서도 역전. Stage 5 상환 시 함께 교정.
- **라이브 결손 명시**: BT1_021(EoTLose3Memory)이 현재 새-턴 프레임에서 오해소(rule_deficiency §RD-6와 일치) — 테스트 미고정이라 회귀에 안 보일 뿐 latent 아님.
- **공격 서브케이스**: `EndOfTurnEffectAttack`(창 열림→턴 미종료→재적용)는 플립 지연으로 프레임-정확 — 유지.
- **잔여 조치(D-1)**: `MetadataActionProcessor.cs:812-817` 주석의 동일 허위("empirically broke 6 tests…not frame-independent") 정정 필요 — P0-1 커밋에 동승, 주석에 "TODO" 문자열 금지(린트 가드).

AS-IS 단계 그래프(AutoProcessing.cs:675-727 + TurnStateMachine.cs:3151-3210) 미러:
```
EndTurn(액션, 구 턴 컨텍스트 유지)
 1. EndOfTurnEffectAttack.TryOpen (기존 GR-006 — 위치 불변, AS-IS 어택 루프 상당)
 2. pass면 메모리=3 세팅 (기존 CompleteMemoryPassTurn에서 분리 — 전환 전으로 이동)
 3. [End of Turn] 창: OnEndTurn 방출 → 동기 해소(구 턴 상태: IsOwnerTurn/메모리 좌표계/until-효과 모두 살아있음)
 4. 재검사: NonTurnPlayer.MemoryForPlayer >= TurnEndMinMemory(resolved) — 미달 시
    "턴 지속" 결과 반환(Main 잔류; cleanup/전환/리셋 전부 미수행) ← 신규 분기
 5. 충족 시: HeadlessEndTurnCleanupFlow.Cleanup(until-키 소거 + RD-3 버스트 스윕)
 6. OnceFlags.ResetForTurn + PlayerTurnCounters.Reset (AS-IS InitUseCountThisTurn 위치 = 해소 후)
 7. TurnController.EndTurn() + 메모리 플립 → OnStartTurn 방출
```
- 3의 "동기 해소"는 2단계 hotfix 랜딩 후의 스케줄러로 수행(RunToStable 미대기 — EndTurn 액션 내에서 drain). 창-루프(5단계) 랜딩 후엔 자동으로 재진입 의미론 획득.
- 4의 임계는 TODO-67(TurnEndMinMemory 스캔 스코프: 양 플레이어·GetMinMemory 체이닝) 동시 상환 — `HeadlessMainPhaseFlow` 스캔을 양측 population+체이닝 fold로 확장.
- **리스크**: EndTurn 액션의 반환 계약 변화("턴 지속" 결과) — MetadataActionProcessor 소비자(RL 액션 루프)와 기존 테스트(GR-001 MemoryTurnEnd 등) 회귀 확인 필수.
**테스트** `RD6-EndTurnSequence`: ①EoT 창이 구 턴 상태에서 해소(IsOwnerTurn 게이트 효과) ②BT1_021로 메모리 −3 후 임계 미달 → 턴 지속 ③until-턴종료 효과를 EoT 핸들러가 관측 ④EoT 효과 once가 구 턴 장부에 기록.

### D-RD4. 삭제-확정 시퀀스 (진화원 트래시 + PRE 창 정렬) — ✅게이트 오류·미배선 상환(P0-3/P0-4, 2026-07-10); Decode/Partition PRE 이동만 잔여(TODO-96)
**상환 요약(2026-07-10)**: 게이트에서 Save/Fortitude 제거(무조건 트래시=AS-IS). Fortitude 적격성은 `DeletionReplacementGate.SourceCountAtDeletion`(삭제-시점 count 스냅샷)으로 전환 → 소스 트래시돼도 발화·고아 해소. 미배선 2경로(`GameFlowProcessor.RuleProcessAsync:217` PRE-거절 마무리, `SecurityResolver:389` 시큐리티-배틀 패자)에 OnDeleted→소스트래시→top 이동→Fortitude 재생 배선. 통합 census 테스트 `RD4-DeletionWiring`(sink 관통·Fortitude 스냅샷·Decode 보류). 회귀 386/386·RuleAudit 0. **잔여**: Decode/Partition은 여전히 게이트 유지(POST가 None 소스 플레이) — 완전 정합은 PRE 이동(TODO-96).

**구현분(원 주장, step 4의 "안전" 부분)**: 삭제 시 진화원(sourceIds) 트래시를 `DeletionSourceTrash.TrashEvoSourcesAsync`로 미러.
AS-IS `Permanent.DiscardEvoRoots`(CardController.cs:3846, 톱 `AddTrashCard`(:3852) 前) 순서 그대로 **소스 먼저→톱**.
- 배선 2곳: sink `MatchStateMutationSink.ApplyDelete`(톱-트래시 pending 前), `BattleResolver.FinalizeAsync`(phase-2 톱-이동 前).
- `DiscardEvoRoots`는 `AddTrashCard` 직접 호출=`ITrashDigivolutionCards` 아님 → `OnDigivolutionCardDiscarded` **미발화**(gameEventQueue=null).
- ~~**게이트(안전 서브셋 핵심)**: {Save,Decode,Partition,Fortitude} 보유 시 스킵(소스 잔류) — POST 창/재생이 삭제 後 참조하므로~~ **[정정 P0-3, L6 참조]** 게이트는 AS-IS 위반: AS-IS는 무조건 트래시(Save=top-only 이동, Fortitude=삭제-前 스냅샷 판독이라 소스 트래시와 무관). 게이트 제거+POST/재생의 소스 참조를 스냅샷·트래시-존 기반으로 전환해야 함.
- 테스트 `RD4-SourceTrash`(8 checks): 평범 삭제→소스 전부 트래시·스택 비움; ~~Decode/Save/Partition/Fortitude→소스 잔류~~(check 2-5는 발산 동작 고정 — 상환 시 교정); 무소스 no-op. 회귀 384/384, RuleAudit 0.
- **이연(아래 목표의 나머지)**: step 1(PRE RemoveField 브릿지 TODO-97)·step 2(Decode/Partition PRE 이동 TODO-96)·step 3(삭제-직전 스냅샷 TODO-99)·step 4의 ACE-소스 Overflow(TODO-98)·LinkedCards 트래시.
**⚔️점검(2026-07-10)** — 생존: 배선 2곳 내 순서(소스先톱後·Fortitude 최후)·트리거 미발화(AS-IS AddTrashCard도 무발화 확인, CardObjectController.cs:717-735)·`gameEventQueue:null` 가드 실효. 격파:
- **P0-3 게이트=AS-IS 위반**(위 정정 취소선): 4키워드 카드는 수락/거절 불문 트래시 카운트 영구 부족(Save N·Decode N-1·Partition N-2) — POST 거절 시 잔여 소스를 트래시하는 코드 경로가 **전무**(DeletionReplacementTiming:587-591 skip→Mark만). **Fortitude 고아화**: 재생 시 `metadata.Remove(SourceIdsKey)`(DeletionReplacementGate.cs:221)로 소스 인스턴스들이 ChoiceZone.None에 참조 없이 영구 잔류(도달 불가).
- **P0-4 미배선 2경로**: ①PRE-거절 마무리(`RuleProcessAsync:217-228` raw move — Evade/Scapegoat/Decoy 카드가 창 거절 시 소스 유실, 4키워드 무관) ②시큐리티-배틀 패자(`SecurityResolver.cs:396-398` raw move — 소스트래시·cleanup·창 전부 부재; AS-IS는 CardController.cs:4705 동일 삭제 플로우). 커버 확인된 경로: 턴-종료 due 스윕(sink 경유)·TrashNoDpPermanentAsync(자체 이동, 무조건=AS-IS 정합). `DpZeroDeletionHelpers.SweepAsync`는 프로덕션 무호출(테스트 전용)이나 미배선 함정으로 잔존.
- **P1-9 보호필터 밀수**: `TrashSourcesAsync` 경유로 `CanNotTrashFromDigivolutionCards` 필터(ITrashDigivolutionCards 전용 의미론)가 혼입 — AS-IS DiscardEvoRoots는 보호 무시(Permanent.cs:121 직접 호출). producer 0이라 inert; 키워드 포팅 前 무필터 경로로 분리.
- **P1-10 battle knock-out 창 pre-trash 관측**: AS-IS는 [On Deletion] 해소가 소스+톱 트래시 **後**(스택만 :3736) — 헤드리스 F-6.3 2-phase는 창 해소가 트래시 前(RD-4 이전부터의 발산, 배선이 상속). 트래시-카운트 판독 [On Deletion] 효과에서 관측 차이.
- **D-3 테스트 갭**: RD4 테스트는 헬퍼 직접 호출만 — sink/BattleResolver **배선 관통 미검증**. 상환 시 통합 테스트(스택 삭제→트래시 census) 필수.
- **부기(residual)**: 헤드리스 소스 이동은 None→Trash CardMoved 원시 이벤트를 큐에 남김(파생 시맨틱 타이밍 없음 — TriggerTimingMap이 None-출발 무시 확인) + `FaceUp` 미지정(AS-IS SetFace=강제 앞면) — 원시-이벤트 바인딩·faceness 판독 도입 시 재검.

AS-IS 순서(CardController.cs:3690-3900) 미러 목표:
```
would-be-deleted 확정(치환 전부 거절/소진) 시:
 1. PRE 창 잔여: WhenRemoveField-타이밍 카드효과 브릿지(TODO-97) — 기존 PreOptions에
    CustomWouldBeDeletedOption과 나란히 CustomRemoveFieldOption 추가(등록 효과 존재 시)
 2. Decode/Partition을 PRE로 이동(TODO-96): 살아있는 스택에서 소스 선택·플레이
    (CanPlayAsNewPermanent 게이트 추가=TODO-96b; Partition 2장 원자 플레이 — 2장 확정 후 일괄)
 3. 삭제-직전 스냅샷(TODO-99): DP/Level/Cost/CardNames/Traits + permanent 동일성 토큰을
    인스턴스 메타로 기록(JustBeforeRemoveField 계열 — [On Deletion] 게이트 기반)
 4. 확정: DiscardEvoRoots 미러 — 잔여 sourceIds+linkIds 전부 AddToTrashAsync
    (각 소스에 AceOverflowGate 적용=TODO-98, 턴 플레이어 우선 순서) → 톱 트래시 → leave-play cleanup
 5. POST 창: ArmorPurge/Ascension/Save 유지(이들은 AS-IS도 트래시 후) — 단 Save/Ascension이
    집는 소스는 이제 트래시에 있으므로 참조 존 변경
```
- 적용 지점 3곳 동일 패턴: sink `ApplyDelete`(:711-796), `BattleResolver.FinalizeAsync`(2-phase의 phase-2), `DeletionReplacementGate` 경유 삭제.
- 바운스 창(TODO-95=RD 관련 P1)은 같은 프레임워크 재사용: ReturnToHand/Deck sink 앞에 `willBeRemoveField` 창(PreOptions의 ArmorPurge/Fragment/MaterialSave 부분집합) — 별도 커밋.
- **마이그레이션**: Decode/Partition PRE 이동은 기존 POST 테스트(DecoyFortitude/Partition.Tests)를 AS-IS 순서 기준으로 갱신. 소스 잔류(ChoiceZone.None) 전제 코드(DeletionReplacementGate.cs:571-573) 제거.
**테스트** `RD4-DeletionSequence`: 소스 2장 스택 삭제→트래시 3장·순서(소스들→톱); Partition 2장 원자성; Decode가 PRE에서 플레이(ETB가 [On Deletion]보다 先=AS-IS 순서); ACE 소스 Overflow 벌점.

---

## 4단계 — 파이프라인 통합

### D-RD7. 시큐리티 디지몬 배틀 → BattleResolver 공용화
- `BattleResolver`에 defender-as-card 모드 추가: `ResolveSecurityBattleAsync(ctx, attackerId, securityCardId)` — 참가자 추상화 `BattleParticipant`를 "필드 permanent | 시큐리티 카드(DP=CardDP)"로 확장(AS-IS IBattle(attacker, null, DefendingCard) 미러).
- 시큐리티 측 DP = **CardDP 메커니즘**(TODO-71 동시 상환): IChangeCardDPEffect 상당(`securityCardDpDelta` + ChangeSecurityDigimonCardDP 계열)만 fold — permanent-DP 폴드(ContinuousDpGate) 사용 금지. 면역/DPBoost/LinkedDP 미적용(AS-IS CardDP에 없음).
- 공격자 패배 시: 일반 배틀과 동일하게 would-be-deleted 창(Evade/Barrier) → 확정 시 RD-4 시퀀스 → leave-play cleanup. 시큐리티 카드 측은 배틀 결과와 무관하게 기존 체크 플로우(트래시/림보)로.
- OnStartBattle/OnEndBattle/UntilEndBattle 만료/배틀 결과값도 공용 경로가 제공(TODO-79·81의 순서 수정과 결합: 만료는 OnEndBattle 해소 **후**).
- `SecurityResolver.ResolveSecurityDigimonBattleAsync`(:350-400) 제거→위임.
**테스트** `RD7-SecurityBattlePipeline`: Evade 보유 공격자가 시큐리티 배틀 패배→언서스펜드 생존; 패배 공격자의 연속효과 바인딩 소멸; Jamming 공격자 생존(기존 W5 유지).

### D-RD8. 시큐리티 체크 창 순서 원복
`SecurityResolver` 체크 루프 재배열(AS-IS CardController.cs:3928-4210):
```
per check: ①OnSecurityCheck 수집(공개 前 상태, 전역 브로드캐스트 — SourceEntityId 축소 제거,
  수집만 하고 보관) ②공개(+Executing 림보로 이동=TODO-83: Security→Executing 존, 트래시는 말미)
 ③[Security] 효과 해소(다중이면 체크당한 플레이어 순서 선택=TODO-89 — 5단계 창-루프 랜딩 전엔
  단순 반복 choice) ④보관한 OnSecurityCheck/OnLoseSecurity 해소 ⑤시큐리티 디지몬이면 RD-7 배틀
 ⑥Executing에 잔존하면 트래시 ⑦UntilSecurityCheckEnd 만료(TODO-89 신규 duration)
 루프 조건: 매회 Strike 라이브 재평가 + SecurityCards.Count 재확인(TODO-82)
```
- Executing 존: `ChoiceZone.Execution` 신설(AS-IS Root.Execution 상당) — 옵션 해소 림보(TODO-68)와 공유.
- "공개 前 수집"의 substrate: 이벤트에 pre-reveal 스냅샷 플래그를 실어 collector가 게이트 평가 시점을 보존(AS-IS CanTrigger 시점 미러).
**테스트** `RD8-CheckWindowOrder`: [Security]와 OnSecurityCheck 공존 시 [Security] 先; 제3자 OnSecurityCheck 발화; 체크 중 Recovery로 시큐리티 증가 시 추가 체크; 해소 중 트래시 카운트에 공개 카드 미포함.

### D-RD9. 공격 선언 단일 chokepoint
- `AttackDeclarationCommons.DeclareAsync(ctx, attackerId, target, options)` 신설: 선언 검증→서스펜드(RD 관련 TODO-87: sink SuspendKind 경유=OnTappedAnyone 발화·CanSuspend 재필터·DPWhenSuspended 기록)→**OnAllyAttack 단일 창** 방출.
- `AttackPermanentAction`과 `EffectDrivenAttack.Initiate` 모두 이 공통부 호출. `AttackPermanentAction.cs:146-149`의 OnUseAttack/OnDeclaration 발화 제거(TODO-90 발명 창 — OnUseAttack은 AS-IS 死 타이밍; OnDeclaration은 메인 스킬 선언 전용으로 이전). 제거 전 OnDeclaration/OnUseAttack에 바인딩된 기포팅 카드 grep → 있으면 OnAllyAttack로 재타깃(포팅 수정).
**테스트** `RD9-AttackChokepoint`: Vortex/효과-공격에서 [When Attacking] 발화; 공격 서스펜드 시 OnTappedAnyone 발화; OnDeclaration 리스너가 공격에 미발화.

---

## 5단계 — 트리거 창-루프(WindowResolver) 구조 설계 (RD-14~17, TODO-1~4·18)

### 5.1 핵심 구조 (AS-IS MultipleSkills 미러)
**신규**: `Effects/TriggerWindow.cs` + `Effects/WindowResolver.cs` — 배치(수집→정렬→FIFO)를 **재진입 창 루프**로 대체:
```
WindowResolver.RunWindowAsync(ctx, seedEvents, depth):
  stack = Collect(seedEvents)                       // 이벤트별 SkillInfo (RD-11 반영)
  while (true):
    active = stack.Where(s => Gate(s))              // 매 반복 재평가: CanResolve+IsDisabled(TODO-13)
                                                    //  +스냅샷 lapse 게이트(TODO-12/11 인프라, 후속)
    if (active.Empty) break
    turnSide = active.Where(owner == TurnPlayer)    // AS-IS: 턴P 창 전부 → 비턴P (RD-15)
    side = turnSide.Any() ? turnSide : active
    pick = side.Count == 1 ? side[0]
         : await ChooseAsync(side.Player, side,     // RD-14: 플레이어 순서 선택 (의무+선택 혼합 제시)
             canSkip: side.All(s => s.IsOptional))  // 전원 optional일 때만 "활성화 안 함"(전량 소거)
    if (pick == Skip) { stack.RemoveAll(side); continue }   // AS-IS 342-345
    if (pick.IsOptional) confirm = await YesNo(...)          // RD-13과 공유
    result = await ResolveOne(pick)                 // 성공 시 once 소모(RD-12)
    stack.Remove(pick)                              // Gate-fizzle이면 그냥 제거(RD-10)
    newEvents = ctx.GameEventQueue.DrainPending()
    if (newEvents.Any() && depth < ChainLimit)
      await RunWindowAsync(ctx, newEvents, depth+1) // RD-17: 컷인 재귀 — 신규 트리거 우선
    // 루프 헤드로 → 잔여 재평가·재제시 (RD-16: optional 잔여 소실 해소)
```
### 5.2 대체·통합 대상
- `GameFlowProcessor.AutoProcessAsync`의 "drain→collect→EnqueueOrdered→ResolveAllAsync→optional 프롬프트" 시퀀스 → `WindowResolver.RunWindowAsync` 1회 호출로 대체. `MandatoryEffectOrdering`(고정 정렬)·`OptionalPromptQueue`(maxCount:1 소진) 폐기 경로 — 창-루프의 ChooseAsync가 두 역할 통합.
- `EffectScheduler`는 단건 해소기(ResolveOne의 하부)로 축소 유지 — 큐잉 의미론은 창이 소유.
- 동기 창 호출부(knock-out/start-battle/시큐리티/EndTurn)는 `RunWindowAsync(subject-scoped seed)`로 일원화 — 컷인 재귀·순서선택이 전 창에 균일 적용.
- 컷인 상한·중복 방지(TODO-18): `depth >= ChainLimit`(AS-IS ChainActivations 미러) + 창 내 `HasExecutedSameEffect` skip-집합.
### 5.3 RL 액션 표면
ChooseAsync/YesNo는 기존 ChoiceRequest 프로토콜 재사용(Type=EffectOrder/OptionalUse). 결정 지점 증가는 RL 관측·재현성에 영향 → seed-고정 리플레이 테스트(L4 매치로그)로 창 결정 시퀀스 직렬화 확인.
### 5.4 마이그레이션·리스크
- **최대 회귀 표면**: 기존 375+ 테스트 중 트리거 순서를 암묵 전제한 것들 — 단계적 랜딩: ①WindowResolver를 기존 배치와 결과 비교하는 shadow 모드(순서 결정이 1개뿐인 창은 동작 동일) ②단일-트리거 창부터 전환 ③다중-트리거 창 전환+테스트 갱신.
- once-키 정밀도(TODO-14)·수집 population(TODO-10)·lapse 스냅샷(TODO-12)은 창-루프 위에 얹는 후속 — 본 설계의 Gate/Collect 심에 삽입 지점을 남겨둠.

---

## 6. 테스트·검증 전략
- 항목별 신규 동작-단언 테스트(위 명세) + 전체 회귀 + RuleAudit(20게임) 매 커밋.
- RuleAudit에 룰 검사 추가 제안: 진화-드로우 카운트 일치, 옵션 색 위반 0, 시큐리티 배틀 후 바인딩 잔존 0 — 상환이 실제 게임 루프에서 유지되는지 상시 감시.
- 5단계는 shadow-모드 비교 리포트(창 결정 로그 diff)로 등가성 확인 후 컷오버.
- **⚔️점검 교훈(2026-07-10) — 테스트 작성 원칙 추가**:
  1. **관통(라이브-경로) 원칙**: 수정 계층을 우회하는 목/람다 주입 테스트 금지 — 실제 프로덕션 배선(EngineContext 기본 체인)을 관통해야 "수정 작동"의 증거(P0-1이 tautology 테스트 뒤에 숨었음).
  2. **AS-IS 기대값 원칙**: 테스트 기대값은 구현이 아니라 AS-IS에서 도출·출처 주석 필수(RD11 테스트가 反AS-IS +2를 고정했음). 기대값 도출 근거(AS-IS file:line)를 테스트 헤더에 기록.
  3. **린트-가드 인지**: 6개 테스트가 `MetadataActionProcessor.cs` 소스의 "TODO" 문자열을 스캔 — 이 파일 주석에 TODO-번호 참조 금지(설계 문서 번호는 "T-67"식 표기 또는 문서 참조로 대체). 테스트 실패 진단 시 **실패 단언의 실체 확인 先**(어느 Check가, 왜 — 행동 실패와 린트 실패 구분).
  4. **삭제-경로 census 테스트**: 삭제류 상환은 "최종 트래시 내용물 전수 카운트" 단언을 표준 포함(P0-4류 미배선을 자동 검출).

## 7. 순서 의존성 요약
```
1단계(RD-1/2/3/5) ── 독립, 병렬 가능
2단계(RD-10~13) ── 독립 hotfix, 단 RD-12↔RD-13은 once-소모 규약 공유(같은 커밋 권장)
3단계 RD-6 ← 2단계(EndTurn 내 동기 창 해소가 wedge-free 전제) · RD-3와 순서 접점
3단계 RD-4 ← 없음(독립) · TODO-95 바운스 창은 RD-4 프레임 재사용
⚔️P0-1~4(적대 점검) ← 없음(즉시) — 4단계·후속 포팅보다 先行(P0-1은 2단계 RD-10의 전제 복구, P0-3/4는 RD-7의 전제)
4단계 RD-7 ← RD-4(삭제 시퀀스 공용, P0-3/P0-4 상환 포함) · TODO-71(CardDP) 동시
4단계 RD-8 ← RD-7(배틀 위임) · Executing 존은 TODO-68(옵션)과 공유
4단계 RD-9 ← 독립(TODO-87·90 동시 상환)
5단계 창-루프 ← 2단계 hotfix 선랜딩 필수(재설계 시 자연 흡수), 3·4단계의 동기 창 호출부가 이후 일원화 대상
```
