// Source: Assets/Scripts/CardEffect/BT1/Red/BT1_004.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect.
//
// 1:1 mirror of the original BT1_004: inherited [Your Turn] while there are >= 2 opponent
// digimons with no digivolution cards on the battle area, DP +2000.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_004 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.MatchConditionPermanentCount(card, id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) && CardEffectCommons.HasNoDigivolutionCards(card, id)) >= 2)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(changeValue: 2000, isInheritedEffect: true, card: card, condition: Condition));
        }

        return cardEffects;
    }
}