// Source: Assets/Scripts/CardEffect/BT2/BT2_031.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect.
//
// 1:1 mirror of the original BT2_031: [Your Turn] while there is >= 1 opponent Digimon
// with no digivolution cards on the battle area, DP +1000 and S.Attack +1 (non-inherited).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_031 : CEntity_Effect
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
                        if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, id => CardEffectCommons.IsBattleAreaDigimon(card, id) && CardEffectCommons.HasNoDigivolutionCards(card, id)))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(changeValue: 1000, isInheritedEffect: false, card: card, condition: Condition));
        }

        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, id => CardEffectCommons.IsBattleAreaDigimon(card, id) && CardEffectCommons.HasNoDigivolutionCards(card, id)))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card, condition: Condition));
        }

        return cardEffects;
    }
}