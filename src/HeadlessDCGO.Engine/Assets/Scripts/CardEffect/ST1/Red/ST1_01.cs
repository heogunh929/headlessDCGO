// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_01.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 wave 1).
//
// 1:1 mirror of the original ST1_01: inherited [Your Turn] while this has >= 4 digivolution sources,
// DP +1000.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST1_01 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsOwnerTurn(card))
                {
                    if (card.PermanentOfThisCard().DigivolutionCards.Count >= 4)
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(changeValue: 1000, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
