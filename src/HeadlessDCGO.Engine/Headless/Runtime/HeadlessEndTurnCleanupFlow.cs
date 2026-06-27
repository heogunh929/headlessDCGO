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

        return new EndTurnCleanupResult(
            Applied: true,
            Reason: "EndTurnCleanup",
            ResetAttackCount: resetAttackCount,
            CleanedCardIds: cleanedCardIds.ToArray(),
            RemovedKeys: removedKeys.ToArray());
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
