// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/ArmorPurge.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord ASYNC slice) 1:1 mirror of the AS-IS ArmorPurge.cs factory partial.
// ADAPTATION: coroutine `IEnumerator ActivateCoroutine` (pure delegation) -> non-async `Task ActivateCoroutine`
// (return-type swap). stripped `using UnityEngine;`. Replaces the old mirror-invented `static class ArmorPurge`
// (.Create; ZERO consumers) and the monolith's invented ArmorPurgeEffect.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public partial class CardEffectFactory
{
    #region Trigger effect of [Armor Purge]
    public static ActivateClass ArmorPurgeEffect(CardSource card)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Armor Purge", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, DataBase.ArmorPurgeEffectDiscription());
        activateClass.SetHashString($"ArmorPurge_{card.CardID}");

        bool CanUseCondition(Hashtable hashtable)
        {
            if (CardEffectCommons.IsExistOnBattleArea(card))
            {
                if (CardEffectCommons.CanTriggerWhenRemoveField(hashtable, card))
                {
                    return true;
                }
            }

            return false;
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanActivateArmorPurge(card);
        }

        Task ActivateCoroutine(Hashtable _hashtable)
        {
            return CardEffectCommons.ArmorPurgeProcess(card);
        }

        return activateClass;
    }
    #endregion
}
