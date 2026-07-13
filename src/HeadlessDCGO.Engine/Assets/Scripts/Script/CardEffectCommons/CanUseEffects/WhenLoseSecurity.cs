// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/WhenLoseSecurity.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "when security cards lose" effect
    public static bool CanTriggerWhenLoseSecurity(Hashtable hashtable, Func<Player, bool> playerCondition)
    {
        Player Player = GetPlayerFromHashtable(hashtable);

        if (Player != null)
        {
            if (playerCondition == null || playerCondition(Player))
            {
                return true;
            }
        }

        return false;
    }
    #endregion
}
