namespace HeadlessDCGO.Engine.Headless.Runtime;

using System.Globalization;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class SecurityResolver
{
    public const string StrikeKey = "strike";
    public const string CheckedBySecurityCheckKey = "checkedBySecurityCheck";
    public const string SecurityCheckOrderKey = "securityCheckOrder";
    public const string SecurityCheckedPlayerIdKey = "securityCheckedPlayerId";
    public const string SecurityCheckAttackerIdKey = "securityCheckAttackerId";

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

        int strike = ReadStrike(attacker!, attackerCard!);
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

        int checkCount = Math.Min(strike, security.Count);
        var checkedCards = new List<HeadlessEntityId>();
        var movementResults = new List<ZoneMoveResult>();

        for (int index = 0; index < checkCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HeadlessEntityId checkedCardId = zoneReader.GetCards(attack.DefendingPlayerId.Value, ChoiceZone.Security)[0];
            MarkCheckedSecurityCard(context, checkedCardId, attack, index + 1);
            ZoneMoveResult move = await context.ZoneMover.MoveAsync(
                new ZoneMoveRequest(
                    attack.DefendingPlayerId.Value,
                    checkedCardId,
                    ChoiceZone.Security,
                    ChoiceZone.Trash,
                    FaceUp: true),
                cancellationToken);
            checkedCards.Add(checkedCardId);
            movementResults.Add(move);
        }

        HeadlessAttackState resolvedAttack = context.AttackController.ResolveAttack("Security check resolved.");
        return SecurityResolutionResult.Success(
            resolvedAttack,
            attack.DefendingPlayerId.Value,
            strike,
            checkedCards,
            movementResults);
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

        if (!IsDigimon(attackerCard))
        {
            return $"Attacker '{attack.AttackerId.Value}' is not a Digimon.";
        }

        zoneReader = readableZones;
        return null;
    }

    private static bool IsDigimon(CardRecord definition)
    {
        return string.Equals(definition.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadStrike(CardInstanceRecord attacker, CardRecord attackerCard)
    {
        if (TryReadInt(attacker.Metadata, StrikeKey, out int instanceStrike))
        {
            return Math.Max(0, instanceStrike);
        }

        if (TryReadInt(attackerCard.Metadata, StrikeKey, out int cardStrike))
        {
            return Math.Max(0, cardStrike);
        }

        return 1;
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

    private static void MarkCheckedSecurityCard(
        EngineContext context,
        HeadlessEntityId cardId,
        HeadlessAttackState attack,
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
            [SecurityCheckedPlayerIdKey] = attack.DefendingPlayerId!.Value.Value,
            [SecurityCheckAttackerIdKey] = attack.AttackerId!.Value.Value
        };

        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }
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
    bool DefenderHasNoSecurity = false)
{
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
        IReadOnlyList<ZoneMoveResult> movementResults)
    {
        return new SecurityResolutionResult(
            true,
            string.Empty,
            attack,
            checkedPlayerId,
            strike,
            checkedCardIds.ToArray(),
            movementResults.ToArray(),
            attack.IsResolved);
    }
}
