// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeCardNamesClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeCardNamesClass : ICardEffect, IChangeCardNamesEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeCardNamesClass : ICardEffect, IChangeCardNamesEffect
{
    public void SetUpChangeCardNamesClass(Func<CardSource, List<string>, List<string>> changeCardNames)
    {
        _changeCardNames = changeCardNames;
    }

    Func<CardSource, List<string>, List<string>> _changeCardNames = null;

    public List<string> ChangeCardNames(List<string> cardNames, CardSource cardSource)
    {
        if (_changeCardNames != null)
        {
            cardNames = _changeCardNames(cardSource, cardNames);
        }

        return cardNames;
    }
}
