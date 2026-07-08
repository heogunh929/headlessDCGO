// Source: Assets/Scripts/CardEffect/BT2/BT2_013.cs
// Decision: PORT
// Category: CardEffect
// Migration: Ported per-card effect. Digimon (inherited [When Attacking]).
//
// 1:1 mirror of BT2_013:
//   [When Attacking][Inherited] Delete 1 of your opponent's Digimon with 2000 DP or less.
//     -> SelectAndDestroyEffect (maxCount:1, canEndNotMax:false, DP threshold 2000)
// NOTE: isInheritedEffect(true) has no headless factory parameter — handled by the card framework.
// DP threshold mirrors card.Owner.MaxDP_DeleteEffect(2000) as flat 2000 (raise-effects later).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_013 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                if (CardEffectCommons.IsOpponentBattleAreaDigimon(card, id))
                {
                    if (CardEffectCommons.CurrentDp(card, id) <= CardEffectCommons.MaxDpDeleteThreshold(card, baseThreshold: 2000))
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[When Attacking] Delete 1 of your opponent's Digimon with 2000 DP or less."));
        }

        return cardEffects;
    }
}