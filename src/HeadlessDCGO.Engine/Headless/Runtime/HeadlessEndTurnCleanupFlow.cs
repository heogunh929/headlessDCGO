namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class HeadlessEndTurnCleanupFlow
{
    private static readonly string[] SharedTurnEndKeys =
    {
        "untilEachTurnEndEffects",
        "untilEndTurnEffects",
        "untilEndOfTurnEffects",
        "untilEndTurnModifiers",
        "untilEndTurnFlags",
        "untilCalculateFixedCostEffect",
        "temporaryPower",
        "turnTemporaryPower",
        "digivolveCountThisTurn",
        "useCountThisTurn",
        "effectUseCountThisTurn",
        "oncePerTurnUsed",
    };

    private static readonly string[] OwnerTurnEndKeys =
    {
        "untilOwnerTurnEndEffects",
    };

    private static readonly string[] OpponentTurnEndKeys =
    {
        "untilOpponentTurnEndEffects",
    };

    public EndTurnCleanupResult Cleanup(
        EngineContext context,
        HeadlessTurnState endingTurn)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(endingTurn);

        HeadlessPlayerId? turnPlayerId = endingTurn.TurnPlayerId;
        HeadlessPlayerId? nonTurnPlayerId = endingTurn.NonTurnPlayerId;
        int resetAttackCount = context.AttackController.Current.AttackCount;
        context.AttackController.ResetTurnAttackState();

        // CV-A1: expire continuous effect bindings whose duration ends with this turn (UntilEachTurnEnd,
        // and owner/opponent-turn-end scoped by the ending turn player vs the binding controller).
        EffectDurationExpiry.ExpireTurnEnd(context.EffectRegistry, turnPlayerId);

        List<string> cleanedCardIds = new();
        List<string> removedKeys = new();
        foreach (CardInstanceRecord record in FieldCardInstances(context))
        {
            Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
            List<string> removedForCard = new();

            // (W6 tail) AddSelfDeleteEffect: promote a matching turn-end self-delete marker to DUE — the
            // game loop runs the real sink deletion ("own" = the card owner's turn just ended;
            // "opponent" = the other player's; "each" = any turn end).
            if (metadata.TryGetValue(GameFlowProcessor.DeleteAtTurnEndKey, out object? timing) &&
                timing?.ToString() is { Length: > 0 } deleteTiming && turnPlayerId.HasValue)
            {
                bool ownerTurnEnded = record.OwnerId == turnPlayerId.Value;
                bool isDue = deleteTiming switch
                {
                    "own" => ownerTurnEnded,
                    "opponent" => !ownerTurnEnded,
                    _ => true,
                };
                if (isDue)
                {
                    metadata[GameFlowProcessor.DeleteAtTurnEndDueKey] = true;
                    removedForCard.Add(GameFlowProcessor.DeleteAtTurnEndKey);
                }
            }

            RemoveKeys(metadata, removedForCard, SharedTurnEndKeys);

            if (turnPlayerId.HasValue && record.OwnerId == turnPlayerId.Value)
            {
                RemoveKeys(metadata, removedForCard, OwnerTurnEndKeys);
            }

            if (nonTurnPlayerId.HasValue && record.OwnerId == nonTurnPlayerId.Value)
            {
                RemoveKeys(metadata, removedForCard, OpponentTurnEndKeys);
            }

            if (removedForCard.Count == 0)
            {
                continue;
            }

            context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
            cleanedCardIds.Add(record.InstanceId.Value);
            removedKeys.AddRange(removedForCard.Select(key => $"{record.InstanceId.Value}:{key}"));
        }

        PromoteBurstMarkers(context, turnPlayerId, cleanedCardIds, removedKeys);

        return new EndTurnCleanupResult(
            Applied: true,
            Reason: "EndTurnCleanup",
            ResetAttackCount: resetAttackCount,
            CleanedCardIds: cleanedCardIds.ToArray(),
            RemovedKeys: removedKeys.ToArray());
    }

    /// <summary>(E2-01 / AS-IS SelectBurstDigivolutionEffect.cs:249-344) The burst end-turn trash is registered on
    /// the <em>permanent</em> (<c>permanent.UntilEachTurnEndEffects</c>), and its <c>ActivateCoroutine1</c> reads
    /// <c>permanent.TopCard</c> LIVE (:315). Headless anchors the marker on the burst-card <em>instance</em>, which a
    /// same-turn re-digivolve buries under a new top — so promotion must reach the marker wherever it now sits in the
    /// permanent's stack (top or buried), not only when it is the battle-area top. The sweep then trashes the stack's
    /// current live top (<see cref="GameFlowProcessor.SweepDueTurnEndDeletionsAsync"/>), mirroring the live TopCard read.</summary>
    private static void PromoteBurstMarkers(
        EngineContext context,
        HeadlessPlayerId? turnPlayerId,
        List<string> cleanedCardIds,
        List<string> removedKeys)
    {
        if (!turnPlayerId.HasValue || context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return;
        }

        foreach (HeadlessEntityId topId in zoneReader.GetCards(turnPlayerId.Value, ChoiceZone.BattleArea).ToArray())
        {
            if (!context.CardInstanceRepository.TryGetInstance(topId, out CardInstanceRecord? top) || top is null)
            {
                continue;
            }

            // Walk the permanent's stack (top first, then buried sources) and promote the burst marker on whichever
            // instance carries it — the permanent, not the card instance, owns the AS-IS effect.
            List<HeadlessEntityId> stack = new() { topId };
            stack.AddRange(DeletionReplacementGate.ReadSourceIds(top.Metadata));
            foreach (HeadlessEntityId stackId in stack)
            {
                if (!context.CardInstanceRepository.TryGetInstance(stackId, out CardInstanceRecord? member) || member is null
                    || member.OwnerId != turnPlayerId.Value
                    || !member.Metadata.TryGetValue(GameFlowProcessor.BurstTrashAtTurnEndKey, out object? burst) || burst is not true)
                {
                    continue;
                }

                Dictionary<string, object?> memberMeta = new(member.Metadata, StringComparer.Ordinal)
                {
                    [GameFlowProcessor.BurstTrashAtTurnEndDueKey] = true,
                };
                memberMeta.Remove(GameFlowProcessor.BurstTrashAtTurnEndKey);
                context.CardInstanceRepository.Upsert(member with { Metadata = memberMeta });
                cleanedCardIds.Add(member.InstanceId.Value);
                removedKeys.Add($"{member.InstanceId.Value}:{GameFlowProcessor.BurstTrashAtTurnEndKey}");
            }
        }
    }

    private static IReadOnlyList<CardInstanceRecord> FieldCardInstances(EngineContext context)
    {
        if (context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return context.CardInstanceRepository.Snapshot();
        }

        HashSet<HeadlessEntityId> fieldIds = new();
        foreach (HeadlessPlayerId playerId in context.TurnController.Current.PlayerOrder)
        {
            foreach (ChoiceZone zone in new[] { ChoiceZone.BattleArea, ChoiceZone.BreedingArea })
            {
                foreach (HeadlessEntityId cardId in zoneReader.GetCards(playerId, zone))
                {
                    fieldIds.Add(cardId);
                }
            }
        }

        return context.CardInstanceRepository
            .Snapshot()
            .Where(record => fieldIds.Contains(record.InstanceId))
            .ToArray();
    }

    private static void RemoveKeys(
        Dictionary<string, object?> metadata,
        List<string> removedKeys,
        IReadOnlyList<string> candidateKeys)
    {
        foreach (string key in candidateKeys)
        {
            if (metadata.Remove(key))
            {
                removedKeys.Add(key);
            }
        }
    }
}

public sealed record EndTurnCleanupResult(
    bool Applied,
    string Reason,
    int ResetAttackCount,
    IReadOnlyList<string> CleanedCardIds,
    IReadOnlyList<string> RemovedKeys);
