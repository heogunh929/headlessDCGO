namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_097 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        bool CanSelectPermanentCondition(HeadlessEntityId id)
        {
            return CardEffectCommons.IsOpponentBattleAreaDigimon(card, id)
                && CardEffectCommons.LevelOf(card, id) == 3;
        }

        if (timing == EffectTiming.OptionSkill)
        {
            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 3,
                changeValue: -4000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Main] 3 of your opponent's level 3 Digimon get -4000 DP for the turn."));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.SelectAndBuffDpEffect(
                card: card,
                canTarget: CanSelectPermanentCondition,
                maxCount: 3,
                changeValue: -4000,
                duration: EffectDuration.UntilEachTurnEnd,
                description: "[Security] 3 of your opponent's level 3 Digimon get -4000 DP for the turn."));
        }

        return cardEffects;
    }
}