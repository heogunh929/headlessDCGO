// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Retaliation.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Retaliation.cs factory partial.
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card); coroutine
// `IEnumerator ActivateCoroutine` (pure delegation) -> non-async `Task ActivateCoroutine`; stripped
// `using UnityEngine;` / `using UnityEngine.Pool;`. Replaces the old mirror-invented `static class Retaliation`
// (.Create; ZERO consumers) plus the monolith's invented RetaliationSelfEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Retaliation] on oneself
    public static ICardEffect RetaliationSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition, bool isLinkedEffect = false)
    {
        Permanent targetPermanent = ICardEffect.ResolvePermanentOfThisCard(card) ?? new Permanent(card.Context, card.InstanceId, card.Owner);

        bool CanUseCondition()
        {
            if (condition == null || condition())
            {
                return true;
            }

            return false;
        }

        return RetaliationEffect(targetPermanent: targetPermanent, isInheritedEffect: isInheritedEffect, condition: CanUseCondition, rootCardEffect: null, card, isLinkedEffect);
    }
    #endregion

    #region Trigger effect of [Retaliation]
    public static ActivateClass RetaliationEffect(Permanent targetPermanent, bool isInheritedEffect, Func<bool> condition, ICardEffect rootCardEffect, CardSource card, bool isLinkedEffect = false)
    {
        if (targetPermanent == null) return null;
        if (targetPermanent.TopCard == null) return null;
        if (card == null) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Retaliation", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, DataBase.RetaliationEffectDiscription());
        activateClass.SetIsInheritedEffect(isInheritedEffect);
        activateClass.SetIsLinkedEffect(isLinkedEffect);

        if (rootCardEffect != null)
        {
            activateClass.SetIsInheritedEffect(false);
            activateClass.SetEffectSourcePermanent(targetPermanent);
            activateClass.SetRootCardEffect(rootCardEffect);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, (permanent) => permanent.cardSources.Contains(targetPermanent.TopCard)))
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
            return CardEffectCommons.CanActivateRetaliation(hashtable);
        }

        Task ActivateCoroutine(Hashtable _hashtable)
        {
            return CardEffectCommons.RetaliationProcess(_hashtable, activateClass);
        }

        return activateClass;
    }
    #endregion
}
