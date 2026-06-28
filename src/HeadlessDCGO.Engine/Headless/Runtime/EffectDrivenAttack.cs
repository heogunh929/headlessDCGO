namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// (S1) Effect-driven attack — the engine hub for an EFFECT initiating an attack with a specific Digimon
/// (AS-IS <c>SelectAttackEffect</c>). No new pipeline: declaring the attack on <see cref="IHeadlessAttackController"/>
/// is enough — <c>GameFlowProcessor.RunToStableAsync</c> step 3 then drives block timing → combat/security →
/// end-attack, pausing/resuming on block choices like any other attack. This helper only (a) enumerates the
/// effect-legal targets (bypassing the normal main-phase / summoning-sickness gates the effect overrides),
/// (b) optionally surfaces the target as an AGENT choice (rules-faithful — an attack target is a player
/// decision), and (c) declares the attack, applying per-effect options. Unlocks C-20 Vortex / C-16 Overclock
/// (attack part); reused by later effect-attack cards.
/// </summary>
public static class EffectDrivenAttack
{
    public const string RequestIdPrefix = "effect-attack";
    public const string WithoutTapPendingKey = "effectAttackWithoutTap";
    public const string IsSuspendedKey = "isSuspended";
    private const string PlayerTargetSuffix = ":effect-attack-player";

    /// <summary>The targets this effect-driven attack may declare on, honouring the effect's options
    /// (player / Digimon / unsuspended Digimon). Defending player = the current non-turn player.</summary>
    public static IReadOnlyList<AttackTargetCandidate> GetTargets(
        EngineContext context, HeadlessEntityId attackerId, EffectAttackOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (attackerId.IsEmpty ||
            context.ZoneMover is not IZoneStateReader zones ||
            context.TurnController.Current.NonTurnPlayerId is not HeadlessPlayerId defendingPlayerId ||
            !context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) ||
            attacker is null ||
            !zones.GetCards(attacker.OwnerId, ChoiceZone.BattleArea).Contains(attackerId) ||
            !TryGetDefinition(context, attacker.DefinitionId, out HeadlessEntityId attackerDefinitionId, isDigimon: true))
        {
            return Array.Empty<AttackTargetCandidate>();
        }

        HeadlessPlayerId attackingPlayerId = attacker.OwnerId;
        var candidates = new List<AttackTargetCandidate>();

        if (options.AllowDigimonTarget)
        {
            foreach (HeadlessEntityId targetId in zones.GetCards(defendingPlayerId, ChoiceZone.BattleArea))
            {
                if (!context.CardInstanceRepository.TryGetInstance(targetId, out CardInstanceRecord? target) ||
                    target is null ||
                    !TryGetDefinition(context, target.DefinitionId, out HeadlessEntityId targetDefinitionId, isDigimon: true))
                {
                    continue;
                }

                // Normal attacks only hit SUSPENDED Digimon; TargetUnsuspended lifts that (AS-IS isVortex).
                if (!options.TargetUnsuspended && !ReadFlag(target.Metadata, IsSuspendedKey))
                {
                    continue;
                }

                candidates.Add(AttackTargetCandidate.Digimon(
                    attackingPlayerId, attackerId, attackerDefinitionId, defendingPlayerId, targetId, targetDefinitionId));
            }
        }

        if (options.AllowPlayerTarget)
        {
            candidates.Add(AttackTargetCandidate.DirectPlayer(
                attackingPlayerId, attackerId, attackerDefinitionId, defendingPlayerId));
        }

        return candidates;
    }

    /// <summary>Declares the effect-driven attack on the chosen target, applying options (suspend the
    /// attacker unless <see cref="EffectAttackOptions.WithoutTap"/>). Returns false if an attack is already
    /// pending (no nested attacks) or the target's attacker mismatches. The existing pipeline drives the rest.</summary>
    public static bool Initiate(
        EngineContext context, HeadlessEntityId attackerId, AttackTargetCandidate target, EffectAttackOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        if (context.AttackController.Current.IsPending ||
            target.AttackerId != attackerId)
        {
            return false;
        }

        if (!options.WithoutTap &&
            context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) &&
            attacker is not null &&
            !ReadFlag(attacker.Metadata, IsSuspendedKey))
        {
            context.CardInstanceRepository.Upsert(attacker with
            {
                Metadata = new Dictionary<string, object?>(attacker.Metadata, StringComparer.Ordinal) { [IsSuspendedKey] = true }
            });
        }

        context.AttackController.DeclareAttack(
            target.PlayerId, attackerId, target.DefendingPlayerId, target.TargetId, target.IsDirectAttack);
        return true;
    }

    /// <summary>Opens the OPTIONAL effect-driven attack target choice (rules-faithful: the controller picks a
    /// target or declines). Returns true when a choice opened. <see cref="EffectAttackOptions.WithoutTap"/> is
    /// stashed on the attacker so <see cref="ResolveChoice"/> can apply it.</summary>
    public static bool RequestChoice(EngineContext context, HeadlessEntityId attackerId, EffectAttackOptions options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        if (context.ChoiceController.Current.IsPending ||
            context.AttackController.Current.IsPending)
        {
            return false;
        }

        IReadOnlyList<AttackTargetCandidate> targets = GetTargets(context, attackerId, options);
        if (targets.Count == 0)
        {
            return false;
        }

        ChoiceCandidate[] candidates = targets
            .Select(t => new ChoiceCandidate(
                CandidateId(attackerId, t),
                t.IsDirectAttack ? "Attack the player" : $"Attack {t.TargetId!.Value}",
                ChoiceZone.BattleArea,
                IsSelectable: true,
                ownerId: t.PlayerId))
            .ToArray();

        if (options.WithoutTap &&
            context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) &&
            attacker is not null)
        {
            context.CardInstanceRepository.Upsert(attacker with
            {
                Metadata = new Dictionary<string, object?>(attacker.Metadata, StringComparer.Ordinal) { [WithoutTapPendingKey] = true }
            });
        }

        HeadlessPlayerId attackingPlayerId = targets[0].PlayerId;
        var request = new ChoiceRequest(
            ChoiceType.EffectAttack,
            attackingPlayerId,
            "Effect attack: choose a target, or decline.",
            minCount: 0,
            maxCount: 1,
            canSkip: true,
            ChoiceZone.BattleArea,
            candidates);
        context.ChoiceController.RequestChoice(request, new HeadlessEntityId($"{RequestIdPrefix}:{attackerId.Value}"));
        return true;
    }

    /// <summary>Resolves the effect-driven attack choice: declare the attack on the chosen target, or skip.</summary>
    public static bool ResolveChoice(EngineContext context, ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        if (!context.ChoiceController.Current.IsPending ||
            context.ChoiceController.PendingRequest is not ChoiceRequest request ||
            request.Type != ChoiceType.EffectAttack)
        {
            return false;
        }

        HeadlessEntityId attackerId = AttackerFromRequestId(context.ChoiceController.Current.RequestId ?? default);

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        bool withoutTap = ConsumeWithoutTap(context, attackerId);

        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return true;   // declined
        }

        var options = new EffectAttackOptions(WithoutTap: withoutTap);
        AttackTargetCandidate? target = ResolveTarget(context, attackerId, result.SelectedIds[0], options);
        if (target is null)
        {
            return true;   // target no longer legal
        }

        Initiate(context, attackerId, target, options);
        return true;
    }

    private static HeadlessEntityId CandidateId(HeadlessEntityId attackerId, AttackTargetCandidate target) =>
        target.IsDirectAttack
            ? new HeadlessEntityId($"{attackerId.Value}{PlayerTargetSuffix}")
            : target.TargetId!.Value;

    private static AttackTargetCandidate? ResolveTarget(
        EngineContext context, HeadlessEntityId attackerId, HeadlessEntityId selectedId, EffectAttackOptions options)
    {
        bool isPlayer = selectedId.Value.EndsWith(PlayerTargetSuffix, StringComparison.Ordinal);
        foreach (AttackTargetCandidate candidate in GetTargets(context, attackerId, options with { WithoutTap = false }))
        {
            if (isPlayer && candidate.IsDirectAttack)
            {
                return candidate;
            }

            if (!isPlayer && !candidate.IsDirectAttack && candidate.TargetId == selectedId)
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool ConsumeWithoutTap(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) ||
            attacker is null ||
            !ReadFlag(attacker.Metadata, WithoutTapPendingKey))
        {
            return false;
        }

        var metadata = new Dictionary<string, object?>(attacker.Metadata, StringComparer.Ordinal);
        metadata.Remove(WithoutTapPendingKey);
        context.CardInstanceRepository.Upsert(attacker with { Metadata = metadata });
        return true;
    }

    private static HeadlessEntityId AttackerFromRequestId(HeadlessEntityId requestId)
    {
        string value = requestId.Value;
        string prefix = $"{RequestIdPrefix}:";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            ? new HeadlessEntityId(value[prefix.Length..])
            : default;
    }

    private static bool TryGetDefinition(EngineContext context, HeadlessEntityId definitionId, out HeadlessEntityId resolvedId, bool isDigimon)
    {
        resolvedId = definitionId;
        if (!context.CardRepository.TryGetCard(definitionId, out CardRecord? card) || card is null)
        {
            return false;
        }

        return !isDigimon || string.Equals(card.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
}

/// <summary>(S1) Per-effect options for an effect-driven attack (mirrors AS-IS SelectAttackEffect flags).</summary>
public sealed record EffectAttackOptions(
    bool WithoutTap = false,         // attacker is NOT suspended (Overclock untapped attack)
    bool AllowPlayerTarget = true,   // may attack the player directly (security)
    bool AllowDigimonTarget = true,  // may attack opponent Digimon (Overclock = false: player only)
    bool TargetUnsuspended = true);  // unsuspended Digimon are also targetable (AS-IS isVortex)
