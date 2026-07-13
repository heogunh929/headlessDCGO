// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeEndTurnMinMemoryClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeEndTurnMinMemoryClass : ICardEffect, IChangeEndTurnMinMemoryEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeEndTurnMinMemoryClass : ICardEffect, IChangeEndTurnMinMemoryEffect
{
    Func<int, int> _changetMinMemory = null;
    public void SetUpChangeEndTurnMinMemoryClass(Func<int, int> changetMinMemory)
    {
        _changetMinMemory = changetMinMemory;
    }

    public int GetMinMemory(int minMemory)
    {
        if (_changetMinMemory != null)
        {
            minMemory = _changetMinMemory(minMemory);
        }

        return minMemory;
    }
}
