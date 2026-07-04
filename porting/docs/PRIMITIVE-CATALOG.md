# 프리미티브 카탈로그 (카드-facing 팩토리 전수)

> 자동생성 · `CardEffectFactory` 공개 팩토리 **128종**. 포팅 시 원본 `CardEffectFactory.<이름>(...)` 호출을 아래 헤드리스 시그니처로 미러한다(이름 동일이 원칙). 시그니처가 다르면 아래를 따른다.

> 공통 인자: `card`=`CardSource`(호스트), `isInheritedEffect`=진화원 상속 여부(대개 false), `condition`=`Func<bool>?`(발동 게이트, 없으면 null). **모든 술어/값 인자는 실동작한다** — 원본이 넘기는 값을 그대로 넘겨라(null로 뭉개지 말 것): `permanentCondition`(대상 술어), `skillCondition`(원인-효과 술어), `level/minLevel/maxLevel`(진화 레벨 게이트), `trashValue`(Fragment X), `cardSourceConditions`(Partition 색 그룹), `isLinkedEffect`(링크 상태 게이트 — 원본이 `SetIsLinkedEffect(true)` 하면 true), `defenderCondition`/`canAttackPlayer`(공격 대상 술어).
>
> `rootCardEffect` 인자는 넘겨도 무시된다(원본 소비자 = 중복판정 전용; 헤드리스는 binding id로 이미 구분) — null/원본값 어느 쪽이든 무해.

> 재생성: `python3 scripts/generate-primitive-catalog.py` (알파벳 마스터만; 카테고리 빠른참조는 수기).


## 카테고리별 빠른참조


### 키워드 grant (36)

- **AllianceSelfEffect** — grants Alliance to self (Batch2)
- **AllianceStaticEffect** — grants Alliance to the owner's Digimon (player-scope keyword)
- **ArmorPurgeEffect** — grants ArmorPurge to self (Batch2).
- **AscensionSelfEffect** — grants the Ascension keyword (post-deletion → security)
- **BarrierSelfEffect** — grants Barrier (deletion-replacement) to self.
- **BlitzSelfEffect** — grants Blitz to self (Batch2).
- **BlockerSelfStaticEffect** — grants Blocker to self.
- **BlockerStaticEffect** — grants Blocker to a set of permanents
- **CollisionSelfStaticEffect** — grants Collision (forced-block) to self.
- **CollisionStaticEffect** — grants Collision to the owner's Digimon (player-scope keyword)
- **DecodeSelfEffect** — grants Decode to self (Batch2).
- **DecoySelfEffect** — grants Decoy (deletion-replacement) to self
- **EvadeSelfEffect** — grants Evade (deletion-replacement) to self.
- **ExecuteSelfEffect** — grants Execute to self. 원본 등록 타이밍 = `EffectTiming.OnEndTurn`(그대로 미러). 턴종료 공격창·플레이어/미서스펀드 타깃·공격 후 self-delete는 엔진이 자동(EndOfTurnEffectAttack) — 카드는 grant만.
- **FortitudeSelfEffect** — grants Fortitude (post-deletion replay) to self.
- **FragmentSelfEffect** — grants Fragment (deletion-replacement) to self.
- **IcecladSelfStaticEffect** — grants Iceclad to self.
- **JammingSelfStaticEffect** — grants Jamming to self.
- **JammingStaticEffect** — grants Jamming to the owner's Digimon (player-scope keyword)
- **MaterialSaveEffect** — move of this Digimon's digivolution cards under another of your Digimon (, chosen at port time).
- **MindLinkSelfEffect** — grants the MindLink keyword (Tamer↔Digimon link)
- **OverclockSelfEffect** — grants Overclock to self (Batch2).
- **PartitionSelfEffect** — grants Partition to self (Batch2)
- **PlayMindLinkTamerFromDigivolutionCards** — plays a Tamer under-card (MindLink) from a Digimon's digivolution stack onto the field (cost-free)
- **ProgressSelfStaticEffect** — grants Progress to self (Batch2).
- **RaidSelfEffect** — grants Raid (attack-switch) to self
- **RebootSelfStaticEffect** — grants Reboot to self (Batch1).
- **RebootStaticEffect** — grants Reboot to the owner's Digimon (player-scope)
- **RetaliationSelfEffect** — grants Retaliation to self (Batch2)
- **RushSelfStaticEffect** — grants Rush to self (Batch2).
- **RushStaticEffect** — grants Rush to the owner's Digimon (player-scope)
- **SaveEffect** — grants Save (deletion-replacement: place under a Tamer instead of trashing) to self.
- **ScapegoatSelfEffect** — grants Scapegoat (deletion-replacement) to self.
- **TreatAsDigimonStaticEffect** — grants the TreatAsDigimon keyword
- **VortexCanAttackPlayersStaticEffect** — grants Vortex to the owner's Digimon (player-scope keyword)
- **VortexSelfEffect** — grants Vortex to self (Batch2)

### 제약/면역 (19)

- **CanNotAffectedStaticEffect** — this Digimon is immune to opponent effects
- **CanNotAttackSelfStaticEffect** — "this Digimon cannot attack" (self)
- **CanNotAttackStaticEffect** — the scoped player's Digimon cannot attack (player-scope CannotAttack restriction consulted by AttackPermanentA
- **CanNotBeAttackedSelfStaticEffect** — this Digimon cannot be attacked (self CannotBeAttacked restriction consulted on the defender by AttackPermanen
- **CanNotBeBlockedStaticSelfEffect** — this Digimon cannot be blocked (unblockable); consulted by BlockTiming when enumerating blocker candidates.
- **CanNotBeDestroyedByBattleStaticEffect** — this Digimon cannot be deleted in battle (effect deletion still applies)
- **CanNotBeDestroyedBySkillStaticEffect** — this Digimon cannot be deleted by effects/skills (battle deletion still applies); consulted by the effect-sour
- **CanNotBeDestroyedStaticEffect** — registers a continuous Delete/Prevent replacement on the HOST (battle + effect deletion), honoured by BattleDe
- **CanNotBeTrashedBySkillStaticEffect** — this Digimon's digivolution cards cannot be trashed by effects
- **CanNotBlockStaticEffect** — the scoped player's Digimon cannot block (player-scope CannotBlock restriction).
- **CanNotBlockStaticSelfEffect** — this Digimon cannot block (self CannotBlock restriction consulted by ContinuousRestrictionGate.EvaluateBlock).
- **CanNotDigivolveStaticEffect** — a continuous "the scoped player's Digimon (optionally of ) cannot digivolve" restriction
- **CanNotDigivolveStaticSelfEffect** — a continuous "this card cannot be digivolved (as the digivolution source)" restriction on self
- **CannotReturnToDeckStaticEffect** — this Digimon cannot be returned to the deck (self restriction consulted by the ReturnToDeck sink paths).
- **CannotReturnToHandStaticEffect** — this Digimon cannot be returned to hand (self restriction consulted by the ReturnToHand sink path).
- **CantSuspendStaticEffect** — this Digimon cannot be suspended (self CannotSuspend restriction consulted by the Suspend sink path)
- **CantUnsuspendStaticEffect** — this Digimon does not unsuspend; consulted by the Unsuspend step.
- **ImmuneFromDPMinusStaticEffect** — this Digimon is immune to DP-reducing effects (D-A3)
- **ImmuneStackTrashingClass** — alias of .

### DP/SA 수정자 (3)

- **ChangeBaseDPGlobalEffect** — continuous ±base-DP on the owner's Digimon (player-scope BaseDp modifier consulted by ContinuousDpGate)
- **ChangeDPStaticEffect** — continuous ±DP on a set of permanents
- **ChangeSelfDPStaticEffect** — continuous ±DP on self.

### 진화/요건 (11)

- **AddDigivolutionRequirementStaticEffect** — grant this card an ADDITIONAL digivolution path "from Lv"
- **BlastDNADigivolveEffect** — declares this card's Blast-DNA recipe: the material names (from ) fuse as sources, played for free (DnaDigivol
- **BlastDigivolveEffect** — declares this card as Blast-capable: it may digivolve onto a single matching battle-area Digimon for free (Spe
- **ChangeDigivolutionCostStaticEffect** — (PRIM-W1-3) Dynamic (Func&lt;int&gt;) variant of .
- **ChangeDigivolutionCostStaticEffect** — continuous ±digivolution cost on self (delta)
- **JogressEffectFromNames** — declares this card's Jogress (DNA digivolve) recipe: the two material names that fuse under it (SpecialPlayKin
- **ReturnToLibraryBottomDigivolutionCardsClass** — returns the host's own digivolution (under-)cards to the bottom of the deck (activated).
- **SelectAndDeDigivolveEffect** — (PRIM-W5) Declarative form of the AS-IS CardEffectCommons.DigivolveIntoHandOrTrashCard(..): select up to battl
- **SelectAndTrashDigivolutionEffect** — An activated "select up to opponent Digimon and trash of each host's digivolution cards from the bottom/top" e
- **TrainingEffect** — activated [Breeding]: suspend self, place the top deck card at the bottom of self's digivolution stack.
- **UseRequirements** — lets this card digivolve ignoring the COLOR part of the printed requirement (level still enforced)

### 메모리 (8)

- **AddMemoryTriggerEffect** — A triggered "[When ...] gain/lose N memory" effect (the common ActivateClass memory form)
- **AddSelfDigivolutionRequirementStaticEffect** — adds an alternative digivolution source for THIS card: it may digivolve from any under-card matching (for memo
- **EoTLose3Memory** — "[End of Your Turn] lose 3 memory."
- **Gain1MemoryTamerOpponentDigimonEffect** — "[Start of Your Turn] if your opponent has a Digimon, gain 1 memory." (main-phase timing mapped to OnStartTurn
- **Gain1MemoryTamerOwnerDigimonConditionalEffect** — "[Start of Your Turn] if you have a matching Digimon, gain 1 memory." The per-permanent predicate is captured 
- **Gain2MemoryOptionDelayEffect** — a delayed "gain 2 memory" (resolves at the next start of the owner's turn)
- **GainMemoryActivatedEffect** — An activated "gain/lose memory" skill (Option [Main] / [Security], e.g
- **SetMemoryTo3TamerEffect** — "[Start of Your Turn] If you have 2 or less memory, set your memory to 3." (Tamer memory-setter)

### 트리거 (3)

- **RecoveryTriggerEffect** — A triggered "[When ...] &lt;Recovery + (Deck)&gt;" effect (e.g
- **SelfDpBuffTriggerEffect** — A triggered "[When ...] this Digimon gets + DP for " effect (e.g
- **UnsuspendSelfTriggerEffect** — A triggered "[When ...] unsuspend this Digimon" effect (e.g

### 링크 (4)

- **ChangeLinkMaxStaticEffect** — continuous ±link-maximum on the owner's Digimon (player-scope LinkedMaxDelta modifier, queryable)
- **ChangeSelfLinkMaxStaticEffect** — continuous ±link-maximum on self
- **GrantedReduceLinkCostClass** — continuous link-cost reduction
- **LinkEffect** — the &lt;Link&gt; activation: attach this card to a chosen own Digimon, paying the link cost (read from the car

### 시큐리티 (16)

- **ChangeSAttackStaticEffect** — continuous ±security attack on the owner's Digimon (player-scope SA modifier consulted by ContinuousModifierGa
- **ChangeSecurityDigimonCardDPStaticEffect** — continuous ±DP on the owner's Security-zone Digimon, optionally conditional (e.g
- **ChangeSelfSAttackStaticEffect** — continuous ±security attack on self with a dynamic (read-time) value.
- **ChangeSelfSAttackStaticEffect** — continuous ±security attack on self.
- **InvertSAttackStaticEffect** — continuous invert-security-attack on self (consumed by ContinuousModifierGate.ResolveSecurityAttack).
- **OpponentScopeBuffSAttackEffect** — An activated "all of your opponent's Digimon get + Security Attack for " player-scope effect, scoped to (e.g
- **PlaceSelfDelayOptionSecurityEffect** — "[Security] place this card in the battle area" (a Delay Option triggered from security)
- **PlaySelfDigimonAfterBattleSecurityEffect** — "[Security] play this Digimon" (from security to the battle area)
- **PlaySelfTamerSecurityEffect** — a Tamer's [Security] "play this Tamer"
- **PlayerScopeBuffDpEffect** — An activated "all your Digimon get + DP for " player-scope effect (e.g
- **PlayerScopeBuffSAttackEffect** — An activated "all your Digimon gain + Security Attack for " player-scope effect (e.g
- **PlayerScopeBuffSecurityDpEffect** — An activated "all your Security Digimon get + DP for " player-scope effect, scoped to the owner's Security-zon
- **ReplaceBottomSecurityWithFaceUpOptionEffect** — Option [Main]: add the bottom security card to hand, then place this card face up as the bottom security card.
- **ReplaceBottomSecurityWithFaceUpOptionMainEffect** — Main-phase variant of .
- **ReplaceTopSecurityWithFaceUpOptionMainEffect** — Option [Main]: add the TOP security card to hand, then place this card face up as the top security card.
- **SelectAndBuffSAttackEffect** — An activated "select up to Digimon and give each + Security Attack for " effect (e.g

### 기타/특수 (21)

- **AddThisCardToHandEffect** — return this card to the owner's hand.
- **ChangeCardNamesStaticEffect** — grants this card an additional name (), folded into CardSource.CardNames.
- **ChangePlayCostStaticEffect** — continuous ±play cost with a dynamic (read-time) value.
- **ChangePlayCostStaticEffect** — continuous ±play cost
- **DigiXrosEffect** — the faithful form of the AS-IS AddDigiXrosConditionClass whose getDigiXrosCondition returns DigiXrosConditionE
- **DigiXrosEffectFromNames** — declares this card's DigiXros recipe: the named materials (hand/field) that fuse under it
- **DrawCardsEffect** — the declarative form of the AS-IS new DrawClass(owner, count, ...).Draw() coroutine: the owner draws cards
- **JogressEffect** — (PRIM-W5) Jogress with ARBITRARY per-material predicates (faithful form of AddJogressConditionClass's GetJogre
- **MandatorySelfPlayCostReduction** — dynamic-magnitude self play-cost reduction.
- **MandatorySelfPlayCostReduction** — reduce THIS card's play cost by (a positive magnitude; the original does cost -= _changeValue())
- **PierceSelfEffect** — grants Piercing to self.
- **RevealLibraryClass** — reveals the top cards of the owner's deck
- **SelectAndBounceEffect** — (PRIM-W5) Declarative form of the AS-IS bounce coroutine: select up to matching permanents and return them to 
- **SelectAndBounceEffect** — An activated "select up to Digimon and return each to its owner's hand" effect (Option [Main] bounce, e.g
- **SelectAndBuffDpEffect** — An activated "select up to matching Digimon and give each + DP for " effect (e.g
- **SelectAndDestroyEffect** — An activated "select up to matching permanents and delete them" effect (Option [Main] delete skill, e.g
- **SelectAndPlayFromZoneEffect** — (PRIM-W5) Declarative form of the AS-IS CardEffectCommons.PlayPermanentCards(.., root) coroutine: select up to
- **SelectAndRestrictEffect** — An activated "select up to Digimon and make each unable to attack and/or block for " effect (e.g
- **SelectAndSuspendEffect** — (PRIM-W5) Declarative form of the AS-IS new SuspendPermanentsClass(perms, ..).Tap() coroutine: select up to ma
- **SelectAndUnsuspendEffect** — (PRIM-W5) Declarative form of the AS-IS unsuspend coroutine: select up to matching permanents and unsuspend th
- **SimplifiedRevealDeckTopCardsAndSelect** — (PRIM-W5) Mirror of the AS-IS CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect: reveal the top cards of


### 클래스 직접 생성 표면 (팩토리 아님 — 원본도 클래스를 직접 생성)

원본 카드가 `new <클래스>()` + `SetUp...` 으로 쓰는 것들. 헤드리스도 같은 이름·같은 SetUp으로 미러한다.

- **PartitionCondition** (`CardEffectFactory.KeyWordEffects`) — Partition 색 그룹 정의. 생성자 3형 그대로: `new PartitionCondition(4, "Red")` / `new PartitionCondition(4, "Red", "Yellow")` / `new PartitionCondition("이름")`. **항상 2개 리스트**로 `PartitionSelfEffect(..., cardSourceConditions: new[]{cond0, cond1})`. 색은 문자열("Red" 등).
- **MindLinkClass** (`CardEffectCommons.KeyWordEffects`) — Mind Link는 키워드가 아니라 프로세스: `new MindLinkClass(tamerPermanent, digimonCondition, activateClass)` → `BuildRequest()`(선택 optional·max1) / `MindLink(선택된디지몬Id)`(테이머를 진화원 bottom에 배치). 역방향은 `PlayMindLinkTamerFromDigivolutionCards`.
- **ChangeCardLevelClass / ChangePermanentLevelClass / ChangeCardColorClass / ChangeBaseCardColorClass / ChangeTraitsClass** (`CardEffects`) — 레벨/색/특성 변경 연속효과. 원본과 동일 패턴: `SetUpICardEffect(설명, CanUseCondition, card)` + `SetUpChange...Class(변환 Func)` → `cardEffects.Add(인스턴스)`. 변환 Func는 원본 클로저를 그대로(누산기 in→out, 색/특성은 `List<string>`). 뷰(`CardSource.Level/CardColors/CardTraits`, `Permanent.Level`)가 라이브 폴딩한다.
- **SelectPermanentEffect** (`Script`) — `SetUp(...)` 뒤 추가 세터: `SetDegenerationCount(n)`(Degenerate 모드 디진화 수), `SetAttackOptions(canAttackPlayer, defenderCondition)`(Attack 모드 — 다중 공격자는 자동 순차 큐), `SetCanEndSelectCondition(집합술어)`(조합 제약 — resolve 시 중앙 거부). Attack 모드 실행은 `TryOpenAttack(context, selected)`.
- **RevealAndSelect / RevealDeckTopCardsAndSelect** — 리빌-선택: 다중 조건은 **팩토리** `CardEffectFactory.RevealDeckTopCardsAndSelect(card, revealCount, RevealSelectPass[], remainingCardsPlace, 설명, canNoAction, isOpponentDeck, mutualConditions)`(원본과 같은 이름; 조건별 `RevealSelectPass(조건, maxCount, 목적지, 메시지, canNoSelect, canEndNotMax)`, `Mode.Custom`→`RevealDestination.Custom`은 이동 없이 `RevealMultiSelectEffect.CustomSelections`에 기록). 단순형은 기존 `SimplifiedRevealDeckTopCardsAndSelect`. 엔진-레벨 인터랙티브 플로우(`RequestChoice`/`RequestMultiChoice`/`RevealAndProcessAllAsync`)는 엔진 코드 전용 — 카드는 팩토리만 쓴다.
- **[Counter] 효과 마커** — 카운터 타이밍 효과가 진짜 [Counter]면 binding values에 `AutoProcessingTriggerCollector.IsCounterEffectKey = true`(원본 `IsCounterEffect` 미러; 비-[Counter] 카운터타이밍 효과가 먼저 해소됨).
- **dual 카드** — 카드가 두 종류(예: Digimon/Option)면 정의 메타 `CardRecord.AdditionalCardTypesKey`(`"cardTypes"`)에 추가 종류 배열. 모든 타입 판정(`IsDigimon/IsOption/...`)이 양쪽을 본다.
- **AddAssemblyConditionClass / AssemblyCondition / AssemblyConditionElement** (AD1-A) — Assembly 특수플레이 선언. 원본 AD1_025 형태 그대로: timing None에서 `new AddAssemblyConditionClass()` + `SetUpICardEffect` + `SetUpAddAssemblyConditionClass(GetAssembly)` + `SetNotShowUI(true)`; `GetAssembly`는 `new AssemblyCondition(new List<AssemblyConditionElement>{ new(술어, selectMessage: "...", elementCount: 1), ... }, reduceCost: N)` 반환. 재료는 **자기 트래시**에서, full set일 때만 -reduceCost, 진입 후 진화원 bottom 스택 — 전부 엔진(PlayCardAction)이 자동. 필드-대체(`ICanSelectAssemblyEffect`)는 미모델(STOP).
- **CanNotSwitchAttackTargetClass / PermanentEffectFactory.CanNotSwitchAttackTargetEffect** (AD1-S) — "공격 대상 변경 불가"(블록+재타게팅 양쪽 차단). 원본 `UntilEachTurnEndEffects.Add(_ => PermanentEffectFactory.CanNotSwitchAttackTargetEffect(perm, activateClass))` → `ctx.EffectRegistry.Register(PermanentEffectFactory.CanNotSwitchAttackTargetEffect(perm, activateClass).ToBinding(id, EffectDuration.UntilEachTurnEnd))`. 직접 생성형은 클래스 그대로(`SetUpCanNotSwitchAttackTargetClass(자체 술어)`).
- **CardEffectCommons.GainCanNotBeDeletedByBattle(targetPermanent, 4-인자술어, EffectDuration, sourceCard, effectName)** (AD1-G) — 시한부 전투삭제 면역 grant(동기, bool 반환). 원본 코루틴 호출을 동명 커먼즈로 미러. 4-인자 술어는 현재 공격 상태로 라이브 평가됨.


## 알파벳 마스터 (이름 → 시그니처)

| 팩토리 | 반환 | 시그니처 |
|---|---|---|
| `AddAppfuseMethodByCondition` | ICardEffect | `ICardEffect AddAppfuseMethodByCondition(IReadOnlyList<Func<CardSource, bool>> cardConditions, CardSource card, int cost = 0, string effectName = "App Fusion")` |
| `AddAppfuseMethodByName` | ICardEffect | `ICardEffect AddAppfuseMethodByName(IReadOnlyList<string> cardNames, CardSource card, int cost = 0, string effectName = "App Fusion")` |
| `AddDetailClass` | ICardEffect | `ICardEffect AddDetailClass(Func<bool>? canUseCondition, Func<Permanent, bool>? permanentCondition, string detail, bool triggerEffect, CardSource card)` |
| `AddDigivolutionRequirementStaticEffect` | ICardEffect | `ICardEffect AddDigivolutionRequirementStaticEffect(string fromColor, int fromLevel, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `AddMemoryTriggerEffect` | ICardEffect | `ICardEffect AddMemoryTriggerEffect(EffectTiming timing, int amount, bool isInheritedEffect, CardSource card, Func<bool>? condition, string description, Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null, bool? isOptional = null)` |
| `AddSelfDigivolutionRequirementStaticEffect` | ICardEffect | `ICardEffect AddSelfDigivolutionRequirementStaticEffect(Func<Permanent, bool> permanentCondition, int digivolutionCost, bool ignoreDigivolutionRequirement, CardSource card, Func<bool>? condition, string? effectName = null, Func<CardSource, bool>? cardCondition = null, Func<int>? costEquation = null, int level = -1, int minLevel = -1, int maxLevel = -1)` |
| `AddSelfLinkConditionStaticEffect` | ICardEffect | `ICardEffect AddSelfLinkConditionStaticEffect(Func<Permanent, bool> permanentCondition, int linkCost, CardSource card, Func<bool>? condition = null, Func<CardSource, bool>? cardCondition = null, string? effectName = null)` |
| `AddThisCardToHandEffect` | IActivatedCardEffect | `IActivatedCardEffect AddThisCardToHandEffect(CardSource card)` |
| `AllianceSelfEffect` | ICardEffect | `ICardEffect AllianceSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `AllianceStaticEffect` | ICardEffect | `ICardEffect AllianceStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ArmorPurgeEffect` | ICardEffect | `ICardEffect ArmorPurgeEffect(CardSource card)` |
| `ArtsDigivolveEffect` | IActivatedCardEffect | `IActivatedCardEffect ArtsDigivolveEffect(CardSource card)` |
| `AscensionSelfEffect` | ICardEffect | `ICardEffect AscensionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false)` |
| `BarrierSelfEffect` | ICardEffect | `ICardEffect BarrierSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `BlastDigivolveEffect` | ICardEffect | `ICardEffect BlastDigivolveEffect(CardSource card, Func<bool>? condition)` |
| `BlastDNADigivolveEffect` | ICardEffect | `ICardEffect BlastDNADigivolveEffect(CardSource card, IReadOnlyList<BlastDNACondition> blastDNAConditions, Func<bool>? condition)` |
| `BlitzSelfEffect` | ICardEffect | `ICardEffect BlitzSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `BlockerSelfStaticEffect` | ICardEffect | `ICardEffect BlockerSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `BlockerStaticEffect` | ICardEffect | `ICardEffect BlockerStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false)` |
| `CanNotAffectedStaticEffect` | ICardEffect | `ICardEffect CanNotAffectedStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? skillCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `CanNotAttackSelfStaticEffect` | ICardEffect | `ICardEffect CanNotAttackSelfStaticEffect(Func<Permanent, bool>? defenderCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null)` |
| `CanNotAttackStaticEffect` | ICardEffect | `ICardEffect CanNotAttackStaticEffect(HeadlessPlayerId scopePlayerId, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null)` |
| `CanNotBeAttackedSelfStaticEffect` | ICardEffect | `ICardEffect CanNotBeAttackedSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `CanNotBeBlockedStaticSelfEffect` | ICardEffect | `ICardEffect CanNotBeBlockedStaticSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `CanNotBeDestroyedByBattleStaticEffect` | ICardEffect | `ICardEffect CanNotBeDestroyedByBattleStaticEffect(Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition, Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null, bool isLinkedEffect = false)` |
| `CanNotBeDestroyedBySkillStaticEffect` | ICardEffect | `ICardEffect CanNotBeDestroyedBySkillStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `CanNotBeDestroyedStaticEffect` | ICardEffect | `ICardEffect CanNotBeDestroyedStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null)` |
| `CanNotBeTrashedBySkillStaticEffect` | ICardEffect | `ICardEffect CanNotBeTrashedBySkillStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null)` |
| `CanNotBlockStaticEffect` | ICardEffect | `ICardEffect CanNotBlockStaticEffect(HeadlessPlayerId scopePlayerId, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `CanNotBlockStaticSelfEffect` | ICardEffect | `ICardEffect CanNotBlockStaticSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `CanNotDigivolveStaticEffect` | ICardEffect | `ICardEffect CanNotDigivolveStaticEffect(HeadlessPlayerId scopePlayerId, string? scopeCardType, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `CanNotDigivolveStaticSelfEffect` | ICardEffect | `ICardEffect CanNotDigivolveStaticSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `CannotReturnToDeckStaticEffect` | ICardEffect | `ICardEffect CannotReturnToDeckStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null)` |
| `CannotReturnToHandStaticEffect` | ICardEffect | `ICardEffect CannotReturnToHandStaticEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null)` |
| `CantSuspendStaticEffect` | ICardEffect | `ICardEffect CantSuspendStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null)` |
| `CantUnsuspendStaticEffect` | ICardEffect | `ICardEffect CantUnsuspendStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangeBaseDPGlobalEffect` | ICardEffect | `ICardEffect ChangeBaseDPGlobalEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangeCardNamesStaticEffect` | ICardEffect | `ICardEffect ChangeCardNamesStaticEffect(string addedName, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangeDigivolutionCostStaticEffect` | ICardEffect | `ICardEffect ChangeDigivolutionCostStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangeDigivolutionCostStaticEffect` | ICardEffect | `ICardEffect ChangeDigivolutionCostStaticEffect(Func<int> changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangeDPStaticEffect` | ICardEffect | `ICardEffect ChangeDPStaticEffect(Func<Permanent, bool> permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<string>? effectName = null)` |
| `ChangeLinkMaxStaticEffect` | ICardEffect | `ICardEffect ChangeLinkMaxStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangePlayCostStaticEffect` | ICardEffect | `ICardEffect ChangePlayCostStaticEffect(int changeValue, Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool setFixedCost)` |
| `ChangePlayCostStaticEffect` | ICardEffect | `ICardEffect ChangePlayCostStaticEffect(Func<int> changeValue, Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool setFixedCost)` |
| `ChangeSAttackStaticEffect` | ICardEffect | `ICardEffect ChangeSAttackStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangeSecurityDigimonCardDPStaticEffect` | ICardEffect | `ICardEffect ChangeSecurityDigimonCardDPStaticEffect(Func<CardSource, bool> cardCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null)` |
| `ChangeSelfDPStaticEffect` | ICardEffect | `ICardEffect ChangeSelfDPStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangeSelfLinkMaxStaticEffect` | ICardEffect | `ICardEffect ChangeSelfLinkMaxStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangeSelfSAttackStaticEffect` | ICardEffect | `ICardEffect ChangeSelfSAttackStaticEffect(int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ChangeSelfSAttackStaticEffect` | ICardEffect | `ICardEffect ChangeSelfSAttackStaticEffect(Func<int> changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `CollisionSelfStaticEffect` | ICardEffect | `ICardEffect CollisionSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false)` |
| `CollisionStaticEffect` | ICardEffect | `ICardEffect CollisionStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `DecodeSelfEffect` | ICardEffect | `ICardEffect DecodeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `DecoySelfEffect` | ICardEffect | `ICardEffect DecoySelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, Func<Permanent, bool>? permanentCondition = null, string? effectName = null, string? effectDescription = null)` |
| `DigiXrosEffect` | ICardEffect | `ICardEffect DigiXrosEffect(CardSource card, int costReduction, params SpecialPlayMaterial[] materials)` |
| `DigiXrosEffectFromNames` | ICardEffect | `ICardEffect DigiXrosEffectFromNames(CardSource card, int costReduction, object? canTargetCondition = null, params string[] names)` |
| `DrawCardsEffect` | IActivatedCardEffect | `IActivatedCardEffect DrawCardsEffect(CardSource card, int count)` |
| `EoTLose3Memory` | ICardEffect | `ICardEffect EoTLose3Memory(CardSource card)` |
| `EvadeSelfEffect` | ICardEffect | `ICardEffect EvadeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ExecuteSelfEffect` | ICardEffect | `ICardEffect ExecuteSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `FortitudeSelfEffect` | ICardEffect | `ICardEffect FortitudeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `FragmentSelfEffect` | ICardEffect | `ICardEffect FragmentSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, int trashValue = 0, string? effectName = null, string? effectDescription = null)` |
| `Gain1MemoryTamerOpponentDigimonEffect` | ICardEffect | `ICardEffect Gain1MemoryTamerOpponentDigimonEffect(CardSource card)` |
| `Gain1MemoryTamerOwnerDigimonConditionalEffect` | ICardEffect | `ICardEffect Gain1MemoryTamerOwnerDigimonConditionalEffect(string effectDescription, Func<Permanent, bool>? permanentCondition, Func<bool>? condition, CardSource card)` |
| `Gain2MemoryOptionDelayEffect` | ICardEffect | `ICardEffect Gain2MemoryOptionDelayEffect(CardSource card)` |
| `GainMemoryActivatedEffect` | ICardEffect | `ICardEffect GainMemoryActivatedEffect(CardSource card, int amount, string description)` |
| `GetJogressConditionClass` | ICardEffect | `ICardEffect GetJogressConditionClass(Func<Permanent, bool> permanentCondition1, string description1, Func<Permanent, bool> permanentCondition2, string description2, CardSource card, int cost = 0, Func<bool>? canUseCondition = null)` |
| `GrantedReduceLinkCostClass` | ICardEffect | `ICardEffect GrantedReduceLinkCostClass(CardSource card, int reducedCost, bool isInheritedEffect = false, Func<bool>? condition = null)` |
| `IcecladSelfStaticEffect` | ICardEffect | `ICardEffect IcecladSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ImmuneFromDPMinusStaticEffect` | ICardEffect | `ICardEffect ImmuneFromDPMinusStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `ImmuneStackTrashingClass` | ICardEffect | `ICardEffect ImmuneStackTrashingClass(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `InvertSAttackStaticEffect` | ICardEffect | `ICardEffect InvertSAttackStaticEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `JammingSelfStaticEffect` | ICardEffect | `ICardEffect JammingSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `JammingStaticEffect` | ICardEffect | `ICardEffect JammingStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false)` |
| `JogressEffect` | ICardEffect | `ICardEffect JogressEffect(CardSource card, Func<bool>? condition, params SpecialPlayMaterial[] materials)` |
| `JogressEffectFromNames` | ICardEffect | `ICardEffect JogressEffectFromNames(CardSource card, Func<bool>? condition, params string[] names)` |
| `LinkEffect` | ICardEffect | `ICardEffect LinkEffect(CardSource card, Func<bool>? condition = null)` |
| `MandatorySelfPlayCostReduction` | ICardEffect | `ICardEffect MandatorySelfPlayCostReduction(int changeValue, CardSource card, Func<bool>? condition = null)` |
| `MandatorySelfPlayCostReduction` | ICardEffect | `ICardEffect MandatorySelfPlayCostReduction(Func<int> changeValue, CardSource card, Func<bool>? condition = null)` |
| `MaterialSaveEffect` | IActivatedCardEffect | `IActivatedCardEffect MaterialSaveEffect(CardSource card, HeadlessEntityId destinationId, int count)` |
| `MindLinkSelfEffect` | ICardEffect | `ICardEffect MindLinkSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `OpponentScopeBuffSAttackEffect` | ICardEffect | `ICardEffect OpponentScopeBuffSAttackEffect(CardSource card, int changeValue, EffectDuration duration, HeadlessPlayerId opponentId, string description)` |
| `OverclockSelfEffect` | ICardEffect | `ICardEffect OverclockSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `PartitionSelfEffect` | ICardEffect | `ICardEffect PartitionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, IReadOnlyList<PartitionCondition>? cardSourceConditions = null)` |
| `PierceSelfEffect` | ICardEffect | `ICardEffect PierceSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `PlaceSelfDelayOptionSecurityEffect` | ICardEffect | `ICardEffect PlaceSelfDelayOptionSecurityEffect(CardSource card)` |
| `PlayerScopeBuffDpEffect` | ICardEffect | `ICardEffect PlayerScopeBuffDpEffect(CardSource card, int changeValue, EffectDuration duration, string description)` |
| `PlayerScopeBuffSAttackEffect` | ICardEffect | `ICardEffect PlayerScopeBuffSAttackEffect(CardSource card, int changeValue, EffectDuration duration, string description)` |
| `PlayerScopeBuffSecurityDpEffect` | ICardEffect | `ICardEffect PlayerScopeBuffSecurityDpEffect(CardSource card, int changeValue, EffectDuration duration, string description)` |
| `PlayMindLinkTamerFromDigivolutionCards` | IActivatedCardEffect | `IActivatedCardEffect PlayMindLinkTamerFromDigivolutionCards(CardSource card, string cardName, string effectDescription)` |
| `PlaySelfDigimonAfterBattleSecurityEffect` | ICardEffect | `ICardEffect PlaySelfDigimonAfterBattleSecurityEffect(CardSource card)` |
| `PlaySelfTamerSecurityEffect` | ICardEffect | `ICardEffect PlaySelfTamerSecurityEffect(CardSource card)` |
| `ProgressSelfStaticEffect` | ICardEffect | `ICardEffect ProgressSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `RaidSelfEffect` | ICardEffect | `ICardEffect RaidSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, ICardEffect? rootCardEffect = null, bool isLinkedEffect = false)` |
| `RebootSelfStaticEffect` | ICardEffect | `ICardEffect RebootSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `RebootStaticEffect` | ICardEffect | `ICardEffect RebootStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false)` |
| `RecoveryTriggerEffect` | ICardEffect | `ICardEffect RecoveryTriggerEffect(EffectTiming timing, int amount, CardSource card, Func<bool>? condition, string description)` |
| `ReplaceBottomSecurityWithFaceUpOptionEffect` | IActivatedCardEffect | `IActivatedCardEffect ReplaceBottomSecurityWithFaceUpOptionEffect(CardSource card)` |
| `ReplaceBottomSecurityWithFaceUpOptionMainEffect` | IActivatedCardEffect | `IActivatedCardEffect ReplaceBottomSecurityWithFaceUpOptionMainEffect(CardSource card)` |
| `ReplaceTopSecurityWithFaceUpOptionMainEffect` | IActivatedCardEffect | `IActivatedCardEffect ReplaceTopSecurityWithFaceUpOptionMainEffect(CardSource card)` |
| `RetaliationSelfEffect` | ICardEffect | `ICardEffect RetaliationSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, bool isLinkedEffect = false)` |
| `ReturnToLibraryBottomDigivolutionCardsClass` | IActivatedCardEffect | `IActivatedCardEffect ReturnToLibraryBottomDigivolutionCardsClass(CardSource card, int count)` |
| `RevealDeckTopCardsAndSelect` | IActivatedCardEffect | `IActivatedCardEffect RevealDeckTopCardsAndSelect(CardSource card, int revealCount, IReadOnlyList<HeadlessDCGO.Engine.Headless.Runtime.RevealSelectPass> selectCardConditions, RevealDestination remainingCardsPlace, string description, bool canNoAction = false, bool isOpponentDeck = false, bool mutualConditions = false)` |
| `RevealLibraryClass` | IActivatedCardEffect | `IActivatedCardEffect RevealLibraryClass(CardSource card, int revealCount)` |
| `RushSelfStaticEffect` | ICardEffect | `ICardEffect RushSelfStaticEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `RushStaticEffect` | ICardEffect | `ICardEffect RushStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `SaveEffect` | ICardEffect | `ICardEffect SaveEffect(CardSource card)` |
| `ScapegoatSelfEffect` | ICardEffect | `ICardEffect ScapegoatSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, string? effectName = null, string? effectDescription = null, bool isLinkedEffect = false)` |
| `SelectAndBounceEffect` | ICardEffect | `ICardEffect SelectAndBounceEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description)` |
| `SelectAndBounceEffect` | ICardEffect | `ICardEffect SelectAndBounceEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, string description)` |
| `SelectAndBuffDpEffect` | ICardEffect | `ICardEffect SelectAndBuffDpEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description)` |
| `SelectAndBuffSAttackEffect` | ICardEffect | `ICardEffect SelectAndBuffSAttackEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int changeValue, EffectDuration duration, string description)` |
| `SelectAndDeDigivolveEffect` | ICardEffect | `ICardEffect SelectAndDeDigivolveEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int count, bool canEndNotMax, string description)` |
| `SelectAndDestroyEffect` | ICardEffect | `ICardEffect SelectAndDestroyEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description)` |
| `SelectAndPlayFromZoneEffect` | ICardEffect | `ICardEffect SelectAndPlayFromZoneEffect(CardSource card, ChoiceZone fromZone, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description)` |
| `SelectAndRestrictEffect` | ICardEffect | `ICardEffect SelectAndRestrictEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, EffectDuration duration, bool cannotAttack, bool cannotBlock, string description)` |
| `SelectAndSuspendEffect` | ICardEffect | `ICardEffect SelectAndSuspendEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description)` |
| `SelectAndTrashDigivolutionEffect` | ICardEffect | `ICardEffect SelectAndTrashDigivolutionEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, int trashCount, bool fromBottom, string description)` |
| `SelectAndUnsuspendEffect` | ICardEffect | `ICardEffect SelectAndUnsuspendEffect(CardSource card, Func<HeadlessEntityId, bool> canTarget, int maxCount, bool canEndNotMax, string description)` |
| `SelfDpBuffTriggerEffect` | ICardEffect | `ICardEffect SelfDpBuffTriggerEffect(EffectTiming timing, int changeValue, EffectDuration duration, CardSource card, Func<bool>? condition, string description, Func<CardEffectResolveContext, bool>? triggerGate = null, int? maxCountPerTurn = null, string? hash = null)` |
| `SetMemoryTo3TamerEffect` | ICardEffect | `ICardEffect SetMemoryTo3TamerEffect(CardSource card)` |
| `SimplifiedRevealDeckTopCardsAndSelect` | IActivatedCardEffect | `IActivatedCardEffect SimplifiedRevealDeckTopCardsAndSelect(CardSource card, int revealCount, IReadOnlyList<SimplifiedSelectCardConditionClass> conditions, RevealDestination remainingTo, string description)` |
| `TrainingEffect` | IActivatedCardEffect | `IActivatedCardEffect TrainingEffect(CardSource card)` |
| `TreatAsDigimonStaticEffect` | ICardEffect | `ICardEffect TreatAsDigimonStaticEffect(Func<Permanent, bool>? permanentCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `UnsuspendSelfTriggerEffect` | ICardEffect | `ICardEffect UnsuspendSelfTriggerEffect(EffectTiming timing, CardSource card, string description, int? maxCountPerTurn = null, string? hash = null)` |
| `UseRequirements` | ICardEffect | `ICardEffect UseRequirements(CardSource card, Func<CardSource, bool>? cardCondition = null, bool isInheritedEffect = false, Func<bool>? condition = null)` |
| `VortexCanAttackPlayersStaticEffect` | ICardEffect | `ICardEffect VortexCanAttackPlayersStaticEffect(Func<Permanent, bool>? attackerCondition, bool isInheritedEffect, CardSource card, Func<bool>? condition)` |
| `VortexSelfEffect` | ICardEffect | `ICardEffect VortexSelfEffect(bool isInheritedEffect, CardSource card, Func<bool>? condition, ICardEffect? rootCardEffect = null)` |

## CardEffectCommons 헬퍼 마스터 (이름 → 시그니처)

> 카드 코드가 직접 부르는 `CardEffectCommons.<이름>(...)` 헬퍼 전수(자동생성). 술어(`HasMatchCondition*`, `CanTrigger*`, `Is*`)는 condition/코루틴 안에서 그대로 호출 가능. 명령형 헬퍼(`ChangeDigimonDP`, `AddThisCardToHand` 등)는 코루틴 번역 시 레시피 의도→팩토리 표를 먼저 본다.

| 헬퍼 | 반환 | 시그니처 |
|---|---|---|
| `ActivateMainOfOptionSide` | `Task<int>` | `Task<int> ActivateMainOfOptionSide(CardSource card, CardSource sourceCard, CancellationToken cancellationToken = default)` |
| `AddActivateMainOptionSecurityEffect` | `void` | `void AddActivateMainOptionSecurityEffect(CardSource card, ref List<ICardEffect> cardEffects, string effectName)` |
| `AddEffectToPermanent` | `void` | `void AddEffectToPermanent(Permanent? targetPermanent, EffectDuration effectDuration, CardSource card, ICardEffect cardEffect, EffectTiming timing)` |
| `AddEffectToPlayer` | `void` | `void AddEffectToPlayer(EffectDuration effectDuration, CardSource card, ICardEffect cardEffect, EffectTiming timing)` |
| `AddSelfDeleteEffect` | `void` | `void AddSelfDeleteEffect(Permanent? permanent, string deleteTiming, CardSource sourceCard)` |
| `AddThisCardToHand` | `async Task` | `async Task AddThisCardToHand(CardSource card1, CardSource sourceCard)` |
| `BecomeDigimonThatCantDigivolve` | `bool` | `bool BecomeDigimonThatCantDigivolve(Permanent? targetPermanent, int DP, EffectDuration effectDuration, CardSource sourceCard)` |
| `BlitzProcess` | `bool` | `bool BlitzProcess(CardSource cardSource)` |
| `BouncePeremanentAndProcessAccordingToResult` | `async Task` | `async Task BouncePeremanentAndProcessAccordingToResult(IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard, Func<Task>? successProcess, Func<Task>? failureProcess)` |
| `CanActivateBlitz` | `bool` | `bool CanActivateBlitz(CardSource cardSource)` |
| `CanActivateFortitude` | `bool` | `bool CanActivateFortitude(Headless.Effects.CardEffectResolveContext ctx, CardSource card, bool isInheritedEffect = false)` |
| `CanActivateOnDeletion` | `bool` | `bool CanActivateOnDeletion(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanActivateOnDeletionWithCardColors` | `bool` | `bool CanActivateOnDeletionWithCardColors(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<IReadOnlyList<string>, bool>? cardColorCondition, Func<CardSource, bool>? cardCondition = null)` |
| `CanActivateOnDeletionWithContainingCardName` | `bool` | `bool CanActivateOnDeletionWithContainingCardName(Headless.Effects.CardEffectResolveContext ctx, CardSource card, string name, Func<CardSource, bool>? cardCondition = null)` |
| `CanActivateOnDeletionWithContainingTrait` | `bool` | `bool CanActivateOnDeletionWithContainingTrait(Headless.Effects.CardEffectResolveContext ctx, CardSource card, string name, Func<CardSource, bool>? cardCondition = null)` |
| `CanActivateOnDeletionWithSaveText` | `bool` | `bool CanActivateOnDeletionWithSaveText(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)` |
| `CanActivateSave` | `bool` | `bool CanActivateSave(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? CanSelectPermanentCondition)` |
| `CanActivateSelefOnDeletionWithSaveText` | `bool` | `bool CanActivateSelefOnDeletionWithSaveText(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanActivateSelfOnDeletionWithCardColors` | `bool` | `bool CanActivateSelfOnDeletionWithCardColors(Headless.Effects.CardEffectResolveContext ctx, Func<IReadOnlyList<string>, bool>? cardColorCondition, CardSource card)` |
| `CanActivateSelfOnDeletionWithContainingCardName` | `bool` | `bool CanActivateSelfOnDeletionWithContainingCardName(Headless.Effects.CardEffectResolveContext ctx, string name, CardSource card)` |
| `CanActivateSelfOnDeletionWithContainingTrait` | `bool` | `bool CanActivateSelfOnDeletionWithContainingTrait(Headless.Effects.CardEffectResolveContext ctx, string name, CardSource card)` |
| `CanActivateSuspendCostEffect` | `bool` | `bool CanActivateSuspendCostEffect(CardSource card, bool includeBreeding = false)` |
| `CanDeclareOptionDelayEffect` | `bool` | `bool CanDeclareOptionDelayEffect(CardSource card)` |
| `CanJogressWithHandOrTrash` | `bool` | `bool CanJogressWithHandOrTrash(CardSource source, HeadlessPlayerId owner, bool isWithHandCard, bool isIntoHandCard, Func<CardSource, bool>? targetCardCondition = null)` |
| `CanPlayAsNewPermanent` | `bool` | `bool CanPlayAsNewPermanent(CardSource cardSource, bool payCost, ICardEffect? cardEffect, bool isPlayOption = false, int fixedCost = -1)` |
| `CanTriggerOnAddDigivolutionCard` | `bool` | `bool CanTriggerOnAddDigivolutionCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? cardEffectSourceCondition = null, Func<CardSource, bool>? cardCondition = null)` |
| `CanTriggerOnAttack` | `bool` | `bool CanTriggerOnAttack(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerOnAttackTargetSwitch` | `bool` | `bool CanTriggerOnAttackTargetSwitch(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerOnDeletion` | `bool` | `bool CanTriggerOnDeletion(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerOnEndAttack` | `bool` | `bool CanTriggerOnEndAttack(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerOnFaceUpSecurityIncreases` | `bool` | `bool CanTriggerOnFaceUpSecurityIncreases(Headless.Effects.CardEffectResolveContext ctx, CardSource card, HeadlessPlayerId? player = null, Func<CardSource, bool>? cardCondition = null)` |
| `CanTriggerOnHandAdded` | `bool` | `bool CanTriggerOnHandAdded(Headless.Effects.CardEffectResolveContext ctx, CardSource card, HeadlessPlayerId player, Func<CardSource, bool>? cardEffectSourceCondition = null)` |
| `CanTriggerOnMove` | `bool` | `bool CanTriggerOnMove(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)` |
| `CanTriggerOnPermanentAttack` | `bool` | `bool CanTriggerOnPermanentAttack(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition)` |
| `CanTriggerOnPermanentAttackTargetSwitch` | `bool` | `bool CanTriggerOnPermanentAttackTargetSwitch(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition)` |
| `CanTriggerOnPermanentDeleted` | `bool` | `bool CanTriggerOnPermanentDeleted(CardSource card, CardEffectResolveContext context, Func<HeadlessEntityId, bool> permanentCondition)` |
| `CanTriggerOnPermanentLeave` | `bool` | `bool CanTriggerOnPermanentLeave(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool> permanentCondition)` |
| `CanTriggerOnPermanentPlay` | `bool` | `bool CanTriggerOnPermanentPlay(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, Func<ChoiceZone, bool>? rootCondition = null)` |
| `CanTriggerOnPlay` | `bool` | `bool CanTriggerOnPlay(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<ChoiceZone, bool>? rootCondition = null)` |
| `CanTriggerOnReturnToLibraryBottomDigivolutionCard` | `bool` | `bool CanTriggerOnReturnToLibraryBottomDigivolutionCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)` |
| `CanTriggerOnTrashBySelfDigiBurst` | `bool` | `bool CanTriggerOnTrashBySelfDigiBurst(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerOnTrashDigivolutionCard` | `bool` | `bool CanTriggerOnTrashDigivolutionCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition)` |
| `CanTriggerOnTrashHand` | `bool` | `bool CanTriggerOnTrashHand(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition)` |
| `CanTriggerOnTrashLinkedCard` | `bool` | `bool CanTriggerOnTrashLinkedCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition)` |
| `CanTriggerOnTrashSecurity` | `bool` | `bool CanTriggerOnTrashSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition)` |
| `CanTriggerOnTrashSelfDigivolutionCard` | `bool` | `bool CanTriggerOnTrashSelfDigivolutionCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null)` |
| `CanTriggerOnTrashSelfHand` | `bool` | `bool CanTriggerOnTrashSelfHand(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null)` |
| `CanTriggerOnTrashSelfSecurity` | `bool` | `bool CanTriggerOnTrashSelfSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null)` |
| `CanTriggerOptionMainEffect` | `bool` | `bool CanTriggerOptionMainEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerSecurityEffect` | `bool` | `bool CanTriggerSecurityEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerWhenAddHand` | `bool` | `bool CanTriggerWhenAddHand(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<HeadlessPlayerId, bool>? playerCondition = null, Func<CardSource, bool>? cardEffectSourceCondition = null)` |
| `CanTriggerWhenAddSecurity` | `bool` | `bool CanTriggerWhenAddSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<HeadlessPlayerId, bool>? playerCondition = null)` |
| `CanTriggerWhenCardsReturnToHandFromTrash` | `bool` | `bool CanTriggerWhenCardsReturnToHandFromTrash(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)` |
| `CanTriggerWhenDeleteOpponentDigimonByBattle` | `bool` | `bool CanTriggerWhenDeleteOpponentDigimonByBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? winnerCondition, Func<Permanent, bool>? loserCondition, bool isOnlyWinnerSurvive, Func<Permanent, bool>? winnerRealCondition = null, Func<Permanent, bool>? loserRealCondition = null)` |
| `CanTriggerWhenDigivolving` | `bool` | `bool CanTriggerWhenDigivolving(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<ChoiceZone, bool>? rootCondition = null)` |
| `CanTriggerWhenDiscardLibrary` | `bool` | `bool CanTriggerWhenDiscardLibrary(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)` |
| `CanTriggerWhenLinked` | `bool` | `bool CanTriggerWhenLinked(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? sourceCondition = null)` |
| `CanTriggerWhenLinking` | `bool` | `bool CanTriggerWhenLinking(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)` |
| `CanTriggerWhenLoseSecurity` | `bool` | `bool CanTriggerWhenLoseSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<HeadlessPlayerId, bool>? playerCondition = null)` |
| `CanTriggerWhenOwnerCardsReturnToLibraryFromTrash` | `bool` | `bool CanTriggerWhenOwnerCardsReturnToLibraryFromTrash(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)` |
| `CanTriggerWhenOwnerUseOption` | `bool` | `bool CanTriggerWhenOwnerUseOption(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null, Func<int, bool>? costCondition = null)` |
| `CanTriggerWhenPermanentDigivolving` | `bool` | `bool CanTriggerWhenPermanentDigivolving(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, Func<ChoiceZone, bool>? rootCondition = null)` |
| `CanTriggerWhenPermanentRemoveField` | `bool` | `bool CanTriggerWhenPermanentRemoveField(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition)` |
| `CanTriggerWhenPermanentSuspends` | `bool` | `bool CanTriggerWhenPermanentSuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition)` |
| `CanTriggerWhenPermanentUnsuspends` | `bool` | `bool CanTriggerWhenPermanentUnsuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition)` |
| `CanTriggerWhenPermanentWouldDigivolve` | `bool` | `bool CanTriggerWhenPermanentWouldDigivolve(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? cardCondition = null)` |
| `CanTriggerWhenPermanentWouldDigivolveOfCard` | `bool` | `bool CanTriggerWhenPermanentWouldDigivolveOfCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)` |
| `CanTriggerWhenPermanentWouldPlay` | `bool` | `bool CanTriggerWhenPermanentWouldPlay(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)` |
| `CanTriggerWhenRemoveField` | `bool` | `bool CanTriggerWhenRemoveField(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerWhenSelfDiscardLibrary` | `bool` | `bool CanTriggerWhenSelfDiscardLibrary(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerWhenSelfPermanentSuspends` | `bool` | `bool CanTriggerWhenSelfPermanentSuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerWhenSelfPermanentUnsuspends` | `bool` | `bool CanTriggerWhenSelfPermanentUnsuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerWhenTopCardTrashed` | `bool` | `bool CanTriggerWhenTopCardTrashed(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool> cardCondition)` |
| `CanTriggerWhenUseDigiBurst` | `bool` | `bool CanTriggerWhenUseDigiBurst(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerWhenUseOption` | `bool` | `bool CanTriggerWhenUseOption(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null, Func<int, bool>? costCondition = null)` |
| `CanTriggerWhenWinBattle` | `bool` | `bool CanTriggerWhenWinBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `CanTriggerWhenWouldLink` | `bool` | `bool CanTriggerWhenWouldLink(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null, Func<Permanent, bool>? permanentCondition = null, Func<ChoiceZone, bool>? rootCondition = null, Func<CardSource, bool>? cardEffectSourceCondition = null)` |
| `CanUnsuspend` | `bool` | `bool CanUnsuspend(Permanent? permanent)` |
| `CanUseIgnoreBattle` | `bool` | `bool CanUseIgnoreBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `ChangeBaseDigimonDP` | `bool` | `bool ChangeBaseDigimonDP(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` |
| `ChangeDigimonDP` | `bool` | `bool ChangeDigimonDP(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` |
| `ChangeDigimonDPPlayerEffect` | `bool` | `bool ChangeDigimonDPPlayerEffect(Func<Permanent, bool>? permanentCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` |
| `ChangeDigimonSAttack` | `bool` | `bool ChangeDigimonSAttack(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard, bool activateAnimation = true, string? hashstring = null)` |
| `ChangeDigimonSAttackPlayerEffect` | `bool` | `bool ChangeDigimonSAttackPlayerEffect(Func<Permanent, bool>? permanentCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` |
| `ChangePlayCostPlayerEffect` | `bool` | `bool ChangePlayCostPlayerEffect(Func<Permanent, bool>? permanentCondition, int changeValue, bool setFixedCost, EffectDuration effectDuration, CardSource sourceCard)` |
| `ChangeSecurityDigimonCardDPPlayerEffect` | `bool` | `bool ChangeSecurityDigimonCardDPPlayerEffect(Func<CardSource, bool>? cardCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)` |
| `CurrentBattleOpponent` | `HeadlessEntityId` | `HeadlessEntityId CurrentBattleOpponent(CardSource card)` |
| `CurrentDp` | `int` | `int CurrentDp(CardSource card, HeadlessEntityId id)` |
| `DeckBouncePeremanentAndProcessAccordingToResult` | `async Task` | `async Task DeckBouncePeremanentAndProcessAccordingToResult(IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard, Func<Task>? successProcess, Func<Task>? failureProcess)` |
| `DeletePeremanentAndProcessAccordingToResult` | `async Task` | `async Task DeletePeremanentAndProcessAccordingToResult(IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard, Func<IReadOnlyList<Permanent>, Task>? successProcess, Func<Task>? failureProcess, CancellationToken cancellationToken = default)` |
| `DigivolveIntoExcecutingAreaCard` | `async Task` | `async Task DigivolveIntoExcecutingAreaCard(Permanent? targetPermanent, Func<CardSource, bool>? cardCondition, bool payCost, (int reduceCost, Func<CardSource, bool>? reduceCostCardCondition)? reduceCostTuple, (int fixedCost, Func<CardSource, bool>? fixedCostCardCondition)? fixedCostTuple, int ignoreDigivolutionRequirementFixedCost, CardSource sourceCard, Func<Task>? successProcess, bool ignoreSelection = false, bool ignoreRequirements = false, CancellationToken cancellationToken = default)` |
| `DigivolveIntoHandOrTrashCard` | `Task` | `Task DigivolveIntoHandOrTrashCard(Permanent? targetPermanent, Func<CardSource, bool>? cardCondition, bool payCost, (int reduceCost, Func<CardSource, bool>? reduceCostCardCondition)? reduceCostTuple, (int fixedCost, Func<CardSource, bool>? fixedCostCardCondition)? fixedCostTuple, int ignoreDigivolutionRequirementFixedCost, bool isHand, CardSource sourceCard, Func<Task>? successProcess, bool ignoreSelection = false, bool ignoreRequirements = false, Func<Task>? failedProcess = null, bool isOptional = true, CancellationToken cancellationToken = default)` |
| `DNADigivolvePermanentsIntoHandOrTrashCard` | `async Task` | `async Task DNADigivolvePermanentsIntoHandOrTrashCard(Func<CardSource, bool>? canSelectDNACardCondition, bool payCost, bool isHand, CardSource sourceCard, Func<Permanent, bool>[]? permanentConditions = null, Func<CardSource, Task>? successProcess = null, bool ignoreSelection = false, Func<Task>? failedProcess = null, bool isOptional = true, CancellationToken cancellationToken = default)` |
| `DNADigivolveWithHandOrTrashCardIntoHandOrTrash` | `Task` | `Task DNADigivolveWithHandOrTrashCardIntoHandOrTrash(CardSource sourceCard)` |
| `DrawAndDiscardCards` | `async Task` | `async Task DrawAndDiscardCards((HeadlessPlayerId drawPlayer, HeadlessPlayerId trashPlayer) player, int drawAmount, int trashAmount, CardSource sourceCard, Func<CardSource, bool>? canTrashTargetCondition = null, bool canNoSelect = false, bool canEndNotMax = false, CancellationToken cancellationToken = default)` |
| `EnforceLocationCheck` | `void` | `void EnforceLocationCheck()` |
| `FortitudeProcess` | `Task` | `Task FortitudeProcess(CardSource card, CardSource sourceCard)` |
| `GainAlliance` | `bool` | `bool GainAlliance(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainAlliancePlayerEffect` | `bool` | `bool GainAlliancePlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainBarrier` | `bool` | `bool GainBarrier(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainBlitz` | `bool` | `bool GainBlitz(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard, bool isWhenDigivolving = false)` |
| `GainBlocker` | `bool` | `bool GainBlocker(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainBlockerPlayerEffect` | `bool` | `bool GainBlockerPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainCanNotAttack` | `bool` | `bool GainCanNotAttack(Permanent? targetPermanent, Func<Permanent, bool>? defenderCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't attack")` |
| `GainCanNotAttackPlayerEffect` | `bool` | `bool GainCanNotAttackPlayerEffect(Func<Permanent, bool>? attackerCondition, Func<Permanent, bool>? defenderCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't attack")` |
| `GainCanNotBeAttacked` | `bool` | `bool GainCanNotBeAttacked(Permanent? targetPermanent, Func<Permanent, bool>? attackerCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be attacked")` |
| `GainCanNotBeBlocked` | `bool` | `bool GainCanNotBeBlocked(Permanent? targetPermanent, Func<Permanent, bool>? defenderCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be blocked")` |
| `GainCanNotBeDeletedByBattle` | `bool` | `bool GainCanNotBeDeletedByBattle(Permanent targetPermanent, Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName)` |
| `GainCanNotBeDeletedByEffect` | `bool` | `bool GainCanNotBeDeletedByEffect(Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be deleted by effects")` |
| `GainCanNotBeDeletedPlayerEffect` | `bool` | `bool GainCanNotBeDeletedPlayerEffect(Func<Permanent, bool>? permanentCondition, Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be deleted in battle")` |
| `GainCanNotBlock` | `bool` | `bool GainCanNotBlock(Permanent? targetPermanent, Func<Permanent, bool>? attackerCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't block")` |
| `GainCanNotBlockPlayerEffect` | `bool` | `bool GainCanNotBlockPlayerEffect(Func<Permanent, bool>? attackerCondition, Func<Permanent, bool>? defenderCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't block")` |
| `GainCanNotReturnToDeck` | `bool` | `bool GainCanNotReturnToDeck(Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to deck")` |
| `GainCanNotReturnToDeckPlayerEffect` | `bool` | `bool GainCanNotReturnToDeckPlayerEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to deck")` |
| `GainCanNotReturnToHand` | `bool` | `bool GainCanNotReturnToHand(Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to hand")` |
| `GainCanNotReturnToHandPlayerEffect` | `bool` | `bool GainCanNotReturnToHandPlayerEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to hand")` |
| `GainCanNotSuspend` | `bool` | `bool GainCanNotSuspend(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard, Func<bool>? condition = null, string effectName = "Can't suspend")` |
| `GainCanNotSuspendPlayerEffect` | `bool` | `bool GainCanNotSuspendPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard, bool isOnlyActivePhase = false, string effectName = "Can't suspend")` |
| `GainCanNotUnsuspend` | `bool` | `bool GainCanNotUnsuspend(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard, Func<bool>? condition = null, string effectName = "Can't unsuspend")` |
| `GainCanNotUnsuspendPlayerEffect` | `bool` | `bool GainCanNotUnsuspendPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard, bool isOnlyActivePhase = false, string effectName = "Can't unsuspend")` |
| `GainCantSuspendUntilOpponentTurnEnd` | `bool` | `bool GainCantSuspendUntilOpponentTurnEnd(Permanent? targetPermanent, CardSource sourceCard)` |
| `GainCantUnsuspendNextActivePhase` | `bool` | `bool GainCantUnsuspendNextActivePhase(Permanent? targetPermanent, CardSource sourceCard)` |
| `GainCantUnsuspendUntilOpponentTurnEnd` | `bool` | `bool GainCantUnsuspendUntilOpponentTurnEnd(Permanent? targetPermanent, CardSource sourceCard)` |
| `GainCollision` | `bool` | `bool GainCollision(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainEvade` | `bool` | `bool GainEvade(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainExecute` | `bool` | `bool GainExecute(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainFortitude` | `bool` | `bool GainFortitude(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainIceclad` | `bool` | `bool GainIceclad(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainIcecladPlayerEffect` | `bool` | `bool GainIcecladPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainImmuneFromDPMinus` | `bool` | `bool GainImmuneFromDPMinus(Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Immune from DP minus")` |
| `GainImmuneFromDPMinusPlayerEffect` | `bool` | `bool GainImmuneFromDPMinusPlayerEffect(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, EffectDuration effectDuration, CardSource sourceCard, string effectName = "Immune from DP minus")` |
| `GainJamming` | `bool` | `bool GainJamming(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainPierce` | `bool` | `bool GainPierce(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainRaid` | `bool` | `bool GainRaid(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainReboot` | `bool` | `bool GainReboot(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainRetaliation` | `bool` | `bool GainRetaliation(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainRush` | `bool` | `bool GainRush(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainRushPlayerEffect` | `bool` | `bool GainRushPlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard)` |
| `GainVortex` | `bool` | `bool GainVortex(Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard)` |
| `GetAttackerFromHashtable` | `Permanent?` | `Permanent? GetAttackerFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetCardEffectFromHashtable` | `CardSource?` | `CardSource? GetCardEffectFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetCardFromHashtable` | `CardSource?` | `CardSource? GetCardFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetCardSourcesFromHashtable` | `List<CardSource>` | `List<CardSource> GetCardSourcesFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetDigivolutionRootsFromEnterFieldHashtable` | `List<CardSource>` | `List<CardSource> GetDigivolutionRootsFromEnterFieldHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)` |
| `GetDiscardedCardsFromHashtable` | `List<CardSource>` | `List<CardSource> GetDiscardedCardsFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetEvoRootTopsFromEnterFieldHashtable` | `List<CardSource>` | `List<CardSource> GetEvoRootTopsFromEnterFieldHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)` |
| `GetFaceDownFromHashtable` | `bool` | `bool GetFaceDownFromHashtable(Headless.Effects.CardEffectResolveContext ctx)` |
| `GetMovedPermanentFromHashtable` | `Permanent?` | `Permanent? GetMovedPermanentFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetNonMaxCostPermanents` | `List<Permanent>` | `List<Permanent> GetNonMaxCostPermanents(CardSource card, HeadlessPlayerId owner, bool digimonOnly = true)` |
| `GetPermanentFromHashtable` | `Permanent?` | `Permanent? GetPermanentFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetPermanentsFromHashtable` | `List<Permanent>` | `List<Permanent> GetPermanentsFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetPlayedPermanentsFromEnterFieldHashtable` | `List<Permanent>` | `List<Permanent> GetPlayedPermanentsFromEnterFieldHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<ChoiceZone, bool>? rootCondition = null)` |
| `GetTopCardFromEffectHashtable` | `CardSource?` | `CardSource? GetTopCardFromEffectHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetTopCardFromOneHashtable` | `CardSource?` | `CardSource? GetTopCardFromOneHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `GetUniqueColourCountOnOpponentsBattleArea` | `int` | `int GetUniqueColourCountOnOpponentsBattleArea(CardSource card, Func<Permanent, bool> canGetCardColour)` |
| `GetUniqueColourCountOnOwnerBattleArea` | `int` | `int GetUniqueColourCountOnOwnerBattleArea(CardSource card, Func<Permanent, bool> canGetCardColour)` |
| `HasBlocker` | `bool` | `bool HasBlocker(CardSource card)` |
| `HasJamming` | `bool` | `bool HasJamming(CardSource card)` |
| `HasKeyword` | `bool` | `bool HasKeyword(CardSource card, string keyword)` |
| `HasMatchConditionOpponentsCardInTrash` | `bool` | `bool HasMatchConditionOpponentsCardInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)` |
| `HasMatchConditionOpponentsPermanent` | `bool` | `bool HasMatchConditionOpponentsPermanent(CardSource card, Func<HeadlessEntityId, bool> condition)` |
| `HasMatchConditionOwnersBreedingPermanent` | `bool` | `bool HasMatchConditionOwnersBreedingPermanent(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)` |
| `HasMatchConditionOwnersCardInTrash` | `bool` | `bool HasMatchConditionOwnersCardInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)` |
| `HasMatchConditionOwnersHand` | `bool` | `bool HasMatchConditionOwnersHand(CardSource card, Func<CardSource, bool> CanSelectCardCondition)` |
| `HasMatchConditionOwnersPermanent` | `bool` | `bool HasMatchConditionOwnersPermanent(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)` |
| `HasMatchConditionOwnersSecurity` | `bool` | `bool HasMatchConditionOwnersSecurity(CardSource card, Func<CardSource, bool> CanSelectCardCondition)` |
| `HasMatchConditionPermanent` | `bool` | `bool HasMatchConditionPermanent(CardSource card, Func<HeadlessEntityId, bool> condition, bool isContainBreedingArea = false)` |
| `HasMatchConditionPermanent` | `bool` | `bool HasMatchConditionPermanent(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition, bool isContainBreedingArea = false)` |
| `HasMatchConditionPermanentDigivolutionCards` | `bool` | `bool HasMatchConditionPermanentDigivolutionCards(CardSource card, Func<CardSource, bool> CanSelectPermanentCondition)` |
| `HasNoDigivolutionCards` | `bool` | `bool HasNoDigivolutionCards(CardSource card, HeadlessEntityId id)` |
| `HasPierce` | `bool` | `bool HasPierce(CardSource card)` |
| `HasTrashableDigivolutionCards` | `bool` | `bool HasTrashableDigivolutionCards(CardSource card, HeadlessEntityId hostId)` |
| `IsAlliance` | `bool` | `bool IsAlliance(Headless.Effects.CardEffectResolveContext ctx)` |
| `IsBattleAreaDigimon` | `bool` | `bool IsBattleAreaDigimon(CardSource card, HeadlessEntityId id)` |
| `IsBlock` | `bool` | `bool IsBlock(Headless.Effects.CardEffectResolveContext ctx)` |
| `IsByBattle` | `bool` | `bool IsByBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `IsByEffect` | `bool` | `bool IsByEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null)` |
| `IsDigivolvedByTheEffect` | `bool` | `bool IsDigivolvedByTheEffect(Permanent? permanent, CardSource cardSource, CardSource effectSourceCard)` |
| `IsDigivolvedFromSameLevelFromEnterFieldHashtable` | `bool` | `bool IsDigivolvedFromSameLevelFromEnterFieldHashtable(Headless.Effects.CardEffectResolveContext ctx, Permanent? permanent)` |
| `IsDijiXros` | `bool` | `bool IsDijiXros(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<int, bool>? digixrosCountCondition)` |
| `IsDPZeroDelete` | `bool` | `bool IsDPZeroDelete(CardSource card, CardEffectResolveContext context)` |
| `IsExistDigivolutionCards` | `bool` | `bool IsExistDigivolutionCards(CardSource card)` |
| `IsExistInAnyTrash` | `bool` | `bool IsExistInAnyTrash(CardSource card)` |
| `IsExistInSecurity` | `bool` | `bool IsExistInSecurity(CardSource card, bool isFlipped = false)` |
| `IsExistLinked` | `bool` | `bool IsExistLinked(CardSource card)` |
| `IsExistOnBattleArea` | `bool` | `bool IsExistOnBattleArea(CardSource card)` |
| `IsExistOnBattleAreaActivate` | `bool` | `bool IsExistOnBattleAreaActivate(CardSource card, ICardEffect? cardEffect = null)` |
| `IsExistOnBattleAreaDigimon` | `bool` | `bool IsExistOnBattleAreaDigimon(CardSource card)` |
| `IsExistOnBattleAreaTrigger` | `bool` | `bool IsExistOnBattleAreaTrigger(CardSource card, ICardEffect? cardEffect = null)` |
| `IsExistOnBreedingArea` | `bool` | `bool IsExistOnBreedingArea(CardSource card)` |
| `IsExistOnBreedingAreaDigimon` | `bool` | `bool IsExistOnBreedingAreaDigimon(CardSource card)` |
| `IsExistOnExecutingArea` | `bool` | `bool IsExistOnExecutingArea(CardSource card)` |
| `IsExistOnField` | `bool` | `bool IsExistOnField(CardSource card)` |
| `IsExistOnHand` | `bool` | `bool IsExistOnHand(CardSource card)` |
| `IsExistOnTrash` | `bool` | `bool IsExistOnTrash(CardSource card)` |
| `IsFromDigimon` | `bool` | `bool IsFromDigimon(Headless.Effects.CardEffectResolveContext ctx)` |
| `IsFromDigimonDigivolutionCards` | `bool` | `bool IsFromDigimonDigivolutionCards(Headless.Effects.CardEffectResolveContext ctx)` |
| `IsFromSameDigimon` | `bool` | `bool IsFromSameDigimon(Headless.Effects.CardEffectResolveContext ctx)` |
| `IsJogress` | `bool` | `bool IsJogress(Headless.Effects.CardEffectResolveContext ctx)` |
| `IsLeavingForDigiXros` | `bool` | `bool IsLeavingForDigiXros(Headless.Effects.CardEffectResolveContext ctx)` |
| `IsMaxCost` | `bool` | `bool IsMaxCost(Permanent? permanent, HeadlessPlayerId owner, bool IsDigimonOnly)` |
| `IsMaxDP` | `bool` | `bool IsMaxDP(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? permanentCondition = null)` |
| `IsMaxLevel` | `bool` | `bool IsMaxLevel(Permanent? permanent, HeadlessPlayerId owner)` |
| `IsMinCost` | `bool` | `bool IsMinCost(Permanent? permanent, HeadlessPlayerId owner, bool IsDigimonOnly, Func<Permanent, bool>? condition = null)` |
| `IsMinDigivolutionCards` | `bool` | `bool IsMinDigivolutionCards(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? condition = null)` |
| `IsMinDP` | `bool` | `bool IsMinDP(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? condition = null)` |
| `IsMinLevel` | `bool` | `bool IsMinLevel(Permanent? permanent, HeadlessPlayerId owner)` |
| `IsMinLevelBoard` | `bool` | `bool IsMinLevelBoard(Permanent? permanent)` |
| `IsOpponentBattleAreaDigimon` | `bool` | `bool IsOpponentBattleAreaDigimon(CardSource card, HeadlessEntityId id)` |
| `IsOpponentEffect` | `bool` | `bool IsOpponentEffect(CardSource? effectSourceCard, CardSource card)` |
| `IsOpponentOwnedDigimon` | `bool` | `bool IsOpponentOwnedDigimon(CardSource card, HeadlessEntityId id)` |
| `IsOpponentPermanent` | `bool` | `bool IsOpponentPermanent(Permanent? permanent, CardSource card)` |
| `IsOpponentTurn` | `bool` | `bool IsOpponentTurn(CardSource card)` |
| `IsOwnerBattleAreaDigimon` | `bool` | `bool IsOwnerBattleAreaDigimon(CardSource card, HeadlessEntityId id)` |
| `IsOwnerEffect` | `bool` | `bool IsOwnerEffect(CardSource? effectSourceCard, CardSource card)` |
| `IsOwnerPermanent` | `bool` | `bool IsOwnerPermanent(Permanent? permanent, CardSource card)` |
| `IsOwnerTurn` | `bool` | `bool IsOwnerTurn(CardSource card)` |
| `IsPermanentExistsOnBattleArea` | `bool` | `bool IsPermanentExistsOnBattleArea(Permanent? permanent)` |
| `IsPermanentExistsOnBattleAreaDigimon` | `bool` | `bool IsPermanentExistsOnBattleAreaDigimon(Permanent? permanent)` |
| `IsPermanentExistsOnBattleAreaTamer` | `bool` | `bool IsPermanentExistsOnBattleAreaTamer(Permanent? permanent)` |
| `IsPermanentExistsOnBreedingArea` | `bool` | `bool IsPermanentExistsOnBreedingArea(Permanent? permanent)` |
| `IsPermanentExistsOnField` | `bool` | `bool IsPermanentExistsOnField(Permanent? permanent)` |
| `IsPermanentExistsOnOpponentBattleArea` | `bool` | `bool IsPermanentExistsOnOpponentBattleArea(Permanent? permanent, CardSource card)` |
| `IsPermanentExistsOnOpponentBattleAreaDigimon` | `bool` | `bool IsPermanentExistsOnOpponentBattleAreaDigimon(Permanent? permanent, CardSource card)` |
| `IsPermanentExistsOnOpponentBattleAreaTamer` | `bool` | `bool IsPermanentExistsOnOpponentBattleAreaTamer(Permanent? permanent, CardSource card)` |
| `IsPermanentExistsOnOwnerBattleArea` | `bool` | `bool IsPermanentExistsOnOwnerBattleArea(Permanent? permanent, CardSource card)` |
| `IsPermanentExistsOnOwnerBattleAreaDigimon` | `bool` | `bool IsPermanentExistsOnOwnerBattleAreaDigimon(Permanent? permanent, CardSource card)` |
| `IsPermanentExistsOnOwnerBattleAreaTamer` | `bool` | `bool IsPermanentExistsOnOwnerBattleAreaTamer(Permanent? permanent, CardSource card)` |
| `IsPermanentExistsOnOwnerBreedingArea` | `bool` | `bool IsPermanentExistsOnOwnerBreedingArea(Permanent? permanent, CardSource card)` |
| `IsSuspended` | `bool` | `bool IsSuspended(CardSource card, HeadlessEntityId id)` |
| `IsTopCardInTrashOnDeletion` | `bool` | `bool IsTopCardInTrashOnDeletion(Headless.Effects.CardEffectResolveContext ctx, CardSource card)` |
| `LevelOf` | `int` | `int LevelOf(CardSource card, HeadlessEntityId id)` |
| `MatchConditionOpponentsCardCountInTrash` | `int` | `int MatchConditionOpponentsCardCountInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)` |
| `MatchConditionOpponentsPermanentCount` | `int` | `int MatchConditionOpponentsPermanentCount(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)` |
| `MatchConditionOwnersCardCountInHand` | `int` | `int MatchConditionOwnersCardCountInHand(CardSource card, Func<CardSource, bool> CanSelectCardCondition)` |
| `MatchConditionOwnersCardCountInTrash` | `int` | `int MatchConditionOwnersCardCountInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)` |
| `MatchConditionOwnersPermanentCount` | `int` | `int MatchConditionOwnersPermanentCount(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)` |
| `MatchConditionPermanentCount` | `int` | `int MatchConditionPermanentCount(CardSource card, Func<HeadlessEntityId, bool> condition, bool isContainBreedingArea = false)` |
| `MaxDpDeleteThreshold` | `int` | `int MaxDpDeleteThreshold(CardSource card, int baseThreshold)` |
| `MemoryForPlayer` | `int` | `int MemoryForPlayer(CardSource card)` |
| `OpponentOf` | `HeadlessPlayerId` | `HeadlessPlayerId OpponentOf(CardSource card)` |
| `PlaceDelayOptionCards` | `async Task<bool>` | `async Task<bool> PlaceDelayOptionCards(CardSource card, ICardEffect? cardEffect = null, ChoiceZone root = ChoiceZone.Execution)` |
| `PlacePermanentInSecurityAndProcessAccordingToResult` | `async Task` | `async Task PlacePermanentInSecurityAndProcessAccordingToResult(Permanent? targetPermanent, bool toTop, CardSource sourceCard, Func<CardSource, Task>? successProcess, Func<Task>? failureProcess)` |
| `PlayAmonToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayAmonToken(CardSource sourceCard)` |
| `PlayAthoRenePorToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayAthoRenePorToken(CardSource sourceCard)` |
| `PlayDiaboromonToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayDiaboromonToken(CardSource sourceCard, int quantity = 1)` |
| `PlayFamiliarToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayFamiliarToken(CardSource sourceCard, int quantity = 1)` |
| `PlayFujitsumonToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayFujitsumonToken(CardSource sourceCard, bool isOwnerPermanent)` |
| `PlayGyuukimonToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayGyuukimonToken(CardSource sourceCard)` |
| `PlayHinukamuyToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayHinukamuyToken(CardSource sourceCard)` |
| `PlayKoHagurumonToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayKoHagurumonToken(CardSource sourceCard)` |
| `PlayPermanentCards` | `async Task` | `async Task PlayPermanentCards(IReadOnlyList<CardSource> cardSources, CardSource sourceCard, bool payCost, bool isTapped, ChoiceZone root, bool activateETB, bool isBreedingArea = false, int fixedCost = -1)` |
| `PlayPetrificationToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayPetrificationToken(CardSource sourceCard, int quantity = 1)` |
| `PlayPipeFox` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayPipeFox(CardSource sourceCard)` |
| `PlayRapidmonToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayRapidmonToken(CardSource sourceCard)` |
| `PlaySelfDeleteFamiliarToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlaySelfDeleteFamiliarToken(CardSource sourceCard, int quantity = 1)` |
| `PlayTaomonToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayTaomonToken(CardSource sourceCard)` |
| `PlayToken` | `async Task<IReadOnlyList<HeadlessEntityId>>` | `async Task<IReadOnlyList<HeadlessEntityId>> PlayToken(TokenSpec tokenData, CardSource sourceCard, bool isOwnerPermanent, bool isTapped, int quantity = 1)` |
| `PlayUkaNoMitama` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayUkaNoMitama(CardSource sourceCard)` |
| `PlayUmonToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayUmonToken(CardSource sourceCard)` |
| `PlayVoleeZerdrucken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayVoleeZerdrucken(CardSource sourceCard)` |
| `PlayWarGrowlmonToken` | `Task<IReadOnlyList<HeadlessEntityId>>` | `Task<IReadOnlyList<HeadlessEntityId>> PlayWarGrowlmonToken(CardSource sourceCard)` |
| `ReturnRevealedCardsToLibraryBottom` | `async Task` | `async Task ReturnRevealedCardsToLibraryBottom(IReadOnlyList<CardSource> remainingCards, CardSource sourceCard, CancellationToken cancellationToken = default)` |
| `SaveProcess` | `async Task` | `async Task SaveProcess(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? CanSelectPermanentCondition, CancellationToken cancellationToken = default)` |
| `SecurityCount` | `int` | `int SecurityCount(CardSource card)` |
| `SelectTrashDigivolutionCards` | `async Task` | `async Task SelectTrashDigivolutionCards(Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardCondition, int maxCount, bool canNoTrash, bool isFromOnly1Permanent, CardSource sourceCard, string selectString = "Digimon", Func<Permanent, IReadOnlyList<CardSource>, Task>? afterSelectionCoroutine = null, bool canEndNotMax = false, CancellationToken cancellationToken = default)` |
| `StartOfMainAttack` | `void` | `void StartOfMainAttack(Permanent? targetPermanent, CardSource sourceCard)` |
| `SuspendPeremanentAndProcessAccordingToResult` | `async Task` | `async Task SuspendPeremanentAndProcessAccordingToResult(IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard, Func<IReadOnlyList<Permanent>, Task>? successProcess, Func<Task>? failureProcess)` |
| `TopCardHasLevel` | `bool` | `bool TopCardHasLevel(CardSource card, HeadlessEntityId id)` |
| `TrashableDigivolutionCount` | `int` | `int TrashableDigivolutionCount(CardSource card, HeadlessEntityId hostId)` |
| `TrashDigivolutionCardsAndProcessAccordingToResult` | `async Task` | `async Task TrashDigivolutionCardsAndProcessAccordingToResult(Permanent? targetPermanent, int trashCount, bool isFromTop, CardSource sourceCard, Func<int, Task>? successProcess, Func<Task>? failureProcess)` |
| `TrashDigivolutionCardsFromTopOrBottom` | `Task<int>` | `Task<int> TrashDigivolutionCardsFromTopOrBottom(Permanent? targetPermanent, int trashCount, bool isFromTop, CardSource sourceCard)` |
| `TrashHandAndProcessAccordingToResult` | `async Task` | `async Task TrashHandAndProcessAccordingToResult(CardSource? handCard, CardSource sourceCard, Func<Task>? successProcess, Func<Task>? failureProcess)` |
| `TrashLinkCardsAndProcessAccordingToResult` | `async Task` | `async Task TrashLinkCardsAndProcessAccordingToResult(Permanent? hostPermanent, IReadOnlyList<HeadlessEntityId> linkCardIds, CardSource sourceCard, Func<int, Task>? successProcess, Func<Task>? failureProcess)` |
| `TrashSecurityAndProcessAccordingToResult` | `async Task` | `async Task TrashSecurityAndProcessAccordingToResult(HeadlessPlayerId player, int trashAmount, bool fromTop, CardSource sourceCard, Func<int, Task>? successProcess, Func<Task>? failureProcess)` |
