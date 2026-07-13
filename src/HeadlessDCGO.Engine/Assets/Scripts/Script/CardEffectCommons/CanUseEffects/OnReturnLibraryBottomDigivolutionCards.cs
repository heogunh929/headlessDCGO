// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OnReturnLibraryBottomDigivolutionCards.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "When card is returned to library bottom from this permanent due to effect" effect
    public static bool CanTriggerOnReturnToLibraryBottomDigivolutionCard(Hashtable hashtable, Func<CardSource, bool> cardCondition, CardSource card)
    {
        if (IsExistOnBattleArea(card))
        {
            if (hashtable != null)
            {
                Permanent Permanent = GetPermanentFromHashtable(hashtable);

                if (Permanent != null)
                {
                    if (Permanent == ICardEffect.ResolvePermanentOfThisCard(card))
                    {
                        List<CardSource> DeckBottomCards = GetDeckBottomCardsFromHashtable(hashtable);

                        if (DeckBottomCards != null)
                        {
                            if (DeckBottomCards.Count((cardSource) => Permanent.DigivolutionCards.Contains(cardSource) && (cardCondition == null || cardCondition(cardSource))) >= 1)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion
}
