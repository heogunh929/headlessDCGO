// Source: Assets/Scripts/CardEffect/ST1/Red/ST1_11.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, ST1 wave 2).
//
// 1:1 mirror of the original ST1_11: [Your Turn] this Digimon gets <Security Attack +X>, where
// X = (its digivolution source count) / 2. The original calls the generic
// ChangeSelfSAttackStaticEffect<Func<int>>; the headless overload takes the Func<int> directly so the
// explicit type argument is dropped.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class ST1_11 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            int count()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    return card.PermanentOfThisCard().DigivolutionCards.Count / 2;
                }

                return 0;
            }

            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (count() >= 1)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(
                changeValue: () => count(),
                isInheritedEffect: false,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}
