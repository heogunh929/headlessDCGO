namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

/// <summary>
/// (C-3 Raid) When a Raid Digimon attacks, its controller may switch the attack onto one of the
/// opponent's UNSUSPENDED Digimon with the highest DP (other than the current defender). Mirrors AS-IS
/// <c>CardEffectCommons.RaidProcess</c> — <c>CanActivateRaid</c> requires this Digimon to be the
/// attacker and an eligible <c>IsMaxDP</c> enemy permanent (<c>!IsSuspended</c>, not the current
/// <c>DefendingPermanent</c>) to exist; the process then calls <c>attackProcess.SwitchDefender</c>.
/// Consumed in <see cref="AttackPipeline"/> before block timing, so the block window and battle use the
/// switched defender.
///
/// LIMITATION: applied automatically, picking the first highest-DP candidate, rather than surfacing the
/// AS-IS optional "you may / select 1" choice — consistent with the other auto-resolved keyword effects
/// (<see cref="DeletionReplacementGate"/>).
/// </summary>
public static class RaidAttackSwitch
{
    public const string HasRaidKey = "hasRaid";
    public const string IsSuspendedKey = "isSuspended";
    private const string DpKey = "dp";
    private const string DpModifiersKey = "dpModifiers";

    public const string RaidResolvedKey = "raidResolved";
    public const string RequestIdPrefix = "raid-switch";

    /// <summary>The highest-DP eligible enemy Digimon (unsuspended, not the current defender) the attack
    /// may switch onto. AS-IS restricts the choice to the IsMaxDP set; a tie offers all of them.</summary>
    public static IReadOnlyList<HeadlessEntityId> GetSwitchCandidates(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HeadlessAttackState attack = context.AttackController.Current;
        if (!attack.IsPending ||
            attack.DefendingPlayerId is not HeadlessPlayerId defendingPlayerId ||
            context.ZoneMover is not IZoneStateReader zones)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var byDp = new List<(HeadlessEntityId Id, int Dp)>();
        foreach (HeadlessEntityId candidateId in zones.GetCards(defendingPlayerId, ChoiceZone.BattleArea))
        {
            if (candidateId == attack.TargetId ||
                !context.CardInstanceRepository.TryGetInstance(candidateId, out CardInstanceRecord? instance) ||
                instance is null ||
                ReadFlag(instance.Metadata, IsSuspendedKey) ||
                !TryReadDigimonDp(context, candidateId, instance, out int dp))
            {
                continue;
            }

            byDp.Add((candidateId, dp));
        }

        if (byDp.Count == 0)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        int max = byDp.Max(c => c.Dp);
        return byDp.Where(c => c.Dp == max).Select(c => c.Id).ToArray();
    }

    /// <summary>(F-6.8) Opens the OPTIONAL Raid attack-switch choice for the attacker's controller — switch
    /// to one of the highest-DP eligible Digimon, or decline. Returns true when a choice opened.</summary>
    public static bool RequestChoice(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HeadlessAttackState attack = context.AttackController.Current;
        if (context.ChoiceController.Current.IsPending ||
            !attack.IsPending ||
            attack.AttackerId is not HeadlessEntityId attackerId ||
            attack.AttackingPlayerId is not HeadlessPlayerId attackingPlayerId ||
            !HasRaid(context, attackerId) ||
            IsResolved(context, attackerId))
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> candidates = GetSwitchCandidates(context);
        if (candidates.Count == 0)
        {
            return false;
        }

        ChoiceCandidate[] choiceCandidates = candidates
            .Select(id => new ChoiceCandidate(id, $"Switch the attack to {id.Value}", ChoiceZone.BattleArea, IsSelectable: true, ownerId: attackingPlayerId))
            .ToArray();

        var request = new ChoiceRequest(
            ChoiceType.AttackTarget,
            attackingPlayerId,
            "Raid: switch the attack to a highest-DP Digimon, or decline.",
            minCount: 0,
            maxCount: 1,
            canSkip: true,
            ChoiceZone.BattleArea,
            choiceCandidates);
        context.ChoiceController.RequestChoice(request, new HeadlessEntityId($"{RequestIdPrefix}:{attackerId.Value}"));
        return true;
    }

    /// <summary>Resolves the Raid switch choice: switch to the selected Digimon, or skip. Either way the
    /// attacker is marked resolved so the window does not re-open this attack.</summary>
    public static bool ResolveChoice(EngineContext context, ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);
        if (!context.ChoiceController.Current.IsPending ||
            context.ChoiceController.PendingRequest is not ChoiceRequest request ||
            request.Type != ChoiceType.AttackTarget)
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

        if (context.AttackController.Current.AttackerId is HeadlessEntityId attackerId)
        {
            MarkResolved(context, attackerId);
        }

        if (!result.IsSkipped && result.SelectedIds.Count > 0)
        {
            context.AttackController.SwitchDefender(result.SelectedIds[0], "Raid switched the attack.");
        }

        return true;
    }

    private static bool IsResolved(EngineContext context, HeadlessEntityId attackerId) =>
        context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? r) && r is not null &&
        ReadFlag(r.Metadata, RaidResolvedKey);

    private static void MarkResolved(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? r) || r is null)
        {
            return;
        }

        context.CardInstanceRepository.Upsert(r with
        {
            Metadata = new Dictionary<string, object?>(r.Metadata, StringComparer.Ordinal) { [RaidResolvedKey] = true }
        });
    }

    /// <summary>Clears the per-attack resolved marker (called at attack cleanup) so a later attack re-offers.</summary>
    public static void ClearResolved(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? r) || r is null ||
            !ReadFlag(r.Metadata, RaidResolvedKey))
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(r.Metadata, StringComparer.Ordinal);
        metadata.Remove(RaidResolvedKey);
        context.CardInstanceRepository.Upsert(r with { Metadata = metadata });
    }

    private static bool HasRaid(EngineContext context, HeadlessEntityId attackerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) || attacker is null)
        {
            return false;
        }

        if (ReadFlag(attacker.Metadata, HasRaidKey))
        {
            return true;
        }

        // (S1) The Raid keyword is granted as a live continuous keyword (RaidSelfEffect → SelfKeywordByNameEffect);
        // the hasRaid metadata flag is only set by the GrantRaid mutation, which the keyword grant never emits. Read
        // the live keyword directly (AS-IS evaluates the keyword at attack time), else Raid is inert.
        if (ContinuousKeywordGate.HasKeyword(context, attackerId, ContinuousKeywordGate.Raid))
        {
            return true;
        }

        return context.CardRepository.TryGetCard(attacker.DefinitionId, out CardRecord? card) &&
            card is not null &&
            ReadFlag(card.Metadata, HasRaidKey);
    }

    private static bool TryReadDigimonDp(EngineContext context, HeadlessEntityId id, CardInstanceRecord instance, out int dp)
    {
        dp = 0;
        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? card) ||
            card is null ||
            // (K4) type judgement via the central chokepoint (AS-IS Permanent.IsDigimon incl. TreatAsDigimon).
            (!IsDigimon(card) && !ContinuousKeywordGate.IsDigimon(context, id)))
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

    private static bool IsDigimon(CardRecord card) =>
        string.Equals(card.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);

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
