// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeTraitsClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeTraitsClass : ICardEffect, IChangeTraitsEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeTraitsClass : ICardEffect, IChangeTraitsEffect
{
    public void SetUpChangeTraitsClass(Func<CardSource, List<string>, List<string>> changeeTraits)
    {
        this.changeeTraits = changeeTraits;
    }

    Func<CardSource, List<string>, List<string>> changeeTraits { get; set; }

    public List<string> ChangTraits(List<string> Traits, CardSource cardSource)
    {
        if (changeeTraits != null)
        {
            Traits = changeeTraits(cardSource, Traits);
        }

        return Traits;
    }
}
