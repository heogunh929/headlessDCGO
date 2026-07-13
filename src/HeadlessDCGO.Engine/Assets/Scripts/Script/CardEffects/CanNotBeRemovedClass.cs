// Source: DCGO/Assets/Scripts/Script/CardEffects/CanNotBeRemovedClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanNotBeRemovedClass : ICardEffect, ICanNotBeRemovedEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanNotBeRemovedClass : ICardEffect, ICanNotBeRemovedEffect
{
    Func<Permanent, bool> _permanentCondition { get; set; }
    public void SetUpCanNotBeRemovedClass(Func<Permanent, bool> permanentCondition)
    {
        _permanentCondition = permanentCondition;
    }

    public bool CanNotBeRemoved(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (_permanentCondition != null)
                {
                    if (_permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
