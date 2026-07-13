// Source: DCGO/Assets/Scripts/Script/CardEffects/AddMaxTrashCountDigiXrosClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddMaxTrashCountDigiXrosClass : ICardEffect, IAddMaxTrashCountDigiXrosEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddMaxTrashCountDigiXrosClass : ICardEffect, IAddMaxTrashCountDigiXrosEffect
{
    Func<CardSource, int> _getMaxTrashCount { get; set; }
    public void SetUpAddMaxTrashCountDigiXrosClass(Func<CardSource, int> getMaxTrashCount)
    {
        _getMaxTrashCount = getMaxTrashCount;
    }
    public int GetMaxTrashCount(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (_getMaxTrashCount != null)
            {
                return _getMaxTrashCount(cardSource);
            }
        }

        return 0;
    }
}
