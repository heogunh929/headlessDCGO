// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeCardColorClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeCardColorClass : ICardEffect, IChangeCardColorEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeCardColorClass : ICardEffect, IChangeCardColorEffect
{
    public void SetUpChangeCardColorClass(Func<CardSource, List<CardColor>, List<CardColor>> ChangeCardColors)
    {
        this.ChangeCardColors = ChangeCardColors;
    }

    Func<CardSource, List<CardColor>, List<CardColor>> ChangeCardColors { get; set; }
    public List<CardColor> GetCardColors(List<CardColor> CardColors, CardSource cardSource)
    {
        if (ChangeCardColors != null)
        {
            if (cardSource != null)
            {
                CardColors = ChangeCardColors(cardSource, CardColors);
            }
        }

        return CardColors;
    }
}
