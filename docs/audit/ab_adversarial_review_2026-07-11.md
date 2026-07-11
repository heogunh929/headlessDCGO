# A/B군 적대 리뷰 결과 (2026-07-11)

> **상환 현황(2026-07-11, 같은 날 진행)** — 하단 "권고 상환 순서" 기준:
> 1. ✅ 소모 파이프라인 재작업 완료: consume-before-body 복원(창 경로=optional 수락 후·body 前 / 선언 경로=optional 前, declarative flavor) + **OnceFlags uniform-cycle 트랜잭션**(staged pending + per-key replay cursor — suspend 시 staged 유지, resume replay 시 재소모/자기-cap-차단 없음, 완료 시 1회 커밋) + **mutation replay 저널**(즉시-적용 mutation의 replay 이중적용 차단 — P0-3의 실제 형태; sink.Apply 래핑 + EmitJournaled로 OnUseOption/OnUseDigiburst 재-emit도 차단) + B-4 환불을 `refundWhenNotExecuted`/`executedPredicate` per-card opt-in으로 강등(기본=AS-IS 소모 유지). 테스트: B1 4건(기본 무환불·opt-in 환불·다중-효과 suspend 1회-적용) + B2 declarative-decline cap 소각.
> 2. ✅ MarkerGate 술어 분리: `CanActivateAt`=canActivate-半+cap(per-pass), 신규 `CanCollectAt`=canUse-半+cap(수집 1회, CollectActivatedBridgeTriggers 배선), 창-디스패치 resolver 최종 게이트=activate-半만(windowDispatched flavor). 테스트: B5 SplitGateHalves.
> 3. ✅ B-3 tuck 리셋: AddSourcesBottom/TopAsync·AddLinkCardAsync·FuseAsync·MoveSourcesBottom(MindLink)에 onceFlags 배선(MindLink/DNA/DigiXros/Save/Assembly/링크 경로). 존-진입·UseOption 리셋은 debt로 명시(OnceFlagHelpers doc). 테스트: B3 tuck/link 2건.
> 4. ✅ A-1: 동치키를 소스카드-단위 collapse로(AS-IS 빈-HashString 동일시; SetHashString 미러=design item RDx-A1-HASH), Resolved 수명을 프레임-스코프(live 프레임 합산, pop 시 망각)로. 테스트: Stage5-WindowResolver #11/#12.
> 5. ✅ B-2 DigiBurst: 컨트롤러가 버릴 진화원 선택(bottom-N 자동 제거) + OnUseDigiburst 타이밍 신설·트래시 前 emit + 선택-트래시 sink 경로(SelectedCardIdsKey→TrashSpecificSourcesAsync). up-to 변형=design item B2-UPTO. 테스트: B2 DigiBurstPaysWithSelectedSources. **추가**: BT1_088/089 [Main] 실 포팅 완료(STOP 해소 — 선언 액션이 B-2로 실재하므로; BT1.StopRemainder 4 테스트).
> 6. ✅ B-5 래퍼 게이트 7장 canUse/canActivate 이관 + BT1_044 후보군(자기스택+Lv≤4) + ST2_14 계열 resolver case — 상세는 하단 항목별 결과 참조.
> 7. ✅ 문서/주석 정정(WindowResolver PHASE-2 모순, F3 예외 메시지 AS-IS 귀속 제거, ResetForCard 커버리지, IsCutInEffectUsedMaxCount 死경로 근거).
> **미상환(명시 이연)**: A-2 P1-1(Vortex/Execute 선언의 EoT 창 order-choice 멤버십 — EoT-창 멤버 재설계 필요, EX8_074+BT1_021 공존 시 라이브), A-2 P1-2(#67 player-scope 티어+종속 fold — producer 0 latent), A-2 P2×4, A-1 P2(재진입 mainStack=true skipCondition 강등 표현·컷인 15곳 인벤토리), B-2 P2(AfterEffectsActivate 창·비-uniform 폴백 게이트·B2-05), A-3 P1(비-uniform per-pass always-true — B-5 이관으로 축소 중), 존-진입 리셋(위 3). 각각 AS-IS 앵커는 본문 참조.
>
> **상환 라운드 자체에 대한 독립 적대 재검수(2026-07-11, reviewer 2개)** — P0 0건 · P1 다수 → **2차 수정 반영**:
> - ✅ DigiBurst 호스트-스코프 `ImmuneFromStackTrashing` 게이트(양 reviewer 독립 발견 — AS-IS CanDigiBurst 첫 검사 :2141): `CanDeclareAt`+resolver 게이트에 `RestrictionScan.IsRestricted(ImmuneStackTrashingKey)` 추가.
> - ✅ cap 파티션을 AS-IS IsSameEffect(카드-단위, 해시로만 분리)로: uniform `ActivatedEffect.EffectId` = `{card}:ae`(기본 collapse) + `capHash`(SetHashString 미러) — HasExecutedSameEffect와 동일 술어의 두 소비처가 이제 일관.
> - ✅ replay 이중적용 잔여 봉합: `RunJournaledImmediate(Async)` — PlayOption의 직접 trash-move+OnUseOption, DigiBurst continuous inner 등록(결정적 id로 교체), ST2_14 ApplyRestriction, DNA FuseAsync를 저널 경유로.
> - ✅ 마커 per-pass에 `IsEffectsDisabled` 추가(AS-IS CanActivate :421; collect=CanTrigger엔 없음 — 미추가가 정합).
> - ✅ SuspendedExternally 재개 시 cut-in drain 비대칭: `WindowContinuation.ExternallySuspended` 플래그로 재drive head에서 drain(RD-17 순서 복원; 동기 창 siphon 방지 위해 플래그-게이트).
> - ✅ Fortitude/Decode/Partition 부활 경로에 enter-play 훅(RegisterCard=use 리셋+효과 재등록; AS-IS PlayPermanentCards→card.Init :1361).
> - ✅ BT1_089 move 분기에 CannotMove 제약 스캔(AS-IS Permanent.CanMove :2010-2040).
> - ✅ reveal `maxCount:-1` = AS-IS ForAll 자동 라우팅(프롬프트 발명 제거 — BT1_088/ST4_03/BT1_048 계열).
> - ✅ BT2_087 무음 no-op(resolver `ActivatedMemoryEffect` 케이스 부재, 선재 버그) + `ActivatedSelectAndDeDigivolveEffect` 케이스 배선.
> - ✅ cycle-open 누수(P2-3): effects 리스트 구축을 Begin 앞으로.
> - **2차 이연(앵커 포함)**: ①마커 per-CARD 입도 vs AS-IS per-effect stack(canUse-false 형제 효과 편승 — 현 풀 witness 0, 마커 per-effect화 재설계 필요), ②A-1 skipCondition 프로덕션 배선(AS-IS 컷인 창들이 헤드리스선 메인루프에 흡수·null — 창 포팅 태스크와 동행), ③중첩 full-`ResolveAsync`(ActivateMainOfOptionSide)의 자기-flush replay 이중적용+coordinator 재진입(실 security-option 카드 포팅 전 상환 필수), ④EX8_074 #1 BeforePayCost ≥2 게이트의 `!CanNotBeAffected`/`CanSuspend` 절, ⑤EX8_074 #6 재활성의 played-card 타입 절(+자기-플레이 제외의 AS-IS 대조 — OnPlayReactivation LA-3), ⑥ApplyDelete 혼합-Apply 즉시半 replay(문서화된 status-quo 잔여), ⑦OnUseDigiburst 리스너 표현(EffectTiming enum 멤버+브릿지 분류 — emit만 존재), ⑧선언 경로 비-uniform capped register(현 풀 witness 0), ⑨WindowChoicePending suspend 소유권 참칭 부비트랩(현재 도달 불가).

기준: **AS-IS와 내부 로직 구조·타이밍·처리순서 1:1** ([[result-equivalence-not-completion]]). 단순 입출력 등가는 불합격.
방법: 항목별 독립 적대 reviewer 7개(A-1 / A-2 / A-3·A-4 / B-1·B-4 / B-2 / B-3 / B-5)가 AS-IS 소스를 직접 재도출해 커밋·문서의 자기-정당화를 반증 시도.
대상: 커밋 1525b2e6(A-1), ee1c255b·e93205e9·b0569133(A-2), 533cc12c·ceb7a118(A-3/A-4), 090bb2ab(B-1), b9dad4de(B-4), 6dff06f2(B-3), 812d7966(B-2), 워킹트리 diff(B-5).

**판정 요약: P0 4건 · P1 13건 · P2 다수. "A/B군 완료" 주장은 1:1 기준으로 유지 불가.**
골격(창-루프 3축, collect-1회, pre-flip drain, 진화 시 리셋 스코프, attack-proxy 제거 등)은 정합 확인됐으나,
세 갈래의 **구조적 발산 축**이 여러 항목을 관통한다. 발견의 상당수는 리뷰어 간 독립 수렴(동일 발산을 2~3개 리뷰가 각자 재발견)으로 상호 검증됨.

---

## 구조 발산 축 1 — 소모 시점 역전(consume-after-body): B-1이 도입, B-2가 승계, B-4가 증폭, B-5가 노출면 확장

AS-IS는 **모든 경로에서 register-before-body**다:
- 메인 선언: `TurnStateMachine.cs:1183-1186` — `SetIsDeclarative(true)` → register → body. register가 optional 확인보다도 앞.
- 창(스택) 경로: `ICardEffect.cs:1119-1121` — optional 수락 후, body 직전 register.
- background: `AutoProcessing.cs:902/925/948/971/1039` — CanUse → register → Activate.
- 환불(`RemoveUse`)은 **전 카드풀 38장만의 per-card opt-in**([Once Per Turn] 카드는 1,211장). 기본 규칙 = "body가 no-op이어도 소모 유지".
- register-before는 load-bearing: AS-IS 저자 스스로 `AutoProcessing.cs:1068`의 `CanActivate || IsDeclarative` 우회를 박아 자기-cap 차단을 회피했다(원본 구조의 내재 전제).

헤드리스는 `ActivatedEffectResolver.cs:539-591`에서 **body 完走 後 + executed일 때만 Consume** — 의도적 구조 변경.

### P0-1. B-4가 opt-in 환불을 전 카드 무조건 환불로 반전 (기본 시맨틱 정반대)
- witness **BT2_078**(`DCGO/.../BT2/Purple/BT2_078.cs:19-26,74`): canNoSelect:true + RemoveUse 없음 → AS-IS는 0장 선택해도 use 소모. 헤드리스(`ActivatedEffect.cs:583-600` IsSkipped→executed=false)는 환불 → 같은 턴 재offer. 현재 전 body canSkip:false라 latent이나 skippable body 포팅 즉시 발화.
- 커밋 메시지의 "AS-IS 10+장이 환불"은 38/1211 예외를 일반 규칙으로 오독.

### P0-2. B-4가 인용한 witness 2장 모두 현 프리미티브로 표현 불가
- **BT14_029**(:108-114): 환불 조건이 보드 술어(no-op 판정) — 헤드리스 non-interactive no-op body는 executed=true→소모(AS-IS는 환불). 정반대.
- **AD1_024**(:154-265): executed가 3개 분기(yes/no·0-선택·전제 불성립)의 카드-정의 합성 술어 — 단일 `IsSkipped`로 분할 불일치. 카드별 executed 술어 훅 부재 = 이 두 카드는 uniform 포팅 불가(STOP감)인데 "구축 완료"로 기록됨.

### P0-3. consume-after가 만든 신규 버그: 다중-효과 리스트 suspend 시 Consume 커밋 + sink 폐기 불일치
- `ActivatedEffectResolver.cs:257-322` ResolveListAsync는 여러 효과를 한 sink로 처리, flush는 전체 완료 후(:316). `OnceFlags.Consume`(:590)은 즉시 커밋·롤백 없음.
- 시나리오: capped 효과 A 완주(Consume 커밋) → 같은 리스트 효과 B suspend → un-flushed sink 폐기 = **A의 mutation 소실 + cap 소진**. resume 시 A는 :560 cap 게이트에 막혀 영원히 미실행. `PlayOptionCardEffect` 재귀 경로도 동일 sink 공유로 발화. B-1이 잡았다는 바로 그 버그 패턴의 리스트-수준 재도입.

### "consume-after가 유일하게 안전" 주장 — 기각
- 제약(순진한 consume-before + 전체 재invoke의 자기-cap 차단)은 실재하나, **AS-IS 순서를 유지하는 대안이 같은 코드베이스에 이미 구현돼 있다**: `WindowResolver.cs:224`(Commit=body 前 소모) + `:86-95`(InFlightPick — resume 시 Commit 재실행 없이 body만 replay). uniform case에 동일 패턴(in-flight 마커 또는 OnceFlags 스냅샷-롤백을 DeferredChoiceCoordinator 사이클에 결합)을 적용하면 P0-3까지 동시 해결. consume-after는 P0-3을 해결하지 못한다.

### 파생 P1 (B-2·B-5에서 재확인)
- **P1(B-2 F-1)**: optional-decline cap 소각 — AS-IS는 register가 optional 프롬프트 앞이라 capped [Main] 선언 후 거절해도 cap 소모 유지(이 경로 RemoveUse 없음). 헤드리스는 무소모. + 본체 실행 중 cap 가시성(AS-IS=+1, 헤드리스=0; `BT14_046.cs:238` 등 isOverMaxCountPerTurn을 continuous 술어로 읽는 AS-IS 카드 ~20장 목록은 B-1/B-4 리뷰 F-5 참조 — 포팅 전 의무 감사 항목).
- **P1(B-5 P1-4)**: resolver-직행 경로(Option/BeforePayCost)에서 capped 본체가 창을 열면 미소모 cap 재수집 → 이중 발화 가능.

---

## 구조 발산 축 2 — 게이트 술어 구성 오류: per-pass 재검이 잘못된 반쪽을 재검

AS-IS의 게이트 2분법 (A-3/A-4 리뷰 재도출):
- **수집 게이트 = CanTrigger**(CanUseCondition 포함, `ICardEffect.cs:319-358`) — **collect 이후 절대 재평가 안 함**(실행 경로 :1116-1286 전체 확인).
- **per-pass·픽 직전·실행 진입 = CanActivate만**(`MultipleSkills.cs:122/164-165/366`, `AutoProcessing.cs:1068`) = cap(:368) + CanActivateCondition(:377) + 위치영역(:386-415) + IsDisabled(:421) + PermanentWhenTriggered(:428-452).

### P0-4. MarkerGate per-pass 재검이 CanUse까지 conflate + 정작 CanActivate 구성요소는 누락 (RDx-A3 533cc12c)
- `CanActivateAt`(`ActivatedEffectResolver.cs:113-152`)→`CanResolve = CanUse && CanActivate`(`ActivatedEffect.cs:564-573`) — 두 게이트 conflate.
- witness **ST4_14**(OnTappedAnyone): CanUseCondition에 이벤트-스코프 절(서스펜드된 subject 존재). 창 중간 그 subject가 삭제되면 — AS-IS는 CanActivateCondition만 재검(참)→해소(메모리+1), 헤드리스는 CanUse 재평가 거짓→영구 미제시(메모리+1 상실). 역방향(over-admission)도 성립.
- 동시에 AS-IS per-pass CanActivate의 구성요소인 **cap·IsDisabled·위치영역·상속identity**는 MarkerGate에 전무(P1 3건):
  - cap 누락: capped 마커 2개 시 둘째가 phantom offer(순서선택 오염) — B-1/B-4 리뷰 F-4·B-5 리뷰 P2-8이 독립 재발견. `CanDeclareAt`(:191-192)은 cap을 검사하는 비대칭.
  - IsDisabled/위치: 창 중간 "효과 무효"·digivolve-over 시 AS-IS 억제, 헤드리스 해소.
- 충실 해법: substrate가 CanUse/CanActivate Func를 분리 보유(`ActivatedEffect.cs:521-522`) → canUse는 collect 1회 평가(마커에 동결), per-pass·픽은 canActivate-半(cap·disabled·위치 포함)만.

### P1(A-3/A-4 P1-4). 비-uniform 마커 게이트 always-true — AS-IS per-pass CanActivateCondition 통째 폐기 (witness BT1_078: 라이브러리 0이어도 제시·no-op·소모).

### P1(A-4 P1-5). F3 "mid-window 비-인터랙티브"는 AS-IS 불변식이 아님
- AS-IS RuleProcess는 최소 두 경로에서 인터랙티브: 링크 초과 트리밍 선택(`AutoProcessing.cs:526-541`→`Permanent.cs:1321-1344` canNoSelect:false 선택), DP-부족 삭제의 인라인 컷인 창(`CardController.cs:~3690-3718`). 현재 헤드리스 미구현이라 throw 도달 불가(P0 아님)이나, 링크 룰 처리 1:1 포팅 순간 AS-IS-정당 경로에서 NotSupportedException이 터지는 지뢰. 예외 메시지가 설계 선택을 AS-IS 불변식인 양 서술.

---

## 구조 발산 축 3 — 절반-이식 + 문서의 커버리지 과잉 주장

### A-1 (컷인 dedup 인프라)
- **P1**: IsSameEffect 동치 오번역 — AS-IS(`ICardEffect.cs:860-933`)는 참조-동일 분기가 사실상 죽어있고(효과 인스턴스가 쿼리마다 재생성, `Player.cs:830-880`) 실동작 = **카드-단위 collapse**(같은 카드+빈 HashString+null 루트 = 같은 효과; 카드들이 `SetHashString`을 박는 이유). 헤드리스 EffectId 동치는 효과-단위 = 더 좁은 분할 → 같은 카드의 해시 미설정 효과 2개를 AS-IS는 skip, 헤드리스는 실행.
- **P1**: used-set 수명 불일치 — AS-IS는 라이브 MultipleSkills 인스턴스들의 실시간 합산(`AutoProcessing.cs:604-620`), 창 종료 시 클리어(`MultipleSkills.cs:58`) = **중첩 창 완료분은 망각**. 헤드리스 `WindowContinuation.Resolved`는 run 전체 append-only → 중첩에서 해소된 X'가 외부 seed의 X를 영구 억제(AS-IS는 발화, x 총 2회 vs 1회).
- P2: "컷인 5곳"은 과소(AS-IS 전달부 15곳 — 카드 이펙트 10곳 + AttackProcess:296은 별개 HasCounterEffect·mainStack=true); 재진입 mainStack=true 경로의 skipCondition null-강등 미표현; WindowResolver.cs:30-33 주석이 A-1 결정과 자기모순.
- 확인됨: 메인 루프 null 정합, skip 평가 시점, used 누적 시점(optional 수락 후·본체 전), IsCutInEffectUsedMaxCount 死경로 판정(세터 부재로 지지).

### A-2 (턴 종료 시퀀스)
- **P1**: Vortex/Overclock/Execute 어택 **선언**은 AS-IS에서 [End of Turn] 창의 order-choice 멤버(일반 ActivateClass, `EX8_074.cs:231-235`) — 다른 EoT 효과와 순서 교차 가능(선언 시점 타겟 적법성/공격자 생존이 달라짐). 헤드리스 `EndOfTurnEffectAttack`은 drain 종료 후 별도 offer = 순서 고정. task7의 ":699 < :705"는 사실이나 AS-IS 실구조는 "선언은 창 안, 스텝만 창 밖".
- **P1**: #67 ResolveTurnEndMinMemory — AS-IS는 player-scope 티어(`player.EffectList(None)`, `AutoProcessing.cs:651-657`)를 먼저 스캔 + `Func<int,int>` 종속 fold(`ChangeEndTurnMinMemoryClass.cs:14-22`). 헤드리스는 BattleArea만 + int 상수 SET만. 현 producer 2장이 상수라 결과 등가(latent).
- P2: EndPhase 진입 직후 AutoProcessCheck(:3168) 상당 스텝 부재 / use-count·턴카운터 리셋이 flip 後로 이동(AS-IS는 flip 前) / phase==End ENDTURN offer 경로 / pass 메모리 세팅 원자성.
- 확인됨: pre-flip drain, 이중수집 disjoint(HasActivatedEffectsAt 정당), 재검 비교식·턴지속 복귀, **emit-once 마커는 AS-IS 재진입 시맨틱과 합치**(턴 지속 시 AS-IS도 :699 재실행 = 헤드리스 마커 클리어), cleanup 위치(창·어택·재검 後, flip 前).

### B-2 ([Main] 선언 서브시스템)
- **P1**: Digi-Burst 지불 내부 구조 상이(ST4_13 라이브 경로) — AS-IS(`CardController.cs:2163-2233`)는 ①어느 진화원을 버릴지 **플레이어 선택** ②트래시 前 `OnUseDigiburst` 창 발화 ③트래시 ④본체. 헤드리스는 bottom-N 자동 + 창 미발화 + 본체 선택이 트래시 적용 전 상태에서 구성(sink 일괄 flush).
- **P1**: CanDeclareAt의 DigiBurst 게이트가 AS-IS CanDigiBurst(`CardController.cs:2135-2161`)의 부분집합 — `ImmuneFromStackTrashing`(`Permanent.cs:853`)·동적 술어 스캔(`CardSource.cs:2478`)·up-to 분기·cap 검사 부재.
- **P1**: 커밋이 해소 주장한 **BT1_088/089는 여전히 주석 처리로 미등록**(stale STOP 문서 그대로).
- P2: 비-uniform 폴백 무조건 true(AS-IS는 전 효과 CanUse 게이트, `Permanent.cs:1617-1636`; CanUse==null→false 의미론도 미러 안 됨) / 본체 후 `AfterEffectsActivate` 창 부재(`ICardEffect.cs:1283`, 소비 카드 풀 밖) / cap 키잉·다중스킬 resume 잔여(B2-05 선결).
- 확인됨: per-skill-index 현 풀 동치(OnDeclaration 다중 스킬 0장 실증), attack-proxy 제거 정당(AS-IS 공격 OnDeclaration 미emit 전역 grep), uniform 게이트 집합 실질 동일, 배타/재선언 위치, suspend/resume 단일-효과 정합.

### B-3 (use 리셋)
- **전제 오류**: CardSource는 카드당 1개 영속(MonoBehaviour) — "새 CardSource 생성 시 Init"이 아니라 존 이동/플레이 시퀀스의 명시 호출.
- **P1**: AS-IS 리셋 지점 전수 = 존-진입 7곳(RemoveField 스택 전체·손·트래시·덱·처리영역) + 플레이/Jogress/옵션(CardController.cs:1361/1438/1739) + **tuck 4곳**(PlacePermanentToDigivolutionCards :3093 · AddLinkCard :3393 · Jogress 진화원 :1511 · DigiXros 소재 SelectDigiXrosClass.cs:923) + 턴경계(:3207). 헤드리스는 **enter-play(RegisterCard) 1곳만**. 특히 "re-stack 포괄" 주장과 달리 tuck 경로 미커버 — MindLink(`MindLink.cs:93-115` 바인딩만 제거)·DNA·DigiXros가 이미 지나가는 라이브 경로.
- P2: 존-진입 리셋 전면 부재(트래시/손 리스너 포팅 시 상환 필수인데 경계 미기록) / AfterPayCost 창이 AS-IS는 리셋 前(stale) 헤드리스는 리셋 後(fresh) / UseOption Init 미러 부재 / "de-digivolve 포괄" 주장 허위(행동은 우연히 정합 — AS-IS도 승격 카드 미리셋).
- 확인됨: 진화 시 새 top만 리셋(1:1), 리셋 범위·키(`{owner}:{source}:` prefix = CardSource 단위 통클리어와 동일), 턴경계 리셋 기존 인프라 커버.

### B-5 (uniform 이관, 미커밋)
- **P1**: 래퍼 게이트 전부 null — 카드 7장 모두 canUse/canActivate 미이관(AS-IS BT1_056.cs:45-61·BT2_080.cs:58-69·BT2_081.cs:60-71·EX8_074.cs:275-278의 CanUseCondition/CanActivateCondition). "uniform 게이트 강제 경유"는 형식상 참이나 게이트 내용물이 비어 fizzle 규칙 부재(예: BT2_081 트리거 후 본체 카드 이탈 시 AS-IS fizzle, 헤드리스 실행).
- **P1**: BT1_044 본체 후보군 오답 — AS-IS(레벨≤4 필터 + **자기 스택 밑만**, `BT1_044.cs:27-44/55/89`) vs 헤드리스 2-인자 디폴트(무필터 + 자기 소유 전 디지몬 밑, `CardPortingFramework.cs:5127-5204`). pre-B-5 버그를 래핑이 봉인.
- **P1**: ST2_14/ST4_12/BT1_113 `ActivatedTargetRestrictionEffect` resolver case 부재 = **조용히 드롭**(코스트·트래시만 발생, 제한 미등록) — 실동작 버그 확정(별건, B-5 원인 아님).
- P2: cap↔전제 평가 순서 교환(결과 등가, 구조 비1:1) / optional-3 접기는 3장 모두 AS-IS도 canNoSelect:true + 무cap이라 **use-count 등가 성립 확인**(잔여 = 2-decision 프로토콜·구조 debt) / EffectId 키잉은 AS-IS 동형 충돌이라 대체로 충실.
- 확인됨: 삭제된 10개 케이스 전수 대조 소실 없음, BounceDiscard 순서 유지, PlayCardAction 언랩 양쪽 커버, ST4_13 파라미터 1:1, EX8_074 BeforePayCost 동적 canNoSelect 미러.

---

## 패턴 진단 (재발 방지)

1. **"result-equivalent" 자기-정당화의 반복 실패**: B-1/B-2/B-4의 소모 등가 주장은 optional-decline·per-card 환불·mid-body 가시성·다중-효과 suspend 4개 코너에서 결과 수준조차 비등가. B-3 "re-stack 포괄"·B-2 "BT1_088/089 해소"·B-4 "witness 10+장"은 사실관계가 틀림. → 등가 주장에는 반드시 갈라질 수 있는 코너의 전수 나열 + witness 대조를 요구할 것.
2. **프리미티브를 만들며 인용한 witness와 실대조하지 않음**(B-4). witness 대조를 했으면 통과 불가능한 설계였다.
3. **대안 미탐색 임의 변경**: consume-after는 자기 코드베이스의 InFlightPick 패턴(AS-IS 순서 보존)을 검토하지 않은 결정.
4. **한쪽 반쪽만 미러**: RDx-A3는 "per-pass 재검"이라는 축은 맞췄으나 재검 술어의 구성(CanActivate-半만)을 틀림. B-3은 리셋 이벤트 집합의 1/13만.

## 권고 상환 순서

1. **소모 파이프라인 재작업**(P0-1·2·3 일괄): uniform case를 commit-before-body + in-flight replay(또는 OnceFlags 트랜잭션)로 — AS-IS register-before 복원. B-4 환불을 per-card executed 술어 훅 opt-in으로 강등.
2. **MarkerGate 술어 분리**(P0-4): canUse는 collect 동결, per-pass는 canActivate-半 + cap + IsDisabled + 위치영역.
3. **B-3 리셋 지점 확장**: tuck 4곳(라이브) 우선, 존-진입은 리스너 포팅 경계에 debt 등재.
4. **A-1 인프라 수정**(배선 전): 동치 키를 카드-단위 collapse+HashString 미러로, Resolved 수명을 라이브-프레임 스코프로.
5. **B-2 DigiBurst 내부**(ST4_13 라이브): 진화원 선택 + OnUseDigiburst 창 + 지불→본체 순서.
6. **B-5 래퍼 게이트 이관**(커밋 전 필수): 7장 canUse/canActivate 채우기 + BT1_044 후보군 수정. ST2_14 계열 resolver case 별건 처리.
7. **문서 정정**: BT1_088/089 미해소, B-3 커버리지, A-1 "5곳", WindowResolver.cs:30-33 모순 주석, F3 예외 메시지의 AS-IS 귀속.
