namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

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
        // selection wait: Pass + the main-phase plays.
        bool mainSelectionWait =
            turn.Phase == HeadlessPhase.Main && turn.StepCursor == TurnStepCursor.PhaseStart;

        // (RD-R4B6-P2-1 → SpecialPlay re-migration) A non-pump (OLD/scripting) match dispatches NO step-cadence
        // / phase-flow actions — system/zone/choice actions are applied directly in the unguarded profile. This
        // arm USED to make ONE exception, enumerating SpecialPlay off the invented SpecialPlayAction +
        // SpecialPlayRecipeRegistry pair; both are DELETED (their rule logic re-migrated to the AS-IS
        // SelectDigiXrosClass / SelectJogressEffect / SelectBurstDigivolutionEffect components, which the
        // ORDINARY PlayCard entry drives — CardController.cs:3387-3404). There is therefore no separate
        // SpecialPlay legal set on ANY lane: exactly the Option A position the pump table below already takes
        // (a crafted SpecialPlay packet is rejected at the boundary — NormalizedSpecialPlay ∈ AgentFacingTypes,
        // absent from every dispatched set).
        if (TurnFlowPumpHost.Find(context) is null)
        {
            return Array.Empty<LegalAction>();
        }

        if (!mainSelectionWait)
        {
            return Array.Empty<LegalAction>();
        }

        // (RD-RLENV-05 — Option A landed, batch 7b) SpecialPlay is DELIBERATELY OMITTED from the pump table.
        // The DigiXros/Assembly component cluster (SelectDigiXrosClass.Select / SelectAssemblyClass.Select) is now
        // fully ported (RD-P6C1-5 / RD-R5-04 상환: Permanent.CanSubstituteForDigiXrosCondition + SelectHandEffect
        // 미러 실착지), so its pump EXECUTION IS the mirror interactive pre-play selection reached through the
        // ORDINARY PlayCard entry below: a HasDigiXros / HasAssembly card is already offered by
        // PlayCardLegalActions (availability-projected cost), and the pump play routes it through
        // Cec.PlayCardAction → PlayCardClass.PlayCard → SelectDigiXros/SelectAssembly (CardController.cs:3387-3404),
        // which tucks the materials and applies the live cost subtraction (CardSource.cs:1173-1233). Adding a
        // separate SpecialPlay packet to this table would be a REDUNDANT invented shortcut for the same play, so it
        // stays out (Option A); a crafted pump-match SpecialPlay is REJECTED at the boundary
        // (NormalizedSpecialPlay ∈ AgentFacingTypes, absent from this set). The invented SpecialPlayAction path
        // that the NON-PUMP lane above used to keep is now DELETED too (SpecialPlay re-migration), so Option A
        // holds uniformly on both lanes. Witness: RD-BATCH7B.Witness.
        return new[] { HeadlessActionFactory.Pass(playerId) }
            .Concat(PlayCardLegalActions(context, playerId))
            .Concat(DigivolveLegalActions(context, playerId))
            .Concat(new OptionActivateAction().GetLegalActions(context, playerId))
            .Concat(new MainSkillActivateAction().GetLegalActions(context, playerId))
            .Concat(AttackLegalActions(context, playerId))
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
            context.CardInstanceRepository.Snapshot().Count == 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// (PLAYCARD cluster teardown) The PlayCard lane of the pump action table — re-homed here from the retired
    /// invented <c>Headless/Runtime/PlayCardAction.cs</c>, exactly as <see cref="DigivolveLegalActions"/> was.
    /// Only the ENUMERATION half survived that teardown: the class's <c>ProcessAsync</c> was already superseded
    /// (play is pump-only — <see cref="MetadataActionProcessor"/> rejects it and the <c>TurnFlowDriver</c>
    /// converts the packet to the AS-IS <c>PlayCardAction(cardIndex, targetFrameID, …)</c> →
    /// <c>PlayCardClass.PlayCard()</c>), and its validation body was a substrate re-implementation of machinery
    /// the mirror now holds AS-IS-literally on <see cref="Cec.CardSource"/>. Enumeration is a headless-only
    /// surface (AS-IS has no legal-action table — the UI offers frames), so it stays substrate.
    ///
    /// <para>Legality is the AS-IS seat <c>CardSource.CanPlayCardTargetFrame</c> reduces to for an EMPTY
    /// BATTLE-AREA frame (CardSource.cs:1144-1171) — the same reduction the live mirror getter
    /// <c>CardSource.CanPlayFromHandDuringMainPhase</c> (CardSource.cs:1004-1012, the AS-IS :148-157
    /// <c>CanPutFieldThisPermanentCard(true, null)</c> arm) already inlines: NOT an Option (:1146) + the offered
    /// cost payable against <c>Player.MaxMemoryCost</c> (:1150-1155) + <c>CanEnterField(null)</c> (:1167). The
    /// frame-capacity half of the AS-IS frame model is omitted (RD-P6C1-2, established no-capacity convention).
    /// The card's OCCUPIED-frame arm is the digivolve lane (<see cref="DigivolveLegalActions"/>), so this lane
    /// does not evaluate <c>CanEvolve</c> — the two lanes together are the AS-IS per-frame disjunction.</para>
    ///
    /// <para>DROPPED with the substrate class (each duplicated a now-live AS-IS getter or a retired helper):
    /// the <c>PlayCostHelpers.TryResolveCost</c> metadata pre-fold (that helper is RETIRED — AS-IS has no such
    /// stage; the printed base cost goes straight into the ChangeCostClass fold, so the cost is now the AS-IS
    /// <c>CardSource.PayingCost(Root.Hand, null, checkAvailability: true)</c>), the instance/owner/zone/
    /// from-zone/to-zone/negative-cost re-validations (this lane iterates the player's OWN hand through the zone
    /// reader, so they are structurally guaranteed — the <see cref="DigivolveLegalActions"/> precedent), and the
    /// separate <c>Validate</c> pass over a freshly built payload (the offered action IS the validated one).</para>
    /// </summary>
    private static IReadOnlyList<LegalAction> PlayCardLegalActions(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        if (playerId.IsEmpty || context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return Array.Empty<LegalAction>();
        }

        // SUBSTRATE: the AS-IS legality/cost scans (CanUse → CheckEffectDisabledClass, GetChangedCost…) read game
        // state through the process-global GManager.instance, which the mirror resolves from AmbientMatchContext.
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);

        var player = new Cec.Player(context, playerId);
        List<LegalAction> actions = new();

        foreach (HeadlessEntityId cardId in zoneReader.GetCards(playerId, ChoiceZone.Hand).ToArray())
        {
            var view = new Cec.CardSource(context, cardId, playerId, playerId);

            // AS-IS :1146-1149 — an Option never takes the EMPTY-frame play seat; its main-phase entry is the
            // Option lane (OptionActivateAction → the same mirror PlayCardClass option play).
            if (view.IsOption)
            {
                continue;
            }

            // AS-IS :1150 PayingCost(root, {frame permanent}, checkAvailability: true). The empty frame carries no
            // target permanent, so targetPermanents is null — verbatim the CanPlayFromHandDuringMainPhase inline.
            int playCost = view.PayingCost(
                HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand,
                targetPermanents: null,
                checkAvailability: true);

            // AS-IS :1167 — the ICanNotPutFieldEffect field-placement scan (e.g. EX7_014). A player-initiated hand
            // play carries no source cardEffect (AS-IS CanPlayFromHandDuringMainPhase → CanPutFieldThisPermanentCard
            // (true, null)). Returns true for the vast majority of plays (no producer registered).
            if (!view.CanEnterField(null))
            {
                continue;
            }

            // AS-IS :1152 `Owner.MaxMemoryCost < cost` — the live mirror gauge getter (Player.cs:259).
            if (player.MaxMemoryCost >= playCost)
            {
                actions.Add(HeadlessActionFactory.PlayCard(playerId, cardId, playCost));
            }

            // (AD1-A) the Assembly variant of the SAME play (AS-IS folds it into the ordinary play flow,
            // CardController.cs:753-761). RETAINED from the substrate class because the mirror cost pipeline
            // DELEGATES its Assembly availability projection to this enumeration seat: unlike DigiXros, the mirror
            // does NOT take the AS-IS `if (checkAvailability) return 0;` early-return for Assembly, and instead
            // offers a dedicated variant at the PRECISE (base - reduceCost) — the documented ADAPTATION at
            // CardSource.cs:1550-1562. Dropping this arm would delete that projection outright, so an Assembly card
            // affordable ONLY at the discounted cost would vanish from the table (AS-IS offers it: availability 0).
            // Legality reuses the live AS-IS-position mirror halves — CardSource.AssemblyConditionOf and the STATIC
            // feasibility matcher SelectAssemblyClass.TryMatchMaterials (SelectAssemblyClass.cs:403) — plus the same
            // CanEnterField gate already passed above; AS-IS gates the Assembly play on `card.HasAssembly &&
            // !isEvolution` ALONE (CardController.cs:755), reduceCost-independent.
            if (view.AssemblyConditionOf() is not Cec.AssemblyCondition condition ||
                !HeadlessDCGO.Engine.Assets.Scripts.Script.SelectAssemblyClass.TryMatchMaterials(
                    context, view, condition, out List<HeadlessEntityId> materials))
            {
                continue;
            }

            // AS-IS: Cost -= reduceCost only for the FULL set (GetPayingCost, CardSource.cs:705-737).
            int reducedCost = Math.Max(0, playCost - condition.reduceCost);
            if (player.MaxMemoryCost < reducedCost)
            {
                continue;
            }

            PlayCardActionPayload payload = new(cardId, reducedCost, ChoiceZone.Hand, ChoiceZone.BattleArea)
            {
                AssemblyMaterials = materials,
            };
            actions.Add(HeadlessActionFactory.Create(
                HeadlessActionTypes.PlayCard,
                playerId,
                $"{playerId.Value}:{HeadlessActionTypes.PlayCard}:assembly:{cardId.Value}",
                payload.ToParameters()));
        }

        return actions
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// (ATTACK cluster teardown) The DeclareAttack lane of the pump action table — re-homed here from the retired
    /// invented <c>Headless/Runtime/AttackPermanentAction.cs</c>, the same move as <see cref="DigivolveLegalActions"/>
    /// and <see cref="PlayCardLegalActions"/>. Only the ENUMERATION half survived: that class's <c>Process</c> is
    /// superseded (declaration is pump-only — <see cref="MetadataActionProcessor"/> rejects it and the
    /// <c>TurnFlowDriver</c> converts the packet to the AS-IS <c>AttackPermanentAction(attackerIndex,
    /// targetIndex)</c> → <c>AttackProcess.Attack</c>), and its ~200-line <c>Validate</c> body was a substrate
    /// re-implementation of <see cref="Cec.Permanent.CanAttack"/> / <see cref="Cec.Permanent.CanAttackTargetDigimon"/>,
    /// which the mirror now holds AS-IS-literally (Permanent.cs:3432 / :3560, 1:1 of AS-IS Permanent.cs:2090-2372).
    ///
    /// <para>Legality is exactly the AS-IS pair: the attacker gate is <c>Permanent.CanAttack(null)</c> — the AS-IS
    /// main-phase selectability term (TurnStateMachine.cs :921 <c>GetFieldPermanents().Count(p =&gt; p.CanAttack(null))</c>,
    /// mirrored at TurnStateMachine.cs:568) — and each candidate is <c>CanAttackTargetDigimon(defender, null)</c>,
    /// with <c>defender == null</c> being the AS-IS spelling of the direct (security) attack. Iteration follows the
    /// AS-IS collections and the TurnFlowDriver's index currency: attackers = turn player's
    /// <c>GetFieldPermanents()</c>, candidates = <c>Enemy.GetFieldPermanents()</c> (AS-IS Permanent.cs:3454) — the
    /// battle-area restriction is CanAttackTargetDigimon's own <c>IsPermanentExistsOnBattleArea</c> /
    /// <c>GetBattleAreaPermanents().Contains(Defender)</c> check, not a pre-filter here.</para>
    ///
    /// <para>DROPPED with the substrate class — every item below is a term the live AS-IS getters already
    /// evaluate, so keeping it would have been a second, divergent copy:
    /// the <c>canAttack</c>/<c>cannotAttack</c>/<c>isSuspended</c>/<c>canSuspend</c>/<c>enteredThisTurn</c>/
    /// <c>hasRush</c>/<c>canAttackUnsuspendedDigimon</c> metadata-flag layer (→ <c>Permanent.IsSuspended</c>
    /// (Permanent.cs:1695, which READS the isSuspended metadata key — the fixture seam is preserved),
    /// <c>Permanent.CanSuspend</c> (:1953), <c>Permanent.EnteredThisTurn</c>, <c>Permanent.HasRush</c> (:1196));
    /// <c>ContinuousRestrictionGate.EvaluateAttack</c> and <c>EvaluateBeAttacked</c> (both DELETED — AS-IS
    /// expresses BOTH as the single <c>ICanNotAttackTargetDefendingPermanentEffect</c> scan inside
    /// CanAttackTargetDigimon, and "this Digimon cannot be attacked" is produced as exactly that interface:
    /// <c>CardEffectFactory.CanNotBeAttackedSelfStaticEffect</c> returns a
    /// <c>CanNotAttackTargetDefendingPermanentClass</c>); <c>ContinuousKeywordGate.HasKeyword(Execute)</c>
    /// (DELETED — AS-IS "unsuspended Digimon can also be attacked" is the
    /// <c>ICanAttackTargetDefendingPermanentEffect</c> scan at Permanent.cs:3662-3701, plus the effect-driven
    /// <c>isExecute</c> parameter, which a player declaration passes false); the attacker/target IsDigimon,
    /// owner, battle-area-membership, turn-player, another-attack-pending and target-suspended checks (all inside
    /// CanAttack / CanAttackTargetDigimon); and the <c>IsMainPlayPhase</c> gate (this table is only built at the
    /// main selection wait — see the mainSelectionWait guard above — and AS-IS re-checks turn/attack state inside
    /// CanAttack itself).</para>
    /// </summary>
    private static IReadOnlyList<LegalAction> AttackLegalActions(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        if (playerId.IsEmpty)
        {
            return Array.Empty<LegalAction>();
        }

        HeadlessPlayerId? defendingPlayerId = context.TurnController.Current.NonTurnPlayerId;
        if (!defendingPlayerId.HasValue)
        {
            return Array.Empty<LegalAction>();
        }

        // SUBSTRATE: the AS-IS restriction scans reach game state through GManager.instance / the ambient match.
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);

        List<Cec.Permanent> defenders = new Cec.Player(context, defendingPlayerId.Value).GetFieldPermanents();
        List<LegalAction> actions = new();

        foreach (Cec.Permanent attacker in new Cec.Player(context, playerId).GetFieldPermanents())
        {
            // AS-IS TurnStateMachine :921 — the attacker-level gate (empty stack, turn ownership, no attack in
            // progress, and at least one reachable target).
            if (!attacker.CanAttack(null))
            {
                continue;
            }

            // AS-IS Permanent.cs:3451 `CanAttackTargetDigimon(null, …)` — the direct (security) attack.
            if (attacker.CanAttackTargetDigimon(null, null))
            {
                actions.Add(HeadlessActionFactory.DeclareAttack(
                    playerId, attacker.InstanceId, defendingPlayerId.Value, targetId: null, isDirectAttack: true));
            }

            // AS-IS Permanent.cs:3454 `Enemy.GetFieldPermanents().Count(p => CanAttackTargetDigimon(p, …))`.
            foreach (Cec.Permanent defender in defenders)
            {
                if (attacker.CanAttackTargetDigimon(defender, null))
                {
                    actions.Add(HeadlessActionFactory.DeclareAttack(
                        playerId, attacker.InstanceId, defendingPlayerId.Value, defender.InstanceId, isDirectAttack: false));
                }
            }
        }

        return actions
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// (DIGIVOLVE cluster teardown) The Digivolve lane of the pump action table — re-homed here from the retired
    /// invented <c>Headless/Runtime/DigivolveAction.cs</c>. Only the ENUMERATION half survived that teardown: the
    /// class's <c>ProcessAsync</c> was already superseded (digivolve is pump-only — <c>MetadataActionProcessor</c>
    /// rejects it and the <c>TurnFlowDriver</c> converts the packet to the AS-IS
    /// <c>PlayCardAction(cardIndex, targetFrameID, …)</c> → <c>PlayCardClass.PlayCard()</c>), and its ~900-line
    /// legality/cost body was a substrate re-implementation of machinery the mirror now holds AS-IS-literally on
    /// <see cref="Cec.CardSource"/> (<c>EvoCosts</c> / <c>CostList</c> / <c>CanEvolve</c> / <c>CanNotEvolve</c> /
    /// <c>AddedDigivolutionCosts</c> / <c>IgnoreColorConditionActive</c> / <c>PayingCost</c>). Enumeration is a
    /// headless-only surface (AS-IS has no legal-action table — the UI offers frames), so it stays substrate,
    /// alongside the other lanes of this table.
    ///
    /// Legality is the AS-IS seat <c>CardSource.CanPlayCardTargetFrame</c> reduces to for an OCCUPIED frame
    /// (CardSource.cs:1116-1194): owner match (guaranteed by iterating the player's own zones) +
    /// <c>CanEvolve(framePermanent, checkAvailability)</c>. The offered cost is the AS-IS
    /// <c>PayingCost(root, {target})</c> — <c>CostList(target).Min()</c> folded through the ChangeCostClass
    /// pipeline — and only affordable actions are offered (the pump has no "declare then fail to pay" lane).
    /// GR-004: a target may sit in the BATTLE area or the BREEDING area (the digi-egg ramp).
    /// </summary>
    private static IReadOnlyList<LegalAction> DigivolveLegalActions(
        EngineContext context,
        HeadlessPlayerId playerId)
    {
        if (playerId.IsEmpty || context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return Array.Empty<LegalAction>();
        }

        // SUBSTRATE: the AS-IS legality/cost scans (CanUse → CheckEffectDisabledClass, GetChangedCost…) read game
        // state through the process-global GManager.instance, which the mirror resolves from AmbientMatchContext.
        using AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context);

        HeadlessEntityId[] handCards = zoneReader.GetCards(playerId, ChoiceZone.Hand).ToArray();
        HeadlessEntityId[] targetCards = zoneReader.GetCards(playerId, ChoiceZone.BattleArea)
            .Concat(zoneReader.GetCards(playerId, ChoiceZone.BreedingArea))
            .ToArray();
        List<LegalAction> actions = new();

        foreach (HeadlessEntityId cardId in handCards)
        {
            var evolvingCard = new Cec.CardSource(context, cardId, playerId, playerId);
            foreach (HeadlessEntityId targetCardId in targetCards)
            {
                if (cardId == targetCardId)
                {
                    continue;
                }

                var targetPermanent = new Cec.Permanent(context, targetCardId, playerId);
                if (!evolvingCard.CanEvolve(targetPermanent, checkAvailability: false))
                {
                    continue;
                }

                int evolutionCost = evolvingCard.PayingCost(
                    HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand,
                    new List<Cec.Permanent> { targetPermanent });
                if (!context.MemoryController.CanPay(evolutionCost))
                {
                    continue;
                }

                actions.Add(HeadlessActionFactory.Digivolve(playerId, cardId, targetCardId, evolutionCost));
            }
        }

        // (W6-F) App Fusion (AS-IS CardController.cs:400 — an EVOLUTION variant): a hand card declaring an
        // AppFusionCondition may fuse onto an owner Digimon whose top matches one material and one of whose LINK
        // cards matches a different material; the chosen link card is consumed into the fused sources. Legality is
        // the AS-IS getter CardSource.CanAppFusionFromTargetPermanent (the declared condition IS the requirement —
        // the printed level/colour requirement does not apply); cost = the condition's cost through the same
        // play-cost pipeline.
        foreach (HeadlessEntityId cardId in handCards)
        {
            var view = new Cec.CardSource(context, cardId, playerId, playerId);
            if (view.AppFusionConditionOf() is not Cec.AppFusionCondition condition)
            {
                continue;
            }

            int fusionCost = Math.Max(0, view.GetPayingCostWithBaseCost(
                condition.cost, HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect.Root.Hand, targetPermanents: null));
            if (!context.MemoryController.CanPay(fusionCost))
            {
                continue;
            }

            foreach (HeadlessEntityId hostId in zoneReader.GetCards(playerId, ChoiceZone.BattleArea))
            {
                var host = new Cec.Permanent(context, hostId, playerId);
                if (!view.CanAppFusionFromTargetPermanent(host, PayCost: false))
                {
                    continue;
                }

                // AS-IS Permanent.LinkedCards (Permanent.cs:1041) — the host's live link cards.
                foreach (Cec.CardSource linkView in host.LinkedCards)
                {
                    if (!condition.linkedCondition(host, linkView))
                    {
                        continue;
                    }

                    HeadlessEntityId linkId = linkView.InstanceId;
                    var payload = new DigivolveActionPayload(cardId, hostId, fusionCost) { AppFusionLinkCardId = linkId };
                    actions.Add(HeadlessActionFactory.Create(
                        HeadlessActionTypes.Digivolve, playerId,
                        $"{playerId.Value}:{HeadlessActionTypes.Digivolve}:appfusion:{cardId.Value}:{hostId.Value}:{linkId.Value}",
                        payload.ToParameters()));
                }
            }
        }

        return actions
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }
}
