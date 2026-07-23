// Source: DCGO/Assets/Scripts/CardEffect/BT19/Yellow/BT19_035.cs (1:1 mirror) — "ShootingStarmons".
//
// S6 롱테일 트랜치 — 감사 시절 ⚠STOP-예상(DigiXros 계열)이었으나 표면 실존 확인(S5/EX4_062 연장).
//   * [None] — AddSelfDigivolutionRequirementStaticEffect(Lv.3 [Xros Heart] 위 코스트2).
//   * [None] — ChangeCardNamesForDigiXrosClass("Starmons"로도 취급, DigiXros 합성명).
//   * [All Turns][Once/turn] — 자신의 [Xros Heart] 디지몬 플레이 시(CanTriggerOnPermanentPlay) 상대 1체
//     <Security Attack -1> + DP -3000(상대 턴 종료까지).
//   * [On Deletion] — 트래시/손패에서 [Xros Heart]/[Blue Flare] 카드 1장을 자기 테이머 밑으로.
//   * [When Attacking][ESS 상속] — 이 디지몬이 [Xros Heart]면 상대 1체 DP -2000(이번 턴).
//
// 치환(substrate translations only): IEnumerator→async Task, StartCoroutine(X)→await X;
// SelectPermanentEffect.canTargetCondition = Permanent-형 술어 직접 전달; `selectedPermanent.
// AddDigivolutionCardsBottom(list, cause)` → cause = `activateClass.EffectSourceCard?.InstanceId`(BT17_026 idiom);
// ChangeDigimonDP/ChangeDigimonSAttack AS-IS-시그니처 브릿지(targetPermanent, changeValue, effectDuration,
// activateClass) 그대로.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Yellow;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT19_035 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent) =>
                targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 3
                && targetPermanent.TopCard.EqualsTraits("Xros Heart");

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition,
                digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region DigiXros name
        if (timing == EffectTiming.None)
        {
            ChangeCardNamesForDigiXrosClass changeCardNamesForDigiXrosClass = new ChangeCardNamesForDigiXrosClass();
            changeCardNamesForDigiXrosClass.SetUpICardEffect("Also treated as [Starmons] for a DigiXros", CanUseCondition, card);
            changeCardNamesForDigiXrosClass.SetUpChangeCardNamesForDigiXrosClass(changeCardNames: ChangeCardNames);

            cardEffects.Add(changeCardNamesForDigiXrosClass);

            bool CanUseCondition(Hashtable hashtable) => true;

            List<string> ChangeCardNames(CardSource cardSource, List<string> cardNames)
            {
                if (cardSource == card)
                {
                    cardNames.Add("Starmons");
                }

                return cardNames;
            }
        }
        #endregion

        #region All Turns
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("1 of your opponent's digimon gets <Security Attack -1> and -3000 DP for the turn",
                CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetHashString("Debuff_BT19_035");
            cardEffects.Add(activateClass);

            string EffectDescription() =>
                "[All Turns] (Once Per Turn) When any of your [Xros Heart] trait Digimon are played, give 1 of your opponent's Digimon <Security Attack -1> and it gets -3000 DP until the end of your opponent's turn.";

            bool PlayerPermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card) &&
                permanent.TopCard.EqualsTraits("Xros Heart");

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                CardEffectCommons.CanTriggerOnPermanentPlay(hashtable, PlayerPermanentCondition);

            bool CanSelectPermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                Permanent? selectedPermanent = null;

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                Task SelectPermanentCoroutine(Permanent permanent)
                {
                    selectedPermanent = permanent;
                    return Task.CompletedTask;
                }

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

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get DP -3000 and <Security Attack -1>.",
                    "The opponent is selecting 1 Digimon that will get DP -3000 and <Security Attack -1>.");

                await selectPermanentEffect.Activate();

                if (selectedPermanent != null)
                {
                    await CardEffectCommons.ChangeDigimonDP(
                        targetPermanent: selectedPermanent, changeValue: -3000,
                        effectDuration: EffectDuration.UntilOpponentTurnEnd, activateClass: activateClass);

                    await CardEffectCommons.ChangeDigimonSAttack(
                        targetPermanent: selectedPermanent, changeValue: -1, effectDuration: EffectDuration.UntilOpponentTurnEnd,
                        activateClass: activateClass);
                }
            }
        }
        #endregion

        #region On Deletion
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place 1 [Xros Heart]/[Blue Flare] card from trash under 1 of your Tamers, then <Save>",
                CanUseCondition,
                card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription() =>
                "[On Deletion] Place 1 Digimon card with the [Xros Heart]/[Blue Flare] trait from your hand or trash under your Tamers.";

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerOnDeletion(hashtable, card);

            bool IsOwnTamerCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card) && permanent.IsTamer;

            bool HasTraitCondition(CardSource cardSource) =>
                cardSource.IsDigimon && (cardSource.EqualsTraits("Xros Heart") || cardSource.EqualsTraits("Blue Flare"));

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.CanActivateOnDeletion(hashtable, card) &&
                CardEffectCommons.HasMatchConditionOwnersPermanent(card, IsOwnTamerCondition) &&
                (CardEffectCommons.HasMatchConditionOwnersHand(card, HasTraitCondition) ||
                 CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasTraitCondition));

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                bool canSelectHand = CardEffectCommons.HasMatchConditionOwnersHand(card, HasTraitCondition);
                bool canSelectTrash = CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, HasTraitCondition);

                if (canSelectHand || canSelectTrash)
                {
                    if (canSelectHand && canSelectTrash)
                    {
                        List<SelectionElement<bool>> selectionElements = new List<SelectionElement<bool>>
                        {
                            new(message: "From hand", value: true, spriteIndex: 0),
                            new(message: "From trash", value: false, spriteIndex: 1),
                        };

                        string selectPlayerMessage = "From which area do you place a card?";
                        string notSelectPlayerMessage = "The opponent is choosing from which area to place a card.";

                        GManager.instance.userSelectionManager.SetBoolSelection(selectionElements: selectionElements,
                            selectPlayer: card.Owner, selectPlayerMessage: selectPlayerMessage,
                            notSelectPlayerMessage: notSelectPlayerMessage);
                    }
                    else
                    {
                        GManager.instance.userSelectionManager.SetBool(canSelectHand);
                    }

                    await GManager.instance.userSelectionManager.WaitForEndSelect();

                    bool fromHand = GManager.instance.userSelectionManager.SelectedBoolValue;

                    List<CardSource> selectedCards = new List<CardSource>();

                    Task SelectCardCoroutine(CardSource cardSource)
                    {
                        selectedCards.Add(cardSource);
                        return Task.CompletedTask;
                    }

                    if (fromHand)
                    {
                        SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                        selectHandEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: HasTraitCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: false,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            mode: SelectHandEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectHandEffect.SetUpCustomMessage("Select 1 card to place under a tamer.",
                            "The opponent is selecting 1 card to place.");
                        selectHandEffect.SetUpCustomMessage_ShowCard("Placed card");

                        await selectHandEffect.Activate();
                    }
                    else
                    {
                        SelectCardEffect selectCardEffect = GManager.instance.GetComponent<SelectCardEffect>();

                        selectCardEffect.SetUp(
                            canTargetCondition: HasTraitCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            canNoSelect: () => false,
                            selectCardCoroutine: SelectCardCoroutine,
                            afterSelectCardCoroutine: null,
                            message: "Select 1 card to place under a tamer.",
                            maxCount: 1,
                            canEndNotMax: false,
                            isShowOpponent: true,
                            mode: SelectCardEffect.Mode.Custom,
                            root: SelectCardEffect.Root.Trash,
                            customRootCardList: null,
                            canLookReverseCard: true,
                            selectPlayer: card.Owner,
                            cardEffect: activateClass);

                        selectCardEffect.SetUpCustomMessage("Select 1 card to place under a tamer.",
                            "The opponent is selecting 1 card to place under a tamer.");

                        await selectCardEffect.Activate();
                    }

                    if (selectedCards.Count >= 1)
                    {
                        Permanent? selectedPermanent = null;

                        SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                        Task SelectPermanentCoroutine(Permanent permanent)
                        {
                            selectedPermanent = permanent;
                            return Task.CompletedTask;
                        }

                        selectPermanentEffect.SetUp(
                            selectPlayer: card.Owner,
                            canTargetCondition: IsOwnTamerCondition,
                            canTargetCondition_ByPreSelecetedList: null,
                            canEndSelectCondition: null,
                            maxCount: 1,
                            canNoSelect: true,
                            canEndNotMax: false,
                            selectPermanentCoroutine: SelectPermanentCoroutine,
                            afterSelectPermanentCoroutine: null,
                            mode: SelectPermanentEffect.Mode.Custom,
                            cardEffect: activateClass);

                        selectPermanentEffect.SetUpCustomMessage("Select 1 Tamer to place the chosen card under.",
                            "The opponent is selecting 1 Tamer to place the chosen card under.");

                        await selectPermanentEffect.Activate();

                        if (selectedPermanent != null)
                        {
                            await selectedPermanent.AddDigivolutionCardsBottom(selectedCards, activateClass.EffectSourceCard?.InstanceId);
                        }
                    }
                }
            }
        }
        #endregion

        #region When Attacking - ESS
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("DP -2000 if this Digimon has [Xros Heart] trait", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDescription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDescription() =>
                "[When Attacking] If this Digimon has the [Xros Heart] trait, 1 of your opponent's Digimon gets -2000 DP for the turn.";

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.CanTriggerOnAttack(hashtable, card);

            bool CanSelectPermanentCondition(Permanent permanent) =>
                CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                ICardEffect.ResolvePermanentOfThisCard(card).TopCard.EqualsTraits("Xros Heart");

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, CanSelectPermanentCondition))
                {
                    Permanent? selectedPermanent = null;

                    SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                    Task SelectPermanentCoroutine(Permanent permanent)
                    {
                        selectedPermanent = permanent;
                        return Task.CompletedTask;
                    }

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

                    selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will get DP -2000.",
                        "The opponent is selecting 1 Digimon that will get DP -2000.");

                    await selectPermanentEffect.Activate();

                    if (selectedPermanent != null)
                    {
                        await CardEffectCommons.ChangeDigimonDP(
                            targetPermanent: selectedPermanent, changeValue: -2000,
                            effectDuration: EffectDuration.UntilEachTurnEnd, activateClass: activateClass);
                    }
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
