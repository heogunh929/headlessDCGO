namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
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

        // (R4 S3c-b, decision 3 = B) PUMP match action table: the invented step cadence (AdvancePhase / EndTurn
        // / breeding actions) is retired — phases auto-flow in the TurnFlowPump, breeding is a CHOICE (covered
        // by the pending-choice branch above), the memory-pass "awaiting" step does not exist (EndTurnCheck
        // auto-ends), and the ONLY action surface is the MAIN selection wait: Pass + the main-phase plays.
        // SpecialPlay is omitted: the pump's special-play seams are the registered component STOPs
        // (RD-P6C1-5 Assembly / RD-R5-04 DigiXros) until that cluster ports.
        if (TurnFlowPumpHost.Find(context) is not null)
        {
            if (turn.Phase != HeadlessPhase.Main || turn.StepCursor != TurnStepCursor.PhaseStart)
            {
                return Array.Empty<LegalAction>();
            }

            return new[] { HeadlessActionFactory.Pass(playerId) }
                .Concat(new PlayCardAction().GetLegalActions(context, playerId))
                .Concat(new DigivolveAction().GetLegalActions(context, playerId))
                .Concat(new OptionActivateAction().GetLegalActions(context, playerId))
                .Concat(new MainSkillActivateAction().GetLegalActions(context, playerId))
                .Concat(new AttackPermanentAction().GetLegalActions(context, playerId))
                .Where(action => !CheatActionGuard.IsCheatOrDebugAction(action.ActionType))
                .ToArray();
        }

        // (R4 S2) Legal-action table re-keyed by (phase, step-cursor). Behaviour is identical to the former
        // 9-value phase table: each old state's exposed action set is preserved by its (phase, cursor) pair.
        //   Setup=(None,Starting) → AdvancePhase; Active/Unsuspend=(Active,*) → AdvancePhase; Draw → AdvancePhase;
        //   Main-play=(Main,PhaseStart) → play; MemoryPass=(Main,AwaitingMemoryPassEnd) → EndTurn;
        //   End → EndTurn; pure (None,PhaseStart) → none.
        return ((turn.Phase, turn.StepCursor) switch
        {
            (HeadlessPhase.None, TurnStepCursor.Starting) or
            (HeadlessPhase.Active, _) or
            (HeadlessPhase.Draw, _) => new[] { HeadlessActionFactory.AdvancePhase(playerId) },
            // D-6: the breeding step is a player DECISION — offer the available breeding actions
            // (hatch / move) plus AdvancePhase to decline, instead of auto-resolving it.
            (HeadlessPhase.Breeding, _) => BuildBreedingActions(context, playerId),
            (HeadlessPhase.Main, TurnStepCursor.PhaseStart) => new[] { HeadlessActionFactory.Pass(playerId) }
                .Concat(new PlayCardAction().GetLegalActions(context, playerId))
                .Concat(new DigivolveAction().GetLegalActions(context, playerId))
                .Concat(new SpecialPlayAction().GetLegalActions(context, playerId))
                .Concat(new OptionActivateAction().GetLegalActions(context, playerId))
                // (B-2 / P1-5) declare a battle-area permanent's [Main] activated skill (AS-IS CanDeclareSkillList).
                .Concat(new MainSkillActivateAction().GetLegalActions(context, playerId))
                .Concat(new AttackPermanentAction().GetLegalActions(context, playerId))
                .ToArray(),
            // (C-2 / RD-CATK-BLITZ re-judgment) The memory-pass window offers EndTurn. AS-IS Blitz is NOT a phase
            // permission — the invented "<Blitz> may attack during memory-pass" gate was retired (AttackPermanentAction
            // :217; Blitz is rehoused onto the [On Play]/[When Digivolving] window). AttackPermanentAction is still
            // concatenated for shape uniformity, but its phase gate yields no memory-pass declarations, so this
            // branch exposes just EndTurn.
            (HeadlessPhase.Main, TurnStepCursor.AwaitingMemoryPassEnd) => new[] { HeadlessActionFactory.EndTurn(playerId) }
                .Concat(new AttackPermanentAction().GetLegalActions(context, playerId))
                .ToArray(),
            (HeadlessPhase.End, _) => new[] { HeadlessActionFactory.EndTurn(playerId) },
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
            IReadOnlyList<HeadlessEntityId> breeding = zones.GetCards(playerId, ChoiceZone.BreedingArea);

            // Hatch: only with a digi-egg available and an empty breeding area (AS-IS Player.CanHatch).
            if (digitama > 0 && breeding.Count == 0)
            {
                actions.Add(HeadlessActionFactory.HatchDigitama(playerId));
            }

            // Move: only when the breeding permanent's top card is a movable Digimon — NOT a freshly-hatched
            // Digi-Egg. Mirrors AS-IS Permanent.CanMove (Permanent.cs:2068 `if (!IsDigimon) return false;`
            // + 2071 `if (TopCard.IsDigiEgg && DP <= 0) return false;`). Previously this offered Move whenever
            // the area was non-empty, letting a level-2 egg illegally walk into the battle area (GR-002).
            if (breeding.Count > 0 && IsMovableBreedingDigimon(context, breeding[0]))
            {
                actions.Add(HeadlessActionFactory.MoveBreedingToBattle(playerId, count: 1));
            }
        }

        actions.Add(HeadlessActionFactory.AdvancePhase(playerId));
        return actions.ToArray();
    }

    /// <summary>(GR-002) The breeding permanent may move to the battle area only once its top card is an
    /// actual Digimon — a hatched Digi-Egg (and any DP&lt;=0 egg) cannot move until it is digivolved into a
    /// Digimon. Mirrors AS-IS <c>Permanent.CanMove</c>.</summary>
    private static bool IsMovableBreedingDigimon(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) || definition is null)
        {
            return false;
        }

        // AS-IS Permanent.CanMove `if (!IsDigimon) return false;` (Permanent.cs:2068). IsDigimon is true for a
        // Digimon OR a Digi-Egg (Permanent.cs:3448) — so an egg PASSES this guard; a Tamer/Option is blocked here.
        // The egg's own move restriction is the DP<=0 rule below (1:1 with AS-IS's two-part guard).
        if (!definition.IsCardType("Digimon")
            && !new Assets.Scripts.Script.CardEffectCommons.Permanent(context, cardId).IsDigimon)
        {
            return false;
        }

        // (R3-W3c-4c D-1 flip) AS-IS Permanent.CanMove ICanNotMoveEffect scan (Permanent.cs:2878-2934): scan every
        // player's field permanents' + players' EffectList(None) for a usable ICanNotMoveEffect whose predicate
        // CanNotMove(TopCard, null) holds — the move gate passes a NULL causing effect (AS-IS CanNotMove(top, null)).
        // The joint carrier CanNotMoveEffect now implements ICanNotMoveEffect, so this LIVE scan replaces the
        // retired registry RestrictionScan(CannotMoveKey) 1:1 (only this restriction portion is flipped; the
        // IsDigimon / egg-DP guards above stay as the GR-002 decomposition).
        var movePermanent = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, cardId);
        Assets.Scripts.Script.CardEffectCommons.CardSource? moveTop = movePermanent.TopCard;
        if (moveTop is not null)
        {
            foreach (Assets.Scripts.Script.CardEffectCommons.Player scanPlayer in new Assets.Scripts.Script.CardEffectCommons.GameContext(context).Players)
            {
                foreach (Assets.Scripts.Script.CardEffectCommons.Permanent fieldPermanent in scanPlayer.GetFieldPermanents())
                {
                    foreach (Assets.Scripts.Script.CardEffectCommons.ICardEffect cardEffect in fieldPermanent.EffectList(Assets.Scripts.Script.CardEffectCommons.EffectTiming.None))
                    {
                        if (cardEffect is Assets.Scripts.Script.CardEffectCommons.ICanNotMoveEffect cannotMove
                            && cardEffect.CanUse(null) && cannotMove.CanNotMove(moveTop, null))
                        {
                            return false;
                        }
                    }
                }

                foreach (Assets.Scripts.Script.CardEffectCommons.ICardEffect cardEffect in scanPlayer.EffectList(Assets.Scripts.Script.CardEffectCommons.EffectTiming.None))
                {
                    if (cardEffect is Assets.Scripts.Script.CardEffectCommons.ICanNotMoveEffect cannotMove
                        && cardEffect.CanUse(null) && cannotMove.CanNotMove(moveTop, null))
                    {
                        return false;
                    }
                }
            }
        }

        // AS-IS `if (TopCard.IsDigiEgg && DP <= 0) return false;` (Permanent.cs:2069-2071): a Digi-Egg with no DP
        // cannot move. This is the actual egg-move block (IsDigimon is true for an egg, so the guard above lets it
        // through) — a DP>0 egg CAN move, matching AS-IS.
        if (definition.IsCardType("DigiEgg") && ReadDp(context, instance, definition) <= 0)
        {
            return false;
        }

        return true;
    }

    private static int ReadDp(EngineContext context, CardInstanceRecord instance, CardRecord definition)
    {
        if (instance.Metadata.TryGetValue("dp", out object? iv) && iv is int idp) return idp;
        if (definition.Metadata.TryGetValue("dp", out object? dv) && dv is int ddp) return ddp;
        return 0;
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
                // (RD-S3D-01) The AS-IS selection UI only activates its confirm button when the request's
                // combination validator (CanEndSelect) accepts the selected set — "on the table = executable"
                // is the AS-IS contract. A ResolveChoice table entry IS a complete size-1 resolution, so a
                // candidate whose 1-element set fails the SelectionValidator must not be listed (picking it
                // could only throw/fail at resolve time). Validator-less requests keep the previous table.
                if (request.SelectionValidator is not null
                    && !request.SelectionValidator(new[] { candidate.Id }))
                {
                    continue;
                }

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
