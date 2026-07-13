// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeCardLevelForAssemblyClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeCardLevelForAssemblyClass : ICardEffect, IChangeCardLevelForAssemblyEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeCardLevelForAssemblyClass : ICardEffect, IChangeCardLevelForAssemblyEffect
{
    public void SetUpChangeCardLevelForAssemblyClass(Func<CardSource, List<int>, List<int>> changeCardLevel)
    {
        this.changeCardLevel = changeCardLevel;
    }

    Func<CardSource, List<int>, List<int>> changeCardLevel { get; set; }

    public List<int> ChangeCardLevelForAssembly(List<int> Level, CardSource cardSource)
    {
        if (changeCardLevel != null)
        {
            Level = changeCardLevel(cardSource, Level);
        }

        return Level;
    }
}
