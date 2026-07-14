// R6-A CUTOVER STOP (design item RD-R6-01, same root cause as RD-P8-01): kept in old-model ActivatedEffect. The
// AS-IS ActivateCoroutine's "from hand" branch uses `GManager.instance.GetComponent<SelectHandEffect>()` (Mode.Custom
// hand select), but the mirror Script/SelectHandEffect.cs is still a 7-line skeleton (no class body, no SetUp/Mode) —
// unchanged by R1~R3. Substituting SelectCardEffect(Root.Hand) is invention (rejected precedent BT9_109/BT1_039).
// Left as ActivatedEffect + ActivatedSelectAndPlayFromZonesEffect until SelectHandEffect is ported (Opus-gated).
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
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            // AS-IS CanSelectCardCondition (BT1_056.cs:27-38): CardNames.Contains("Tinkermon") &&
            // CanPlayAsNewPermanent(payCost:false).
            bool CanSelectCardCondition(CardSource cardSource) =>
                cardSource.EqualsCardName("Tinkermon")
                && CardEffectCommons.CanPlayAsNewPermanent(cardSource, payCost: false, cardEffect: null);

            bool CanTarget(HeadlessEntityId id) => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner));

            const string desc = "[On Play] You may play 1 [Tinkermon] from your hand or recycle bin without paying its memory cost.";
            cardEffects.Add(new ActivatedEffect(
                card,
                timing: EffectTiming.OnEnterFieldAnyone,
                // AS-IS CanUseCondition (BT1_056.cs:40-43).
                canUse: ctx => CardEffectCommons.CanTriggerOnPlay(ctx, card),
                // AS-IS CanActivateCondition (BT1_056.cs:45-61): on the battle area AND (owner's HAND has >= 1
                // card — a RAW count, NOT filtered by CanSelectCardCondition — OR a matching card is in the
                // owner's trash).
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && (((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand).Count >= 1
                        || CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanSelectCardCondition)),
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
