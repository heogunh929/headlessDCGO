// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OnCardsReturnToHandFromTrash.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "When your cards return to Hand from tash" effects

    public static bool CanTriggerWhenOwnerCardsReturnToHandFromTrash(Hashtable hashtable, Func<CardSource, bool> cardCondition, CardSource card)
    {
        bool CardCondition(CardSource cardSource) => cardSource.Owner == card.Owner && (cardCondition == null || cardCondition(cardSource));

        return CanTriggerWhenCardsReturnToHandFromTrash(hashtable, CardCondition, card);
    }
    #endregion

    #region Can trigger "When cards return to hand from tash" effects

    public static bool CanTriggerWhenCardsReturnToHandFromTrash(Hashtable hashtable, Func<CardSource, bool> cardCondition, CardSource card)
    {
        List<CardSource> CardSources = GetCardSourcesFromHashtable(hashtable);

        if (CardSources != null)
        {
            CardSources = CardSources.Filter(cardSource => !cardSource.IsDigiEgg && (cardCondition == null || cardCondition(cardSource)));

            if (CardSources.Count >= 1)
            {
                return true;
            }
        }

        return false;
    }
    #endregion
}
