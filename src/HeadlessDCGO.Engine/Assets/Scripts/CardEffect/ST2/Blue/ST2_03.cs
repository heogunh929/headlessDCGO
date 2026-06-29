// 1:1 mirror of the original ST2_03 (ST2/Blue).
//   [When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon with a
//   level of 5 or less.  -> SelectAndTrashDigivolutionEffect (OnAllyAttack, from bottom, count 1)
// The original wraps an inline ActivateClass + SelectPermanentEffect; the headless uses the
// SelectAndTrashDigivolutionEffect helper (select -> TrashDigivolutionCards mutation). Resolved
// imperatively until the interactive activation flow is wired (docs/audit/card_porting_recipe.md §5).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_03 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.LevelOf(card, id) <= 5
                    && !CardEffectCommons.HasNoDigivolutionCards(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndTrashDigivolutionEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                trashCount: 1,
                fromBottom: true,
                description: "[When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon with a level of 5 or less."));
        }

        return cardEffects;
    }
}
