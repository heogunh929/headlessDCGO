namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// (C-18 Alliance) When an Alliance Digimon attacks, its controller MAY suspend one OTHER owner
/// battle-area Digimon (the cost); if it becomes suspended, the attacker gains +DP equal to that ally's
/// DP and +1 Security Attack, both <see cref="EffectDuration.UntilEndAttack"/>. Mirrors AS-IS
/// <c>CardEffectCommons.AllianceProcess</c> — <c>CanActivateAlliance</c> requires another owner Digimon
/// that can pay the suspend cost (unsuspended); the process suspends the chosen ally then
/// <c>ChangeDigimonDP(+ally.DP)</c> + <c>ChangeDigimonSAttack(+1)</c> UntilEndAttack on the attacker.
/// Consumed in <see cref="AttackPipeline"/> before block timing (sibling of <see cref="RaidAttackSwitch"/>),
/// so the +DP applies to this battle's comparison. The optional "you may / select 1" is an agent choice.
/// </summary>
public static class AllianceAttackBoost
{
    public const string HasAllianceKey = "hasAlliance";
    public const string AllianceResolvedKey = "allianceResolved";
    public const string IsSuspendedKey = "isSuspended";
    public const string RequestIdPrefix = "alliance-boost";
    private const string DpKey = "dp";
    private const string DpModifiersKey = "dpModifiers";

    /// <summary>The owner's other unsuspended battle-area Digimon eligible to be suspended for the cost
    /// (AS-IS CanActivateSuspendCostEffect on another owner Digimon != the attacker).</summary>
    public static IReadOnlyList<HeadlessEntityId> GetAllyCandidates(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HeadlessAttackState attack = context.AttackController.Current;
        if (!attack.IsPending ||
            attack.AttackerId is not HeadlessEntityId attackerId ||
            attack.AttackingPlayerId is not HeadlessPlayerId attackingPlayerId ||
            context.ZoneMover is not IZoneStateReader zones)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var candidates = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId candidateId in zones.GetCards(attackingPlayerId, ChoiceZone.BattleArea))
        {
            if (candidateId == attackerId ||
                !context.CardInstanceRepository.TryGetInstance(candidateId, out CardInstanceRecord? instance) ||
                instance is null ||
                ReadFlag(instance.Metadata, IsSuspendedKey) ||
                !IsDigimon(context, instance))
            {
                continue;
            }

            candidates.Add(candidateId);
        }

        return candidates;
    }

    /// <summary>Opens the OPTIONAL Alliance choice for the attacker's controller — suspend an ally to gain
    /// its DP + 1 Security Attack, or decline. Returns true when a choice opened.</summary>
    public static bool RequestChoice(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HeadlessAttackState attack = context.AttackController.Current;
        if (context.ChoiceController.Current.IsPending ||
            !attack.IsPending ||
            attack.AttackerId is not HeadlessEntityId attackerId ||
            attack.AttackingPlayerId is not HeadlessPlayerId attackingPlayerId ||
            !HasAlliance(context, attackerId) ||
            IsResolved(context, attackerId))
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> candidates = GetAllyCandidates(context);
        if (candidates.Count == 0)
        {
            return false;
        }

        ChoiceCandidate[] choiceCandidates = candidates
            .Select(id => new ChoiceCandidate(id, $"Suspend {id.Value} for Alliance", ChoiceZone.BattleArea, IsSelectable: true, ownerId: attackingPlayerId))
            .ToArray();

        var request = new ChoiceRequest(
            ChoiceType.AllianceTarget,
            attackingPlayerId,
            "Alliance: suspend an ally to gain its DP and +1 Security Attack, or decline.",
            minCount: 0,
            maxCount: 1,
            canSkip: true,
            ChoiceZone.BattleArea,
            choiceCandidates);
        context.ChoiceController.RequestChoice(request, new HeadlessEntityId($"{RequestIdPrefix}:{attackerId.Value}"));
        return true;
    }

    /// <summary>Resolves the Alliance choice: suspend the chosen ally and grant the attacker +ally-DP and
    /// +1 Security Attack (UntilEndAttack), or skip. Either way the attacker is marked resolved so the
    /// window does not re-open this attack.</summary>
    public static bool ResolveChoice(EngineContext context, ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        if (!context.ChoiceController.Current.IsPending ||
            context.ChoiceController.PendingRequest is not ChoiceRequest request ||
            request.Type != ChoiceType.AllianceTarget)
        {
            return false;
        }

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        HeadlessAttackState attack = context.AttackController.Current;
        if (attack.AttackerId is not HeadlessEntityId attackerId ||
            attack.AttackingPlayerId is not HeadlessPlayerId attackingPlayerId)
        {
            return true;
        }

        MarkResolved(context, attackerId);

        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return true;   // declined: nothing granted
        }

        HeadlessEntityId allyId = result.SelectedIds[0];
        if (!context.CardInstanceRepository.TryGetInstance(allyId, out CardInstanceRecord? ally) ||
            ally is null ||
            ReadFlag(ally.Metadata, IsSuspendedKey) ||
            !TryReadDigimonDp(context, allyId, ally, out int plusDp))
        {
            return true;   // ally became ineligible after selection
        }

        // Pay the cost: suspend the chosen ally.
        context.CardInstanceRepository.Upsert(ally with
        {
            Metadata = new Dictionary<string, object?>(ally.Metadata, StringComparer.Ordinal) { [IsSuspendedKey] = true }
        });

        // Grant the attacker +ally-DP and +1 Security Attack, both UntilEndAttack (auto-expire at attack end).
        RegisterModifier(context, attackerId, attackingPlayerId, "dp", ModifierHelpers.DpDeltaKey, plusDp);
        RegisterModifier(context, attackerId, attackingPlayerId, "sa", ModifierHelpers.SAttackDeltaKey, 1);
        return true;
    }

    private static void RegisterModifier(
        EngineContext context,
        HeadlessEntityId attackerId,
        HeadlessPlayerId attackingPlayerId,
        string suffix,
        string deltaKey,
        int delta)
    {
        var effectId = new HeadlessEntityId($"{attackerId.Value}:alliance:{suffix}");
        var effectContext = new EffectContext(
            attackingPlayerId,
            attackingPlayerId,
            attackerId,
            triggerEntityId: null,
            targetEntityIds: new[] { attackerId },
            values: new Dictionary<string, object?>(StringComparer.Ordinal) { [deltaKey] = delta });

        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(effectId, attackingPlayerId, "Continuous", effectContext),
            keywords: new[] { "Alliance" },
            EffectQueryRole.Continuous,
            new[] { ContinuousRestrictionGate.Scope },
            effect: null,
            duration: EffectDuration.UntilEndAttack));
    }

    /// <summary>Clears the per-attack resolved marker (called at attack cleanup) so a later attack re-offers.</summary>
    public static void ClearResolved(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? r) || r is null ||
            !ReadFlag(r.Metadata, AllianceResolvedKey))
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(r.Metadata, StringComparer.Ordinal);
        metadata.Remove(AllianceResolvedKey);
        context.CardInstanceRepository.Upsert(r with { Metadata = metadata });
    }

    private static bool IsResolved(EngineContext context, HeadlessEntityId attackerId) =>
        context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? r) && r is not null &&
        ReadFlag(r.Metadata, AllianceResolvedKey);

    private static void MarkResolved(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? r) || r is null)
        {
            return;
        }

        context.CardInstanceRepository.Upsert(r with
        {
            Metadata = new Dictionary<string, object?>(r.Metadata, StringComparer.Ordinal) { [AllianceResolvedKey] = true }
        });
    }

    private static bool HasAlliance(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) || attacker is null)
        {
            return false;
        }

        if (ReadFlag(attacker.Metadata, HasAllianceKey))
        {
            return true;
        }

        return context.CardRepository.TryGetCard(attacker.DefinitionId, out CardRecord? card) &&
            card is not null &&
            ReadFlag(card.Metadata, HasAllianceKey);
    }

    private static bool IsDigimon(EngineContext context, CardInstanceRecord instance) =>
        context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) &&
        card is not null &&
        string.Equals(card.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadDigimonDp(EngineContext context, HeadlessEntityId id, CardInstanceRecord instance, out int dp)
    {
        dp = 0;
        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) ||
            card is null ||
            !string.Equals(card.CardType, "Digimon", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryReadInt(instance.Metadata, DpKey, out int baseDp) && !TryReadInt(card.Metadata, DpKey, out baseDp))
        {
            return false;
        }

        int staticDp = DpCalculator.ComputeDp(baseDp, ReadDpModifiers(instance.Metadata));
        dp = ContinuousDpGate.ResolveDp(context, id, staticDp);
        return true;
    }

    private static IReadOnlyList<DpModifier> ReadDpModifiers(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(DpModifiersKey, out object? raw) && raw is IEnumerable<DpModifier> mods
            ? mods.ToArray()
            : Array.Empty<DpModifier>();

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool value && value;

    private static bool TryReadInt(IReadOnlyDictionary<string, object?> metadata, string key, out int value)
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
            case string text when int.TryParse(text, out int parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }
}
