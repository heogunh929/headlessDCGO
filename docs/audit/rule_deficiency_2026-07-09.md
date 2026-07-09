# 라이브 룰 결손 명세 (2026-07-09 전면 점검)

기반: `docs/audit/fidelity_todo_2026-07-09.md`(TODO 113건) 중 **현재 카드풀·현재 게임 루프에서 즉시 발현하는 게임-룰 결손**만 추려 상세 명세. 각 항목은 Opus가 AS-IS(`DCGO/`)·헤드리스 양측 코드로 직접 검증 완료([검증] 표기). latent(트리거 카드 미포팅) 항목은 본 문서 범위 밖 — TODO 문서 참조.

분류: **A = 기본 게임 규칙 미집행**(룰북 수준 규칙이 엔진에 없음), **B = 룰 처리 순서/시점 발산**(규칙은 있으나 AS-IS와 다른 시점·순서로 집행), **C = 트리거 해소 시맨틱스 결손**(효과 해소 규칙 자체의 발산).

---

## A. 기본 게임 규칙 미집행

### RD-1. 진화 시 1드로우 부재 [검증] (TODO-43)
- **룰**: 디지볼브 성공 시 그 플레이어는 카드 1장을 드로우한다(일반/조그레스/버스트/앱퓨전 공통).
- **AS-IS**: CardController.cs:1526-1529 — `if (isEvolution) { DigivolveCount_ThisTurn++; yield return new DrawClass(card.Owner, 1, null).Draw(); }`. 모든 진화 경로가 이 단일 지점을 통과.
- **헤드리스**: DigivolveAction.cs:263-283 — 카운터 증가만. DigivolveAction/SpecialPlayAction/FreeDigivolveHelpers/FusionDigivolveHelpers + 런타임 전체에 진화-드로우 `DrawAsync` 호출 0건(직접 grep). 기존 테스트 어느 것도 진화 후 손패 수를 단언하지 않아 회귀에 안 잡혔음.
- **관측 발산**: 매 진화마다 손패 −1, 덱 +1 상태 유지 → 손패 자원 곡선·덱아웃 타이밍·"드로우할 때" 트리거(OnDraw, RD-부속: TODO-40) 전부 어긋남. RL 학습 관점에서 진화 가치가 실제보다 낮게 학습됨.
- **상환**: 진화 확정(스택 이동 완료) 지점에 공통 드로우 삽입 — AS-IS와 동일하게 모든 진화 변형이 지나는 단일 chokepoint에. DrawClass 경유이므로 덱아웃 판정·OnDraw 방출(TODO-40 상환 시)과 결합. 테스트: 일반/조그레스/버스트 각 1건 손패+1 단언.

### RD-2. 옵션 색 요건(MatchColorRequirement) 게이트 전무 [검증] (TODO-42)
- **룰**: 옵션 카드는 그 카드의 모든 색이 자기 필드(배틀에리어+브리딩)의 디지몬/테이머 색에 존재해야 사용 가능. "색 조건 무시" 효과(IIgnoreColorConditionEffect)로만 면제.
- **AS-IS**: CardSource.cs:184-249 `CanNotPlayThisOption` — ①ICanNotPlayCardEffect 3-region 스캔(플레이어→필드 퍼머넌트→자기; AS-IS 특이하게 플레이어 우선 순서) ②`if (!MatchColorRequirement) return true`(:240-245). MatchColorRequirement(:255-321) = 옵션 전 색 ⊆ 소유자 필드 톱카드 색 합집합. `CanPlayFromHandDuringMainPhase`(:158-174)가 이 getter를 경유 = 손패 옵션 합법성의 필수 관문.
- **헤드리스**: OptionActivateAction.Validate(:166-251)에 색 검사 0(전 src `MatchColorRequirement|colorRequirement` grep 0건). 제약은 정적 메타 플래그(`canNotPlayThisOption`)만(:300-306) — 동적 효과-스캔 아님.
- **관측 발산**: 색 불일치 옵션이 전부 합법 — 예: 필드가 적색뿐인데 청색 옵션 사용 가능. 포팅된 모든 옵션 카드의 합법 액션 집합이 과대. RL 정책이 불법 수를 학습.
- **상환**: ①MatchColorRequirement 미러 — population(배틀에리어+브리딩 톱카드), 색 합집합 비교, ignore-color 연속효과 게이트(negation 게이트 P1-DV-2와 일관). ②ICanNotPlayCardEffect 연속 스캔 인프라(TODO-49와 공유). 테스트: 색 일치/불일치/무시효과 3건 + 테이머 색 포함 확인.

### RD-3. 버스트 진화의 임시성(턴 종료 시 톱카드 트래시) 부재 [검증] (TODO-45)
- **룰**: 버스트 디지볼브는 임시 진화 — 턴 종료 시 버스트로 올린 톱카드를 트래시하고 원래 형태로 되돌린다.
- **AS-IS**: CardController.cs:1531-1538 — 버스트 성공 시 `permanent.IsBurstDigivolved = true` + `selectBurstDigivolutionEffect.AddTrashTopCardAtTurnEnd(permanent)`.
- **헤드리스**: SpecialPlayAction.cs:347-361 — 테이머 손패 반환 + 무료 진화만 수행, 영구 진화로 잔존. `BurstDigivolved|TrashTopCardAtTurnEnd` 전 src 0건(직접 grep). PRIM.BurstDigivolve.Tests도 턴 종료 미커버.
- **관측 발산**: 버스트 진화체가 영구히 유지 — 버스트의 리스크/리턴 구조 소멸.
- **상환**: 버스트 성공 시 인스턴스 마커 기록 + HeadlessEndTurnCleanupFlow에 톱카드 트래시(트래시는 DeDigivolve 유사 경로 아닌 AS-IS "톱만 트래시" 의미론) 추가. 단 RD-5(턴 종료 시퀀스)와 순서 정합 필요 — [End of Turn] 창 해소 후 cleanup 단계에서. 테스트: 버스트→턴 종료→톱 트래시+이전 형태 복귀.

### RD-4. 삭제 확정 시 진화원 트래시(DiscardEvoRoots) 누락 [검증] (TODO-93)
- **룰**: 퍼머넌트가 삭제되면 톱카드와 **모든 진화원(및 링크 카드)**이 트래시로 간다.
- **AS-IS**: CardController.cs:3846 — 톱 트래시 직전 `permanent.DiscardEvoRoots()`(Permanent.cs:106-142: evoRoots·linkRoots 전부 AddTrashCard, 내장 ACE Overflow 포함).
- **헤드리스**: MatchStateMutationSink.cs:790·BattleResolver.cs:192-194 — **톱카드만** 트래시. 소스 인스턴스들은 `ChoiceZone.None`에 영구 잔류(DeletionReplacementGate.cs:571-573이 POST Decode/Save/Partition이 소스를 집을 수 있도록 의도적으로 남긴 구조).
- **관측 발산**: 트래시 매수가 실제보다 작음 → §4 트래시-카운트 술어("트래시가 10장 이상이면"), 트래시-쿼리 select 효과("트래시에서 ~를 고른다"), "카드가 트래시에 놓일 때" 트리거의 모집단 전부 오류. 진화원 많은 스택일수록 편차 큼.
- **상환**: 삭제-확정 시퀀스 재설계(TODO-96과 한 묶음) — AS-IS 순서 미러: PRE 창(would-be-deleted/WhenRemoveField)에서 Decode/Partition류가 소스를 **사용**하고, 확정 시 잔여 소스 전부 트래시(ACE Overflow 스택 전체 적용=TODO-98 동시 상환). POST-창 모델을 유지한다면 최소한 POST 종료 시점에 잔여 소스 일괄 트래시. 테스트: 소스 2장 스택 삭제→트래시 3장 단언.

### RD-5. Scapegoat 제물 후보에 아군-디지몬 한정 누락 [검증] (TODO-94)
- **룰**: Scapegoat의 대체 제물은 자기 배틀에리어의 **디지몬**(자신 제외)만.
- **AS-IS**: CardEffectFactory/KeyWordEffects/Scapegoat.cs:53 — `IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) && permanent != holder`.
- **헤드리스**: DeletionReplacementGate.cs:492-526 — battleArea 전 카드에서 `!= holder`(+CannotBeDeleted)만 필터 → **테이머·필드 옵션도 제물 지정 가능**.
- **관측 발산**: Scapegoat 사용 시 불법 제물 선택지 노출(선택하면 테이머가 삭제됨).
- **상환**: 후보 필터에 `ContinuousKeywordGate.IsDigimon`(TreatAsDigimon 포함 = AS-IS Permanent.IsDigimon chokepoint) 추가. 부수: 자기-효과-삭제 불발 게이트(TODO-102)도 같은 파일 — 동시 상환 권장.

---

## B. 룰 처리 순서/시점 발산

### RD-6. [End of Turn] 창 시점 역전 + 턴종료 취소(Main 복귀) 부재 [검증] (TODO-44, +TODO-11 once 리셋)
- **룰**: 턴 종료 선언 → (아직 구 턴인 상태로) 패스 메모리 세팅 → [End of Turn] 효과 해소 → **상대 메모리 재검사: 임계 미달이면 턴이 끝나지 않고 Main으로 복귀** → until-턴종료 정리·once 카운터 리셋 → 턴 전환.
- **AS-IS**: AutoProcessing.cs:675-727(EndTurnProcess: pass 메모리=3 → OnEndTurn 스택·해소 → `NonTurnPlayer.MemoryForPlayer >= TurnEndMinMemory` 재검사 → 미달 시 `SetMainPhase()` 복귀) → TurnStateMachine.cs:3151-3210(EndPhase: until-정리·InitUseCountThisTurn) → 전환.
- **헤드리스**: MetadataActionProcessor.cs:783-834 — cleanup(until-키 소거)→턴 전환→메모리 플립을 **먼저** 하고, OnEndTurn은 그 뒤 방출(해소는 다음 drain = 이미 새 턴 상태), OnceFlags.ResetForTurn도 방출 직후·해소 전. 재검사/Main 복귀 경로 없음.
- **관측 발산**: ①[End of Turn] 효과가 새 턴 컨텍스트에서 해소 — `IsOwnerTurn` 게이트 역평가, 메모리 부호 좌표계 반전, until-턴종료 효과를 관측 불가(이미 소거됨). ②EoT 효과로 메모리가 임계 아래로 돌아와도 턴이 끝나버림(AS-IS는 턴 지속). ③EoT 효과의 once-per-turn 소비가 새 턴 장부에 기록. **BT1_021(EoTLose3Memory) 기포팅 = 현재 오동작 경로 위에 있음.**
- **상환**: EndTurn 시퀀스 재배열 — 구 턴 컨텍스트에서 [End of Turn] 창 동기 해소 → 메모리 재검사(TurnEndMinMemory, TODO-67의 스코프 확장과 결합) → 미달 시 Main 잔류 반환 → 충족 시 cleanup→once 리셋→전환→OnStartTurn. 버스트 트래시(RD-3)·Vortex 창(기존 GR-006)과 같은 단계 그래프에 배치. 테스트: EoT 메모리-회복 카드로 턴 지속 단언 + IsOwnerTurn 게이트 EoT 효과.

### RD-7. 시큐리티 디지몬 배틀이 삭제 파이프라인 전체 우회 [보고→구조 확인] (TODO-46)
- **룰**: 시큐리티에서 공개된 디지몬과의 배틀도 일반 배틀 — 패자 삭제는 삭제 룰 전체(치환 창, 삭제 트리거, leave-play 정리)를 따른다.
- **AS-IS**: CardController.cs:4179 — 완전한 `IBattle(attacker, null, DefendingCard).Battle()` → DestroyPermanentsClass(:4705): would-be-deleted 치환(Evade/Barrier), OnStartBattle/OnEndBattle 창, 배틀 해시테이블, Pierce 판정(:4731)까지 일반 배틀과 동일.
- **헤드리스**: SecurityResolver.cs:350-400 — 인라인 DP 비교 후 `MoveAsync(BattleArea→Trash)` 직행. 치환 창·CardLeavePlayCleanup(**연속효과 바인딩 미해제**)·삭제 트리거·Fortitude·UntilEndBattle 만료·배틀 결과값 전무.
- **관측 발산**: 시큐리티 배틀 패배 공격자가 Evade/Barrier로 살 수 없음; 죽은 공격자의 연속효과가 레지스트리에 잔존(과잉 지속); [On Deletion]류 미발화.
- **상환**: 시큐리티 배틀을 BattleResolver 공용 경로로 통합(defender=CardSource 형태 지원 — AS-IS IBattle의 DefendingCard 인자 미러). RD-8(체크 창 순서)과 함께 SecurityResolver 재설계 단위.

### RD-8. 시큐리티 체크 내부 창 순서 역전 + OnSecurityCheck 모집단 축소 [보고] (TODO-47)
- **룰**: 시큐리티 1장 체크 = OnSecurityCheck 트리거를 공개 **전 상태 기준으로 수집**(보관) → 공개 → **[Security] 효과 먼저 해소** → 보관한 OnSecurityCheck/OnLoseSecurity 해소 → (디지몬이면) 시큐리티 배틀.
- **AS-IS**: CardController.cs:3954(수집·보관, **전역 스캔** — 필드·플레이어 전체) → :3987-4103([Security] 해소, 다중이면 체크당한 플레이어가 순서 선택) → :4111-4117(보관분 스택·해소) → :4121(배틀).
- **헤드리스**: SecurityResolver.cs:138 — OnSecurityCheck를 **먼저** 해소 + `SourceEntityId=공개카드`로 자기-스코프 축소(collector:309-313이 타 소스 드롭) → :144 [Security]를 **나중** 해소.
- **관측 발산**: 두 창의 순서가 정반대(공존 시 결과 상이); 제3자(공격자/테이머)의 "시큐리티가 체크될 때" 트리거가 영구 미발화.
- **상환**: 순서 원복 + OnSecurityCheck 브로드캐스트화. 공개 시큐리티 카드의 Executing 림보(TODO-83)·다중 [Security] 순서 선택(TODO-89)과 같은 재설계 단위.

### RD-9. 효과-기인 공격이 [When Attacking] 창 미발화 [보고] (TODO-48)
- **룰**: 효과로 유발된 공격(Vortex/Execute/Overclock/"이 디지몬으로 공격한다"류)도 공격 선언 룰 전체를 따른다 — [When Attacking]/상대 대응 트리거 발화 포함.
- **AS-IS**: 모든 공격이 AttackProcess.Attack() 단일 진입(:73), :197-199에서 OnAllyAttack 스택. SelectAttackEffect.cs:543(효과 공격)도 동일 코루틴.
- **헤드리스**: EffectDrivenAttack.Initiate(:184-211)는 AttackController.DeclareAttack만 — OnAttack/OnAllyAttack 발화는 수동 선언 액션(AttackPermanentAction.cs:146-149)에만 존재.
- **관측 발산**: Vortex/Execute/EndOfTurn 공격에서 "[어택 시]" 계열 트리거 전부 침묵.
- **상환**: 공격 선언을 단일 chokepoint로 통일(수동/효과 공격이 같은 발화 지점 경유). 동시에 발명 창 2개(OnUseAttack/OnDeclaration 3중 발화, TODO-90)를 제거해 AS-IS의 "OnAllyAttack 하나" 구조로 정렬.

---

## C. 트리거 해소 시맨틱스 결손 (TODO-1~8; 공통 근원 = 창 재평가 루프 소실)

AS-IS의 해소 루프는 "스택 → 매 해소마다 재검사·재선택·중첩 재귀"(MultipleSkills.cs)의 **재진입 구조**. 헤드리스는 "일괄 수집 → 고정 정렬 → FIFO 소진"의 **배치 구조**. 아래 8건은 이 구조 차이의 표면들로, RD-10~12는 구조 재설계 전에도 국소 hotfix 가능.

### RD-10. 해소 시점 게이트 실패 = 큐 영구 블로킹(wedge) [검증] (TODO-7)
- **룰**: 해소 시점에 조건을 잃은 트리거는 불발(skip)하고 창은 진행한다.
- AS-IS: MultipleSkills.cs:122-126 continue. 헤드리스: EffectScheduler.cs:89-93 — Failure 시 dequeue 안 함 → 큐 헤드 영구 잔류, **후속 트리거 전부 미해소**.
- 상환(hotfix 가능): CanResolve-실패 Failure는 dequeue+skip. 리졸버 실오류와 구분 유지.

### RD-11. pass 내 다중 이벤트 EffectId dedupe — 다회 발화 소실 [검증] (TODO-8)
- **룰**: "~할 때마다" 효과는 한 창에서 사건 수만큼 발화한다(사건별 컨텍스트로).
- AS-IS: 이벤트별 SkillInfo 스택(AutoProcessing.cs:984-989). 헤드리스: GameFlowProcessor.cs:438-451 — pendingEvents 전체에 걸친 EffectId dedupe → 1회만, 첫 이벤트 subject만.
- 상환(hotfix 가능): dedupe 키를 (EffectId × 이벤트)로.

### RD-12. once-per-turn 소모 시점: 실행 시가 아닌 수집 시 [검증] (TODO-5)
- **룰**: 1턴 1회 사용은 효과가 **실제 실행**될 때 소모(거절/불발 시 미소모).
- AS-IS: ICardEffect.cs:1118-1121 — `UseOptional || !IsOptional` 확인 후 등록. 헤드리스: GameFlowProcessor.cs:479-483 — 수집 시 TryActivate(거절/Failure에도 소모).
- 상환(hotfix 가능): 소모를 해소 성공 시점으로 이동.

### RD-13. 비대화형 optional 효과 무프롬프트 강제 실행 [검증] (TODO-6)
- **룰**: "~해도 된다" 효과는 실행 직전 사용 여부를 묻는다.
- AS-IS: OptionalSkill.cs:14-132 전 IsOptional에 yes/no. 헤드리스: ActivatedEffect.IsOptional 死필드(resolver 미참조 — 직접 확인).
- 상환: ActivatedEffectResolver/브릿지에 yes/no 게이트.

### RD-14~16. 창 구조 3건 — 순서선택권·창 순서·재제시 [검증] (TODO-1·2·3)
- **룰**: 동시 트리거는 **턴 플레이어의 (의무+선택) 전부 → 비턴 플레이어** 순이며, 각 플레이어 창 안에서 처리 순서는 그 플레이어가 선택하고, optional은 하나 해소 후 잔여가 재제시된다.
- 헤드리스: 고정 정렬(양측 의무 전부→선택), 순서 선택권 없음, optional 1개 선택 시 잔여 소실.
- 상환: 창-루프 재설계 단위(개별 패치 불가) — RD-17과 함께.

### RD-17. 중첩(컷인) 해소 순서 역전 [검증] (TODO-4, +TODO-18 컷인 상한)
- **룰**: 효과 해소 중 새로 트리거된 효과는 **남은 스택보다 먼저** 해소된다(재귀 컷인).
- AS-IS: MultipleSkills.cs:397-415 재귀 + 매 효과 후 재스택. 헤드리스: 현행 배치 소진 후 다음 pass 수집(뒤로 밀림) + 조건 평가 지연.
- 상환: 창-루프 재설계의 핵심 — 해소 직후 재수집·우선 삽입(또는 재귀 창).

---

## 상환 순서 제안

| 단계 | 항목 | 성격 |
|---|---|---|
| 1 (즉시, 국소) | RD-1 진화드로우 · RD-2 옵션색요건 · RD-3 버스트 · RD-5 Scapegoat | 독립 패치 + 단언 테스트 |
| 2 (hotfix) | RD-10 wedge · RD-11 dedupe · RD-12 once소모 · RD-13 IsOptional | 트리거 구조 재설계 전 선행 가능 |
| 3 (시퀀스 재설계) | RD-6 턴종료 · RD-4 삭제-확정(+TODO-95~103) | 단계 그래프 재배열 |
| 4 (파이프라인 통합) | RD-7 시큐리티배틀 · RD-8 체크 창 · RD-9 공격 chokepoint | BattleResolver/SecurityResolver/공격 진입 통일 |
| 5 (구조 설계) | RD-14~17 창-루프 (TODO-1~4·18) | 별도 설계 문서 → 일괄 구현 |

각 상환은 [[check-asis-before-implementing]] — 착수 전 해당 AS-IS 지점 재확인 후 미러. 검증 게이트: green + 동작-단언 테스트 + RuleAudit 0.

**상환 설계**: `docs/audit/rule_deficiency_remediation_design_2026-07-09.md` — RD-1~17 전 항목의 구체 설계(신규/변경 컴포넌트·삽입 지점·의사코드·테스트·마이그레이션) + 5.x WindowResolver 창-루프 구조 + §7 순서 의존성 그래프.
