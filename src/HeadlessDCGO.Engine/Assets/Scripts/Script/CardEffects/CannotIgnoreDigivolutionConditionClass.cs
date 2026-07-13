// Source: DCGO/Assets/Scripts/Script/CardEffects/CannotIgnoreDigivolutionConditionClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CannotIgnoreDigivolutionConditionClass : ICardEffect, ICannotIgnoreDigivolutionConditionEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CannotIgnoreDigivolutionConditionClass : ICardEffect, ICannotIgnoreDigivolutionConditionEffect
{
    public void SetUpCannotIgnoreDigivolutionConditionClass(Func<Player, Permanent, CardSource, bool> IgnoreDigivolutionCondition)
    {
        this.IgnoreDigivolutionCondition = IgnoreDigivolutionCondition;
    }

    Func<Player, Permanent, CardSource, bool> IgnoreDigivolutionCondition;

    public bool cannotIgnoreDigivolutionCondition(Player player, Permanent targetPermanent, CardSource cardSource)
    {
        if (player != null && targetPermanent != null && cardSource != null)
        {
            if (targetPermanent.TopCard != null)
            {
                if (IgnoreDigivolutionCondition != null)
                {
                    if (IgnoreDigivolutionCondition(player, targetPermanent, cardSource))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
