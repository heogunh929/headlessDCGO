// 1:1 mirror of BT2_095 — an Option (Blue).
//   [Main] Return up to 3 of your opponent's level 3 Digimon to their hand.
//          (Trash all of the digivolution cards of those Digimon.)
//          -> SelectAndBounceEffect (OptionSkill, canEndNotMax:true, maxCount:3, level-3 filter)
// No [Security] effect in original — not ported.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_095 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                    && CardEffectCommons.LevelOf(card, id) == 3;
            }

            cardEffects.Add(CardEffectFactory.SelectAndBounceEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 3,
                canEndNotMax: true,
                description: "[Main] Return up to 3 of your opponent's level 3 Digimon to their hand."));
        }

        return cardEffects;
    }
}