// Source: DCGO/Assets/Scripts/Script/CardEffects/TreatAsDigimonClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class TreatAsDigimonClass : ICardEffect, ITreatAsDigimonEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class TreatAsDigimonClass : ICardEffect, ITreatAsDigimonEffect
{
    Func<Permanent, bool> PermanentCondition { get; set; }
    public void SetUpTreatAsDigimonClass(Func<Permanent, bool> permanentCondition)
    {
        this.PermanentCondition = permanentCondition;
    }

    public bool IsDigimon(Permanent permanent)
    {
        if (PermanentCondition != null && permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (PermanentCondition(permanent))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
