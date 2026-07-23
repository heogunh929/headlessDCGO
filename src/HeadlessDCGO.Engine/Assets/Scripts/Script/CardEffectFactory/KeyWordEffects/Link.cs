// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Link.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Link.cs factory partial.
// ADAPTATION (substrate only; logic verbatim):
//   * card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card).
//   * coroutine `IEnumerator ActivateCoroutine` (has yields) -> `async Task`; nested `IEnumerator
//     SelectPermanentCoroutine` -> `async Task`; `yield return StartCoroutine(X)` -> `await X`; lone
//     `yield return null` -> `await Task.CompletedTask;`.
//   * stripped `using UnityEngine;`. Replaces the monolith's invented LinkEffect + GrantedReduceLinkCostClass.
//   * (G-Link batch 2, RD-P6C2-7 RESOLVED) ActivateCoroutine is the AS-IS body (Link.cs:65-101):
//     SelectPermanentEffect(maxCount=min(1,count), canNoSelect:false) -> new ILinkCard(true, card,
//     selectedPermanent, activateClass).LinkCard(). SelectPermanentEffect's id-based canTargetCondition uses
//     the PermanentOf(id) adapter (the ArtsDigivolve/BT19_091/BT25_104 idiom); card-context
//     MatchConditionPermanentCount(card, cond) is the mirror overload of the AS-IS card-less form.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public partial class CardEffectFactory
{
    #region Trigger effect of Link
    /// <summary>
    /// Linking 1 digimon in hand/field to another digimon
    /// </summary>
    /// <param name="CardSource">card being linked</param>
    /// <param name="int">cost for link</param>
    /// <param name="int">DP buff</param>
    /// <param name="Func(bool)">condition for you to be able to link</param>
    /// <param name="Func(Permanent,bool)">Any permaent condtion for you to link to</param>
    /// <author>Mike Bunch</author>
    public static ActivateClass LinkEffect(CardSource card, Func<bool> condition = null)
    {
        if (card == null) return null;
        if (!CardEffectCommons.IsOwnerTurn(card)) return null;
        if (!CardEffectCommons.IsExistOnHand(card) && !CardEffectCommons.IsExistOnBattleAreaDigimon(card)) return null;
        if (!CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition)) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect($"Link (Cost: {card.LinkConditionOf()?.cost})", CanUseCondition, card);
        activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, true, DataBase.LinkEffectDiscription());

        bool CanSelectPermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
            {
                if(permanent != ICardEffect.ResolvePermanentOfThisCard(card))
                {
                    if (permanent.IsDigimon)
                    {
                        if (card.LinkConditionOf() is not { } linkCondition || linkCondition.digimonCondition(permanent))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.IsExistOnHand(card) || (CardEffectCommons.IsExistOnBattleAreaDigimon(card) && !card.IsLinked))
            {
                if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                {
                    if (condition == null || condition())
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            Permanent selectedPermanent = null;

            int maxCount = Math.Min(1, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            selectPermanentEffect.SetUp(
                selectPlayer: card.Owner,
                canTargetCondition: CanSelectPermanentCondition,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: maxCount,
                canNoSelect: false,
                canEndNotMax: false,
                selectPermanentCoroutine: SelectPermanentCoroutine,
                afterSelectPermanentCoroutine: null,
                mode: SelectPermanentEffect.Mode.Custom,
                cardEffect: activateClass);

            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to link.", "The opponent is selecting 1 Digimon to link.");

            await selectPermanentEffect.Activate();

            async Task SelectPermanentCoroutine(Permanent permanent)
            {
                selectedPermanent = permanent;

                await Task.CompletedTask;
            }

            if (selectedPermanent != null)
            {
                await new ILinkCard(true, card, selectedPermanent, activateClass).LinkCard();
            }
        }

        return activateClass;
    }
    #endregion

    #region Granted Link Cost Reduction
    public static ChangeLinkCostClass GrantedReduceLinkCostClass(CardSource card, int reducedCost, Func<CardSource, bool> cardSourceCondition, Func<Permanent, bool> permanentCondition, Func<SelectCardEffect.Root, bool> rootCondition)
    {
        return ReduceLinkCostClass(card, _ => true, false, false, reducedCost, cardSourceCondition, permanentCondition, rootCondition);
    }
    #endregion

    #region Get when would link granted cost reduction
    public static ChangeLinkCostClass GrantedChangeLinkCostClass(CardSource card, string effectName, Func<CardSource, Permanent, int, SelectCardEffect.Root, int> changeCostFunc, Func<CardSource, bool> cardSourceCondition, Func<Permanent, bool> permanentCondition, Func<SelectCardEffect.Root, bool> rootCondition, Func<bool> isUpDown)
    {
        return ChangeLinkCostClass(card, _ => true, effectName, false, false, changeCostFunc, cardSourceCondition, permanentCondition, rootCondition, isUpDown);
    }
    #endregion

    #region GetChangeLinkCostClass
    public static ChangeLinkCostClass ChangeLinkCostClass(CardSource card, Func<Hashtable, bool> canUseCondition, string effectName, bool isInheritedEffect, bool isOptional, Func<CardSource, Permanent, int, SelectCardEffect.Root, int> changeCostFunc, Func<CardSource, bool> cardSourceCondition, Func<Permanent, bool> permanentCondition, Func<SelectCardEffect.Root, bool> rootCondition, Func<bool> isUpDown)
    {
        ChangeLinkCostClass changeLinkCostClass = new ();
        changeLinkCostClass.SetUpICardEffect(effectName, canUseCondition, card);
        changeLinkCostClass.SetUpChangeLinkCostClass(changeCostFunc, cardSourceCondition, permanentCondition, rootCondition, isUpDown);
        changeLinkCostClass.SetNotShowUI(isOptional);
        changeLinkCostClass.SetIsInheritedEffect(isInheritedEffect);
        return changeLinkCostClass;
    }
    #endregion

    #region GetChangeLinkCostClass for Cost Reduction
    public static ChangeLinkCostClass ReduceLinkCostClass(CardSource card, Func<Hashtable, bool> canUseCondition, bool isInheritedEffect, bool isOptional, int reducedCost, Func<CardSource, bool> cardSourceCondition, Func<Permanent, bool> permanentCondition, Func<SelectCardEffect.Root, bool> rootCondition)
    {
        return ChangeLinkCostClass(card, canUseCondition, $"Link Cost -{reducedCost}", isInheritedEffect, isOptional, ChangeCost, cardSourceCondition, permanentCondition, rootCondition, () => true);

        int ChangeCost(CardSource cardSource, Permanent targetPermanent, int Cost, SelectCardEffect.Root root)
        {
            if (cardSourceCondition(cardSource))
            {
                if (rootCondition(root))
                {
                    if (permanentCondition(targetPermanent))
                    {
                        Cost -= reducedCost;
                    }
                }
            }

            return Cost;
        }
    }
    #endregion
}
