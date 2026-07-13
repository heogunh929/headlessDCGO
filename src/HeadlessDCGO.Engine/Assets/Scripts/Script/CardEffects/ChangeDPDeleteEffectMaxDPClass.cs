// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeDPDeleteEffectMaxDPClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeDPDeleteEffectMaxDPClass : ICardEffect, IChangeDPDeleteEffectMaxDPEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeDPDeleteEffectMaxDPClass : ICardEffect, IChangeDPDeleteEffectMaxDPEffect
{
    Func<int, ICardEffect, int> _changeMaxDP = null;
    public void SetUpChangeDPDeleteEffectMaxDPClass(Func<int, ICardEffect, int> changeMaxDP)
    {
        _changeMaxDP = changeMaxDP;
    }

    public int GetMaxDP(int maxDP, ICardEffect cardEffect)
    {
        if (cardEffect != null)
        {
            if (_changeMaxDP != null)
            {
                maxDP = _changeMaxDP(maxDP, cardEffect);
            }
        }

        return maxDP;
    }
}
