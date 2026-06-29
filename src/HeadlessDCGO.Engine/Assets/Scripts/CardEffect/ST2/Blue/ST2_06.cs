// 1:1 mirror of the original ST2_06 (ST2/Blue).
//   [When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon.
//   -> SelectAndTrashDigivolutionEffect (OnAllyAttack, from bottom, count 1) — same as ST2_03 without the
//   level<=5 / has-source gate on the target.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_06 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndTrashDigivolutionEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                trashCount: 1,
                fromBottom: true,
                description: "[When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon."));
        }

        return cardEffects;
    }
}
