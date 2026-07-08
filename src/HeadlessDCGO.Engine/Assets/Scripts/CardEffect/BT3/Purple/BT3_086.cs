// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_086.cs
// STOP: [When Attacking] You may pay 3 memory to play 1 [MaloMyotismon] from your hand without paying its
// memory cost. Then, delete this Digimon.
//
// AS-IS ActivateClass is ONE optional (isOptional:true) activation whose ActivateCoroutine, once chosen,
// runs THREE unconditional/ordered steps: (1) MANDATORY AddMemory(-3) cost, (2) an OPTIONAL
// (canNoSelect:true) select-1-and-play-cost-free step scoped to hand cards named "MaloMyotismon", (3) an
// UNCONDITIONAL self-destroy that fires regardless of whether step 2 selected anything.
//
// No headless IEffectBody/factory composes "pay a fixed memory cost, then run an optional interactive
// select-and-play step, then unconditionally destroy self" as one atomic body. The closest generic
// wrapper, SuspendSelfCostThenBody (ActivatedEffect.cs), only prepends a SELF-SUSPEND cost (not a memory
// cost) ahead of an inner body, and has no "run an unconditional step AFTER the inner body" hook either —
// it cannot express step 3 unconditionally following an OPTIONAL step 2. SelectBody's onEachSelected /
// onEachSelectedWithSink hooks only fire per SELECTED id, so if the (optional) selection is skipped, the
// mandatory self-destroy (step 3) would never run — silently changing "always delete this Digimon" into
// "delete this Digimon only if a target was chosen", a real behavioral divergence, not an approximation
// of ordering.
//
// Splitting into independently-registered ActivatedEffect entries under the same OnAllyAttack timing
// faces the same pre-flush-sequencing problem documented for BT3_006/BT3_088 (ActivatedEffectResolver
// materializes the whole CardEffects() list once and flushes the shared sink once at the end) plus the
// unconditional-step-3-after-optional-step-2 ordering problem above, which pure independent registration
// cannot express (an independent "always destroy self" effect would fire even when the memory-cost gate
// itself was never met, or would need to duplicate the cost gate exactly — and could not skip when the
// player declines the WHOLE optional activation, since "decline" is a property of the single combined
// activation, not of the per-effect CanActivate gate).
//
// No primitive composes this "pay cost -> optional select-play -> unconditional self-destroy" shape. Per
// the primitive-gap rule (no new primitives, no engine edits, no throw), this card is left unregistered.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_086 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        // STOP: [When Attacking] pay 3 memory (may) -> play 1 [MaloMyotismon] from hand (optional) ->
        // unconditionally delete this Digimon — see file-header STOP note (no pay-cost + optional-select
        // + unconditional-finisher composite primitive). Not registered.
        return new List<ICardEffect>();
    }
}
