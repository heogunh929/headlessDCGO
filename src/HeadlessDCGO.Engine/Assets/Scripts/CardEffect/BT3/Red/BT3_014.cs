namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_014 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = [];

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id) &&
                new Permanent(card.Context, id, card.Owner).TopCard is { Level: <= 4 } &&
                new Permanent(card.Context, id, card.Owner).Level <= 4;

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: null,
                canActivate: () =>
                    CardEffectCommons.IsExistOnBattleArea(card) &&
                    CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelectPermanentCondition,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[When Digivolving] Change the original DP of 1 of your opponent's level 4 or lower Digimon to 1000 for the turn.",
                    onEachSelected: id => CardEffectCommons.ChangeBaseDigimonDP(
                        new Permanent(card.Context, id, card.Owner),
                        changeValue: 1000,
                        EffectDuration.UntilEachTurnEnd,
                        card)),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Digivolving] Change the original DP of 1 of your opponent's level 4 or lower Digimon to 1000 for the turn."));
        }

        if (timing == EffectTiming.None)
        {
            bool CanUse() =>
                CardEffectCommons.IsExistOnBattleArea(card) && CardEffectCommons.IsOwnerTurn(card);

            var changeCardColorClass = new ChangeCardColorClass();
            changeCardColorClass.SetUpICardEffect("Also treated as yellow", CanUse, card);
            changeCardColorClass.SetUpChangeCardColorClass((CardSource target, List<string> cardColors) =>
            {
                if (target == card)
                {
                    cardColors.Add("Yellow");
                }

                return cardColors;
            });

            cardEffects.Add(changeCardColorClass);
        }

        return cardEffects;
    }
}
