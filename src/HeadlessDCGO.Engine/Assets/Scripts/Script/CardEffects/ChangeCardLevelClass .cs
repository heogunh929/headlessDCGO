// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangeCardLevelClass .cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangeCardLevelClass : ICardEffect, IChangeCardLevelEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangeCardLevelClass : ICardEffect, IChangeCardLevelEffect
{
    Func<CardSource, int, int> GetLevel { get; set; } = null;
    public void SetUpChangeCardLevelClass(Func<CardSource, int, int> GetLevel)
    {
        this.GetLevel = GetLevel;
    }

    public int GetCardLevel(int level, CardSource card)
    {
        if (card != null)
        {
            if (GetLevel != null)
            {
                level = GetLevel(card, level);
            }
        }

        return level;
    }
}
