// Source: DCGO/Assets/Scripts/Script/CardEffects/CanSelectDigiXrosClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanSelectDigiXrosClass : ICardEffect, ICanSelectDigiXrosEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanSelectDigiXrosClass : ICardEffect, ICanSelectDigiXrosEffect
{
    public void SetUpCanSelectDigiXrosClass(Func<CardSource, Permanent, bool> CanSelectCondition)
    {
        this.CanSelectCondition = CanSelectCondition;
    }

    Func<CardSource, Permanent, bool> CanSelectCondition { get; set; }

    public bool CanSelect(CardSource cardSource, Permanent permanent)
    {
        if (CanSelectCondition != null)
        {
            if (permanent != null)
            {
                if (permanent.TopCard != null)
                {
                    if (cardSource != null)
                    {
                        if (CanSelectCondition(cardSource, permanent))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
}
