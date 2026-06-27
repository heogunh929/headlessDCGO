// Source: Assets/Scripts/Script/ICardEffect.cs (CanUse inherited/main branch)
// Decision: PORT
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons
//
// AS-IS mirror of the original ICardEffect.CanUse activation gate for INHERITED effects (the bottom
// "inherited effect" text of a digivolution source). The original rule (ICardEffect.cs ~390-405):
//   For an inherited (or linked) effect whose source card sits in a permanent:
//     - if the source card IS the permanent's top card        -> NOT active (that's a main effect)
//     - if the source card is flipped (face down)             -> NOT active
//     - if the permanent is not a Digimon                     -> NOT active
//   For a MAIN (non-inherited) effect:
//     - active only when the source card IS the top card.
// This is the activation gate; binding creation lives in InheritedGrantedSecurityHelpers.

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public static class InheritedEffectHelpers
{
    /// <summary>
    /// (F-7.1) Whether an inherited effect sourced from <paramref name="sourceCardId"/> is active on its
    /// permanent: the source must be an under-card (not the top), must not be flipped, and the permanent
    /// (top card) must be a Digimon.
    /// </summary>
    public static bool IsInheritedEffectActive(
        DigivolutionStack stack,
        HeadlessEntityId sourceCardId,
        bool sourceFlipped,
        bool permanentIsDigimon)
    {
        ArgumentNullException.ThrowIfNull(stack);
        if (sourceCardId.IsEmpty || stack.IsEmpty || !permanentIsDigimon || sourceFlipped)
        {
            return false;
        }

        // The top card's own printed text is a MAIN effect, never an inherited one.
        if (stack.TopCard is { } top && top.InstanceId == sourceCardId)
        {
            return false;
        }

        // The source must actually be one of the under-cards of this stack.
        return stack.UnderCards.Any(card => card.InstanceId == sourceCardId);
    }

    /// <summary>
    /// (F-7.1) Whether a MAIN (non-inherited) effect sourced from <paramref name="sourceCardId"/> is
    /// active: it must be the permanent's top card.
    /// </summary>
    public static bool IsMainEffectActive(DigivolutionStack stack, HeadlessEntityId sourceCardId)
    {
        ArgumentNullException.ThrowIfNull(stack);
        return !sourceCardId.IsEmpty
            && stack.TopCard is { } top
            && top.InstanceId == sourceCardId;
    }

    /// <summary>
    /// (F-7.2) The under-cards whose inherited effects are currently granted to the top card — every
    /// non-flipped source, provided the permanent is a Digimon. <paramref name="isFlipped"/> reports a
    /// source card's flip state (the original checks <c>EffectSourceCard.IsFlipped</c>).
    /// </summary>
    public static IReadOnlyList<HeadlessEntityId> ActiveInheritedSources(
        DigivolutionStack stack,
        Func<HeadlessEntityId, bool> isFlipped,
        bool permanentIsDigimon)
    {
        ArgumentNullException.ThrowIfNull(stack);
        ArgumentNullException.ThrowIfNull(isFlipped);

        if (stack.IsEmpty || !permanentIsDigimon)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var active = new List<HeadlessEntityId>();
        foreach (StackedCard card in stack.UnderCards)
        {
            if (!isFlipped(card.InstanceId))
            {
                active.Add(card.InstanceId);
            }
        }

        return active;
    }
}
