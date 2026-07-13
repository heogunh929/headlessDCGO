// Source: DCGO/Assets/Scripts/Script/CardEffects/ChangePermanentLevelClass.cs
// (EFFECT-MODEL REBUILD / kind-class) 1:1 mirror of AS-IS public class ChangePermanentLevelClass : ICardEffect, IChangePermanentLevelEffect

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

public class ChangePermanentLevelClass : ICardEffect, IChangePermanentLevelEffect
{
    Func<Permanent, int, int> GetLevel { get; set; } = null;
    public void SetUpChangePermanentLevelClass(Func<Permanent, int, int> GetLevel)
    {
        this.GetLevel = GetLevel;
    }

    public int GetPermanentLevel(int level, Permanent permanent)
    {
        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (GetLevel != null)
                {
                    level = GetLevel(permanent, level);
                }
            }
        }

        return level;
    }
}
