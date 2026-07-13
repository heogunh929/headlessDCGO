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
