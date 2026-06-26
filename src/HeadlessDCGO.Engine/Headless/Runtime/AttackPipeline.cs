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
public sealed class AttackPipeline
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

    public async Task<AttackAdvanceResult> AdvanceAsync(
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
            AttackPhase.Resolved => AdvanceEndAttack(context, attack),
            AttackPhase.Completed => AdvanceCleanup(context),
            _ => AttackAdvanceResult.Idle(),
        };
    }

    private AttackAdvanceResult AdvanceBlockTiming(EngineContext context)
    {
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

    // G3.5-RL-C2: the security check a Piercing attacker performs after winning a battle. Mirrors a
    // direct attack's security check (strike-count cards, no-security → defending player loses) but
    // runs after the battle has already resolved the attack.
    private static async Task ApplyPiercingSecurityAsync(
        EngineContext context,
        HeadlessAttackState attack,
        CancellationToken cancellationToken)
    {
        if (attack.DefendingPlayerId is not HeadlessPlayerId defender ||
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

        int available = zoneReader.GetCards(defender, ChoiceZone.Security).Count;
        int checkCount = Math.Min(strike, available);
        for (int index = 0; index < checkCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<HeadlessEntityId> security = zoneReader.GetCards(defender, ChoiceZone.Security);
            if (security.Count == 0)
            {
                break;
            }

            await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(defender, security[0], ChoiceZone.Security, ChoiceZone.Trash, FaceUp: true),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static int ReadStrike(EngineContext context, HeadlessEntityId? attackerId)
    {
        if (attackerId is not HeadlessEntityId id ||
            !context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? attacker) ||
            attacker is null)
        {
            return 1;
        }

        if (attacker.Metadata.TryGetValue(SecurityResolver.StrikeKey, out object? raw) && raw is not null)
        {
            return raw switch
            {
                int intValue => Math.Max(0, intValue),
                long longValue when longValue is >= int.MinValue and <= int.MaxValue => Math.Max(0, (int)longValue),
                string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => Math.Max(0, parsed),
                _ => 1
            };
        }

        return 1;
    }

    private static AttackAdvanceResult AdvanceEndAttack(EngineContext context, HeadlessAttackState attack)
    {
        int enqueued = 0;
        if (attack.AttackingPlayerId is HeadlessPlayerId turnPlayer)
        {
            var hook = new EndAttackTriggerHook(new AutoProcessingTriggerCollector(context.EffectRegistry));
            EndAttackTriggerHookResult result = hook.Process(
                attack,
                attack.AttackCount,
                context.EffectScheduler,
                turnPlayer,
                attack.DefendingPlayerId);
            enqueued = result.EnqueuedMandatoryCount;
        }

        context.AttackController.AdvancePhase(AttackPhase.Completed, "End attack triggers collected.");
        return AttackAdvanceResult.Transitioned(AttackPhase.Resolved, AttackPhase.Completed, enqueuedEndAttackTriggers: enqueued);
    }

    private static AttackAdvanceResult AdvanceCleanup(EngineContext context)
    {
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
