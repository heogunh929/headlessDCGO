// Source: DCGO/Assets/Scripts/Script/CardEffects/AddDetailClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddDetailClass : ICardEffect, IAddDetailEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddDetailClass : ICardEffect, IAddDetailEffect
{
    Func<Permanent, bool> _permanentCondition = null;
    string _detail = null;
    bool _triggerEffect = false;

    public void SetUpAddDetailClass(Func<Permanent, bool> permanentCondition, string detail, bool triggerEffect)
    {
        _permanentCondition = permanentCondition;
        _detail = detail;
        _triggerEffect = triggerEffect;
    }

    public bool PermanentCondition(Permanent permanent)
    {
        return _permanentCondition != null
            && permanent != null
            && permanent.TopCard != null
            && _permanentCondition(permanent);
    }
    public string GetDetail()
    {
        return _detail;
    }

    public bool TriggerEffect()
    {
        return _triggerEffect;
    }
}
