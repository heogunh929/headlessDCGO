// Source: DCGO/Assets/Scripts/Script/CardEffects/AddDigiXrosConditionClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddDigiXrosConditionClass : ICardEffect, IAddDigiXrosConditionEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddDigiXrosConditionClass : ICardEffect, IAddDigiXrosConditionEffect
{
    Func<CardSource, DigiXrosCondition> _getDigiXrosCondition { get; set; }
    public void SetUpAddDigiXrosConditionClass(Func<CardSource, DigiXrosCondition> getDigiXrosCondition)
    {
        _getDigiXrosCondition = getDigiXrosCondition;
    }
    public DigiXrosCondition GetDigiXrosCondition(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (_getDigiXrosCondition != null)
            {
                if (_getDigiXrosCondition(cardSource) != null)
                {
                    return _getDigiXrosCondition(cardSource);
                }
            }
        }

        return null;
    }
}
