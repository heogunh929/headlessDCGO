// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/WhendAddSecurity.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "when security cards added" effect
    public static bool CanTriggerWhenAddSecurity(Hashtable hashtable, Func<Player, bool> playerCondition)
    {
        return CanTriggerWhenLoseSecurity(hashtable, playerCondition);
    }
    #endregion
}
