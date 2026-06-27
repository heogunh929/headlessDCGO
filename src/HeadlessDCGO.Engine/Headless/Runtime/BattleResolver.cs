namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

public sealed class BattleResolver
{
    public const string DpKey = "dp";
    public const string DpModifiersKey = "dpModifiers";
    public const string DeletedByBattleKey = "deletedByBattle";
    public const string DpBeforeBattleKey = "dpBeforeBattle";
    public const string PreventBattleDeletionKey = "preventBattleDeletion";
    public const string HasPiercingKey = "hasPiercing";

    public async Task<BattleResolutionResult> ResolveAsync(
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        HeadlessAttackState attack = context.AttackController.Current;
        BattleParticipant? attacker = null;
        BattleParticipant? defender = null;

        string? validationFailure = ValidateBattle(
            context,
            attack,
            out attacker,
            out defender);
        if (validationFailure is not null)
        {
            return BattleResolutionResult.Failure(validationFailure, attack);
        }

        // G3.5-RL-C2: who is deleted is the DP comparison adjusted by battle keywords.
        int comparison = Math.Clamp(attacker!.Dp.CompareTo(defender!.Dp), -1, 1);
        var deleted = new List<BattleParticipant>();
        switch (comparison)
        {
            case > 0:
                deleted.Add(defender);
                break;
            case < 0:
                deleted.Add(attacker);
                break;
            default:
                deleted.Add(attacker);
                deleted.Add(defender);
                break;
        }

        // NOTE: Jamming is intentionally NOT a mutual-deletion rule here. In the original, Jamming is a
        // conditional CanNotBeDestroyedByBattle that protects the ATTACKER only when it battles a
        // Security Digimon. That surface lives in SecurityResolver (W5): the security-Digimon battle
        // honours PreventBattleDeletion on the attacker, which is what Jamming applies. This field
        // battle (attacker vs a battle-area Digimon) is unaffected by Jamming.

        // PreventBattleDeletion (CanNotBeDeletedByBattle): flagged participants survive the battle.
        // R2-1/N-2: also honour continuous deletion-prevention REPLACEMENTS from other cards.
        deleted.RemoveAll(participant =>
            HasFlag(participant, PreventBattleDeletionKey) ||
            BattleDeletionGate.PreventsBattleDeletion(context, participant.InstanceId));

        bool defenderDeletedNow = deleted.Contains(defender);
        bool attackerSurvives = !deleted.Contains(attacker);

        var movementResults = new List<ZoneMoveResult>();
        foreach (BattleParticipant participant in deleted)
        {
            MarkDeletedByBattle(context, participant);
            movementResults.Add(await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(
                    participant.OwnerId,
                    participant.InstanceId,
                    ChoiceZone.BattleArea,
                    ChoiceZone.Trash),
                cancellationToken));
        }

        // Piercing: when the attacker survives and deletes the defender in battle, it also checks
        // the defending player's security (the AttackPipeline performs the follow-up check).
        bool piercing = attackerSurvives && defenderDeletedNow && HasFlag(attacker, HasPiercingKey);

        // CV-A1: expire continuous bindings that last only until the end of this battle.
        EffectDurationExpiry.ExpireBattleEnd(context.EffectRegistry);

        HeadlessAttackState resolvedAttack = context.AttackController.ResolveAttack("Battle resolved by DP comparison.");
        return BattleResolutionResult.Success(
            resolvedAttack,
            attacker.Dp,
            defender.Dp,
            deleted.Select(participant => participant.InstanceId).ToArray(),
            movementResults,
            attackerDeleted: deleted.Any(participant => participant.InstanceId == attacker.InstanceId),
            defenderDeleted: defenderDeletedNow,
            triggersPiercingSecurityCheck: piercing);
    }

    private static bool HasFlag(BattleParticipant participant, string key)
    {
        return ReadFlag(participant.Instance.Metadata, key) || ReadFlag(participant.Definition.Metadata, key);
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
    }

    private static string? ValidateBattle(
        EngineContext context,
        HeadlessAttackState attack,
        out BattleParticipant? attacker,
        out BattleParticipant? defender)
    {
        attacker = null;
        defender = null;

        if (!attack.IsPending)
        {
            return "No pending attack exists.";
        }

        if (!attack.AttackingPlayerId.HasValue || !attack.AttackerId.HasValue)
        {
            return "Pending attack has no attacker.";
        }

        if (!attack.DefendingPlayerId.HasValue || !attack.TargetId.HasValue || attack.IsDirectAttack)
        {
            return "Battle resolution requires a non-direct attack target.";
        }

        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return "Zone mover does not expose readable zone state.";
        }

        string? attackerFailure = TryReadParticipant(
            context,
            zoneReader,
            attack.AttackingPlayerId.Value,
            attack.AttackerId.Value,
            "Attacker",
            out attacker);
        if (attackerFailure is not null)
        {
            return attackerFailure;
        }

        string? defenderFailure = TryReadParticipant(
            context,
            zoneReader,
            attack.DefendingPlayerId.Value,
            attack.TargetId.Value,
            "Defender",
            out defender);
        if (defenderFailure is not null)
        {
            return defenderFailure;
        }

        return null;
    }

    private static string? TryReadParticipant(
        EngineContext context,
        IZoneStateReader zoneReader,
        HeadlessPlayerId expectedOwner,
        HeadlessEntityId instanceId,
        string role,
        out BattleParticipant? participant)
    {
        participant = null;

        if (!context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? instance) ||
            instance is null)
        {
            return $"{role} '{instanceId}' was not found.";
        }

        if (instance.OwnerId != expectedOwner)
        {
            return $"{role} '{instanceId}' is not owned by player '{expectedOwner}'.";
        }

        if (!zoneReader.GetCards(expectedOwner, ChoiceZone.BattleArea).Contains(instanceId))
        {
            return $"{role} '{instanceId}' is not in the battle area.";
        }

        if (!context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? definition) ||
            definition is null)
        {
            return $"{role} definition '{instance.DefinitionId}' was not found.";
        }

        if (!IsDigimon(definition))
        {
            return $"{role} '{instanceId}' is not a Digimon.";
        }

        if (!TryReadDp(instance.Metadata, definition.Metadata, out int baseDp))
        {
            return $"{role} '{instanceId}' has no battle DP.";
        }

        // G3.5-RL-B1: effective DP = base (printed) DP combined with typed DP modifiers using the
        // original accumulation order. With no modifiers this equals the base DP (no behavior change).
        int staticDp = DpCalculator.ComputeDp(baseDp, ReadDpModifiers(instance.Metadata));

        // N-2 / D-A1: layer continuous DP effects from other cards on top of the static DP (the original
        // GetDP rescans every field/security/player effect each access). No-op until such effects are
        // registered; the gate also honours DP-reduction immunity (D-A3).
        int dp = ContinuousDpGate.ResolveDp(context, instanceId, staticDp);

        participant = new BattleParticipant(instanceId, instance.OwnerId, instance, definition, dp);
        return null;
    }

    private static IReadOnlyList<DpModifier> ReadDpModifiers(IReadOnlyDictionary<string, object?> metadata)
    {
        return metadata.TryGetValue(DpModifiersKey, out object? raw) && raw is IEnumerable<DpModifier> modifiers
            ? modifiers.ToArray()
            : Array.Empty<DpModifier>();
    }

    private static bool IsDigimon(CardRecord definition)
    {
        return string.Equals(definition.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadDp(
        IReadOnlyDictionary<string, object?> instanceMetadata,
        IReadOnlyDictionary<string, object?> cardMetadata,
        out int dp)
    {
        return TryReadInt(instanceMetadata, DpKey, out dp) ||
            TryReadInt(cardMetadata, DpKey, out dp);
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

    private static void MarkDeletedByBattle(
        EngineContext context,
        BattleParticipant participant)
    {
        var metadata = new Dictionary<string, object?>(participant.Instance.Metadata, StringComparer.Ordinal)
        {
            [DeletedByBattleKey] = true,
            [DpBeforeBattleKey] = participant.Dp
        };

        context.CardInstanceRepository.Upsert(participant.Instance with { Metadata = metadata });
    }

    private sealed record BattleParticipant(
        HeadlessEntityId InstanceId,
        HeadlessPlayerId OwnerId,
        CardInstanceRecord Instance,
        CardRecord Definition,
        int Dp);
}

public sealed record BattleResolutionResult(
    bool IsSuccess,
    string FailureReason,
    HeadlessAttackState Attack,
    int? AttackerDp,
    int? DefenderDp,
    IReadOnlyList<HeadlessEntityId> DeletedCardIds,
    IReadOnlyList<ZoneMoveResult> MovementResults,
    bool AttackerDeleted,
    bool DefenderDeleted,
    bool TriggersPiercingSecurityCheck = false)
{
    public static BattleResolutionResult Failure(
        string failureReason,
        HeadlessAttackState attack)
    {
        return new BattleResolutionResult(
            false,
            failureReason,
            attack,
            null,
            null,
            Array.Empty<HeadlessEntityId>(),
            Array.Empty<ZoneMoveResult>(),
            AttackerDeleted: false,
            DefenderDeleted: false);
    }

    public static BattleResolutionResult Success(
        HeadlessAttackState attack,
        int attackerDp,
        int defenderDp,
        IReadOnlyList<HeadlessEntityId> deletedCardIds,
        IReadOnlyList<ZoneMoveResult> movementResults,
        bool attackerDeleted,
        bool defenderDeleted,
        bool triggersPiercingSecurityCheck = false)
    {
        return new BattleResolutionResult(
            true,
            string.Empty,
            attack,
            attackerDp,
            defenderDp,
            deletedCardIds.ToArray(),
            movementResults.ToArray(),
            AttackerDeleted: attackerDeleted,
            DefenderDeleted: defenderDeleted,
            TriggersPiercingSecurityCheck: triggersPiercingSecurityCheck);
    }
}
