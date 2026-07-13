// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeCardNamesForDigiXrosClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeCardNamesForDigiXrosClass : ICardEffect, IChangeCardNamesForDigiXrosEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeCardNamesForDigiXrosClass : ICardEffect, IChangeCardNamesForDigiXrosEffect
{
    public void SetUpChangeCardNamesForDigiXrosClass(Func<CardSource, List<string>, List<string>> changeCardNames)
    {
        this.changeCardNames = changeCardNames;
    }

    Func<CardSource, List<string>, List<string>> changeCardNames { get; set; }

    public List<string> ChangeCardNamesForDigiXros(List<string> CardNames, CardSource cardSource)
    {
        if (changeCardNames != null)
        {
            CardNames = changeCardNames(cardSource, CardNames);
        }

        return CardNames;
    }
}
