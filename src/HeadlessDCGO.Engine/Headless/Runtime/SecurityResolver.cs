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

            // W1/W4: open the OnSecurityCheck timing window for each revealed security card (scoped).
            TriggerEventEmitter.Emit(
                context.GameEventQueue,
                TriggerTimings.OnSecurityCheck,
                actor: defendingPlayerId,
                subject: checkedCardId);

            // G7-004: resolve the revealed card's [Security] activated effect (e.g. a Tamer/Option
            // security skill). No-op for cards with no ported SecuritySkill effect.
            await ActivatedEffectResolver
                .ResolveAsync(context, checkedCardId, defendingPlayerId, EffectTiming.SecuritySkill, cancellationToken)
                .ConfigureAwait(false);

            // W5: a revealed security Digimon battles the attacker. The security card is trashed by the
            // check regardless (already moved above); the only persistent outcome is the attacker's
            // fate. Mirrors AS-IS ISecurityCheck → IBattle(AttackingPermanent, DefendingCard).
            if (isSecurityDigimon)
            {
                securityDigimonBattles++;
                if (await ResolveSecurityDigimonBattleAsync(context, attackerId, attackingPlayerId, securityDp, zoneReader, cancellationToken)
                    .ConfigureAwait(false))
                {
                    attackerDeletedBySecurity = true;
                    // AS-IS StopSecurityCheck: with the attacker gone, no further security is checked.
                    break;
                }
            }
        }

        return new SecurityCheckLoopResult(checkedCards, movementResults, securityDigimonBattles, attackerDeletedBySecurity);
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

    /// <summary>
    /// (W5) Resolves the battle between the attacker and a revealed security Digimon. Returns true when
    /// the attacker is deleted (DP ≤ the security Digimon's DP) and not protected — in which case the
    /// attacker is moved to the trash and the security check stops. The security Digimon itself is
    /// already trashed by the check, so it has no field presence to delete here.
    /// </summary>
    private static async Task<bool> ResolveSecurityDigimonBattleAsync(
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
            return false;
        }

        // Like a field battle (BattleResolver), an attacker with no defined DP cannot battle.
        if (!TryReadDp(attacker.Metadata, attackerCard.Metadata, out int attackerDp))
        {
            return false;
        }

        // N-2 / D-A2: layer continuous DP effects on the attacker's DP (mirrors the field-battle path).
        attackerDp = ContinuousDpGate.ResolveDp(context, attackerId, attackerDp);

        // The attacker is deleted when it does not exceed the security Digimon's DP (equal DP deletes
        // both; the security Digimon is already gone). Jamming / CanNotBeDeletedByBattle protects it —
        // via the static flag OR a continuous deletion-prevention replacement (R2-1/N-2).
        if (attackerDp > securityDp ||
            HasFlag(attacker.Metadata, attackerCard.Metadata, BattleResolver.PreventBattleDeletionKey) ||
            BattleDeletionGate.PreventsBattleDeletion(context, attackerId))
        {
            return false;
        }

        var metadata = new Dictionary<string, object?>(attacker.Metadata, StringComparer.Ordinal)
        {
            [BattleResolver.DeletedByBattleKey] = true,
            [BattleResolver.DpBeforeBattleKey] = attackerDp
        };
        context.CardInstanceRepository.Upsert(attacker with { Metadata = metadata });

        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(attackerOwner, attackerId, ChoiceZone.BattleArea, ChoiceZone.Trash),
            cancellationToken).ConfigureAwait(false);
        return true;
    }

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

        // N-2 / D-A2: the security Digimon's DP also reflects continuous DP effects from other cards.
        securityDp = ContinuousDpGate.ResolveDp(context, cardId, securityDp);
        return true;
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

/// <summary>(D-1) Outcome of the shared per-card security-check loop.</summary>
public sealed record SecurityCheckLoopResult(
    IReadOnlyList<HeadlessEntityId> CheckedCards,
    IReadOnlyList<ZoneMoveResult> Movements,
    int SecurityDigimonBattles,
    bool AttackerDeleted);

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
    bool AttackerDeletedBySecurity = false)
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
