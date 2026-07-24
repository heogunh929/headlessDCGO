// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_070.cs
// AS-IS has two branches:
//   [None] <Blocker> (not inherited).  -> BlockerSelfStaticEffect (ported, verbatim factory match).
//   [On Deletion] Reveal 5 cards from the top of your deck. You may play 1 level 6 Digimon card with
//   [Etemon] in its name among them without paying its memory cost. Place the remaining cards at the
//   bottom of your deck in any order.
//
// STOP: the [On Deletion] branch needs the exact same "reveal top N -> select 1 matching -> play free ->
// remainder to bottom" primitive that BT3_063 lacks — see that file's header for the full grep trail
// (SimplifiedRevealAndSelectEffect / RevealMultiSelectEffect route selections to Hand/DeckTop/DeckBottom/
// Trash or merely record a Custom pick with no wired play-as-permanent follow-up; ActivatedSelectAndPlayEffect
// plays from an entire zone, not a top-N reveal, and has no remainder-to-bottom step). No factory composes
// it. Per rule 4 this is a primitive gap, out of scope for a single-card porting pass. — Sonnet

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_070 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        // STOP: [On Deletion] "Reveal 5 cards from the top of your deck. You may play 1 level 6 Digimon
        // card with [Etemon] in its name among them without paying its memory cost. Place the remaining
        // cards at the bottom of your deck in any order." — needs the reveal -> select -> play-free ->
        // remainder-to-bottom primitive that does not exist yet (see file header / BT3_063).
        // if (timing == EffectTiming.OnDestroyedAnyone) { ... }

        return cardEffects;
    }
}
