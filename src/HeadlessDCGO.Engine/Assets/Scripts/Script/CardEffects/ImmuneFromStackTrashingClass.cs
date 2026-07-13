// Source: DCGO/Assets/Scripts/Script/CardEffects/ImmuneFromStackTrashingClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ImmuneStackTrashingClass : ICardEffect, IImmuneFromStackTrashingEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ImmuneStackTrashingClass : ICardEffect, IImmuneFromStackTrashingEffect
{
    Func<Permanent, bool> PermanentCondition { get; set; }
    Func<ICardEffect, bool> EffectCondition { get; set; }
    public void SetUpImmuneFromStackTrashingClass(Func<Permanent, bool> PermanentCondition, Func<ICardEffect, bool> EffectCondition)
    {
        this.PermanentCondition = PermanentCondition;
        this.EffectCondition = EffectCondition;
    }

    public bool ImmuneStackTrashing(Permanent permanent, ICardEffect effect)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (EffectCondition != null)
                {
                    if (!EffectCondition(effect))
                    {
                        return false;
                    }
                }

                if (PermanentCondition != null)
                {
                    if (!PermanentCondition(permanent))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        return false;
    }
}
