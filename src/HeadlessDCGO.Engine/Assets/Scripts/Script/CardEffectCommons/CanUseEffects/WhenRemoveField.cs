// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/WhenRemoveField.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "When this permanent would leave the battle area" effects of 1 card

    public static bool CanTriggerWhenRemoveField(Hashtable hashtable, CardSource card)
    {
        return CanTriggerWhenPermanentRemoveField(hashtable, (permanent) => permanent.cardSources.Contains(card));
    }
    #endregion

    #region Can trigger "When this permanent would leave the battle area" effects of 1 permanent

    public static bool CanTriggerWhenPermanentRemoveField(Hashtable hashtable, Func<Permanent, bool> permanentCondition)
    {
        List<Permanent> permanents = GetPermanentsFromHashtable(hashtable);

        if (permanents != null && permanentCondition != null)
        {
            if (permanents.Count((permanent) => permanent != null && permanent.TopCard != null && permanentCondition(permanent)) >= 1)
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Can trigger "When top card of a permanent is trashed" effects of 1 permanent

    public static bool CanTriggerWhenTopCardTrashed(Hashtable hashtable, Func<CardSource, bool> cardCondition)
    {
        List<CardSource> cards = GetCardSourcesFromHashtable(hashtable);

        if (cards != null && cardCondition != null)
        {
            if (cards.Count((card) => card != null && cardCondition(card)) >= 1)
            {
                return true;
            }
        }

        return false;
    }
    #endregion
}
