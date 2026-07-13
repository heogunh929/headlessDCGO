// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Pierce.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Pierce.cs factory partial.
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card); coroutine
// `IEnumerator ActivateCoroutine` (pure delegation) -> non-async `Task ActivateCoroutine`; stripped
// `using UnityEngine;`. Replaces the old mirror-invented `static class Pierce` (.Create; ZERO consumers) plus
// the monolith's invented PierceSelfEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Pierce] on oneself
    public static ActivateClass PierceSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition, bool isLinkedEffect = false)
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

        return PierceEffect(targetPermanent: targetPermanent, isInheritedEffect: isInheritedEffect, condition: CanUseCondition, rootCardEffect: null, card, isLinkedEffect: isLinkedEffect);
    }
    #endregion

    #region Trigger effect of [Pierce]
    public static ActivateClass PierceEffect(Permanent targetPermanent, bool isInheritedEffect, Func<bool> condition, ICardEffect rootCardEffect, CardSource card, bool isLinkedEffect = false)
    {
        if (targetPermanent == null) return null;
        if (targetPermanent.TopCard == null) return null;
        if (card == null) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Pierce", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, DataBase.PierceEffectDiscription());
        activateClass.SetIsInheritedEffect(isInheritedEffect);
        activateClass.SetIsLinkedEffect(isLinkedEffect);

        if (rootCardEffect != null)
        {
            activateClass.SetIsInheritedEffect(false);
            activateClass.SetIsLinkedEffect(false);
            activateClass.SetEffectSourcePermanent(targetPermanent);
            activateClass.SetRootCardEffect(rootCardEffect);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanTriggerPierce(hashtable, targetPermanent))
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
            if (CardEffectCommons.CanActivatePierce(targetPermanent))
            {
                if (condition == null || condition())
                {
                    return true;
                }
            }

            return false;
        }

        Task ActivateCoroutine(Hashtable _hashtable)
        {
            return CardEffectCommons.PierceProcess();
        }

        return activateClass;
    }
    #endregion
}
