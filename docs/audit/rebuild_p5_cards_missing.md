# P5 card re-port — members referenced verbatim, dispatch status unconfirmed

Cards re-ported this batch: BT1_001, BT1_010, BT1_011, BT1_012, BT1_017, BT1_023, BT1_025, BT1_092, BT1_094,
BT1_114 (BT1/Red). All AS-IS members referenced by these cards' `CardEffects` bodies already exist on the
mirror (`CardEffectCommons`, `ActivatedEffect`/`IEffectBody` family, `DisableEffectClass`) — nothing was
stubbed or simplified. One item is flagged below because its END-TO-END dispatch wiring in the CURRENT headless
resolution path is unconfirmed (the referenced member compiles and is structurally verbatim; whether it is
actually CONSULTED at runtime for cards using the newer `ActivatedEffect` substrate is a separate question).

## BT1_025 — `DisableEffectClass` ("Ignore Security Effect") dispatch-wiring gap

AS-IS BT1_025 has a second, independent `timing == None` effect: a `DisableEffectClass` registering
`InvalidateCondition(ICardEffect cardEffect)` = "while this Digimon is the attacker on its owner's turn,
negate any Option-card-sourced `[Security]` effect" (`EffectSourceCard.IsOption && IsSecurityEffect &&
AttackingPermanent == PermanentOfThisCard()`).

Ported verbatim using the existing mirror members: `DisableEffectClass`, `SetUpDisableEffectClass`,
`ICardEffect.EffectSourceCard`, `ICardEffect.IsSecurityEffect`, `CardSource.IsOption`,
`card.Context.AttackController.Current.AttackerId` (mirror of `attackProcess.AttackingPermanent`).

**Wiring caveat**: `CheckEffectDisabledClass.isDisabled(this)` is consulted inside the OLD/legacy
`ICardEffect.cs` `CanUse` composite (ICardEffect.cs:840) — i.e. any effect still expressed as a literal
`ActivateClass`/`ICardEffect` subclass correctly asks "am I disabled?" before firing. However, the [Security]
Option-skill effects most already-ported Option cards use (including this repo's own
`AddActivateMainOptionSecurityEffect` / `ReuseMainOptionEffect` reuse-from-Main path, and the general uniform
`ActivatedEffect` substrate) are `IActivatedCardEffect`/`ActivatedEffect`-based, NOT `ICardEffect` subclasses —
`ActivatedEffect.CanResolve` does not call `CheckEffectDisabledClass` anywhere. `Headless/Runtime/
EffectInvalidation.cs` (`EffectInvalidation.IsEffectsDisabled`) is a DIFFERENT, blunter mechanism ("disable
ALL of a card's effects", continuous marker scan) — it does not implement BT1_025's targeted "invalidate only
Option-sourced [Security] effects while attacking" semantics either.

Net effect: BT1_025's "Ignore Security Effect" is structurally ported (compiles, AS-IS-shaped, no
simplification), but is likely INERT against the opponent's [Security] Option effects as currently resolved by
this engine (a pre-existing cross-cutting gap in the `ActivatedEffect` substrate, not something a single-card
port can fix — engine files are out of scope for this task). Flagging for the F-1-style bridge work that would
teach `ActivatedEffectResolver`/`ActivatedEffect.CanResolve` to also consult `CheckEffectDisabledClass` (or an
equivalent `IDisableCardEffect` scan) for uniform activated effects.

## 2026-07-13 — TRUE AS-IS-verbatim RE-RE-PORT of the same 10 cards (this pass)

The entry above described the PREVIOUS pass's port, which (despite this file's header claim) actually landed the
`[When ...]`/`[On Play]`/`[Main]` bodies on the OLD `ActivatedEffect`/`IEffectBody` model, not literal AS-IS
`ActivateClass` structure — that was the mistake this pass was asked to correct (see the task brief: "do NOT
fall back to ActivatedEffect/IEffectBody or any old-model primitive"). All 10 cards were rewritten to the AS-IS
inline `new ActivateClass()` + `SetUpICardEffect`/`SetUpActivateClass` + local-function structure (or genuine
AS-IS `CardEffectFactory.*` calls, e.g. BT1_114's `ChangeSelfSAttackStaticEffect`/`ChangeSelfDPStaticEffect`,
left unchanged).

**Fully resolved, zero body errors** (verified via the shim-declared `IActivatedCardEffect` check,
docs/audit/rebuild_bridge_w3_notes.md's method): BT1_001, BT1_010, BT1_012, BT1_025 (`[When Digivolving]` half
rewritten; the `[All Turns]` `DisableEffectClass` half was already correct AS-IS and is unchanged, EXCEPT one
pre-existing bug fixed in passing — see below).

**Pre-existing bug fixed** (BT1_025, `[All Turns]` `DisableEffectClass` half, untouched by the previous
description above but surfaced by this pass's shim check): `ICardEffect.ResolvePermanentOfThisCard(card)`
returns the mirror `Permanent` type, which exposes `.InstanceId` (the top card's id — see `Permanent.cs`'s ctor:
`TopCard => new(_context, InstanceId, OwnerId)`), NOT `.TopInstanceId` (that member belongs to the DIFFERENT
`PermanentView` type `CardSource.PermanentOfThisCard()` itself returns). The previous pass wrote
`...ResolvePermanentOfThisCard(card).TopInstanceId`, a CS1061 masked by the intentionally-RED baseline. Fixed to
`.InstanceId`. No logic change (same value — the top card's instance id — via the correct member name).

**New unresolved-member findings** (kept AS-IS-verbatim per the no-fallback rule; NOT routed through
`ActivatedEffect`/`IEffectBody`; each site commented in-file):

1. **`Player.AddMemory(int, ICardEffect)` — no mirror bridge** (BT1_114, `[When Attacking]` "Lose 5 memory").
   AS-IS calls `card.Owner.AddMemory(-5, activateClass)` where AS-IS `Player` is a real object with this
   instance coroutine (Player.cs:1082). The mirror `CardSource.Owner` is a bare `HeadlessPlayerId` (no `Player`
   handle at the call site), and the mirror `Player` class (Assets/Scripts/Script/Player.cs, MIG5) exposes only
   `CanAddMemory`-adjacent gates, not `AddMemory` itself (design item MIG5-CANADDMEMORY only covers the gate).
   None of the W1-W3 bridge batches added this — they covered only `CardEffectCommons.*` mutation coroutines,
   and `Player.AddMemory` is a `Player` instance method, out of that scope. CS1061 at BT1_114.cs.

2. **`GManager.GetComponent<T>()` / AS-IS-signature `SelectPermanentEffect`/`SelectCardEffect` `SetUp(...)` +
   `Activate()` — no mirror bridge at all** (BT1_011, BT1_017, BT1_023, BT1_092's buff-select half, BT1_094).
   The AS-IS pattern `GManager.instance.GetComponent<SelectPermanentEffect>(); selectPermanentEffect.SetUp(...
   11 AS-IS params incl. canTargetCondition_ByPreSelecetedList/canEndSelectCondition/selectPermanentCoroutine/
   afterSelectPermanentCoroutine/cardEffect...); yield return ...StartCoroutine(selectPermanentEffect.Activate())`
   (and its `SelectCardEffect` sibling for BT1_011) is NOT reachable on the mirror:
   - `GManager` (Assets/Scripts/Script/GManager.cs) declares no `GetComponent<T>()` at all — the file's own
     header states "UI/Photon/component members of the original GManager are NOT ported".
   - The mirror `SelectPermanentEffect`/`SelectCardEffect` (Assets/Scripts/Script/SelectPermanentEffect.cs /
     SelectCardEffect.cs) are DIFFERENT, simplified types: `SetUp` takes far fewer params (no
     `canTargetCondition_ByPreSelecetedList`, `canEndSelectCondition`, `selectPermanentCoroutine`/
     `selectCardCoroutine`'s per-target callback wiring, `afterSelectPermanentCoroutine`/
     `afterSelectCardCoroutine`, `cardEffect`, `isShowOpponent`, `customRootCardList`, `canLookReverseCard`), and
     neither type has an `Activate()` method at all — they only build a `ChoiceRequest`/mutation list
     (`BuildRequest`/`Apply`); the actual resolution loop lives entirely in the `ActivatedEffect`/`IEffectBody`/
     `ChoiceRequest` resolver machinery (WindowResolver etc.), which this task's no-fallback rule forbids
     reaching for from a verbatim card body.
   - None of the W1/W2/W3 bridge batches (docs/audit/rebuild_bridge_w{1,2,3}_notes.md) touched this selection
     machinery — their scope was `CardEffectCommons.*` mutation coroutines exclusively.
   Kept in AS-IS shape (only the standard `IEnumerator`->`Task` / `StartCoroutine`->`await` substrate
   translation applied); CS1061 (`GetComponent`, `Activate`)/CS1739/CS1501 at each site. This is the single
   largest remaining gap for a genuinely verbatim card-effect corpus: an AS-IS-signature bridge for
   `SelectPermanentEffect`/`SelectCardEffect`'s `SetUp(...).Activate()` pattern (parallel to the W1-W3
   `CardEffectCommons.*` bridge work) would very likely unblock a large fraction of the remaining BT1/Red (and
   later sets') cards, since this exact pattern recurs constantly across the AS-IS corpus.

3. **`CardSource.BaseENGCardNameFromEntity` — no mirror member** (BT1_092, BT1_094, both `[Main]`'s
   `SetUpICardEffect(card.BaseENGCardNameFromEntity, ...)` call). AS-IS `CardSource.BaseENGCardNameFromEntity`
   (CardSource.cs:1359) = `_cEntity_Base.CardName_ENG`. The mirror `CardSource` has `CardNames`/`EqualsCardName`/
   `ContainsCardName` but no single "base ENG name" accessor. Used only as the informational `effectName`
   argument to `SetUpICardEffect` (an id/display string; `EffectName` is read by `ICardEffect.IsSameEffect`'s
   `HashString`/`RootCardEffect` comparison, not by this value directly, so the omission is unlikely to change
   dispatch, but was NOT silently patched with a literal string per the no-simplification rule). CS1061 at each
   site.

**Shim-check result** (per docs/audit/rebuild_bridge_w3_notes.md's method — temporary `IActivatedCardEffect`
shim added, full rebuild, then removed): with the shim, the 10 files contribute exactly 6 error lines beyond the
~700-line old-corpus noise, ALL matching the 3 findings above verbatim (1× `Player.AddMemory` in BT1_114; 2×
`CardSource.BaseENGCardNameFromEntity` in BT1_092/BT1_094; 4× each of `GetComponent`/`SetUp` CS1739/
`SetUpCustomMessage` CS1501/`Activate` CS1061 across BT1_011/017/023/092/094, i.e. 3-4 lines per card depending
on whether `SetUpCustomMessage` is called) — no unexplained/accidental body errors. Baseline re-confirmed at
`59 error CS0246` after the shim was deleted.

## 2026-07-14 — TRUE AS-IS-verbatim re-port, BATCH 2 (BT1 remaining, 27 cards)

Cards re-ported this batch (all rewritten from the previous, non-verbatim old-model mirror to literal AS-IS
inline `new ActivateClass()` + `SetUpICardEffect`/`SetUpActivateClass` + local-function structure): Blue
BT1_029/030/035/036/040/041/043/096/097/099/115; Yellow BT1_048/055/062/104/106; Green
BT1_007/067/070/074/076/077/108/110/111/112/113.

**BT1_104 note**: the PRIOR pass had STOPped this card entirely ("no composed primitive" for the AddSkillClass
grant). Re-ported this pass because the literal AS-IS structure (inline `AddSkillClass.SetUpAddSkillClass` +
`CardEffectCommons.AddEffectToPlayer(effectDuration, card, cardEffect, timing)`, the exact bridge for AS-IS
`GiveEffectToPermanentOrPlayer.cs:57`) is NOT the declarative old-model factory route the prior STOP was
about — going fully verbatim resolved what looked like a primitive gap under the old model.

**New unresolved-member findings** (kept AS-IS-verbatim per the no-fallback rule; each site commented in-file;
shim-verified — see below):

1. **`HeadlessPlayerId.CanAddMemory(ICardEffect)` — no mirror extension** (BT1_030, BT1_035, BT1_041, BT1_076,
   BT1_077, all via AS-IS `card.Owner.CanAddMemory(activateClass)`). Bridge W4 added a `HeadlessPlayerId.
   AddMemory(int, ICardEffect)` extension (`PlayerIdAsIsExtensions`) but NOT a matching `CanAddMemory`
   extension — `Player.CanAddMemory(ICardEffect)` (Player.cs) exists only as a `Player`-instance member,
   unreachable from the bare-id `CardSource.Owner` call site. CS1061 at each site (5 total).

2. **`CardEffectCommons.customPermanentMessageArray_ChangeDP(int, int)` — no mirror member** (BT1_055,
   BT1_096, BT1_104, all via `selectPermanentEffect.SetUpCustomMessage(customMessageArray:
   CardEffectCommons.customPermanentMessageArray_ChangeDP(...))`). AS-IS defines this (and its
   `customPermanentMessageArrayTemplate`/`_ChangeOriginDP`/`_ChangeSAttack` siblings) in
   `CardEffectCommons/CustomMessage.cs` as a pure string-template helper (no gameplay state) that the W4
   `SetUpCustomMessage(string, string, string[]?)` overload is shaped to accept, but the helper method itself
   was never ported. Purely informational (the ChoiceRequest prompt text); CS0117 at each site (3 total).

3. **`GManager.userSelectionManager` / `SelectionElement<T>` — no mirror surface at all** (BT1_111's [Main],
   the "Suspend 1 vs Suspend 2" mode pick: `GManager.instance.userSelectionManager.SetBoolSelection(...)` /
   `.SetBool(...)` / `.WaitForEndSelect()` / `.SelectedBoolValue`). Unlike the W4-bridged
   `SelectPermanentEffect`/`SelectCardEffect`, the mirror `GManager` has no `userSelectionManager` field and NO
   mirror `UserSelectionManager`/`SelectionElement<T>` type exists anywhere in the codebase — this is a
   genuine interactive gameplay CHOICE ("which of two effects do you want?"), not cosmetic UI, so per the
   no-simplification rule it is kept in AS-IS shape rather than auto-picking a branch or dropping the choice.
   **Note for the build-verification gate**: since `SelectionElement<T>` is a wholly undeclared TYPE (not just
   a missing member on an existing type), referencing it produces **new CS0246 errors even in the PLAIN
   (non-shim) build** — 4 occurrences in BT1_111.cs, on top of the pre-existing 59. This is the first
   verbatim-card gap in the P5/W-series corpus that moves the plain-build CS0246 count (all prior "kept
   verbatim" gaps — `GetComponent<T>`/`BaseENGCardNameFromEntity`/`Player.AddMemory` — referenced EXISTING
   mirror types with wrong member shapes, i.e. CS1061/CS1739/CS1501, which stay masked under the
   declaration-phase-error suppression the W3 notes describe). `userSelectionManager` itself is CS1061 (5
   occurrences, `GManager` exists, just lacks the field).

**Shim-check result** (same method): with the shim, this batch's 27 files contribute exactly 16 error lines
beyond the ~700-line old-corpus noise — 5× `HeadlessPlayerId.CanAddMemory` (finding 1), 3×
`customPermanentMessageArray_ChangeDP` (finding 2), 4× `SelectionElement<>` CS0246 + 4× `userSelectionManager`
CS1061 (finding 3) — no unexplained/accidental body errors in any of the 27 files. Baseline re-confirmed at
`59 error CS0246` after the shim was deleted, WITH ONE CAVEAT: the plain (non-shim) baseline build is also
`59 error CS0246` exactly as before — the 4 new `SelectionElement<>` CS0246s do NOT appear in the plain build
either, because they are themselves masked by the SAME declaration-phase-error suppression the W3 notes
describe (the missing `IActivatedCardEffect` blocks body binding project-wide, and `SelectionElement<>` is
referenced only inside a method body). So the plain-build "59 CS0246, unchanged" gate holds, but is (as W3
already flagged) a signatures-only check — the shim pass is what actually proves these 4 sites' declaration
type is absent, same rigor as every other finding here.
