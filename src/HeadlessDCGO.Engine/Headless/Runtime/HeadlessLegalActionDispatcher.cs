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

            return BuildChoiceResolutionActions(pending, context.ChoiceController.Current)
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

        // (R4 S3c-b, decision 3 = B / 4b B6) The ONLY phase-action table: the invented step cadence
        // (AdvancePhase / EndTurn / breeding actions) is physically retired — phases auto-flow in the
        // TurnFlowPump, breeding is a CHOICE (covered by the pending-choice branch above), the memory-pass
        // "awaiting" step does not exist (EndTurnCheck auto-ends), and the ONLY action surface is the MAIN
        // selection wait: Pass + the main-phase plays. A non-pump (scripting) match exposes NO dispatched
        // phase actions — system/zone/choice actions are applied directly in the unguarded profile.
        // SpecialPlay is omitted: the pump's special-play seams are the registered component STOPs
        // (RD-P6C1-5 Assembly / RD-R5-04 DigiXros) until that cluster ports.
        if (TurnFlowPumpHost.Find(context) is null)
        {
            return Array.Empty<LegalAction>();
        }

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

    /// <summary>
    /// Enumerates the choice actions a policy can take for a pending choice (G3.5-RL-A2).
    /// Single-select requests yield one ResolveChoice per selectable candidate; Count requests yield one
    /// action per allowed count; skippable requests add a Skip action. Multi-select requests
    /// (<c>Type != Count &amp;&amp; MaxCount &gt; 1</c>) open a partial-selection SESSION instead (B5-2,
    /// 설계 §B5.5): per-candidate ToggleChoiceCandidate lanes + a Confirm lane (a ResolveChoice carrying the
    /// current partial set, only while it would validate) + Skip only at zero picks — the AS-IS incremental
    /// selection loop (tap/re-tap + confirm button + "No Selection" back button) as an action surface.
    /// </summary>
    private static IReadOnlyList<LegalAction> BuildChoiceResolutionActions(
        ChoiceRequest request,
        HeadlessChoiceState state)
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
        else if (request.MaxCount > 1)
        {
            // (B5-2, 설계 §B5.5) Multi-select session — covers both forced MinCount>1 (previously an EMPTY
            // table = stall) and optional up-to-N MinCount<=1<MaxCount (previously size-1 lanes only, an
            // expressiveness gap). MaxCount<=1 requests below keep the pre-B5 table byte-for-byte (기존
            // 궤적 보존 경계). The unsatisfiable-forced demotion (§B5.6) runs at choice-OPEN time in
            // DeferredChoiceProvider, so a session that reaches this table always has a completable
            // confirmation (교착 0 논거).
            return BuildMultiSelectSessionActions(request, state);
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

    /// <summary>
    /// (B5-2, 설계 §B5.5 표) The multi-select session table, recomputed from state on every enumeration
    /// (no stored session — the partial set lives on <see cref="HeadlessChoiceState.PendingSelectedIds"/>).
    /// Lanes, with their AS-IS anchors (SelectHandEffect.cs / SelectPermanentEffect.cs):
    /// <list type="bullet">
    /// <item><b>Toggle</b> (per candidate): a picked candidate is ALWAYS legal to un-tap (AS-IS
    /// Contains→Remove precedes every gate, :271-274 — 핀 3's escape lane). An unpicked candidate is legal
    /// when it is selectable and, if the request carries the path-dependent per-pick gate
    /// (<see cref="ChoiceRequest.PartialPickGate"/> = AS-IS canTargetCondition_ByPreSelecetedList), the
    /// gate passes against the CURRENT partial set — evaluated with the LAST pick excluded when the set is
    /// at MaxCount, because that tap is a replace-last (AS-IS :283-289: the _PreSelectedList loop skips
    /// index Count-1 when Count &gt;= maxCount; W5).</item>
    /// <item><b>Confirm</b>: a ResolveChoice carrying the partial set in pick order, listed only while
    /// AS-IS CanEndSelect would light the confirm button: (count==Max || count&gt;=MinCount[the mirror
    /// translation of canEndNotMax]) &amp;&amp; SelectionValidator(set) — re-evaluated every enumeration
    /// (AS-IS CheckEndSelect runs after every tap, :575-591). "표에 뜬다=실행 가능" (B1/RD-S3D-01 계약):
    /// a listed Confirm always passes ChoiceResult.Validate.</item>
    /// <item><b>Skip</b>: only when CanSkip and ZERO picks — the AS-IS "No Selection" back button is shown
    /// only at an empty selection (:433-446; 핀 2: partial state must be toggled empty first).</item>
    /// </list>
    /// </summary>
    private static IReadOnlyList<LegalAction> BuildMultiSelectSessionActions(
        ChoiceRequest request,
        HeadlessChoiceState state)
    {
        List<LegalAction> actions = new();
        IReadOnlyList<HeadlessEntityId> picked = state.PendingSelectedIds;

        // Toggle lanes — one per candidate, in candidate (request) order, ids stable across the session.
        foreach (ChoiceCandidate candidate in request.Candidates)
        {
            bool isPicked = picked.Contains(candidate.Id);
            if (!isPicked)
            {
                if (!candidate.IsSelectable)
                {
                    continue;
                }

                if (request.PartialPickGate is not null)
                {
                    // AS-IS :283-289 — at MaxCount the tap is a replace-last, so the gate sees the partial
                    // list MINUS its last element; below MaxCount it sees the full partial list.
                    IReadOnlyList<HeadlessEntityId> gateBasis = picked.Count >= request.MaxCount
                        ? picked.Take(picked.Count - 1).ToArray()
                        : picked;
                    if (!request.PartialPickGate(gateBasis, candidate.Id))
                    {
                        continue;
                    }
                }
            }

            actions.Add(HeadlessActionFactory.ToggleChoiceCandidate(
                request.PlayerId,
                candidate.Id,
                actionId: $"togglechoice:{request.PlayerId.Value}:{candidate.Id.Value}"));
        }

        // Confirm lane — the AS-IS confirm button (CanEndSelect): count gate + combination validator.
        bool confirmCountOk = picked.Count == request.MaxCount || picked.Count >= request.MinCount;
        if (confirmCountOk && (request.SelectionValidator?.Invoke(picked) ?? true))
        {
            actions.Add(HeadlessActionFactory.ResolveChoice(
                request.PlayerId,
                ChoiceResult.Select(picked),
                actionId: $"resolvechoice:{request.PlayerId.Value}:confirm"));
        }

        // Skip lane — AS-IS back button: empty selection only (핀 2).
        if (request.CanSkip && picked.Count == 0)
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
