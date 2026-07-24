// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_063.cs
// AS-IS (single branch, OnDestroyedAnyone / [On Deletion]):
//   [On Deletion] Reveal the top 3 cards of your deck. You may play 1 [Chuumon] among them without paying
//   its memory cost. Place the remaining cards at the bottom of your deck in any order.
//   CanUseCondition = CanTriggerOnDeletion [ported: CardEffectCommons.CanTriggerOnDeletion, exists].
//   CanActivateCondition = CanActivateOnDeletion [ported: exists] && owner's library has >= 1 card.
//
// STOP (genuine primitive gap, grepped 2x+ per rule 4): the body needs "reveal the top N cards of the
// owner's library, let the owner select up to 1 REVEALED card matching a predicate, PLAY that selection
// onto the battle area for free (ignoring its memory cost), then send every other revealed (unselected)
// card to the bottom of the deck." Grepped the reveal-and-select primitives in
// Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs / Headless/Runtime/RevealAndSelect.cs:
// SimplifiedRevealAndSelectEffect (CardEffectFactory.SimplifiedRevealDeckTopCardsAndSelect) and
// RevealMultiSelectEffect (CardEffectFactory.RevealDeckTopCardsAndSelect) only route a selected card to
// RevealDestination.Hand / DeckTop / DeckBottom / Trash — or, with RevealDestination.Custom, merely RECORD
// the pick (RevealFlowState.CustomSelections) for a later imperative follow-up. No destination plays the
// card as a free permanent, and there is no wiring for a card's declarative CardEffects() list to feed one
// activated effect's CustomSelections output into another activated effect's input: each
// IActivatedCardEffect is resolved independently by ActivatedEffectResolver.ResolveListAsync's fixed
// switch (ActivatedEffectResolver.cs), which recognises no "then play the custom-selected reveal pick"
// case. The zone-scoped play helper (ActivatedSelectAndPlayEffect / CardEffectFactory.
// SelectAndPlayFromZoneEffect) plays from an ENTIRE ChoiceZone (Hand/Trash) matching a predicate — it has
// no top-N-of-library reveal input and no remainder-to-bottom step, so substituting it would silently
// search the WHOLE deck instead of only the top 3 and drop the "remaining cards to the bottom" requirement
// (a materially different, stronger effect — forbidden by fidelity-over-coverage). No factory composes
// "reveal top N -> select 1 matching -> play free -> remainder to the bottom." Per rule 4 this is a
// primitive gap (new engine-layer IActivatedCardEffect work), out of scope for a single-card porting pass.
// No cardEffects registered (OnDestroyedAnyone is the only branch the AS-IS declares). — Sonnet

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_063 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // STOP: [On Deletion] "Reveal the top 3 cards of your deck. You may play 1 [Chuumon] among them
        // without paying its memory cost. Place the remaining cards at the bottom of your deck in any
        // order." — needs a "reveal top N -> select 1 matching -> play free -> remainder to bottom"
        // primitive that does not exist yet (see file header).
        // if (timing == EffectTiming.OnDestroyedAnyone) { ... }

        return cardEffects;
    }
}
