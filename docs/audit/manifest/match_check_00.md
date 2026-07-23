# AS-IS ↔ TO-BE 매칭 검증 — 파트 0/13

담당 파일: `both_part_00.txt` = **`Script/CardController.cs`** (1개 파일).

- AS-IS: `/home/hg/git/headlessDCGO/DCGO/Assets/Scripts/Script/CardController.cs` (5988줄)
- TO-BE: `/home/hg/git/headlessDCGO/src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardController.cs` (5228줄)

검증 방식: AS-IS 36개 클래스를 4개 그룹으로 나눠 각 클래스를 **양측 전문(全文) 정독** 후 심볼 단위(생성자·메서드·프로퍼티·필드·enum·로컬함수·제어흐름) 1:1 대조. 재하우징/개명/삭제로 보이는 항목은 TO-BE 트리 전역 grep + 실제 이전 대상 소스(SelectPermanentEffect / MatchStateMutationSink / AttackProcess / InMemoryZoneMover / Player 등)를 직접 읽어 근거 확인. 코드 주석의 "deferred라 괜찮다"류 설명은 근거로 채택하지 않고 실소스로 재검증함.

substrate 번역(허용): `IEnumerator`/`StartCoroutine`→`async Task`/`await`, `Player` 객체→`new Player(context, playerId)` 뷰, `card.Owner`→`HeadlessPlayerId`, `ICardEffect` cause→`HeadlessEntityId? causeEffectSourceId`, `Hashtable`→타입 인자, `Filter`→`Where`, UI(PlayLog/Effects.*/brainStorm/transform/ShowCardEffect/WaitForSeconds) 스트립. 이들은 문제로 보지 않음.

---

## 이 파일의 종합 판정: **문제 있음**

한 파일에 36개 클래스가 들어있고, 그중 **정상(clean/notes-only)이 다수이나, 게임 로직이 실제로 누락·변형·역배선된 항목이 6건** 발견됨. 파일 단위로는 "문제 있음"으로 판정.

- 제대로 매칭됨(clean 또는 substrate note만): **26개 클래스**
- 문제 발견(로직 누락/변형/역배선/발명 브릿지): **6개 클래스 영역** — 아래 P1~P6

---

## 문제 발견 전건 (심각도 순)

### P1 — IUnsuspendPermanents: 재필터 도메인 확대 (실 로직 결함)
- AS-IS 5723: `untappedPermanets_Fixed = untappedPermanets.Filter(...)` — **1차 통과 생존자**를 재필터.
- TO-BE `.../Script/CardController.cs:2090`: `untappedPermanentsFixed = _permanents.Where(IsUnsuspendTarget)` — **1차 프리필터 이전의 전체 `_permanents`**를 재필터.
- `WhenUntapAnyone` 컷인이 멤버십을 바꾸는 경우 두 식이 다름: 1차 술어를 탈락(예: `!IsSuspended`, `CanNotBeAffected`, `!CanUnsuspend`)했지만 컷인 반응으로 자격이 생긴 permanent를 AS-IS는 제외, TO-BE는 포함. 이중 필터의 존재 이유(컷인 후 상태 변화 처리) 그 자체 케이스에서 도메인이 넓어짐. 근거: 양측 전문 대조. 수정 방향 = `_permanents.Where(...)` → `untappedPermanents.Where(...)`.

### P2 — 바운스 3종(DeckBottomBounceClass / DeckTopBounceClass / HandBounceClaass): pre-move 인터랙티브 "would-return" 창 누락
- AS-IS 2271-2436 / 2437-2602 / 2603-2837. TO-BE 트리 전역 grep 결과 **동명 클래스 없음**(주석 참조만). 로직은 `MatchStateMutationSink` kind `ReturnToDeckBottom`/`ReturnToDeckTop`/`ReturnToHand`(MatchStateMutationSink.cs:683-710)로, `SelectPermanentEffect.Mode.PutLibraryBottom/PutLibraryTop/Bounce`(SelectPermanentEffect.cs:218-220,559-568) 경유로 접힘.
- **미러된 부분(충실)**: ctor 필터의 `CannotReturnToLibrary/Hand` + `CanBeRemoved` 제약 스캔, POST-move 창(DeckBounce=`OnLeaveFieldAnyone`; HandBounce=`OnPermamemtReturnedToHand`→`OnLeaveFieldAnyone`), HandBounce의 `IsReturnedToHandByBurstDigivolution` 마커, "직전 파라미터 기록"(DP/Level/Cost/CardNames/Traits) → `CardLeavePlayCleanup`.
- **누락(핵심 결함)**: AS-IS DeckBounce(:2313-2351)·HandBounce(:2640-2681)의 **pre-move 컷인 창** — `autoProcessing_CutIn.StackSkillInfos(WhenReturntoLibraryAnyone / WhenReturntoHandAnyone / WhenRemoveField)` + `HasAwaitingActivateEffects()` → `TriggeredSkillProcess` → `willBeRemoveField` 재조준(바운스 취소 가능). sink 바운스 케이스는 이 창을 열지 않고 POST-move 창만 스테이징. TO-BE는 이 타이밍을 완료된 `CardMoved` 이벤트에서 **POST-move 파생**(TriggerTimingMap.cs:123-149)으로 대체 → 인터랙티브 prevent/replacement가 사라짐. Destroy 경로(`ApplyDelete` :1293-1600)는 pre-move `WhenPermanentWouldBeDeleted`/`WhenRemoveField`를 구현하므로 **비대칭 결함**. 실증: **BT5_086**(prevent-removal, `WhenPermanentWouldBeDeleted || WhenReturntoHandAnyone || WhenReturntoLibraryAnyone` 키)은 바운스 시 카드가 이미 이동한 뒤에야 발화 → 방지 무력화.
- **추가 미확인/누락**: `LibraryBounceEffect`/`HandBounceEffect` = cardEffect 기록(AS-IS :2394/:2783) 미러 없음; HandBounce `IsDigiEgg → AddLibraryBottomCards`(디지에그 바운스는 덱 밑, AS-IS :2801-2810) sink `ReturnToHand`는 무조건 `AddToHandAsync`; 바운스 경로의 `DiscardEvoRoots`(언더스택 트래시, AS-IS :2401/:2567/:2793) 미러 미발견. 카드파일 주석의 "SelectPermanentEffect Mode로 미러됨" 주장은 **부분적 사실(존 이동+POST 창)이나 과대**(pre-move 창 미포함).

### P3 — IPutSecurityPermanent: happy-path만 재구현 (미완 미러)
- AS-IS 3503-3647. TO-BE 동명 클래스 없음. 래핑 캐리어 `CardEffectCommons.PlacePermanentInSecurityAndProcessAccordingToResult`(:380-425)가 sink `AddToSecurityKind`(MatchStateMutationSink.cs:711-740) 경유로 대체. EX8_028 주석 "미러에 해당 클래스 없음"은 정확.
- **미러됨**: `CanAddSecurity` 게이트, faceUp/toTop, per-card OnAddSecurity, OnLeaveFieldAnyone, face-up→OnFaceUpSecurityIncreased.
- **누락(게임 로직)**: `TopCard.CanNotBeAffected` per-card 면역(:3525), `permanent.CanBeRemoved()`(:3526), **PRE `WhenRemoveField` 교체 컷인 창**(:3528-3558 + `willBeRemoveField` 재검/abort), `DiscardEvoRoots`(:3611), **DigiEgg→라이브러리 밑 분기**(:3616-3637), 토큰 게이트(:3582/3614). sink는 무조건 시큐리티 추가.

### P4 — ISecurityCheck: SecurityResolver 재아키텍처 — 해결 순서 스왑 + 다중 [Security] select 미재현
- AS-IS 3880-4234. TO-BE 동명 클래스 없음. 로직은 `SecurityResolver`(RunSecurityCheckLoopAsync / ResolveSecurityCheckWindowAsync / ResolveSecurityDigimonBattleAsync / RevealedSecurityCardSkipsBattle)로 실재 재구현(스텁 아님).
- **미러됨**: StopSecurityCheck 재평가, Strike 루프 바운드, OnSecurityCheck+IReduceSecurity(OnLoseSecurity), IDontBattleSecurityDigimonEffect 이중 스캔(:4136-4171), 시큐리티-디지몬 배틀→IBattle→Destroy, per-iter UntilSecurityCheckEndEffects 리셋.
- **발산(실 fidelity 델타)**: (a) **순서 스왑** — AS-IS는 공개된 카드의 `[Security]` activated 효과(인터랙티브 멀티-select, :4023-4102)를 OnSecurityCheck/OnLoseSecurity 반응자보다 **먼저** 해결; TO-BE는 `ResolveSecurityCheckWindowAsync`(OnSecurityCheck/OnLoseSecurity)를 먼저, `[Security]`를 나중(:180→:198). (b) **다중 `[Security]` 효과 인터랙티브 select 루프**(카드가 ≥2 SecuritySkill ActivateICardEffect일 때 플레이어 순서 선택, :4037-4102)가 `ActivatedEffectResolver.ResolveAsync` 단일-효과 경로로 대체돼 미재현. (c) **존 스테이징** — AS-IS는 깨진 카드를 Execution 존(`AddExecutingCard` :3980)으로 옮기고 창+배틀 이후에야 트래시(:4192); TO-BE는 Security→Trash 즉시(:147-154). 창/배틀 중 카드의 라이브 존을 읽는 효과가 다른 존을 봄. (d) 인터랙티브 OnSecurityCheck 반응자가 배틀도 하는 디지몬은 배틀 스킵(design item RD-C2-SECCHECK-INTERACTIVE-ORDERING).

### P5 — IRecovery: 제약 게이트 오배선 (실 로직 결함)
- AS-IS 2085-2108 / TO-BE `.../Script/CardController.cs:463-500`. AS-IS 게이트3(:2102) `if (!_player.CanAddSecurity(_cardEffect)) yield break;` — 완전한 `ICannotAddSecurityEffect` 스캔.
- TO-BE(:491)는 `SecurityRuleGateSeam.CanAddSecurity(...)`(=`=> true` 스텁)를 호출. **충실 미러 `Player.CanAddSecurity`(Player.cs:477, 실 스캔 존재)를 부르지 않음.** 하류(`IAddSecurityFromLibrary`→raw `AddSecurityFromLibraryAsync`, InMemoryZoneMover.cs:250)도 재게이트 안 함. 올바른 게이트가 한 호출 거리에 존재하므로 단순 deferred가 아닌 **배선 결함** — "cannot add security" 제약이 recovery 경로에서 조용히 무시됨.

### P6 — PlayPermanentClass: AS-IS에 없는 WhenDigivolving 창 + STOP 가드 추가 (발명 브릿지)
- AS-IS 1150-1698 / TO-BE 3855-4356. 본체 대부분 1:1 충실.
- **AS-IS에 없는 메커니즘 추가**: TO-BE 4303-4352가 `isEvolution` 시 **두 번째 창** `StackSkillInfos(EffectTiming.WhenDigivolving)` + `NotSupportedException` "double-key 가드"를 추가. AS-IS는 이 지점에서 `OnEnterFieldAnyone` **하나만** 연다. 주석은 "DISPATCH-REMAP BRIDGE"(포팅된 카드 코퍼스가 digivolve 효과를 `OnEnterFieldAnyone` 대신 전용 `WhenDigivolving` 키로 등록해서 필요)라 라벨. 프로젝트의 "미러 not 발명 / no bridge" 규칙에 반하는 발명 어댑터(코퍼스 재키잉 전까지 임시라 인정). 감사 세트 중 1:1 이탈이 가장 뚜렷.

---

## 참고/주의 항목 (현재 무해하나 1:1 아님 · design item)

- **HatchDigiEggClass**(AS-IS 1056-1092): 동명 클래스 없음. 로직이 `IZoneMover.HatchDigitamaAsync`(InMemoryZoneMover.cs:326-342, `TurnStateMachine.cs:373`/`TfxHatch.cs:51`/`BT1_089.cs:94`에서 호출)의 **맨 존 이동**으로 축소. 브리딩 배치·`EnterFieldTurnCount = -1`(TO-BE는 파생 getter로 결과 등가)·CanHatch 게이트(호출부 각각 재구현)는 보존되나, **AS-IS의 `PlayPermanentClass.PlayPermanent()`(ActivateETB:true) 파이프라인/ETB 창 라우팅이 재현되지 않음**. 디지에그는 대개 on-enter 반응이 없어 무해하나 구조적 발산.
- **PlayCardClass evolution 게이트 축소**(AS-IS :865): `!CanPlayCardTargetFrame(...)` → TO-BE 3660-3661 `OwnerId != card.Owner || !card.CanEvolve(...)`. 문서화된 축약이나 등가성은 파일 밖(CanPlayCardTargetFrame) 근거에 의존 — 문헌 신뢰 대상이므로 감사 flag.
- **PlayPermanentClass 필드 용량 미적용**(AS-IS :1287-1340 frameId 탐색 → `framePlaceable=true` 상수화, RD-P6C1-2). 배틀에어리어 하드 상한이 룰상 없어 무해 추정; 브리딩 1슬롯은 여전히 강제.
- **IBattle `Permanent.battle` 필드 채널 삭제**(AS-IS :4503-4511/:4763-4771): 미러 Permanent에 `battle` 필드 없음. 단 효과가 실제로 읽는 `"battle"` hashtable 키(:4929)는 채워짐 → 효과-가시 동작 보존(design item RD-EXT2B-01-BATTLEFIELD).
- **IDegeneration / IMassDegeneration**: `SelectCountEffect` 인터랙티브 count 선택(:4813-4835)이 AS-IS "컴포넌트 부재" fallback로만 미러(MIG3-DEGEN-COUNTSELECT); `SetChangedLocationTime()`(:4897) 헤드리스 아날로그 없음(MIG3-LOCATIONTIME). 조인트 `ValidTarget` 1회 프리필터·count 비대칭·AceOverflow·per-permanent WhenTopCardTrashed emit는 충실.
- **ITrashDigivolutionCards**: OnDigivolutionCardDiscarded emit가 `TrashSpecificSourcesAsync` 내부로 위치 이동(RD-C1b-DIGIDISCARD-POS, 인클래스 StackSkillInfos는 무해 무드레인); `willBeRemoveSources` clear가 제거 헬퍼 **앞**으로 이동(RD-S4-BT5_056, 헬퍼가 같은 플래그를 읽는 재필터 재적용 → 자기차단 방지). 위치 변경만, 근거 타당.
- **ITrashStack**: `SetChangedLocationTime()`(:5940) per-step 스트립(MIG3-LOCATIONTIME) — 타임스탬프 연속효과 시작 시점에 영향. 실 게임 로직 갭이나 인지·이연됨(`.../Script/CardController.cs:2849`).
- **IReduceSecurity 비-null 분기**(AS-IS :5448-5451): `GetSkillInfos`를 지금 열거해 SkillInfo append → TO-BE는 파라미터 record(`PendingSecurityTrigger`)만 append하고 열거를 미래 호출자에 지연. 반응자 스냅샷 시점 변화; non-null(ISecurityCheck) 호출자 미배선이라 latent.
- **DrawClass / IDiscardHands / IDigiBurst 이중 emit seam**: OnDraw는 `TriggerEventEmitter.Emit` + `StackSkillInfos` 둘 다(AS-IS는 StackSkillInfos만); OnUseDigiburst도 `EmitJournaled` + `StackSkillInfos`. 캐리어가 "오늘은 inert"라 rationalize; 양쪽 live화 시 이중발화 위험 — flag.
- **CanNotBeAffected 게이트-프록시 패턴**(SuspendPermanentsClass / IUnsuspendPermanents 술어 / ReturnToLibraryBottom / IDigiBurst): AS-IS `cardEffect != null` → TO-BE `causeEffectSourceId is {IsEmpty:false}`. `cardEffect≠null`인데 `EffectSourceCard==null`인 경우만 발산(현 호출자 미유발, latent).
- **AceOverflowClass 술어 fold**: AS-IS `IsACE && !IsFlipped && ...` → TO-BE `OverflowFor>0 && ...`. `OverflowMemory==0`인 미-flip ACE에서만 미세 차이(AS-IS `AddMemory(0)` 무해) → 순효과 동일.
- **문서 staleness(로직 아님)**: `CardSourceAsIsPlayAccessors` 클래스 summary(4567-4582)는 `CanEvolve/CostList/GetPayingCostWithBaseCost/CanJogress.../CanBurst.../CanAppFusion`을 STOP 브릿지로 서술하나 하단 per-member 은퇴 주석(4659-4682)은 실 CardSource 인스턴스 메서드로 해소됐다 함 — 서로 모순. 이 술어들은 PlayCard/PlayPermanent 코스트/진화 판정 의존이므로 `CardSource.cs`에서 실 STOP-vs-live 확인 권장.

---

## 클래스별 판정 일람 (누락 0 — 36개 전건)

그룹 A (앞부분):
1. **IDiscardHands** — 매칭됨(notes: 빈-리스트 early-return 추가, 배치-id substrate).
2. **IDiscardHand** — 매칭됨(notes: 죽은 `hashtable` 필드/ctor 인자 드롭, AS-IS에서 미참조 확인).
3. **PlayCardClass** — 매칭됨(notes: frame→GetFieldPermanents 인덱스 적응, evolution 게이트 축약=위 참고, 이중 GetPayingCost 호출까지 보존).
4. **HatchDigiEggClass** — 주의(위 참고: 맨 존 이동으로 축소, ETB 창 미확인).
5. **OnEnterFieldHashtableParams** — 매칭됨(clean, verbatim).
6. **PlayPermanentClass** — **문제 P6**(WhenDigivolving 발명 창) + 필드용량 미적용 note.
7. **UseOptionClass** — 매칭됨(clean; 죽은 CardEffect 읽기까지 보존, select substrate seat).
8. **DrawClass** — 매칭됨(notes: ctor 오버로드 분할, 이중 emit seam).
9. **IAddTrashCardsFromLibraryTop** — 매칭됨(notes: SetNotShowCards inert 보존).

그룹 B:
10. **IAddSecurityFromLibrary** — 매칭됨(notes: faceUp substrate 확장, 토큰/DigiEgg 스킵 미러 없으나 inert). `SecurityRuleGateSeam`(신규) = 정당한 빈 plumbing(실 로직은 미러 Player).
11. **IRecovery** — **문제 P5**(게이트 오배선).
12. **IDigiBurst** — 매칭됨(notes: ImmuneFromStackTrashing null-cause 프리가드, OnUseDigiburst 이중 emit — 둘 다 inert).
13. **DeckBottomBounceClass** — **문제 P2**.
14. **DeckTopBounceClass** — **문제 P2**.
15. **HandBounceClaass** — **문제 P2**.
16. **IPlacePermanentToDigivolutionCards** — 매칭됨(clean).
17. **IPlacePermanentToLinkCards** — 매칭됨(clean; per-pair OnLeaveFieldAnyone 창 구분 정확 보존).
18. **ILinkCard** — 매칭됨(clean).

그룹 C:
19. **IPutSecurityPermanent** — **문제 P3**.
20. **DestroyPermanentsClass** — 매칭됨(clean; EOF에서 절단 아님, PRE/POST 창·직전기록·트래시 루프 전부 순서 보존).
21. **ISecurityCheck** — **문제 P4**.
22. **IDestroySecurity** — 매칭됨(clean; TrashMode enum·이중 ctor·단일 IReduceSecurity/OnDiscardSecurity 창 보존).
23. **IBattle** — 매칭됨(notes: `Permanent.battle` 필드 삭제=hashtable 채널 보존). `CardSourceAsIsPlayAccessors`(신규) = 정당한 CardSource substrate 확장, 오배치 로직 아님.
24. **IDegeneration** — 매칭됨(notes: count-select fallback, locationtime 스텁).
25. **IMassDegeneration** — 매칭됨(clean; 조인트 ValidTarget 1회 프리필터·주석보존 count-select).
26. **ITrashDigivolutionCards** — 매칭됨(notes: emit 위치·마커 clear 위치 이동, 근거 타당; 라이브 컷인 RD-SW-E-01 해소).
27. **ITrashLinkCards** — 매칭됨(clean; AS-IS 죽은 컷인 재발명 없이 주석보존).

그룹 D:
28. **ReturnToLibraryBottomDigivolutionCardsClass** — 매칭됨(notes: gate-proxy latent, C1-drain 생략은 무해).
29. **IReduceSecurity** — 매칭됨(notes: 비-null 분기 GetSkillInfos 지연, latent).
30. **IAddSecurity** — 매칭됨(clean).
31. **IFlipSecurity** — 매칭됨(clean; 항상-참 재검 quirk 보존).
32. **SuspendPermanentsClass** — 매칭됨(notes: 죽은 IsAttack 드롭, PermanentCondition quirk·DP 스냅샷 순서 보존, gate-proxy).
33. **IUnsuspendPermanents** — **문제 P1**(재필터 도메인 확대).
34. **ITrashDeckCards** — 매칭됨(clean; 배치-once 보존, region 주석 오타는 양측 공통).
35. **AceOverflowClass** — 매칭됨(notes: 술어 fold·서명 memory 모델 substrate, 순효과 동일).
36. **ITrashStack** — 매칭됨(notes: SetChangedLocationTime 스트립 MIG3-LOCATIONTIME, 죽은 `_fromTop` 보존).
