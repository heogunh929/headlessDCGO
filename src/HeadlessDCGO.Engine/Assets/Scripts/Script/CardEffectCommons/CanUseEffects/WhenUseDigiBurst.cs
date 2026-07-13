// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/WhenUseDigiBurst.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "When permanent uses Digi-Burst" effects

    public static bool CanTriggerWhenUseDigiBurst(Hashtable hashtable, Func<Permanent, bool> permanentCondition, Func<ICardEffect, bool> cardEffectCondition)
    {
        Permanent permanent = GetPermanentFromHashtable(hashtable);

        if (permanent != null)
        {
            if (permanent.TopCard != null)
            {
                if (permanentCondition == null || permanentCondition(permanent))
                {
                    ICardEffect CardEffect = GetCardEffectFromHashtable(hashtable);

                    if (CardEffect != null)
                    {
                        if (cardEffectCondition == null || cardEffectCondition(CardEffect))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion
}
