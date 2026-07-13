// Source: DCGO/Assets/Scripts/Script/CardEffects/AddAssemblyConditionClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddAssemblyConditionClass : ICardEffect, IAddAssemblyConditionEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddAssemblyConditionClass : ICardEffect, IAddAssemblyConditionEffect
{
    Func<CardSource, AssemblyCondition> _getAssemblyCondition { get; set; }
    public void SetUpAddAssemblyConditionClass(Func<CardSource, AssemblyCondition> getAssemblyCondition)
    {
        _getAssemblyCondition = getAssemblyCondition;
    }
    public AssemblyCondition GetAssemblyCondition(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (_getAssemblyCondition != null)
            {
                if (_getAssemblyCondition(cardSource) != null)
                {
                    return _getAssemblyCondition(cardSource);
                }
            }
        }

        return null;
    }
}
