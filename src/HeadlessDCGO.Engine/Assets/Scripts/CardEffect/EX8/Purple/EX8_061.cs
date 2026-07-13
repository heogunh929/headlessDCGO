// Source: Assets/Scripts/CardEffect/EX8/Purple/EX8_061.cs (1:1 mirror)
// Alternate Digivolution — AS-IS (:15-29): timing == EffectTiming.None ->
//   AddSelfDigivolutionRequirementStaticEffect(permanentCondition: p => p.TopCard.IsLevel4 &&
//   p.TopCard.EqualsTraits("DS"), digivolutionCost: 3, ignoreDigivolutionRequirement: false, card,
//   condition: null). Headless mirror: the PRIM-W5 predicate primitive
//   (AddedDigivolutionRequirementPredicateEffect), consumed by DigivolveAction's added-requirement scan.
//   AS-IS IsLevel4 = HasLevel && Level == 4 (CardSource.cs:2929) -> HasLevel && IsLevel(4).
// <Scapegoat> — AS-IS (:33-45): timing == WhenPermanentWouldBeDeleted -> ScapegoatSelfEffect(false, card,
//   null, "<Scapegoat>", discription). Verbatim factory mirror. The AS-IS by-own-effect exclusion
//   ("other than by your own effects", Scapegoat.cs:65-73 IsByEffect(IsOwnerEffect) -> false) lives in
//   the shared ScapegoatEffect factory, mirrored by DeletionReplacementTiming's DeletedByOwnEffectKey
//   suppression (RD-5) — not per-card logic in either codebase.
// [When Attacking] [Once Per Turn] If you have 1 or more memory, you may play 1 level 4 or lower Digimon
// card with the [DS], [Mollusk], or [Crustacean] trait from your trash without paying the cost.
//   AS-IS (:49-135): ActivateClass on OnAllyAttack, SetHashString("PlayDigimon_EX8_061"), ORDER=1
//   ([Once Per Turn]), ISOPTIONAL=true.
//     DigimonToPlay (:62-70): CanPlayAsNewPermanent(payCost:false, root:Trash) && IsDigimon && HasLevel &&
//       Level <= 4 && (EqualsTraits DS | Mollusk | Crustacean).
//     CanUseCondition (:72-77): IsExistOnBattleAreaDigimon(card) && CanTriggerOnAttack.
//     CanActivateCondition (:79-84): IsExistOnBattleAreaDigimon && HasMatchConditionOwnersCardInTrash &&
//       Owner.MemoryForPlayer >= 1.
//     ActivateCoroutine: SelectCardEffect(root:Trash, maxCount:1, canNoSelect:()=>true, canEndNotMax:false)
//       -> PlayPermanentCards(payCost:false, isTapped:false, root:Trash, activateETB:true).
//   Headless mirror: uniform ActivatedEffect + ActivatedSelectAndPlayFromZonesEffect({Trash}, maxCount:1,
//   canEndNotMax:true), maxCountPerTurn:1, capHash "PlayDigimon_EX8_061" (AS-IS SetHashString — splits the
//   cap partition from the [On Deletion] sibling). activateETB:true matches the body's play (ETB fires).
// [On Deletion] You may play 1 level 4 or lower Digimon card with the [DS], [Mollusk], or [Crustacean]
// trait from your trash without paying the cost.
//   AS-IS (:139-232): ActivateClass on OnDestroyedAnyone, SetIsInheritedEffect(true), ORDER=-1,
//   ISOPTIONAL=true.
//     HasCorrectTrait (:153-167): traits -> HasLevel && Level <= 4 -> CanPlayAsNewPermanent(payCost:false).
//     CanUseCondition (:169-172): CanTriggerOnDeletion. CanActivateCondition (:174-178):
//       CanActivateOnDeletion && HasMatchConditionOwnersCardInTrash. Headless mirrors
//       CanActivateOnDeletion's non-token branch as IsExistOnTrash(card) (BT2_034 convention — the
//       ctx-less canActivate half; the trigger half already pinned the subject to this card's permanent).
//     ActivateCoroutine: SelectCardEffect(root:Trash, maxCount:1, canNoSelect:()=>true) ->
//       PlayPermanentCards(payCost:false, activateETB:true).
//   Headless mirror: same select-and-play body, maxCountPerTurn:null. Fires from the TRASH via the
//   SubjectScoped OnDestroyedAnyone bridge (the deleted card is the event subject).
//   NOTE: AS-IS SetIsInheritedEffect(true) — same accepted, documented systemic gap as BT1_022 / BT1_079.
// (B-5) Both trash-plays fold AS-IS isOptional=true into the body canNoSelect (canEndNotMax:true) —
// result-equivalent; a true 2-decision restore needs the optional-prompt protocol (deferred, BT2_081).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Purple;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_061 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution
        if (timing == EffectTiming.None)
        {
            // AS-IS PermanentCondition (EX8_061.cs:17-20): TopCard.IsLevel4 && TopCard.EqualsTraits("DS").
            bool PermanentCondition(Permanent targetPermanent) =>
                targetPermanent.TopCard.HasLevel
                && targetPermanent.TopCard.IsLevel(4)
                && targetPermanent.TopCard.EqualsTraits("DS");

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 3,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null));
        }
        #endregion

        #region Scapegoat
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            // AS-IS EX8_061.cs:37-40 (local EffectDiscription; AS-IS param spelling kept by the mirror factory).
            string EffectDiscription()
            {
                return "<Scapegoat> (When this Digimon would be deleted other than by your own effects, by deleting 1 of your other Digimon, prevent that deletion.)";
            }

            cardEffects.Add(CardEffectFactory.ScapegoatSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null,
                effectName: "<Scapegoat>",
                effectDiscription: EffectDiscription()));
        }
        #endregion

        #region When Attacking
        if (timing == EffectTiming.OnAllyAttack)
        {
            // AS-IS DigimonToPlay (EX8_061.cs:62-70), same predicate order.
            bool DigimonToPlay(CardSource cardSource) =>
                CardEffectCommons.CanPlayAsNewPermanent(cardSource, payCost: false, cardEffect: null)
                && cardSource.IsDigimon
                && cardSource.HasLevel
                && cardSource.Level <= 4
                && (cardSource.EqualsTraits("DS") || cardSource.EqualsTraits("Mollusk") || cardSource.EqualsTraits("Crustacean"));

            bool CanTarget(HeadlessEntityId id) => DigimonToPlay(new CardSource(card.Context, id, card.Owner));

            const string desc = "[When Attacking] [Once Per Turn] If you have 1 or more memory, you may play 1 level 4 or lower Digimon card with the [DS], [Mollusk], or [Crustacean] trait from your trash without paying the cost.";
            cardEffects.Add(new ActivatedEffect(
                card,
                timing: EffectTiming.OnAllyAttack,
                // AS-IS CanUseCondition (EX8_061.cs:72-77).
                canUse: ctx => CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerOnAttack(ctx, card),
                // AS-IS CanActivateCondition (EX8_061.cs:79-84).
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, DigimonToPlay)
                    && CardEffectCommons.MemoryForPlayer(card) >= 1,
                body: new ActivatedSelectAndPlayFromZonesEffect(
                    card,
                    fromZones: new[] { ChoiceZone.Trash },
                    canTarget: CanTarget,
                    maxCount: 1,
                    canEndNotMax: true,
                    description: desc),
                maxCountPerTurn: 1,   // AS-IS ORDER=1 — [Once Per Turn]
                isOptional: false,    // (B-5) AS-IS isOptional=true folded into the body canNoSelect (header note)
                description: desc,
                capHash: "PlayDigimon_EX8_061"));   // AS-IS SetHashString("PlayDigimon_EX8_061")
        }
        #endregion

        #region On Deletion - ESS
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            // AS-IS HasCorrectTrait (EX8_061.cs:153-167), same predicate order.
            bool HasCorrectTrait(CardSource cardSource) =>
                (cardSource.EqualsTraits("DS") || cardSource.EqualsTraits("Mollusk") || cardSource.EqualsTraits("Crustacean"))
                && cardSource.HasLevel
                && cardSource.Level <= 4
                && CardEffectCommons.CanPlayAsNewPermanent(cardSource, payCost: false, cardEffect: null);

            bool CanTarget(HeadlessEntityId id) => HasCorrectTrait(new CardSource(card.Context, id, card.Owner));

            const string desc = "[On Deletion] You may play 1 level 4 or lower Digimon card with the [DS], [Mollusk], or [Crustacean] trait from your trash without paying the cost.";
            cardEffects.Add(new ActivatedEffect(
                card,
                timing: EffectTiming.OnDestroyedAnyone,
                // AS-IS CanUseCondition (EX8_061.cs:169-172).
                canUse: ctx => CardEffectCommons.CanTriggerOnDeletion(ctx, card),
                // AS-IS CanActivateCondition (EX8_061.cs:174-178) — CanActivateOnDeletion mirrored as
                // IsExistOnTrash (header note, BT2_034 convention).
                canActivate: () => CardEffectCommons.IsExistOnTrash(card)
                    && CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasCorrectTrait),
                body: new ActivatedSelectAndPlayFromZonesEffect(
                    card,
                    fromZones: new[] { ChoiceZone.Trash },
                    canTarget: CanTarget,
                    maxCount: 1,
                    canEndNotMax: true,
                    description: desc),
                maxCountPerTurn: null,   // AS-IS ORDER=-1
                isOptional: false,       // (B-5) AS-IS isOptional=true folded into the body canNoSelect (header note)
                description: desc));
        }
        #endregion

        return cardEffects;
    }
}
