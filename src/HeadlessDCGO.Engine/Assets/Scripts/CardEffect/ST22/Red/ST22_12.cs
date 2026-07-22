// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// G-Link 마감 트랜치 — ST22_12 (DoGatchamon, Digimon / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/ST22/Red/ST22_12.cs (293 lines, 7 regions)
//    * Alt Digivolution   :17-27  (None — AddSelfDigivolutionRequirementStaticEffect, [Stnd.] cost3)
//    * Raid               :29-36  (OnAllyAttack — RaidSelfEffect)
//    * Link Condition     :38-49  (None — AddSelfLinkConditionStaticEffect Appmon cost2)
//    * App Fusion         :51-58  (None — AddAppfuseMethodByName {Gatchmon,Navimon,Tweetmon})
//    * Link               :60-65  (OnDeclaration — LinkEffect factory)
//    * When Attacking     :69-223 (OnAllyAttack — [WA][OPT] bool-area link from hand/digi for -2, direct ILinkCard)
//    * When Linked        :225-287(WhenLinked — [When Linking] linked-effect: return 1 enemy ≤5000 DP to deck bottom)
//
// ② 프리미티브 매핑: P:AddSelfDigivolutionRequirementStaticEffect, K:Raid, P:AddSelfLinkConditionStaticEffect,
//    X:AddAppfuseMethodByName, K:Link (LinkEffect + direct ILinkCard), T:OnAllyAttack (CanTriggerOnAttack),
//    T:WhenLinked/WhenLinking (SelectPermanentEffect.PutLibraryBottom, IsLinkedEffect).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return (Continuous/Start)Coroutine(X)`→`await X`; lone `yield return null`→omit.
//    * `card.Owner.UntilCalculateFixedCostEffect` → `new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect`.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `.DigivolutionCards` (미러 IReadOnlyList<CardSource>) → `.ToList()` where a List<CardSource> is needed
//      (customRootCardList / Filter extension).
//    * `HasAppmonTraits` → `EqualsTraits("Appmon")` (BT22_035/BT25_061 확립 inline idiom).
//    * SelectPermanentEffect canTargetCondition = id-형 `Func<HeadlessEntityId, bool>` — PermanentOf(id) adapter
//      (BT25_089 idiom). `HasMatchConditionOpponentsPermanent(card, PermanentPred)` 는 id-오버로드만 존재 →
//      `id => PermanentOf(id) is { } p && PermanentPred(p)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST22.Red;

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

public sealed class ST22_12 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Static effects

        #region Alternative Digivolution Condition - Stnd.
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Stnd.");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Raid

        if (timing == EffectTiming.OnAllyAttack)
        {
            cardEffects.Add(CardEffectFactory.RaidSelfEffect(isInheritedEffect: false, card: card, condition: null));
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

        #region App Fusion (Gatchmon, Navimon, Tweetmon)

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.AddAppfuseMethodByName(new List<string>() { "Gatchmon", "Navimon", "Tweetmon" }, card));
        }

        #endregion

        #region Link
        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.LinkEffect(card));
        }
        #endregion

        #endregion

        #region When Attacking

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("You may Link 1 digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("WA_ST22-12");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] [Once Per Turn] You may link 1 Digimon card with the [Social], [Navi] or [Tool] trait from your hand or this Digimon's digivolution cards to this Digimon with the link cost reduced by 2.";
            }

            bool CanSelectCardConditionShared(CardSource source)
            {
                return source.IsDigimon &&
                       source.CanLinkToTargetPermanent(ICardEffect.ResolvePermanentOfThisCard(card), true) &&
                       (source.ContainsTraits("Social") || source.ContainsTraits("Navi") || source.ContainsTraits("Tool"));
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       (CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardConditionShared) || ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList().Filter(x => CanSelectCardConditionShared(x)).Count > 0);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool canSelectHand = CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardConditionShared);
                bool canSelectSources = ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList().Filter(x => CanSelectCardConditionShared(x)).Count > 0;

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

                if (canSelectHand || canSelectSources)
                {
                    if (canSelectHand && canSelectSources)
                    {
                        List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                    {
                        new SelectionElement<bool>(message: $"From hand", value : true, spriteIndex: 0),
                        new SelectionElement<bool>(message: $"From digivolution cards", value : false, spriteIndex: 1),
                    };

                        string selectPlayerMessage = "From which area do you select a card?";
                        string notSelectPlayerMessage = "The opponent is choosing from which area to select a card.";

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements, selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage, notSelectPlayerMessage: notSelectPlayerMessage);
                    }
                    else
                    {
                        GManager.instance.userSelectionManager.SetBool(canSelectHand);
                    }

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool fromHand = GManager.instance.userSelectionManager.SelectedBoolValue;
                    CardSource selectedCard = null;

                    if (fromHand)
                    {
                        int maxCount = Math.Min(1, CardEffectCommons.MatchConditionOwnersCardCountInHand(card, CanSelectCardConditionShared));
                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: CanSelectCardConditionShared,
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

                        selectHandEffect.SetUpCustomMessage("Select 1 card to link.", "The opponent is selecting 1 card to link.");
                        selectHandEffect.SetUpCustomMessage_ShowCard("Selected Card");

                        await selectHandEffect.Activate();
                    }
                    else
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: CanSelectCardConditionShared,
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
                            root: SelectCardEffect.Root.Custom,
                            customRootCardList: ICardEffect.ResolvePermanentOfThisCard(card).DigivolutionCards.ToList(),
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 card to link.", "The opponent is selecting 1 card to link.");
                        selectCardEffect.SetUpCustomMessage_ShowCard("Selected Card");

                        await selectCardEffect.Activate();
                    }

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCard = cardSource;
                        return Task.CompletedTask;
                    }

                    if (selectedCard != null)
                    {
                        await new ILinkCard(true, selectedCard, ICardEffect.ResolvePermanentOfThisCard(card), activateClass).LinkCard();
                    }
                }

                #region Remove Link Cost Reduction
                new Player(card.Context, card.Owner).UntilCalculateFixedCostEffect.Remove(GetCardEffect);
                #endregion
            }
        }

        #endregion

        #region When Linked

        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Return a digimon to bottom of deck", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
                => "[When Linking] Return 1 of your opponent's 5000 DP or lower Digimon to the bottom of the deck.";

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerWhenLinking(hashtable, null, card))
                    {
                        return true;
                    }
                }
                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition);
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) &&
                       permanent.DP <= 5000;
            }

            // (G-Link) SelectPermanentEffect canTargetCondition / HasMatchConditionOpponentsPermanent 는
            // id-형 오버로드만 존재 → PermanentOf(id) 어댑터 (BT25_089 idiom).
            Permanent? PermanentOf(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                    ? new Permanent(card.Context, id, rec.OwnerId)
                    : null;

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, id => PermanentOf(id) is { } p && PermanentCondition(p)))
                {
                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    selectPermanentEffect.SetUp(
                        selectPlayer: card.Owner,
                        canTargetCondition: id => PermanentOf(id) is { } p && PermanentCondition(p),
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        maxCount: 1,
                        canNoSelect: false,
                        canEndNotMax: false,
                        selectPermanentCoroutine: null,
                        afterSelectPermanentCoroutine: null,
                        mode: SelectPermanentEffect.Mode.PutLibraryBottom,
                        cardEffect: activateClass);

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon.", "The opponent is selecting 1 Digimon.");
                    await selectPermanentEffect.Activate();
                }
            }
        }

        #endregion

        return cardEffects;
    }
}
