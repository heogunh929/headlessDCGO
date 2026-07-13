# 효과 모델 재구축 설계서 — AS-IS ↔ 헤드리스 대응표 · 컷오버 · 리스크 · 추정

작성 2026-07-13. 목적: upstream DCGO2 업데이트를 기계적 diff로 대응하기 위해 엔진 게임로직을 AS-IS 구조 미러로 재홈잉(Headless=substrate만). 사용자 결정: 이 문서화 후 빅뱅 재구축.

---

## 0. 핵심 발견 (요약)

두 효과 모델은 **근본적으로 다르지 않고 구조적으로 평행**하다:

| 축 | AS-IS | 헤드리스 |
|---|---|---|
| 효과 베이스 | `ICardEffect` **추상클래스**(1291줄, 상태+게이팅) | `ICardEffect` **인터페이스**(ToBinding)+IHeadlessCardEffect(본체)+EffectBinding(레코드) |
| 효과 종류 태그 | **74 마커 인터페이스**(ICanNotX/IChangeX…, `is` 스캔) | **문자열 키 ~18 restriction + ~25 keyword** + 게이트 스캔 |
| 행동 표현 | **uniform `ActivateClass` + 클로저** / per-kind XClass | **파라미터화 프리미티브 85개** + 델리게이트 |
| 컨텍스트 payload | **untyped Hashtable**(per-timing 형태, HashtableSetting/GetFromHashtable) | **EffectContext.Values dict**(typed-on-read, 델리게이트 포함) |
| 카드→효과 | `CEntity_Effect.CardEffects(timing,card)` | `CEntity_Effect.CardEffects(timing,card)` — **동일** |
| 팩토리 | `CardEffectFactory.XEffect(...)` → XClass/ActivateClass | `CardEffectFactory.XEffect(...)` → **동명** → 프리미티브 |
| per-turn 카운트 | CEntity_EffectController.UseEffectsThisTurn | OnceFlagController(트랜잭션) |
| 트리거 수집 | AutoProcessing.GetSkillInfos/PutStackedSkill(코루틴) | AutoProcessAsync/CollectUnifiedSeed(async) |
| 창 해소 | MultipleSkills(turn-first, 재진입 while, 코루틴) | WindowResolver(동형, 외부화 continuation, async) |
| 실행 | IEnumerator Activate + Optional→Effect→Execute 코루틴 | ActivatedEffectResolver + park/resume(DeferredChoicePendingException) |

**이미 1:1인 것**:
- **카드 파일**(`CardEffect/<Set>/<Card>.cs`): CEntity_Effect 상속 + `CardEffects(timing,card)` 스위치 — AS-IS와 구조 동일. 274/3984 포팅됨(나머지 3709는 **볼륨 백로그**, 구조 문제 아님).
- **CEntity_Effect 추상**(CardEffects 시그니처)·**CardEffectDispatch**(reflection by type name)·**CardEffectFactory 메서드명**(1:1).

**즉 upstream 신규 카드 세트(최빈 업데이트)는 이미 기계 diff된다.** 재구축이 실제로 필요한 것은 아래 5개 축의 **엔진-레벨 구조 격차**뿐이다.

---

## 1. 대응표 — 축별 1:1 상태 + 재구축 필요분

| # | AS-IS 컴포넌트 | 헤드리스 현행 | 1:1? | 재구축 필요 |
|---|---|---|---|---|
| A | `ICardEffect` 추상(상태 필드 15+·CanTrigger/CanActivate/CanUse 게이팅·IsOnPlay/Deletion 접두판정·IsSameEffect) | ICardEffect 인터페이스 + ActivatedEffect의 CanResolveUse/ActivateHalf + collector + 게이트에 **분산** | ✗ | **베이스 추상클래스 이식** — 게이팅 로직을 한 곳(미러 ICardEffect)에. Hashtable→Values, GManager→context, 코루틴 없음(순수 판정). |
| B | 74 마커 인터페이스(CardEffectInterfaces.cs 547줄) | 소수만 존재 | ✗ | **74 인터페이스 정의 이식** + 스캐너가 `is IInterfaceX`로 필터하도록. |
| C | per-kind XClass(CanNotBeDestroyedClass 등)·KeyWordEffects Gain* | 파라미터화 프리미티브+문자열 키+게이트로 **통합** | ✗ | **탈-통합**: 종류별 클래스 재생성(마커 인터페이스 구현+클로저). 최대 격차. |
| D | CardEffectFactory 본체(XEffect가 `new XClass`) | 동명 메서드가 `new Primitive(key)` | 부분 | 팩토리 **본체 교체**(XClass 반환). 메서드명은 이미 일치. |
| E | AutoProcessing(트리거스택+GetSkillInfos)·MultipleSkills(창 루프)·CardEffectCommons(Hashtable 빌더/접근자·존 술어·grant) | AutoProcessAsync/WindowResolver(async)·게이트·mirror CardEffectCommons(273KB, 상당수 1:1 명명) | 혼재 | **게임로직 부분만** 미러로(창 ORDERING 규칙·술어·Hashtable 빌더). **코루틴 실행기·registry 저장·choice provider·mutation 적용 메커니즘 = substrate 유지**. |
| — | CEntity_Effect·CardEffects·CardEffectDispatch·카드 274장 | 동일 | ✓ | 없음(백로그 3709장은 별도 포팅). |

**substrate로 정당하게 남는 것**(사용자 기준: 코루틴/유니티 대체물): async 실행기(WindowResolver.DriveAsync 구동부)·EffectRegistry 저장소·ChoiceProvider/Controller·GameEventQueue·MemoryController·CardInstanceRepository·ZoneMover·OnceFlag 저장·mutation 물리적용(sink의 zone-move/upsert 부분). **단 이들 위의 규칙 로직**(창 순서·게이트 판정·Hashtable 스키마·mutation의 배치/replacement 규칙)은 미러로.

---

## 2. 근본 격차의 성격 (정직한 판단)

가장 큰 격차 C(per-kind 클래스 통합)는 **헤드리스의 의도적 최적화**다: AS-IS "종류×타이밍×형태별 서브클래스"를 프리미티브 85개+문자열 키로 접었다. 1:1 재구축 = **탈-통합**(one-file-per-kind 복원)이고, 이는:
- upstream이 종류별 파일(Evade.cs·ChangeDP.cs)이나 ICardEffect.cs·CardEffectInterfaces.cs를 바꾸면 **기계 diff 가능**해짐.
- 대신 헤드리스 통합 아키텍처를 해체하고, **엔진 dispatch를 AS-IS 클래스+인터페이스-스캔 모델로 컷오버**해야 함(작동 엔진을 그 위로).

**비용/편익 냉정 평가**(사용자 결정은 존중, 정보 제공 목적):
- upstream 최빈 = **신규 카드**(새 세트). 카드층은 **이미 1:1** → 이미 기계 diff됨. 재구축의 편익은 여기 **거의 없음**.
- upstream 희소 = **신규 효과 종류**(새 키워드/인터페이스), **규칙/밸런스 변경**(ICardEffect 게이트·키워드 로직). 재구축은 **여기서** 기계 diff를 가능케 함.
- 현재도 신규 종류/규칙 변경은 "유추 포팅"(헤드리스 패턴 따라)으로 가능 — 기계 diff보다 어렵지만 재해석-from-scratch는 아님.

즉 재구축은 **희소 upstream 카테고리(엔진-레벨 변경)의 대응력**을 위해 **대규모 엔진 효과모델 탈-통합·컷오버**를 치르는 것. 카드(최빈)는 무관.

---

## 3. 컷오버 순서 (의존성 leaf→root, 빅뱅 내 단계)

1. **파운데이션**: ICardEffect 추상클래스(A) + 74 인터페이스(B) + enums(EffectTiming 65·Duration·CalculateOrder) + CEntity_Base/Effect/EffectController + ActivateClass + SkillInfo. → 컴파일 기반.
2. **Hashtable 계층**: CardEffectCommons의 HashtableSetting(빌더)/GetFromHashtable(접근자)/GameContextDeterminarion(존 술어)/CanUseEffects(CanTriggerX 술어 41)를 AS-IS 구조로. Values dict를 Hashtable 역할로.
3. **per-kind 효과 클래스(C)**: XClass 군(CanNotBe*·Change*·Give*·KeyWord* ~250) 재생성.
4. **팩토리(D)**: CardEffectFactory 본체를 XClass 반환으로 교체.
5. **트리거/창 규칙(E-게임로직)**: AutoProcessing 트리거스택 + MultipleSkills 순서규칙을 미러로(async 실행기는 Headless).
6. **엔진 dispatch 컷오버**: RegisterCard/GetContinuousEffects/gates가 AS-IS 클래스+인터페이스-스캔을 소비하도록. 통합 프리미티브+게이트 은퇴.
7. **검증**: 427 테스트 + RuleAudit 0 복구.

각 단계에서 이전 모델과 신모델이 **동시 컴파일 안 됨**(ICardEffect 이름 충돌·dispatch 이중) → 빅뱅 구간은 1~6 완료까지 **빌드/테스트 red**.

---

## 4. 리스크

- **장기 red**: 1~6 완료 전까지 엔진 미작동. 427 테스트·RuleAudit 회귀 추적 불가(전부 red). 중간 검증 부재 = 발산 은폐 위험(원래 마이그가 막으려던 것).
- **탈-통합 재발산**: 프리미티브가 파라미터로 처리하던 미묘한 경계(배치 의미론·player-scope·delegate payload)를 종류별 클래스로 풀 때 재구현 오류. 헤드리스가 상환한 fidelity(C/D/R2 리뷰·D-1/D-2 배치)를 다시 검증해야.
- **substrate 경계 회색지대**: sink/gates/WindowResolver의 규칙-vs-실행기 분리가 파일마다 애매 → 잘못 나누면 로직이 다시 Headless에 남거나 substrate가 미러로 새어듦.
- **번역 규칙 확정 필요**: Hashtable→Values, IEnumerator→(순수판정은 sync/실행은 async), UnityAction/UI→strip, GManager→context, ScriptableObject(CEntity_Base)→CardRecord — 파일 착수 전 규칙집 고정 안 하면 파일마다 발산.
- **되돌리기 비용**: 빅뱅 중단 시 부분상태에서 복구 어려움(원래 모델을 지웠다면).

완화: (a) 신모델을 **전이 네임스페이스**로 병행 구축→단계별 컴파일 유지→최후 rename(빅뱅의 red 구간 단축, 사용자 선택은 순수 빅뱅이나 이 하이브리드가 리스크 대폭↓), (b) 파운데이션(1~2) 후 **소수 카드로 신모델 왕복 검증**(dispatch가 신모델로 도는지), (c) 번역 규칙집 문서 선행.

---

## 5. 세션/노력 추정 (거칠게)

| 단계 | 내용 | 추정 |
|---|---|---|
| 규칙집 | 번역 표준 문서(Hashtable/코루틴/Unity 대체 규칙) | 0.5 세션 |
| 1 파운데이션 | ICardEffect 추상+74인터페이스+CEntity+ActivateClass+enums | 1~2 세션 |
| 2 Hashtable층 | 빌더/접근자/술어(CardEffectCommons 상당부) | 2~3 세션 |
| 3 per-kind(250) | XClass 군 탈-통합(sonnet 배치+검증) | 4~6 세션 |
| 4 팩토리 | 본체 교체 | 1 세션 |
| 5 트리거/창 | AutoProcessing/MultipleSkills 규칙부 | 2~3 세션 |
| 6 dispatch 컷오버 | 엔진을 신모델로·구모델 은퇴 | 2~4 세션 |
| 7 검증/상환 | 427+RuleAudit 복구, fidelity 재검증 | 2~4 세션(발산량 의존) |
| **합계** | | **~15~26 세션** |

카드 백로그 3709장 포팅은 **별개**(이 재구축이 끝나면 신모델 위에서, 로컬LLM 배치).

---

## 6. 권장 (실행 전 확정 사항)

1. **하이브리드 전이(병행→rename)** 채택 권장 — 순수 빅뱅의 장기 red/발산 은폐 리스크를 크게 낮추면서 최종 결과는 동일(1:1). 사용자가 순수 빅뱅을 고수하면 그대로 진행하되 리스크 4장 감수.
2. **번역 규칙집 문서 선행**(0.5 세션) — 이후 모든 파일 이식의 발산 방지 앵커.
3. 착수 순서: 규칙집 → 파운데이션(1) → 소수 카드 왕복검증 → Hashtable(2) → per-kind(3) 배치 → 팩토리(4) → 트리거/창(5) → dispatch 컷오버(6) → 검증(7).

**결정 필요**: (a) 순수 빅뱅 vs 하이브리드 전이, (b) 규칙집 선행 여부, (c) 재구축의 편익이 "희소 upstream 엔진변경 대응"에 집중되고 최빈(신규 카드)은 이미 1:1임을 인지한 상태에서 착수 확정.

**사용자 결정(2026-07-13)**: **순수 빅뱅**으로 진행("하이브리드 전이시 강모델이 리스크를 이유로 제대로 작업 안 하는 것도 리스크"). 규칙집은 별도 문서 대신 파운데이션 파일 헤더에 번역규칙 인라인 고정. 아래 §7~§9는 착수 후 실측·정정.

---

## 7. 착수 후 구조 정정 (2026-07-13, P1~P3 진행 중)

설계서 §1의 축 C("per-kind 클래스 **재생성/발명** ~250, 탈-통합")는 **과대·부정확**했다. 실측 결과 AS-IS 효과모델은 **3층으로 이미 조직화**되어 있고, 미러도 그 골격을 갖고 있다:

| 층 | AS-IS 위치 | 파일 수 | 미러 현황 |
|---|---|---|---|
| **kind-클래스** | `DCGO/Assets/Scripts/Script/CardEffects/*.cs` | **~73** (ActivateClass·ChangeDPClass·CanNotSuspendClass·RushClass·BlockerClass…) | `src/.../Script/CardEffects/*.cs`에 **73 스켈레톤 이미 존재**(대부분 ~300B TODO 스텁, 11개는 구모델로 기채움) |
| **팩토리** | `DCGO/Assets/Scripts/Script/CardEffectFactory/*.cs` (`partial class CardEffectFactory`) | **56** | 미러 `CardEffectFactory.cs`(구모델, 미러-발명 메서드 다수) |
| **카드** | `DCGO/Assets/Scripts/CardEffect/**` | **3,918** | 274 포팅(단 **구 헤드리스 인터페이스 대상** — AS-IS 인라인을 미러-발명 팩토리로 우회, 1:1 아님) |

**따라서 P3의 실제 정의 = "발명"이 아니라 "AS-IS `CardEffects/` 73 kind-클래스 + `CardEffectFactory/` 56 partial 파일을 1:1 미러(스켈레톤 채우기)"** — ~129 엔진파일, 잘 정의되고 기계적. kind-클래스는 거의 전부 **순수 술어 impl**(`class XClass : ICardEffect, IXEffect` + Func 필드 + `SetUpXClass` + 마커 메서드 CanNotSuspend/HasRush/GetDP…). 74 마커 인터페이스가 시그니처를 이미 강제 → 저위험. 유일한 async = ActivateClass(완료).

**카드층은 이미 1:1이 아님**(§0의 "카드 이미 1:1" 주장 정정): 미러 BT1_001은 `CardEffectFactory.SelfDpBuffTriggerEffect(...)`를 호출하나 **AS-IS BT1_001은 그런 팩토리 없이 `new ActivateClass()` + 로컬함수로 인라인**한다(`SelfDpBuffTriggerEffect`는 AS-IS 무연고 미러-발명). 즉 274 카드 **재포팅** 필요(각 AS-IS 카드파일의 인라인/팩토리 구조를 정확히 미러). 단 AS-IS 카드파일이 존재하므로 **기계적 번역**(Unity strip·코루틴→async)이지 재해석 아님 — 이게 원래 마이그가 노린 "로컬LLM 기계 diff" 지점.

## 8. 숨은 에러 발견 (Roslyn 선언-단계 컷오프)

**Roslyn은 선언부(타입/시그니처/override) 에러가 있으면 메서드 본문 바인딩을 건너뛴다.** 그래서 clean 리빌드가 보고한 **340 에러(274 CS0508 + 66 CS0246 IActivatedCardEffect)는 "선언부 에러"만**이고, 본문 참조 에러(`GManager.instance.autoProcessing`·`GetComponent<OptionalSkill>()`·`CardEffectCommons.*Hashtable` 빌더·`new ActivateClass()`·미러-발명 팩토리)는 **전부 가려져 있다**. 즉 **실제 에러 표면 ≫ 340**이며, 선언부(카드 274 시그니처 `IReadOnlyList`→`List` + 팩토리의 IActivatedCardEffect 제거)를 해소해야 순차적으로 드러난다. 이것이 설계서 §4 "장기 red가 발산 은폐" 리스크의 구체적 메커니즘 — **컴파일러에 의존 불가, 충실 1:1 + AS-IS 대조로만 관리**.

측정: 파운데이션 파일 자체 에러 = 문서화된 4개 미싱타입뿐(CardColor·JogressCondition·DigiXrosCondition·BurstDigivolutionCondition, §9에서 해소). 나머지 340은 순수 카드/팩토리 캐스케이드.

## 9. 진행 상태 (2026-07-13)

- **P1 파운데이션**: ICardEffect 추상·74 인터페이스·CEntity_Effect·CEntity_EffectController·CheckEffectDisabledClass 이식 완료(별도 에이전트, 충실 검증됨). **이번 세션 추가**: 4 미싱타입 해소(JogressCondition/DigiXrosCondition/BurstDigivolutionCondition/CardColor → `Conditions.cs`, AS-IS 1:1), **ActivateClass 채움**(kind-클래스 async 템플릿 확립) + minimal `DataBase.ReplaceToASCII`. 잔여 P1갭 work-list = `docs/audit/rebuild_p1_missing.md`(GManager 멤버·Hashtable 빌더·IBattle·EffectList 등).
- **색모델 이중성(design item COLOR-MODEL-DUALITY)**: 미러는 string 색모델(`CardSource.CardColors`→`IReadOnlyList<string>`), AS-IS는 `CardColor` enum. P3의 `ChangeCardColorClass`/`ChangeBaseCardColorClass` 이식이 재조정해야(현재 공존).
- **P3 kind-클래스**: ActivateClass 완료 + 나머지 61 스텁을 AS-IS 1:1 배치 채움 진행 중. 11개 기채움(구모델)은 별도 재검증 대상.
- **다음**: P2 Hashtable층/CardEffectCommons 게임로직(GManager 멤버 autoProcessing/attackProcess/GetComponent·HashtableSetting/GetFromHashtable 빌더·IBattle·EffectList) → P4 팩토리 56 partial 1:1 → 카드 274 재포팅 → P6 dispatch 컷오버 → P7 검증.

## 10. P2 진행 (2026-07-13, Hashtable층 착수)

- **CardEffectCommons → `static partial class`**: AS-IS의 `partial class CardEffectCommons` 파일분할(HashtableSetting/GetFromHashtable/…)을 미러 sibling partial 파일로 1:1 미러하기 위함.
- **IgnoreRequirement 중첩 fidelity 수정**: top-level enum(CardPortingFramework.cs) → `CardEffectCommons` 클래스 내부 중첩(AS-IS CardEffectCommons.cs:11 대로) → `CardEffectCommons.IgnoreRequirement` 텍스트 1:1, CS0426×3+CS0535 해소.
- **Hashtable 층 배치(진행중)**: HashtableSetting.cs(17빌더)·GetFromHashtable.cs(39접근자)·IBattle.cs 1:1 이식. 어댑테이션 규칙=transient `new Permanent(List)`→`new Permanent(ctx,id,owner)`·PermanentOfThisCard PermanentView→`ICardEffect.ResolvePermanentOfThisCard` 브릿지·Hashtable BCL 유지·Mathf verbatim. work-list=rebuild_p2_hashtable_missing.md.
- **GManager 멤버 배선(부분)**: `autoProcessing`→`AutoProcessing.For(ctx)`·`attackProcess`→`AttackProcess.For(ctx)` 완료(미러 골1·2 서비스, context-cached). 선언부 clean 확인.
- **IEnumerableExtension 완료**: Map/Filter/Some/Flat/Reduce/Clone/Every/GetRandom AS-IS 1:1(스켈레톤 채움). `.Filter`/`.Map`(foundation·kind-클래스·Hashtable 전반) 해소.
- **P2-ISEXECUTING 완료**: `turnStateMachine.isExecuting`를 match-scoped box(ConditionalWeakTable<EngineContext,StrongBox<bool>>, cEntity_EffectControllerStore 동형)로 안정 백킹 — 뷰가 매 접근 new여도 save/restore 유지. 미러 async 모델은 이 재진입 플래그 불필요(코루틴 프레임 없음), AS-IS save/restore verbatim 재현용.
- **CardSource.EffectList 계열 완료**(EffectList/EffectList_ForCard/EffectList_ExceptAddedEffects/EffectList_ForCard_ExceptAddedEffects): `cEntity_EffectController.GetCardEffects().Filter()` 얇은 위임, AS-IS 1:1.
  - **잔여 design item(entangled→P5/P6)**: (a) **P2-STACKSKILLINFOS** — 미러 AutoProcessing엔 AS-IS `StackSkillInfos(Hashtable,EffectTiming,…)`(AutoProcessing.cs:984 코루틴)가 없음(async 콜렉터로 대체) → 파운데이션 Activate tail이 verbatim 참조, P5 트리거/창에서 배선. (b) **P2-OPTIONALSKILL** — `GManager.GetComponent<OptionalSkill>().SelectOptional(ICardEffect,Hashtable)` → OptionalSkill 클래스 + GetComponent<T> + choice provider(P5/P6). (c) **Player.EffectList / Permanent.EffectList_Added / Permanent.cardSources** — AS-IS는 per-player/permanent Func<EffectTiming,ICardEffect> 백(PermanentEffects·UntilEndBattleEffects·UntilEachTurnEndEffects·UntilOwnerTurnEndEffects)인데 미러는 EffectRegistry 모델 → **effect-storage divergence**, P6 dispatch 컷오버와 함께 재조정.
- **P2 상태**: Hashtable 층(HashtableSetting/GetFromHashtable/IBattle, 검증 1:1) + IEnumerableExtension + GManager autoProcessing/attackProcess + isExecuting + CardSource.EffectList = **P2 기계적 본체 완료**. 잔여(StackSkillInfos·OptionalSkill·Player/Permanent EffectList)는 async executor·effect-storage와 entangled라 P5/P6 자연이관. 빌드 344(274 CS0508 카드 + 70 CS0246, 후자 4=Hashtable층 verbatim-미싱 SkillInfo/PlayCardClass/OnEnterFieldHashtableParams).

## 11. P4 구조 규명 (2026-07-13) — "56 스켈레톤 채우기"가 아니라 167→63 의미론적 재조정

착수 조사 결과 P4는 설계서 §3/§5의 "팩토리 본체 교체" 프레이밍(1세션)보다 훨씬 크고 P5와 co-dependent:

- **팩토리가 두 위치**: ①**working** `CardEffectCommons/CardEffectFactory.cs`(`static partial`, 1476 LOC, **167 메서드**, 274 카드가 `using ...CardEffectCommons`로 호출 → 427 green의 근원, 헤더는 "names match original" 주장하나 실제 **body는 구모델**=ICardEffect 반환→registrar가 binding으로 lower) ②`Script/CardEffectFactory.cs`+`Script/CardEffectFactory/*.cs` **TODO 스켈레톤**(중복 스캐폴딩, KeyWordEffects만 채워짐, 네임스페이스 `...Script.CardEffectFactory`).
- **AS-IS는 63 메서드**(메인 1530 LOC + ~26 partial `CardEffectFactory/` + KeyWordEffects 서브폴더). **미러 167 vs AS-IS 63** = 미러가 AS-IS가 **카드에 인라인**(`new ActivateClass()`+로컬함수)하거나 **kind-클래스**로 두는 로직을 **~100개 발명 팩토리 메서드**(SelfDpBuffTriggerEffect·BlitzSelfEffect·AllianceStaticEffect·*SelfEffect/*StaticEffect 변형)로 흡수. 즉 미러 팩토리는 헤더 주장과 달리 **1:1 아님, 심한 통합/발명**.
- **결론**: **canonical 팩토리 = working `CardEffectCommons.CardEffectFactory`**(이미 partial·카드가 호출). `Script/CardEffectFactory` 중복 스켈레톤은 **폐기 대상**(채우면 두 번째 CardEffectFactory 클래스 충돌). P4 = working 팩토리 method body를 **AS-IS 63메서드 1:1로 in-place 재작성**(kind-클래스/ActivateClass 반환) + **~100 발명 메서드 제거**. 단 발명 메서드는 카드가 호출하므로 **먼저/동시에 카드를 AS-IS 인라인 구조로 재포팅(P5)** 해야 제거 가능.
- **P4≡P5 co-dependent, 남은 재구축의 대부분**. 권장 실행 = **수직 슬라이스**(method-family 단위): AS-IS 카드가 실제 호출하는 팩토리 메서드는 1:1 이식, AS-IS가 인라인하는 것은 카드에 인라인 재포팅. 슬라이스별 (AS-IS 팩토리 메서드 + 그걸 쓰는 카드들)를 함께 처리. 가장 clean한 시작 슬라이스 = continuous DP(ChangeDP.cs factory + DP변경 카드). **async-mutation 경계**(팩토리 ActivateCoroutine이 `CardEffectCommons.ChangeDigimonDP`(미러=sync bool) 호출 → 코루틴→async·sync/async per-helper 판정)가 이 층의 핵심 난점.
- **재추정**: P4+P5 통합이 남은 노력의 최대 비중(설계서 §5의 "3 per-kind 4~6 + 4 팩토리 1 + 5 카드"를 P4≡P5 수직슬라이스로 재편). 순수 기계적 아님(카드별 AS-IS 인라인/팩토리 판별 필요), 단 AS-IS 카드파일 존재하므로 재해석 아닌 번역.
- **task 재구성**: #25(P4 56 partial)·#26(P5 카드274)를 **P4+P5 수직슬라이스**로 병합 재정의.

### 11.1 최종 구조 결정 (2026-07-13, 사용자 "제대로 1:1" 지시 후)

**사용자 지적**: "AS-IS 로직대로면 두 개인 이유가 있을 것" → 조사 결과 **AS-IS 팩토리는 딱 하나**(`partial class CardEffectFactory`, **global 네임스페이스**, `Script/CardEffectFactory.cs`+`Script/CardEffectFactory/`+`.../KeyWordEffects/`, 전부 partial-class 메서드; CardEffectCommons엔 팩토리 없음). 미러의 "두 위치"는 AS-IS 이유 아님 — 골7.5가 AS-IS 경로 스켈레톤(위치2)을 만들었으나 working 코드는 네임스페이스 충돌 회피로 위치1(CardEffectCommons)에 들어감.

**핵심 통찰**: **AS-IS는 네임스페이스가 없다** → 기계-diff에 필요한 건 **파일 경로 + 클래스/메서드 구조**이지 네임스페이스가 아님. 미러 네임스페이스는 자유 선택. **`class CardEffectFactory`를 네임스페이스 `...Script.CardEffectCommons`에 두면 충돌 없음**(`...CardEffectCommons.CardEffectFactory` 네임스페이스가 없으므로) — 게다가 AS-IS도 Factory/Commons가 **같은 global 네임스페이스의 별개 클래스**라 오히려 더 충실. (앞서 §11의 "위치2=Script 네임스페이스, 위치1 폐기" 혼선을 이걸로 정정.)

**최종 target 구조**:
- **네임스페이스**: `...Script.CardEffectCommons` (location 1 그대로, 카드가 이미 `using ...CardEffectCommons` — 마이그 불필요). Factory/Commons 같은 네임스페이스의 별개 클래스 = AS-IS(global) 관계 미러.
- **파일 경로**: AS-IS와 동일 — `Script/CardEffectFactory.cs`(메인) + `Script/CardEffectFactory/*.cs` + `Script/CardEffectFactory/KeyWordEffects/*.cs`. (upstream diff 기계적용 위해 경로 일치 필수.)
- **클래스**: 하나의 `static partial class CardEffectFactory`. KeyWordEffects도 **partial-class 메서드**(AS-IS대로, 미러의 `static class Reboot` 등 분리구조 폐기). 보조타입(PartitionCondition 등)=`...CardEffectCommons` top-level.
- **메서드**: **AS-IS 63 1:1**(구모델 167 body→kind-클래스/ActivateClass 반환). ~100 발명 메서드는 카드 재포팅 후 제거.
- **위치2 빈 스텁**: 채우는 게 아니라, location 1 monolith(1476 LOC)의 메서드를 **AS-IS 경로 partial 파일로 추출+AS-IS 1:1 재작성**하며 자연 대체. (빈 스텁의 `...Script` 네임스페이스 선언은 채울 때 `...CardEffectCommons`로 교체.)

**실행 = 수직슬라이스(monolith 추출)**: AS-IS 팩토리 파일(예 ChangeDP.cs)별로 → location 1 monolith에서 대응 메서드 찾아 AS-IS 경로 partial 파일로 추출·AS-IS 1:1 재작성·monolith에서 제거 + 호출 카드 재포팅. 동일명 메서드 중복(CS0111) 방지 위해 monolith에서 반드시 제거. 첫 슬라이스=continuous DP(sync, ChangeDPClass 반환, 코루틴 없음 — 가장 clean).

### 11.4 P4 팩토리 층 진행 (2026-07-13, 사용자 "전부 진행")

**완료·검증** (배치별 선언부 344 기준선 + CS0111/0121 0 + 본문 diff 스팟체크):
- **continuous DP 슬라이스**(ChangeDP.cs) — end-to-end 템플릿(팩토리+카드). 카드: 17 ChangeSelfDP 자동바인딩·ST1_12 이미 6-arg·TfxSecurityDpBuff effectName 추가.
- **restriction-gate 16**(CanNotSuspend/Block/BeDeleted/TreatAsDigimon/ImmuneFromDPMinus 등) — 미싱멤버 0.
- **stat/cost + 조건추가 10**(ChangeSAttack/CardDP/OriginDP/LinkMax/PlayCost/DigivolutionCost/AddLink/AddDigivolution/AddAppfusion/Vortex).
- **활성 timing 빌더 22**(monolith 내: ActivateClass 베이스 + OnPlay/WhenMoving/WhenDigivolving/WhenAttacking/OnDeletion/WhenLinking/Security/EndOfAttack/Counter/턴타이밍 11 + AddDetailClass). async 번역=`Func<Hashtable,ActivateClass,IEnumerator>`→`Task`, pass-through 람다 verbatim.
- **=non-keyword 팩토리 partial 27 + 코어 timing 22 = 팩토리 층 대부분 관통.**

**masked latent gaps**(verbatim 유지, work-list=rebuild_p4_factory_missing.md, 선언부 CS0246가 본문 진단 억제→P6에서 표면화):
- **[중대] CanTrigger* impedance**: 미러 `CardEffectCommons.CanTrigger*`(OnPlay/OnMove/WhenDigivolving/OnAttack/OnDeletion/WhenLinking/SecurityEffect/OnPermanentAttack)가 **`CardEffectResolveContext ctx` 기반인데 AS-IS는 `Hashtable`** — 모든 활성효과 트리거 게이트 영향. **CanTrigger* 계열 Hashtable-시그니처 재조정 필요**(P5 트리거/창 핵심). `IsExistOnBattleAreaDigimonTrigger/Activate` 2종은 미러 부재.
- **4 substrate gap**(stat/cost 배치): AttackProcess.SecurityDigimon(HeadlessEntityId? vs CardSource)·CardSource.Owner(HeadlessPlayerId, CanIgnoreDigivolutionRequirement/CanReduceCost 부재)·CardColors(string vs CardColor enum=COLOR-MODEL-DUALITY)·CardSource implicit-bool(Unity truthiness).
- **CARD-MIGRATION-NEEDED**: 시그니처 바뀐 소수(ChangeSAttack scopeAnyPlayer·AddDigivolutionRequirement predicate형·AddSelfDigivolution CardColor param·Vortex/SecurityDP effectName 필수 등) — 대부분 카드는 자동바인딩, 이들만 AS-IS 대조 재작성.

**남은 P4/P5**: KeyWordEffects ~35(sync 기계적/async 신중, 미러 static class→partial 메서드 재구조화) → 인라인 뮤테이션 메서드(PlaySelfTamer 등) → CanTrigger* Hashtable 재조정 → 카드 274 재포팅(대부분 자동) → 4 substrate gap 상환 → P6 dispatch 컷오버(IActivatedCardEffect 제거→274 CS0508+66 CS0246 해소, masked 본문에러 표면화·순차상환) → P7 검증.

### 11.5 1:1 방향성 전수 감사 (2026-07-13, 사용자 요청)

기계 대조(본문 diff 전수 스윕) 결과 — **이식한 층은 검증되게 1:1로 가고 있음**:
- **kind-클래스 73**: 본문 IDENTICAL **61** / 발산 12 = 기채움 11(구모델, task #28 예정) + ActivateClass(14줄=문서화된 coroutine→Task 어댑테이션).
- **팩토리 partial 27/27**: 발산 **0**(IDENTICAL 5 + 문서화 어댑테이션≤8줄 22).
- **timing 빌더**: monolith 내 AS-IS 1:1(스팟 OnPlayClass=Task delegate 교체 외 verbatim), **위치도 올바름**(AS-IS도 메인파일에 둠).

**수치 교정(측정 아티팩트)**: §11의 "AS-IS 63메서드·발명 ~100"은 추출 정규식이 **제네릭 `<T>(`와 KeyWordEffects를 누락**한 과소집계였음. 정정: **AS-IS 팩토리 147종**(메인+partial+KeyWordEffects). 카드→팩토리 호출 265회 중 **진짜 발명 37종/145회**(DrawCardsEffect 19·SelectAndBuffDp 16·AddMemoryTrigger 16·SelectAndDestroy 10…), AS-IS 대응 120회. 즉 카드 재포팅 실규모=발명 145 call-site(+시그니처 변경분)+CS0508 시그니처 274.

**아직 1:1 아닌 잔여(전부 기지·계획됨)**: ①기채움 kind 11(#28) ②monolith 발명 ~109종(카드가 쓰는 동안 잔존→카드 재포팅 후 제거) + AS-IS 대응 미이식 ~15(인라인 뮤테이션·KeyWordEffects) ③카드 274 구시그니처+발명호출 145 ④비-AS-IS 구모델 파일(ActivatedEffects·ContinuousAndRestrictionEffects·CardPortingFramework·*Helpers 다수, P6 은퇴) ⑤CanTrigger* Hashtable impedance(P5) ⑥네임스페이스(문서화된 안정 delta).

### 11.6 팩토리 층 완료 (2026-07-13)

**kind-클래스 73 완전 1:1**(#28로 기채움 11 재포팅 완료 — 72 IDENTICAL + ActivateClass async 어댑테이션).

**팩토리 partial 59개(non-keyword 27 + KeyWordEffects 32) 검증 완료 1:1**: 전수 diff = IDENTICAL(≤2줄) 31 / 문서화 어댑테이션(≤12줄) 25 / 고diff 3(Partition·Decode=미러전용 aux타입 PartitionCondition·const DecodeSourceConditionKey 보존; BlastDNADigivolution=중첩 coroutine→async). **실발산 0.** + monolith 내 활성 timing 빌더 22 AS-IS 1:1(위치도 AS-IS와 동일=메인파일).

**빌드 342**(68 CS0246 + 274 CS0508, CS0246가 344→342로 감소=삭제된 발명 메서드 참조 소멸). monolith 발명 메서드가 배치마다 제거되며 P6 은퇴대상 축소.

**어댑테이션 규칙 확립·일관 적용**: PermanentView→`ICardEffect.ResolvePermanentOfThisCard` 브릿지 · `CanNotBeAffected(effect)`→`(effect.EffectSourceCard?.InstanceId)` · coroutine `IEnumerator`→`async Task`·`StartCoroutine(X)`→`await X` · 네임스페이스 `...CardEffectCommons` · `using ...CardEffects` · aux타입/const 보존.

**팩토리 층 잔여 = 인라인 뮤테이션 메서드**(monolith: PlaySelfTamerSecurityEffect·PlayMindLinkTamer·ReplaceSecurity·ActivateMainOption 등, coroutine 본문이 mutation 헬퍼 호출 — CanTrigger*/뮤테이션헬퍼 재조정과 함께). **다음 대형 페이즈 = CanTrigger* Hashtable 재조정(P5) → 카드 재포팅(발명호출 145+시그니처 274) → 4 substrate gap → P6 dispatch 컷오버(IActivatedCardEffect 제거→masked 표면화·순차상환) → P7 검증(427+RuleAudit).**

### 11.7 substrate 다리 착수 (2026-07-13, 설계+위임 모드)

사용자 지시=Opus 설계·검증만, 실행은 서브에이전트. 병렬 위임(충돌 없는 파일):
- **CanUseEffects 게이트 41파일·~69 CanTrigger*/CanActivate* 완료**(Hashtable-기반 AS-IS 1:1, 스팟 OnAttack.cs 본문 identical). 구 ctx-기반(CardEffectCommons.cs)과 **오버로드 공존**(시그니처 상이) → 팩토리의 hashtable 게이트 호출 바인딩, 구 ctx는 P6 은퇴. CS0246 68→65. **키 계약 보존**(HashtableSetting write ↔ 게이트 read, 양쪽 1:1).
  - **⚠ 행동-fidelity 플래그(P7 상환)**: 3 게이트(CanUnsuspend·CanActivateSuspendCostEffect·CanDeclareOptionDelayEffect)는 기존 미러가 **substrate 재구현**(ContinuousRestrictionGate/DigivolutionStackReader/enteredThisTurn, AS-IS verbatim 아님). CS0111 회피 위해 P5 verbatim 중복 제거하고 substrate 버전 유지 → **행동 동일성 검증 필요**(substrate 버전이 AS-IS와 동치인지, work-list rebuild_p5_gates_missing.md). design item P5-GATE-SUBSTRATE-3.
- **인라인 뮤테이션 팩토리 메서드**(monolith, 위임 실행 중).
- **뮤테이션 헬퍼 = 사실상 이미 존재**(골1~7 substrate, 컴파일 authoritative 확인 — grep 과소집계였음). 실제 미싱 타입 **단 4종**(PlayCardClass·OptionResolutionClass·SkillInfo·OnEnterFieldHashtableParams, 위임 중).

### 11.8 P6 dispatch 컷오버 설계 (2026-07-13)

**현행 구조 파악**:
- `CardEffectDispatch.TryCreate(cardNumber)` → 리플렉션으로 카드 `CEntity_Effect` 서브클래스 인스턴스화(`new BT1_001()`).
- `CardEffectRegistrar.RegisterOnEnterPlay/RegisterFaceUpSecurity` → **이미 신모델 `card.CardEffects(timing)` 호출** → 결과 `ICardEffect`를 `cardEffect.ToBinding(...)`로 `EffectBinding` 변환 → `context.EffectRegistry.Register`.
- 게이트(ContinuousKeywordGate/RestrictionGate)·`ActivatedEffectResolver`·WindowResolver가 `EffectRegistry`(EffectBinding 리스트)를 스캔.

**flip 급소**: 신 `ICardEffect`(추상클래스)엔 `ToBinding` 없음 → `Registrar`의 `.ToBinding` 컴파일 불가. 이게 274 CS0508(카드 override 시그니처)과 함께 남은 선언에러의 뿌리.

**P6 컷오버 설계 (AS-IS 엔진 스캔 모델로)**:
1. **런타임 표현 교체**: `EffectBinding`(data record) → **`ICardEffect` 객체 직접 보유**(추상클래스가 상태+게이트+74인터페이스 구현을 다 가짐). registry = 활성 `ICardEffect` per (card,timing).
2. **연속/제약 게이트**: EffectBinding 스캔 → **`ICardEffect` 객체를 74 마커 인터페이스로 `is`-스캔**(`registry.OfType<IChangeDPEffect>()...GetDP(dp,permanent)` 식) — AS-IS `GetContinuousEffects().Where(is IChangeDPEffect)` 미러.
3. **활성 해소**: ActivatedEffectResolver/EffectBinding → **`ActivateClass.Activate(Hashtable)`** 직접 구동(Optional→Effect→Execute 순서는 파운데이션 `ActivateICardEffectExtensionClass`에 이미 이식).
4. **은퇴**: EffectBinding·EffectRegistry(data-oriented)·ToBinding 경로·구 통합 프리미티브(ActivatedEffects/ContinuousAndRestrictionEffects ~5,300 LOC)·IActivatedCardEffect. → 59 IActivatedCardEffect CS0246 해소.
5. **영향 파일**: EffectRegistry 참조 30·AutoProcessing 20·ActivatedEffectResolver 12·WindowResolver 11·Registrar 8·Dispatch 5 — **엔진(Headless/) 재배선, 카드 무관**.

**핵심 성격**: P6은 배치-포팅이 아니라 **아키텍처 flip**(data-oriented EffectRegistry → AS-IS OOP 스캔 모델). 원자적이라 신중한 단계 설계 필요. 이걸 통과하면 **남은 에러 = 274 카드 시그니처 + masked 카드 본문(발명호출 145) = 전부 카드 층(추후 포팅)**. **행동검증(P7 427+RuleAudit)은 카드까지 green 후**.

**상세 설계 (2026-07-13 심화)**: AS-IS 스캔 패턴은 **Permanent/CardSource 클래스 자체**에 있음 — `Permanent.DP`/`CardColors`/restriction 멤버가 `GetContinuousEffects().Where(cardEffect is IChangeDPEffect).ForEach(e=>dp=((IChangeDPEffect)e).GetDP(dp,this))` 식으로 활성 ICardEffect를 74인터페이스로 `is`-스캔(AS-IS Permanent.cs:2433 등). 미러 현재는 같은 멤버가 **구 게이트**(ContinuousDpGate·ContinuousModifierGate·ContinuousKeywordGate·ContinuousRestrictionGate·ContinuousImmunityGate·DeletionReplacementGate·AttackTargetSwitchGate·BattleDeletionGate·AceOverflowGate ~10개)로 질의.

**flip 단계**:
1. **registry**: `EffectRegistry`(Register(EffectBinding)/GetContinuousEffects→EffectRequest) → **활성 `ICardEffect` 객체 보유**로 교체(또는 병행 ICardEffect-registry).
2. **Registrar**: `cardEffect.ToBinding(...)`→EffectRegistry → **`ICardEffect` 직접 등록**.
3. **Permanent/CardSource**: `GetContinuousEffects()`(registry+inherited+linked에서 활성 ICardEffect 수집) 구현 + DP/color/restriction/immunity 멤버를 **AS-IS 74인터페이스 `is`-스캔으로 재작성**(스캔 순서·aggregation이 AS-IS와 동일해야=행동 급소).
4. **활성 해소**: ActivatedEffectResolver/EffectBinding → **`ActivateClass.Activate(Hashtable)` 직접 구동**(Optional→Effect→Execute 파운데이션 기존).
5. **은퇴**: 구 게이트 ~10·EffectBinding·EffectRegistry(data)·ToBinding·구 프리미티브·IActivatedCardEffect.
**행동 급소**: §3의 상환버그(emit 반전·double-fire·batch-collapse·order-choice) 재발 방지 — 스캔/집계/해소 순서가 AS-IS와 동일해야. flip은 게이트별로 단계 실행(DP게이트→restriction→immunity…) + 각 단계 후 masked 표면화 관찰.

### 11.9 "카드 제외 소규모 정리" 조사 결과 (2026-07-13) — 전부 flip-의존

사용자가 (A) 카드 제외 소규모 경계 정리 선택 → 조사 결과 **정리 대상이 전부 flip-의존/masked**로, 지금 손대면 검증 불가한 dormant 코드(행동-fidelity 규율 위반). 상세:
- **3-게이트 substrate(P5-GATE-SUBSTRATE-3)**: CanUnsuspend/CanActivateSuspendCostEffect/CanDeclareOptionDelayEffect 미러 substrate가 **구 게이트**(ContinuousRestrictionGate 등)로 판정. **행동은 동치**(구 게이트=rebuild 전 427/427 검증). AS-IS는 `permanent.IsSuspended && permanent.CanUnsuspend`(속성 스캔) → **flip 때 AS-IS 속성으로 전환**(구 게이트 은퇴와 함께). **지금 행동버그 없음** → 조치 불요, flip-time.
- **gap3 color(COLOR-MODEL-DUALITY)**: AS-IS `CardSource.CardColors`가 `GetContinuousEffects().Filter(is IChangeCardColorEffect).GetCardColors(...)` = **신모델 스캔 = flip 자체**. 미러 string+FoldListTransforms(구 게이트). → color 재조정 = flip의 일부(GetContinuousEffects 필요). flip-time.
- **gap1 SecurityDigimon**(id vs CardSource)·**gap2 Owner→Player**(타입 재조정, 다수 소비자): masked 본문, flip/카드와 얽힘. **gap4 implicit-bool**: 현재 사용처 0(비-이슈).

**결론**: **카드 제외 엔진 작업 = 모델 층(파운데이션·kind-클래스·팩토리·게이트·미싱타입) 완료가 진짜 경계.** DP/color/restriction/immunity 스캔·3-게이트·gap 전부 **P6 flip(GetContinuousEffects+is-스캔)에 수렴**하고, flip은 구모델 은퇴↔카드 신모델 전환이라 **카드 층을 요구**. 따라서 (A)의 실질 산출 = **경계가 crisp함을 확인(행동버그 없음)+문서화**. 실제 다음 한 걸음은 flip=카드.

### 11.2 선결 해소: CARDSOURCE-EQUALITY (2026-07-13)

첫 슬라이스 착수 중 **필수 선결** 발견: AS-IS 팩토리 로직이 `permanent == targetPermanent`·`LinkedCards.Contains(card)`·`EffectSourceCard == permanent.TopCard` 등 **CardSource/Permanent identity 비교**에 의존하는데, 미러는 뷰가 매 접근 새 인스턴스라 **reference equality → 절대 안 맞음**(P1 design item CARDSOURCE-EQUALITY, ICardEffect.CanActivate/IsSameEffect도 동일 의존). 이걸 먼저 해소 안 하면 포팅한 팩토리·게이트가 **조용히 틀림**(red window가 가림).
- **해소**: 미러 `CardSource`·`Permanent`에 **instance-id 기반 value equality**(Equals/GetHashCode/`==`/`!=`) 부여 — 동일 match(Context/`_context`) 내 동일 InstanceId면 equal. 뷰 두 개가 같은 live 카드면 equal. Permanent는 top-card InstanceId 기준(top 변경 시 새 identity지만 same-moment 비교=common case 정확, across-time는 trigger 시점 id 캡처가 커버). 선언부 clean(344 불변, 신규 에러 0).
- 이로써 ICardEffect 게이팅(CanActivate inherited/linked)·팩토리 PermanentCondition·IsSameEffect가 **올바르게 동작** → P4 슬라이스 언블록.

### 11.3 경로 충실도 교정 (2026-07-13, 사용자 "최종 유지보수성 우선, 커도 하라")

**사용자 지적**("진짜 1:1이 맞아? 경로랑 내부 로직 같게 만들었어?") → 감사 결과: **내부 로직은 1:1(검증됨)이나 파일 경로가 어긋남**. 미러가 골1~7 관행으로 **거의 모든 core 파일을 `Script/CardEffectCommons/` 폴더에 몰아넣었는데 AS-IS는 `Script/` flat**. upstream이 `DataBase.cs`·`CardSource.cs` 등을 바꾸면 diff가 안 맞음 → 마이그 목적 훼손.

**교정 완료** (파일을 AS-IS flat 경로로 이동, **네임스페이스 유지**=참조 무영향, 빌드 344 불변 전과정 검증 → 순수 relocation·무손상):
- `Script/`로 이동: **CardSource·Permanent·Player·GameContext·CardEffectCommons(메인)·CardEffectFactory·DataBase·GManager·IBattle·CEntity_Base(CardColor)·TurnStateMachine**(GameContext에서 분리). 골7.5가 모든 AS-IS 경로에 빈 stub을 만들어둬서 "stub 채우기/그 자리로 이동"이 맞음.
- **조건 9종**(Assembly/Link/AppFusion/Jogress/DigiXros/Burst + Element)을 **CardSource.cs로 통합**(AS-IS는 전부 CardSource.cs). Conditions.cs=미러-발명 DigivolveCost만 잔존. CardEffectCommons/CardEffectInterfaces.cs(주석-only 잔재) 삭제.
- **CardEffectCommons/에 정당히 잔존**(AS-IS도 그 폴더): HashtableSetting·GetFromHashtable·GameContextDeterminarion·CustomMessage·DNADigivolveEffects·DigiXrosEffects·IsDigivolvedByTheEffect·RevealLibrary·ShowReducedCost·TrashDigivolutionCards·TrashLinkedCards.
- **미러-발명 헬퍼/구프리미티브**(AS-IS 경로 無): ModifierHelpers·ActivatedEffects·ContinuousAndRestrictionEffects·TriggeredEffects·CardEffectDispatch/Registrar·CardPortingFramework·*Helpers 등 → CardEffectCommons/ 유지(P6 dispatch 컷오버에서 은퇴).
- **실수·교훈**: git mv가 untracked라 실패했는데 골7.5 stub 존재를 몰라 DataBase/GManager 실내용 잠시 삭제→즉시 복구. **규칙: 모든 AS-IS 경로에 골7.5 빈 stub 존재 → 신규 타입은 CardEffectCommons/에 만들지 말고 "그 AS-IS 경로 stub을 채운다"**.
- **네임스페이스**: 전부 `...CardEffectCommons` 유지(경로만 교정). AS-IS는 네임스페이스 無라 어떤 네임스페이스든 비-1:1·안정 delta → 정규화는 별도 후속(참조 대량churn 회피).
- **정정 추정**: P3가 "발명"이 아니라 "스켈레톤 채우기"로 판명 → §5의 "3 per-kind 4~6세션"은 하향 여지(kind-클래스 기계적), 단 카드 274 재포팅이 신규 계상됨(설계서는 "카드 이미 1:1"로 0 계상했으나 실제 재포팅 필요).
