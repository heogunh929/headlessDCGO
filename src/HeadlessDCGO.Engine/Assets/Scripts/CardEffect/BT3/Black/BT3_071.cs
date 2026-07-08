// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_071.cs
// AS-IS has two branches:
//   [None] <Reboot> (not inherited).  -> RebootSelfStaticEffect (ported, verbatim factory match).
//   [When Digivolving] You may return 1 level 7 Digimon card with [Virus] in its attribute from your trash
//   to your hand.
//
// STOP (genuine primitive gap, grepped 2x+ per rule 4): the [When Digivolving] body needs "select up to 1
// of the owner's TRASH cards matching a predicate, and move the selection to the owner's hand." Grepped
// Assets/Scripts/Script/CardEffectCommons/CardPortingFramework.cs's IActivatedCardEffect catalog: the only
// zone-select-then-move helper is ActivatedSelectAndPlayEffect (CardEffectFactory.SelectAndPlayFromZoneEffect),
// which PLAYS the selection onto the battle area (PlayCardKind mutation, cost-free) — not "add to hand"
// (a materially different destination/mutation; substituting it would let this Tamer replay a Digimon
// instead of merely returning it to hand, forbidden by fidelity-over-coverage). The reveal-and-select
// primitives (SimplifiedRevealAndSelectEffect / RevealMultiSelectEffect) DO support a Hand destination, but
// they operate over a REVEALED top-N of the LIBRARY, not the owner's entire TRASH zone — not the same
// source pool. No factory composes "select from trash by predicate -> add to hand." Per rule 4 this is a
// primitive gap, out of scope for a single-card porting pass. — Sonnet

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_071 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        // STOP: [When Digivolving] "You may return 1 level 7 Digimon card with [Virus] in its attribute
        // from your trash to your hand." — needs a "select from trash by predicate -> add to hand"
        // primitive that does not exist yet (see file header).
        // if (timing == EffectTiming.OnEnterFieldAnyone) { ... }

        return cardEffects;
    }
}
