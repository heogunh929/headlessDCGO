// Source: DCGO/Assets/Scripts/Script/CardEffects/CannotBlockClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CannotBlockClass : ICardEffect, ICannotBlockEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CannotBlockClass : ICardEffect, ICannotBlockEffect
{
    Func<Permanent, Permanent, bool> _permanentsCondition = null;
    public void SetUpCannotBlockClass(Func<Permanent, Permanent, bool> permanentsCondition)
    {
        _permanentsCondition = permanentsCondition;
    }

    public bool CannotBlock(Permanent attackingPermanent, Permanent defendingPermanent)
    {
        if (CardEffectCommons.IsPermanentExistsOnBattleArea(attackingPermanent))
        {
            if (CardEffectCommons.IsPermanentExistsOnBattleArea(defendingPermanent))
            {
                if (_permanentsCondition != null)
                {
                    if (_permanentsCondition(attackingPermanent, defendingPermanent))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
