// 1:1 mirror of the original ST3_16 (ST3/Yellow) — an Option.
//   [Main]     1 of your opponent's Digimon gets -10000 DP for the turn.  -> SelectAndBuffDpEffect (opponent, -10000)
//   [Security] (use the Main effect)                                      -> AddActivateMainOptionSecurityEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST3_16 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id)
            {
                return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);
            }

            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 1,
                changeValue: -10000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] 1 of your opponent's Digimon gets -10000 DP for the turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            CardEffectCommons.AddActivateMainOptionSecurityEffect(card: card, cardEffects: ref cardEffects, effectName: "DP -10000");
        }

        return cardEffects;
    }
}
