# BT3 STOP Gap Verification & Missing-Primitive Design

**Role:** Opus verifier over Sonnet-ported BT3 `// STOP` cards.
**Goal:** Separate *false* STOPs (an equivalent primitive already exists, Sonnet missed it)
from *genuine* primitive gaps, then design (design only — **no code written**) the genuine
missing primitives.

Source tree read for this audit (read-only): `/home/hg/git/headlessDCGO/.claude/worktrees/agent-a0e43dc872850b93c`
- Framework: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs` (CPF)
- Uniform bodies: `src/HeadlessDCGO.Engine/Assets/Scripts/Script/CardEffectCommons/ActivatedEffect.cs`
- Resolver: `.../CardEffectCommons/ActivatedEffectResolver.cs`
- Trigger bridge: `src/HeadlessDCGO.Engine/Headless/Runtime/GameFlowProcessor.cs`
- Reveal runtime: `src/HeadlessDCGO.Engine/Headless/Runtime/RevealAndSelect.cs`

34 STOP files verified (Black 7, Blue 4, Green 4, Purple 8, Red 4, White 1, Yellow 6).

---

## 1. Headline result

- **FALSE STOP: 9 cards** (13 timing-branches) — an equivalent primitive already exists; Sonnet
  looked at the wrong symbol / missed the uniform-`ActivatedEffect` composition path.
- **GENUINE gaps: ~25 card-branches → 17 primitive families** (after dedup).
  - 4 of these families are **shallow wiring** (the logic already exists verbatim; only a
    dispatch-set entry or a thin factory is missing) — flagged ⚡.
  - 1 family is **deep engine work** (Digisorption keyword) — flagged 🧱.

The single most common Sonnet error: **stopping at a leaf factory** (`DrawCardsEffect`,
`ChangeSAttackStaticEffect`, a standalone select factory) **without checking the uniform
`ActivatedEffect(body:, maxCountPerTurn:)` + `SelectBody.onEachSelected` +
`GrantPlayerScopeRestrictionBody(action)` composition layer**, which is exactly the layer built to
carry these shapes. Same miss class as the BT2 `MatchConditionOwnersCardCountInTrash` precedent.

---

## 2. FALSE STOPs (do NOT need a new primitive)

| Card | Branch | Correct headless port | Evidence / precedent |
|---|---|---|---|
| **BT3_003** | [When Attacking][OPT] Draw 1 if sec ≤3 | `new ActivatedEffect(card, EffectTiming.OnAllyAttack, canUse: CanTriggerOnAttack, canActivate: () => SecurityCount ≤ 3, body: new DrawBody(1), maxCountPerTurn: 1, isOptional:false)` | Resolver enforces cap at `ActivatedEffectResolver.cs:355` (`uniform.MaxCountPerTurn … OnceFlags.TryActivate`). Live pattern: **BT1_003, BT3_002** (`new DrawBody` + `maxCountPerTurn: 1`). Sonnet only checked the standalone `DrawCardsEffect` factory (which can't carry the cap) and missed `ActivatedEffect`+`DrawBody`. |
| **BT3_014** | [When Digivolving] change original DP of 1 opp Lv≤4 → 1000 (turn) | `SelectBody(mode: Custom, canTarget: opp Lv≤4, maxCount:1) { onEachSelected: id => ChangeBaseDigimonDP(new Permanent(card.Context,id,opp), delta→1000, UntilEachTurnEnd, card) }` | **Verbatim identical to BT1_105** (`SelectBody(Custom, opponent-scope) → ChangeBaseDigimonDP`). Color block already ported via `ChangeCardColorClass` in the same file. |
| **BT3_015** | [When Digivolving] may return 1 Lv7 Virus from trash→hand | `SelectAndAddToHandFromZoneEffect(card, ChoiceZone.Trash, canTarget: Lv7 && trait Virus, maxCount:1, canEndNotMax:true)` under `timing==WhenDigivolving` | `ActivatedSelectFromZoneEffect.BuildRequest`: `canEndNotMax:true` ⇒ `minCount:0, canSkip:true` (optional "may"). Zone Trash/Library/Security all supported. Live at WhenDigivolving: ST4_10, BT1_011. |
| **BT3_071** | [When Digivolving] may return 1 Lv7 Virus from trash→hand | Same as BT3_015 (AS-IS is the same coroutine byte-for-byte). | Same. |
| **BT3_099** | [Main] neither player's Digimon deletable in battle (turn) / [Security] add self to hand | [Main] `GrantPlayerScopeRestrictionBody(c => GainCanNotBeDeletedPlayerEffect(permanentCondition: IsPermanentExistsOnBattleArea, canNotBeDestroyedByBattleCondition: attacker/defender-in-battle, UntilOwnerTurnEnd))`; [Security] `AddThisCardToHandEffect`. AS-IS literally calls `GainCanNotBeDeletedPlayerEffect`. | Helper exists (CPF:8787); wiring pattern = **BT1_100** (`GrantPlayerScopeRestrictionBody(c => GainCanNot…PlayerEffect(…))`). Both-player scope handled (predicate = all battle-area). |
| **BT3_101** | [Main]&[Security] 1 opp Digimon: −3000 DP **and** SA −1 (one select) | `SelectBody(Custom, opp Digimon, maxCount:1) { onEachSelected: id => { ChangeDigimonDP(perm,−3000,UntilEachTurnEnd,card); ChangeDigimonSAttack(perm,−1,UntilEachTurnEnd,card); } }` | `onEachSelected` is an `Action` — it may apply **multiple** mutations to one pick. `ChangeDigimonDP` (CPF:7428) + `ChangeDigimonSAttack` (CPF:7433) both exist. Sonnet's "no combining primitive" is wrong. |
| **BT3_105** | [Main] grant 1 own Digimon Reboot + no-DP-minus + no-return / [Security] opp can't attack players (turn) | [Main] `SelectBody(Custom, own Digimon, maxCount:1){ onEachSelected: id => { GainReboot; GainImmuneFromDPMinus; GainCanNotReturnToHand; GainCanNotReturnToDeck — all UntilOpponentTurnEnd } }`; [Security] `GrantPlayerScopeRestrictionBody(c => GainCanNotAttackPlayerEffect(attackerCondition: opp battle Digimon, defenderCondition: player-only, UntilEachTurnEnd))`. | All four Gain* helpers exist; multi-grant in one `onEachSelected` mirrors BT1_103. [Security] = exact BT1_100 pattern. |
| **BT3_106** | [Main] all own Digimon w/ Blocker|Reboot: SA +1 (turn) / [Security] add self to hand | [Main] `GrantPlayerScopeRestrictionBody(c => ChangeDigimonSAttackPlayerEffect(permanentCondition: own battle Digimon && (HasBlocker||HasReboot), +1, UntilEachTurnEnd, c))`; [Security] `AddThisCardToHandEffect`. AS-IS literally calls `ChangeDigimonSAttackPlayerEffect`. | `ChangeDigimonSAttackPlayerEffect` (CPF:8855) exists and is predicate-filtered + duration-tagged (`GainToPlayerScope`). Sonnet's "no predicate-filtered player-scope buff" is wrong. |
| **BT3_040** | (color half only) "also treated as Blue" | Color block → `ChangeCardColorClass` (the accepted path, cf. BT3_014's ported color block). | *Partly* false: the color half is portable now. The SA half is a genuine (shallow) gap → **G6**. |

> BT3_040: only the color half is a false STOP. Sonnet's "no opponent-scope continuous SA modifier"
> is **correct** for the current owner-scoped `ChangeSAttackStaticEffect` — see G6.

---

## 3. GENUINE primitive families (design sketches — design only)

### G1 — Reveal-top-N → PLAY/DIGIVOLVE the reveal-selected card  ⭐ (3 cards + BT1_078)
**Cards:** BT3_063 (play a [Chuumon] from top 3), BT3_070 (play a Lv6 Digimon from top 5),
BT3_073 (reveal N = opp-Digimon-count, play among them). **Sibling:** BT1_078 (digivolve top-3 Lv6
green onto self — also STOP).

**Why genuine:** the reveal+select machinery is fully present — `CardEffectFactory
.RevealDeckTopCardsAndSelect(revealCount, [RevealSelectPass(cond, 1, RevealDestination.Custom, …)],
remainingTo: DeckBottom)` records the pick in `RevealFlowState.CustomSelections` /
`TakeCustomSelections()`. Missing = the **follow-up that consumes the Custom selection and
plays/digivolves it**. `PlayCardEffect`/`ApplyPlayCard` only drop onto an empty slot;
`PlayPermanentCards` plays from a *zone*, not a reveal-recorded id list; `SelectAndDigivolveEffect`
opens its own fresh selections and can't consume a fixed id. (BT1_078's header documents this exact hole.)

**Design:** a new `IActivatedCardEffect` owning the whole reveal→play flow so the play step reads
`CustomSelections` directly:
```
CardEffectFactory.RevealTopAndPlaySelected(
    CardSource card,
    int revealCount,                     // int or Func<int> (073 dynamic)
    Func<HeadlessEntityId,bool> playable, // 063 name==Chuumon; 070 Lv6 Digimon; 073 playable Digimon
    RevealDestination remainingTo,        // DeckBottom
    PlayMode mode,                        // PlayAsNewPermanent | DigivolveOntoSelf (BT1_078)
    bool payCost = false, string description)
```
Internals: reuse `RevealAndSelect.RequestMultiChoice` with a single `Custom` pass; on flow completion,
for each `TakeCustomSelections()` id run the existing runtime primitive (`PlayPermanentCards(root:
Library, activateETB:true)` for new-permanent, or `FreeDigivolveHelpers.DigivolveFreeAsync(cardId,
self, fromZone:Library)` for BT1_078). Self-driven activated effect (resolver drives it at
OnAllyAttack/WhenDigivolving/OnDeletion). **No new choice type** — chains two existing primitives.

### G2 — OnUseOption reactive dispatch  ⚡ (3 cards) — SHALLOW WIRING
**Cards:** BT3_091 ([Your Turn][OPT] you use Option → +2 memory), BT3_096 ([All Turns] any Option
used → may suspend this Tamer for +1 memory), BT3_088-branch2 ([Your Turn][OPT] you use Option →
delete 1 opp Lv3 Digimon).

**Why genuine but shallow:** the `OnUseOption` window **is emitted** — `OptionActivateAction.cs:94`
`TriggerEventEmitter.Emit(…, TriggerTimings.OnUseOption, subject: optionCardId)` — and the gates
`CanTriggerWhenUseOption`/`CanTriggerWhenOwnerUseOption` exist (CPF:6884/6909), plus the
`EffectTiming.OnUseOption` enum member. The **only** missing piece:
`GameFlowProcessor.BridgeActivatedTriggersAsync` never routes it — `OnUseOption` is in **none** of
`SubjectScopedActivatedTimings` / `BoundaryActivatedTimings` / `EventBroadcastActivatedTimings`
(GameFlowProcessor.cs:528/556/581), so the emitted event is dropped before any reacting card resolves.
Sonnet's "no resolver call-site dispatches it" is literally true, but the fix is a one-line set entry.

**Design:** add `EffectTiming.OnUseOption` to `EventBroadcastActivatedTimings` (broadcast to every
battle-area card, threading the driving event so `TriggerEntityId=subject=optionCard`; each listener
self-gates via `CanTriggerWhenUseOption`). Confirm `TriggerTimingMap.Derive` yields the `"OnUseOption"`
name. Then the cards are plain uniform `ActivatedEffect`s: 091 → `MemoryBody(+2)`; 096 →
`SuspendSelfAndGainMemoryBody(+1)` (exists, ActivatedEffect.cs:205); 088-b2 → `SelectBody(Custom, opp
Lv3, 1) → destroy`. **Behavior caveat:** verify one window per option use and no double-fire with the
option's own OptionSkill.

### G3 — Play permanent with [On Play]/ETB suppressed (`activateETB:false`)  (2 cards)
**Cards:** BT3_110 (play purple Lv5 from trash, cost-free, ETB suppressed), **BT3_109** (grant a
Digimon "[On Deletion] play this card from trash cost-free, ETB suppressed" for the turn).
**Why genuine:** `PlayPermanentCards(activateETB:false)` **throws NotSupportedException** (CPF:7506) —
entry triggers derive from the zone move, no way to suppress [On Play]. Every current caller passes true.
**Design:** thread `activateETB` through the PlayCard mutation: `MatchStateMutationSink.PlayCardKind`
gains a `SuppressOnPlayKey`; when set, the post-move emitter skips the OnPlay/OnEnterField broadcast for
that card id (one-shot tag consumed at emit). Card-facing `SelectAndPlayFromZoneEffect(…,
suppressOnPlay:true)` covers 110. **BT3_109** additionally wraps it: `SelectBody(Custom, own Digimon){
onEachSelected: id => AddEffectToPermanent(perm, UntilEachTurnEnd, card, cardEffect:<On-Deletion
suppressed-play>, timing: OnDestroyedAnyone) }` — `AddEffectToPermanent`@OnDestroyedAnyone already
exists, so G3 unblocks the core of both.

### G4 — Atomic draw-then-discard factory/body  ⚡ (2 cards) — LOGIC ALREADY EXISTS
**Cards:** BT3_006 ([On Deletion] draw 1, then discard 1 — mandatory), BT3_088-branch1
([When Digivolving] draw 2, then discard up to 2).
**Sonnet claim is WRONG:** header says "no atomic draw-then-discard; splitting causes pre-flush
candidate-pool" — but `CardEffectCommons.DrawAndDiscardCards` (CPF:8289, "AS-IS verbatim") **draws AND
flushes** (`await sink.FlushAsync()` @8308) *before* building the discard list from the resulting hand,
with full `canNoSelect/canEndNotMax/targetCondition`. Atomicity is correct. Real gap: the helper has
**zero callers** and **no factory/body wraps it**.
**Design (thin):** `CardEffectFactory.DrawThenDiscardEffect(card, drawAmount, trashAmount,
discardOptional, description) => new ActivatedDrawThenDiscardEffect(...)` (async `IActivatedCardEffect`
calling `DrawAndDiscardCards`) + a resolver case (same "resolved via activation flow" pattern as
`ActivatedSelectAndPlayEffect`). 006 → OnDeletion (`CanActivateOnDeletion` gate); 088-b1 →
WhenDigivolving. No new mechanic.

### G5 — Digivolve-cost reduction gated on the FROM-permanent's identity  (3 cards) — BT1_109 family
**Cards:** BT3_031-branch1, BT3_103 ([Main] next green digivolution −5, may suspend 1), BT3_111-branch1.
**Sibling:** **BT1_109**.
**Why genuine:** headless `ChangeDigivolutionCostStaticEffect(int, bool, CardSource, Func<bool>?)`
(CPF:4653/4657) takes only a scalar `condition` — it cannot express "reduce cost **when digivolving FROM
a permanent whose top card is Paildramon/Dinobeemon**" (031/111) or "reduce the **next** digivolution of
a green permanent" (103). The digivolve-cost query doesn't thread the FROM (source) permanent id.
**Design:** thread the FROM-permanent id through cost resolution + overload
`ChangeDigivolutionCostStaticEffect(int changeValue, Func<Permanent,bool> fromPermanentCondition,
Func<CardSource,bool> movingCardCondition, ChoiceZone rootZone, bool isInheritedEffect, CardSource card,
Func<bool>? condition)` applying the delta only when the source permanent matches. 103 = activated
sibling: a one-shot `BeforePayCost`-gated reduction keyed on `CanTriggerWhenPermanentWouldDigivolve(green
top card)`, cleaned up at `AfterPayCost` (the two-effect `AddEffectToPlayer` pattern already exists) +
existing suspend-for-reduction (`SelectPermanentEffect.Mode.Tap` + `ChangeCost`).

### G6 — Continuous any-player-scoped predicate-filtered Security-Attack modifier  ⚡ (1 card)
**Card:** BT3_040 (SA half). **Why genuine:** `ChangeSAttackStaticEffect` (CPF:4893) is **owner-scoped**
(`PlayerScopeModifierEffect` with no `scopeAnyPlayer`), so an opponent-scope predicate never matches.
**Design:** add the `scopeAnyPlayer:true` overload already used by `ChangeSecurityDigimonCardDPStaticEffect`
(CPF:5816) — 1:1 mirror. Predicate = opp battle Digimon && `HasNoDigivolutionCards`; `condition = IsOpponentTurn`.

### G7 — Select from a permanent's OWN digivolution stack → move → self follow-up  (1 BT3 + BT1_084)
**Cards:** BT3_112-[When Attacking] (return 1 Lv6 digivolution card of this Digimon to hand → self
`Unblockable` for turn). **Sibling:** **BT1_084** (return Lv6 source → self Unsuspend).
**Why genuine:** the source pool is the permanent's **own digivolution cards** (AS-IS `SelectCardEffect
Root.Custom, customRootCardList: this.DigivolutionCards`), not a `ChoiceZone`; and it needs a per-select
**self-mutation** follow-up. **Design:** `SelectFromOwnDigivolutionStackBody(card, canTarget: Lv6 source
predicate, maxCount:1, toZone: Hand, onSelected: () => GainCanNotBeBlocked(self, UntilEachTurnEnd,
card))`. Mirrors `SelectBody.onEachSelected` but pool = `permanent.DigivolutionCards`. Grants already exist.

### G8 — Attach a hand card onto a permanent's OWN digivolution stack  (1)
**Card:** BT3_019 ([When Digivolving] may place 1 named card from hand under this Digimon → +3 memory).
**Why genuine:** no headless mutation writes a selected card **into** a permanent's digivolution stack
(catalog has stack read/trash only; AS-IS `AddDigivolutionCardsTop(selected)`). **Design:** sink mutation
`AttachAsDigivolutionCardKind` (target permanent + source card from Hand) + factory
`SelectAndAttachDigivolutionEffect(card, canTarget, maxCount, toPermanent:self, canEndNotMax:true, desc)`.
+3 memory = a following `MemoryBody(+3)` (resolver runs multi-effect sequences, cf. `TfxSelectFollowUp`).

### G9 — Nested dependent-pool select → play (from a permanent's digivolution cards)  (1)
**Card:** BT3_030 ([When Digivolving] select own Digimon → select 1 of **its** Lv≤4 digivolution cards →
play as new Digimon, cost-free). **Why genuine:** two chained selects, the second pool depends on the
first pick (`customRootCardList = selectedPermanent.DigivolutionCards`), then play. **Design:** two-phase
`SelectPermanentThenPlayFromItsStackEffect` — permanent choice, then a choice scoped to that permanent's
digivolution cards, then `PlayPermanentCards(root: DigivolutionCards, activateETB:true)`. Shares the
"play a card that isn't a plain zone member" need with G1.

### G10 — De-Digivolve then act on the RESULTING (post-de-digivolve) state  (2 cards)
**Cards:** BT3_107 ([Main] select 1 opp Digimon → De-Digivolve 1 → **if new top cost ≤4** delete it),
BT3_112-[When Digivolving] (De-Digivolve 1 on **all** opp Digimon → delete all opp Digimon DP ≤5000).
**Why genuine:** the destroy decision depends on state produced by the earlier de-digivolve within one
activation. In the sink model both flush together, so a body can't read post-de-digivolve cost/DP before
deciding. `SelectAndDeDigivolveEffect` exists but has no conditional follow-up. **Design:** a sequenced
composite with an explicit flush boundary between de-digivolve and predicate eval —
`SelectDeDigivolveThenConditionalDestroyEffect(card, canTarget, count, destroyIf: perm => cost ≤4)` (107)
and a no-select mass variant `MassDeDigivolveThenConditionalDestroy(match, count, destroyIf: DP ≤ thr)` (112-WD).

### G11 — Digisorption keyword family  🧱 (2 cards) — DEEP ENGINE
**Cards:** BT3_054, BT3_056. **Why genuine:** a whole keyword mechanic — a new `WhenDigisorption` timing
broadcast fired during a from-battle-area digivolution, a cut-in resolution
(`autoProcessing_CutIn.TriggeredSkillProcess`), "suspend a Digimon as a cost to reduce this
digivolution's memory cost by N" (`ChangeCostClass` on `UntilCalculateFixedCostEffect`), and 056's
[Once Per Turn] "suspend an opponent's Digimon instead" (`CanSuspendByDigisorptionClass`).
**Design (outline):** (1) emit `WhenDigisorption` from the digivolution flow when the source is an own
battle-area permanent; add `EffectTiming.WhenDigisorption` to the broadcast set. (2)
`SuspendForDigivolveCostReductionEffect(count, reduction)` body (SelectPermanentEffect.Mode.Tap +
fixed-cost reduction). (3) `CanSuspendByDigisorption` continuous grant redirecting the tap target to an
opponent's Digimon. Largest item — a dedicated engine goal, not a card pass.

### G12 — Choose a count (0..N), then apply to ALL matching  (1)
**Card:** BT3_100-Part A (choose 0–2, trash that many digivolution cards from the **bottom** of **every**
opponent Digimon). (Part B "if you have a green Digimon, suspend 1 opp Digimon with no digivolution
cards" is portable now via `SelectBody(Mode.Tap)` gated on `HasMatchConditionOwnersPermanent(green)`.)
**Why genuine:** no count-selection primitive (AS-IS `SelectCountEffect`); `TrashDigivolutionCardsFromTopOrBottom`
exists but nothing picks a shared N and fans it out. **Design:** `SelectCountEffect(min,max)` choice
primitive + body `ChooseCountThenApplyToAllMatching(match, perTarget: (perm,n) =>
TrashDigivolutionCardsFromTopOrBottom(perm, min(n, perm.DigivolutionCards.Count), fromTop:false))`.

### G13 — Opponent makes a binary decision → branch  (1)
**Card:** BT3_102 ([Main] opponent may trash their top security; **if they don't**, you Recovery +1).
**Why genuine:** AS-IS `SetBoolSelection(selectPlayer: Owner.Enemy)` — an **opponent-owned** yes/no whose
result branches to two effects. No headless primitive requests an opponent binary decision + branch.
**Design:** `OpponentBinaryChoiceEffect(card, prompt, ifYes:Action, ifNo:Action, autoNoWhen:Func<bool>)`
— issues a `ChoiceType.Confirm` request to the opponent (auto-false when they have no security), then runs
the branch (`ifNo` = `RecoveryBody(+1)`, `ifYes` = destroy 1 own-security). Reuses the choice controller.

### G14 — Optional select-from-zone with a CONDITIONAL follow-up  ⚡ (1) — SHALLOW
**Card:** BT3_034 ([On Play] look at top security → **may** add to hand → **if added**, Draw 1).
**Why genuine (minor):** `SelectAndAddToHandFromZoneEffect(Security, top-only)` covers the optional add,
but the Draw must fire **only when the add happened**; a sequential unconditional `DrawCardsEffect`
over-delivers. **Design:** add an `onEachSelected` hook to the zone-select body (symmetric with
`SelectBody.onEachSelected`, which today exists only for the permanent-select body):
`SelectAndAddToHandFromZoneEffect(…, onEachSelected: () => DrawCards(1))`. Reusable for any
"move a card then react per move".

### G15 — Pay-memory cost + optional play-from-zone + immediate self-delete  (2 cards)
**Cards:** BT3_086 (pay 3 → may play 1 [MaloMyotismon] from **hand** cost-free → delete self),
BT3_087 (same, from **trash**). **Why genuine (low depth):** pieces exist —
`GainMemoryActivatedEffect(−3)`, `SelectAndPlayFromZoneEffect(zone, activateETB:true, canEndNotMax:true)`,
and multi-effect sequencing (`TfxSelectFollowUp`) — but there is **no immediate self-destroy activated
primitive** (`AddSelfDeleteEffect` is *delayed/timing-scheduled*, CPF:8246; AS-IS runs
`DestroyPermanentsClass(self).Destroy()` immediately) and no composite ties cost→play→self-delete.
**Design:** (1) `SelfDestroyEffect(card)` activated body (immediate self-Destroy mutation). (2) Port each
as a 3-effect sequence under one `isOptional:true` OnAllyAttack activation gated on `MaxMemoryCost ≥ 3`:
`GainMemoryActivatedEffect(−3)` → `SelectAndPlayFromZoneEffect(zone, MaloMyotismon, 1, canEndNotMax:true)`
→ `SelfDestroyEffect`. **Verify behaviorally** the 3-effect sequence runs atomically (Tfx proves seq
resolution; memory-as-cost + mandatory tail ordering needs a behavior check).

### G16 — Select a card from a zone (trash) → place onto SECURITY (top)  (1)
**Card:** BT3_041 ([When Attacking] if sec ≤3, place 1 own yellow Digimon **card** from trash face-down on
top of security). **Why genuine:** `SelectAndPutSecurityEffect` targets **battle-area permanents** (Mode
PutSecurity*), not a card in the trash; `SelectAndAddToHandFromZoneEffect` only routes to Hand. Nothing
selects a *card* from trash and inserts it into security. **Design:**
`SelectAndPutSecurityFromZoneEffect(card, fromZone: Trash, canTarget, maxCount, toTop:true, canEndNotMax,
description)` — a zone-card select whose mutation is `AddSecurityKind(top)` (AS-IS `IAddSecurity`, which
also fires the security-count recovery visual).

### G17 — Grant "ignore the [Security] effects of Option cards it checks"  (1)
**Card:** BT3_097 ([Main] select 1 own Digimon → for the turn it "doesn't activate the [Security] effects
of any Option cards it checks"). **Why genuine (verify):** AS-IS grants `DisableEffectClass` with
`InvalidateCondition = effect.SourceCard.IsOption && effect.IsSecurityEffect && attackingPermanent ==
selected`. Closest headless is `CanNotAffectedStaticEffect(perm, skillCondition)` (immunity keyed on
source predicate), but it's an *immunity*, not a per-security-check disable, and it's unclear
`CardSource` exposes `IsSecurityEffect`. **Design:** `GainIgnoreOptionSecurityEffect(perm, duration,
card)` consumed at the option-security-check seam. **Confirm against `CanNotAffectedStaticEffect` first**
— if source-predicate + effect-flag are reachable there, this collapses to a `SelectBody.onEachSelected
→ CanNotAffectedStaticEffect(...)` FALSE STOP; otherwise it is a genuine narrow grant.

---

## 4. Priority (most cards unblocked first)

| Rank | Family | Cards | Depth |
|---|---|---|---|
| 1 | **G2** OnUseOption dispatch | 091, 096, 088-b2 | ⚡ one-line set membership |
| 2 | **G1** reveal→play selection | 063, 070, 073 (+BT1_078) | medium (compose 2 existing prims) |
| 3 | **G5** FROM-permanent cost gate | 031-b1, 103, 111-b1 (+BT1_109) | medium (cost-query id threading) |
| 4 | **G4** draw-then-discard wrapper | 006, 088-b1 | ⚡ logic exists, wrap only |
| 5 | **G3** play w/ ETB suppressed | 109, 110 | medium |
| 6 | **G10** de-digivolve→conditional | 107, 112-WD | medium (flush boundary) |
| 7 | **G15** pay+play+self-delete | 086, 087 | low (self-destroy prim + verify) |
| 8 | **G11** Digisorption | 054, 056 | 🧱 deep (new keyword) |
| 9 | **G7** own-stack select→self | 112-atk (+BT1_084) | medium |
| 10 | G6 SA any-scope | 040 | ⚡ overload |
| 11 | G14 conditional zone-select follow-up | 034 | ⚡ hook |
| 12 | G8 attach-to-stack | 019 | medium |
| 13 | G9 nested dependent play | 030 | medium |
| 14 | G12 count-select+apply-all | 100 | medium |
| 15 | G13 opponent binary branch | 102 | medium |
| 16 | G16 zone-card→security | 041 | low |
| 17 | G17 ignore-option-security grant | 097 | low (verify vs CanNotAffected) |

**Quick wins (⚡, wiring-only, unblock 7 cards):** G2 (3), G4 (2), G6 (1), G14 (1) — plus the 9 FALSE
STOPs need no primitive at all, only re-porting.

---

## 5. Verification method note

Every FALSE STOP was confirmed by (a) locating the exact factory/body/helper symbol, (b) reading a live
ported card using the same composition (BT1_003/BT1_100/BT1_103/BT1_105/ST4_10), and (c) confirming the
resolver/bridge path (`ActivatedEffectResolver.cs:355` for `maxCountPerTurn`;
`ActivatedSelectFromZoneEffect.BuildRequest` for optional select). Genuine gaps were confirmed by reading
the throwing/absent path (`PlayPermanentCards:7506`; owner-only `PlayerScopeModifierEffect`; the empty
`OnUseOption` dispatch sets; absent `SelectCount`). Recurring Sonnet failure mode: stopping at a leaf
factory instead of the uniform composition layer — same class as the BT2
`MatchConditionOwnersCardCountInTrash` precedent.
