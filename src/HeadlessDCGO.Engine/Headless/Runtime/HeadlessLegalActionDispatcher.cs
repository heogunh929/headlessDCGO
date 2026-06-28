namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class HeadlessLegalActionDispatcher
{
    public IReadOnlyList<LegalAction> GetLegalActions(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (playerId.IsEmpty || context.RuleQueryService.IsTerminal())
        {
            return Array.Empty<LegalAction>();
        }

        // G3.5-RL-A2: a pending choice takes precedence over phase actions. The player who owns the
        // choice resolves it; everyone else has no legal action until it is resolved.
        if (context.ChoiceController.Current.IsPending)
        {
            ChoiceRequest? pending = context.ChoiceController.PendingRequest;
            if (pending is null || pending.PlayerId != playerId)
            {
                return Array.Empty<LegalAction>();
            }

            return BuildChoiceResolutionActions(pending)
                .Where(action => !CheatActionGuard.IsCheatOrDebugAction(action.ActionType))
                .ToArray();
        }

        if (!IsDispatchAvailable(context, playerId))
        {
            return Array.Empty<LegalAction>();
        }

        HeadlessTurnState turn = context.TurnController.Current;
        if (turn.TurnPlayerId is null || turn.TurnPlayerId.Value != playerId)
        {
            return Array.Empty<LegalAction>();
        }

        return (turn.Phase switch
        {
            HeadlessPhase.Setup or
            HeadlessPhase.Active or
            HeadlessPhase.Unsuspend or
            HeadlessPhase.Draw => new[] { HeadlessActionFactory.AdvancePhase(playerId) },
            // D-6: the breeding step is a player DECISION — offer the available breeding actions
            // (hatch / move) plus AdvancePhase to decline, instead of auto-resolving it.
            HeadlessPhase.Breeding => BuildBreedingActions(context, playerId),
            HeadlessPhase.Main => new[] { HeadlessActionFactory.Pass(playerId) }
                .Concat(new PlayCardAction().GetLegalActions(context, playerId))
                .Concat(new DigivolveAction().GetLegalActions(context, playerId))
                .Concat(new OptionActivateAction().GetLegalActions(context, playerId))
                .Concat(new AttackPermanentAction().GetLegalActions(context, playerId))
                .ToArray(),
            // (C-2 Blitz) The memory-pass window normally only offers EndTurn, but a <Blitz> Digimon may
            // still attack here (opponent already has >=1 memory). AttackPermanentAction's phase gate makes
            // GetLegalActions yield declarations only for Blitz-eligible attackers in this phase, so a board
            // without Blitz keeps exposing just EndTurn.
            HeadlessPhase.MemoryPass => new[] { HeadlessActionFactory.EndTurn(playerId) }
                .Concat(new AttackPermanentAction().GetLegalActions(context, playerId))
                .ToArray(),
            HeadlessPhase.End => new[] { HeadlessActionFactory.EndTurn(playerId) },
            _ => Array.Empty<LegalAction>()
        })
        .Where(action => !CheatActionGuard.IsCheatOrDebugAction(action.ActionType))
        .ToArray();
    }

    /// <summary>
    /// (D-6) Breeding-step legal actions for the turn player: hatch a digitama (when a digitama is
    /// available and the breeding area is empty), move the breeding Digimon to the battle area (when the
    /// breeding area is occupied), and AdvancePhase to decline the breeding action. Mirrors the AS-IS
    /// optional BreedingPhase decision instead of auto-resolving it.
    /// </summary>
    private static LegalAction[] BuildBreedingActions(EngineContext context, HeadlessPlayerId playerId)
    {
        var actions = new List<LegalAction>();
        if (context.ZoneMover is IZoneStateReader zones)
        {
            int digitama = zones.GetCards(playerId, ChoiceZone.DigitamaLibrary).Count;
            int breeding = zones.GetCards(playerId, ChoiceZone.BreedingArea).Count;

            if (digitama > 0 && breeding == 0)
            {
                actions.Add(HeadlessActionFactory.HatchDigitama(playerId));
            }

            if (breeding > 0)
            {
                actions.Add(HeadlessActionFactory.MoveBreedingToBattle(playerId, count: 1));
            }
        }

        actions.Add(HeadlessActionFactory.AdvancePhase(playerId));
        return actions.ToArray();
    }

    /// <summary>
    /// Enumerates the ResolveChoice actions a policy can take for a pending choice (G3.5-RL-A2).
    /// Single-select and "choose up to one" requests yield one action per selectable candidate;
    /// Count requests yield one action per allowed count; skippable requests add a Skip action.
    /// Multi-select (MinCount &gt; 1) full subset enumeration is deferred to the factored action
    /// space work (G3.5-RL-A3); such requests only expose Skip when allowed.
    /// </summary>
    private static IReadOnlyList<LegalAction> BuildChoiceResolutionActions(ChoiceRequest request)
    {
        List<LegalAction> actions = new();

        if (request.Type == ChoiceType.Count)
        {
            for (int count = request.MinCount; count <= request.MaxCount; count++)
            {
                actions.Add(HeadlessActionFactory.ResolveChoice(
                    request.PlayerId,
                    ChoiceResult.SelectCount(count),
                    actionId: $"resolvechoice:{request.PlayerId.Value}:count:{count}"));
            }
        }
        else if (request.MinCount <= 1 && request.MaxCount >= 1)
        {
            // A single pick is a valid selection of size 1 whenever MinCount <= 1 <= MaxCount.
            foreach (ChoiceCandidate candidate in request.SelectableCandidates)
            {
                actions.Add(HeadlessActionFactory.ResolveChoice(
                    request.PlayerId,
                    ChoiceResult.Select(candidate.Id),
                    actionId: $"resolvechoice:{request.PlayerId.Value}:{candidate.Id.Value}"));
            }
        }

        if (request.CanSkip)
        {
            actions.Add(HeadlessActionFactory.ResolveChoice(
                request.PlayerId,
                ChoiceResult.Skip(),
                actionId: $"resolvechoice:{request.PlayerId.Value}:skip"));
        }

        return actions;
    }

    private static bool IsDispatchAvailable(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        if (playerId.IsEmpty ||
            context.RuleQueryService.IsTerminal() ||
            context.ChoiceController.Current.IsPending ||
            context.EffectScheduler.HasPendingEffects ||
            context.CardInstanceRepository.Snapshot().Count == 0)
        {
            return false;
        }

        return true;
    }
}
