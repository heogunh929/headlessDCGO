// Source: Assets/Scripts/CardEffect/BT1/BT1_008.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect (Phase 1, BT1 wave 1).
//
// 1:1 mirror of the original BT1_008: inherited [Your Turn] while 2 or more opponent's Digimon
// are suspended, DP +2000.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT1_008 : CEntity_Effect
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
                        if (CardEffectCommons.MatchConditionPermanentCount(card, id =>
                            CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) &&
                            CardEffectCommons.IsSuspended(card, id)) >= 2)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: 2000,
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }

        return cardEffects;
    }
}