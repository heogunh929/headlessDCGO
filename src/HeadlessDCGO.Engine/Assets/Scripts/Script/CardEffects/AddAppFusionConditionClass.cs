// Source: DCGO/Assets/Scripts/Script/CardEffects/AddAppFusionConditionClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddAppFusionConditionClass : ICardEffect, IAddAppFusionConditionEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddAppFusionConditionClass : ICardEffect, IAddAppFusionConditionEffect
{
    Func<CardSource, AppFusionCondition> _getAppFusionCondition { get; set; }
    public void SetUpAddAppFusionConditionClass(Func<CardSource, AppFusionCondition> getAppFusionCondition)
    {
        _getAppFusionCondition = getAppFusionCondition;
    }
    public AppFusionCondition GetAppFusionCondition(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (_getAppFusionCondition != null)
            {
                if (_getAppFusionCondition(cardSource) != null)
                {
                    return _getAppFusionCondition(cardSource);
                }
            }
        }

        return null;
    }
}
