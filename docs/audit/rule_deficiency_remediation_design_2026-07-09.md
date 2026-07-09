# 라이브 룰 결손 상환 설계 (RD-1~17)

입력: `rule_deficiency_2026-07-09.md`(룰 결손 명세) · `fidelity_todo_2026-07-09.md`(TODO 113). 원칙: [[check-asis-before-implementing]](착수 시 AS-IS 지점 재확인 후 미러), [[result-equivalence-not-completion]](구조 1:1, substrate 번역만 허용), 게이트=green+동작단언+RuleAudit 0. 각 항목은 독립 커밋 단위로 설계했고, 단계 간 의존성은 §7에 집약.

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

## 2단계 — 트리거 hotfix (창-루프 재설계 前 선행 가능)

### D-RD10. fizzle = skip (wedge 제거)
- `EffectScheduler.ResolveNextAsync`: 결과 분류 3종으로 — ①`Resolved` → dequeue ②**`GateFailed`(CanResolve-실패)** → **dequeue + skip 기록**(AS-IS MultipleSkills.cs:122-126 continue 미러) ③`Error`(리졸버 예외/불변 위반) → 현행 유지(비-dequeue, 진단 보존).
- 구현: `EffectResult`에 `FailureKind { Gate, Error }` 추가; `HeadlessCardEffectContract`(:274-282)의 CanResolve-실패 반환을 Gate로 태깅. `ResolveAllAsync`는 Gate면 continue, Error면 break.
**테스트** `RD10-FizzleSkip`: 선행 해소가 후행 조건을 무너뜨리는 2-트리거 배치 — 후행 skip + 큐 소진; Error는 여전히 블록.

### D-RD11. 이벤트별 발화 (dedupe 키 확장)
- `GameFlowProcessor.AutoProcessAsync`(:438-451): `seen`을 `HashSet<(HeadlessEntityId effectId, long eventSeq)>`로 — **같은 이벤트 내** 중복 수집만 차단, 이벤트별로는 각각 enqueue(각자의 enriched subject 유지 = AS-IS 이벤트별 SkillInfo/hashtable).
- once-cap과의 상호작용: 캡 소비는 RD-12에 따라 실행 시점이므로 여기서 N회 enqueue돼도 캡이 M<N이면 실행 시 잘림(AS-IS 동일).
**테스트** `RD11-PerEventFire`: 한 pass 2건 삭제 → "삭제될 때마다" 효과 2회 발화, 각 발화의 subject가 해당 삭제 카드.

### D-RD12. once-per-turn 소모를 실행 시점으로
- 수집 루프(:479-483)의 `OnceFlags.TryActivate` → `OnceFlags.CanActivate`(비소모 사전 필터, 캡 초과분 수집 제외는 유지)로 교체.
- 소모 지점: `EffectScheduler` 해소 성공 직후 + `OptionalPromptQueue.ResolveChoice`의 **선택된** 트리거 enqueue 시(거절분 미소모). AS-IS `UseOptional || !IsOptional` 게이트(ICardEffect.cs:1118-1121) 미러.
- 키는 enriched request에 실어 전달(수집 시 계산한 (effectId,source,owner) 키 재사용 — TODO-14 키 정밀도는 별건, 여기선 시점만).
**테스트** `RD12-OnceOnExecute`: 캡1 optional 거절→같은 턴 재트리거 가능; 수락→불가; Gate-fizzle→미소모.

### D-RD13. IsOptional yes/no 게이트
- `ActivatedEffectResolver.ResolveAsync` 진입부: `effect.IsOptional`이면 body 구동 전 yes/no ChoiceRequest(AS-IS OptionalSkill "Will you use ~?" 미러; RL 표면 = 2후보 choice). 거절 시 `EffectResult.Skipped`(once 미소모 — RD-12와 정합).
- 브릿지/스케줄러 경로: `TimingWindowTriggerKind.Optional` 재분류는 기존대로 프롬프트 경유 — 이 게이트는 **비대화형 body의 직접 해소 경로**(PlayCardAction/DigivolveAction/OptionActivate가 부르는 resolver) 전용. 이중 질문 방지: 프롬프트 경유로 이미 수락된 요청에는 `optionalAccepted` 마커를 실어 게이트 스킵.
**테스트** `RD13-OptionalPrompt`: 비대화형 optional(메모리+1) — 거절 시 무변화, 수락 시 +1; 프롬프트 경유 시 단일 질문.

---

## 3단계 — 시퀀스 재설계

### D-RD6. 턴 종료 시퀀스 (MetadataActionProcessor.EndTurn 재배열)
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

### D-RD4. 삭제-확정 시퀀스 (진화원 트래시 + PRE 창 정렬)
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

## 7. 순서 의존성 요약
```
1단계(RD-1/2/3/5) ── 독립, 병렬 가능
2단계(RD-10~13) ── 독립 hotfix, 단 RD-12↔RD-13은 once-소모 규약 공유(같은 커밋 권장)
3단계 RD-6 ← 2단계(EndTurn 내 동기 창 해소가 wedge-free 전제) · RD-3와 순서 접점
3단계 RD-4 ← 없음(독립) · TODO-95 바운스 창은 RD-4 프레임 재사용
4단계 RD-7 ← RD-4(삭제 시퀀스 공용) · TODO-71(CardDP) 동시
4단계 RD-8 ← RD-7(배틀 위임) · Executing 존은 TODO-68(옵션)과 공유
4단계 RD-9 ← 독립(TODO-87·90 동시 상환)
5단계 창-루프 ← 2단계 hotfix 선랜딩 필수(재설계 시 자연 흡수), 3·4단계의 동기 창 호출부가 이후 일원화 대상
```
