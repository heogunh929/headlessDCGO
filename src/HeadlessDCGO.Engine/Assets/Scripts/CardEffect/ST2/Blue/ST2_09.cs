// 1:1 mirror of the original ST2_09 (ST2/Blue).
//   [When Digivolving] Trash 2 digivolution cards at the bottom of 1 of your opponent's Digimon.
//   -> SelectAndTrashDigivolutionEffect (WhenDigivolving, from bottom, count 2)
// Declared under WhenDigivolving (the DigivolveAction-wired timing that resolves activated selects via
// ActivatedEffectResolver), NOT OnEnterFieldAnyone — the bridge excludes OnEnterFieldAnyone, so an activated
// select there never fires live. Matches ST1_08's WhenDigivolving idiom.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST2_09 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.HasTrashableDigivolutionCards(card, id);
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
