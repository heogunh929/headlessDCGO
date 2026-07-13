namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST5;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class ST5_09 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: ctx => CardEffectCommons.CanTriggerWhenDigivolving(ctx, card),
                canActivate: CanActivate,
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelectPermanentCondition,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "[When Digivolving] Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>. (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)"),
                maxCountPerTurn: null,
                isOptional: false,
                description: "[When Digivolving] Until the end of your opponent's next turn, 1 of your Digimon gains <Blocker>. (When an opponent's Digimon attacks, you may suspend this Digimon to force the opponent to attack it instead.)"));
        }

        return cardEffects;
    }
}