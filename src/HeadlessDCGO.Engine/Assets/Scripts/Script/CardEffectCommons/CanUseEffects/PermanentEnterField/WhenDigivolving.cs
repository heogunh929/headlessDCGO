// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/PermanentEnterField/WhenDigivolving.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script;

public static partial class CardEffectCommons
{
    #region Can trigger [When Digivolving] effect
    public static bool CanTriggerWhenDigivolving(Hashtable hashtable, CardSource card, Func<SelectCardEffect.Root, bool> rootCondition = null)
    {
        return CanTriggerOnEnterField(hashtable, card, true, rootCondition);
    }
    #endregion

    #region Can trigger "When permanent digivolves" effect
    public static bool CanTriggerWhenPermanentDigivolving(
        Hashtable hashtable,
        Func<Permanent, bool> permanentCondition,
        Func<SelectCardEffect.Root, bool> rootCondition = null)
    {
        return CanTriggerOnPermanentEnterField(hashtable, permanentCondition, true, rootCondition);
    }
    #endregion


}
