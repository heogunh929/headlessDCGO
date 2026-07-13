// Source: DCGO/Assets/Scripts/Script/CardEffects/AddLinkConditionClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddLinkConditionClass : ICardEffect, IAddLinkConditionEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddLinkConditionClass : ICardEffect, IAddLinkConditionEffect
{
    Func<CardSource, LinkCondition> _getLinkCondition { get; set; }
    public void SetUpAddLinkConditionClass(Func<CardSource, LinkCondition> getLinkCondition)
    {
        _getLinkCondition = getLinkCondition;
    }
    public LinkCondition GetLinkCondition(CardSource cardSource)
    {
        if (cardSource != null)
        {
            if (_getLinkCondition != null)
            {
                if (_getLinkCondition(cardSource) != null)
                {
                    return _getLinkCondition(cardSource);
                }
            }
        }

        return null;
    }
}
