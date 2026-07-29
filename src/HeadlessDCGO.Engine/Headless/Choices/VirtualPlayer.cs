// ============================================================================================================
// THE SEAM WHERE A DECISION ENTERS THE ENGINE.
//
// WHY IT LOOKS LIKE THIS. The AS-IS engine already has a complete protocol for asking a player something: it
// opens one of its panels, which carries the question (`Message`), the legal answers
// (`_CanTargetCondition`, `_MaxCount`, `_CanNoSelect`) and the answer channel — the panel's own
// `public void OnClickXxx()` methods, which Unity's buttons call. A click runs
//     OnClickNotSelectButton() -> SetIsEndSelection(true) -> the panel's WaitWhile releases
//                              -> the caller's callback -> RPC -> Player.QueuePlayerSelection
//                              -> the engine's WaitUntil(player.HasPlayerSelection()) releases
// so there is no protocol to invent. This class is a player that clicks: it reads the panel that is open and
// calls the same method a human click would.
//
// WHY NOT CALL `QueuePlayerSelection` DIRECTLY. It is shorter and it is wrong: it skips
// `SetIsEndSelection`/`CloseSelectCardPanel`, so the panel stays open and its own wait never ends. The
// short-cut desynchronises the very state the engine is about to read.
//
// WHERE IT SITS. Between the engine and whatever decides — the ENGINE-FACING HALF OF THE ADAPTER, not part of
// the engine. It inverts control: the engine ASKS and blocks (pull), an RL loop STEPS (push). The same seam
// takes a scripted policy (smoke tests), the AS-IS built-in AI, a learned policy, or a human, and each seat
// can have a different one.
//
// HOW A PENDING DECISION IS DETECTED. NOT by the scheduler going idle — it never does. The AS-IS UI leaves
// idle animation loops running for the whole match (`LoadingObject.SetLoadingText` is a `while (true)` started
// on `ContinuousController`, so it outlives the loading screen and Unity does not stop it either), so
// something is always advancing and a no-progress test can never fire. That was a real dead end here.
//
// The panel answers the question directly instead: it is ACTIVE, and its own click flag is still unset, which
// is exactly the state its `WaitWhile(() => !_isEndSelection)` is parked on. Answering is gated on the
// scheduler having at least one SUSPENDED routine as well, so a panel that is still being built — its setup
// runs before that wait — is not clicked out from under itself.
//
// THE ENGINE ASKS IN TWO SHAPES, and both are answered the same way — by calling the public method Unity's
// input would have called:
//
//   A PANEL      `SelectCardPanel` becomes active and parks on `WaitWhile(() => !_isEndSelection)`.
//                Answered with `OnClickNotSelectButton()` / `OnClickEndSelectButton()` / `OnClickHandCard(x)`.
//   A CLICK      No panel: the engine stores a callback on an object and waits. `BreedingPhase` does
//                `TurnPlayer.SetUpHatchObject(() => SendShouldHatch(true))` (TurnStateMachine.cs:738), which
//                puts the action in `Player.OnClickHatchObjectAction`. Answered with the object's own
//                `OnClickHatchObject()`, which invokes it.
//
// AUTO MODES ARE NOT USED. AS-IS has a developer `isAuto` that self-answers both seats, and six `auto*`
// gameplay options (`autoHatch`, `autoMaxCardCount`, …) that self-answer parts. All stay false (user decision,
// 2026-07-29): `isAuto` is an unverified leftover — relying on it means trusting a path the real game may
// never have run — and each `auto*` option removes a decision an agent has to make.
//
//   AN ACTION     The main phase is not a question at all: `MainPhase` spins on
//                     while (PlayCard == null && UseCardEffect == null && AttackingPermanent == null)
//                         if (TurnPlayer.HasMainPhaseAction()) TurnPlayer.DequeueMainPhaseAction().Execute(this);
//                 (TurnStateMachine.cs:972-985) waiting for the player to DO something. The five things a
//                 player can do are the `MainPhaseAction` subclasses — Pass, PlayCard, ActivateCard,
//                 ActivatePermanent, AttackPermanent — and they are queued through
//                 `TurnStateMachine.QueueMainPhaseAction(player, action)`, which serialises them over the same
//                 RPC a button press uses (TurnStateMachine.cs:3027). This is the seam an RL policy acts
//                 through; the prompts above are the seam it ANSWERS through.
//
// SCOPE TODAY. Only `SelectCardPanel` (the mulligan prompt and the card-selection prompts built on it). The
// other panels — SelectPermanentEffect, SelectHandEffect, SelectAttackEffect, SelectCountEffect,
// SelectDigiXrosClass — have their own OnClick surfaces and are not handled yet; `Answer` reports that it
// could not act rather than guessing, so an unhandled prompt shows up as a stall with a named reason instead
// of a silent hang.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Choices;

using System.Reflection;
using UnityEngine;

/// <summary>What the engine is asking, as far as the substrate can see it.</summary>
public sealed record ChoicePrompt(string Panel, string Message);

/// <summary>Answers the engine's prompts by clicking, the way a player does.</summary>
public abstract class VirtualPlayer
{
    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>The waits the scheduler is currently parked on. Set by the caller each tick.</summary>
    public IReadOnlyCollection<CustomYieldInstruction> Waits { get; set; } = Array.Empty<CustomYieldInstruction>();

    /// <summary>Selectors seen parked that this seam has no channel for. A named gap, not a silent hang.</summary>
    public HashSet<string> Unhandled { get; } = new(StringComparer.Ordinal);

    /// <summary>Answers whatever prompt is currently waiting. Returns false when none is.</summary>
    public bool Answer()
    {
        SelectCardPanel? panel = GManager.instance?.selectCardPanel;

        if (panel is not null && panel.gameObject.activeSelf && IsAwaitingClick(panel))
        {
            return Decide(panel, new ChoicePrompt(nameof(SelectCardPanel), Describe(panel)));
        }

        // A pending click target: the engine parked after storing the action on the object.
        foreach (Player? seat in new[] { GManager.instance?.You, GManager.instance?.Opponent })
        {
            if (seat?.OnClickHatchObjectAction is null)
            {
                continue;
            }

            return DecideHatch(seat, new ChoicePrompt(nameof(Player), $"{seat.name}: hatch?"));
        }

        // A parked selection wait: identify it from the predicate closure and answer through its channel.
        foreach (CustomYieldInstruction wait in Waits)
        {
            if (SelectionChannels.Identify(wait) is not { } pending)
            {
                continue;
            }

            if (pending.Seat.HasPlayerSelection())
            {
                continue;   // already answered; the engine just has not resumed yet
            }

            if (SelectionChannels.Answer(pending))
            {
                Record(new ChoicePrompt(pending.Selector, $"seat {pending.Seat.PlayerID}"));

                return true;
            }

            Unhandled.Add(pending.Selector);
        }

        // The main phase asks for an ACTION, not an answer. It is recognisable by the turn player having no
        // queued action while the phase is Main.
        if (PendingMainPhase() is { } actor)
        {
            return Act(actor, new ChoicePrompt("MainPhase", $"{actor.name}: act?"));
        }

        return false;
    }

    /// <summary>The player the main phase is waiting on, or null when it is not waiting.</summary>
    private static Player? PendingMainPhase()
    {
        TurnStateMachine? machine = GManager.instance?.turnStateMachine;

        if (machine?.gameContext is not { } context || context.TurnPhase != GameContext.phase.Main)
        {
            return null;
        }

        return context.TurnPlayer is { } turnPlayer && !turnPlayer.HasMainPhaseAction() ? turnPlayer : null;
    }

    /// <summary>Takes a turn. The default passes — enough to carry a match through its whole turn loop, phase
    /// transitions and end conditions without playing anything.</summary>
    protected virtual bool Act(Player actor, ChoicePrompt prompt)
    {
        Record(prompt);
        GManager.instance!.turnStateMachine.QueueMainPhaseAction(actor, new PassAction());

        return true;
    }

    /// <summary>Answers the breeding-phase prompt. The default takes it, because declining is not always an
    /// available answer: `BreedingPhase` only registers this action when hatching (or moving) is the thing to
    /// do, and there is no "no" callback to invoke.</summary>
    protected virtual bool DecideHatch(Player seat, ChoicePrompt prompt)
    {
        Record(prompt);
        seat.OnClickHatchObject();

        return true;
    }

    /// <summary>Notes a prompt that was answered.</summary>
    protected virtual void Record(ChoicePrompt prompt)
    {
    }

    /// <summary>Chooses and clicks. Implementations call the panel's own <c>OnClickXxx()</c> methods.</summary>
    protected abstract bool Decide(SelectCardPanel panel, ChoicePrompt prompt);

    private static string Describe(SelectCardPanel panel) => panel.name;

    /// <summary>True when the panel has reached its own wait: `WaitWhile(() =&gt; !_isEndSelection)`. Reading
    /// that private flag is how the substrate sees the question without inventing a protocol — the flag IS the
    /// engine's own "answered yet?" state.</summary>
    private static bool IsAwaitingClick(SelectCardPanel panel)
    {
        FieldInfo? flag = typeof(SelectCardPanel).GetField("_isEndSelection", AnyInstance);

        return flag?.GetValue(panel) is false;
    }
}

/// <summary>The simplest possible player: always takes the "select nothing" answer. Enough to carry the engine
/// past a prompt so a whole match can be exercised; it is a SMOKE-TEST policy, not a playing one.</summary>
public sealed class AlwaysDeclineVirtualPlayer : VirtualPlayer
{
    /// <summary>Prompts answered so far, for reporting.</summary>
    public List<ChoicePrompt> Answered { get; } = new();

    protected override bool Decide(SelectCardPanel panel, ChoicePrompt prompt)
    {
        Record(prompt);

        // The mulligan prompt's "Keep Hand" button, and the generic "select nothing" answer elsewhere.
        panel.OnClickNotSelectButton();

        return true;
    }

    protected override void Record(ChoicePrompt prompt) => Answered.Add(prompt);
}
