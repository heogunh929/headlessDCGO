// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/CanUseEffects/OnDeletion.cs
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static partial class CardEffectCommons
{
    // ADAPTATION (C2 decision-4 deletion transport, until battle/effect rehousing lands the live IBattle/ICardEffect
    // — RD-C1-CARDEFFECT-IDTHREAD / battle-rehousing non-scope): the sink/battle/deferred deletion openers have NO
    // live IBattle/ICardEffect object at the sink, so they cannot populate the AS-IS "battle"/"CardEffect" hashtable
    // keys that IsByBattle/IsByEffect read. They instead carry the DERIVED boolean cause — byBattle from the loser's
    // MarkDeletedByBattle instance flag, byEffect from the sink mutation's cause-id presence (non-DPZero effect
    // delete) — via these marker keys. IsByBattle/IsByEffect read EITHER the live object (faithful AS-IS card path)
    // OR these markers (transport path) with the SAME truth table. The DP-zero sweep sets NEITHER marker, so it
    // reports IsByBattle=false/IsByEffect=false/DPZero=true exactly as AS-IS's DPZero-only hashtable.
    public const string ByBattleCauseKey = "byBattleCause";
    public const string ByEffectCauseKey = "byEffectCause";

    private static bool ReadCauseMarker(Hashtable hashtable, string key)
    {
        return hashtable != null
            && hashtable.ContainsKey(key)
            && hashtable[key] is bool flag
            && flag;
    }

    #region Can trigger

    #region Can trigger [On Deletion] effect
    public static bool CanTriggerOnDeletion(Hashtable hashtable, CardSource card)
    {
        return CanTriggerOnPermanentDeleted(hashtable, (permanent) => permanent.cardSources.Contains(card));
    }
    #endregion

    #region Can trigger "when permanent is deleted" effect
    public static bool CanTriggerOnPermanentDeleted(Hashtable hashtable, Func<Permanent, bool> permanentCondition)
    {
        List<Hashtable> hashtables = GetHashtablesFromHashtable(hashtable);

        if (hashtables != null)
        {
            foreach (Hashtable hashtable1 in hashtables)
            {
                Permanent permanent = GetPermanentFromHashtable(hashtable1);

                if (permanent != null)
                {
                    if (permanent.TopCard != null)
                    {
                        if (permanentCondition != null)
                        {
                            if (permanentCondition(permanent))
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

    #region Can trigger "when permanent leaves the battle area" effect
    public static bool CanTriggerOnPermanentLeave(Hashtable hashtable, Func<Permanent, bool> permanentCondition)
    {
        List<Hashtable> hashtables = GetHashtablesFromHashtable(hashtable);

        if (hashtables != null)
        {
            foreach (Hashtable hashtable1 in hashtables)
            {
                Permanent permanent = GetPermanentFromHashtable(hashtable1);

                if (permanent != null)
                {
                    if (permanent.TopCard != null)
                    {
                        if (permanentCondition != null)
                        {
                            if (permanentCondition(permanent))
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

    #region Can trigger "when permanent is deleted by battle" effects
    public static bool IsByBattle(Hashtable hashtable)
    {
        // AS-IS: GetBattleFromHashtable(hashtable) != null. Transport path (no live IBattle at the sink): the
        // derived byBattle marker (BattleResolver's MarkDeletedByBattle flag). See the ADAPTATION note above.
        return GetBattleFromHashtable(hashtable) != null || ReadCauseMarker(hashtable, ByBattleCauseKey);
    }
    #endregion

    #region Can trigger "when permanent is deleted or played by effect that satisfies the condition" effects
    public static bool IsByEffect(Hashtable hashtable, Func<ICardEffect, bool> cardEffectCondition)
    {
        ICardEffect CardEffect = GetCardEffectFromHashtable(hashtable);

        if (CardEffect != null)
        {
            if (CardEffect.EffectSourceCard != null)
            {
                if (cardEffectCondition == null || cardEffectCondition(CardEffect))
                {
                    return true;
                }
            }

            return false;
        }

        // Transport path (no live ICardEffect — RD-C1-CARDEFFECT-IDTHREAD): the derived byEffect marker answers the
        // cause-presence question (AS-IS "cardEffect non-null ⇒ by-effect" for the common null-condition case). The
        // cardEffectCondition inspects the LIVE effect and cannot be evaluated until effect-rehousing lands the
        // object; a null condition (deleted by ANY effect) is exact. See the ADAPTATION note above.
        return ReadCauseMarker(hashtable, ByEffectCauseKey);
    }
    #endregion

    #endregion

    #region Can activate

    #region Can activate [On Deletion] effect
    public static bool CanActivateOnDeletion(Hashtable hashtable, CardSource card)
    {
        if (card.IsToken)
            return true;

        List<Hashtable> hashtables = GetHashtablesFromHashtable(hashtable);

        if (hashtables != null)
        {
            foreach (Hashtable hashtable1 in hashtables)
            {
                CardSource TopCard = GetTopCardFromOneHashtable(hashtable1);

                if (TopCard != null)
                {
                    if (TopCard.PermanentJustBeforeRemoveField != null)
                    {
                        if (card.PermanentJustBeforeRemoveField == TopCard.PermanentJustBeforeRemoveField)
                        {
                            return IsExistOnTrash(TopCard);
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Whether any TopCard is in trash when check [On Deletion] effect
    public static bool IsTopCardInTrashOnDeletion(Hashtable hashtable)
    {
        List<Hashtable> hashtables = GetHashtablesFromHashtable(hashtable);

        if (hashtables != null)
        {
            foreach (Hashtable hashtable1 in hashtables)
            {
                CardSource TopCard = GetTopCardFromOneHashtable(hashtable1);

                if (TopCard != null)
                {
                    if (IsExistOnTrash(TopCard) || TopCard.IsToken)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Whether the card that uses the effect and the top card belonged to the same permanent
    public static bool IsTopCardSamePermanent(Hashtable hashtable, CardSource card)
    {
        if (card == null) return false;
        if (card.PermanentJustBeforeRemoveField == null) return false;

        List<Hashtable> hashtables = GetHashtablesFromHashtable(hashtable);

        if (hashtables != null)
        {
            foreach (Hashtable hashtable1 in hashtables)
            {
                CardSource TopCard = GetTopCardFromOneHashtable(hashtable1);

                if (TopCard != null)
                {
                    if (TopCard.PermanentJustBeforeRemoveField != null)
                    {
                        if (card.PermanentJustBeforeRemoveField == TopCard.PermanentJustBeforeRemoveField)
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

    #region Can activate [On Deletion] effect that can activate if the permanent conains specific name
    public static bool CanActivateSelfOnDeletionWithContainingCardName(Hashtable hashtable, string name, CardSource card)
    {
        return CanActivateOnDeletionWithContainingCardName(
            hashtable: hashtable,
            name: name,
            cardCondition: cardSource => cardSource == card
        );
    }

    public static bool CanActivateOnDeletionWithContainingCardName(
        Hashtable hashtable,
        string name,
        Func<CardSource, bool> cardCondition)
    {
        List<Hashtable> hashtables = GetHashtablesFromHashtable(hashtable);

        if (hashtables != null)
        {
            foreach (Hashtable hashtable1 in hashtables)
            {
                if (hashtable1 != null)
                {
                    List<CardSource> CardSources = GetCardSourcesFromHashtable(hashtable1);

                    if (CardSources != null)
                    {
                        if (CardSources.Some(cardSource => cardCondition == null || cardCondition(cardSource)))
                        {
                            if (hashtable1.ContainsKey("CardNames"))
                            {
                                if (hashtable1["CardNames"] is List<string>)
                                {
                                    List<string> CardNames = (List<string>)hashtable1["CardNames"];

                                    if (CardNames != null)
                                    {
                                        if (CardNames.Count((cardName) => cardName.Contains(name)) >= 1)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Can activate [On Deletion] effect that can activate if the permanent conains specific trait
    public static bool CanActivateSelfOnDeletionWithContainingTrait(Hashtable hashtable, string name, CardSource card)
    {
        return CanActivateOnDeletionWithContainingTrait(
            hashtable: hashtable,
            name: name,
            cardCondition: cardSource => cardSource == card
        );
    }

    public static bool CanActivateOnDeletionWithContainingTrait(
        Hashtable hashtable,
        string name,
        Func<CardSource, bool> cardCondition)
    {
        List<Hashtable> hashtables = GetHashtablesFromHashtable(hashtable);
        if (hashtables != null)
        {
            foreach (Hashtable hashtable1 in hashtables)
            {
                if (hashtable1 != null)
                {
                    List<CardSource> CardSources = GetCardSourcesFromHashtable(hashtable1);
                    if (CardSources != null)
                    {
                        if (CardSources.Some(cardSource => cardCondition == null || cardCondition(cardSource)))
                        {
                            if (hashtable1.ContainsKey("TopCard"))
                            {
                                if (hashtable1["TopCard"] is CardSource)
                                {
                                    CardSource topCard = (CardSource)hashtable1["TopCard"];
                                    if (topCard != null)
                                    {
                                        if (topCard.ContainsTraits(name))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Can activate [On Deletion] effect that can activate if the permanent has specific colors
    public static bool CanActivateSelfOnDeletionWithCardColors(Hashtable hashtable, Func<List<CardColor>, bool> cardColorCondition, CardSource card)
    {
        return CanActivateOnDeletionWithCardColors(
            hashtable: hashtable,
            cardColorCondition: cardColorCondition,
            cardCondition: cardSource => cardSource == card
        );
    }

    public static bool CanActivateOnDeletionWithCardColors(
        Hashtable hashtable,
        Func<List<CardColor>, bool> cardColorCondition,
        Func<CardSource, bool> cardCondition)
    {
        if (hashtable != null)
        {
            List<Hashtable> hashtables = GetHashtablesFromHashtable(hashtable);

            if (hashtables != null)
            {
                foreach (Hashtable hashtable1 in hashtables)
                {
                    List<CardSource> CardSources = GetCardSourcesFromHashtable(hashtable1);

                    if (CardSources != null)
                    {
                        if (CardSources.Some(cardSource => cardCondition == null || cardCondition(cardSource)))
                        {
                            if (hashtable1.ContainsKey("CardColors"))
                            {
                                if (hashtable1["CardColors"] is List<CardColor>)
                                {
                                    List<CardColor> CardColors = (List<CardColor>)hashtable1["CardColors"];

                                    if (CardColors != null)
                                    {
                                        if (cardColorCondition == null || cardColorCondition(CardColors))
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #region Can activate [On Deletion] effect that can activate if the permanent has Save text
    public static bool CanActivateSelefOnDeletionWithSaveText(Hashtable hashtable, CardSource card)
    {
        return CanActivateOnDeletionWithSaveText(
            hashtable: hashtable,
            cardCondition: cardSource => cardSource == card
        );
    }

    public static bool CanActivateOnDeletionWithSaveText(
        Hashtable hashtable,
        Func<CardSource, bool> cardCondition)
    {
        List<Hashtable> hashtables = GetHashtablesFromHashtable(hashtable);

        if (hashtables != null)
        {
            foreach (Hashtable hashtable1 in hashtables)
            {
                List<CardSource> CardSources = GetCardSourcesFromHashtable(hashtable1);

                if (CardSources != null)
                {
                    if (CardSources.Some(cardSource => cardCondition == null || cardCondition(cardSource)))
                    {
                        if (hashtable1.ContainsKey("HasSaveText"))
                        {
                            if (hashtable1["HasSaveText"] is bool)
                            {
                                bool HasSaveText = (bool)hashtable1["HasSaveText"];

                                if (HasSaveText)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }

        return false;
    }
    #endregion

    #endregion
}
