// Source: DCGO/Assets/Scripts/Script/CardEffects/DisableEffectClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class DisableEffectClass : ICardEffect, IDisableCardEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class DisableEffectClass : ICardEffect, IDisableCardEffect
{
    Func<ICardEffect, bool> DisableCondition { get; set; }
    public void SetUpDisableEffectClass(Func<ICardEffect, bool> DisableCondition)
    {
        this.DisableCondition = DisableCondition;
    }

    public bool IsDisabled(ICardEffect cardEffect)
    {
        if (DisableCondition(cardEffect))
        {
            return true;
        }

        return false;
    }
}
