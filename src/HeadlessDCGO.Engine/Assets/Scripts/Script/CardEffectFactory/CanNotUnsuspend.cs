// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotUnsuspend.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS CanNotUnsuspend.cs factory partial.
// Returns the ported CanNotUnsuspendClass kind-class (CardEffects/CanNotUnsuspendClass.cs).
// ADAPTATION: permanent.TopCard.CanNotBeAffected(ICardEffect) -> CanNotBeAffected(EffectSourceCard?.InstanceId).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CanNotUnsuspendClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that can't unsuspend
    public static CanNotUnsuspendClass CantUnsuspendStaticEffect(Func<Permanent, bool> permanentCondition, bool isInheritedEffect, CardSource card,
    Func<bool> condition, string effectName)
    {
        CanNotUnsuspendClass canNotUnsuspendClass = new CanNotUnsuspendClass();
        canNotUnsuspendClass.SetUpICardEffect(effectName, CanUseCondition, card);
        canNotUnsuspendClass.SetUpCanNotUntapClass(PermanentCondition: PermanentCondition);

        if (isInheritedEffect)
        {
            canNotUnsuspendClass.SetIsInheritedEffect(true);
        }

        bool CanUseCondition(Hashtable hashtable)
        {
            return condition == null || condition();
        }

        bool PermanentCondition(Permanent permanent)
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(canNotUnsuspendClass.EffectSourceCard?.InstanceId))
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        return canNotUnsuspendClass;
    }
    #endregion
}
