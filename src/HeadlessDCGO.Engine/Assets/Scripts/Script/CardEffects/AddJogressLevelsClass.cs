// Source: DCGO/Assets/Scripts/Script/CardEffects/AddJogressLevelsClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class AddJogressLevelsClass : ICardEffect, IAddJogressLevelsEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class AddJogressLevelsClass : ICardEffect, IAddJogressLevelsEffect
{
    Func<CardSource, Permanent, List<int>> _getJogressLevels { get; set; }
    public void SetUpAddJogressLevelsClass(Func<CardSource, Permanent, List<int>> getJogressLevels)
    {
        _getJogressLevels = getJogressLevels;
    }

    public List<int> GetJogressLevels(CardSource cardSource, Permanent permanent)
    {
        if (cardSource != null)
        {
            if (permanent != null)
            {
                if (permanent.TopCard != null)
                {
                    if (_getJogressLevels != null)
                    {
                        return _getJogressLevels(cardSource, permanent);
                    }
                }
            }
        }

        return null;
    }
}
