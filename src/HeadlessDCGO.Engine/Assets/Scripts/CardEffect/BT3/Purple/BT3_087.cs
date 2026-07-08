// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_087.cs
// STOP: [When Attacking] You may pay 3 memory to play 1 [MaloMyotismon] from your trash without paying its
// memory cost. Then, delete this Digimon.
//
// Same composite shape as BT3_086 (trash-sourced instead of hand-sourced): ONE optional activation whose
// body is (1) MANDATORY AddMemory(-3), (2) an OPTIONAL select-1-and-play-cost-free step, (3) an
// UNCONDITIONAL self-destroy that must fire even when step 2 is declined. See BT3_086's file header for
// the full reasoning — no headless IEffectBody/factory composes "pay cost -> optional select-play ->
// unconditional self-destroy" as one atomic body, and independent multi-effect registration cannot
// express the unconditional-finisher-after-an-optional-step ordering (nor the shared pre-flush
// sequencing constraint of ActivatedEffectResolver). Per the primitive-gap rule (no new primitives, no
// engine edits, no throw), this card is left unregistered.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_087 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        // STOP: [When Attacking] pay 3 memory (may) -> play 1 [MaloMyotismon] from trash (optional) ->
        // unconditionally delete this Digimon — see file-header STOP note (same composite gap as
        // BT3_086). Not registered.
        return new List<ICardEffect>();
    }
}
