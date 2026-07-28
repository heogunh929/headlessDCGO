// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// G-Link 마감 트랜치 — BT25_070 (Logamon, Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Black/BT25_070.cs (333 lines, 6 regions)
//    * Alt Digivolution :16-24 (None — AddSelfDigivolutionRequirementStaticEffect [Stnd.App] cost2)
//    * Link Condition   :27-37 (None — AddSelfLinkConditionStaticEffect Appmon cost2)
//    * App Fusion       :39-44 (None — AddAppfuseMethodByName {Offmon,Hackmon})
//    * Main             :46-190(OnDeclaration — [Main][OPT] link 1 Social/Tool/Game from trash/digi for -1, ILinkCard)
//    * Your Turns       :192-252(WhenLinked — [YT][OPT] delete 1 enemy ≤4 cost Digimon; SelectPermanent.Destroy)
//    * Link             :254-259(OnDeclaration — LinkEffect factory)
//    * When Linking     :261-327(WhenLinked — [When Linking] linked-effect: 1 enemy can't unsuspend; GainCanNotUnsuspend)
//
// 치환(substrate translations only): IEnumerator→async Task; `yield return (Continuous/Start)Coroutine(X)`→`await X`;
//   lone `yield return null`→omit; `card.Owner.UntilCalculateFixedCostEffect`→`new Player(card.Context, card.Owner)...`;
//   `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`; `HasStandardAppTraits`→`EqualsTraits("Stnd.")`,
//   `HasAppmonTraits`→`EqualsTraits("Appmon")`; `.DigivolutionCards`(IReadOnlyList<CardSource>)→`.ToList()` where List needed;
//   SelectPermanentEffect canTargetCondition = Permanent-형 술어 직접 전달; `HasMatchConditionPermanent(pred)`→`(card, pred)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Black;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_070 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternative Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Stnd."); // AS-IS TopCard.HasStandardAppTraits
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Link Condition
        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Appmon"); // AS-IS TopCard.HasAppmonTraits
            }

            cardEffects.Add(CardEffectFactory.AddSelfLinkConditionStaticEffect(permanentCondition: PermanentCondition, linkCost: 2, card: card));
        }
        #endregion

        #region App Fusion (Offmon & Hackmon)
        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.AddAppfuseMethodByName(new List<string>() { "Offmon", "Hackmon" }, card));
        }
        #endregion

        #region Main
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Link for -1", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, 1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[Main] [Once Per Turn] You may link 1 [Social], [Tool] or [Game] trait Digimon card from your trash or this Digimon's digivolution cards to this Digimon with the cost reduced by 1.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && (CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanLinkCardActivateCondition)
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

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                #region Link Cost Reduction
                ICardEffect GetCardEffect(EffectTiming _timing)
                {
                    if (_timing == EffectTiming.None)
                    {
                        return CardEffectFactory.GrantedReduceLinkCostClass(
                            card: card,
                            reducedCost: 1,
                            cardSourceCondition: _ => true,
                            permanentCondition: _ => true,
                            rootCondition: _ => true
                        );
                    }

                    return null;
                }

                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Add(GetCardEffect);
                #endregion

                bool canSelectTrash = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, CanLinkCardEffectCondition);
                bool canSelectSources = ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.Any(CanLinkCardEffectCondition);

                List<SelectionElement<int>> selectionElements = new List<SelectionElement<int>>();
                if (canSelectTrash)
                {
                    selectionElements.Add(new SelectionElement<int>(message: $"From trash", value : 1, spriteIndex: 0));
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
                bool fromTrash = GManager.instance.userSelectionManager.SelectedIntValue == 1;
                if (doLink)
                {
                    if (fromTrash)
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
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 card to link.", "The opponent is selecting 1 card to link.");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Selected Card");

                        await selectCardEffect.Activate();
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
        }
        #endregion

        #region Your Turns

        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 enemy 4 cost or less Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("BT25_070_YT");
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[Your Turn] [Once Per Turn] When this Digimon gets linked, delete 1 of your opponent's Digimon with a play cost of 4 or less.";

            bool PermanentCondition(Permanent permanent)
            {
                return permanent == ICardEffect.ResolvePermanentOfThisCard(card);
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

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card)
                    && permanent.TopCard.HasPlayCost
                    && permanent.TopCard.GetCostItself <= 4;
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Destroy,
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
            activateClass.SetUpICardEffect("1 enemy Digimon or Tamers can't unsuspend until their turn ends", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Linking] 1 of your opponent's Digimon or Tamers can't unsuspend until their turn ends.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleArea(permanent, card)
                    && (permanent.IsDigimon || permanent.IsTamer);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenLinking(hashtable, null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: CanSelectPermanentCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: SelectPermanentCoroutine,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.Custom,
                        cardEffect: activateClass);

                    await selectPermanentEffect.Activate();

                    async Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        await CardEffectCommons.GainCanNotUnsuspend(
                            targetPermanent: permanent,
                            effectDuration: EffectDuration.UntilOpponentTurnEnd,
                            activateClass: activateClass,
                            condition: null,
                            effectName: "Can't unsuspend"
                        );
                    }
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
