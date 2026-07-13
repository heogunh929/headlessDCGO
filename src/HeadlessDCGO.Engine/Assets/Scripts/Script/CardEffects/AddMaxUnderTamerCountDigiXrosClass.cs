// Source: DCGO/Assets/Scripts/Script/CardEffects/AddMaxUnderTamerCountDigiXrosClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddMaxUnderTamerCountDigiXrosClass : ICardEffect, IAddMaxUnderTamerCountDigiXrosEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddMaxUnderTamerCountDigiXrosClass : ICardEffect, IAddMaxUnderTamerCountDigiXrosEffect
{
    Func<CardSource, int> _getMaxUnderTamerCount { get; set; }
    public void SetUpAddMaxUnderTamerCountDigiXrosClass(Func<CardSource, int> getMaxUnderTamerCount)
    {
        _getMaxUnderTamerCount = getMaxUnderTamerCount;
    }
    public int getMaxUnderTamerCount(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (_getMaxUnderTamerCount != null)
            {
                return _getMaxUnderTamerCount(cardSource);
            }
        }

        return 0;
    }
}
