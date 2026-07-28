// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// G-Link 마감 트랜치 — BT25_056 (Bootmon, Digimon / Green)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Green/BT25_056.cs (335 lines, 8 regions)
//    * Alt Digivolution :14-24 (None — AddSelfDigivolutionRequirementStaticEffect [Sup.App] cost3)
//    * Link Condition   :26-38 (None — AddSelfLinkConditionStaticEffect Appmon cost3)
//    * App Fusion       :40-48 (None — AddAppfuseMethodByName {Logimon,Craftmon})
//    * Barrier          :50-55 (WhenPermanentWouldBeDeleted — BarrierSelfEffect)
//    * Shared OP/WD/WA  :57-205(ActivateClassesForSharedEffects — link 1 Social/Tool/Game from hand/digi for -2, ILinkCard)
//    * All Turns        :207-266(WhenLinked — [AT] suspend 1 enemy; SelectPermanent.Tap)
//    * Link             :268-273(OnDeclaration — LinkEffect factory)
//    * When Linking     :275-330(WhenLinked — [When Linking] linked-effect: bottom-deck 1 enemy suspended Digimon; PutLibraryBottom)
//
// 치환(substrate translations only): IEnumerator→async Task; StartCoroutine(X)→await X;
//   `card.Owner.UntilCalculateFixedCostEffect`→`new Player(card.Context, card.Owner)...`;
//   `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`; `HasSuperAppTraits`→`EqualsTraits("Sup.")`,
//   `HasAppmonTraits`→`EqualsTraits("Appmon")`; `.DigivolutionCards`(IReadOnlyList<CardSource>)→`.ToList()` where List needed;
//   SelectPermanentEffect canTargetCondition = Permanent-형 술어 직접 전달; `HasMatchConditionPermanent(pred)`→`(card, pred)`;
//   SharedActivateCoroutine 델리게이트는 `Func<Hashtable,ActivateClass,Task>`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Green;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_056 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternative Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Sup."); // AS-IS TopCard.HasSuperAppTraits
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Link Condition

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Appmon"); // AS-IS TopCard.HasAppmonTraits
            }

            cardEffects.Add(CardEffectFactory.AddSelfLinkConditionStaticEffect(permanentCondition: PermanentCondition, linkCost: 3, card: card));
        }

        #endregion

        #region App Fusion (Logimon & Craftmon)

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.AddAppfuseMethodByName(new List<string>() { "Logimon", "Craftmon" }, card));

        }

        #endregion

        #region Barrier
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region Shared OP / WD / WA

        string SharedEffectName = "Link from hand or digivolution cards to this for -2";

        string SharedEffectDescription(string tag)
            => $"[{tag}] If it's your turn, you may link 1 [Social], [Tool] or [Game] trait Digimon card from your hand or this Digimon's digivolution cards to this Digimon with the cost reduced by 2.";

        bool AdditionalActivateCondition(Hashtable hashtable, ActivateClass activateClass)
        {
            return CardEffectCommons.IsOwnerTurn(card)
                && (CardEffectCommons.HasMatchConditionOwnersHand(card, CanLinkCardActivateCondition)
                    || ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Any(CanLinkCardActivateCondition));
        }

        bool CanLinkCardActivateCondition(CardSource cardSource) => CanLinkCardCondition(cardSource, false);

        bool CanLinkCardEffectCondition(CardSource cardSource) => CanLinkCardCondition(cardSource, true);

        bool CanLinkCardCondition(CardSource cardSource, bool payCost)
        {
            return cardSource.IsDigimon
                && (cardSource.EqualsTraits("Social")
                    || cardSource.EqualsTraits("Tool")
                    || cardSource.EqualsTraits("Game"))
                && cardSource.CanLinkToTargetPermanent(ICardEffect.ResolvePermanentOfThisCard(card), payCost);
        }

        CardEffectFactory.ActivateClassesForSharedEffects
            (ref cardEffects, timing, card,
                SharedEffectName,
                SharedActivateCoroutine,
                SharedEffectDescription,
                additionalActivateCondition: AdditionalActivateCondition,
                optional: false,
                isSkippable: true,
                onPlay: true,
                whenDigivolving: true,
                whenAttacking: true);

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            #region Link Cost Reduction
            ICardEffect GetCardEffect(EffectTiming _timing)
            {
                if (_timing == EffectTiming.None)
                {
                    return CardEffectFactory.GrantedReduceLinkCostClass(
                        card: card,
                        reducedCost: 2,
                        cardSourceCondition: _ => true,
                        permanentCondition: _ => true,
                        rootCondition: _ => true
                    );
                }

                return null;
            }

            new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add(GetCardEffect);
            #endregion

            bool canSelectHand = CardEffectCommons.HasMatchConditionOwnersHand(card, CanLinkCardEffectCondition);
            bool canSelectSources = ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Any(CanLinkCardEffectCondition);

            List<SelectionElement<int>> selectionElements = new List<SelectionElement<int>>();
            if (canSelectHand)
            {
                selectionElements.Add(new SelectionElement<int>(message: $"From hand", value : 1, spriteIndex: 0));
            }
            if (canSelectSources)
            {
                selectionElements.Add(new SelectionElement<int>(message: $"From digivolution cards", value : 2, spriteIndex: 0));
            }
            selectionElements.Add(new SelectionElement<int>(message: $"Do not Link", value : 3, spriteIndex: 1));

            string selectPlayerMessage = "From which area will you link a card?";
            string notSelectPlayerMessage = "The opponent is choosing from which area to select a card.";

            GManager.instance.userSelectionManager.SetIntSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);

            await GManager.instance.userSelectionManager.WaitForEndSelect();

            bool doLink = GManager.instance.userSelectionManager.SelectedIntValue != 3;
            bool fromHand = GManager.instance.userSelectionManager.SelectedIntValue == 1;
            if (doLink)
            {
                if (fromHand)
                {
                    SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                    selectHandEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanLinkCardEffectCondition,
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
                    selectHandEffect.SetUpCustomMessage_ShowCard("Selected Card");

                    await selectHandEffect.Activate();
                }
                else
                {
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanLinkCardEffectCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 card to add as source.",
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

                async Task SelectCardCoroutine(CardSource cardSource)
                {
                    await new ILinkCard(true, cardSource, ICardEffect.ResolvePermanentOfThisCard(card), activateClass).LinkCard();
                }
            }

            #region Remove Link Cost Reduction
            new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Remove(GetCardEffect);
            #endregion
        }

        #endregion

        #region All Turns
        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Suspend 1 enemy Digimon or Tamer", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[All Turns] When this Digimon get linked, suspend 1 of your opponent's Digimon or Tamers.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return permanent == ICardEffect.ResolvePermanentOfThisCard(card);
            }

            bool IsOpponentsPermanent(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleArea(permanent, card)
                    && (permanent.IsDigimon
                        || permanent.IsTamer);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenLinked(hashtable, PermanentCondition, null);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, IsOpponentsPermanent))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOpponentsPermanent,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Tap,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }
        #endregion

        #region Link
        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.LinkEffect(card));
        }
        #endregion

        #region When Linking

        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Bottom deck 1 of enemy suspended Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Linking] Return 1 of your opponent's suspended Digimon to the bottom of the deck.";
            }

            bool IsOpponentsDigimon(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
                    && permanent.IsSuspended;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenLinking(hashtable, null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, IsOpponentsDigimon))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: IsOpponentsDigimon,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.PutLibraryBottom,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
