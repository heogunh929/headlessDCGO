// Source: Assets/Scripts/CardEffect/BT1/Blue/BT1_039.cs
// 1:1 mirror of the original BT1_039 (BT1/Blue) — a Digimon.
//   [When Attacking][Twice Per Turn] You can unsuspend this Digimon by trashing 3 cards in your hand.
//   -> ActivatedEffect(OnAllyAttack, canUse = CanTriggerOnAttack, canActivate = IsExistOnBattleArea &&
//        hand >= 3, body = SelectTrashHandThenSelfMutationBody(3, Unsuspend), maxCountPerTurn = 2 [ORDER=2],
//        isOptional = true [may]).
// AS-IS ActivateCoroutine: SelectHandEffect(discard, maxCount = Min(3, hand), canNoSelect:false) as a COST,
// then IUnsuspendPermanents(self). The hand select+trash cost + self-unsuspend follow-up compose in
// SelectTrashHandThenSelfMutationBody (hand ChoiceRequest via EffectChoiceHelpers; TrashCard then Unsuspend).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_039 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand).Count >= 3;

            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                canActivate: CanActivate,
                body: new SelectTrashHandThenSelfMutationBody(3, MatchStateMutationSink.UnsuspendKind, "Trash 3 cards from your hand to unsuspend this Digimon."),
                maxCountPerTurn: 2,
                isOptional: true,
                description: "[When Attacking][Twice Per Turn] You can unsuspend this Digimon by trashing 3 cards in your hand."));
        }

        return cardEffects;
    }
}
