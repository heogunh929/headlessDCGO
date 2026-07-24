// 1:1 mirror of the original BT3_098 (BT3/Red).
//   [Main]     Delete 1 of your opponent's Digimon with 13000 DP or more. -> SelectAndDestroyEffect
//   [Security] (use the Main effect)                                     -> AddActivateMainOptionSecurityEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_098 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) && CardEffectCommons.CurrentDp(card, id) >= 13000;

            cardEffects.Add(CardEffectFactory.SelectAndDestroyEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                canEndNotMax: false,
                description: "[Main] Delete 1 of your opponent's Digimon with 13000 DP or more."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "Delete 1 Digimon with 13000 DP or more");
        }

        return cardEffects;
    }
}
