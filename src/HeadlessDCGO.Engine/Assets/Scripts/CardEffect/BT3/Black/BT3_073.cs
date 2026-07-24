// Source: Assets/Scripts/CardEffect/BT3/Black/BT3_073.cs
// AS-IS has two branches:
//   [None] <Reboot> (not inherited).  -> RebootSelfStaticEffect (ported, verbatim factory match).
//   [When Digivolving] Reveal 1 card from the top of your deck for each Digimon your opponent has in play.
//   You may play 1 black or red Digimon card with a level of 5 or less among them without paying its
//   memory cost. Place the remaining cards at the bottom of your deck in any order.
//
// STOP: the [When Digivolving] branch needs the same "reveal top N -> select 1 matching -> play free ->
// remainder to bottom" primitive that BT3_063 lacks (see that file's header for the full grep trail); here
// N is additionally DYNAMIC (opponent's current battle-area Digimon count), which the existing
// SimplifiedRevealDeckTopCardsAndSelect / RevealDeckTopCardsAndSelect factories could in principle accept
// (revealCount is a plain int the card computes before calling), but the underlying gap is the same
// missing "play the selection free" destination — no factory composes it regardless. Per rule 4 this is a
// primitive gap, out of scope for a single-card porting pass. — Sonnet

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Black;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT3_073 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.RebootSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        // STOP: [When Digivolving] "Reveal 1 card from the top of your deck for each Digimon your opponent
        // has in play. You may play 1 black or red Digimon card with a level of 5 or less among them
        // without paying its memory cost. Place the remaining cards at the bottom of your deck in any
        // order." — needs the reveal -> select -> play-free -> remainder-to-bottom primitive that does not
        // exist yet (see file header / BT3_063).
        // if (timing == EffectTiming.OnEnterFieldAnyone) { ... }

        return cardEffects;
    }
}
