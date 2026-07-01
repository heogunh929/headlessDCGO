namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BlockTiming
{
    public const string HasBlockerKey = "hasBlocker";
    public const string CanBlockKey = "canBlock";
    public const string CannotBlockKey = "cannotBlock";
    public const string CanSuspendKey = "canSuspend";
    public const string IsSuspendedKey = "isSuspended";
    public const string HasCollisionKey = "hasCollision";
    public const string CannotBeAffectedByCollisionKey = "cannotBeAffectedByCollision";
    public const string RequestIdPrefix = "block-choice";
    public const string ChoiceMessage = "Select 1 Digimon that will block.";

    public IReadOnlyList<BlockerCandidate> GetBlockerCandidates(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HeadlessAttackState attack = context.AttackController.Current;
        if (!attack.IsPending ||
            attack.AttackerId is null ||
            attack.AttackingPlayerId is null ||
            attack.DefendingPlayerId is null ||
            context.ZoneMover is not IZoneStateReader zoneReader ||
            !TryReadInstanceAndCard(context, attack.AttackerId.Value, out CardInstanceRecord? attacker, out CardRecord? attackerCard))
        {
            return Array.Empty<BlockerCandidate>();
        }

        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(attackerCard);

        // (PRIM-W3) an unblockable attacker (CanNotBeBlockedStaticSelfEffect) offers no blocker candidates.
        if (ContinuousRestrictionGate.EvaluateBeBlocked(context, attack.AttackerId.Value).IsRestricted)
        {
            return Array.Empty<BlockerCandidate>();
        }

        bool attackerHasCollision = ReadBool(attacker.Metadata, HasCollisionKey) ||
            ReadBool(attackerCard.Metadata, HasCollisionKey);

        return zoneReader
            .GetCards(attack.DefendingPlayerId.Value, ChoiceZone.BattleArea)
            .Where(candidateId => attack.TargetId != candidateId)
            .Select(candidateId => TryCreateCandidate(context, attack, candidateId, attackerHasCollision))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .OrderBy(candidate => candidate.BlockerId.Value, StringComparer.Ordinal)
            .ToArray();
    }

    public BlockTimingResult RequestBlockChoice(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ChoiceController.Current.IsPending)
        {
            return BlockTimingResult.Failure("Cannot open block timing while another choice is pending.");
        }

        HeadlessAttackState attack = context.AttackController.Current;
        if (!attack.IsPending || attack.AttackerId is null || attack.DefendingPlayerId is null)
        {
            return BlockTimingResult.Failure("Block timing requires a pending attack.");
        }

        IReadOnlyList<BlockerCandidate> candidates = GetBlockerCandidates(context);
        if (candidates.Count == 0)
        {
            return BlockTimingResult.Success(
                attack,
                HeadlessChoiceState.Empty,
                candidates,
                choiceRequested: false,
                choiceResolved: false,
                skipped: false,
                blockerId: null);
        }

        bool canSkip = CanSkipBlock(context, attack);
        ChoiceRequest request = new(
            ChoiceType.Blocker,
            attack.DefendingPlayerId.Value,
            ChoiceMessage,
            minCount: canSkip ? 0 : 1,
            maxCount: 1,
            canSkip,
            ChoiceZone.BattleArea,
            candidates.Select(candidate => candidate.ToChoiceCandidate()).ToArray());

        HeadlessChoiceState choice = context.ChoiceController.RequestChoice(
            request,
            new HeadlessEntityId($"{RequestIdPrefix}:{attack.DefendingPlayerId.Value}:{attack.AttackCount}"));

        return BlockTimingResult.Success(
            attack,
            choice,
            candidates,
            choiceRequested: true,
            choiceResolved: false,
            skipped: false,
            blockerId: null);
    }

    public BlockTimingResult ResolveBlockChoice(
        EngineContext context,
        ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        if (!context.ChoiceController.Current.IsPending ||
            context.ChoiceController.PendingRequest is not ChoiceRequest request)
        {
            return BlockTimingResult.Failure("Block choice resolution requires a pending block choice.");
        }

        if (request.Type != ChoiceType.Blocker)
        {
            return BlockTimingResult.Failure("Pending choice is not a block choice.");
        }

        HeadlessAttackState before = context.AttackController.Current;
        if (!before.IsPending)
        {
            return BlockTimingResult.Failure("Block choice resolution requires a pending attack.");
        }

        try
        {
            HeadlessChoiceState choice = context.ChoiceController.ResolveChoice(result);
            if (result.IsSkipped)
            {
                return BlockTimingResult.Success(
                    before,
                    choice,
                    Array.Empty<BlockerCandidate>(),
                    choiceRequested: false,
                    choiceResolved: true,
                    skipped: true,
                    blockerId: null);
            }

            HeadlessEntityId blockerId = result.SelectedIds.Count == 0
                ? throw new InvalidOperationException("Block choice requires a selected blocker.")
                : result.SelectedIds[0];

            IReadOnlyList<BlockerCandidate> candidates = GetBlockerCandidates(context);
            if (!candidates.Any(candidate => candidate.BlockerId == blockerId))
            {
                throw new InvalidOperationException($"Selected blocker '{blockerId.Value}' is not a legal blocker candidate.");
            }

            HeadlessAttackState blocked = context.AttackController.SelectBlocker(blockerId);

            // A3 fix — DCG rule: the blocking Digimon is suspended, then "when blocking" effects fire.
            // Mirrors AS-IS AttackProcess.SwitchDefender (SuspendPermanentsClass.Tap() followed by
            // StackSkillInfos(OnBlockAnyone)). Without this the blocker stayed unsuspended after a block.
            SuspendBlocker(context, blockerId);
            TriggerEventEmitter.Emit(
                context.GameEventQueue,
                TriggerTimings.OnBlock,
                actor: blocked.DefendingPlayerId,
                subject: blockerId);

            return BlockTimingResult.Success(
                blocked,
                choice,
                candidates,
                choiceRequested: false,
                choiceResolved: true,
                skipped: false,
                blockerId);
        }
        catch (InvalidOperationException ex)
        {
            return BlockTimingResult.Failure(ex.Message);
        }
    }

    /// <summary>Suspends the blocking Digimon (DCG: a Digimon that blocks is tapped). Idempotent —
    /// writes <see cref="IsSuspendedKey"/> = true onto the blocker's instance metadata.</summary>
    private static void SuspendBlocker(EngineContext context, HeadlessEntityId blockerId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(blockerId, out CardInstanceRecord? blocker) ||
            blocker is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(blocker.Metadata, StringComparer.Ordinal)
        {
            [IsSuspendedKey] = true
        };
        context.CardInstanceRepository.Upsert(blocker with { Metadata = metadata });
    }

    private static BlockerCandidate? TryCreateCandidate(
        EngineContext context,
        HeadlessAttackState attack,
        HeadlessEntityId blockerId,
        bool attackerHasCollision)
    {
        if (!TryReadInstanceAndCard(context, blockerId, out CardInstanceRecord? blocker, out CardRecord? blockerCard))
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(blocker);
        ArgumentNullException.ThrowIfNull(blockerCard);
        if (blocker.OwnerId != attack.DefendingPlayerId ||
            !IsDigimon(blockerCard) ||
            ReadBool(blocker.Metadata, IsSuspendedKey) ||
            !ReadBool(blocker.Metadata, CanSuspendKey, defaultValue: true) ||
            ReadBool(blocker.Metadata, CannotBlockKey) ||
            ReadBool(blockerCard.Metadata, CannotBlockKey) ||
            !ReadBool(blocker.Metadata, CanBlockKey, defaultValue: true) ||
            !(HasBlocker(blocker, blockerCard, attackerHasCollision)
                // (GR-005) a self-static <Blocker> lives as a registry keyword binding, not the hasBlocker
                // metadata flag — derive it from the registry so ported blockers actually block in live play.
                || ContinuousKeywordGate.HasKeyword(context, blockerId, ContinuousKeywordGate.Blocker)))
        {
            return null;
        }

        // (X-04) Continuous effects from other cards can forbid this Digimon from blocking.
        if (ContinuousRestrictionGate.EvaluateBlock(context, blockerId, attack.AttackerId).IsRestricted)
        {
            return null;
        }

        return new BlockerCandidate(
            blocker.InstanceId,
            blocker.DefinitionId,
            blocker.OwnerId,
            attack.AttackerId!.Value);
    }

    private static bool HasBlocker(
        CardInstanceRecord blocker,
        CardRecord blockerCard,
        bool attackerHasCollision)
    {
        if (ReadBool(blocker.Metadata, HasBlockerKey) || ReadBool(blockerCard.Metadata, HasBlockerKey))
        {
            return true;
        }

        return attackerHasCollision &&
            !ReadBool(blocker.Metadata, CannotBeAffectedByCollisionKey) &&
            !ReadBool(blockerCard.Metadata, CannotBeAffectedByCollisionKey);
    }

    private static bool CanSkipBlock(
        EngineContext context,
        HeadlessAttackState attack)
    {
        if (attack.AttackerId is not HeadlessEntityId attackerId ||
            !TryReadInstanceAndCard(context, attackerId, out CardInstanceRecord? attacker, out CardRecord? attackerCard) ||
            attacker is null ||
            attackerCard is null)
        {
            return true;
        }

        return !(ReadBool(attacker.Metadata, HasCollisionKey) || ReadBool(attackerCard.Metadata, HasCollisionKey));
    }

    private static bool TryReadInstanceAndCard(
        EngineContext context,
        HeadlessEntityId instanceId,
        out CardInstanceRecord? instance,
        out CardRecord? card)
    {
        card = null;
        return context.CardInstanceRepository.TryGetInstance(instanceId, out instance) &&
            instance is not null &&
            context.CardRepository.TryGetCard(instance.DefinitionId, out card) &&
            card is not null;
    }

    private static bool IsDigimon(CardRecord card)
    {
        return string.Equals(card.CardType, "Digimon", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, object?> metadata,
        string key,
        bool defaultValue = false)
    {
        if (!metadata.TryGetValue(key, out object? rawValue) || rawValue is null)
        {
            return defaultValue;
        }

        return rawValue switch
        {
            bool value => value,
            string value => bool.TryParse(value, out bool parsed) ? parsed : defaultValue,
            _ => defaultValue
        };
    }
}

public sealed record BlockerCandidate(
    HeadlessEntityId BlockerId,
    HeadlessEntityId BlockerDefinitionId,
    HeadlessPlayerId PlayerId,
    HeadlessEntityId AttackerId)
{
    public ChoiceCandidate ToChoiceCandidate()
    {
        return new ChoiceCandidate(
            BlockerId,
            $"Block with {BlockerDefinitionId.Value}",
            ChoiceZone.BattleArea,
            IsSelectable: true,
            ownerId: PlayerId);
    }
}

public sealed record BlockTimingResult(
    bool IsSuccess,
    HeadlessAttackState Attack,
    HeadlessChoiceState Choice,
    IReadOnlyList<BlockerCandidate> Candidates,
    bool ChoiceRequested,
    bool ChoiceResolved,
    bool IsSkipped,
    HeadlessEntityId? BlockerId,
    string FailureReason)
{
    public static BlockTimingResult Success(
        HeadlessAttackState attack,
        HeadlessChoiceState choice,
        IReadOnlyList<BlockerCandidate> candidates,
        bool choiceRequested,
        bool choiceResolved,
        bool skipped,
        HeadlessEntityId? blockerId)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return new BlockTimingResult(
            true,
            attack,
            choice,
            candidates.ToArray(),
            choiceRequested,
            choiceResolved,
            skipped,
            blockerId,
            string.Empty);
    }

    public static BlockTimingResult Failure(string failureReason)
    {
        return new BlockTimingResult(
            false,
            HeadlessAttackState.Empty,
            HeadlessChoiceState.Empty,
            Array.Empty<BlockerCandidate>(),
            ChoiceRequested: false,
            ChoiceResolved: false,
            IsSkipped: false,
            BlockerId: null,
            FailureReason: failureReason ?? string.Empty);
    }
}
