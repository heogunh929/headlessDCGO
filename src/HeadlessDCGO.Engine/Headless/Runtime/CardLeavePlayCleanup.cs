namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (P1) The single leave-play cleanup seam. AS-IS effects live on the permanent, so leaving the field ends
/// them implicitly; headless bindings must be DROPPED explicitly — previously only the sink's effect-delete
/// path did (G6-001), while battle deletions and the pending-sweep finish leaked the dead card's continuous
/// bindings (a deleted Tamer's player-scope buff kept applying). Deletion-type departures additionally
/// SNAPSHOT the card's own post-deletion keywords first (Fortitude / Ascension / Save / Decode / Partition /
/// Armor Purge + Partition's condition list): AS-IS evaluates a dead card's effects DURING its own deletion
/// processing, so the deletion-time keyword state must survive the drop (A4).
/// </summary>
public static class CardLeavePlayCleanup
{
    /// <summary>Deletion-type departure: snapshot the post-deletion keyword state into the card's metadata,
    /// then drop its registered bindings. Idempotent — flags are only ever set true, never cleared.</summary>
    public static void OnDeleted(
        ICardInstanceRepository repository,
        EffectRegistry? effectRegistry,
        EngineContext? context,
        HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (effectRegistry is null || cardId.IsEmpty)
        {
            return;
        }

        if (repository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null)
        {
            var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
            SnapshotPostReplacementKeywords(effectRegistry, context, cardId, metadata);
            repository.Upsert(record with { Metadata = metadata });
        }

        OnLeftPlay(effectRegistry, cardId);
    }

    /// <summary>Non-deletion departure (bounce / deck / stack placement): drop the card's bindings only.</summary>
    public static void OnLeftPlay(EffectRegistry? effectRegistry, HeadlessEntityId cardId)
    {
        if (effectRegistry is null || cardId.IsEmpty)
        {
            return;
        }

        effectRegistry.RemoveWhere(binding => binding.Request.Context.SourceEntityId == cardId);
    }

    /// <summary>(A4) snapshot the live post-deletion keyword state — and Partition's stored condition list —
    /// into the per-instance flags the POST window / Fortitude replay read (the deletion-time evaluation
    /// moment, 1:1 with AS-IS reading the dead card's effects during its deletion processing).</summary>
    public static void SnapshotPostReplacementKeywords(
        EffectRegistry effectRegistry,
        EngineContext? context,
        HeadlessEntityId cardId,
        Dictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(effectRegistry);
        ArgumentNullException.ThrowIfNull(metadata);

        foreach ((string keyword, string flagKey) in new[]
        {
            (ContinuousKeywordGate.Fortitude, DeletionReplacementGate.HasFortitudeKey),
            (ContinuousKeywordGate.Ascension, DeletionReplacementGate.HasAscensionKey),
            (ContinuousKeywordGate.Save, DeletionReplacementGate.HasSaveKey),
            (ContinuousKeywordGate.Decode, DeletionReplacementGate.HasDecodeKey),
            (ContinuousKeywordGate.Partition, DeletionReplacementGate.HasPartitionKey),
            (ContinuousKeywordGate.ArmorPurge, DeletionReplacementGate.HasArmorPurgeKey),
        })
        {
            bool has = context is not null
                ? ContinuousKeywordGate.HasKeyword(context, cardId, keyword)
                : ContinuousKeywordGate.HasKeyword(effectRegistry, cardId, keyword);
            if (has)
            {
                metadata[flagKey] = true;
            }
        }

        // Partition's stored per-card colour groups travel with the snapshot (the grant binding is dropped).
        foreach (EffectBinding binding in effectRegistry.GetKeywordEffects(ContinuousKeywordGate.Partition))
        {
            EffectContext bindingContext = binding.Request.Context;
            if ((bindingContext.SourceEntityId == cardId || bindingContext.TargetEntityIds.Contains(cardId))
                && bindingContext.Values.TryGetValue(
                    Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition.PartitionConditionsKey, out object? raw)
                && raw is not null)
            {
                metadata[Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition.PartitionConditionsKey] = raw;
                break;
            }
        }
    }
}
