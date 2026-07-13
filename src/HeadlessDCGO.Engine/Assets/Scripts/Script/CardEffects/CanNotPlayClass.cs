// Source: DCGO/Assets/Scripts/Script/CardEffects/CanNotPlayClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanNotPlayClass : ICardEffect, ICanNotPlayCardEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanNotPlayClass : ICardEffect, ICanNotPlayCardEffect
{
    Func<CardSource, bool> _cardCondition = null;
    public void SetUpCanNotPlayClass(Func<CardSource, bool> cardCondition)
    {
        _cardCondition = cardCondition;
    }

    public bool CanNotPlay(CardSource cardSource)
    {
        if (_cardCondition != null)
        {
            if (cardSource != null)
            {
                if (_cardCondition(cardSource))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
