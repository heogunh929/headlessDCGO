// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Fortitude.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Fortitude.cs factory partial.
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card); coroutine
// `IEnumerator ActivateCoroutine` (has a yield) -> `async Task ActivateCoroutine`; `yield return
// ContinuousController.instance.StartCoroutine(CardEffectCommons.FortitudeProcess(...))` -> `await CardEffectCommons.FortitudeProcess(...)`;
// stripped `using UnityEngine;`. Replaces the monolith's invented FortitudeSelfEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Fortitude] on oneself
    public static ActivateClass FortitudeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition)
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

        return FortitudeEffect(
            targetPermanent: targetPermanent,
            isInheritedEffect: isInheritedEffect,
            condition: CanUseCondition,
            rootCardEffect: null, card);
    }
    #endregion

    #region Trigger effect of [Fortitude]
    public static ActivateClass FortitudeEffect(Permanent targetPermanent, bool isInheritedEffect, Func<bool> condition, ICardEffect rootCardEffect, CardSource card)
    {
        if (targetPermanent == null) return null;
        if (targetPermanent.TopCard == null) return null;
        if (card == null) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Fortitude", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, DataBase.FortitudeEffectDiscription());
        activateClass.SetHashString($"Fortitude_{card.CardNumber}" + (isInheritedEffect ? "_inherited" : ""));
        activateClass.SetIsInheritedEffect(isInheritedEffect);

        if (rootCardEffect != null)
        {
            activateClass.SetIsInheritedEffect(false);
            activateClass.SetEffectSourcePermanent(targetPermanent);
            activateClass.SetRootCardEffect(rootCardEffect);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanTriggerFortitude(hashtable, card))
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
            return CardEffectCommons.CanActivateFortitude(hashtable, card, isInheritedEffect, activateClass);
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            await CardEffectCommons.FortitudeProcess(_hashtable, card, activateClass);
        }

        return activateClass;
    }
    #endregion
}
