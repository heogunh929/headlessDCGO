namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT3.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT3_097 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = [];

        if (timing == EffectTiming.OptionSkill)
        {
            const string description =
                "[Main] 1 of your Digimon gains \"This Digimon doesn't activate the [Security] effects of any Option cards it checks\" for the turn.";

            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOwnerBattleAreaDigimon(card, id);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OptionSkill,
                canUse: ctx => CardEffectCommons.CanTriggerOptionMainEffect(ctx, card),
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
                    description: description,
                    onEachSelected: id =>
                    {
                        var selectedPermanent = new Permanent(card.Context, id, card.Owner);

                        bool CanProtect(Permanent permanent) => permanent.InstanceId == id;

                        bool IsOpponentOptionSecurity(CardSource effectSourceCard) =>
                            effectSourceCard is not null &&
                            CardEffectCommons.IsOpponentEffect(effectSourceCard, card) &&
                            effectSourceCard.IsOption &&
                            CardEffectCommons.IsExistInSecurity(effectSourceCard) &&
                            CardEffectCommons.IsExistOnBattleArea(selectedPermanent.TopCard) &&
                            card.Context.AttackController.Current.AttackerId == selectedPermanent.InstanceId;

                        var canNotAffected = CardEffectFactory.CanNotAffectedStaticEffect(
                            permanentCondition: CanProtect,
                            skillCondition: IsOpponentOptionSecurity,
                            isInheritedEffect: false,
                            card: card,
                            condition: null);

                        CardEffectCommons.AddEffectToPermanent(
                            selectedPermanent,
                            EffectDuration.UntilEachTurnEnd,
                            card,
                            canNotAffected,
                            EffectTiming.None);
                    }),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.AddThisCardToHandEffect(card));
        }

        return cardEffects;
    }
}
