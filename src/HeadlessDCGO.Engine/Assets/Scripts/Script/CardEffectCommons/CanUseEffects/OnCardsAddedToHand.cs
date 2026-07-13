// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OnCardsAddedToHand.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "an effect adds a card to 1 player's hand" effect
    public static bool CanTriggerOnHandAdded(Hashtable hashtable, Player player, Func<ICardEffect, bool> cardEffectCondition)
    {
        List<Player> Players = GetPlayersFromHashtable(hashtable);

        if (Players != null)
        {
            if (Players.Contains(player))
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

        return false;
    }
    #endregion
}
