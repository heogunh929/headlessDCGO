// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OnTrashBySelfDigiBurst.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    #region Can trigger "When this digivolution card is trashed due to activating this Digimon's <Digi-Burst>" effect
    public static bool CanTriggerOnTrashBySelfDigiBurst(Hashtable hashtable, CardSource card)
    {
        bool CardEffectCondition(ICardEffect cardEffect)
        {
            if (cardEffect != null)
            {
                if (!string.IsNullOrEmpty(cardEffect.EffectDiscription))
                {
                    if (cardEffect.EffectDiscription.Contains("Digi-Burst"))
                    {
                        if (cardEffect.EffectSourceCard != null)
                        {
                            if (IsExistOnBattleArea(cardEffect.EffectSourceCard))
                            {
                                if (ICardEffect.ResolvePermanentOfThisCard(cardEffect.EffectSourceCard).cardSources.Contains(card))
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

        return CanTriggerOnTrashSelfDigivolutionCard(hashtable, CardEffectCondition, card);

    }
    #endregion
}
