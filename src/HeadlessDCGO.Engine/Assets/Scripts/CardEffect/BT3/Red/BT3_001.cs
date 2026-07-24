// 1:1 mirror of the original BT3_001 (BT3/Red).
//   [When Attacking] Delete 1 of your opponent's Digimon with 1000 DP or less.
//   -> SelectAndDestroyEffect (OnAllyAttack, mandatory pick of 1, DP gate via MaxDpDeleteThreshold — the
//   AS-IS card.Owner.MaxDP_DeleteEffect(1000, activateClass) raise-able threshold, same shape as ST1_15).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_001 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.CurrentDp(card, id) <= CardEffectCommons.MaxDpDeleteThreshold(card, baseThreshold: 1000);

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[When Attacking] Delete 1 of your opponent's Digimon with 1000 DP or less."));
        }

        return cardEffects;
    }
}
