// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotBeDeleted.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS CanNotBeDeleted.cs factory partial.
// Returns the ported CanNotBeDestroyedClass kind-class (CardEffects/CanNotBeDestroyedClass.cs).
// ADAPTATION: permanent.TopCard.CanNotBeAffected(ICardEffect) -> CanNotBeAffected(EffectSourceCard?.InstanceId).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CanNotBeDestroyedClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that can't be deleted
    public static CanNotBeDestroyedClass CanNotBeDestroyedStaticEffect(Func<Permanent, bool> permanentCondition, bool isInheritedEffect, CardSource card, Func<bool> condition, string effectName)
    {
        CanNotBeDestroyedClass canNotBeDestroyedClass = new CanNotBeDestroyedClass();
        canNotBeDestroyedClass.SetUpICardEffect(effectName, CanUseCondition, card);
        canNotBeDestroyedClass.SetUpCanNotBeDestroyedClass(permanentCondition: PermanentCondition);

        if (isInheritedEffect)
        {
            canNotBeDestroyedClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        bool PermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(canNotBeDestroyedClass))
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        return canNotBeDestroyedClass;
    }
    #endregion
}
