// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeBaseCardNameClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeBaseCardNameClass : ICardEffect, IChangeBaseCardNameEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeBaseCardNameClass : ICardEffect, IChangeBaseCardNameEffect
{
    public void SetUpChangeBaseCardNamesClass(Func<CardSource, List<string>, List<string>> changeBaseCardNames)
    {
        this.changeBaseCardNames = changeBaseCardNames;
    }

    Func<CardSource, List<string>, List<string>> changeBaseCardNames { get; set; }

    public List<string> ChangeBaseCardNames(List<string> BaseCardNames, CardSource cardSource)
    {
        if (changeBaseCardNames != null)
        {
            BaseCardNames = changeBaseCardNames(cardSource, BaseCardNames);
        }

        return BaseCardNames;
    }
}
