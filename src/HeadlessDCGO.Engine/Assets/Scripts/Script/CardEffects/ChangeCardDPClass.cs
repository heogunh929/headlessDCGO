// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeCardDPClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeCardDPClass : ICardEffect, IChangeCardDPEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeCardDPClass : ICardEffect, IChangeCardDPEffect
{
    Func<CardSource, int, int> _changeDPFunc { get; set; }
    Func<CardSource, bool> _cardSourceCondition { get; set; }
    Func<bool> _isUpDown { get; set; }
    Func<bool> _isMinusDP { get; set; }
    public void SetUpChangeCardDPClass(Func<CardSource, int, int> changeDPFunc, Func<CardSource, bool> cardSourceCondition, Func<bool> isUpDown, Func<bool> isMinusDP)
    {
        _changeDPFunc = changeDPFunc;
        _cardSourceCondition = cardSourceCondition;
        _isUpDown = isUpDown;
        _isMinusDP = isMinusDP;
    }
    public int GetDP(int DP, CardSource cardSource)
    {
        if (_changeDPFunc != null)
        {
            if (cardSource != null)
            {
                if (CardCondition(cardSource))
                {
                    DP = _changeDPFunc(cardSource, DP);
                }
            }
        }

        return DP;
    }

    public bool CardCondition(CardSource cardSource)
    {
        if (_cardSourceCondition != null)
        {
            return _cardSourceCondition(cardSource);
        }

        return false;
    }

    public bool IsUpDown()
    {
        if (_isUpDown != null)
        {
            return _isUpDown();
        }

        return false;
    }

    public bool IsMinusDP()
    {
        if (_isMinusDP != null)
        {
            return _isMinusDP();
        }

        return false;
    }
}
