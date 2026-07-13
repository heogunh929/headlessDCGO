// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Save.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS Save.cs factory partial.
// ADAPTATION: coroutine `IEnumerator ActivateCoroutine` (pure delegation) -> non-async `Task ActivateCoroutine`;
// stripped `using UnityEngine;`. Replaces the monolith's invented SaveEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Save]
    public static ActivateClass SaveEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Save", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.SaveEffectDiscription());

        bool CanSelectPermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnOwnerBattleArea(permanent, card))
            {
                if (permanent.IsTamer)
                {
                    if (!permanent.IsToken)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerOnDeletion(hashtable, card);
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanActivateSave(hashtable, CanSelectPermanentCondition);
        }

        Task ActivateCoroutine(Hashtable _hashtable)
        {
            return CardEffectCommons.SaveProcess(_hashtable, activateClass, card, CanSelectPermanentCondition);
        }

        return activateClass;
    }
    #endregion
}
