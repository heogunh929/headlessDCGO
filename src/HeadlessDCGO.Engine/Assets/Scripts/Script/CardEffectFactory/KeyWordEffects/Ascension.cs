// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Ascension.cs
// (EFFECT-MODEL REBUILD / P4 KeyWord SYNC slice) 1:1 mirror of the AS-IS Ascension.cs factory partial.
// Returns the ported ActivateClass kind-class (CardEffects/ActivateClass.cs). Replaces the monolith's old
// invented SelfKeywordByNameEffect-based AscensionSelfEffect.
// ADAPTATION (substrate only; logic verbatim): AS-IS ActivateCoroutine returns IEnumerator; the mirror
// ActivateClass.SetUpActivateClass takes Func<Hashtable, Task> (the documented IEnumerator->Task coroutine
// substrate adaptation in ActivateClass.cs), so ActivateCoroutine returns Task.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // ActivateClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Trigger effect of [Ascension] on oneself
    public static ICardEffect AscensionSelfEffect(bool isInheritedEffect, CardSource card, Func<bool> condition, bool isLinkedEffect = false)
    {
        ActivateClass activateClass = new ActivateClass();
        activateClass.SetUpICardEffect("Ascension", CanUseCondition, card);
        activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, DataBase.AscensionEffectDescription());
        activateClass.SetIsInheritedEffect(isInheritedEffect);
        activateClass.SetIsLinkedEffect(isLinkedEffect);

        bool CanUseCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanTriggerAscension(hashtable, card)
                && (condition == null || condition());
        }

        bool CanActivateCondition(Hashtable hashtable)
        {
            return CardEffectCommons.CanActivateAscension(hashtable, card);
        }

        Task ActivateCoroutine(Hashtable _hashtable)
        {
            return CardEffectCommons.AscensionProcess(_hashtable, activateClass, card);
        }

        return activateClass;
    }
    #endregion
}
