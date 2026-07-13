// Source: DCGO/Assets/Scripts/Script/CardEffects/CanNotBeDestroyedClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanNotBeDestroyedClass : ICardEffect, ICanNotBeDestroyedEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanNotBeDestroyedClass : ICardEffect, ICanNotBeDestroyedEffect
{
    Func<Permanent, bool> _permanentCondition { get; set; }
    public void SetUpCanNotBeDestroyedClass(Func<Permanent, bool> permanentCondition)
    {
        _permanentCondition = permanentCondition;
    }

    public bool CanNotBeDestroyed(Permanent permanent)
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
