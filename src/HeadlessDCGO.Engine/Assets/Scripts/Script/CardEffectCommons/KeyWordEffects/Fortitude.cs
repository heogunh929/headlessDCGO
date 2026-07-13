// Source: DCGO/Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Fortitude.cs
// (P6 cluster2) 1:1 port of the AS-IS Hashtable-based helpers. A DIFFERENT (CardEffectResolveContext-based)
// CanActivateFortitude/FortitudeProcess overload already lives in the monolith CardEffectCommons.cs (for the
// newer kind-class DeletionReplacementGate consumer) — these are additional overloads for the old-model
// ActivateClass factory (Script/CardEffectFactory/KeyWordEffects/Fortitude.cs), matching AS-IS parameter
// shapes exactly (Hashtable, not ctx).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public static partial class CardEffectCommons
{
    /// <summary>AS-IS <c>CanTriggerFortitude</c> (KeyWordEffects/Fortitude.cs:9, verbatim).</summary>
    public static bool CanTriggerFortitude(Hashtable hashtable, CardSource card) =>
        CanTriggerOnDeletion(hashtable, card);

    /// <summary>AS-IS <c>CanActivateFortitude</c> (KeyWordEffects/Fortitude.cs:16, verbatim): this card is in
    /// the trash, was part of a deleted stack with at least 1 digivolution source, and can replay for free.</summary>
    public static bool CanActivateFortitude(Hashtable hashtable, CardSource card, bool isInheritedEffect, ICardEffect activateClass)
    {
        if (!IsExistOnTrash(card) || (isInheritedEffect && !CanActivateOnDeletion(hashtable, card)))
        {
            return false;
        }

        List<Hashtable>? hashtables = GetHashtablesFromHashtable(hashtable);
        if (hashtables is null)
        {
            return false;
        }

        foreach (Hashtable hashtable1 in hashtables)
        {
            List<CardSource>? cardStack = GetCardSourcesFromHashtable(hashtable1);
            List<CardSource>? digivolutionSources = GetDigivolutionSourcesFromHashtable(hashtable1);
            if (cardStack is null || digivolutionSources is null)
            {
                continue;
            }

            if (cardStack.Contains(card) && digivolutionSources.Count >= 1
                && CanPlayAsNewPermanent(cardSource: card, payCost: false, cardEffect: activateClass))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>FortitudeProcess</c> (KeyWordEffects/Fortitude.cs:54, verbatim): replay this card
    /// from the trash for free. <c>PlayPermanentCards</c>'s mirror shape drops the AS-IS <c>root</c>
    /// (<c>SelectCardEffect.Root</c>) parameter — it resolves the source zone live off the card itself.</summary>
    public static Task FortitudeProcess(Hashtable hashtable, CardSource card, ICardEffect activateClass) =>
        PlayPermanentCards(
            cardSources: new List<CardSource> { card },
            sourceCard: card,
            payCost: false,
            isTapped: false,
            root: Headless.Choices.ChoiceZone.Trash,
            activateETB: true);
}
