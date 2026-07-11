// Source: Assets/Scripts/CardEffect/BT1/Yellow/BT1_056.cs
//   [On Play] You may play 1 [Tinkermon] from your hand or recycle bin without paying its memory cost.
// AS-IS: ActivateClass on EffectTiming.OnEnterFieldAnyone.
//   CanUseCondition = CanTriggerOnPlay. CanActivateCondition = IsExistOnBattleArea(card) &&
//   (owner's hand has >=1 card OR HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition)),
//   where CanSelectCardCondition(cardSource) = cardSource.CardNames.Contains("Tinkermon") &&
//   CanPlayAsNewPermanent(cardSource, payCost:false). ORDER=-1, ISOPTIONAL=true.
//   ActivateCoroutine: if both hand and trash have a match, asks the owner to pick a ROOT ZONE first
//   ("From hand" / "From trash"); if only one, defaults to it. THEN a zone-scoped select (Mode.Custom,
//   canNoSelect:true) picks 1 matching card, and CardEffectCommons.PlayPermanentCards(selected, payCost:false,
//   root: fromHand ? Hand : Trash, activateETB:true) plays it as a new permanent, cost-free.
//
// Headless mirror: ActivatedSelectAndPlayFromZonesEffect — a single logical select over the COMBINED
//   candidate pool (Hand ∪ Trash), each candidate tagged with its own origin zone so the play mutation stamps
//   the correct FromZone (cost-free PlayCard). The AS-IS "from hand / from trash" zone prompt is UI sugar; the
//   selectable outcome set is identical. isOptional ("you may") -> canEndNotMax:true (skippable pick). The
//   candidate gate mirrors CanSelectCardCondition (name "Tinkermon" + CanPlayAsNewPermanent, cost-free).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_056 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            // AS-IS CanSelectCardCondition: CardNames.Contains("Tinkermon") && CanPlayAsNewPermanent(payCost:false).
            bool CanTarget(HeadlessEntityId id)
            {
                var candidate = new CardSource(card.Context, id, card.Owner);
                return candidate.EqualsCardName("Tinkermon")
                    && CardEffectCommons.CanPlayAsNewPermanent(candidate, payCost: false, cardEffect: null);
            }

            const string desc = "[On Play] You may play 1 [Tinkermon] from your hand or recycle bin without paying its memory cost.";
            cardEffects.Add(new ActivatedEffect(
                card, EffectTiming.None, canUse: null, canActivate: null,
                body: new ActivatedSelectAndPlayFromZonesEffect(
                    card,
                    fromZones: new[] { ChoiceZone.Hand, ChoiceZone.Trash },
                    canTarget: CanTarget,
                    maxCount: 1,
                    canEndNotMax: true, // AS-IS canNoSelect:true (skippable pick).
                    description: desc),
                maxCountPerTurn: null, isOptional: false, desc)); // (B-5) AS-IS isOptional=true is folded into the body canNoSelect (canEndNotMax:true) — result-equivalent; a true 2-decision restore needs the optional-prompt protocol (deferred).
        }

        return cardEffects;
    }
}
