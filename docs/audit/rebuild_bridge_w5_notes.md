# Bridge batch W5 — notes (UserSelectionManager mode-pick + IDigiBurst) + final invented-caller re-ports

Implements the two remaining verbatim-corpus type gaps from docs/audit/rebuild_p5_cards_missing.md
(batch-2 finding 3: `GManager.userSelectionManager` / `SelectionElement<T>`; batch-3 finding 1: `IDigiBurst`)
and re-ports the last two invented-caller cards (BT8_057, BT9_109) to the literal AS-IS structure. Continues
the W1–W4 conventions: AS-IS-path files, AS-IS signatures translated (`IEnumerator`→`Task`,
`Player`→`HeadlessPlayerId` at param sites, `ICardEffect activateClass`/`cardEffect` KEPT), delegation into
verified substrate only, UI/Photon strips cited in-code with AS-IS line anchors, design item (RD-W5-n) where a
corner is not reproducible — never a silent drop.

## Files

- `Script/UserSelectionManager.cs` (AS-IS-path skeleton FILLED) — `UserSelectionManager` + `SelectionElement<T>`.
- `Script/GManager.cs` — additive `userSelectionManager` property (AS-IS GManager.cs:114 field).
- `Script/CardController.cs` — new `#region Digi-Burst` with the `IDigiBurst` mirror (AS-IS
  CardController.cs:2114-2264), placed after the Recovery region (AS-IS adjacency).
- `Script/Player.cs` — `Player.HandCards` (AS-IS Player.cs:506).
- `Script/CardSource.cs` — `HasXAntibodyTraits` (AS-IS CardSource.cs:1975).
- `Script/DataBase.cs` — `IsXAntibodyString` (AS-IS DataBase.cs:440, pure string helper).
- Cards: `CardEffect/BT8/Green/BT8_057.cs`, `CardEffect/BT9/White/BT9_109.cs` (full AS-IS re-ports);
  `CardEffect/BT1/Green/BT1_111.cs`, `CardEffect/ST4/Green/ST4_13.cs` (UNRESOLVED markers retired; ST4_13's
  `card.PermanentOfThisCard()`-as-`Permanent` args moved to the BT1_001 `ICardEffect.ResolvePermanentOfThisCard`
  convention).

## 1. UserSelectionManager / SelectionElement&lt;T&gt; (choice-substrate mapping)

AS-IS (DCGO Script/UserSelectionManager.cs, 221 lines): `SetBoolSelection`/`SetIntSelection(selectionElements,
selectPlayer, selectPlayerMessage, notSelectPlayerMessage, IsLocal)` reset state (:104-107/:158-161) and open
the command panel (select player) or the AI random branch; the pick travels button-closure → `SendSelection` →
`Set*_RPC` → `Set*ForPlayer` → `player.QueuePlayerSelection(ValueSelection)`; `WaitForEndSelect` (:77-100)
polls the queue, dequeues the value onto the shared INT channel (`_selectedIntValue`; bool rides
`getIntFromBool`/`getBoolFromInt` — the AS-IS RPC transport is int-typed for both), resets
`_endSelect`/`_selectPlayer`, and the effect reads `SelectedBoolValue`/`SelectedIntValue`. `SetBool`/`SetInt`
are the direct setters for the "only one branch is live" path (`_endSelect = true`, no player choice).

Mirror mapping:

- **All transport plumbing** (panel buttons / [PunRPC] / player selection queue / AI random branch) collapses
  to ONE `context.ChoiceProvider.ChooseAsync(ChoiceRequest)` inside `WaitForEndSelect` — the W4 select-effect
  precedent ("the ChoiceProvider request IS the transport"). The request follows the established **ModeChoice
  primitive** exactly (`ChoiceType.ModeChoice`, minCount 1 / maxCount 1 / canSkip false,
  `ChoiceZone.BattleArea`, synthetic labeled candidates — `ModeChoiceEffect.BuildRequest`'s shape; that enum
  member's doc names AS-IS UserSelectionManager as its original). Candidate id = `userSelection#{index}`,
  label = `SelectionElement.Message`; the picked index maps back to the element's value on the int channel.
- `Set*Selection` stores the normalized `(Message, Value, SpriteIndex)` list + select player + prompt with the
  verbatim AS-IS reset; the request is issued at `WaitForEndSelect`, where AS-IS blocks (RD-W5-1).
- A skipped/empty result reproduces the AS-IS null-dequeue fallthrough (:84-88): `_selectedIntValue` keeps its
  reset value 0.
- Post-wait reset (:95-96) verbatim (`_endSelect = false; _selectPlayer = null;` + pending list cleared —
  the mirror of the panel closing); the SELECTED VALUE persists across the reset, exactly as AS-IS
  (`SelectedBoolValue` is read after `WaitForEndSelect`).
- Deferred-choice/replay safety: a suspension inside or after the pick re-runs the card body on resume;
  `Set*Selection` is pure in-memory state and the re-issued request is served by the DeferredChoiceProvider's
  recorded answer — same contract as the W4 select effects (no journal needed; nothing is emitted).
- ONE match-scoped instance via `GManager.userSelectionManager` (context-cached service, `AttachContext`),
  mirroring the single component on the AS-IS GManager GameObject — per-instance field state (e.g. the value
  surviving the post-wait reset) behaves as AS-IS.
- `SelectionElement<T>` ported verbatim (same file, namespace level, ctor param names kept — BT1_111 calls it
  with named args).

## 2. IDigiBurst

AS-IS `IDigiBurst` (CardController.cs:2114-2264) ported at the AS-IS class shape — ctor
`(Permanent, int DigiBurstCount, ICardEffect cardEffect)` (ICardEffect KEPT, W4 convention),
`SetUpToMaxCount()`, `public List<CardSource> discardedCards`, `CanDigiBurst()`, `DigiBurst()` — into the
mirror CardController.cs (its AS-IS home file), delegating every step to verified substrate:

- **CanDigiBurst** (:2135-2160) verbatim: host non-null → TopCard non-null (mirror TopCard is never null,
  kept for shape — the ITrashDigivolutionCards :5153 precedent) → `ImmuneFromStackTrashing(_cardEffect)`
  (:2141) = the SAME `RestrictionScan.IsRestricted(ImmuneStackTrashingKey, host, causeId)` scan
  ITrashDigivolutionCards applies (self-contained-privates style) → `Some`/`Count` over
  `_permanent.DigivolutionCards` with the public MIG5 `CardSource.CanNotTrashFromDigivolutionCards(causeId)`
  per-source protection (cause = `_cardEffect.EffectSourceCard.InstanceId` throughout).
- **DigiBurst** (:2162-2263) in AS-IS statement order: the controller SELECTS which sources to discard via the
  W4-bridged `GManager.instance.GetComponent<SelectCardEffect>()` at the EXACT AS-IS 16-param `SetUp`
  (maxCount = burst count, `canEndNotMax = _upToMaxCount`, `canNoSelect: () => false`,
  `canEndSelectCondition` = non-empty, Mode.Custom over `customRootCardList = _permanent.DigivolutionCards`,
  select player = the host's top-card owner, `cardEffect: null` — AS-IS passes null here) + `SetUseFaceDown()`
  + the 2-param `SetUpCustomMessage`, then:
  - **OnUseDigiburst window BEFORE the trash** (:2218-2228, AS-IS `StackSkillInfos(hashtable
    {"Permanent","CardEffect"}, EffectTiming.OnUseDigiburst)`): the queue emit
    `TriggerTimings.OnUseDigiburst(actor = host controller, subject = host top card)` — the exact verified
    emit shape of the resolver's DigiBurstActivatedEffect path (ActivatedEffectResolver.cs:508), **journaled**
    (a private duplicate of the resolver's EmitJournaled/RunJournaledImmediate) so a later choice in the same
    body suspending and replaying does not double-emit. RD-W5-5 records the payload nuance.
  - trash = the 1:1 `ITrashDigivolutionCards(_permanent, selectedCards, causeId).TrashDigivolutionCards()`
    (:2233) — the MIG3-3b carrier (host gates + per-source protection + OnDigivolutionCardDiscarded window +
    ACE overflow inside).
  - `discardedCards` collects ALL selected cards (:2235) whether or not each was actually trashed — AS-IS
    quirk KEPT (partial-success callers read the AS-IS truth).
  - add-log/PlayLog (:2237-2259) = UI (stripped, cited).
- Card-site convention: `new IDigiBurst(card.PermanentOfThisCard(), …)` is expressed as
  `new IDigiBurst(ICardEffect.ResolvePermanentOfThisCard(card), …)` (the established
  PermanentView-vs-Permanent bridge, BT1_001 header) — ST4_13's two sites updated.

## 3. Card re-ports (the last 2 invented callers)

### BT8_057 (E-3 WITNESS) — fully resolved, zero shim errors

Both halves rewritten from the invented `CardEffectFactory.CanNotPlayOptionStaticEffect` / old-model
`ActivatedEffect`+`TrashSecurityBody` to the literal AS-IS structure:

- `timing == None`: literal `CanNotPlayClass` + `SetUpCanNotPlayClass(cardCondition:)` (the ported kind-class)
  with the verbatim nested CanUse (host on field ∧ ALL owner battle-area Digimon suspended — the
  `GetBattleAreaDigimons().Count == …Count(IsSuspended)` comparison verbatim via the batch-3 HeadlessPlayerId
  extension ∧ opponent's turn) and CardCondition (`cardSource.Owner == card.Owner.Enemy` →
  `new Player(card.Context, card.Owner).Enemy?.PlayerId`, the BT2_023 idiom; `IsOption`).
- `timing == OnUnTappedAnyone`: inline `ActivateClass`; gate verbatim incl. the MIG6
  `GManager.instance.turnStateMachine.gameContext.TurnPhase == GameContext.phase.Active` and
  `CanTriggerWhenPermanentUnsuspends(hashtable, permanent => permanent.InstanceId ==
  card.PermanentOfThisCard().TopInstanceId)` (the BT2_002 identity idiom); CanActivate =
  `card.Owner.Enemy.SecurityCards.Count >= 1` → `CardEffectCommons.OpponentSecurityCount(card) >= 1` (the
  mirror helper documented against exactly this AS-IS line); body = the MIG3-3a `IDestroySecurity` carrier at
  its mirror ctor shape (`(context, OpponentOf(card), 1, activateClass.EffectSourceCard.InstanceId,
  fromTop: true)`).

### BT9_109 (C-3 WITNESS) — whole 5-block AS-IS body restored; 5 explained shim errors

All five AS-IS timing blocks (the old port had dropped/composited three and STOPped two):

1. `None` / `IgnoreColorConditionClass` — literal kind-class (was the invented `UseRequirements` route).
2. `SecuritySkill` — inline `ActivateClass` + `SetIsSecurityEffect(true)`; body =
   `card.Owner.AddMemory(1, activateClass)` (W4 extension) then `AddThisCardToHand(card, card)` (ST3_13
   sourceCard convention). Replaces the old two-effect composite (which had also lost the single-ActivateClass
   atomicity of memory+hand).
3. `OptionSkill` (**retires the old C3-03 STOP**) — verbatim Permanent-shaped `CanSelectPermanentCondition`
   (owner battle-area Digimon with zero "X Antibody"/"XAntibody" digivolution cards) + the BT2_097 id-shape
   adapter for the W4 call sites; W4 `SelectPermanentEffect` Mode.Custom + per-selected coroutine capturing
   `selectedPermanent`; `!IsToken` guard verbatim; tuck = MIG4
   `Permanent.AddDigivolutionCardsBottom([this card], causeId)`. `card.Owner.brainStormObject.
   CloseBrainstrorm(card)` = UI strip (BrainStormObject.cs is a pure gameObject.SetActive hand-overlay widget).
4. `None` / `CanNotTrashFromDigivolutionCardsClass` — the C-3 WITNESSED half, literal kind-class,
   `SetIsInheritedEffect(true)`, incl. the AS-IS `CardEffectCondition(ICardEffect) = cardEffect != null` at
   its TRUE signature (the old port had re-shaped it to `Func<CardSource,bool>`).
5. `OnAllyAttack` (**retires the old C3-04 STOP**, inherited optional) — verbatim structure; hand reads via the
   new `Player.HandCards` (`new Player(card.Context, card.Owner).HandCards`, BT2_023 idiom);
   `cardSource.HasXAntibodyTraits` via the new mirror property; the digivolve-play =
   the mirror `PlayCardClass(cardSources, CardEffectHashtable(activateClass), payCost: true, targetPermanent:
   ResolvePermanentOfThisCard(card), isTapped: false, root: Hand, activateETB: true).PlayCard()`.
   **Kept verbatim + logged (the pass's only unresolved members, all in this block):**
   1. `SelectHandEffect` — the mirror Script/SelectHandEffect.cs declares NO type (skeleton file): the AS-IS
      `GetComponent<SelectHandEffect>()` + 12-arg `SetUp` + `SetUpCustomMessage_ShowCard` + `Activate()` block
      is kept at AS-IS shape → 2× CS0246 + 1× CS0103 (`SelectHandEffect.Mode.Custom`) under the shim, masked
      in the plain build (body-only references — the batch-2 SelectionElement precedent).
   2. `cardSource.CanPlayCardTargetFrame(...)` — declared nowhere on the mirror CardSource (pre-existing
      masked-verbatim precedent: PlayCardClass.cs's own call) → 1× CS1061.
   3. `card.PermanentOfThisCard().PermanentFrame` — no frame/slot model (MIG5-FRAME-MODEL) → 1× CS1061.

## 4. New mirror surfaces (AS-IS-anchored, verified-delegate only)

- `Player.HandCards` (AS-IS Player.cs:506) — the same IZoneStateReader Hand read
  `HasMatchConditionOwnersHand` uses; property (a bare HeadlessPlayerId cannot carry an extension PROPERTY,
  so card sites use the `new Player(card.Context, card.Owner).HandCards` route).
- `CardSource.HasXAntibodyTraits` (AS-IS CardSource.cs:1975) + `DataBase.IsXAntibodyString` (AS-IS
  DataBase.cs:440) — verbatim pure helpers.
- `GManager.userSelectionManager` — see §1.

## 5. Verification

Plain declaration gate:

```
dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj -clp:ErrorsOnly 2>&1 \
  | grep -oE 'error CS[0-9]+' | sort | uniq -c
     59 error CS0246        # identical to baseline, before AND after the batch
```

Shim body-check (the W3 method — temporary empty `IActivatedCardEffect`, full rebuild, filter, delete,
reconfirm): pre-change total **646** project-wide (matches the batch-3 notes), post-change **637**. The −9 is
fully accounted per-file (verified by a stash/rebuild diff of the two error lists — no other file's count
moved):

- BT1_111: 8 → **0** (SelectionElement CS0246s + userSelectionManager CS1061s — resolved by the W5 bridge).
- ST4_13: 2 → **0** (IDigiBurst CS0246s — resolved).
- BT8_057: 1 → **0** (the invented-factory residue — resolved).
- BT9_109: 3 → **5** — exactly the three logged findings above (2× SelectHandEffect CS0246 + 1× CS0103 +
  1× CanPlayCardTargetFrame CS1061 + 1× PermanentFrame CS1061); zero unexplained.
- W5 bridge/support files (UserSelectionManager.cs, GManager.cs, CardController.cs, Player.cs, DataBase.cs):
  **0** error lines each; CardSource.cs shows exactly its 14 PRE-EXISTING masked-verbatim errors (same set as
  the W4 notes, line numbers shifted).

Shim deleted; plain baseline re-confirmed at `59 error CS0246`.

## Design items (RD-W5-n)

- **RD-W5-1** — UserSelectionManager request timing: AS-IS presents the panel at `Set*Selection` and blocks at
  `WaitForEndSelect`; the mirror issues the ChoiceProvider request at `WaitForEndSelect` (request/response
  substrate). No AS-IS caller reads state between the two calls, so the action surface is identical.
- **RD-W5-2** — the AS-IS AI branch (`GManager.instance.IsAI` → `UnityEngine.Random` pick, :128-140/:182-194)
  is folded into the single ChoiceProvider request: the provider policy owns every seat's decision (the same
  collapse the W4 select effects apply to the AS-IS panel/AI split). The AS-IS random tie-break is not
  reproduced (it is a policy, not a rule).
- **RD-W5-3** — `WaitForEndSelect` with neither a pending selection nor a direct SetInt/SetBool value is an
  AS-IS infinite poll (`WaitWhile(!_endSelect)`); the mirror THROWS instead of hanging.
- **RD-W5-4** — `SelectionElement.SpriteIndex` (the AS-IS button icon) and `notSelectPlayerMessage`/`_isLocal`
  are carried as AS-IS state but have no ChoiceRequest surface (candidate label = Message only) — cosmetic.
- **RD-W5-5** — IDigiBurst's OnUseDigiburst emit carries `(actor, subject)` like the verified resolver path;
  the AS-IS hashtable also threads the live `_cardEffect` ("[When you use Digi-Burst]" gates may take a
  `cardEffectCondition`, CanTriggerWhenUseDigiBurst). The mirror gate reconstructs the Permanent from the
  subject; the CardEffect payload half is unthreaded — SAME nuance as the pre-existing
  DigiBurstActivatedEffect emit, latent (no OnUseDigiburst reactor with a cardEffectCondition is ported, and
  OnUseDigiburst is not yet in the ActivatedBridgeTimings/EventBroadcast sets — an F-1 confluence item).
