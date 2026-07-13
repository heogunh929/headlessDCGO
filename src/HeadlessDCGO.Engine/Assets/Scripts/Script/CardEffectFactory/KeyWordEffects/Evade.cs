// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Evade.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Evade.cs factory partial.
// ADAPTATION: card.PermanentOfThisCard() -> ICardEffect.ResolvePermanentOfThisCard(card); coroutine
// `IEnumerator ActivateCoroutine` (has a yield) -> `async Task ActivateCoroutine`; `yield return
// ContinuousController.instance.StartCoroutine(CardEffectCommons.EvadeProcess(...))` -> `await CardEffectCommons.EvadeProcess(...)`;
// stripped `using UnityEngine;`. Replaces the monolith's invented EvadeSelfEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Evade] on oneself
    public static ActivateClass EvadeSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition)
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

        return EvadeEffect(targetPermanent: targetPermanent, isInheritedEffect: isInheritedEffect, condition: CanUseCondition, rootCardEffect: null, card);
    }
    #endregion

    #region Trigger effect of [Evade]
    public static ActivateClass EvadeEffect(Permanent targetPermanent, bool isInheritedEffect, Func<bool> condition, ICardEffect rootCardEffect, CardSource card)
    {
        if (targetPermanent == null) return null;
        if (targetPermanent.TopCard == null) return null;
        if (card == null) return null;

        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Evade", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.EvadeEffectDiscription());
        activateClass.SetHashString($"Evade_{card.CardID}" + (isInheritedEffect ? "_inherited" : ""));
        activateClass.SetIsInheritedEffect(isInheritedEffect);

        if (rootCardEffect != null)
        {
            activateClass.SetIsInheritedEffect(false);
            activateClass.SetEffectSourcePermanent(targetPermanent);
            activateClass.SetRootCardEffect(rootCardEffect);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.CanTriggerEvade(hashtable, targetPermanent))
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
            if (CardEffectCommons.CanActivateEvade(targetPermanent))
            {
                if (condition == null || condition())
                {
                    return true;
                }
            }

            return false;
        }

        async Task ActivateCoroutine(Hashtable _hashtable)
        {
            await CardEffectCommons.EvadeProcess(targetPermanent, activateClass);
        }

        return activateClass;
    }
    #endregion
}
