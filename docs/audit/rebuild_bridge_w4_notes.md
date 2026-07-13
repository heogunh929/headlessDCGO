# Bridge batch W4 — notes (the AS-IS SELECTION machinery)

Implements the P5 log's single largest remaining verbatim-corpus gap (docs/audit/rebuild_p5_cards_missing.md,
2026-07-13 section, finding 2): the AS-IS `GManager.instance.GetComponent<SelectPermanentEffect|SelectCardEffect>()`
→ `SetUp(full AS-IS params)` → `await Activate()` pattern, plus the two small gaps from the same log
(`CardSource.BaseENGCardNameFromEntity`, `Player.AddMemory(int, ICardEffect)`). Continues the W1–W3 conventions:
AS-IS-path files, AS-IS signatures translated (`IEnumerator`→`Task`, `Player`→`HeadlessPlayerId`,
`Func<Permanent,bool>` target predicates → the established `Func<HeadlessEntityId,bool>` id idiom the ported
corpus already uses, `ICardEffect activateClass`/`cardEffect` KEPT), delegation into verified substrate wherever
it models the behaviour, wrapper-side implementation where it does not, explicit design item (RD-W4-n) where
neither is possible — never a silent drop. UI/Photon statements are stripped with their AS-IS line anchors cited
in-code.

## Files

- `Script/GManager.cs` — additive generic `GetComponent<T>()`.
- `Script/SelectPermanentEffect.cs` — AS-IS 11-param `SetUp` + `Activate()` + the AS-IS setter surface.
- `Script/SelectCardEffect.cs` — AS-IS 16-param `SetUp` + `Activate()` + the AS-IS setter surface.
- `Script/CardSource.cs` — `BaseENGCardNameFromEntity` (one property).
- `Script/Player.cs` — `MemoryForPlayer` / `CanAddMemory(ICardEffect)` / `AddMemory(int, ICardEffect)` + the
  `HeadlessPlayerId.AddMemory` extension (`PlayerIdAsIsExtensions`).

No card, monolith, or engine-executor edits. All legacy members of the two select classes (`BuildRequest` /
`BuildMutations` / `Apply` / `TryOpenAttack` / the 8-param `SetUp`s) are untouched; the AS-IS surface is purely
additive (separate fields where a legacy member of a different shape already owns the AS-IS name — W3's
`…Card` / `…_Permanents` suffix convention).

## 1. GManager.GetComponent&lt;T&gt;()

AS-IS `GetComponent<T>()` returns THE one component instance on the GManager GameObject. Mirrored as ONE
match-scoped instance, context-cached exactly like `AutoProcessing.For` (`EngineContext.TryGetService` /
`RegisterService`); the instance gets the `EngineContext` injected (`AttachContext`) — the AS-IS component reads
the same state through the `GManager.instance` global. **AS-IS quirk deliberately reproduced by the caching**:
`SelectPermanentEffect.SetUp` does NOT reset `_canAttackPlayer`/`_defenderCondition`/`_isFaceUp`, so those
persist across uses of the shared instance — a per-call fresh instance would silently diverge.

Supported T: `SelectPermanentEffect`, `SelectCardEffect`. **OptionalSkill was NOT bridged (not trivial)** — the
mirror `Script/OptionalSkill.cs` declares no type at all (skeleton comment file), and its `SelectOptional`
yes/no flow is the WindowResolver's optional-prompt territory. Any other T throws `NotSupportedException`
(design item RD-W4-3). Note: the generic method makes `GetComponent<X>()` COMPILE for any existing type X, so
the old masked-verbatim call sites (ICardEffect.cs `OptionalSkill`, PlayCardClass.cs `SelectAssemblyClass`/
`SelectCountEffect`/`Effects`/…) shift from "no GetComponent" body errors to their real missing-type/member
errors — same files, still pre-existing, unchanged count class.

## 2. SetUp overloads bridged (count) and shapes

**SetUp overloads: 2** — AS-IS has exactly ONE `SetUp` per class; both are bridged at full AS-IS arity:

- `SelectPermanentEffect.SetUp` — all **11** AS-IS params (selectPlayer, canTargetCondition,
  canTargetCondition_ByPreSelecetedList, canEndSelectCondition, maxCount, canNoSelect, canEndNotMax,
  selectPermanentCoroutine, afterSelectPermanentCoroutine, mode, cardEffect). Resets exactly the AS-IS-reset
  fields (`_isLocal`/`_isdigiXros`/custom messages/`_degenerationCount`); keeps the AS-IS non-reset quirk.
- `SelectCardEffect.SetUp` — all **16** AS-IS params (…, canNoSelect as `Func<bool>`, message, maxCount,
  canEndNotMax, isShowOpponent, mode, root, customRootCardList, canLookReverseCard, selectPlayer, cardEffect).
  Resets the full AS-IS reset list (:45-62) including `_skillInfos`/`_afterSelectIndexCoroutine`.

**Full AS-IS setter surface added** (beyond what the cards use today, so future verbatim ports compile):
permanent side — `SetIsLocal`, `SetDigiXros`, AS-IS 3-param `SetUpCustomMessage(CustomMessage,
CustomMessage_Enemy, customMessageArray)`, `SetUpCustomBackButtonMessage`, `SetCanNotAttackPlayer`,
`SetDefenderCondition(Func<Permanent,bool>)` (wrapped onto the id-shape attack option at Activate time),
`SetPlaceFaceUp`, public field `_noSelect`, `active()` (AS-IS names; `SetDegenerationCount` already existed);
card side — `SetIsLocal`, `SetIsDeckBottom/Top`, `SetNotShowCard`, `SetNotAddLog`, `SetDigiXros`, `SetAssembly`,
`SetIsSecurity`, `SetUseFaceDown`, `SetUpSkillInfos`, `SetReducedCostTuple`/`SetFixedCostTuple` (STOP on use —
RD-W4-1), 2-param `SetUpCustomMessage`, `SetUpCustomMessage_ShowCard`, `SetUpCustomCountText`,
`SetShowReverseCard`, `SetUpAfterSelectIndexCoroutine`, `RootCardList()`, `active()`.

Overload-resolution note: the legacy 1-param `SetUpCustomMessage(string)` wins a 1-arg call over the new
AS-IS 3-optional-param overload (C# prefers the no-optional-fill candidate) — since AS-IS cards CAN legally
call 1-arg (AS-IS has defaults), the legacy overload now ALSO feeds the AS-IS `_customMessage` channel
(inert for legacy consumers, which never read it).

## 3. Activate() semantics mapping

Both `Activate()`s follow the AS-IS statement order 1:1, with every strip citing its AS-IS line anchor in-code.
The selection itself is the mirror choice substrate: `context.ChoiceProvider.ChooseAsync(ChoiceRequest)` — the
same primitives (and the same verified SelectCardPanel formula, `(cond==null||cond(selected)) &&
(canEndNotMax || count==max)`, SelectCardPanel.cs:568) the W3 reveal-select bridge established.

### SelectPermanentEffect.Activate (AS-IS :242-1039)

- **CanTarget** (:150-178) verbatim: the `CanSelectBySkill` untargetability scan applies ONLY to permanents
  owned by neither the effect-source owner nor the select player (AS-IS :158) — via
  `RestrictionScan.IsRestricted(CannotBeSelectedBySkillKey, candidate, TRUE effect-source id)` (the same scan
  the legacy BuildRequest path uses, but with the AS-IS ownership precondition and real cause); then the card
  predicate; then `!TopCard.IsFlipped`.
- **Candidate pool** (:183-194): both players' `GetFieldPermanents()` — battle AND breeding area, via the
  mirror `GameContext(context).Players` (AS-IS `gameContext.Players`), NOT the legacy battle-area-only scan.
- **active()** (:181-217) verbatim incl. the combination pre-check: `ParameterComparer.Enumerate(pool, max)`
  has no mirror — local k-combination enumerator, short-circuited when no `canEndSelectCondition` exists
  (every max-combination trivially passes AS-IS CanEndSelect then).
- **Forced selection** (:366-399): `!canNoSelect && !canEndNotMax && pool.Count == maxCount` ⇒ ALL candidates
  selected with NO choice request (AS-IS fires EndSelect_RPC directly) — action-surface parity.
- **Choice**: batch request (min = 0 when canNoSelect OR canEndNotMax — AS-IS CanEndSelect allows ending at
  ANY count ≤ max incl. 0 with canEndNotMax, :224 — else exactly maxCount; `canEndSelectCondition` rides as
  the `SelectionValidator`); INCREMENTAL one-pick loop when `canTargetCondition_ByPreSelecetedList` is present
  (the AS-IS panel's per-pick re-filter, :439-450/:519-533). Skip ⇒ `_noSelect = true`.
- **Post-gate** `CanEndSelect(_targetPermanents)` (:748) re-checked verbatim, then per-selected
  `selectPermanentCoroutine` in selection order (:873-876), then the Mode batch (:949-1028) — a single
  Activate has a single Mode, so the AS-IS per-mode buckets each collapse to "all selected":
  - `Destroy`/`Bounce`/`PutLibraryBottom`/`PutLibraryTop` — the AS-IS carriers (`DestroyPermanentsClass`,
    `HandBounceClaass`, `Deck(Top|Bottom)BounceClass`) have NO mirror classes (their only mirror references
    are themselves masked-verbatim); routed through the class's own verified `Apply` → sink mutation kinds
    (Delete / ReturnToHand / ReturnToDeck*) in ONE flush = ONE AS-IS batch call (one delete batch id, one
    add-hand batch id — the D-1/F-1 batch semantics), with the centralised immunity/restriction gates.
  - `Tap` → mirror `SuspendPermanentsClass(list, causeId, isBlock:false).Tap()`; `UnTap` → mirror
    `IUnsuspendPermanents(list, causeId).Unsuspend()`; `Degenerate` → per-permanent mirror
    `IDegeneration(p, _degenerationCount, causeId).Degeneration()` — the 1:1 AS-IS carrier classes.
  - `PutSecurity*` — the AS-IS batch gate `_cardEffect.EffectSourceCard.Owner.CanAddSecurity(_cardEffect)`
    (:976/:987) via mirror `Player.CanAddSecurity(causeId)`, then the AddToSecurity mutations (ToBottom/FaceUp
    per `SetPlaceFaceUp`) in staging order — the sink allocates per-card add-security ids, preserving the
    AS-IS per-card sequential `IPutSecurityPermanent` resolution order.
  - `Attack` — the established queued effect-attack flow (`TryOpenAttack` → `EffectDrivenAttack.
    RequestQueuedChoices`), whose per-attacker re-check mirrors the AS-IS per-selected
    `if (selectedPermanent.CanAttack(_cardEffect))` (:1013). `SetDefenderCondition`'s Permanent-shape predicate
    is wrapped onto the id-shape option here. `SetCanNotSelectNotAttack` is unthreaded — RD-W4-6.
- **afterSelectPermanentCoroutine** always runs (:1033-1036), even when `active()` was false — verbatim.
- Cause threading: `_sourceEntityId` = `cardEffect.EffectSourceCard.InstanceId` (SetUp), so every staged
  mutation carries the AS-IS `hashtable {"CardEffect": _cardEffect}` cause.

### SelectCardEffect.Activate (AS-IS :332-1011)

- Guards verbatim: `_maxCount == 0 ⇒ _canNoSelect = () => true` (:366-369); `root == Security ⇒ SetIsSecurity`
  (:371-374).
- **RootCardList** (:229-275) verbatim: custom list, else ONLY Library/Trash/Security/Recollection(Lost) —
  every other root yields empty exactly as AS-IS (those callers always pass customRootCardList).
- **CanSelectCard** (:277-301) verbatim: hidden-zone (non-Library/Security/Custom) flip pass-through, the
  CardSource predicate, the `_allowFaceDown` exclusion.
- **active()** (:303-330) verbatim INCLUDING its side effects (Library ⇒ SetUseFaceDown; Security +
  canLookReverseCard ⇒ SetUseFaceDown) and the AS-IS rule that a non-empty Library/Security pool is always
  active regardless of matches.
- **Choice**: candidates = the WHOLE root pool with unselectable cards flagged (the AS-IS panel greys them);
  batch/incremental per `canTargetCondition_ByPreSelecetedList`; `canNoSelect()` evaluated at request time;
  max CLAMPED to the selectable count (the established substrate clamp — every real AS-IS caller pre-clamps
  with `Math.Min`, e.g. BT1_011). Pick indices into the pool list are recorded (`_slectedInexesInList`,
  AS-IS `SelectedIndex`) for `SetUpAfterSelectIndexCoroutine`.
- **Mode routing** (AS-IS :763-973):
  - `AddHand` (:765-784): per card `SetFace()` (mirror = clear the shared `isFlipped` instance flag, the
    established metadata round-trip); DigiEgg ⇒ LIBRARY BOTTOM (AS-IS AddLibraryBottomCards) via the sink;
    a digivolution-source card is DETACHED first (AS-IS `RemoveDigivolveRootEffect` ⇒ mirror
    `Permanent.RemoveCardSource`, the MIG4 AS-IS-anchored detach); the rest accumulate into `handCards`.
  - `Discard` (:786-813): **AS-IS whole-list quirk KEPT** — the hand branch discards ALL `_targetCards`
    whenever the scanned card is on hand (`_targetCards.Map(new IDiscardHand(...))` verbatim); later
    iterations fall through to the no-op trash move exactly as AS-IS. Carriers: mirror `IDiscardHands` /
    `ITrashLinkCards` / `ITrashDigivolutionCards` (1:1 classes, `ResolvePermanentOfThisCard` for the
    PermanentView→Permanent bridge); the final else = sink TrashCard (AS-IS `AddTrashCard`).
  - `PlayForFree` (:815-823): the W3 AS-IS-signature `CardEffectCommons.PlayPermanentCards(_targetCards,
    _cardEffect, payCost:false, isTapped:false, root:_root, activateETB:true)` — exact AS-IS argument list.
  - `PlayForCost` (:826-962): `PlayPermanentCards(..., payCost:true, root: Root.Hand, ...)` — the AS-IS
    root-Hand quirk (:950) kept. A non-null reduce/fixed-cost tuple THROWS (STOP, RD-W4-1).
  - `Custom` (:964-972): per-card `selectCardCoroutine`.
- **handCards flush** (:975-978): ONE sink flush of ReturnToHand mutations = the single AS-IS
  `AddHandCards(handCards, false, _cardEffect)` call = ONE add-hand batch id (F1-Tier1 OnAddHand semantics).
- **after-coroutines** (:998-1006) always run: `afterSelectCardCoroutine(_targetCards)` then
  `afterSelectIndexCoroutine(_slectedInexesInList)` — verbatim order.

## 4. Small-gap resolutions

- **`CardSource.BaseENGCardNameFromEntity`** — `Definition?.Name ?? ""` (the PRINTED base-entity name,
  UNtransformed — AS-IS `_cEntity_Base.CardName_ENG`, CardSource.cs:1359 — unlike `CardNames`, which folds
  ChangeBaseCardName/ChangeCardNames). Resolves BT1_092/BT1_094.
- **`Player.AddMemory(int, ICardEffect)`** (AS-IS Player.cs:1082-1123) — full port, not just the happy path:
  zero no-op; gain (≥1, cardEffect≠null) gated by the newly-ported **`Player.CanAddMemory(ICardEffect)`**
  (AS-IS :1030-1075 — the `MemoryForPlayer >= 10` cap + the ICannotAddMemoryEffect scan, whose mirror carrier
  is the `CannotAddMemoryKey` player-scope continuous binding; the scan reproduces the sink's private
  `IsPlayerRestricted` gate incl. the condition and causing-effect-predicate halves, but keyed on THIS gaining
  player as AS-IS does — the sink keys on the mutation source's owner — and fed the TRUE causing card); then
  the gauge moves in THIS player's favor with the ±10 clamp. The headless gauge is turn-player-relative
  (`HeadlessMainPhaseFlow` re-signs `|memory|` at turn switch), so "this player's favor" = `+plus` for the
  turn player / `−plus` otherwise — the same mapping `CardEffectCommons.MemoryForPlayer` and
  `AceOverflowGate.MemoryDelta` use; **`Player.MemoryForPlayer`** added as the AS-IS accessor. Applied
  directly on the memory controller (the AS-IS body writes the gauge directly too); the sink's `AddMemoryKind`
  was deliberately NOT used — its amount is applied raw (turn-player-relative convention) so a non-turn-player
  perspective flip would invert, and its gain gate keys on the wrong player for this AS-IS surface.
  Retires design item MIG5-CANADDMEMORY.
- **`HeadlessPlayerId.AddMemory` extension** (`PlayerIdAsIsExtensions`, same file): the actual card idiom is
  `card.Owner.AddMemory(-5, activateClass)` (BT1_114) and mirror `CardSource.Owner` is a bare
  `HeadlessPlayerId` — the extension resolves the context from the causing effect's source card (or the
  ambient match scope, the `GManager.instance` source) and delegates to the mirror `Player.AddMemory`.
  Resolves BT1_114.

## 5. Verification

Declaration gate (batch contract):

```
dotnet build src/HeadlessDCGO.Engine/HeadlessDCGO.Engine.csproj -clp:ErrorsOnly 2>&1 \
  | grep -oE 'error CS[0-9]+' | sort | uniq -c
     59 error CS0246        # identical to baseline; no CS0104/CS0101/CS0111/CS0121
```

(One intermediate CS0104 pair — `SkillInfo` ambiguous between the mirror `CardEffectCommons.SkillInfo` and
`Headless.Effects.SkillInfo` — fixed by namespace-qualifying the AS-IS mirror type. One resolution finding for
future batches: inside `namespace …Assets.Scripts.Script`, the bare identifier `CardEffectCommons` binds to the
SIBLING NAMESPACE, not the static class — the `using Commons = …CardEffectCommons.CardEffectCommons;` alias is
the established fix, per AutoProcessing.cs.)

Shim body-check (per rebuild_bridge_w3_notes.md's method — temporary empty `IActivatedCardEffect`, full
rebuild, filter, delete, reconfirm):

- With the shim: **657** total body errors project-wide (old masked-verbatim corpus; ~700 in W3, DOWN because
  this batch resolves real call sites).
- **W4-edited files contribute ZERO body errors**: SelectPermanentEffect.cs, SelectCardEffect.cs, GManager.cs,
  Player.cs — 0 error lines each; CardSource.cs shows exactly its 14 PRE-EXISTING masked-verbatim errors
  (ChangeBaseCardColorClass keys etc.) — verified identical (same errors, line numbers shifted by the added
  doc comment) by re-running the shim build with the W4 diff stashed.
- **The 6 unblocked cards now bind CLEAN under the shim**: BT1_011, BT1_017, BT1_023, BT1_092, BT1_094
  (selection machinery + BaseENGCardNameFromEntity) and BT1_114 (AddMemory) — zero error lines, i.e. every
  unresolved call from the P5 log's 2026-07-13 findings 1-3 now resolves against real mirror surfaces.
- Shim deleted: baseline re-confirmed at `59 error CS0246`.

## Design items (RD-W4-n)

- **RD-W4-1** — `SelectCardEffect` Mode.PlayForCost with `SetReducedCostTuple`/`SetFixedCostTuple`: the AS-IS
  halves register a transient `ChangeCostClass` on `Player.UntilCalculateFixedCostEffect` for the duration of
  the play; the mirror Player has no such list and the play-cost pipeline (ContinuousModifierGate) has no
  transient registration hook. Non-null tuple THROWS (STOP); pre-computing a discounted cost would bypass the
  AS-IS isCheckAvailability/isChangePayingCost semantics (no-simplification rule).
- **RD-W4-2** (== RD-W3-1) — the incremental byPreSelectedList loop cannot reproduce the AS-IS panel's
  "UN-pick an already-picked card at maxCount to satisfy canEndSelectCondition" corner (unreachable for the
  prefix-monotone AS-IS caller conditions).
- **RD-W4-3** — `GManager.GetComponent<T>()` supported set = SelectPermanentEffect/SelectCardEffect; all other
  AS-IS components (OptionalSkill — no mirror type at all; SelectAssemblyClass/SelectCountEffect — mirror
  types exist but with non-component AS-IS shapes; Effects/SelectDigiXrosClass/SelectDNACondition — no mirror)
  throw until a bridge batch lands them.
- **RD-W4-4** — the AS-IS `IsSelecting` / `IsSecurityLooking` save-restore polling flags have no live mirror
  surface (the headless model replaced polling with the choice-pause mechanism; pre-existing
  MIG6-SECURITYLOOKING). `SetIsSecurity`/`_isSecurity` carry the AS-IS state shape only.
- **RD-W4-5** — when canNoSelect AND canEndNotMax both hold, an empty selection is reported as the skip
  (`_noSelect = true`); the AS-IS back-button-vs-EndSelect distinction at zero picks is unobservable
  downstream (both paths process zero targets).
- **RD-W4-6** — Mode.Attack: `SetCanNotSelectNotAttack` (mandatory attack when `!_canNoSelect`) has no thread
  into the deferred effect-attack choice (the pre-existing TryOpenAttack surface has the same behaviour);
  latent — zero callers in the bridged corpus.
- **RD-W4-7** — SelectCardEffect panel ORDERING: AS-IS sorts Library/Trash pools with `DeckData.
  SortedCardsList` then partitions matching-first (:444-478) before presenting; no DeckData mirror exists, so
  candidates (and therefore `SelectedIndex` values) follow the natural zone order. The candidate SET and the
  index semantics (index into the presented list) are preserved; consumers of `SetUpAfterSelectIndexCoroutine`
  (none in the bridged corpus) would see different index values for those two roots.
