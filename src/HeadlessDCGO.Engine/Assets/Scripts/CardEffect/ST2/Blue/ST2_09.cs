// 1:1 mirror of the original ST2_09 (ST2/Blue).
//   [When Digivolving] Trash 2 digivolution cards at the bottom of 1 of your opponent's Digimon.
//   -> SelectAndTrashDigivolutionEffect (OnEnterFieldAnyone, from bottom, count 2)

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_09 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && !CardEffectCommons.HasNoDigivolutionCards(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndTrashDigivolutionEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                trashCount: 2,
                fromBottom: true,
                description: "[When Digivolving] Trash 2 digivolution cards at the bottom of 1 of your opponent's Digimon."));
        }

        return cardEffects;
    }
}
