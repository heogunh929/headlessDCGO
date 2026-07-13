// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Alliance.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Alliance.cs factory partial.
// ADAPTATION (substrate only; logic verbatim):
//   * card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card).
//   * coroutine `IEnumerator ActivateCoroutine` (pure delegation `return CardEffectCommons.AllianceProcess(...)`)
//     -> `Task ActivateCoroutine` (return-type swap; ActivateClass.SetUpActivateClass takes Func<Hashtable,Task>).
//   * stripped `using UnityEngine;`.
// Replaces the old mirror-invented `static class Alliance` (.Create; ZERO consumers) plus the monolith's
// invented AllianceSelfEffect/AllianceStaticEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Alliance] on oneself
    public static ICardEffect AllianceSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition)
    {
        Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

        bool CanUseCondition()
        {
            if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
            {
                if (condition == null || condition())
                {
                    return true;
                }
            }

            return false;
        }

        return AllianceEffect(
            targetPermanent: targetPermanent,
            isInheritedEffect: isInheritedEffect,
            condition: CanUseCondition,
            rootCardEffect: null, card);
    }
    #endregion

    #region Trigger effect of [Alliance]
    public static ActivateClass AllianceEffect(Permanent targetPermanent, bool isInheritedEffect, Func<bool> condition, ICardEffect rootCardEffect, CardSource card)
    {
        if (targetPermanent == null) return null;
        if (targetPermanent.TopCard == null) return null;
        if (card == null) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Alliance", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, DataBase.AllianceEffectDiscription());
        activateClass.SetIsInheritedEffect(isInheritedEffect);

        if (rootCardEffect != null)
        {
            activateClass.SetIsInheritedEffect(false);
            activateClass.SetEffectSourcePermanent(targetPermanent);
            activateClass.SetRootCardEffect(rootCardEffect);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, (permanent) => permanent.cardSources.Contains(targetPermanent.TopCard)))
            {
                if (condition == null || condition())
                {
                    return true;
                }
            }

            return false;
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanActivateAlliance(hashtable, card);
        }

        Task ActivateCoroutine(Hashtable _hashtable)
        {
            return CardEffectCommons.AllianceProcess(_hashtable, activateClass, targetPermanent, card);
        }

        return activateClass;
    }
    #endregion

    #region Static effect of [Alliance] to all PermanentCondition Digimon
    public static ActivateClass AllianceStaticEffect(Func<Permanent, bool> permanentCondition, bool isInheritedEffect, CardSource card, Func<bool> condition)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Alliance", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, DataBase.AllianceEffectDiscription());
        activateClass.SetIsInheritedEffect(isInheritedEffect);

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, permanentCondition))
            {
                if (condition == null || condition())
                {
                    return true;
                }
            }

            return false;
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            Permanent attackingPermanent = CardEffectCommons.GetAttackerFromHashtable(hashtable);

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card)
                    && permanent != attackingPermanent
                    && CardEffectCommons.CanActivateSuspendCostEffect(permanent.TopCard);
            }

            return CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(attackingPermanent, card)
                && CardEffectCommons.HasMatchConditionOwnersPermanent(card, CanSelectPermanentCondition)
                && (condition == null || condition());
        }

        Task ActivateCoroutine(Hashtable hashtable)
        {
            Permanent attackingPermanent = CardEffectCommons.GetAttackerFromHashtable(hashtable);
            return CardEffectCommons.AllianceProcess(hashtable, activateClass, attackingPermanent, card);
        }

        return activateClass;
    }
    #endregion
}
