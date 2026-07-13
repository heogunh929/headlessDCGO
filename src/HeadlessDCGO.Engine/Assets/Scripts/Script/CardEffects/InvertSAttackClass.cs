// Source: DCGO/Assets/Scripts/Script/CardEffects/InvertSAttackClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class InvertSAttackClass : ICardEffect, IInvertSAttackEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class InvertSAttackClass : ICardEffect, IInvertSAttackEffect
{
    public void SetUpChangeSAttackClass(Func<Permanent, int, int> changeInvertFunc, Func<Permanent, bool> permanentCondition)
    {
        _changeInvertFunc = changeInvertFunc;
        _permanentCondition = permanentCondition;
    }

    Func<Permanent, int, int> _changeInvertFunc = null;
    Func<Permanent, bool> _permanentCondition = null;

    public int InversionValue(Permanent permanent, int invertValue)
    {
        if (PermanentCondition(permanent))
        {
            invertValue = _changeInvertFunc(permanent, invertValue);
        }

        return invertValue;
    }

    public bool PermanentCondition(Permanent permanent)
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
