// Source: Assets/Scripts/CardEffect/BT3/Purple/BT3_088.cs
// STOP (both blocks): BT3/Purple.
//
// [When Digivolving] Trigger <Draw 2>. (Draw 2 cards from your deck.) Then, trash 2 cards in your hand.
//   Same atomicity gap as BT3_006 (see that file's header for the full ActivatedEffectResolver
//   single-flush reasoning), aggravated here: the discard count itself is
//   Math.Min(2, POST-draw HandCards.Count), so both the discard CANDIDATE POOL and the discard COUNT
//   depend on state produced by the draw that has not yet flushed. No composite "draw N, then
//   interactively discard up to M from the resulting hand" IEffectBody exists; splitting into a
//   DrawCardsEffect + SelectAndTrashFromZoneEffect(ChoiceZone.Hand) pair resolved in the same
//   single-flush pass would read the PRE-draw hand for both the candidate pool and the maxCount fold — a
//   real behavioral divergence, not an approximation.
//
// [Your Turn][Once Per Turn] When you use an Option card, delete 1 of your opponent's level 3 Digimon.
//   AS-IS ActivateClass registers on EffectTiming.OnUseOption. The headless EffectTiming enum has no
//   OnUseOption member and no resolver call site (PlayCardAction / DigivolveAction / OptionActivateAction
//   / the ActivatedEffectResolver bridge) ever dispatches CardEffects(EffectTiming.OnUseOption, ...) —
//   TriggerTimings.OnUseOption is emitted as a raw engine trigger string (OptionActivateAction.cs) and
//   CardEffectCommons.CanTriggerWhenOwnerUseOption exists as a predicate helper, but there is no dispatch
//   surface to register this timing block against (same gap as BT3_091/BT3_096's OnUseOption blocks).
//
// Per the primitive-gap rule (no new primitives, no engine edits, no throw), this card is left
// unregistered.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_088 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        // STOP: [When Digivolving] Draw 2, then discard up to 2 from the resulting hand — see file-header
        // STOP note (draw-then-discard atomicity gap, worse than BT3_006 since the discard count itself
        // depends on the post-draw hand size). Not registered.

        // STOP: [Your Turn][Once Per Turn] When you use an Option card, delete 1 opponent level-3 Digimon
        // — see file-header STOP note (no EffectTiming.OnUseOption dispatch surface). Not registered.

        return new List<ICardEffect>();
    }
}
