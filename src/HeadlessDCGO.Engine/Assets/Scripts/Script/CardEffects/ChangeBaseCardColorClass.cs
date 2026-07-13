// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeBaseCardColorClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeBaseCardColorClass : ICardEffect, IChangeBaseCardColorEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeBaseCardColorClass : ICardEffect, IChangeBaseCardColorEffect
{
    public void SetUpChangeBaseCardColorClass(Func<CardSource, List<CardColor>, List<CardColor>> ChangeBaseCardColors)
    {
        this.ChangeBaseCardColors = ChangeBaseCardColors;
    }

    Func<CardSource, List<CardColor>, List<CardColor>> ChangeBaseCardColors { get; set; }
    public List<CardColor> GetBaseCardColors(List<CardColor> BaseCardColors, CardSource cardSource)
    {
        if (ChangeBaseCardColors != null)
        {
            if (cardSource != null)
            {
                BaseCardColors = ChangeBaseCardColors(cardSource, BaseCardColors);
            }
        }

        return BaseCardColors;
    }
}
