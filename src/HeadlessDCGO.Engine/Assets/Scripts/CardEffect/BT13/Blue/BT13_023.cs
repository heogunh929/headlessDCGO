// Source: Assets/Scripts/CardEffect/BT13/Blue/BT13_023.cs (1:1 mirror)
// <Evade> (When this Digimon would be deleted, you may suspend it to prevent that deletion.)
//   AS-IS (:14-17): timing == WhenPermanentWouldBeDeleted ->
//     CardEffectFactory.EvadeSelfEffect(isInheritedEffect: false, card, condition: null). Verbatim factory
//     mirror — the grant registers the Evade keyword; the PRE would-be-deleted window
//     (DeletionReplacementTiming / DeletionReplacementGate.TryEvade) offers "suspend to survive".
// [When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon.
//   AS-IS (:19-92): ActivateClass on EffectTiming.OnAllyAttack.
//     CanUseCondition = CanTriggerOnAttack(hashtable, card) (:44-47).
//     CanActivateCondition = IsExistOnBattleArea(card) && HasMatchConditionPermanent(CanSelectPermanentCondition)
//     (:49-60), where CanSelectPermanentCondition = IsPermanentExistsOnOpponentBattleAreaDigimon(permanent,
//     card) (:32-41 — NO trashable-source-count clause, unlike BT1_086).
//     ORDER=-1 (no once-per-turn cap), ISOPTIONAL=false. SetIsInheritedEffect(true).
//     ActivateCoroutine (:62-91): maxCount = Min(1, MatchConditionPermanentCount) ->
//     SelectPermanentEffect(Mode.Custom, canNoSelect:false, canEndNotMax:false) ->
//     TrashDigivolutionCardsFromTopOrBottom(targetPermanent: pick, trashCount: 1, isFromTop: false).
// Headless mirror: uniform ActivatedEffect + SelectBody(Mode.Custom) with the sink-scoped
// TrashDigivolutionCards(count:1, fromBottom:true) follow-up — same shape as BT1_086's select half.
// Min(1, count) is subsumed by SelectBody's clamp-to-live-candidates (BT1_079 rationale).
// NOTE: AS-IS SetIsInheritedEffect(true) has no equivalent parameter on the uniform ActivatedEffect —
// same accepted, documented systemic gap as BT1_022 / BT1_079 (activated-effect inheritance, not per-card).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT13.Blue;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT13_023 : CEntity_Effect
{
    public override IReadOnlyList<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.EvadeSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            // AS-IS CanSelectPermanentCondition (BT13_023.cs:32-41): opponent battle-area Digimon only.
            bool CanSelectPermanentCondition(HeadlessEntityId id) =>
                CardEffectCommons.IsOpponentBattleAreaDigimon(card, id);

            const string desc = "[When Attacking] Trash the digivolution card at the bottom of 1 of your opponent's Digimon.";
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnAllyAttack,
                // AS-IS CanUseCondition (BT13_023.cs:44-47).
                canUse: ctx => CardEffectCommons.CanTriggerOnAttack(ctx, card),
                // AS-IS CanActivateCondition (BT13_023.cs:49-60).
                canActivate: () => CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition),
                body: new SelectBody(
                    card: card,
                    canTarget: CanSelectPermanentCondition,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    mode: SelectPermanentEffect.Mode.Custom,
                    description: "Select 1 Digimon that will trash digivolution cards.",
                    // AS-IS SelectPermanentCoroutine (:85-90): TrashDigivolutionCardsFromTopOrBottom(1, isFromTop:false).
                    onEachSelectedWithSink: (c, sink, id) => CardEffectCommons.TrashDigivolutionCards(sink, c, id, count: 1, fromBottom: true)),
                maxCountPerTurn: null,
                isOptional: false,
                description: desc));
        }

        return cardEffects;
    }
}
