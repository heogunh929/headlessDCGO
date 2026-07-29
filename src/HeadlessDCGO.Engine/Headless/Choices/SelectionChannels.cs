// ============================================================================================================
// THE TWELVE CHANNELS A DECISION ENTERS THROUGH.
//
// Every question the AS-IS engine asks a player ends at the same shape:
//     if (opponent seat || IsAI)  SetXxx(playerID, chosen);      // the AI answers itself
//     else                        open a panel / register a click // a human answers
//     yield return new WaitUntil(() => somePlayer.HasPlayerSelection());
// Both branches call the SAME `[PunRPC] SetXxx(playerID, …)`, which queues an `IPlayerSelection` on that
// player. So the twelve `SetXxx` methods — not the panels — are the actual seam, and they are what an agent
// will act through. Measured across the tree, they are:
//
//     SelectPermanentEffect.SetTargetFrames(playerID, isTurnPlayer[], unitIndex[])
//     SelectHandEffect.SetTargetHandCards(playerID, cardIds[])
//     SelectCardEffect.SetTargetCardAndIndicies(playerID, cardIds[], indices[])
//     SelectCountEffect.SetCount(playerID, count)
//     SelectAttackEffect.SetAttackTarget(playerID, isTurnPlayer, permanentIndex)
//     SelectDigiXrosClass.SetTargetDigiXrossIndex(playerID, index)
//     MultipleSkills.SetTargetSkill(playerID, skillIndex)
//     OptionalSkill.SetUseOptional(playerID, use)
//     TurnStateMachine.SetRedraw(playerID, redraw)
//     TurnStateMachine.SetBreedingPhase(playerID, breed)
//     TurnStateMachine.SetStartPlayer(change)
//     DNADigivolveEffects — SetJogressEvoRootsFrameIDs(playerID, frameIds[])   (on TurnStateMachine)
//
// FINDING OUT WHICH ONE IS PENDING. The waits are indistinguishable from outside — all of them are
// `WaitUntil(() => x.HasPlayerSelection())`. What differs is the CLOSURE the lambda was compiled into: its
// declaring type is the selector's own nested `<>c__DisplayClass`, so `SelectPermanentEffect` waits carry a
// closure declared inside `SelectPermanentEffect`. Reading that is how the substrate names the question
// without the engine having to publish one. The captured fields also hold the player being asked, which is why
// the seat comes out of the same inspection.
//
// THE ANSWERS HERE ARE MINIMAL, NOT GOOD. Empty selections and first-choice indices: enough to carry a match
// through every branch so the engine can be exercised end to end. A playing policy replaces `SelectionAnswer`;
// the channels do not change.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Choices;

using System.Reflection;
using UnityEngine;

/// <summary>One pending question: which selector asked, and which seat owes the answer.</summary>
public sealed record PendingSelection(string Selector, Player Seat);

/// <summary>Identifies the question the engine is parked on, and answers it.</summary>
public static class SelectionChannels
{
    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>Reads a parked predicate and names the selector plus the seat it is waiting on. Returns null
    /// when the wait is not a player-selection wait.</summary>
    public static PendingSelection? Identify(CustomYieldInstruction wait)
    {
        Delegate? predicate = wait switch
        {
            WaitUntil until => until.Predicate,
            WaitWhile whileWait => whileWait.Predicate,
            _ => null,
        };

        if (predicate?.Target is not { } closure)
        {
            return null;
        }

        // The closure is a compiler-generated nested type of the selector that created the lambda.
        Type? owner = predicate.Method.DeclaringType;

        while (owner is not null && owner.Name.Contains('<', StringComparison.Ordinal))
        {
            owner = owner.DeclaringType;
        }

        if (owner is null)
        {
            return null;
        }

        // The captured player is the seat being asked. A closure that captured none is not a selection wait.
        foreach (FieldInfo field in closure.GetType().GetFields(AnyInstance))
        {
            if (field.GetValue(closure) is Player seat)
            {
                return new PendingSelection(owner.Name, seat);
            }

            // The lambda may have captured `this` (the selector) rather than the player directly.
            if (field.GetValue(closure) is { } captured && SeatOf(captured) is { } indirect)
            {
                return new PendingSelection(owner.Name, indirect);
            }
        }

        return null;
    }

    /// <summary>Answers the pending question with a minimal legal answer. Returns false when the selector is
    /// not one of the twelve — that shows up as a named stall rather than a silent hang.</summary>
    public static bool Answer(PendingSelection pending)
    {
        GManager manager = GManager.instance;
        int seat = pending.Seat.PlayerID;

        switch (pending.Selector)
        {
            case nameof(SelectPermanentEffect):
                // No targets. `_canNoSelect` is the AS-IS flag for whether that is allowed; when it is not,
                // the engine re-asks rather than breaking, so an empty answer is always safe to send.
                manager.GetComponent<SelectPermanentEffect>()!
                    .SetTargetFrames(seat, Array.Empty<bool>(), Array.Empty<int>());

                return true;

            case nameof(SelectHandEffect):
                manager.GetComponent<SelectHandEffect>()!.SetTargetHandCards(seat, Array.Empty<int>());

                return true;

            case nameof(SelectCardEffect):
                manager.GetComponent<SelectCardEffect>()!
                    .SetTargetCardAndIndicies(seat, Array.Empty<int>(), Array.Empty<int>());

                return true;

            case nameof(SelectCountEffect):
                // The lowest candidate. `_candidates` is the legal set the selector computed.
                manager.GetComponent<SelectCountEffect>()!.SetCount(seat, LowestCandidate(manager));

                return true;

            case nameof(SelectAttackEffect):
                // Attack the player rather than a permanent: index -1 is the AS-IS "no permanent" value.
                manager.GetComponent<SelectAttackEffect>()!.SetAttackTarget(seat, isTurnPlayer: false, -1);

                return true;

            case nameof(SelectDigiXrosClass):
                manager.GetComponent<SelectDigiXrosClass>()!.SetTargetDigiXrossIndex(seat, 0);

                return true;

            case nameof(MultipleSkills):
                manager.GetComponent<MultipleSkills>()!.SetTargetSkill(seat, 0);

                return true;

            case nameof(OptionalSkill):
                manager.GetComponent<OptionalSkill>()!.SetUseOptional(seat, useOptional: false);

                return true;

            case nameof(TurnStateMachine):
                // Both TurnStateMachine waits (mulligan, breeding) take a bool; declining is legal for each.
                manager.turnStateMachine.SetBreedingPhase(seat, doBreeding: false);

                return true;

            default:
                return false;
        }
    }

    /// <summary>The smallest count the selector said is legal, or 0 when it published none.</summary>
    private static int LowestCandidate(GManager manager)
    {
        SelectCountEffect? selector = manager.GetComponent<SelectCountEffect>();
        object? candidates = selector is null ? null : FieldValue(selector, "_candidates");

        return candidates is List<int> { Count: > 0 } list ? list.Min() : 0;
    }

    private static Player? SeatOf(object holder) => FieldValue(holder, "_selectPlayer") as Player;

    private static object? FieldValue(object target, string name)
    {
        for (Type? type = target.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetField(name, AnyInstance) is { } field)
            {
                return field.GetValue(target);
            }
        }

        return null;
    }
}
