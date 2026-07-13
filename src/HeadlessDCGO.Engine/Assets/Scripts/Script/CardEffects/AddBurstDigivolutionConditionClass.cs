// Source: DCGO/Assets/Scripts/Script/CardEffects/AddBurstDigivolutionConditionClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddBurstDigivolutionConditionClass : ICardEffect, IAddBurstDigivolutionConditionEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddBurstDigivolutionConditionClass : ICardEffect, IAddBurstDigivolutionConditionEffect
{
    Func<CardSource, BurstDigivolutionCondition> _getBurstDigivolutionCondition { get; set; }
    public void SetUpAddBurstDigivolutionConditionClass(Func<CardSource, BurstDigivolutionCondition> getBurstDigivolutionCondition)
    {
        _getBurstDigivolutionCondition = getBurstDigivolutionCondition;
    }
    public BurstDigivolutionCondition GetBurstDigivolutionCondition(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (_getBurstDigivolutionCondition != null)
            {
                if (_getBurstDigivolutionCondition(cardSource) != null)
                {
                    return _getBurstDigivolutionCondition(cardSource);
                }
            }
        }

        return null;
    }
}
