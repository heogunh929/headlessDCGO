# AS-IS ↔ TO-BE 프리미티브 매핑 정의서 v2 (전수 감사)

- **작성**: 2026-07-23 · **v2 전면 재작성** · 기준 HEAD `6bf5a053` (`id-표면 flip 캠페인 완주`, 워킹트리 clean 시점)
- **방법론**: **실소스 grep 전수 대조**(문서·기억·카탈로그 불신). TO-BE = `src/HeadlessDCGO.Engine/Assets/Scripts/`, AS-IS = `DCGO/Assets/Scripts/Script/`. 모든 grep은 `--binary-files=text`(비-UTF8 조용한 스킵 방지). 모든 심볼 주장에 `파일:라인` 앵커를 부착했고, 전칭 주장에는 측정 명령을 병기했다.
- **이전 판**: 2026-07-08 원판(11 병렬 감사 에이전트, 182 심볼 PASS/FAIL 렌즈) + 2026-07-23 오전 R0~R5 개정 지층은 **git 히스토리에 보존**(`git show 6bf5a053^:docs/audit/asis_tobe_primitive_mapping.md`). 본 v2는 그 지층을 걷어내고, 오늘 오후 **id-표면 flip 캠페인**(커밋 `6bf5a053`) 이후의 **현재 실상태**를 단일 평면으로 재확정한다. 구판의 `CPF:NNNN` 주소는 전부 **역사적 별칭(스테일)** — 그 메서드들은 지금 `CardEffectFactory.cs`/`CardEffectCommons.cs` 및 분해된 partial 파일에 산다.
- **자매 정본**: 카드 1장 포팅 절차·스켈레톤·번역 규칙은 [`card_porting_canonical_2026-07-23.md`](card_porting_canonical_2026-07-23.md)(이하 **canonical**). 본 문서는 **매핑 판정**(무엇이 1:1인가/무엇이 잔여인가)을, canonical은 **포팅 how-to**를 담당한다. 번역 등재 표는 canonical §9/§9.1이 정본이며 본 문서는 이를 참조만 한다.

---

## 0. 판정 분류 (5종)

| 분류 | 정의 | 측정 |
|---|---|---|
| **1:1** | 경로·이름·시그니처·로직 동일 (substrate 번역도 불요) | AS-IS ↔ 미러 grep 시그니처 동형 |
| **1:1+번역** | 로직 동일, **등재된 substrate 번역만** 차이 (coroutine→Task, Player-is-PlayerId, id-핸들 운반 등 — canonical §9/§9.1의 등재 목록에 한함) | 카드 헤더 `// 치환:` 블록 + canonical §9.1 등재표 |
| **STOP** | 진짜 AS-IS 갭 — 충실 번역 불가. **live 4좌석**(전수, §5.1) | `throw new NotSupportedException` grep = 4 |
| **구조-only 폴딩** | 결과동일, 1:1 심볼 부재 (R2 정본 확정분: uniform ActivateClass 폴딩 19 등) | AS-IS 발화 경로 확인 후 폴딩 확정 |
| **UI-스코프밖** | 클라이언트 화면 컴포넌트 — 게임규칙 모델 아님. **7종**(§5.3) | Unity `GameObject`/패널 소비 |

> **엄격 기준**(구판 유지): 결과-동일이라도 **AS-IS가 평가하는 술어를 미러가 무시·하드코딩·평면화하면 FAIL**. 단, **구조-only 폴딩**과 **STOP**은 "지금 틀린 출력을 내는 버그"와 구별한다 — flip 이후 **실동작-버그(도달가능 입력에서 틀린 결과) = 0**(§1 계기판).

---

## 1. flip 캠페인 반영 — id-형 표면 소멸 선언 + 계기판

2026-07-23 오후 **id-표면 flip 캠페인**(`6bf5a053`)이 매핑 지형을 바꿨다. 이전 판이 "미러 편의 번역"으로 계상한 **id-술어 껍데기**(`Func<HeadlessEntityId,bool>` 술어·`PermanentOf...ById` 어댑터)를 **표면 자체에서 삭제**하고, 9종 DEVIATED 시그니처를 **AS-IS 형태로 복원**했다. 결과: **이 문서의 매핑에 없는 id-형 표면을 코드에서 보면 stale 참조를 베낀 것이다**(컴파일도 안 된다).

### 1.1 계기판 지표 (전수 실측 — 대량 포팅 트랜치 게이트에 포함)

| 지표 | 명령 | 실측 |
|---|---|---|
| 카드 파일 id-술어 껍데기 | `grep -rn "Func<HeadlessEntityId" src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect --include=*.cs \| grep -v ":[0-9]*: *//"` | **0** |
| 미러층(Assets/Scripts) id-술어 껍데기 | `… src/HeadlessDCGO.Engine/Assets/Scripts …` | **0** |
| Select* 계열 id-술어 껍데기 | `grep -n "Func<HeadlessEntityId" src/…/Script/Select*.cs \| grep -v "://"` | **0** |
| id-어댑터 (`...ById(`, `PermanentOfThisCardById`) in 카드 파일 | grep = | **0** |
| live `throw new NotSupportedException/NotImplementedException` (전 src) | `grep -rnE "throw new (NotSupportedException\|NotImplementedException)" src --include=*.cs \| grep -v "///"` | **4** (전부 §5.1 STOP 좌석) |

> **잔존 `Func<HeadlessEntityId` 4건은 카드/미러층이 아니라 substrate**: `Headless/Runtime/RevealAndSelect.cs:28,264,320`·`Headless/Runtime/EffectDrivenAttack.cs:405`. canonical §9.1 **등재 어휘**(mutation-sink id 인자·choice 조건 운반)라 정당 — 카드가 직접 쓰는 술어가 아니다.

### 1.2 물리 삭제된 발명 심볼 (살아있다는 서술 금지)

아래는 flip 캠페인 및 선행 소프트 동결에서 **물리 삭제**됐다. **클래스/인터페이스 정의 grep = 0**, 남은 참조는 전부 **은퇴-핀 주석**(retirement-guard 관례)뿐이다. 측정: `grep -rnE "(class|interface|struct|enum)\s+<심볼>" src --include=*.cs = 0`.

| 삭제 심볼 | 상태 |
|---|---|
| `EffectRegistry` / `EffectBinding` / `.ToBinding()` / `LegacyBindingBridge` / `IActivatedCardEffect`(마커) | 타입 정의 0. 참조는 `Headless/Effects/MatchStateMutationSink.cs`·`HeadlessCardEffectContract.cs` 등의 **은퇴 주석**("producer 0 → dead write, retired")뿐 |
| `PermanentEffectFactoryBinding` (string-key 오버로드) | 정의 0. 유일 참조 = `PermanentEffectFactory.cs:137` 삭제-기록 주석 |
| `CardPortingFramework` (**타입 및 `.cs` 파일**) | 파일 물리 삭제(`find src -name CardPortingFramework.cs` = ∅). 참조 3건 = 전부 "deleted CardPortingFramework.cs" 이주-기록 주석 |
| `InheritedEffectHelpers.cs` · `DeDigivolveDestroyHelpers` · `CostReductionScope`(enum) | 파일/정의 0 (`grep -rl` = 0) |
| id-형 오버로드·발명 setter(`SetAttackOptions`/`SetCanEndSelectCondition` id-폼)·발명 `_canTargetCondition`(id-폼) | flip에서 삭제, AS-IS 형태로 복원 |

---

## 2. 전수 인벤토리 매핑 (그룹별 · 현재 src 재실측)

### 2.0 구조 파리티 (AS-IS ↔ TO-BE 파일/심볼 수 — 미러 원칙 검증)

| 그룹 | AS-IS | TO-BE | 파리티 | 측정 |
|---|---|---|---|---|
| CardEffectFactory (monolith + partial) | monolith 41 public + partial 59파일 | monolith `CardEffectFactory.cs`(~64 메서드) + `CardEffectFactory/*.cs` **59파일** | **동형** | `find …/CardEffectFactory -name '*.cs' \| grep -vc meta` = 59 (양쪽) |
| CardEffects (kind-class) | 73파일 | 73파일 | **1:1 파일 파리티** | `find …/CardEffects -name '*.cs' \| grep -vc meta` = 73 (양쪽) |
| PermanentEffectFactory | 6 시그니처 | 6 시그니처 | **동형**(순서만 재배치) | §2.4 |
| 효과 본체 `*Class` (mirror Script 전역 유니크) | 98 | 92 | 6 폴딩/이주(§2.5) | `grep -rhoE "class \w+Class" … \| sort -u \| wc -l` |
| CardEffectCommons | monolith 40 public static + subdir 121파일 | monolith `CardEffectCommons.cs` 249 public static + subdir 151파일 | **미러+substrate 확장** | §2.3 |
| Select* 계열 | Script 루트 산재 | Script 루트 13파일 | 시그니처 복원(§2.6) | §2.6 |

> **주**: Commons는 파일-1:1이 아니다 — AS-IS는 얇은 모놀리스(40) + 두꺼운 subdir(121파일), TO-BE는 두꺼운 모놀리스(249) + subdir(151파일, substrate 헬퍼 포함). 미러 원칙은 **동일 경로·동일 이름의 심볼 존재**로 충족되며, 개별 메서드 시그니처는 canonical §7.2의 정본 grep 순서로 조회한다.

### 2.1 CardEffectFactory (monolith + partial 59, ~170 메서드)

flip 이후 팩토리 표면은 **AS-IS 이름 그대로** 미러에 존재한다(canonical §7.2). 판정 분포:

- **1:1 / 1:1+번역 (대다수)**: `UseRequirements`(:722↔미러), `GetJogressConditionClass`(:752), 전 `Continuous*`/`Grant*`/`ChangeCardColor`·`ChangeTraits` 등 — 술어(`Func<Permanent,bool>`)·게이트를 실평가. substrate 번역은 coroutine→Task, `card.Owner`→`new Player(...)`(canonical §9)만.
- **구조-only 폴딩 (19 — 타이밍-클래스)**: `ActivateClass`(:910) + `OnPlayClass`(:978)·`OnDeletionClass`(:1078)·`CounterClass`(:1206)·`EndOfAttackClass`(:1170)·`EndOfYourTurnClass`(:1320)·`EndOfYourOpponentsTurnClass`(:1392)·`EndOfAllTurnsClass`(:1430)·`AllTurnsClass`(:1447)·`OpponentsTurnClass`(:1409)·`TurnTimingClass`(:1248)·`StartOfYourTurnClass`(:1286)·`StartOfYourMainPhaseClass`(:1303)·`StartOfYourOpponentsMainPhaseClass`(:1375)·`StartOfOpponentsTurnClass`(:1358)·`YourTurnClass`(:1337)·`WhenAttackingClass`(:1044)·`WhenDigivolvingClass`(:1011)·`WhenLinkingClass`(:1111)·`WhenMovingClass`(:940)·`SecurityClass`(:1146). → **uniform `ActivateClass`+triggerGate 카드별 인라인이 AS-IS 정본 경로임이 R2에서 확정**(§3). 1:1 팩토리 심볼 부재를 **갭으로 계상하지 않는다**.
- **구조-only 폴딩 (기타)**: `ActivateClassesForSharedEffects`(:828) — 공유 hashValue once-per-turn 캡. 07-18 실측서 "순수 디스패처(공유 캡 불요)"로 격파, 다중발동 위험 실증 0. 잔여=심볼 조합성뿐.

> AS-IS 라인은 `DCGO/Assets/Scripts/Script/CardEffectFactory.cs`. 대응 미러 메서드는 `grep -rn "<이름>" src/…/Script/CardEffectFactory*` 또는 `docs/porting/symbol_map.csv`(444행)로 조회.

### 2.2 KeyWordEffects (grant 32 · commons 31)

- `src/…/Script/CardEffectFactory/KeyWordEffects/*.cs` = **32파일**, `src/…/Script/CardEffectCommons/KeyWordEffects/*.cs` = **31파일**. 전부 **1:1 / 1:1+번역**.
- flip 재확인: `Scapegoat`·`TreatAsDigimon`·`Vortex`(구판 "name-only seal FAIL" 오분류)는 **LIVE** — producer=`ContinuousPlayerScopeKeywordEffect`, 소비자=`ContinuousKeywordGate`/`DeletionReplacementGate`. `Vortex`는 flip에서 `HasMatchConditionOpponentsPermanent`(Tamer 스캔 폭 수리) 재배선.
- `BlastDNACondition`은 삭제된 `CardPortingFramework.cs`에서 **AS-IS 정위치** `CardEffectFactory/KeyWordEffects/BlastDNADigivolution.cs`로 이주(flip 마지막 조각).

### 2.3 CardEffectCommons (monolith 249 + subdir 151파일)

- **판정 가족 (전수 확인)**: flip이 복원한 4종 — `HasMatchConditionPermanent`·`MatchConditionPermanentCount`·`HasMatchConditionOpponentsPermanent`·`CanTriggerOnPermanentDeleted` — 전부 **live**(소비자 배선 확인: `DNADigivolveEffects.cs`·`KeyWordEffects/Raid.cs`·`Save.cs`·`Alliance.cs`·`CanUseEffects/OnDeletion.cs`·`Ascension.cs`). AS-IS 형태(`Func<Permanent,bool>`/`Func<CardSource,bool>`)로 복원.
- **1:1+번역 (핵심 처리 가족)**: `*PeremanentAndProcessAccordingToResult`(Delete/Suspend/Bounce/DeckBounce), `PlaceDelayOptionCards`, `AddThisCardToHand`, `PlayPermanentCards`/`PlayOptionCards`, `TrashDigivolutionCardsFromTopOrBottom`, `DigivolveInto*`(IgnoreRequirement enum 복원분) — 로직 동형, substrate 번역만.
- **구조-only 잔여 (표본 — 저우선)**: `Trash*ProcessAccordingToResult` success 콜백이 `int`(삭제 카드 리스트 아님). 현 카드 결과 동일, 계약 복원은 저우선.
- **RevealLibrary**: `src/…/Script/CardEffectCommons/RevealLibrary.cs`로 **정위치 이주**(flip). `SelectCardConditionClass` 2종이 여기 동거(AS-IS 동형). 소비: `CardEffectFactory.cs`·`CardEffectCommons.cs`.
- 전수 시그니처는 canonical §7.2 grep 순서로 조회(빈도 가중 없음 — 전수 포팅 전제).

### 2.4 PermanentEffectFactory (6 시그니처 — 전량 실측)

`src/…/Script/PermanentEffectFactory.cs`, AS-IS 형태 오버로드로 전량 live(순서만 재배치):

| 시그니처 | TO-BE 라인 | AS-IS 라인 | 판정 |
|---|---|---|---|
| `CanNotSwitchAttackTargetEffect(…)` | :22 | :109 | 1:1 (CanNotBeAffected 드롭·topCard stale-capture = 구조-only) |
| `DigimonEffectImmunity(Permanent)` | :53 | :51 | **1:1** (상대+Digimon+this-permanent 면역 미러) |
| `OptionEffectImmunity(Permanent)` | :86 | :80 | **1:1** (상대+Option) |
| `CollisionEffect(…)` | :121 | :131 | 1:1 (반환형: TO-BE `ICardEffect` vs AS-IS `CollisionClass` — 인터페이스 상위형) |
| `DeleteSelfEffect(…)` | :153 | :11 | 1:1 (deleteOnOwnturn/OpponentsTurn 분기 보존) |
| `AddDetailClass(…)` | :206 | :146 | 구조-only (detail/triggerEffect 유지, cosmetic) |

> **`PermanentEffectFactoryBinding` string-key 폼은 물리 삭제**(§1.2). 발명 binding 오버로드를 찾으면 stale 문서를 베낀 것.

### 2.5 효과 본체 `*Class` (mirror 92 유니크)

- **1:1 / 1:1+번역 (대다수)**: AceOverflow·Blocker·Reboot·Partition·MindLink·CanNotBeDestroyed(4-arg battle 술어 1:1)·ChangeCardColor/Level/Traits·Iceclad 등. 구판 P0 버그(과다면역·dead·미발화)는 전량 상환(`ImmuneFromDPMinus`·`InvertSAttack`·`ImmuneFromDeDigivolve`·`CannotReturnToLibrary`·`ReturnToLibraryBottomDigivolutionCards`·`CanNotSelectBySkill` 등).
- **AS-IS 이름 있으나 TO-BE 클래스-타입 없음 = 6 (전부 폴딩/이주, 발화 경로 확인 — 갭 아님)**:

| AS-IS `*Class` | TO-BE 거처 | 판정 |
|---|---|---|
| `ArmorPurgeClass` | `DeDigivolveHelpers`(behavior), `CardController.cs`/`PermanentBookkeepingStore.cs` 소비 | 구조-only 폴딩 |
| `DeckBottomBounceClass` | sink 위임(`CardEffectCommons.cs`·`ActivatedEffectResolver.cs`) | 구조-only 폴딩 |
| `DeckTopBounceClass` | select 경로(`SelectPermanentEffect.cs`) | 구조-only 폴딩 (⚠️ 프롬프트-추가 여부 verify 미해소) |
| `HatchDigiEggClass` | sink 위임(`TurnStateMachine.cs`·`ActivatedEffectResolver.cs`) | 구조-only 폴딩 |
| `RevealLibraryClass` | `CardEffectCommons/RevealLibrary.cs`로 이주 | 구조-only 폴딩 (IsBeingRevealed 미모델 = 풀정보라 무의미) |
| `SplitClass` | **AS-IS `DeckData.cs:840` = `public static class SplitClass` (덱-유틸, 카드효과 아님)**; `AutoProcessing.cs`·`SelectBurstDigivolutionEffect.cs` 소비 | 스코프밖 유틸(카드-프리미티브 아님) |

- **딥 잔여 1건**: `CanNotTrashFromDigivolutionCardsClass` — kind-class 미러 FULL(`CardEffects/…`), 소비자 배선(MatchStateMutationSink·DeletionSourceTrash) 존재. 단 **전역 source-protection 스캔 심층 충실성**은 유일 호출카드 `BT9_109`가 STOP-포팅 상태라 정본 카드로 재검증 불가(§5.1).

### 2.6 Select* 계열 (Script 루트 13파일 — flip 후 시그니처 1:1)

`src/…/Script/Select*.cs` 13파일. flip이 **id-형 발명 시그니처를 AS-IS 형태로 전면 복원**:

| 파일 | flip 복원 시그니처 | AS-IS 대조 | 판정 |
|---|---|---|---|
| `SelectPermanentEffect.cs` | `Func<Permanent,bool> _canTargetCondition`(:49)·`_defenderCondition`(:63, AS-IS :123)·`Func<List<Permanent>,bool> _canEndSelectCondition`(:66, AS-IS :104)·`_canTargetCondition_ByPreSelecetedList`(:73) | AS-IS `SetUp(Func<Permanent,bool> canTargetCondition, …)` :12 | **1:1+번역**(coroutine→Task) |
| `SelectCardEffect.cs` | `Func<CardSource,bool> _canTargetCondition`(:215, AS-IS 이름 복원)·`_canTargetCondition_ByPreSelecetedList`(:216)·`_canEndSelectConditionCard`(:217). 후보를 `new CardSource(...)`로 구체화해 술어 평가(:99) | AS-IS `SetUp(Func<CardSource,bool> …)` :10 | **1:1+번역** |
| `SelectHandEffect.cs` | `Func<CardSource,bool> _canTargetCondition`(:58, AS-IS :97)·`_canEndSelectCondition`(:60, AS-IS :101) | AS-IS 라인 앵커 부착 | **1:1+번역** |
| `SelectCountEffect`·`SelectAttackEffect`·`SelectDNACondition`·`SelectAppFusionEffect`·`SelectBurstDigivolutionEffect` | AS-IS 술어/카운트 형 | — | 1:1+번역 |
| `SelectJogressEffect.cs` | **의도적 bodiless** — UI-플로우 오케스트레이터. substrate DNA 경로(`DNADigivolveEffects`+`SpecialPlayRecipeRegistry`)가 게임 결정 담당 | AS-IS는 UI 코루틴 | **UI-스코프밖**(§5.3) |
| `SelectCardPanel.cs`·`SelectDeck.cs` | near-empty | AS-IS UI 패널 | **UI-스코프밖** |

> Select* 계열 id-술어 껍데기 census = **0**(§1.1). `SelectAssemblyClass`·`SelectDigiXrosClass`는 특수플레이 pre-play 조합성 잔여(§5.4).

---

## 3. 정본 idiom 색인 (대량 포팅 참조 · 현재 코퍼스 기준)

> **용도**: Haiku 파일럿/대량 포팅이 프리미티브 호출 시 참조할 **정본 예제 카드**. 카드 경로 규약 = `src/HeadlessDCGO.Engine/Assets/Scripts/CardEffect/<SET>/<Color>/<ID>.cs`. 코퍼스 규모(실측): **factory/ActivateClass 호출 카드 426장**(`grep -rl 'ActivateClass\|CardEffectFactory\.\|CardEffectCommons\.' …/CardEffect` = 426; >30줄 본체 385장). 이전 census "339장"은 07-18 커버리지 시점 수치 — 이후 포팅으로 증가. 예제 선정은 `coverage_exemplar_audit_2026-07-18.md` §3 클린-커버(✅) 앵커와 연결.

### 3.1 프리미티브 축

| 프리미티브/축 | 정본 예제 카드 | 관용구 메모 |
|---|---|---|
| `ActivateClass`(uniform 타이밍-창) | BT13_023, BT16_025 | 전 타이밍-클래스 19종의 **정본 진입점** — triggerGate로 창 지정(§4) |
| `SelectPermanentEffect` | BT13_023, BT16_025, BT19_024 | 후보-스코프 연속 제약(CanNotSelectBySkill 등)이 `BuildRequest` 단일 chokepoint에서 배제; 술어=`Func<Permanent,bool>` |
| `SelectCardEffect` / `SelectHandEffect` | BT19_024, BT1_010/011/039/056 | 술어=`Func<CardSource,bool>`(flip 복원) |
| `DrawClass` | BT1_003, BT1_006 | OnDraw 트리거 sink 배선 확인 |
| `DestroyPermanentsClass` | BT1_084, BT9_081 | CanBeDestroyedBySkill 가드 경유 |
| `SuspendPermanentsClass` | BT16_025, BT1_086 | already-suspended 필터·DPWhenSuspended 스냅샷 |
| `TrashDigivolutionCardsFromTopOrBottom` | BT13_023, BT1_043 | (count,isFromTop) 계약 |
| `ChangeCostClass` | BT2_023, BT2_099 | CostModification 정본 |
| `CanNotPlayClass` | BT8_057, EX1_072 | ICanNotPlay 연속스캔 |
| `PlayPermanentCards` | BT19_024, BT1_044 | CanPlayAsNewPermanent 게이트 |
| `AddActivateMainOptionSecurityEffect` | BT1_094, BT1_101 | [Main]→[Security] 파생 |

### 3.2 키워드/특수플레이 축

Retaliation=BT2_074 · Pierce=BT1_022/026/081 (OnDetermineDoSecurityCheck) · Blocker=BT19_071/BT1_023/031 · Jamming=BT1_016/098/BT2_057 · Reboot=BT2_055/063/065 · Collision/Fragment=EX8_051 · Scapegoat=EX8_061(LIVE) · Barrier=BT14_035 · DigiBurst=ST4_13 · TokenPlay=BT8_092(Form/Attribute 방출) · CostModification=BT2_023/099 · Jogress·DNA=BT16_025(substrate ChoiceProvider 경로).

### 3.3 창-타이밍 축

OnDeclaration=BT1_088/089 · OnEnterFieldAnyone=BT16_025/BT19_024 · OptionSkill=BT1_091/092/093 · OnDestroyedAnyone=BT1_030/035/049 · WhenRemoveField=BT16_025 · WhenPermanentWouldBeDeleted=BT13_023/BT14_035/EX8_051 · OnEndTurn=BT1_040 · OnStartTurn=BT1_085/086/087 · OnStartMainPhase=ST15_02 · OnEndBattle=BT1_077/112/ST4_11 · OnEndAttack=BT19_024/BT1_081 · OnDigivolutionCardDiscarded=BT2_085/EX8_051 · WhenLinked=BT22_003 · OnTappedAnyone=ST4_14 · OnUnTappedAnyone=BT2_002/BT8_057 · OnAddDigivolutionCards=BT22_044/EX6_001 · OnAllyAttack=BT13_023/BT16_025 · OnCounterTiming=EX8_051 · SecuritySkill=BT1_085/086/087.

> **신규 축 witness**: coverage §4 greedy set-cover 상위 30장(BT25_104·LM_054·BT21_030 …)은 미커버 축 witness 후보. STOP-리스크 21장(coverage §6)은 Opus 프리미티브 선행 트리거([[primitive-predevelopment-role]]).

---

## 4. uniform ActivateClass 폴딩의 종결 (R2 정본 확정 근거)

구판이 최대 FAIL 원인으로 지목한 **타이밍-클래스 폴딩 19종**(§2.1)은, 창엔진 `SkillWindowSupply` 컷오버 + A군 키워드 재하우징 18/18 이후 **uniform `ActivateClass`가 AS-IS 정본 경로임이 확정**됐다(`coverage_exemplar_audit` §2 "은퇴가 아니라 표현 방식 확정"). 카드는 `ActivateClass`(정본 시그니처 = canonical §3.1 `Script/CardEffects/ActivateClass.cs` 실측) 하나에 triggerGate로 창을 지정한다. → 이 19종은 **1:1 팩토리 심볼 부재를 갭으로 계상하지 않으며**, 정본 카드가 해당 타이밍 창을 실발화하면 커버로 친다.

---

## 5. 잔여 비-1:1 전수 (빈도 가중 없음 — 전수 분류)

> flip 이후 **실동작-버그(도달가능 입력에서 틀린 결과) = 0**(§1.1 계기판). 아래는 **STOP(진짜 AS-IS 갭)** + **구조-only 이탈(결과동일)** + **UI 스코프밖**의 전수.

### 5.1 STOP — live 4좌석 (전수, 원장 매핑)

| 좌석 | 원장 id | 성격 |
|---|---|---|
| `CardEffectCommons/TrashLinkedCards.cs:72` | RD-SKEL-01 | **진짜 AS-IS 한계** — LinkedCards 풀 vs DigivolutionCards.Count 예산 비대칭이 충실 headless 번역 시 비종결 루프. 얕은 뭉갬/발명 가드 회피 위해 STOP-가드 |
| `GManager.cs:198` | RD-W4-3 | **조건부** — 브릿지 W4 미지원 컴포넌트 타입. 새 컴포넌트 요청 시에만 좌석화(심층-불가 아님) |
| `Permanent.cs:4549` | MIG4-DETACH-LIVE-TOP | latent 가드(호출자 0) — 직접-라이브-탑 detach |
| `CardController.cs:4283` | 리뷰3 P2-② | **fidelity 갭 아님** — 코퍼스 중복-키 방어가드(효과당 1키 강제; 잘못 배선한 카드를 잡음) |

측정: `grep -rnE "throw new (NotSupportedException|NotImplementedException)" src --include=*.cs | grep -v "///"` = **4** (위 4좌석과 정확히 일치).

### 5.2 구조-only 이탈 (결과동일 FAIL — 저우선)

- **타이밍-클래스 uniform 폴딩 (19)**: §2.1·§4 — R2 정본 확정(갭 계상 안 함).
- **공유 once-per-turn**: `ActivateClassesForSharedEffects`(공유 hashValue 캡). 07-18 실측 "순수 디스패처(캡 불요)"로 격파.
- **폴딩/이주 6종**: ArmorPurge·DeckBottomBounce·DeckTopBounce·HatchDigiEgg·RevealLibrary·Split (§2.5 — 발화 경로 확인, behavior present).
- **delta 재구현 (2)**: `ChangeLinkMaxClass`(고정 ±N)·`ChangeSAttackClass`(delta+Func). 현 카드 동일결과; arbitrary `Func<Permanent,int,int>`만 미표현.
- **success 콜백 계약**: `Trash*/Play*ProcessAccordingToResult` success=int(삭제 리스트 아님). 현 카드 동일결과.
- **cosmetic (2)**: `AddDetailClass`(표시 전용)·`CanNotSwitchAttackTargetEffect`(topCard stale-capture).

### 5.3 UI 스코프밖 (엔진 동결 계약 제외 — 7종)

- **UNPORTED UI 패널 (4)**: `SelectBattleDeck`·`SelectBattleMode`·`SelectCommand`·`SelectCommandPanel` — 배틀덱/모드/커맨드 선택 **화면**.
- **near-empty UI 스텁 (2)**: `SelectCardPanel`·`SelectDeck`.
- **재분류 bodiless (1)**: `SelectJogressEffect` — UI-플로우 오케스트레이터, substrate DNA 경로가 게임 결정 담당(§2.6). 엔진-프리미티브 본체 없음(의도적).

### 5.4 포팅 중 정직-STOP 예상 (신규 카드가 건드리면 수확)

Assembly/DigiXros 인터랙티브 pre-play(RD-P6C1-5) · Burst/AppFusion select 컴포넌트(RD-P6C1-6) · Execute 발화(RD-R2-01) · Ascension writer(RD-3A-01) · AddSkillClass 중첩부여(nested-grant STOP) · Digisorption 진입(G11) · 고급 SelectCardCondition 술어(뭉개면 FAIL) · CanNotPutField 필드제약 · 전역 digi-source 보호(딥 잔여, `CanNotTrashFromDigivolutionCards` BT9_109). → 정본 포팅이 실호출하면 **runtime throw 아닌 정직 STOP 마커**(`design item RD-x-NN`)로 원장 등재 후 Opus 프리미티브 선행([[primitive-predevelopment-role]]).

---

## 6. Haiku 파일럿 채점 연동

Haiku 파일럿 산출물(포팅된 카드 파일)을 판정할 때 **본 매핑표 + 구조 대조(AS-IS 원문 diff)를 채점 기준에 포함**한다:

1. **매핑 대조**: 파일럿이 호출한 프리미티브가 §2의 판정(1:1 / 1:1+번역)에 부합하는가. **본 문서에 없는 id-형 표면·발명 심볼(§1.2)을 쓰면 즉시 FAIL**(stale 참조를 베낀 것 — 컴파일도 안 됨).
2. **AS-IS 원문 diff**: 카드가 미러한 AS-IS 파일(`DCGO/…`)을 열어 술어·게이트·분기가 **평면화/하드코딩 없이** 옮겨졌는지 라인 대조. 술어 받는 팩토리는 술어를 **평가**해야 한다(뭉개면 FAIL — [[fidelity-over-coverage]]).
3. **번역 등재 확인**: 카드 헤더 `// 치환:` 블록의 substrate 번역이 canonical §9/§9.1 **등재 목록 안**인가. 미등재 편의 번역 = 이탈.
4. **계기판 게이트**(§1.1): 트랜치마다 `Func<HeadlessEntityId`(카드 파일) = 0 · live STOP = 4좌석 유지 · id-어댑터 = 0.
5. **STOP 정당성**: 파일럿이 낸 STOP이 §5.1/§5.4의 진짜 갭인가, 아니면 실재 surface를 "없다"고 오판했는가(최대 오류 — canonical §8). runtime throw로 낸 STOP은 FAIL, `design item RD-x-NN` 마커만 정당.
6. **완료 정의**([[goal-witness-operating-mode]]): witness green(펌프-드라이브) + 인접 회귀 green + `RuleAudit 0` + 적대 리뷰(잣대=AS-IS diff).

> **게이트 근거값**: 마지막 전체-스위트 완주 인증 = **425/425**(소프트 동결 폴리시 시점, `freeze_evidence_2026-07-23.md`). flip 캠페인은 배치별 witness + 다이제스트 trio bit-identical + 커밋 시점 **429/429** 보고(전체 재인증 런은 외부 정지로 미완주 — 재인증 1회는 사용자 옵션). Haiku 트랜치는 **인접 스위트 green + 다이제스트 불변**을 배치 게이트로 쓰고, 전체 재인증은 아크 종료 시 1회.
