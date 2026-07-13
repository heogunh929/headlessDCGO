// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OnAttackTargetSwitch.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "when this permanent 's attack target switched" effect
    public static bool CanTriggerOnAttackTargetSwitch(Hashtable hashtable, CardSource card)
    {
        return CanTriggerOnPermanentAttackTargetSwitch(hashtable, permanent => permanent.cardSources.Contains(card));
    }
    #endregion

    #region Can trigger "when attack target switched" effect
    public static bool CanTriggerOnPermanentAttackTargetSwitch(Hashtable hashtable, Func<Permanent, bool> permanentCondition)
    {
        return CanTriggerOnPermanentAttack(hashtable, permanentCondition);
    }
    #endregion
}
