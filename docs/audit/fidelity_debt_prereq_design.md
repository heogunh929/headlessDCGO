# 잔여 debt 선행조치(P1~P8) 설계 (2026-07-02)

> **✅ 구현 완료 (같은 날): P1 · P2 · P3 · P5 · P7 · P8** (P4·P6은 설계대로 카드 등장/별도 goal 대기).
> - **P1**: `CardLeavePlayCleanup` 신설(스냅샷 공용화+배틀/sweep drop). 구현 중 발견: 스케줄러는 레지스트리 경유로 effect를 해소하므로 drop 전 enqueue만으로는 부족 → **죽은 카드의 OnKnockOut 창을 finalize 내부에서 동기 해소**(G8-003 OnStartBattle 패턴, AS-IS TriggeredSkillProcess 위치와 일치). G9-062 신설 3(leak 노출 테스트 포함).
> - **P2**: `ChoiceRequest.SelectionValidator` + `ChoiceResult.Validate` 조합 게이트 → resolve 거부·재선택. SelectPermanentEffect가 중앙 부착. CVA2 +1.
> - **P3**: `EffectAttackQueue` 서비스 + `RequestQueuedChoices`/`TryOpenNextQueued`(공격 종료·decline·불법타깃 시 재개). CVA2 +1. → **debt#2 해소**.
> - **P5**: `counter.isCounterEffect` 마커 + 수집기 pass 필터 + Declared 2회 파킹(레코드 없는 계약-수준 파이프라인은 레거시 단일 emit 강등). W6 +1, G3.5-005 기대값 갱신. → **debt#5 해소**.
> - **P7**: 무DP 직접 트래시(DigiEgg·미발동 Option, 배틀에리어 한정, opt-out 플래그, 소스 동반 트래시). AS-IS Digimon 분기는 실카드 도달 불가로 명시적 축소. D2 +2. → **debt#7 해소**.
> - **P8**: probe 결과 [Security] 스킬은 이미 인라인(G12-004 ✓) — 갭은 OnSecurityCheck **트리거** 창의 지연 해소뿐 → 루프 내 동기 해소로 복원(파킹 리팩터 불요, 설계보다 소형). W4 테스트 1건 시맨틱 갱신. → **debt#8 해소**.

> **목적**: [fidelity_debt.md](fidelity_debt.md) "위반 조치 구현" 절의 잔여 8건에 착수하기 전에 필요한 **선행조치**를 리스트업하고, 각각 바로 조치 가능한 수준으로 설계한다. 각 선행조치는 어느 debt를 unblock하는지 명시.
> **설계 전 확정한 코드 사실** (직접 확인):
> - binding drop은 sink 2곳뿐(`MatchStateMutationSink.cs:608` pendingMove / `:692` ApplyDelete). **배틀 삭제(`BattleResolver.cs:161` 직접 트래시 이동)와 sweep의 pending 마무리(`GameFlowProcessor` 직접 이동)는 drop이 없음.**
> - 평가 측(`PlayerScopeContinuousHelpers.CollectApplicable:60-95`)에 **source-생존 필터 없음**(EffectInvalidation만).
> - Choices 인프라에 결과-검증 훅 없음(후보 포함 검증뿐).
> - `SecurityResolver.RunSecurityCheckLoopAsync`는 단일 async 루프(중간 파킹 불가 구조).
> **공통 종료 기준**: 항목별 동작-단언 테스트 + `bash scripts/run-tests.sh` green + `tools/RuleAudit` 0. 커밋은 지시 시.

---

## P1. 🔴 공용 leave-play 정리 루틴 (스냅샷 공용화 + 배틀/sweep 경로 drop) — **debt#1, 실버그**

**재정의된 문제**: 배틀 삭제·sweep 마무리 경로가 binding을 drop하지 않으므로,
1. **죽은 카드의 연속 효과가 계속 적용**된다(예: 배틀로 죽은 테이머의 "+1000 DP" player-scope 버프 잔존) — AS-IS는 필드 이탈 시 그 permanent의 EffectList가 자연 소멸하므로 명백한 위반(leak).
2. 반대로 drop을 추가하면 A4에서 수정한 것과 동일하게 **키워드-grant POST 치환이 유실**되므로, drop 직전 **삭제시점 키워드 스냅샷**이 함께 가야 한다.

**설계**:
1. `Headless/Runtime/CardLeavePlayCleanup.cs` 신설:
   ```csharp
   public static class CardLeavePlayCleanup
   {
       // 삭제 계열 이탈: POST 키워드 스냅샷(A4 헬퍼 이동) → binding drop.
       public static void OnDeleted(ICardInstanceRepository repo, EffectRegistry? registry, EngineContext? context,
                                    HeadlessEntityId cardId, Dictionary<string, object?> metadata);
       // 비삭제 이탈(바운스/덱 등): drop만.
       public static void OnLeftPlay(EffectRegistry? registry, HeadlessEntityId cardId);
   }
   ```
   `SnapshotPostReplacementKeywords`를 sink에서 이 클래스로 이동(sink는 위임 — 동작 무변경 리팩터).
2. 호출부 폴딩:
   - `MatchStateMutationSink.ApplyDelete`(:692 인근) → `OnDeleted` 위임(기존과 동일 동작).
   - **`BattleResolver` 삭제 finalize**(:161 트래시 이동 직전, `TryFortitudeReplayAsync`(:171)보다 **앞**): `OnDeleted` — 스냅샷이 Fortitude의 키워드 읽기(`HasReplacementKeyword`)에 선행해야 함.
   - **`GameFlowProcessor` pending-sweep 마무리**(pending 분기): `OnDeleted`. 단 pending 카드는 defer 시점에 이미 스냅샷됐을 수 있음 — `OnDeleted`는 이미 있는 플래그를 덮지 않고 OR(스냅샷 멱등).
   - pendingMove(:608)는 삭제가 아니므로 `OnLeftPlay`(drop만) — 현행 유지 위임.
3. **평가 측 방어선(선택)**: `CollectApplicable`에 source-생존 필터를 넣는 대안은 **채택하지 않음** — AS-IS 미러는 "이탈 시 효과 소멸"(등록 해제)이지 평가 시 필터가 아니며, 필터는 시큐리티/이탈-예외 케이스(face-up 시큐리티 효과 스캔 등)와 충돌 위험.

**매칭 검증**: AS-IS는 permanent 기반 EffectList라 이탈 즉시 소멸 + 삭제 처리 자체는 삭제 hashtable로 죽은 카드의 응답을 평가 → "스냅샷 후 drop" = 동등. Fortitude/Ascension/Save의 AS-IS 평가 시점(삭제 처리 중) = 스냅샷 시점 ✓.

**테스트**: ① 배틀 삭제된 테이머의 player-scope DP 버프가 사후 미적용(**현재는 FAIL할 회귀 노출 테스트**), ② 배틀 삭제된 키워드-grant Ascension 홀더에 POST 창 열림(스냅샷 경유), ③ sweep-마무리 삭제도 동일, ④ 기존 G9-055/058/C14 회귀.

**unblocks**: debt#1. **우선순위 1** — 잔여 중 유일하게 현재 게임 상태를 오염시키는 실버그.

---

## P2. 초이스 결과 검증 훅 (SelectionValidator) — debt#3·#4의 기반

**설계**:
1. `ChoiceRequest`에 `Func<IReadOnlyList<HeadlessEntityId>, bool>? SelectionValidator { get; init; }` 추가(직렬화 대상 아님 — in-memory choice 흐름 전용).
2. `ChoiceController.ResolveChoice`(InMemory 구현)에서 후보-포함 검증 뒤에 `SelectionValidator?.Invoke(selected) == false → InvalidOperationException`(기존 불법-선택 거부와 동일 경로) → 액션 레이어가 illegal 처리, 초이스는 pending 유지(재선택).
3. `SelectPermanentEffect.BuildRequest`가 `_canEndSelectCondition`을 validator로 부착(B5에서 노출만 해둔 `IsValidSelection`의 중앙 소비).
4. RL 관점 문서화: 액션 마스크는 집합 제약을 표현하지 못하므로 "시도→거부→재시도"가 모델(기존 illegal-action 처리와 동일 계약).

**매칭 검증**: AS-IS `CanEndSelect`(:220-238)는 UI의 종료 버튼 게이트 = "불통과 조합으로는 선택을 끝낼 수 없음" — resolve 거부와 동등.

**테스트**: validator 부착 요청에서 ① 불통과 조합 resolve → 거부·pending 유지, ② 통과 조합 → 정상. **unblocks**: debt#3(B5 조합 술어 중앙화), P4의 mutual 제약.

---

## P3. 공격자 큐 파킹 프리미티브 — debt#2

**설계**:
1. `EffectDrivenAttack`에 큐 API: `RequestQueuedChoices(context, IReadOnlyList<HeadlessEntityId> attackers, EffectAttackOptions options)` — 첫 공격자 초이스 오픈, 나머지는 컨텍스트 서비스 `EffectAttackQueue`(신설, `TryGetService` 패턴)에 `(attackerIds[], options)` 저장(Func 포함 options는 메타가 아닌 **서비스 객체**에 보관 — 직렬화 문제 회피).
2. 재개 지점: `AttackPipeline.AdvanceCleanup`(공격 종료) 후 큐 비었는지 확인 → 다음 공격자 `RequestChoice`(생존·CanAttack 재검증, AS-IS의 `if (selectedPermanent.CanAttack(...))` 순차 재평가 미러).
3. `SelectPermanentEffect.TryOpenAttack`이 다중 선택 시 이 API로 위임.

**매칭 검증**: AS-IS Attack 모드는 `foreach (selected) if (CanAttack) SelectAttackEffect...Activate()` 순차(:1009-1027) — 큐 재개 시 재검증 포함 동등. **테스트**: 2체 선택 → 1번째 공격 완주 후 2번째 초이스 자동 오픈, 1번째 공격 중 2번째가 죽으면 스킵. **unblocks**: debt#2.

---

## P4. RevealSelect 멀티패스 상태 서비스 — debt#4 (BT10-096형)

**설계**:
1. 컨텍스트 서비스 `RevealFlowState` 신설(F68D `IDeletionReplacementCandidateConditions` 선례): 진행 중 플로우의 `조건 배열(Func 포함)`·`현재 패스 인덱스`·`누적 선택`·`남은 리빌 카드`·목적지들 보관 — request-id 인코딩(Func 직렬화 불가)의 한계 해소.
2. `RevealAndSelect.RequestMultiChoice(context, player, revealCount, SelectPass[] passes, remainingTo, isOpponentDeck)` — 패스별 순차 RequestChoice(각 패스의 조건으로 selectable 필터, 이전 패스 선택 제외). mutualConditions(패스 간 상호 제약)는 P2 validator로 표현.
3. 전 패스 종료 후 남은 카드는 기존 `HandleRemainingAsync`(B4)로.

**매칭 검증**: AS-IS `RevealDeckTopCardsAndSelect`(:229-465)의 조건 배열 루프(:291-341) + per-조건 목적지 — 패스 구조 동등. **착수 시점**: BT10-096형 카드가 포팅 큐에 오를 때(선행조치는 서비스 골격+1패스 회귀까지만 해도 됨). **unblocks**: debt#4.

---

## P5. [Counter] 마커 + 카운터 2-pass — debt#5

**probe 선행**: `AutoProcessingTriggerCollector`의 timing 매칭 방식 확인(문자열 timing이면 최소 변경 경로 가능).

**설계**:
1. 마커: binding values `counter.isCounterEffect`(bool) — AS-IS `ICardEffect.IsCounterEffect` 미러. 카드-facing 팩토리(카운터 효과 등록 헬퍼)에 `isCounterEffect` 인자.
2. 2-pass: `AttackPipeline.AdvanceBlockTiming`의 단일 `OnCounter` emit(:86-89)을 B2 패턴의 파킹 phase(`AttackPhase.CounterTiming` 신설)로 분리 — pass1(비-[Counter]) emit → 공용 루프 드레인 → pass2([Counter]) emit → 드레인 → Block으로 진행. 수집기 필터: pass별로 마커 유/무 binding만 수집(timing을 `OnCounter`/`OnCounterEffect` 2종으로 나누는 방안과 구현 시 비교 — 소비자가 timing 문자열이면 후자가 최소 변경).

**매칭 검증**: AS-IS `AttackProcess.cs:266-296 CounterTiming` = 비-IsCounterEffect OnCounterTiming+해소 → IsCounterEffect OnCounterTiming+해소, 블록 前 ✓. **테스트**: 마커 없는 카운터-타이밍 효과가 [Counter] 효과보다 먼저 해소(순서 기록 단언). **unblocks**: debt#5.

---

## P6. 희생의 재귀 삭제 경로 — debt#6 (선행조치 중 최대 작업)

**probe 선행**: AS-IS에서 희생 대상의 자체 치환(희생당하는 Evade 홀더)이 실제 발동하는지 원본 재확인(`DeletePeremanentAndProcessAccordingToResult` 경유이므로 이론상 발동 — 실카드로 확증 후 진행).

**설계**:
1. `SacrificeAsync`를 sink `Delete` mutation 경유로 교체(B3 rule-delete 패턴; source = 홀더의 원인 효과 id) → 희생 대상의 PRE 창(Evade/Barrier...)이 자연 발동.
2. **pending 시맨틱**: 희생이 defer되면 홀더의 스페어 여부가 미확정 — `ApplyWithTarget`의 `(success, complete)`에 `deferred` 상태 추가 + 홀더에 `sacrificePendingKey`(희생 대상 id) 저장 → 희생 대상의 창이 닫힌 뒤(sweep에서 대상 트래시 확인 시) 홀더 `ClearDeletion`, 대상이 치환으로 생존하면 홀더 스페어 실패(AS-IS successProcess만 스페어).
3. **순환 차단**: 희생 체인(A를 지키려 B 희생 → B의 Scapegoat로 C 희생...)은 F-6.8이 카드별 pendingOption으로 직렬화하므로 교착은 없음 — 단 동일 카드 재진입만 `sacrificePendingKey` 존재 시 후보 제외로 차단.

**매칭 검증**: AS-IS Scapegoat.cs:416의 delete-then-successProcess 구조 ✓. **unblocks**: debt#6. **주의**: 상태기계 확장이라 P1~P5 뒤 별도 goal로.

---

## P7. 무DP permanent 직접 트래시 — debt#7 (소형)

**probe 선행**: AS-IS `IsNotHavingDP` 정확 술어 + `TrashNoDPPermanentProcess`의 `DiscardEvoRoots`(소스 처리) 확인.

**설계**: `GameFlowProcessor.RuleProcessAsync`에 `IsNoDpPermanent` 분기 추가 — battle-area 카드 중 (AS-IS 술어 미러: Digimon-type인데 printed/instance DP 부재 등) → **destroy 미경유 직접 트래시**(트리거·치환 없음 — AS-IS가 그럼) + 소스 처리 미러. `HasLethalDp`(defined-DP 요구)와 상호 배타 유지.

**매칭 검증**: `AutoProcessing.cs:439-465` — RemoveField+AddTrash 직행, DestroyPermanentsClass 미경유 ✓. **unblocks**: debt#7.

---

## P8. 시큐리티 체크 내부 순서(스킬 해소 → 시큐리티-디지몬 배틀) — debt#8

**probe 선행 (필수)**: G12-004(SecuritySkillDeferredE2E)가 이미 시큐리티 스킬을 deferred 창으로 파킹한다면, 갭은 "시큐리티-디지몬 **배틀**이 스킬 해소 전에 실행되는" 좁은 구간인지 확인 — `RunSecurityCheckLoopAsync`에서 `OnSecurityCheck` emit(:135)과 시큐리티-디지몬 배틀(:162-172)의 실행 순서 + 스킬 창 파킹 시 루프가 어디서 멈추는지.

**설계(확인 후 확정)**: 루프 상태 외재화 — attack 메타에 `securityCheckRemainingKey`(남은 strike)·`securityBattlePendingKey`(배틀 대기 카드 id) 저장, 체크 1장 단위로: reveal→OnSecurityCheck emit→**파킹**(공용 루프 드레인/스킬 해소)→재개 시 시큐리티-디지몬 배틀→다음 장. B2의 `PiercingSecurity` 파킹과 동일 패턴을 루프 내부에 적용하는 것 — `SecurityCheckLoopResult` 누적값(CheckedCards 등)도 메타로 이동 필요.

**매칭 검증 목표**: `CardController.cs:4108-4184` — AutoProcessCheck+OnSecurityCheck 스킬 해소가 IBattle(:4177)보다 앞. **unblocks**: debt#8. **주의**: 리팩터 규모 중간 — P1 다음으로 영향 큰 전투 정확성 항목.

---

## 우선순위·의존

```
P1 (실버그 leak — 즉시)          → debt#1
P2 (저비용, 파급 큼)             → debt#3 · P4 의존
P3 (독립)                        → debt#2
P7 (소형, probe 1건)             → debt#7
P5 (probe 1건 + 파킹 패턴 재사용) → debt#5
P8 (probe 필수, 중간 규모)       → debt#8
P4 (P2 뒤, 카드 등장 시)         → debt#4
P6 (최대 작업, 별도 goal)        → debt#6
```

별건(C7 dual 카드 · C8 link/진화원 구분 · C9 isLinkedEffect 수명주기)은 선행조치 대상 아님 — 사용 카드 등장 전 라틴트, C9는 별도 표적 감사부터.

## 실행 대화문 (복붙용)
```
선행조치 진행. docs/audit/fidelity_debt_prereq_design.md 우선순위대로(P1 → P2 → P3 → P7 → P5 → P8; P4·P6은 별도 지시 시).
각 항목: probe-선행 명시된 것 먼저 확인(추측 금지) → 설계대로 구현 → 동작-단언 테스트(P1은 leak 노출 테스트 필수) + bash scripts/run-tests.sh green + tools/RuleAudit 0. 이전 항목 green 후 다음. probe 결과 설계와 다르면 STOP+보고. 커밋은 내가 지시할 때.
```
