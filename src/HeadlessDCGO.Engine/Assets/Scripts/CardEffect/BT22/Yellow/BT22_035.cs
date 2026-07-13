// Source: Assets/Scripts/CardEffect/BT22/Yellow/BT22_035.cs (1:1 mirror) — "Entermon" (Appmon).
//
// Static (timing == None):
//   * Sup. Appmon Alternative Digivolution Requirement (:19-27) —
//     AddSelfDigivolutionRequirementStaticEffect(permanentCondition: p => p.TopCard.HasSuperAppTraits,
//     digivolutionCost:4, ignoreDigivolutionRequirement:false). HasSuperAppTraits = EqualsTraits("Sup.").
//   * App Fusion (:33-37) — AddAppfuseMethodByName({"Mediamon","Dreammon"}).
//   * Link Condition (:42-49) — AddSelfLinkConditionStaticEffect(permanentCondition: p =>
//     p.TopCard.HasAppmonTraits, linkCost:3). HasAppmonTraits = EqualsTraits("Appmon").
// [Main] <Link> (OnDeclaration :55-58) — CardEffectFactory.LinkEffect(card).
// Alliance (OnAllyAttack :64-67) — AllianceSelfEffect(isInheritedEffect:false).
//
// [On Play] / [When Digivolving] (OnEnterFieldAnyone :185/:218) — "You may link 1 level 4 or lower Digimon
//   card from your hand or this Digimon's digivolution cards to this Digimon without paying the cost." (C-2
//   witness — the link-host branch.) AS-IS returns BOTH at OnEnterFieldAnyone; the CanUseCondition discriminates
//   (CanTriggerOnPlay vs CanTriggerWhenDigivolving). SharedIsAppMon(cardSource) (:75-81) =
//     IsDigimon && HasLevel && Level<=4 && HasAppmonTraits && CanLinkToTargetPermanent(this permanent, false).
//   CanActivateCondition (:202-206/:235-239) = IsExistOnBattleAreaDigimon && (hand-has-match || own-sources-have-match).
//   ActivateCoroutine (SharedActivateCoroutine :83-179): area-then-card select → AddLinkCard(cardForLinking).
//   ORDER=-1 (no cap), ISOPTIONAL=true. Headless mirror: uniform ActivatedEffect wrapping the new composable
//   LinkFromHandOrSourcesToSelfBody (pools hand + own-source candidates into one optional select, then
//   LinkHelpers.AddLinkCardAsync onto THIS card as host — see the body's header for the AS-IS area-bool fold).
//   isOptional=true folded into the body's canSkip (B-5 convention).
// [Your Turn][Once Per Turn] When linked, play 1 play-cost-4-or-lower [Appmon] card free (WhenLinked :251-330) —
//   ActivateClass, SetHashString("BT22_035_WL"), ORDER=1, ISOPTIONAL=true. CanUseCondition =
//   CanTriggerWhenLinked(IsEntermon = this card's own permanent got linked). CanActivateCondition =
//   IsExistOnBattleAreaDigimon && IsOwnerTurn && hand-has IsAppMonCard. IsAppMonCard = IsExistOnHand &&
//   HasPlayCost && BasePlayCostFromEntity<=4 && HasAppmonTraits (BasePlayCostFromEntity ↦ GetCostItself, the
//   printed play cost). Body = SelectHand(maxCount:1, canNoSelect) → PlayPermanentCards(payCost:false, root:Hand,
//   activateETB:true). Headless mirror: uniform ActivatedEffect + ActivatedSelectAndPlayFromZonesEffect({Hand},
//   maxCount:1, canEndNotMax:true), maxCountPerTurn:1, capHash "BT22_035_WL", isOptional folded into the body.
//
// STOP — [When Linking] "To 1 opponent Digimon, give -4000 DP for the turn per your [Appmon] Digimon"
//   (WhenLinked :336-408, SetIsLinkedEffect(true), CanTriggerWhenLinking). This effect fires from THIS card's
//   LINK-CARD position: AS-IS SetIsLinkedEffect(true) gives it a distinct effect LIFECYCLE (only active while the
//   source card is a link card, and inheritance-cleanup parity — ICardEffect.cs:392/403/430). The uniform
//   ActivatedEffect has no IsLinkedEffect lifecycle flag; reproducing only the fire-gate (CanTriggerWhenLinking +
//   CardSource.IsLinked) would be result-PARTIAL, not a 1:1 structure mirror. Deferred as design item C2-01
//   (IsLinkedEffect marker on activated effects) — the golf's C-2 witness is satisfied by the link-host branch
//   above, so this addendum's absence does not block it.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT22.Yellow;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT22_035 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();

        #region Static Effects (timing == None)
        if (timing == EffectTiming.None)
        {
            // Sup. Appmon Alternative Digivolution Requirement (AS-IS :19-27).
            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: p => p.TopCard.EqualsTraits("Sup."),   // AS-IS TopCard.HasSuperAppTraits
                digivolutionCost: 4,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null));

            // App Fusion (Mediamon & Dreammon) (AS-IS :33-37).
            cardEffects.Add(CardEffectFactory.AddAppfuseMethodByName(new List<string> { "Mediamon", "Dreammon" }, card));

            // Link Condition (AS-IS :42-49).
            cardEffects.Add(CardEffectFactory.AddSelfLinkConditionStaticEffect(
                permanentCondition: p => p.TopCard.EqualsTraits("Appmon"),   // AS-IS TopCard.HasAppmonTraits
                linkCost: 3,
                card: card));
        }
        #endregion

        #region [Main] <Link> (OnDeclaration)
        if (timing == EffectTiming.OnDeclaration)
        {
            // AS-IS :55-58 CardEffectFactory.LinkEffect(card).
            cardEffects.Add(CardEffectFactory.LinkEffect(card));
        }
        #endregion

        #region Alliance (OnAllyAttack)
        if (timing == EffectTiming.OnAllyAttack)
        {
            // AS-IS :64-67 AllianceSelfEffect(isInheritedEffect:false, card, condition:null).
            cardEffects.Add(CardEffectFactory.AllianceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region [On Play] / [When Digivolving] — link a level-4-or-lower [Appmon] onto self (C-2 witness)
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            // AS-IS SharedIsAppMon (:75-81).
            bool SharedIsAppMon(CardSource cardSource) =>
                cardSource.IsDigimon
                && cardSource.HasLevel && cardSource.Level <= 4
                && cardSource.EqualsTraits("Appmon")   // HasAppmonTraits
                && cardSource.CanLinkToTargetPermanent(new Permanent(card.Context, card.InstanceId, card.Owner));

            // AS-IS CanActivateCondition (:202-206 / :235-239).
            bool CanActivate() =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                && (CardEffectCommons.HasMatchConditionOwnersHand(card, SharedIsAppMon)
                    || new Permanent(card.Context, card.InstanceId, card.Owner).DigivolutionCards.Count(SharedIsAppMon) > 0);

            const string onPlayDesc = "[On Play] You may link 1 level 4 or lower Digimon card from your hand or this Digimon's digivolution cards to this Digimon without paying the cost.";
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: ctx => CardEffectCommons.CanTriggerOnPlay(ctx, card),   // AS-IS CanTriggerOnPlay (:199)
                canActivate: CanActivate,
                body: new LinkFromHandOrSourcesToSelfBody(card, SharedIsAppMon, onPlayDesc),
                maxCountPerTurn: null,   // AS-IS ORDER=-1
                isOptional: false,       // AS-IS isOptional=true folded into the body canSkip (B-5)
                description: onPlayDesc));

            const string wdDesc = "[When Digivolving] You may link 1 level 4 or lower Digimon card from your hand or this Digimon's digivolution cards to this Digimon without paying the cost.";
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.OnEnterFieldAnyone,
                canUse: ctx => CardEffectCommons.CanTriggerWhenDigivolving(ctx, card),   // AS-IS CanTriggerWhenDigivolving (:232)
                canActivate: CanActivate,
                body: new LinkFromHandOrSourcesToSelfBody(card, SharedIsAppMon, wdDesc),
                maxCountPerTurn: null,   // AS-IS ORDER=-1
                isOptional: false,       // AS-IS isOptional=true folded into the body canSkip (B-5)
                description: wdDesc));
        }
        #endregion

        #region [Your Turn][Once Per Turn] play a 4-cost-or-lower [Appmon] when linked (WhenLinked)
        if (timing == EffectTiming.WhenLinked)
        {
            // AS-IS IsAppMonCard (:282-287).
            bool IsAppMonCard(CardSource cardSource) =>
                CardEffectCommons.IsExistOnHand(cardSource)
                && cardSource.HasPlayCost && cardSource.GetCostItself <= 4   // AS-IS BasePlayCostFromEntity <= 4
                && cardSource.EqualsTraits("Appmon");

            // AS-IS IsEntermon (:276-280): this card's OWN permanent is the one that got linked.
            bool IsEntermon(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                && permanent.InstanceId == card.InstanceId;

            bool CanTargetPlay(HeadlessEntityId id) => IsAppMonCard(new CardSource(card.Context, id, card.Owner));

            const string wlDesc = "[Your Turn] [Once Per Turn] When this Digimon gets linked, you may play 1 play cost 4 or lower card with the [Appmon] trait from your hand without paying the cost.";
            cardEffects.Add(new ActivatedEffect(
                card: card,
                timing: EffectTiming.WhenLinked,
                canUse: ctx => CardEffectCommons.CanTriggerWhenLinked(ctx, card, IsEntermon, null),   // AS-IS CanUseCondition (:266)
                canActivate: () => CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.HasMatchConditionOwnersHand(card, IsAppMonCard),        // AS-IS CanActivateCondition (:269-274)
                body: new ActivatedSelectAndPlayFromZonesEffect(
                    card,
                    fromZones: new[] { ChoiceZone.Hand },
                    canTarget: CanTargetPlay,
                    maxCount: 1,
                    canEndNotMax: true,   // AS-IS canNoSelect:true — optional pick
                    description: wlDesc),
                maxCountPerTurn: 1,       // AS-IS ORDER=1 — [Once Per Turn]
                isOptional: false,        // AS-IS isOptional=true folded into the body canNoSelect (B-5)
                description: wlDesc,
                capHash: "BT22_035_WL"));  // AS-IS SetHashString("BT22_035_WL")
        }
        #endregion

        return cardEffects;
    }
}
