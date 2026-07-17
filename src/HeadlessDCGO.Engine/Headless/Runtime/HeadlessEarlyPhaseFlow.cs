namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// LEGACY TEST SCAFFOLD (R4 S3c-d1): the OLD step-cadence driver's early-phase body (the AdvancePhase
// arm — Active/Draw/Breeding step model). It survives ONLY for the pre-R4 test corpus; new/RL matches
// use the TurnFlowPump (DcgoMatch.CreatePumpDriven), where the mirror TurnStateMachine phase bodies
// run continuously and AdvancePhase is illegal. Physical retirement gate = the suite re-targeting
// goal (design doc S3c-d ledger).
public sealed class HeadlessEarlyPhaseFlow
{
    public async Task<PhaseTransitionResult> AdvanceAsync(
        EngineContext context,
        LegalAction action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        HeadlessTurnState previous = context.TurnController.Current;
        if (previous.TurnPlayerId is null)
        {
            throw new InvalidOperationException("Cannot advance phase before turn state is initialized.");
        }

        if (action.PlayerId != previous.TurnPlayerId.Value)
        {
            throw new InvalidOperationException("Only the current turn player can advance the early phase flow.");
        }

        HeadlessTurnState current = context.TurnController.AdvancePhase();
        HeadlessPlayerId currentTurnPlayerId = current.TurnPlayerId
            ?? throw new InvalidOperationException("Cannot resolve current turn player after phase advance.");
        List<string> operations = new();
        IReadOnlyList<HeadlessEntityId> drawnCards = Array.Empty<HeadlessEntityId>();
        IReadOnlyList<HeadlessEntityId> unsuspendedCards = Array.Empty<HeadlessEntityId>();
        HeadlessEntityId? hatchedCard = null;
        IReadOnlyList<HeadlessEntityId> movedBreedingCards = Array.Empty<HeadlessEntityId>();
        bool drawSkipped = false;
        bool deckOut = false;
        string breedingAction = "None";

        // (R4 S2) The natural-unsuspend step is (Active, Unsuspending) — former HeadlessPhase.Unsuspend.
        if (current.IsUnsuspendPhase)
        {
            unsuspendedCards = UnsuspendForTurnPlayer(context, current);
            if (unsuspendedCards.Count > 0)
            {
                operations.Add("Unsuspend");

                // (E3-P1-2 / AS-IS IUnsuspendPermanents.Unsuspend, CardController.cs:5754) after the natural
                // unsuspend step, StackSkillInfos(EffectTiming.OnUnTappedAnyone) fires the "when this becomes
                // unsuspended" window over every permanent that just unsuspended. The sink-driven path already
                // emits OnUntapped (MatchStateMutationSink UnsuspendKind); the phase-flow unsuspend bypasses the
                // sink (direct Upsert), so mirror the emission here — one subject-scoped window per permanent
                // (the timing is SubjectScoped, ActivatedBridgeTimings). It resolves while the phase is still
                // Unsuspend, so a [Your Turn] unsuspend gate (BT8_057) reads the correct phase.
                foreach (HeadlessEntityId unsuspendedId in unsuspendedCards)
                {
                    HeadlessPlayerId owner = context.CardInstanceRepository.TryGetInstance(unsuspendedId, out CardInstanceRecord? rec) && rec is not null
                        ? rec.OwnerId
                        : currentTurnPlayerId;
                    TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnUntapped, actor: owner, subject: unsuspendedId);
                }
            }

            // N-1 (summoning sickness): a permanent is sick only on the turn it entered the field. At the
            // controller's Unsuspend step its permanents have survived to a later turn, so clear the
            // entered-this-turn flag. The original models this via EnterFieldTurnCount != TurnCount once
            // the turn advances; here we clear the boolean for the turn player's battle-area permanents.
            // (Silent state cleanup — no operation marker, to leave existing phase-operation assertions
            // untouched.)
            ClearEnteredThisTurnForTurnPlayer(context, currentTurnPlayerId);

            // CV-A1: expire continuous bindings scoped to the controller's active phase / next unsuspend.
            EffectDurationExpiry.ExpireUnsuspend(context.EffectRegistry, currentTurnPlayerId);
        }
        else if (current.Phase == HeadlessPhase.Draw)
        {
            if (current.IsFirstTurn)
            {
                drawSkipped = true;
                operations.Add("DrawSkippedFirstTurn");
            }
            else
            {
                if (GetZoneCount(context, currentTurnPlayerId, ChoiceZone.Library) == 0)
                {
                    deckOut = true;

                    // G3.5-RL-C1: deck-out is a LOSS for the player who must draw from an empty deck.
                    // Mark the loser so the common loop's terminal verdict carries the correct winner
                    // (previously this set terminal with no loser, making it look like a draw).
                    context.PlayerStatusController.MarkLose(
                        currentTurnPlayerId,
                        "Deck-out: required to draw from an empty deck.");

                    operations.Add("DeckOut");
                }
                else
                {
                    drawnCards = await context.ZoneMover
                        .DrawAsync(currentTurnPlayerId, 1, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    operations.Add("Draw");

                    // W1: open the OnDraw timing window for the draw-phase draw.
                    TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnDraw, actor: currentTurnPlayerId);
                }
            }
        }

        // D-6: the breeding step is NOT auto-resolved. When the phase becomes Breeding the turn player
        // decides via the dispatched HatchDigitama / MoveBreedingToBattle actions (or AdvancePhase to
        // decline); see HeadlessLegalActionDispatcher.BuildBreedingActions. breedingAction stays "None"
        // here — the chosen breeding action is processed by MetadataActionProcessor.

        return new PhaseTransitionResult(
            previous,
            current,
            operations.ToArray(),
            drawnCards,
            drawSkipped,
            deckOut,
            unsuspendedCards,
            breedingAction,
            hatchedCard,
            movedBreedingCards);
    }

    // (R4 S3c-d, 은퇴 원장 항1 부속) The auto-resolving ResolveBreedingAsync helper was DEAD CODE (0 callers
    // since D-6 made breeding a player decision) — deleted with its BreedingPhaseResult record.

    private static IReadOnlyList<HeadlessEntityId> UnsuspendForTurnPlayer(
        EngineContext context,
        HeadlessTurnState turn)
    {
        HeadlessPlayerId turnPlayerId = turn.TurnPlayerId!.Value;
        List<HeadlessEntityId> unsuspended = new();
        foreach (HeadlessPlayerId playerId in turn.PlayerOrder)
        {
            foreach (HeadlessEntityId cardId in GetZoneCards(context, playerId, ChoiceZone.BattleArea))
            {
                if (TryUnsuspend(context, cardId, turnPlayerId, allowReboot: true))
                {
                    unsuspended.Add(cardId);
                }
            }
        }

        foreach (HeadlessEntityId cardId in GetZoneCards(context, turnPlayerId, ChoiceZone.BreedingArea))
        {
            // N-9: the original breeding loop unsuspends unconditionally — it does NOT consult
            // CanUnsuspend (which targets field permanents). Bypass the gate here for the breeding area.
            if (TryUnsuspend(context, cardId, turnPlayerId, allowReboot: false, ignoreOwner: true, ignoreCanUnsuspend: true))
            {
                unsuspended.Add(cardId);
            }
        }

        return unsuspended.ToArray();
    }

    private static bool ClearEnteredThisTurnForTurnPlayer(
        EngineContext context,
        HeadlessPlayerId turnPlayerId)
    {
        bool clearedAny = false;
        foreach (HeadlessEntityId cardId in GetZoneCards(context, turnPlayerId, ChoiceZone.BattleArea))
        {
            if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) ||
                record is null ||
                !ReadBool(record.Metadata, "enteredThisTurn"))
            {
                continue;
            }

            Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal)
            {
                ["enteredThisTurn"] = false
            };
            context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            clearedAny = true;
        }

        return clearedAny;
    }

    private static bool TryUnsuspend(
        EngineContext context,
        HeadlessEntityId cardId,
        HeadlessPlayerId turnPlayerId,
        bool allowReboot,
        bool ignoreOwner = false,
        bool ignoreCanUnsuspend = false)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) ||
            record is null ||
            !ReadBool(record.Metadata, "isSuspended") ||
            (!ignoreCanUnsuspend && ReadBool(record.Metadata, "canUnsuspend", defaultValue: true) == false) ||
            // (R1-d) continuous "does not unsuspend" restriction — AS-IS Permanent.CanUnsuspend (mirror getter).
            (!ignoreCanUnsuspend && !new Assets.Scripts.Script.CardEffectCommons.Permanent(context, cardId).CanUnsuspend))
        {
            return false;
        }

        bool belongsToTurnPlayer = record.OwnerId == turnPlayerId;
        bool canReboot = allowReboot
            && (ReadBool(record.Metadata, "hasReboot")
                || new Assets.Scripts.Script.CardEffectCommons.Permanent(context, cardId).HasReboot);
        if (!ignoreOwner && !belongsToTurnPlayer && !canReboot)
        {
            return false;
        }

        Dictionary<string, object?> metadata = new(record.Metadata)
        {
            ["isSuspended"] = false,
            ["unsuspendedByPhase"] = true
        };
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
        return true;
    }

    private static IReadOnlyList<HeadlessEntityId> GetZoneCards(
        EngineContext context,
        HeadlessPlayerId playerId,
        ChoiceZone zone)
    {
        return context.ZoneMover is IZoneStateReader zoneReader
            ? zoneReader.GetCards(playerId, zone)
            : Array.Empty<HeadlessEntityId>();
    }

    private static int GetZoneCount(
        EngineContext context,
        HeadlessPlayerId playerId,
        ChoiceZone zone)
    {
        return GetZoneCards(context, playerId, zone).Count;
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        bool defaultValue = false)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return defaultValue;
        }

        return rawValue switch
        {
            bool boolValue => boolValue,
            string stringValue when bool.TryParse(stringValue, out bool parsed) => parsed,
            _ => defaultValue
        };
    }
}

public sealed record PhaseTransitionResult(
    HeadlessTurnState Previous,
    HeadlessTurnState Current,
    IReadOnlyList<string> Operations,
    IReadOnlyList<HeadlessEntityId> DrawnCardIds,
    bool DrawSkipped,
    bool DeckOut,
    IReadOnlyList<HeadlessEntityId> UnsuspendedCardIds,
    string BreedingAction,
    HeadlessEntityId? HatchedCardId,
    IReadOnlyList<HeadlessEntityId> MovedBreedingCardIds);

