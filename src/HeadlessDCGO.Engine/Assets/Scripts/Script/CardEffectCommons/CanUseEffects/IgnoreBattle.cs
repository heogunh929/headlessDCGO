// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/IgnoreBattle.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Ignore battle Security Effect condition
    public static bool CanUseIgnoreBattle(Hashtable hashtable, CardSource card)
    {
        return CanTriggerOptionMainEffect(hashtable, card);
    }
    #endregion
}
