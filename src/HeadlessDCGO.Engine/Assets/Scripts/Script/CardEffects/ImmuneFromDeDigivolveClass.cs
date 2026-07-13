// Source: DCGO/Assets/Scripts/Script/CardEffects/ImmuneFromDeDigivolveClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ImmuneFromDeDigivolveClass : ICardEffect, IImmuneFromDeDigivolveEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ImmuneFromDeDigivolveClass : ICardEffect, IImmuneFromDeDigivolveEffect
{
    Func<Permanent, bool> PermanentCondition { get; set; }
    public void SetUpImmuneFromDeDigivolveClass(Func<Permanent, bool> PermanentCondition)
    {
        this.PermanentCondition = PermanentCondition;
    }

    public bool ImmuneDeDigivolve(Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (PermanentCondition != null)
                {
                    if (PermanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
