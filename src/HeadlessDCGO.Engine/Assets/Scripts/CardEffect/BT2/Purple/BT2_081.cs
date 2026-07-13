// Source: Assets/Scripts/CardEffect/BT2/Purple/BT2_081.cs
//   [When Attacking] You may play 1 purple level 3 Digimon card from your trash without paying its memory cost.
//   Any [On Play] effects on Digimon played with this effect don't activate.
// AS-IS: ActivateClass on EffectTiming.OnAllyAttack.
//   CanUseCondition = CanTriggerOnAttack. CanActivateCondition = IsExistOnBattleArea(card) &&
//   HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition).
//   CanSelectCardCondition: IsDigimon && Owner==card.Owner && HasCardColor(Purple) && Level==3 &&
//   HasLevel && CanPlayAsNewPermanent(payCost:false). ORDER=-1, ISOPTIONAL=true (canNoSelect:true).
//   ActivateCoroutine: SelectCardEffect from Trash (maxCount=1, canEndNotMax=false),
//   then PlayPermanentCards(payCost:false, activateETB:false).
//
// Headless mirror: ActivatedSelectAndPlayFromZonesEffect over {Trash} only; canEndNotMax:true (you may).
// STOP: activateETB:false (suppress [On Play] effects on played Digimon) —
//   ActivatedSelectAndPlayFromZonesEffect exposes no activateETB parameter in the reference signature.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_081 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            // AS-IS CanSelectCardCondition (BT2_081.cs:27-53): != null -> IsDigimon -> Owner == card.Owner ->
            // CanPlayAsNewPermanent(payCost:false) -> HasCardColor(Purple) -> Level==3 -> HasLevel.
            bool CanSelectCardCondition(CardSource cardSource) =>
                cardSource is not null
                && cardSource.IsDigimon
                && cardSource.Owner == card.Owner
                && CardEffectCommons.CanPlayAsNewPermanent(cardSource, payCost: false, cardEffect: null)
                && cardSource.HasCardColor("Purple")
                && cardSource.Level == 3
                && cardSource.HasLevel;

            bool CanTarget(HeadlessEntityId id) => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner));

            // STOP: activateETB:false (suppress [On Play] effects on played Digimon) —
            //   ActivatedSelectAndPlayFromZonesEffect exposes no activateETB parameter in the reference signature.
            const string desc = "[When Attacking] You may play 1 purple level 3 Digimon card from your trash without paying its memory cost. Any [On Play] effects on Digimon played with this effect don't activate.";
            cardEffects.Add(new ActivatedEffect(
                card,
                timing: EffectTiming.OnAllyAttack,
                // AS-IS CanUseCondition (BT2_081.cs:55-58).
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                // AS-IS CanActivateCondition (BT2_081.cs:60-71). The AS-IS ActivateCoroutine's body-head
                // re-guard (:75-79 isExistOnField + GetBattleAreaDigimons().Contains(PermanentOfThisCard()) +
                // HasMatchConditionOwnersCardInTrash) is BODY logic in the AS-IS structure; here it is subsumed
                // by the per-pass canActivate re-check at execution entry (the resolver re-runs this gate at the
                // same point AS-IS AutoProcessing.cs:1068 re-runs CanActivate immediately before the coroutine).
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition),
                body: new ActivatedSelectAndPlayFromZonesEffect(
                    card,
                    fromZones: new[] { ChoiceZone.Trash },
                    canTarget: CanTarget,
                    maxCount: 1,
                    canEndNotMax: true,
                    description: desc),
                maxCountPerTurn: null, isOptional: false, desc)); // (B-5) AS-IS isOptional=true is folded into the body canNoSelect (canEndNotMax:true) — result-equivalent; a true 2-decision restore needs the optional-prompt protocol (deferred).
        }

        return cardEffects;
    }
}