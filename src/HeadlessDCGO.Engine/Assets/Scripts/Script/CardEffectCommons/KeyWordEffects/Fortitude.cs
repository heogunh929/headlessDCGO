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
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

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

    /// <summary>(C-Del 3a grant rehousing) AS-IS <c>GainFortitude</c> (KeyWordEffects/Fortitude.cs:67-97), 1:1.
    /// AS-IS QUIRK PRESERVED VERBATIM: this grants the target an <see cref="CardEffectFactory.EvadeEffect"/>
    /// ActivateClass — NOT a Fortitude effect — and stores it in the <c>OnDestroyedAnyone</c> duration bucket via
    /// <see cref="AddEffectToPermanent"/> (W3 live). This is the AS-IS behaviour (a copy/paste bug in the original
    /// source): "gains [Fortitude]" actually gives Evade wired to the POST-deletion window. Not corrected — mirrored
    /// exactly. Collect-before-removal (the deletion window) picks the bucket effect up and the post-deletion
    /// AutoProcessCheck resolves it. ADAPTATION: AS-IS's terminal visual <c>CreateBuffEffect</c> (a Unity
    /// presentation coroutine) has no headless substrate — dropped (same as GainRetaliation).</summary>
    public static async Task GainFortitude(Permanent targetPermanent, EffectDuration effectDuration, ICardEffect activateClass)
    {
        if (targetPermanent == null) return;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return;
        if (activateClass == null) return;
        if (activateClass.EffectSourceCard == null) return;

        CardSource card = activateClass.EffectSourceCard;

        bool CanUseCondition()
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (!targetPermanent.TopCard.CanNotBeAffected(activateClass))
                {
                    return true;
                }
            }

            return false;
        }

        ActivateClass evade = CardEffectFactory.EvadeEffect(
            targetPermanent: targetPermanent, isInheritedEffect: false, condition: CanUseCondition,
            rootCardEffect: activateClass, targetPermanent.TopCard);

        AddEffectToPermanent(
            targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
            cardEffect: evade, timing: EffectTiming.OnDestroyedAnyone);

        await Task.CompletedTask;
    }
}
