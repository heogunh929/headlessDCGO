namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class SecurityResolver
{
    public const string StrikeKey = "strike";
    public const string CheckedBySecurityCheckKey = "checkedBySecurityCheck";
    public const string SecurityCheckOrderKey = "securityCheckOrder";
    public const string SecurityCheckedPlayerIdKey = "securityCheckedPlayerId";
    public const string SecurityCheckAttackerIdKey = "securityCheckAttackerId";
    public const string DpKey = "dp";

    public async Task<SecurityResolutionResult> ResolveAsync(
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        HeadlessAttackState attack = context.AttackController.Current;
        string? validationFailure = ValidateSecurityCheck(
            context,
            attack,
            out CardInstanceRecord? attacker,
            out CardRecord? attackerCard,
            out IZoneStateReader? zoneReader);
        if (validationFailure is not null)
        {
            return SecurityResolutionResult.Failure(validationFailure, attack);
        }

        int strike = ReadStrike(context, attacker!, attackerCard!);
        if (strike <= 0)
        {
            HeadlessAttackState skippedAttack = context.AttackController.ResolveAttack("Security check skipped: strike is zero.");
            return SecurityResolutionResult.Success(
                skippedAttack,
                attack.DefendingPlayerId!.Value,
                strike,
                Array.Empty<HeadlessEntityId>(),
                Array.Empty<ZoneMoveResult>());
        }

        IReadOnlyList<HeadlessEntityId> security = zoneReader!.GetCards(attack.DefendingPlayerId!.Value, ChoiceZone.Security);
        if (security.Count == 0)
        {
            return SecurityResolutionResult.Failure(
                "Defending player has no security cards to check.",
                attack,
                defenderHasNoSecurity: true);
        }

        SecurityCheckLoopResult loop = await RunSecurityCheckLoopAsync(
            context,
            zoneReader,
            attack.AttackingPlayerId!.Value,
            attack.AttackerId!.Value,
            attack.DefendingPlayerId.Value,
            strike,
            cancellationToken).ConfigureAwait(false);

        // (C-5/VR-6) a security-battle loss deferred for a would-be-deleted replacement window: do NOT
        // resolve the attack — the pipeline parks at AttackPhase.DeletionReplacement, the common loop opens
        // the window, and FinalizeDeferredSecurityCheckAsync settles the attacker / resumes the check.
        if (loop.DeferredForDeletionReplacement)
        {
            return SecurityResolutionResult.Deferred(
                context.AttackController.Current,
                attack.DefendingPlayerId.Value,
                strike,
                loop.CheckedCards,
                loop.Movements,
                loop.SecurityDigimonBattles);
        }

        HeadlessAttackState resolvedAttack = context.AttackController.ResolveAttack("Security check resolved.");
        return SecurityResolutionResult.Success(
            resolvedAttack,
            attack.DefendingPlayerId.Value,
            strike,
            loop.CheckedCards,
            loop.Movements,
            loop.SecurityDigimonBattles,
            loop.AttackerDeleted);
    }

    /// <summary>
    /// (D-1 fix) The shared per-card security-check loop used by BOTH a direct attack's check
    /// (<see cref="ResolveAsync"/>) and a Piercing attacker's follow-up check (the attack pipeline).
    /// For each of the top <paramref name="strike"/> security cards: reveal face-up to the trash, open
    /// the OnSecurityCheck timing window (W4), and — if the revealed card is a Digimon — battle the
    /// attacker (W5). When the attacker is deleted the loop stops (AS-IS StopSecurityCheck). The caller
    /// owns attack-state transitions (ResolveAttack) and the no-security loss rule.
    /// </summary>
    public async Task<SecurityCheckLoopResult> RunSecurityCheckLoopAsync(
        EngineContext context,
        IZoneStateReader zoneReader,
        HeadlessPlayerId attackingPlayerId,
        HeadlessEntityId attackerId,
        HeadlessPlayerId defendingPlayerId,
        int strike,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(zoneReader);

        int available = zoneReader.GetCards(defendingPlayerId, ChoiceZone.Security).Count;
        int checkCount = Math.Min(Math.Max(0, strike), available);
        var checkedCards = new List<HeadlessEntityId>();
        var movementResults = new List<ZoneMoveResult>();
        int securityDigimonBattles = 0;
        bool attackerDeletedBySecurity = false;

        for (int index = 0; index < checkCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // (C-5 adversarial review P1-1) AS-IS StopSecurityCheck re-evaluates at EVERY iteration head
            // (CardController.cs:3893-3908 via :3930): attacker gone from the battle area / no top card / not a
            // Digimon ⇒ the whole check stops. A checked card's [Security] effect or an OnSecurityCheck reactor
            // can remove the attacker mid-loop; without this guard the remaining checks kept running.
            if (!IsAttackerStillCheckable(context, zoneReader, attackerId, attackingPlayerId))
            {
                break;
            }

            IReadOnlyList<HeadlessEntityId> security = zoneReader.GetCards(defendingPlayerId, ChoiceZone.Security);
            if (security.Count == 0)
            {
                break;
            }

            HeadlessEntityId checkedCardId = security[0];

            // W5: capture the revealed card's identity/DP before it leaves the security stack so a
            // security Digimon can battle the attacker.
            bool isSecurityDigimon = TryReadSecurityDigimonDp(context, checkedCardId, out int securityDp);

            MarkCheckedSecurityCard(context, checkedCardId, defendingPlayerId, attackerId, index + 1);
            ZoneMoveResult move = await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(
                    defendingPlayerId,
                    checkedCardId,
                    ChoiceZone.Security,
                    ChoiceZone.Trash,
                    FaceUp: true),
                cancellationToken).ConfigureAwait(false);
            checkedCards.Add(checkedCardId);
            movementResults.Add(move);

            // W1/W4/P8: resolve the OnSecurityCheck timing window for the revealed card SYNCHRONOUSLY —
            // AS-IS (CardController.cs:4108-4184) resolves AutoProcessCheck + the stacked OnSecurityCheck
            // skills BEFORE the security-Digimon battle (:4177). The queued emit drained only after the
            // whole loop, so a checked-card trigger that buffs the attacker resolved too late.
            await ResolveSecurityCheckWindowAsync(context, defendingPlayerId, checkedCardId, cancellationToken).ConfigureAwait(false);

            // G7-004: resolve the revealed card's [Security] activated effect (e.g. a Tamer/Option
            // security skill). No-op for cards with no ported SecuritySkill effect.
            try
            {
                await ActivatedEffectResolver
                    .ResolveAsync(context, checkedCardId, defendingPlayerId, EffectTiming.SecuritySkill, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DeferredChoicePendingException)
            {
                // G12-004: the [Security] effect asked the agent for a choice (interactive provider). Record
                // the suspended activation so the next ResolveChoice resumes it (re-resolving the effect, no
                // re-reveal) and stop the check — the pending choice is on the controller, so the game loop
                // pauses for the agent.
                context.DeferredActivations.Suspend(checkedCardId, EffectTiming.SecuritySkill, defendingPlayerId);
                break;
            }

            // W5: a revealed security Digimon battles the attacker. The security card is trashed by the
            // check regardless (already moved above); the only persistent outcome is the attacker's
            // fate. Mirrors AS-IS ISecurityCheck → IBattle(AttackingPermanent, DefendingCard).
            // (d-remediation) AS-IS DontBattleSecurityDigimonClass (EX4_013 "Ignore Battle"): a revealed security
            // Digimon whose own intrinsic marker says so does NOT battle the attacker (doBattle = false at :4142).
            if (isSecurityDigimon && !RevealedSecurityCardSkipsBattle(context, checkedCardId, defendingPlayerId))
            {
                securityDigimonBattles++;
                SecurityBattleOutcome outcome = await ResolveSecurityDigimonBattleAsync(
                    context, attackerId, attackingPlayerId, securityDp, zoneReader, cancellationToken).ConfigureAwait(false);
                if (outcome == SecurityBattleOutcome.Deferred)
                {
                    // (C-5/VR-6) the attacker's would-be-deleted window must resolve before the check can go
                    // on — PARK the loop (AS-IS: the PRE cut-in inside DestroyPermanentsClass suspends the
                    // whole coroutine chain mid-SecurityCheck at CardController.cs:3696). The remaining check
                    // count is stamped on the attacker; the pipeline parks at AttackPhase.DeletionReplacement
                    // and FinalizeDeferredSecurityCheckAsync resumes here once the window settled.
                    int remaining = checkCount - (index + 1);
                    StampDeferredRemaining(context, attackerId, remaining);
                    return new SecurityCheckLoopResult(
                        checkedCards, movementResults, securityDigimonBattles, AttackerDeleted: false,
                        DeferredForDeletionReplacement: true, RemainingChecks: remaining);
                }

                if (outcome == SecurityBattleOutcome.AttackerDeleted)
                {
                    attackerDeletedBySecurity = true;
                    // AS-IS StopSecurityCheck: with the attacker gone, no further security is checked.
                    break;
                }
            }
        }

        return new SecurityCheckLoopResult(checkedCards, movementResults, securityDigimonBattles, attackerDeletedBySecurity);
    }

    // --- (C-5/VR-6) deferred security-battle park/resume ----------------------------------------------

    /// <summary>(C-5/VR-6) Park marker: the number of security checks still owed after the one whose battle
    /// deferred. Present only while the attack is parked at <see cref="AttackPhase.DeletionReplacement"/>
    /// for a SECURITY-battle loss.</summary>
    public const string SecurityCheckRemainingKey = "securityCheckDeferredRemaining";

    /// <summary>Whether the attacker carries a parked (deferred) security check — the AttackPipeline's
    /// DeletionReplacement dispatch between the security resume and the field-battle finalize.</summary>
    public static bool HasDeferredSecurityCheck(EngineContext context, HeadlessEntityId attackerId) =>
        context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? record) &&
        record is not null &&
        record.Metadata.ContainsKey(SecurityCheckRemainingKey);

    /// <summary>
    /// (C-5/VR-6) Re-entered each time the pipeline advances <see cref="AttackPhase.DeletionReplacement"/>
    /// for a parked SECURITY-battle loss (the sibling of <see cref="BattleResolver.FinalizeDeferredAsync"/>):
    /// <list type="bullet">
    /// <item>window still undecided → stay parked (the common loop opens/settles the replacement choice);</item>
    /// <item>declined → finalize the deletion (cleanup → evo-source trash → top trash → Fortitude); with the
    /// attacker gone no further security is checked (AS-IS StopSecurityCheck);</item>
    /// <item>survived (Evade suspended it / Barrier trashed a security / …) → resume the REMAINING checks —
    /// AS-IS ISecurityCheck's while loop keeps checking while the attacker persists and checkedCount &lt;
    /// Strike (CardController.cs:3928-3940). A later loss may defer again (fresh window per battle).</item>
    /// </list>
    /// </summary>
    public async Task<SecurityDeferredCheckResult> FinalizeDeferredSecurityCheckAsync(
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        HeadlessAttackState attack = context.AttackController.Current;
        if (attack.AttackerId is not HeadlessEntityId attackerId ||
            attack.AttackingPlayerId is not HeadlessPlayerId attackingPlayer ||
            attack.DefendingPlayerId is not HeadlessPlayerId defendingPlayer ||
            context.ZoneMover is not IZoneStateReader zones ||
            !TryReadDeferredRemaining(context, attackerId, out int remaining))
        {
            return SecurityDeferredCheckResult.Completed(attackerDeleted: false);
        }

        if (IsPendingDeletion(context, attackerId))
        {
            if (BattleResolver.NeedsWindow(context, zones, attackerId))
            {
                // The owner has not decided yet (or a two-step target pick is mid-flight).
                return SecurityDeferredCheckResult.StillDeferred();
            }

            // Declined: the deletion is final.
            ClearDeferredRemaining(context, attackerId);
            await FinalizeSecurityBattleDeletionAsync(context, attackerId, attackingPlayer, cancellationToken).ConfigureAwait(false);
            return SecurityDeferredCheckResult.Completed(attackerDeleted: true);
        }

        // Survived: the replacement cleared pendingDeletion — resume the remaining checks.
        ClearDeferredRemaining(context, attackerId);

        // (C-5 adversarial review P1-1) AS-IS StopSecurityCheck re-evaluates at EVERY loop head
        // (CardController.cs:3893-3908 via :3930): attacker gone / no top card / not a Digimon ⇒ the whole
        // security check stops. A replacement's OWN fallout can remove the attacker (e.g. Barrier's security
        // trash fires an OnLoseSecurity reactor that deletes it, or a custom would-be-deleted body bounces it) —
        // resuming without this guard kept checking security with an attacker no longer on the battle area.
        if (!IsAttackerStillCheckable(context, zones, attackerId, attackingPlayer))
        {
            return SecurityDeferredCheckResult.Completed(attackerDeleted: false);
        }

        if (remaining > 0 && zones.GetCards(defendingPlayer, ChoiceZone.Security).Count > 0)
        {
            SecurityCheckLoopResult loop = await RunSecurityCheckLoopAsync(
                context, zones, attackingPlayer, attackerId, defendingPlayer, remaining, cancellationToken).ConfigureAwait(false);
            if (loop.DeferredForDeletionReplacement)
            {
                // A later security Digimon deferred again — the loop re-stamped the marker.
                return SecurityDeferredCheckResult.StillDeferred();
            }

            return SecurityDeferredCheckResult.Completed(loop.AttackerDeleted);
        }

        return SecurityDeferredCheckResult.Completed(attackerDeleted: false);
    }

    /// <summary>(C-5 P1-1) AS-IS <c>StopSecurityCheck</c> (CardController.cs:3893-3908): the security check
    /// continues only while the ATTACKER still exists as a Digimon on its battle area (AttackingPermanent
    /// non-null, TopCard non-null, IsDigimon). Evaluated at every loop head and on the deferred-window resume.</summary>
    private static bool IsAttackerStillCheckable(
        EngineContext context, IZoneStateReader zones, HeadlessEntityId attackerId, HeadlessPlayerId attackingPlayer)
    {
        if (!zones.GetCards(attackingPlayer, ChoiceZone.BattleArea).Contains(attackerId))
        {
            return false;
        }

        return context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? instance)
            && instance is not null
            && context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition)
            && definition is not null
            && IsDigimon(definition);
    }

    private static bool IsPendingDeletion(EngineContext context, HeadlessEntityId cardId) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) &&
        record is not null &&
        ReadFlag(record.Metadata, GameFlowProcessor.PendingDeletionKey);

    private static bool TryReadDeferredRemaining(EngineContext context, HeadlessEntityId attackerId, out int remaining)
    {
        remaining = 0;
        return context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? record) &&
            record is not null &&
            TryReadInt(record.Metadata, SecurityCheckRemainingKey, out remaining);
    }

    private static void StampDeferredRemaining(EngineContext context, HeadlessEntityId attackerId, int remaining)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [SecurityCheckRemainingKey] = remaining
        };
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    internal static void ClearDeferredRemaining(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? record) || record is null ||
            !record.Metadata.ContainsKey(SecurityCheckRemainingKey))
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        metadata.Remove(SecurityCheckRemainingKey);
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    // (P8) same seam as the OnStartBattle/OnKnockOut windows: collect the subject-scoped triggers and
    // resolve them through the scheduler before the loop proceeds to the security-Digimon battle.
    private static async Task ResolveSecurityCheckWindowAsync(
        EngineContext context, HeadlessPlayerId defendingPlayerId, HeadlessEntityId checkedCardId, CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.TriggerTimingKey] = TriggerTimings.OnSecurityCheck,
            [AutoProcessingTriggerCollector.SourceEntityIdKey] = checkedCardId,
        };
        var gameEvent = new GameEvent(0, GameEventType.StateChanged, $"Timing window: {TriggerTimings.OnSecurityCheck}", metadata)
        {
            Actor = defendingPlayerId,
            Subject = checkedCardId,
            Cause = TriggerTimings.OnSecurityCheck,
        };
        // (F1-M1 P1-2) resolve through the UNIFIED security-check window so the revealed card's ACTIVATED
        // OnLoseSecurity is collected + merged with OnSecurityCheck (AS-IS IReduceSecurity(ref triggeredSkillInfos)
        // merges the per-card OnLoseSecurity into the same resolution), not dropped by a scheduler-only drain.
        await Effects.WindowResolverWiring.RunSecurityCheckWindowAsync(context, gameEvent, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string? ValidateSecurityCheck(
        EngineContext context,
        HeadlessAttackState attack,
        out CardInstanceRecord? attacker,
        out CardRecord? attackerCard,
        out IZoneStateReader? zoneReader)
    {
        attacker = null;
        attackerCard = null;
        zoneReader = null;

        if (!attack.IsPending)
        {
            return "No pending attack exists.";
        }

        if (!attack.IsDirectAttack || attack.TargetId.HasValue || attack.IsBlocked)
        {
            return "Security check requires a pending direct attack.";
        }

        if (!attack.AttackingPlayerId.HasValue || !attack.AttackerId.HasValue)
        {
            return "Pending attack has no attacker.";
        }

        if (!attack.DefendingPlayerId.HasValue)
        {
            return "Pending attack has no defending player.";
        }

        if (context.ZoneMover is not IZoneStateReader readableZones)
        {
            return "Zone mover does not expose readable zone state.";
        }

        if (!context.CardInstanceRepository.TryGetInstance(attack.AttackerId.Value, out attacker) || attacker is null)
        {
            return $"Attacker '{attack.AttackerId.Value}' was not found.";
        }

        if (attacker.OwnerId != attack.AttackingPlayerId.Value)
        {
            return $"Attacker '{attack.AttackerId.Value}' is not owned by attacking player '{attack.AttackingPlayerId.Value}'.";
        }

        if (!readableZones.GetCards(attack.AttackingPlayerId.Value, ChoiceZone.BattleArea).Contains(attack.AttackerId.Value))
        {
            return $"Attacker '{attack.AttackerId.Value}' is not in the battle area.";
        }

        if (!context.CardRepository.TryGetCard(attacker.DefinitionId, out attackerCard) || attackerCard is null)
        {
            return $"Attacker definition '{attacker.DefinitionId}' was not found.";
        }

        // (K4) type judgement via the central chokepoint (AS-IS Permanent.IsDigimon incl. TreatAsDigimon).
        if (!IsDigimon(attackerCard) && !ContinuousKeywordGate.IsDigimon(context, attack.AttackerId.Value))
        {
            return $"Attacker '{attack.AttackerId.Value}' is not a Digimon.";
        }

        zoneReader = readableZones;
        return null;
    }

    private static bool IsDigimon(CardRecord definition)
    {
        return definition.IsCardType("Digimon");
    }

    /// <summary>(d-remediation) AS-IS DontBattleSecurityDigimonClass: true when the revealed security card's own
    /// intrinsic "Ignore Battle" marker (dispatched at EffectTiming.None) says the attacker must not battle it.</summary>
    private static bool RevealedSecurityCardSkipsBattle(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId owner)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) || definition is null
            || !Assets.Scripts.Script.CardEffectCommons.CardEffectDispatch.TryCreateForCard(definition, out Assets.Scripts.Script.CardEffectCommons.CEntity_Effect? effect) || effect is null)
        {
            return false;
        }

        var revealed = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, owner, owner);
        foreach (Assets.Scripts.Script.CardEffectCommons.ICardEffect cardEffect in effect.CardEffects(Assets.Scripts.Script.CardEffectCommons.EffectTiming.None, revealed))
        {
            if (cardEffect is Assets.Scripts.Script.CardEffectCommons.DontBattleSecurityDigimonEffect marker && marker.SkipsBattle(revealed))
            {
                return true;
            }
        }

        return false;
    }

    private static int ReadStrike(EngineContext context, CardInstanceRecord attacker, CardRecord attackerCard)
    {
        int baseStrike = 1;
        if (TryReadInt(attacker.Metadata, StrikeKey, out int instanceStrike))
        {
            baseStrike = Math.Max(0, instanceStrike);
        }
        else if (TryReadInt(attackerCard.Metadata, StrikeKey, out int cardStrike))
        {
            baseStrike = Math.Max(0, cardStrike);
        }

        // C-18 Alliance: fold in continuous Security-Attack modifiers (e.g. Alliance's +1 UntilEndAttack)
        // so the number of security cards checked reflects the buff.
        return Math.Max(0, ContinuousModifierGate.ResolveSecurityAttack(context, attacker.InstanceId, baseStrike));
    }

    private static bool TryReadInt(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        out int value)
    {
        value = 0;
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                value = (int)longValue;
                return true;
            case double doubleValue when doubleValue >= int.MinValue && doubleValue <= int.MaxValue && doubleValue % 1 == 0:
                value = (int)doubleValue;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    /// <summary>(C-5/VR-6) Outcome of a single attacker-vs-security-Digimon battle.</summary>
    private enum SecurityBattleOutcome
    {
        /// <summary>The attacker won or is protected — the check continues.</summary>
        AttackerSurvived,

        /// <summary>The attacker's deletion resolved (no undeclined PRE replacement) — the check stops.</summary>
        AttackerDeleted,

        /// <summary>The attacker's deletion is DEFERRED for a would-be-deleted replacement window
        /// (Evade / by-battle Barrier / …) — the check loop parks until the window resolves.</summary>
        Deferred,
    }

    /// <summary>
    /// (W5) Resolves the battle between the attacker and a revealed security Digimon. The security Digimon
    /// itself is already trashed by the check, so it has no field presence to delete here; the only
    /// persistent outcome is the attacker's fate.
    /// (C-5/VR-6) A losing attacker rides the SAME pending/defer/finalize machine as a field-battle loser —
    /// AS-IS the security battle exits through the same IBattle.Battle → DestroyPermanentsClass.Destroy
    /// (CardController.cs:4165→4705→3696), whose PRE cut-in (WhenPermanentWouldBeDeleted) lets
    /// Evade/Barrier/Fragment/Scapegoat cancel the deletion (willBeRemoveField=false). Barrier is legal here
    /// because the battle hashtable is present for a DefendingCard battle too (IsByBattle, OnDeletion.cs:82).
    /// </summary>
    private static async Task<SecurityBattleOutcome> ResolveSecurityDigimonBattleAsync(
        EngineContext context,
        HeadlessEntityId attackerId,
        HeadlessPlayerId attackerOwner,
        int securityDp,
        IZoneStateReader zoneReader,
        CancellationToken cancellationToken)
    {
        if (!zoneReader.GetCards(attackerOwner, ChoiceZone.BattleArea).Contains(attackerId) ||
            !context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) ||
            attacker is null ||
            !context.CardRepository.TryGetCard(attacker.DefinitionId, out CardRecord? attackerCard) ||
            attackerCard is null)
        {
            return SecurityBattleOutcome.AttackerSurvived;
        }

        // Like a field battle (BattleResolver), an attacker with no defined DP cannot battle.
        if (!TryReadDp(attacker.Metadata, attackerCard.Metadata, out int attackerDp))
        {
            return SecurityBattleOutcome.AttackerSurvived;
        }

        // (R1-a) the attacker's effective DP is AS-IS Permanent.DP (base printed DP folded with every live
        // IChangeDPEffect / LinkedDP / Boost), mirroring the field-battle path.
        attackerDp = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, attackerId, attacker.OwnerId).DP;

        // The attacker is deleted when it does not exceed the security Digimon's DP (equal DP deletes
        // both; the security Digimon is already gone). Jamming / CanNotBeDeletedByBattle protects it —
        // via the static flag OR a continuous deletion-prevention replacement (R2-1/N-2).
        if (attackerDp > securityDp ||
            HasFlag(attacker.Metadata, attackerCard.Metadata, BattleResolver.PreventBattleDeletionKey) ||
            // (GR-005) a self-static <Jamming> lives as a registry keyword binding, not the
            // preventBattleDeletion metadata flag — derive it so a Jamming attacker survives the security battle.
            ContinuousKeywordGate.HasKeyword(context, attackerId, ContinuousKeywordGate.Jamming) ||
            BattleDeletionGate.PreventsBattleDeletion(context, attackerId))
        {
            return SecurityBattleOutcome.AttackerSurvived;
        }

        // (C-5/VR-6) flag the loser exactly like BattleResolver flags a field-battle loser (pendingDeletion +
        // deletedByBattle + dpBeforeBattle), then either DEFER for the owner's optional PRE replacement
        // decision — the common loop opens the DeletionReplacement window — or finalize immediately when no
        // undeclined replacement exists (the plain-loss fast path, byte-for-byte the previous sequence).
        BattleResolver.FlagPendingBattleDeletion(context, attacker, attackerDp);
        if (BattleResolver.NeedsWindow(context, zoneReader, attackerId))
        {
            return SecurityBattleOutcome.Deferred;
        }

        await FinalizeSecurityBattleDeletionAsync(context, attackerId, attackerOwner, cancellationToken).ConfigureAwait(false);
        return SecurityBattleOutcome.AttackerDeleted;
    }

    /// <summary>
    /// (C-5/VR-6) The PROCESSED-DELETION half of the security-battle loss (P0-4/RD-4) — leave-play cleanup
    /// (snapshot keywords + drop bindings), trash the digivolution sources BEFORE the top (AS-IS
    /// DiscardEvoRoots), the top card to the trash, then the mandatory post-deletion Fortitude replay.
    /// Same order as <see cref="BattleResolver"/>'s field-battle finalize.
    /// </summary>
    private static async Task FinalizeSecurityBattleDeletionAsync(
        EngineContext context,
        HeadlessEntityId attackerId,
        HeadlessPlayerId attackerOwner,
        CancellationToken cancellationToken)
    {
        // Settle the deferral markers first (pendingDeletion off, per-attack window markers cleared) so a
        // Fortitude-replayed card starts clean — mirrors the field battle's MarkDeletedByBattle.
        BattleResolver.MarkDeletedByBattle(context, attackerId);

        CardLeavePlayCleanup.OnDeleted(context.CardInstanceRepository, context.EffectRegistry, context, attackerId);
        await DeletionSourceTrash.TrashEvoSourcesAsync(
            context.CardInstanceRepository, context.ZoneMover, attackerId, gameEventQueue: null, cancellationToken,
            context.MemoryController, context.TurnController.Current.TurnPlayerId).ConfigureAwait(false);
        // (C-2 review P1) charge the leaving ACE attacker's OWN top overflow (raw move skips the sink gate).
        AceOverflowGate.ApplyTopOverflowOnDelete(context, attackerId);
        // (R2-P1-2) stamp the delete-BATCH id like the other three deletion finishers (BattleResolver /
        // GameFlowProcessor sweep / sink) — AS-IS the security-battle loss exits through the same
        // IBattle.Battle → DestroyPermanentsClass(LoserPermanents).Destroy() (CardController.cs:4179→4705),
        // ONE Destroy() call == ONE batch (the lone loser here is the attacker; the security card never has
        // field presence). Without the stamp this path's CardMoved carried no id, so its OnDeletion /
        // OnLeaveFieldAnyone reactions collapsed with (or split from) unrelated batches in the same drain —
        // and after R2-P1-4 the deletion marker is what derives those timings at all.
        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(attackerOwner, attackerId, ChoiceZone.BattleArea, ChoiceZone.Trash,
                Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [Effects.MatchStateMutationSink.DeletionBatchIdKey] = context.NextDeletionBatchId(),
                }),
            cancellationToken).ConfigureAwait(false);
        await DeletionReplacementGate.TryFortitudeReplayAsync(
            context.CardInstanceRepository, context.ZoneMover, attackerId, cancellationToken, context.EffectRegistry, context).ConfigureAwait(false);
    }

    /// <summary>(W6 tail) binding-values keys for security-card DP grants (AS-IS ChangeSecurityDigimonCardDPPlayerEffect).</summary>
    public const string SecurityCardDpDeltaKey = "securityCardDpDelta";
    public const string SecurityCardPredicateKey = "securityCardDpDelta.cardCondition";

    private static bool TryReadSecurityDigimonDp(
        EngineContext context,
        HeadlessEntityId cardId,
        out int securityDp)
    {
        securityDp = 0;
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? instance) ||
            instance is null ||
            !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) ||
            definition is null ||
            !IsDigimon(definition))
        {
            return false;
        }

        // A security Digimon with no defined DP cannot battle (mirrors BattleResolver's "no battle DP").
        if (!TryReadDp(instance.Metadata, definition.Metadata, out securityDp))
        {
            return false;
        }

        // (W6 tail) AS-IS ChangeSecurityDigimonCardDPPlayerEffect (ChangeCardDPClass): fold registered
        // security-card DP grants whose card predicate accepts this revealed card.
        foreach (Effects.EffectRequest effect in context.EffectRegistry.GetContinuousEffects(
            new Services.EffectQueryContext(ContinuousModifierGate.Scope)))
        {
            if (!effect.Context.Values.TryGetValue(SecurityCardDpDeltaKey, out object? raw) || raw is not int delta || delta == 0)
            {
                continue;
            }

            if (effect.Context.Values.TryGetValue(
                    Assets.Scripts.Script.CardEffectCommons.ContinuousSelfModifierEffect.ConditionKey, out object? rawCond)
                && rawCond is Func<bool> condition && !condition())
            {
                continue;
            }

            if (effect.Context.Values.TryGetValue(SecurityCardPredicateKey, out object? rawPred)
                && rawPred is Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> predicate
                && !predicate(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, instance.OwnerId, instance.OwnerId)))
            {
                continue;
            }

            securityDp += delta;
        }

        // (RD-7 Part A / design item RD7-71) AS-IS a security Digimon battles with its CardDP (CardSource.CardDP,
        // CardSource.cs:2383) — a SEPARATE fold that scans ONLY IChangeCardDPEffect (the single implementer
        // ChangeCardDPClass = the securityCardDpDelta grants folded above), NOT the permanent-DP pipeline. So
        // generic continuous DP effects (ContinuousDpGate: DP-boosts/reductions, reduction-immunity, LinkedDP)
        // do NOT touch a security Digimon's battle DP. The previous ContinuousDpGate.ResolveDp fold (D-A2)
        // wrongly leaked those permanent-DP effects into the security battle; removed to mirror CardDP.

        // (RD-P6B-18) UNION the NEW-model IChangeCardDPEffect scan (AS-IS CardSource.CardDP): a new-model
        // ChangeCardDPClass (ChangeSecurityDigimonCardDPStaticEffect) registers no binding, so the legacy
        // securityCardDpDelta fold above never sees it. AS-IS CardDP's CardCondition gates on
        // attackProcess.SecurityDigimon == this — the revealed card IS that security Digimon, so establish it
        // before the fold (nested ambient enter is safe; FoldCardDp clamps >= 0). Interface-disjoint from the
        // legacy fold, so no double-count.
        using (AmbientMatchContext.Scope _matchScope = AmbientMatchContext.Enter(context))
        {
            Assets.Scripts.Script.AttackProcess.For(context).SecurityDigimon = cardId;
            securityDp = Assets.Scripts.Script.CardEffectCommons.NewModelContinuousScan.FoldCardDp(context, cardId, securityDp);
        }

        return true;
    }

    /// <summary>(RD-P6B-18) Public accessor for a security Digimon's resolved battle DP (base DP + DP modifiers +
    /// the legacy securityCardDpDelta fold + the new-model IChangeCardDPEffect scan, AS-IS CardSource.CardDP).
    /// The production security-battle path uses <see cref="TryReadSecurityDigimonDp"/> directly; this exposes the
    /// same computation for callers/tests that read a security Digimon's battle DP outside the loop.</summary>
    public static bool TryGetSecurityDigimonBattleDp(EngineContext context, HeadlessEntityId cardId, out int securityDp)
    {
        ArgumentNullException.ThrowIfNull(context);
        return TryReadSecurityDigimonDp(context, cardId, out securityDp);
    }

    private static bool TryReadDp(
        IReadOnlyDictionary<string, object?> instanceMetadata,
        IReadOnlyDictionary<string, object?> cardMetadata,
        out int dp)
    {
        if (!TryReadInt(instanceMetadata, DpKey, out int baseDp) &&
            !TryReadInt(cardMetadata, DpKey, out baseDp))
        {
            dp = 0;
            return false;
        }

        IReadOnlyList<DpModifier> modifiers =
            instanceMetadata.TryGetValue(BattleResolver.DpModifiersKey, out object? raw) && raw is IEnumerable<DpModifier> typed
                ? typed.ToArray()
                : Array.Empty<DpModifier>();

        dp = DpCalculator.ComputeDp(baseDp, modifiers);
        return true;
    }

    private static bool HasFlag(
        IReadOnlyDictionary<string, object?> instanceMetadata,
        IReadOnlyDictionary<string, object?> cardMetadata,
        string key)
    {
        return ReadFlag(instanceMetadata, key) || ReadFlag(cardMetadata, key);
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
    }

    private static void MarkCheckedSecurityCard(
        EngineContext context,
        HeadlessEntityId cardId,
        HeadlessPlayerId defendingPlayerId,
        HeadlessEntityId attackerId,
        int order)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
        {
            [CheckedBySecurityCheckKey] = true,
            [SecurityCheckOrderKey] = order,
            [SecurityCheckedPlayerIdKey] = defendingPlayerId.Value,
            [SecurityCheckAttackerIdKey] = attackerId.Value
        };

        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }
}

/// <summary>(D-1) Outcome of the shared per-card security-check loop. (C-5/VR-6)
/// <paramref name="DeferredForDeletionReplacement"/>: a security-battle loss parked the loop for the
/// attacker's would-be-deleted replacement window; <paramref name="RemainingChecks"/> checks are still owed
/// (also stamped on the attacker as <see cref="SecurityResolver.SecurityCheckRemainingKey"/>).</summary>
public sealed record SecurityCheckLoopResult(
    IReadOnlyList<HeadlessEntityId> CheckedCards,
    IReadOnlyList<ZoneMoveResult> Movements,
    int SecurityDigimonBattles,
    bool AttackerDeleted,
    bool DeferredForDeletionReplacement = false,
    int RemainingChecks = 0);

/// <summary>(C-5/VR-6) Outcome of one <see cref="SecurityResolver.FinalizeDeferredSecurityCheckAsync"/>
/// round — still parked (window undecided / a later battle deferred again) or completed.</summary>
public sealed record SecurityDeferredCheckResult(bool IsDeferred, bool AttackerDeleted)
{
    public static SecurityDeferredCheckResult StillDeferred() => new(IsDeferred: true, AttackerDeleted: false);

    public static SecurityDeferredCheckResult Completed(bool attackerDeleted) => new(IsDeferred: false, attackerDeleted);
}

public sealed record SecurityResolutionResult(
    bool IsSuccess,
    string FailureReason,
    HeadlessAttackState Attack,
    HeadlessPlayerId? CheckedPlayerId,
    int? Strike,
    IReadOnlyList<HeadlessEntityId> CheckedCardIds,
    IReadOnlyList<ZoneMoveResult> MovementResults,
    bool AttackResolved,
    bool DefenderHasNoSecurity = false,
    int SecurityDigimonBattles = 0,
    bool AttackerDeletedBySecurity = false,
    bool RequiresDeletionReplacement = false)
{
    /// <summary>(C-5/VR-6) A security-battle loss was deferred for an optional would-be-deleted replacement
    /// window; the attack stays pending and the pipeline parks at <see cref="AttackPhase.DeletionReplacement"/>,
    /// later calling <see cref="SecurityResolver.FinalizeDeferredSecurityCheckAsync"/> to finish the check.</summary>
    public static SecurityResolutionResult Deferred(
        HeadlessAttackState attack,
        HeadlessPlayerId checkedPlayerId,
        int strike,
        IReadOnlyList<HeadlessEntityId> checkedCardIds,
        IReadOnlyList<ZoneMoveResult> movementResults,
        int securityDigimonBattles)
    {
        return new SecurityResolutionResult(
            true,
            string.Empty,
            attack,
            checkedPlayerId,
            strike,
            checkedCardIds.ToArray(),
            movementResults.ToArray(),
            AttackResolved: false,
            SecurityDigimonBattles: securityDigimonBattles,
            RequiresDeletionReplacement: true);
    }

    public static SecurityResolutionResult Failure(
        string failureReason,
        HeadlessAttackState attack,
        bool defenderHasNoSecurity = false)
    {
        return new SecurityResolutionResult(
            false,
            failureReason,
            attack,
            null,
            null,
            Array.Empty<HeadlessEntityId>(),
            Array.Empty<ZoneMoveResult>(),
            AttackResolved: false,
            DefenderHasNoSecurity: defenderHasNoSecurity);
    }

    public static SecurityResolutionResult Success(
        HeadlessAttackState attack,
        HeadlessPlayerId checkedPlayerId,
        int strike,
        IReadOnlyList<HeadlessEntityId> checkedCardIds,
        IReadOnlyList<ZoneMoveResult> movementResults,
        int securityDigimonBattles = 0,
        bool attackerDeletedBySecurity = false)
    {
        return new SecurityResolutionResult(
            true,
            string.Empty,
            attack,
            checkedPlayerId,
            strike,
            checkedCardIds.ToArray(),
            movementResults.ToArray(),
            attack.IsResolved,
            SecurityDigimonBattles: securityDigimonBattles,
            AttackerDeletedBySecurity: attackerDeletedBySecurity);
    }
}
