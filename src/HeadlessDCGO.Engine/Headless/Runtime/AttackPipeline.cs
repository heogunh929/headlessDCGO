namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Single-step attack state machine. Mirrors Unity AS-IS <c>AttackProcess.ProcessNextState()</c>:
/// each <see cref="AdvanceAsync"/> call moves the current attack forward by exactly one
/// <see cref="AttackPhase"/> (Declared → Blocking → Combat → Resolved → Completed → cleared).
///
/// Re-entrancy: when block timing opens a choice the attack parks in <see cref="AttackPhase.Blocking"/>
/// and the common loop (<see cref="GameFlowProcessor"/>) pauses. The block choice is resolved by the
/// next <c>ResolveChoice</c> action (routed through <see cref="BlockTiming.ResolveBlockChoice"/>),
/// after which the loop resumes here and continues to combat.
/// </summary>
// Non-sealed with a virtual AdvanceAsync so the GameFlowProcessor's injected-pipeline seam can be
// substituted in tests (e.g. forcing a non-converging loop to exercise MaxIterationsExceeded, GPT-#3).
public class AttackPipeline
{
    private readonly BlockTiming _blockTiming;
    private readonly BattleResolver _battleResolver;
    private readonly SecurityResolver _securityResolver;

    public AttackPipeline(
        BlockTiming? blockTiming = null,
        BattleResolver? battleResolver = null,
        SecurityResolver? securityResolver = null)
    {
        _blockTiming = blockTiming ?? new BlockTiming();
        _battleResolver = battleResolver ?? new BattleResolver();
        _securityResolver = securityResolver ?? new SecurityResolver();
    }

    public virtual async Task<AttackAdvanceResult> AdvanceAsync(
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        HeadlessAttackState attack = context.AttackController.Current;
        return attack.Phase switch
        {
            AttackPhase.Declared => AdvanceBlockTiming(context),
            AttackPhase.Blocking => AdvanceAfterBlock(context),
            AttackPhase.Combat => await AdvanceCombatAsync(context, attack, cancellationToken).ConfigureAwait(false),
            AttackPhase.DeletionReplacement => await AdvanceDeletionReplacementAsync(context, attack, cancellationToken).ConfigureAwait(false),
            AttackPhase.Resolved => await AdvanceEndAttackAsync(context, attack, cancellationToken).ConfigureAwait(false),
            AttackPhase.Completed => AdvanceCleanup(context),
            _ => AttackAdvanceResult.Idle(),
        };
    }

    private AttackAdvanceResult AdvanceBlockTiming(EngineContext context)
    {
        // C-3 Raid (F-6.8): an attacker with <Raid> MAY switch the attack onto the opponent's highest-DP
        // unsuspended Digimon — an OPTIONAL "you may" the controller decides (auto would change the rules).
        // Opening the choice parks the pipeline at Declared; on resolution this runs again (attacker marked
        // resolved) and proceeds to counter/block timing with the switched defender.
        if (RaidAttackSwitch.RequestChoice(context))
        {
            return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Declared, choiceRequested: true);
        }

        // C-18 Alliance (after Raid, still at Declared): an attacker with <Alliance> MAY suspend an ally to
        // gain its DP + 1 Security Attack (UntilEndAttack) — an OPTIONAL "you may" the controller decides.
        // Opens before the battle DP comparison so the buff applies this battle; self-gates via its own
        // resolved marker so it opens at most once per attack (after Raid has resolved + re-entered).
        if (AllianceAttackBoost.RequestChoice(context))
        {
            return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Declared, choiceRequested: true);
        }

        // C-15 Progress (S2): a Progress attacker is passively immune to the opponent's effects until the
        // attack ends — a static effect (no agent choice), so it is applied automatically here. The immunity
        // auto-expires with the attack (UntilEndAttack); HasEffect guards against re-registering on re-entry.
        ProgressImmunity.TryRegister(context);

        // W6: the counter timing window opens once per attack, before block timing (AS-IS
        // AttackProcess: State=Counter → CounterTiming → Block). A global window (no subject filter) so
        // any card's OnCounterTiming / [Counter] effect is collected and self-gates, mirroring the
        // original StackSkillInfos(OnCounterTiming).
        TriggerEventEmitter.Emit(
            context.GameEventQueue,
            TriggerTimings.OnCounter,
            actor: context.AttackController.Current.AttackingPlayerId);

        BlockTimingResult block = _blockTiming.RequestBlockChoice(context);
        if (block.IsSuccess && block.ChoiceRequested)
        {
            context.AttackController.AdvancePhase(AttackPhase.Blocking, "Block timing opened.");
            return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Blocking, choiceRequested: true);
        }

        // No legal blockers (auto-skip) or block timing not applicable: proceed to combat.
        context.AttackController.AdvancePhase(AttackPhase.Combat, "Block timing skipped (no blockers).");
        return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Combat);
    }

    private static AttackAdvanceResult AdvanceAfterBlock(EngineContext context)
    {
        // The block choice has already been resolved (selection applied via SelectBlocker, or skipped).
        context.AttackController.AdvancePhase(AttackPhase.Combat, "Block timing resolved.");
        return AttackAdvanceResult.Transitioned(AttackPhase.Blocking, AttackPhase.Combat);
    }

    private async Task<AttackAdvanceResult> AdvanceCombatAsync(
        EngineContext context,
        HeadlessAttackState attack,
        CancellationToken cancellationToken)
    {
        bool directCheck = attack.IsDirectAttack && !attack.TargetId.HasValue && !attack.IsBlocked;
        if (directCheck)
        {
            SecurityResolutionResult security = await _securityResolver
                .ResolveAsync(context, cancellationToken)
                .ConfigureAwait(false);
            if (!security.IsSuccess && context.AttackController.Current.IsPending)
            {
                // (X-02) AS-IS AttackProcess: a direct attack against an empty security stack hits the
                // player directly. Mark the defender as lost so the end-turn check resolves the match.
                if (security.DefenderHasNoSecurity
                    && attack.DefendingPlayerId is HeadlessPlayerId defendingPlayer)
                {
                    context.PlayerStatusController.MarkLose(
                        defendingPlayer,
                        "Direct attack resolved with no security to check.");
                }

                context.LogSink.Warn($"[AttackPipeline] security resolution failed: {security.FailureReason}");
                context.AttackController.ResolveAttack($"Security resolution failed: {security.FailureReason}");
            }

            return AttackAdvanceResult.Transitioned(AttackPhase.Combat, AttackPhase.Resolved, securityResolved: true);
        }

        BattleResolutionResult battle = await _battleResolver
            .ResolveAsync(context, cancellationToken)
            .ConfigureAwait(false);

        // F-6.8: battle deletion deferred for an optional replacement choice — park here; the common loop
        // opens the would-be-deleted window, then the DeletionReplacement phase finalizes the battle.
        if (battle.RequiresDeletionReplacement)
        {
            context.AttackController.AdvancePhase(AttackPhase.DeletionReplacement, "Battle deletion replacement window.");
            return AttackAdvanceResult.Transitioned(AttackPhase.Combat, AttackPhase.DeletionReplacement);
        }

        if (!battle.IsSuccess && context.AttackController.Current.IsPending)
        {
            context.LogSink.Warn($"[AttackPipeline] battle resolution failed: {battle.FailureReason}");
            context.AttackController.ResolveAttack($"Battle resolution failed: {battle.FailureReason}");
        }

        // G3.5-RL-C2: Piercing — a surviving attacker that deleted the defender in battle also
        // checks the defending player's security.
        if (battle.IsSuccess && battle.TriggersPiercingSecurityCheck)
        {
            await ApplyPiercingSecurityAsync(context, attack, cancellationToken).ConfigureAwait(false);
        }

        return AttackAdvanceResult.Transitioned(AttackPhase.Combat, AttackPhase.Resolved, battleResolved: true);
    }

    // F-6.8: the would-be-deleted replacement windows for this battle have all resolved (the common loop
    // pauses while any remain). Finalize the battle — the still-flagged participants are the casualties.
    private async Task<AttackAdvanceResult> AdvanceDeletionReplacementAsync(
        EngineContext context,
        HeadlessAttackState attack,
        CancellationToken cancellationToken)
    {
        BattleResolutionResult battle = await _battleResolver
            .FinalizeDeferredAsync(context, cancellationToken)
            .ConfigureAwait(false);
        if (!battle.IsSuccess && context.AttackController.Current.IsPending)
        {
            context.LogSink.Warn($"[AttackPipeline] deferred battle finalize failed: {battle.FailureReason}");
            context.AttackController.ResolveAttack($"Battle finalize failed: {battle.FailureReason}");
        }

        if (battle.IsSuccess && battle.TriggersPiercingSecurityCheck)
        {
            await ApplyPiercingSecurityAsync(context, attack, cancellationToken).ConfigureAwait(false);
        }

        return AttackAdvanceResult.Transitioned(AttackPhase.DeletionReplacement, AttackPhase.Resolved, battleResolved: true);
    }

    // G3.5-RL-C2 / D-1: the security check a Piercing attacker performs after winning a battle. Reuses
    // SecurityResolver's shared per-card loop so it is IDENTICAL to a direct attack's check — including
    // the OnSecurityCheck window (W4) and the security-Digimon battle (W5), which the old stripped loop
    // skipped. No-security → defending player loses.
    private async Task ApplyPiercingSecurityAsync(
        EngineContext context,
        HeadlessAttackState attack,
        CancellationToken cancellationToken)
    {
        if (attack.DefendingPlayerId is not HeadlessPlayerId defender ||
            attack.AttackingPlayerId is not HeadlessPlayerId attackingPlayer ||
            attack.AttackerId is not HeadlessEntityId attackerId ||
            context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return;
        }

        int strike = ReadStrike(context, attack.AttackerId);
        if (strike <= 0)
        {
            return;
        }

        if (zoneReader.GetCards(defender, ChoiceZone.Security).Count == 0)
        {
            context.PlayerStatusController.MarkLose(defender, "Piercing security check with no security to check.");
            return;
        }

        await _securityResolver.RunSecurityCheckLoopAsync(
            context,
            zoneReader,
            attackingPlayer,
            attackerId,
            defender,
            strike,
            cancellationToken).ConfigureAwait(false);
    }

    private static int ReadStrike(EngineContext context, HeadlessEntityId? attackerId)
    {
        if (attackerId is not HeadlessEntityId id ||
            !context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? attacker) ||
            attacker is null)
        {
            return 1;
        }

        int baseStrike = 1;
        if (attacker.Metadata.TryGetValue(SecurityResolver.StrikeKey, out object? raw) && raw is not null)
        {
            baseStrike = raw switch
            {
                int intValue => Math.Max(0, intValue),
                long longValue when longValue is >= int.MinValue and <= int.MaxValue => Math.Max(0, (int)longValue),
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => Math.Max(0, parsed),
                _ => 1
            };
        }

        // C-18 Alliance: fold in continuous Security-Attack modifiers (e.g. Alliance's +1 UntilEndAttack).
        return Math.Max(0, ContinuousModifierGate.ResolveSecurityAttack(context, id, baseStrike));
    }

    private static async Task<AttackAdvanceResult> AdvanceEndAttackAsync(
        EngineContext context,
        HeadlessAttackState attack,
        CancellationToken cancellationToken)
    {
        int enqueued = 0;
        if (attack.AttackingPlayerId is HeadlessPlayerId turnPlayer)
        {
            var hook = new EndAttackTriggerHook(
                new AutoProcessingTriggerCollector(context.EffectRegistry),
                mandatoryOrdering: null,
                registry: context.EffectRegistry);
            EndAttackTriggerHookResult result = hook.Process(
                attack,
                attack.AttackCount,
                context.EffectScheduler,
                turnPlayer,
                attack.DefendingPlayerId);
            enqueued = result.EnqueuedMandatoryCount;

            // The hook SEPARATES end-attack triggers (mandatory enqueued, optional held in
            // MandatoryOrder.DeferredOptionalTriggers).
            if (result.MandatoryOrder is { } order)
            {
                // Unknown-controller triggers resolve immediately (cannot apply player priority) — as in
                // the common loop's EnqueueOrdered.
                foreach (Rules.TimingWindowTrigger trigger in order.UnknownPlayerTriggers)
                {
                    context.EffectScheduler.Enqueue(trigger.Request, trigger.Mode);
                    enqueued++;
                }

                // #6 (fidelity): OPTIONAL "you may" end-attack triggers are an AGENT decision — route them
                // to the OptionalPromptQueue (turn player first), exactly like the loop's event-driven
                // optionals, instead of auto-enqueuing (which forced activation and changed the rules).
                foreach (HeadlessPlayerId? owner in new[] { (HeadlessPlayerId?)turnPlayer, attack.DefendingPlayerId })
                {
                    if (owner is not { } controller || controller.IsEmpty)
                    {
                        continue;
                    }

                    Rules.TimingWindowTrigger[] forPlayer = order.DeferredOptionalTriggers
                        .Where(trigger => trigger.Request.ControllerId == controller)
                        .ToArray();
                    if (forPlayer.Length > 0)
                    {
                        context.OptionalPromptQueue.EnqueuePrompt(forPlayer, controller);
                        enqueued += forPlayer.Length;
                    }
                }
            }
        }

        // C-9 Execute: a Digimon flagged to self-delete at end of attack (AS-IS UntilEndAttack
        // DeleteSelfEffect — Execute's "attack, then delete this Digimon") is trashed now its attack is
        // over. Combined with the existing canAttackUnsuspendedDigimon flag, this is Execute's mechanism.
        await DeleteSelfAtEndOfAttackAsync(context, attack, cancellationToken).ConfigureAwait(false);

        // F-1.5: the attack is over — expire continuous bindings that last only until the end of this
        // attack (the battle DP comparison already happened in the earlier battle phase, so these have
        // served their purpose). Done before the phase flips to Completed.
        EffectDurationExpiry.ExpireAttackEnd(context.EffectRegistry);

        context.AttackController.AdvancePhase(AttackPhase.Completed, "End attack triggers collected.");
        return AttackAdvanceResult.Transitioned(AttackPhase.Resolved, AttackPhase.Completed, enqueuedEndAttackTriggers: enqueued);
    }

    private const string DeleteSelfAtEndOfAttackKey = "deleteSelfAtEndOfAttack";
    private const string DeletedByEffectKey = "deletedByEffect";

    private static async Task DeleteSelfAtEndOfAttackAsync(
        EngineContext context,
        HeadlessAttackState attack,
        CancellationToken cancellationToken)
    {
        if (attack.AttackerId is not HeadlessEntityId attackerId ||
            !context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) ||
            attacker is null ||
            !ReadSelfDeleteFlag(attacker.Metadata) ||
            context.ZoneMover is not IZoneStateReader zones ||
            !zones.GetCards(attacker.OwnerId, ChoiceZone.BattleArea).Contains(attackerId))
        {
            return;
        }

        context.CardInstanceRepository.Upsert(attacker with
        {
            Metadata = new Dictionary<string, object?>(attacker.Metadata, StringComparer.Ordinal)
            {
                [DeletedByEffectKey] = true,
            }
        });
        await context.ZoneMover.AddToTrashAsync(attacker.OwnerId, attackerId, cancellationToken).ConfigureAwait(false);
    }

    private static bool ReadSelfDeleteFlag(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(DeleteSelfAtEndOfAttackKey, out object? raw) && raw is bool value && value;

    private static AttackAdvanceResult AdvanceCleanup(EngineContext context)
    {
        // C-3 Raid / C-18 Alliance: clear the per-attack resolved markers so a later attack re-offers.
        if (context.AttackController.Current.AttackerId is HeadlessEntityId attackerId)
        {
            RaidAttackSwitch.ClearResolved(context, attackerId);
            AllianceAttackBoost.ClearResolved(context, attackerId);
        }

        context.AttackController.ClearAttack();
        return AttackAdvanceResult.Transitioned(AttackPhase.Completed, AttackPhase.None);
    }
}

public sealed record AttackAdvanceResult(
    bool Progressed,
    AttackPhase FromPhase,
    AttackPhase ToPhase,
    bool ChoiceRequested,
    bool BattleResolved,
    bool SecurityResolved,
    int EnqueuedEndAttackTriggers)
{
    public static AttackAdvanceResult Idle()
    {
        return new AttackAdvanceResult(
            Progressed: false,
            FromPhase: AttackPhase.None,
            ToPhase: AttackPhase.None,
            ChoiceRequested: false,
            BattleResolved: false,
            SecurityResolved: false,
            EnqueuedEndAttackTriggers: 0);
    }

    public static AttackAdvanceResult Transitioned(
        AttackPhase fromPhase,
        AttackPhase toPhase,
        bool choiceRequested = false,
        bool battleResolved = false,
        bool securityResolved = false,
        int enqueuedEndAttackTriggers = 0)
    {
        return new AttackAdvanceResult(
            Progressed: true,
            fromPhase,
            toPhase,
            choiceRequested,
            battleResolved,
            securityResolved,
            enqueuedEndAttackTriggers);
    }
}
