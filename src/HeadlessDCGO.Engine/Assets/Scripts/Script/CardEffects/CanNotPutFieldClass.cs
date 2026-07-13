// Source: DCGO/Assets/Scripts/Script/CardEffects/CanNotPutFieldClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class CanNotPutFieldClass : ICardEffect, ICanNotPutFieldEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class CanNotPutFieldClass : ICardEffect, ICanNotPutFieldEffect
{
    Func<CardSource, bool> _cardCondition = null;
    Func<ICardEffect, bool> _cardEffectCondition = null;
    public void SetUpCanNotPutFieldClass(Func<CardSource, bool> cardCondition, Func<ICardEffect, bool> cardEffectCondition)
    {
        _cardCondition = cardCondition;
        _cardEffectCondition = cardEffectCondition;
    }

    public bool CanNotPutField(CardSource cardSource, ICardEffect cardEffect)
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
