// Source: DCGO/Assets/Scripts/Script/CardEffects/CanSuspendByDigisorptionClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanSuspendByDigisorptionClass : ICardEffect, ICanSuspendByDigisorptionEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanSuspendByDigisorptionClass : ICardEffect, ICanSuspendByDigisorptionEffect
{
    public void SetUpCanSuspendByDigisorptionClass(Func<Permanent, bool> PermanentCondition, Func<ICardEffect, bool> CardEffectCondition, Func<bool> _CheckAvailability)
    {
        this.PermanentCondition = PermanentCondition;
        this.CardEffectCondition = CardEffectCondition;
        this._CheckAvailability = _CheckAvailability;
    }

    Func<Permanent, bool> PermanentCondition { get; set; }
    Func<ICardEffect, bool> CardEffectCondition { get; set; }
    Func<bool> _CheckAvailability { get; set; }

    public bool canSuspendDigisorption(Permanent permanent, ICardEffect cardEffect)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (cardEffect != null)
                {
                    if (PermanentCondition != null)
                    {
                        if (CardEffectCondition != null)
                        {
                            if (PermanentCondition(permanent))
                            {
                                if (CardEffectCondition(cardEffect))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }

        return false;
    }

    public bool isCheckAvailability()
    {
        if (_CheckAvailability != null)
        {
            return _CheckAvailability();
        }

        return false;
    }
}
