// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OnEndAttack.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger [On End Attack] effect
    public static bool CanTriggerOnEndAttack(Hashtable hashtable, CardSource card)
    {
        return CanTriggerOnAttack(hashtable, card);
    }
    #endregion
}
