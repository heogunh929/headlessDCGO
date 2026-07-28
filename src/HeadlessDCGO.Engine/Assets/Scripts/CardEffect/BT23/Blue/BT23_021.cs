// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// BT23_021 (Digimon / Blue) — "Dosukomon" (App Fusion + Link)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT23/Blue/BT23_021.cs (427 lines, #region 구획)
//    * #region App Fusion             :17-102  (None — AddAppFusionConditionClass: Dokamon/Perorimon/Musclemon
//      링크 상호조건, AppFusionCondition(linkCondition, digimonCondition, 0))
//    * #region Alt Digivolution Cond  :108-122 (None — AddSelfDigivolutionRequirementStaticEffect: [Stnd.] 위 코스트3)
//    * #region Link Condition         :128-135 (None — AddSelfLinkConditionStaticEffect: [Appmon] linkCost 2)
//    * #region Link                   :141-144 (OnDeclaration — CardEffectFactory.LinkEffect(card))
//    * #region WD/WA OPT Shared       :152-248 (SharedActivateCoroutine — 손패/진화원 중 하나 선택 후 레벨3 링크,
//      userSelectionManager bool-select → SelectHandEffect / SelectCardEffect(Root.DigivolutionCards) → AddLinkCard)
//    * #region When Digivolving-OPT   :254-277 (OnEnterFieldAnyone, OncePerTurn maxUse1, SetHashString "BT23_021_WD/WA")
//    * #region When Attacking-OPT     :283-306 (OnAllyAttack,      OncePerTurn maxUse1, SetHashString "BT23_021_WD/WA")
//    * #region YT/ESS Shared          :312-336 (SharedActivateCoroutine1 — 자신 GainCanNotBeDeletedByBattle(UntilOpp))
//    * #region Your Turns-OPT         :342-366 (WhenLinked, 자신이 링크될 때, OncePerTurn maxUse1, SetHashString "BT23_021_WL")
//    * #region Link Effect            :372-422 (WhenLinked, SetIsLinkedEffect(true) — 링크카드로서 GainCanNotBeDeletedByBattle)
//
// ② 검증 프리미티브: AddAppFusionConditionClass/SetUpAddAppFusionConditionClass/SetNotShowUI, AppFusionCondition
//    (linkedCondition, digimonCondition, cost), AddSelfDigivolutionRequirementStaticEffect, AddSelfLinkConditionStaticEffect,
//    LinkEffect, SharedActivateCoroutine(userSelectionManager SetBoolSelection/SetBool/WaitForEndSelect/SelectedBoolValue,
//    SelectionElement<bool>, SelectHandEffect.SetUp, SelectCardEffect.SetUp, CanLinkToTargetPermanent, StackCards,
//    HasMatchConditionOwnersHand, AddLinkCard), GainCanNotBeDeletedByBattle(SYNC bool), CanTriggerWhenLinked/Linking.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task; `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`;
//      inner `IEnumerator SelectCardCoroutine`/lone `yield return null`→`Task ...{ ...; return Task.CompletedTask; }`.
//    * `card.PermanentOfThisCard()`→`ICardEffect.ResolvePermanentOfThisCard(card)`; chain
//      `card.PermanentOfThisCard().TopCard.PermanentOfThisCard()`→
//      `ICardEffect.ResolvePermanentOfThisCard(ICardEffect.ResolvePermanentOfThisCard(card).TopCard)` (1:1 semantics).
//    * `permanent == card.PermanentOfThisCard()`→`permanent.InstanceId == ICardEffect.ResolvePermanentOfThisCard(card).InstanceId`
//      (BT21_059 idiom).
//    * `permanent.LinkedCards.Find(pred)`(AS-IS: Unity-Object 암시 bool)→`permanent.LinkedCards.Some(pred)`
//      (미러엔 bool `Find` 확장 부재; `Some`가 유일 등가 bool 확장).
//    * `HasStandardAppTraits`→`EqualsTraits("Stnd.")`, `HasAppmonTraits`→`EqualsTraits("Appmon")` (파생 getter 미러 부재;
//      BT22_035 / BT25_070 확립 idiom). `IsLevel3`(AS-IS `=> HasLevel && Level==3`)→`IsLevel(3)`(미러 파생 getter 부재;
//      직전 `HasLevel` 가드가 이미 존재 → 정확 등가).
//    * `thisPermament.AddLinkCard(addedLinkCard:, cardEffect: activateClass)`→미러 ctor는 `causeEffectSourceId`:
//      `activateClass.EffectSourceCard?.InstanceId`.
//    * `GainCanNotBeDeletedByBattle`는 미러 SYNC bool(AS-IS 코루틴은 UI-only) → await 없이 호출, 순수-sync 코루틴은
//      `Task ...{ ...; return Task.CompletedTask; }` (AD1_011 idiom). 조건 델리게이트 `permanent == AttackingPermanent`는
//      predicate VERBATIM 유지(AD1_011 선례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT23.Blue;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT23_021 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Static Effects

        #region App Fusion (Dokamon, Perorimon, Musclemon)

        if (timing == EffectTiming.None)
        {
            AddAppFusionConditionClass addAppFusionConditionClass = new AddAppFusionConditionClass();
            addAppFusionConditionClass.SetUpICardEffect($"App Fusion", (hashtable) => true, card);
            addAppFusionConditionClass.SetUpAddAppFusionConditionClass(getAppFusionCondition: GetAppFusion);
            addAppFusionConditionClass.SetNotShowUI(true);
            cardEffects.Add(addAppFusionConditionClass);

            AppFusionCondition GetAppFusion(CardSource cardSource)
            {
                bool linkCondition(Permanent permanent, CardSource source)
                {
                    if (source != null && source != card)
                    {
                        if (permanent.TopCard.EqualsCardName("Dokamon"))
                        {
                            if (permanent.LinkedCards.Some(x => x.EqualsCardName("Perorimon") || x.EqualsCardName("Musclemon")))
                            {
                                return true;
                            }
                        }

                        if (permanent.TopCard.EqualsCardName("Perorimon"))
                        {
                            if (permanent.LinkedCards.Some(x => x.EqualsCardName("Dokamon") || x.EqualsCardName("Musclemon")))
                            {
                                return true;
                            }
                        }

                        if (permanent.TopCard.EqualsCardName("Musclemon"))
                        {
                            if (permanent.LinkedCards.Some(x => x.EqualsCardName("Dokamon") || x.EqualsCardName("Perorimon")))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
                bool digimonCondition(Permanent permanent)
                {
                    if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                    {
                        if (permanent.TopCard.EqualsCardName("Dokamon"))
                        {
                            if (permanent.LinkedCards.Some(x => x.EqualsCardName("Perorimon") || x.EqualsCardName("Musclemon")))
                            {
                                return true;
                            }
                        }

                        if (permanent.TopCard.EqualsCardName("Perorimon"))
                        {
                            if (permanent.LinkedCards.Some(x => x.EqualsCardName("Dokamon") || x.EqualsCardName("Musclemon")))
                            {
                                return true;
                            }
                        }

                        if (permanent.TopCard.EqualsCardName("Musclemon"))
                        {
                            if (permanent.LinkedCards.Some(x => x.EqualsCardName("Dokamon") || x.EqualsCardName("Perorimon")))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }

                if (cardSource == card)
                {
                    AppFusionCondition AppFusionCondition = new AppFusionCondition(
                        linkCondition,
                        digimonCondition,
                        0);

                    return AppFusionCondition;
                }

                return null;
            }
        }

        #endregion

        #region Alternative Digivolution Condition

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Stnd."); // AS-IS TopCard.HasStandardAppTraits
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
                permanentCondition: PermanentCondition,
                digivolutionCost: 3,
                ignoreDigivolutionRequirement: false,
                card: card,
                condition: null)
            );
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

        #region Link

        if (timing == EffectTiming.OnDeclaration)
        {
            cardEffects.Add(CardEffectFactory.LinkEffect(card));
        }

        #endregion

        #endregion

        #region WD/WA OPT Shared

        async Task SharedActivateCoroutine(Hashtable hashtable, ActivateClass activateClass)
        {
            Permanent thisPermament = ICardEffect.ResolvePermanentOfThisCard(card);
            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.IsDigimon
                    && cardSource.HasLevel && cardSource.IsLevel(3) // AS-IS IsLevel3 (=> HasLevel && Level==3); HasLevel 이미 가드됨
                    && cardSource.CanLinkToTargetPermanent(thisPermament, false);
            }

            bool canSelectHand = CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardCondition);
            bool canSelectDigivolutionSources = thisPermament.StackCards.Exists(CanSelectCardCondition);

            if (canSelectHand || canSelectDigivolutionSources)
            {
                if (canSelectHand && canSelectDigivolutionSources)
                {
                    List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>()
                    {
                        new SelectionElement<bool>(message: $"From hand", value : true, spriteIndex: 0),
                        new SelectionElement<bool>(message: $"From digivolution", value : false, spriteIndex: 1),
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

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCard = cardSource;
                    return Task.CompletedTask;
                }

                if (fromHand)
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
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        mode: SelectHandEffect.Mode.Custom,
                        cardEffect: activateClass);

                    selectHandEffect.SetUpCustomMessage("Select 1 card to add as link", "The opponent is selecting 1 card to add as link");

                    await selectHandEffect.Activate();
                }
                else
                {
                    SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                    selectCardEffect.SetUp(
                        canTargetCondition: CanSelectCardCondition,
                        canTargetCondition_ByPreSelecetedList: null,
                        canEndSelectCondition: null,
                        canNoSelect: () => true,
                        selectCardCoroutine: SelectCardCoroutine,
                        afterSelectCardCoroutine: null,
                        message: "Select 1 card to add as link.",
                        maxCount: 1,
                        canEndNotMax: false,
                        isShowOpponent: true,
                        mode: SelectCardEffect.Mode.Custom,
                        root: SelectCardEffect.Root.DigivolutionCards,
                        customRootCardList: thisPermament.StackCards,
                        canLookReverseCard: true,
                        selectPlayer: card.Owner,
                        cardEffect: activateClass);

                    selectCardEffect.SetUpCustomMessage("Select 1 card  to add as link.", "The opponent is selecting 1 card to add as link.");

                    await selectCardEffect.Activate();
                }

                if (selectedCard != null) await thisPermament.AddLinkCard(addedLinkCard: selectedCard, causeEffectSourceId: activateClass.EffectSourceCard?.InstanceId);
            }
        }

        #endregion

        #region When Digivolving - OPT

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Link 1 level 3 digimon from hand or digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, hashtable => SharedActivateCoroutine(hashtable, activateClass), 1, true, EffectDiscription());
            activateClass.SetHashString("BT23_021_WD/WA");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] [Once Per Turn] You may link 1 level 3 Digimon card from your hand or this Digimon's digivolution cards to this Digimon without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }
        }

        #endregion

        #region When Attacking - OPT

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Link 1 level 3 digimon from hand or digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, hashtable => SharedActivateCoroutine(hashtable, activateClass), 1, true, EffectDiscription());
            activateClass.SetHashString("BT23_021_WD/WA");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] [Once Per Turn] You may link 1 level 3 Digimon card from your hand or this Digimon's digivolution cards to this Digimon without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }
        }

        #endregion

        #region YT/ESS Shared

        Task SharedActivateCoroutine1(Hashtable hashtable, ActivateClass activateClass)
        {
            bool CanNotBeDestroyedByBattleCondition(Permanent permanent, Permanent AttackingPermanent, Permanent DefendingPermanent, CardSource DefendingCard)
            {
                if (permanent == AttackingPermanent)
                {
                    return true;
                }

                if (permanent == DefendingPermanent)
                {
                    return true;
                }

                return false;
            }

            Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(ICardEffect.ResolvePermanentOfThisCard(card).TopCard);
            CardEffectCommons.GainCanNotBeDeletedByBattle(
                targetPermanent: thisPermanent,
                canNotBeDestroyedByBattleCondition: CanNotBeDestroyedByBattleCondition,
                effectDuration: EffectDuration.UntilOpponentTurnEnd,
                activateClass: activateClass,
                effectName: "Can't be destroyed by battle");

            return Task.CompletedTask;
        }

        #endregion

        #region Your Turns - OPT

        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Gain immunity from battle", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, hashtable => SharedActivateCoroutine1(hashtable, activateClass), 1, false, EffectDiscription());
            activateClass.SetHashString("BT23_021_WL");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] [Once Per Turn] When this Digimon gets linked, it can't be deleted in battle until your opponent's turn ends.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.CanTriggerWhenLinked(hashtable, permanent => permanent.InstanceId == ICardEffect.ResolvePermanentOfThisCard(card).InstanceId, null)
                    && CardEffectCommons.IsOwnerTurn(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }
        }

        #endregion

        #region Link Effect

        if (timing == EffectTiming.WhenLinked)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Gain immunity from battle", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsLinkedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Linking] This Digimon can't be deleted in battle until your opponent's turn ends.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenLinking(hashtable, null, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            Task ActivateCoroutine(Hashtable hashtable)
            {
                bool CanNotBeDestroyedByBattleCondition(Permanent permanent, Permanent AttackingPermanent, Permanent DefendingPermanent, CardSource DefendingCard)
                {
                    if (permanent == AttackingPermanent)
                    {
                        return true;
                    }

                    if (permanent == DefendingPermanent)
                    {
                        return true;
                    }

                    return false;
                }

                Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(ICardEffect.ResolvePermanentOfThisCard(card).TopCard);
                CardEffectCommons.GainCanNotBeDeletedByBattle(
                    targetPermanent: thisPermanent,
                    canNotBeDestroyedByBattleCondition: CanNotBeDestroyedByBattleCondition,
                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                    activateClass: activateClass,
                    effectName: "Can't be deleted in battle");

                return Task.CompletedTask;
            }
        }

        #endregion

        return cardEffects;
    }
}
