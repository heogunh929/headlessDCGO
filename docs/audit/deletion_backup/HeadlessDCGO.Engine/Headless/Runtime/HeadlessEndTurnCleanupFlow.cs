// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): DCGO/Assets/Scripts/Script/TurnStateMachine.cs::EndPhase 지속효과 버킷 리셋 (player/permanent Until*TurnEnd = new List)@3151;3177;3185;3194 | DCGO/Assets/Scripts/Script/Player.cs::Player 지속효과 버킷 필드 (UntilEach
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
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

        // (③-B) The CV-A1 registry turn-end sweep (EffectDurationExpiry.ExpireTurnEnd) is RETIRED: the EffectRegistry
        // continuous-binding producer is 0, so the sweep was a dead write. The live AS-IS turn-end duration expiry is
        // the bucket reset — the player/permanent Until*TurnEnd list resets below (and TSM:256/259), covered by J-2.

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

        // (R6-P / RD-R6-02) AS-IS EndPhase reset of the PLAYER-scope effect buckets (TurnStateMachine.cs:3175-3201),
        // the player half of the same turn-end clears the card loop above does for permanent metadata: UntilEachTurnEnd
        // + UntilCalculateFixedCost drop for EVERY player; UntilOwnerTurnEnd for the ending (turn) player; and
        // UntilOpponentTurnEnd for the non-turn player. Reassigning a fresh list = AS-IS `= new List<…>()`.
        foreach (HeadlessPlayerId playerId in context.TurnController.Current.PlayerOrder)
        {
            var player = new Assets.Scripts.Script.CardEffectCommons.Player(context, playerId);
            player.UntilEachTurnEndEffects = new();
            player.UntilCalculateFixedCostEffect = new();
        }

        if (turnPlayerId.HasValue)
        {
            new Assets.Scripts.Script.CardEffectCommons.Player(context, turnPlayerId.Value).UntilOwnerTurnEndEffects = new();
        }

        if (nonTurnPlayerId.HasValue)
        {
            new Assets.Scripts.Script.CardEffectCommons.Player(context, nonTurnPlayerId.Value).UntilOpponentTurnEndEffects = new();
        }

        // (W3 / P6A-PERMANENT-EFFECTLIST-ADDED) AS-IS EndPhase reset of the PERMANENT-scope duration buckets
        // (TurnStateMachine.cs:3183-3201): every field permanent's UntilEachTurnEndEffects drops; the ending (turn)
        // player's permanents drop UntilOwnerTurnEndEffects; the non-turn player's permanents drop
        // UntilOpponentTurnEndEffects. Reassigning a fresh list = AS-IS `= new List<…>()`. The buckets are dormant
        // today (no live writer until batch C), so this is behavior-neutral — it makes the store correct for cutover.
        foreach (CardInstanceRecord record in FieldCardInstances(context))
        {
            var permanent = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, record.InstanceId, record.OwnerId);
            permanent.UntilEachTurnEndEffects = new();

            if (turnPlayerId.HasValue && record.OwnerId == turnPlayerId.Value)
            {
                permanent.UntilOwnerTurnEndEffects = new();
            }

            if (nonTurnPlayerId.HasValue && record.OwnerId == nonTurnPlayerId.Value)
            {
                permanent.UntilOpponentTurnEndEffects = new();
            }
        }

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
