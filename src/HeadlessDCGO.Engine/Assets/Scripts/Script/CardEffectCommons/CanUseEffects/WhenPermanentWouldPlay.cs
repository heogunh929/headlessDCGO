// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/WhenPermanentWouldPlay.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "When permanent would be played" effects of 1 permanent

    public static bool CanTriggerWhenPermanentWouldPlay(Hashtable hashtable, Func<CardSource, bool> cardCondition)
    {
        bool IsEvolution = CardEffectCommons.IsEvolution(hashtable);

        if (!IsEvolution)
        {
            CardSource Card = GetCardFromHashtable(hashtable);

            if (Card != null)
            {
                if (cardCondition == null || cardCondition(Card))
                {
                    return true;
                }
            }
        }

        return false;
    }
    #endregion
}
