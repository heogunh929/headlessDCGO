// Source: DCGO/Assets/Scripts/Script/CardEffects/CanNotMoveClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanNotMoveClass : ICardEffect, ICanNotMoveEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanNotMoveClass : ICardEffect, ICanNotMoveEffect
{
    Func<CardSource, bool> _cardCondition = null;
    Func<ICardEffect, bool> _cardEffectCondition = null;
    public void SetUpCanNotMoveClass(Func<CardSource, bool> cardCondition, Func<ICardEffect, bool> cardEffectCondition)
    {
        _cardCondition = cardCondition;
        _cardEffectCondition = cardEffectCondition;
    }

    public bool CanNotMove(CardSource cardSource, ICardEffect cardEffect)
    {
        if (cardSource != null)
        {
            if (_cardCondition != null && _cardEffectCondition != null)
            {
                if (_cardCondition(cardSource) && _cardEffectCondition(cardEffect))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
