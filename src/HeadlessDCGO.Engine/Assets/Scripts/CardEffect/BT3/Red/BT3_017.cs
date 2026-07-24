// 1:1 mirror of the original BT3_017 (BT3/Red).
//   [When Digivolving] Delete 1 of your opponent's Digimon with 4000 DP or less.
//   [When Attacking]   Delete 1 of your opponent's Digimon with 4000 DP or less.
//   -> SelectAndDestroyEffect twice (WhenDigivolving / OnAllyAttack), mandatory pick of 1, DP gate via
//   MaxDpDeleteThreshold — same shape as BT3_001/ST1_15.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_017 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        bool CanSelectPermanentCondition(HeadlessEntityId id) =>
            CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                && CardEffectCommons.CurrentDp(card, id) <= CardEffectCommons.MaxDpDeleteThreshold(card, baseThreshold: 4000);

        if (timing == EffectTiming.WhenDigivolving)
        {
            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[When Digivolving] Delete 1 of your opponent's Digimon with 4000 DP or less."));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[When Attacking] Delete 1 of your opponent's Digimon with 4000 DP or less."));
        }

        return cardEffects;
    }
}
