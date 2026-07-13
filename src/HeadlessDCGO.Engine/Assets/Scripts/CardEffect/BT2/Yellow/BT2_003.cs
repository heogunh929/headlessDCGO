// Source: Assets/Scripts/CardEffect/BT2/Blue/BT2_003.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect.
//
// 1:1 mirror of the original BT2_003: inherited [Opponent's Turn] while this card is
// suspended on the battle area, your Security Digimon gains DP +1000.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_003 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool CardCondition(CardSource cardSource)
            {
                return cardSource.Owner == card.Owner;
            }

            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOpponentTurn(card))
                    {
                        if (CardEffectCommons.IsSuspended(card, card.InstanceId))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect(
                cardCondition: CardCondition,
                changeValue: 1000,
                isInheritedEffect: true,
                card: card,
                condition: Condition,
                effectName: "Your Security Digimon gains DP +1000"));
        }

        return cardEffects;
    }
}