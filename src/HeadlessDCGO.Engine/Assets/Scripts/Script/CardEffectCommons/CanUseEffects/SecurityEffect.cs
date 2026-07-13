// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/SecurityEffect.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger [Security] effect
    public static bool CanTriggerSecurityEffect(Hashtable hashtable, CardSource card)
    {
        return CanTriggerOptionMainEffect(hashtable, card);
    }
    #endregion
}
