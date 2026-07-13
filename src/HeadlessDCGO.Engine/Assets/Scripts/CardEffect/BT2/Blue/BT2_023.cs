// Source: Assets/Scripts/CardEffect/BT2/Blue/BT2_023.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect.
//
// Play cost reduced by the number of opponent Digimons with no digivolution cards in battle.
// Dynamic reduction via BeforePayCostReductionEffect (Func<int> overload).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public sealed class BT2_023 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            int Count() => CardEffectCommons.MatchConditionPermanentCount(
                card,
                id => CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) && CardEffectCommons.HasNoDigivolutionCards(card, id));

            bool Condition() => Count() >= 1;

            cardEffects.Add(CardEffectFactory.BeforePayCostReductionEffect(card, (Func<int>)Count, Condition, "Play Cost -"));
        }

        return cardEffects;
    }
}