// Source: DCGO/Assets/Scripts/Script/CardEffects/CanNotSwitchAttackTargetClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanNotSwitchAttackTargetClass : ICardEffect, ICanNotSwitchAttackTargetEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanNotSwitchAttackTargetClass : ICardEffect, ICanNotSwitchAttackTargetEffect
{
    Func<Permanent, bool> PermanentCondition { get; set; }
    public void SetUpCanNotSwitchAttackTargetClass(Func<Permanent, bool> PermanentCondition)
    {
        this.PermanentCondition = PermanentCondition;
    }

    public bool CanNotBeSwitchAttackTarget(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (PermanentCondition != null)
                {
                    if (PermanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
