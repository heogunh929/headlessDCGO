// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// G-Link 마감 트랜치 — BT25_052 (Logimon, Digimon / Green)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT25/Green/BT25_052.cs (333 lines, 7 regions)
//    * Alt Digivolution :15-25 (None — AddSelfDigivolutionRequirementStaticEffect [Stnd.App] cost2)
//    * Link Condition   :27-39 (None — AddSelfLinkConditionStaticEffect Appmon cost2)
//    * App Fusion       :41-49 (None — AddAppfuseMethodByName {Onmon,Gatchmon})
//    * Main             :51-192(OnDeclaration — [Main][OPT] link 1 Social/Tool/Game from hand/digi for -1, ILinkCard)
//    * Your Turns       :194-263(WhenLinked — [YT][OPT] ≤1 Tamer: play 1 [Kazuki & Itsuki] free; SelectHand.PlayForFree)
//    * Link             :265-270(OnDeclaration — LinkEffect factory)
//    * When Linking     :272-327(WhenLinked — [When Linking] linked-effect: suspend 1 enemy; SelectPermanent.Tap)
//
// 치환(substrate translations only): IEnumerator→async Task; StartCoroutine(X)→await X; lone `yield return null`→
//   Task.CompletedTask; `card.Owner.UntilCalculateFixedCostEffect`→`new Player(card.Context, card.Owner)...`;
//   `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`; `HasStandardAppTraits`→`EqualsTraits("Stnd.")`,
//   `HasAppmonTraits`→`EqualsTraits("Appmon")`; `.DigivolutionCards`(IReadOnlyList<CardSource>)→`.ToList()` where List needed;
//   SelectPermanentEffect canTargetCondition = id-형 PermanentOf(id) adapter; `HasMatchConditionPermanent(pred)`→`(card, pred)`;
//   `MatchConditionPermanentCount(pred)`→`(card, pred)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Green;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT25_052 : CEntity_Effect
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

        #region App Fusion (Onmon & Gatchmon)

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.AddAppfuseMethodByName(new List<string>() { "Onmon", "Gatchmon" }, card));

        }

        #endregion

        #region Main
        if (timing == EffectTiming.OnDeclaration)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Link for -1", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("BT25_052_Main");
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[Main] [Once Per Turn] You may link 1 [Social], [Tool] or [Game] trait Digimon card from your hand or this Digimon's digivolution cards to this Digimon with the cost reduced by 1.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
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
        }
        #endregion

        #region Your Turns

        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Kazuki & Itsuki]", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("BT25_052_YT");
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[Your Turn] [Once Per Turn] When this Digimon get linked, if you have 1 or fewer Tamers, you may play 1 [Kazuki & Itsuki] from your hand without paying the cost.";

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
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardCondition)
                    && CardEffectCommons.MatchConditionPermanentCount(card, IsTamerCondition) <= 1;
            }

            bool IsTamerCondition(Permanent permanent) => CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaTamer(permanent, card);

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.EqualsCardName("Kazuki & Itsuki")
                    && cardSource.HasPlayCost
                    && CardEffectCommons.CanPlayAsNewPermanent(cardSource, false, activateClass);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectCardCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: null,
                    afterSelectCardCoroutine: AfterSelectCardCoroutine,
                    mode: SelectHandEffect.Mode.PlayForFree,
                    cardEffect: activateClass);

                await selectHandEffect.Activate();

                Task AfterSelectCardCoroutine(List<CardSource> cardSources)
                {
                    if (cardSources.Count == 0)
                        activateClass.RemoveUse();
                    return Task.CompletedTask;
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
            activateClass.SetUpICardEffect("Suspend 1 enemy Digimon or Tamers", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Linking] Suspend 1 of your opponent's Digimon or Tamers.";
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

            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: id => PermanentOf(id) is { } p && CanSelectPermanentCondition(p),
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

        return cardEffects;
    }
}
