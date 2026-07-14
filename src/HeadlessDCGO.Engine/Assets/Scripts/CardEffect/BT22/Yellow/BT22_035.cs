// Source: DCGO/Assets/Scripts/CardEffect/BT22/Yellow/BT22_035.cs (1:1 mirror) — "Entermon" (Appmon).
//
// R5-A re-port (RD-R6-01 / RD-P8-01 resolved): the two SelectHandEffect-bearing activated effects
// (SharedActivateCoroutine for [On Play]/[When Digivolving], and the [Your Turn] WhenLinked play) are re-housed
// from old-model bodies (LinkFromHandOrSourcesToSelfBody / ActivatedSelectAndPlayFromZonesEffect) to new-model
// ActivateClass now that the mirror SelectHandEffect exists (Script/SelectHandEffect.cs). The AS-IS coroutines are
// mirrored 1:1 including the area bool (from hand / from sources) via GManager.instance.userSelectionManager
// (bridge W5, BT1_111 idiom).
//
// Static (timing == None) — unchanged:
//   * Sup. Appmon Alternative Digivolution Requirement (:19-27) — HasSuperAppTraits = EqualsTraits("Sup.").
//   * App Fusion (:33-37) — AddAppfuseMethodByName({"Mediamon","Dreammon"}).
//   * Link Condition (:42-49) — HasAppmonTraits = EqualsTraits("Appmon").
// [Main] <Link> (OnDeclaration :55-58) — LinkEffect. Alliance (OnAllyAttack :64-67) — AllianceSelfEffect.
//
// STOP — [When Linking] "-4000 DP per your [Appmon] Digimon" (WhenLinked :336-408, SetIsLinkedEffect(true)):
//   still deferred as design item C2-01 (IsLinkedEffect activated-effect lifecycle) — orthogonal to the
//   SelectHandEffect gap this batch resolves (it uses SelectPermanentEffect, not SelectHandEffect). The C-2
//   witness is satisfied by the [On Play]/[When Digivolving] link-host branch, so its absence does not block it.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT22.Yellow;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

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

        #region OP/WD Shared Effects (AS-IS :73-181, declared at method scope for both OnEnterFieldAnyone blocks)

        // AS-IS SharedIsAppMon (:75-81).
        bool SharedIsAppMon(CardSource cardSource)
        {
            return cardSource.IsDigimon
                && cardSource.HasLevel && cardSource.Level <= 4
                && cardSource.EqualsTraits("Appmon")   // AS-IS HasAppmonTraits
                && cardSource.CanLinkToTargetPermanent(ICardEffect.ResolvePermanentOfThisCard(card), false);
        }

        // AS-IS SharedActivateCoroutine (:83-179).
        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            bool canUseHand = CardEffectCommons.HasMatchConditionOwnersHand(card, SharedIsAppMon);
            bool canUseSources = ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(SharedIsAppMon) > 0;

            if (canUseHand || canUseSources)
            {
                #region Select Card Location
                if (canUseHand && canUseSources)
                {
                    List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                    {
                        new SelectionElement<bool>(message: $"From hand", value : true, spriteIndex: 0),
                        new SelectionElement<bool>(message: $"From digivolution sources", value : false, spriteIndex: 1),
                    };

                    string selectPlayerMessage = "From which area do you select a card?";
                    string notSelectPlayerMessage = "The opponent is choosing from which area to select a card.";

                    GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
                }
                else
                {
                    GManager.instance.userSelectionManager.SetBool(canUseHand);
                }

                await GManager.instance.userSelectionManager.WaitForEndSelect();
                #endregion

                bool fromHand = GManager.instance.userSelectionManager.SelectedBoolValue;
                CardSource cardForLinking = null;
                Task SelectCardCoroutine(CardSource cardSource)
                {
                    cardForLinking = cardSource;
                    return Task.CompletedTask;
                }

                #region Select Card from Hand or Sources
                if (fromHand)
                {
                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: SharedIsAppMon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage("Select 1 card to link.", "The opponent is selecting 1 card to link.");

                    await selectHandEffect.Activate();
                }
                else
                {
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: SharedIsAppMon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 card to link.",
                        maxCount: 1,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.DigivolutionCards,
                        customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 card to link.", "The opponent is selecting 1 card to link.");
                    selectCardEffect.SetUpCustomMessage_ShowCard("Selected Card");

                    await selectCardEffect.Activate();
                }
                #endregion

                if (cardForLinking != null)
                {
                    await ICardEffect.ResolvePermanentOfThisCard(card).AddLinkCard(cardForLinking, activateClass.EffectSourceCard?.InstanceId);
                }
            }
        }
        #endregion

        #region [On Play] / [When Digivolving] — link a level-4-or-lower [Appmon] onto self (C-2 witness)
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            // AS-IS CanActivateCondition (:202-206 / :235-239).
            bool CanActivate()
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && (CardEffectCommons.HasMatchConditionOwnersHand(card, SharedIsAppMon)
                        || ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Count(SharedIsAppMon) > 0);
            }

            // [On Play] (AS-IS :185-212).
            {
                ActivateClass activateClass = new ActivateClass();
                activateClass.SetUpICardEffect("Link a level 4 or lower digimon from hand or sources", CanUseCondition, card);
                activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
                cardEffects.Add(activateClass);

                string EffectDiscription()
                {
                    return "[On Play] You may link 1 level 4 or lower Digimon card from your hand or this Digimon's digivolution cards to this Digimon without paying the cost.";
                }

                bool CanUseCondition(Hashtable hashtable)
                {
                    return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
                }

                bool CanActivateCondition(Hashtable hashtable)
                {
                    return CanActivate();
                }

                async Task ActivateCoroutine(Hashtable hashtable)
                {
                    await SharedActivateCoroutine(hashtable, activateClass);
                }
            }

            // [When Digivolving] (AS-IS :218-245).
            {
                ActivateClass activateClass = new ActivateClass();
                activateClass.SetUpICardEffect("Link a level 4 or lower digimon from hand or sources", CanUseCondition, card);
                activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
                cardEffects.Add(activateClass);

                string EffectDiscription()
                {
                    return "[When Digivolving] You may link 1 level 4 or lower Digimon card from your hand or this Digimon's digivolution cards to this Digimon without paying the cost.";
                }

                bool CanUseCondition(Hashtable hashtable)
                {
                    return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
                }

                bool CanActivateCondition(Hashtable hashtable)
                {
                    return CanActivate();
                }

                async Task ActivateCoroutine(Hashtable hashtable)
                {
                    await SharedActivateCoroutine(hashtable, activateClass);
                }
            }
        }
        #endregion

        #region [Your Turn][Once Per Turn] play a 4-cost-or-lower [Appmon] when linked (WhenLinked)
        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 4 cost or lower [Appmon] card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("BT22_035_WL");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] [Once Per Turn] When this Digimon gets linked, you may play 1 play cost 4 or lower card with the [Appmon] trait from your hand without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenLinked(hashtable, IsEntermon, null);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && CardEffectCommons.HasMatchConditionOwnersHand(card, IsAppMonCard);
            }

            // AS-IS IsEntermon (:276-280): this card's OWN permanent got linked.
            bool IsEntermon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent.InstanceId == card.InstanceId;   // AS-IS permanent == card.PermanentOfThisCard()
            }

            // AS-IS IsAppMonCard (:282-287).
            bool IsAppMonCard(CardSource cardSource)
            {
                return CardEffectCommons.IsExistOnHand(cardSource)
                    && cardSource.HasPlayCost && cardSource.GetCostItself <= 4   // AS-IS BasePlayCostFromEntity <= 4
                    && cardSource.EqualsTraits("Appmon");
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOwnersHand(card, IsAppMonCard))
                {
                    CardSource selectedCard = null;

                    #region Select Appmon in Hand
                    int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInHand(card, IsAppMonCard));
                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsAppMonCard,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: maxCount,
                        canNoSelect: true,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                    selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                    await selectHandEffect.Activate();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCard = cardSource;
                        return Task.CompletedTask;
                    }
                    #endregion

                    if (selectedCard != null)
                    {
                        await CardEffectCommons.PlayPermanentCards(cardSources: new List<CardSource>() { selectedCard }, activateClass: activateClass, payCost: false, isTapped: false, root: SelectCardEffect.Root.Hand, activateETB: true);
                    }
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
