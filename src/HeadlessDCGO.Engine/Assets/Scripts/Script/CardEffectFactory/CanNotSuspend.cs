// Source: DCGO/Assets/Scripts/Script/CardEffectFactory/CanNotSuspend.cs
// (EFFECT-MODEL REBUILD / P4 vertical slice) 1:1 mirror of AS-IS CanNotSuspend.cs factory partial.
// Returns the ported CanNotSuspendClass kind-class (CardEffects/CanNotSuspendClass.cs).
// ADAPTATION: AS-IS permanent.TopCard.CanNotBeAffected(ICardEffect) -> mirror CanNotBeAffected(HeadlessEntityId?)
//   takes the cause effect's source-card instance id, so pass <class>.EffectSourceCard?.InstanceId.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;  // CanNotSuspendClass (kind-class layer)

public partial class CardEffectFactory
{
    #region Static effect that can't suspend
    public static CanNotSuspendClass CantSuspendStaticEffect(Func<Permanent, bool> permanentCondition, bool isInheritedEffect, CardSource card,
    Func<bool> condition, string effectName)
    {
        CanNotSuspendClass canNotUnsuspendClass = new CanNotSuspendClass();
        canNotUnsuspendClass.SetUpICardEffect(effectName, CanUseCondition, card);
        canNotUnsuspendClass.SetUpCanNotSuspendClass(PermanentCondition: PermanentCondition);

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
