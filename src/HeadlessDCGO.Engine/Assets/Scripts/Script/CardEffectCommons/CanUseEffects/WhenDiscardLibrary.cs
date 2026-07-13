// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/WhenDiscardLibrary.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "when this permanent suspends" effect
    public static bool CanTriggerWhenSelfDiscardLibrary(Hashtable hashtable, CardSource card)
    {
        return CanTriggerWhenDiscardLibrary(hashtable, (cardSource) => cardSource == card);
    }
    #endregion

    #region Can trigger "when permanent suspends" effect
    public static bool CanTriggerWhenDiscardLibrary(Hashtable hashtable, Func<CardSource, bool> cardCondition)
    {
        List<CardSource> DiscardedCards = GetDiscardedCardsFromHashtable(hashtable);

        if (DiscardedCards != null)
        {
            if (DiscardedCards.Some((cardSource) =>
            cardSource != null
            && !cardSource.IsBeingRevealed
            && (cardCondition == null || cardCondition(cardSource))))
            {
                return true;
            }
        }

        return false;
    }
    #endregion
}
