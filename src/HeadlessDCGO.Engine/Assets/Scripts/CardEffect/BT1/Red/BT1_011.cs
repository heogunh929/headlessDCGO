// 1:1 mirror of the original BT1_011 (BT1/Red).
//   [On Play] Return 1 Digimon card with [Agumon] in its name from your trash to your hand.
//   AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone, CanUseCondition = CanTriggerOnPlay,
//   CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionOwnersCardInTrash(card,
//   CanSelectCardCondition), ORDER=-1, ISOPTIONAL=false, ActivateCoroutine = SelectCardEffect.SetUp
//   (mode: AddHand, root: Trash, maxCount: Min(1, owner's matching trash count), canEndNotMax: false).
//   CanSelectCardCondition = cardSource.IsDigimon && cardSource.ContainsCardName("Agumon").
//   Headless mirror: the uniform ActivatedEffect (= AS-IS ActivateClass) with explicit CanUse/CanActivate gates
//   (CanTriggerOnPlay / IsExistOnBattleArea / HasMatchConditionOwnersCardInTrash, all faithfully evaluated —
//   not folded away) and body=ActivatedSelectFromZoneEffect (AS-IS SelectCardEffect Mode.AddHand / Root.Trash).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_011 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            const string description =
                "[On Play] Return 1 Digimon card with [Agumon] in its name from your trash to your hand.";

            // AS-IS CanSelectCardCondition: cardSource.IsDigimon && cardSource.ContainsCardName("Agumon").
            bool CanSelectCardSource(CardSource cs) => cs.IsDigimon && cs.ContainsCardName("Agumon");

            bool CanSelectId(HeadlessEntityId id) =>
                CanSelectCardSource(new CardSource(card.Context, id, card.Owner, card.Owner));

            // AS-IS CanUseCondition: CanTriggerOnPlay(hashtable, card).
            bool CanUse(CardEffectResolveContext ctx) => CardEffectCommons.CanTriggerOnPlay(ctx, card);

            // AS-IS CanActivateCondition: IsExistOnBattleArea(card) &&
            // HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition).
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardSource);

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: CanUse,
                canActivate: CanActivate,
                body: new ActivatedSelectFromZoneEffect(
                    card, ChoiceZone.Trash, CanSelectId, maxCount: 1, canEndNotMax: false,
                    MatchStateMutationSink.ReturnToHandKind, "Select 1 card to add to your hand."),
                maxCountPerTurn: null,
                isOptional: false,
                description: description));
        }

        return cardEffects;
    }
}
