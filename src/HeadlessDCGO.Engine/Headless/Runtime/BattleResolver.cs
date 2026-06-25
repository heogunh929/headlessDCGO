namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BattleResolver
{
    public const string DpKey = "dp";
    public const string DeletedByBattleKey = "deletedByBattle";
    public const string DpBeforeBattleKey = "dpBeforeBattle";

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

        int comparison = Math.Clamp(attacker!.Dp.CompareTo(defender!.Dp), -1, 1);
        BattleParticipant[] deleted = comparison switch
        {
            > 0 => new[] { defender },
            < 0 => new[] { attacker },
            _ => new[] { attacker, defender }
        };

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

        HeadlessAttackState resolvedAttack = context.AttackController.ResolveAttack("Battle resolved by DP comparison.");
        return BattleResolutionResult.Success(
            resolvedAttack,
            attacker.Dp,
            defender.Dp,
            deleted.Select(participant => participant.InstanceId).ToArray(),
            movementResults,
            attackerDeleted: deleted.Any(participant => participant.InstanceId == attacker.InstanceId),
            defenderDeleted: deleted.Any(participant => participant.InstanceId == defender.InstanceId));
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

        if (!TryReadDp(instance.Metadata, definition.Metadata, out int dp))
        {
            return $"{role} '{instanceId}' has no battle DP.";
        }

        participant = new BattleParticipant(instanceId, instance.OwnerId, instance, definition, dp);
        return null;
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
    bool DefenderDeleted)
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
        bool defenderDeleted)
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
            DefenderDeleted: defenderDeleted);
    }
}
