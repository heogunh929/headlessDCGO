namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

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
            SnapshotPostReplacementKeywords(effectRegistry, context, cardId, metadata, repository);
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

        // (B.O.5-tail) a self-[On Deletion] grant marked SurviveOwnLeave must OUTLIVE the card leaving play so it
        // can fire ON that removal; it is removed after it resolves (DelayedOneShot) or at its duration boundary.
        effectRegistry.RemoveWhere(binding => binding.Request.Context.SourceEntityId == cardId
            && !(binding.Request.Context.Values.TryGetValue(Effects.AutoProcessingTriggerCollector.SurviveOwnLeaveKey, out object? survive) && survive is true));
    }

    // --- (R2-P1-3) AS-IS "record parameters just before deletion" (CardController.cs:3762-3783) -----------
    //
    // Just before a deleted permanent's cards go to the trash — AFTER the PRE cut-in fixed the target list
    // and the OnDestroyedAnyone/OnLeaveFieldAnyone stacks were built — AS-IS records onto the permanent:
    // DPJustBeforeRemoveField (the EFFECTIVE permanent.DP incl. continuous modifiers), LevelJustBeforeRemoveField
    // (only when TopCard.HasLevel), CostJustBeforeRemoveField (only when TopCard.HasPlayCost, GetCostItself),
    // CardNames/CardTraitsJustBeforeRemoveField (copies of the top card's live lists), and stamps
    // cardSource.PermanentJustBeforeRemoveField = permanent onto EVERY card of the stack (top + digivolution
    // sources) so post-trash effects can group cards by "same permanent" identity (OnDeletion.cs:123-191).
    // Consumers (~15 cards: EX4_052 / BT14_030 / EX4_071 / RB1_029 / BT8_107 …) read these DURING the stacked
    // deletion windows, after the cards are already in the trash. Latent infra ([[no-callsite-not-skip-reason]]):
    // recorded uniformly on all four deletion paths (sink immediate / deferred-finalize sweep / battle /
    // security) via this shared snapshot seam. NOTE: AS-IS also records on the bounce / deck-bounce paths
    // (CardController.cs:2719-2737) — those ride the non-deletion departure seam and are wired when a bounce
    // consumer is ported (same keys, same helper).
    /// <summary>Instance metadata: effective DP recorded just before the card left the field (-1 = none).</summary>
    public const string DpJustBeforeRemoveFieldKey = "dpJustBeforeRemoveField";
    /// <summary>Instance metadata: permanent level just before leaving (only when the top card HAS a level).</summary>
    public const string LevelJustBeforeRemoveFieldKey = "levelJustBeforeRemoveField";
    /// <summary>Instance metadata: printed play cost just before leaving (only when the top card HAS one).</summary>
    public const string CostJustBeforeRemoveFieldKey = "costJustBeforeRemoveField";
    /// <summary>Instance metadata: the top card's live name list just before leaving.</summary>
    public const string CardNamesJustBeforeRemoveFieldKey = "cardNamesJustBeforeRemoveField";
    /// <summary>Instance metadata: the top card's live trait list just before leaving.</summary>
    public const string CardTraitsJustBeforeRemoveFieldKey = "cardTraitsJustBeforeRemoveField";
    /// <summary>Instance metadata (top + every digivolution source): the leaving permanent's identity — the
    /// TOP card's instance id (AS-IS <c>cardSource.PermanentJustBeforeRemoveField</c> reference identity).</summary>
    public const string PermanentJustBeforeRemoveFieldKey = "permanentJustBeforeRemoveFieldId";

    /// <summary>(A4) snapshot the live post-deletion keyword state — and Partition's stored condition list —
    /// into the per-instance flags the POST window / Fortitude replay read (the deletion-time evaluation
    /// moment, 1:1 with AS-IS reading the dead card's effects during its deletion processing).
    /// (R2-P1-3) when <paramref name="repository"/> is supplied, ALSO records the AS-IS
    /// record-parameters block (DP / Level / Cost / Names / Traits / permanent identity) — same seam, same
    /// moment (after the windows are decided, before the trash moves).</summary>
    public static void SnapshotPostReplacementKeywords(
        EffectRegistry effectRegistry,
        EngineContext? context,
        HeadlessEntityId cardId,
        Dictionary<string, object?> metadata,
        ICardInstanceRepository? repository = null)
    {
        ArgumentNullException.ThrowIfNull(effectRegistry);
        ArgumentNullException.ThrowIfNull(metadata);

        if (repository is not null)
        {
            RecordParametersJustBeforeRemoveField(repository, context, cardId, metadata);
        }

        // (P0-3/RD-4) freeze the digivolution-source count BEFORE the unconditional source-trash empties the
        // live stack, so Fortitude's post-deletion replay reads the AS-IS OnDeletion snapshot, not 0.
        metadata[DeletionReplacementGate.SourceCountAtDeletionKey] =
            DeletionReplacementGate.ReadSourceIds(metadata).Count;

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

        // (C-1 witness) Decode's stored per-card sourceCondition (BT19_024 Blue Lv.4) travels with the snapshot
        // too — the PRE candidate filter reads it after the grant binding is dropped at leave-play.
        foreach (EffectBinding binding in effectRegistry.GetKeywordEffects(ContinuousKeywordGate.Decode))
        {
            EffectContext bindingContext = binding.Request.Context;
            if ((bindingContext.SourceEntityId == cardId || bindingContext.TargetEntityIds.Contains(cardId))
                && bindingContext.Values.TryGetValue(
                    Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Decode.DecodeSourceConditionKey, out object? decodeRaw)
                && decodeRaw is not null)
            {
                metadata[Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.Decode.DecodeSourceConditionKey] = decodeRaw;
                break;
            }
        }
    }

    /// <summary>(R2-P1-3) The AS-IS record-parameters step (CardController.cs:3762-3783), 1:1 per field:
    /// effective <c>permanent.DP</c> (continuous modifiers included), <c>permanent.Level</c> only when
    /// <c>TopCard.HasLevel</c>, <c>TopCard.GetCostItself</c> only when <c>TopCard.HasPlayCost</c>, copies of
    /// the live <c>CardNames</c>/<c>CardTraits</c>, and the permanent identity stamped onto the top AND every
    /// digivolution source (<c>foreach cardSource in permanent.cardSources</c>). Writes the top-card fields
    /// into <paramref name="metadata"/> (the caller upserts it); the sources are upserted directly.
    /// With a null <paramref name="context"/> (bare-registry sink, unit tests) the card/permanent views are
    /// unavailable — only the static DP (instance dp + typed modifiers) and a metadata-present level are
    /// recorded; every production deletion path carries a context.
    /// NOTE: distinct from <see cref="BattleResolver.DpBeforeBattleKey"/>, which is a HEADLESS defer-time value
    /// (the DP used in the battle comparison, stamped when the loss is flagged) — AS-IS records THIS block
    /// later, after the cut-in fixed the list; both keys are kept.</summary>
    public static void RecordParametersJustBeforeRemoveField(
        ICardInstanceRepository repository,
        EngineContext? context,
        HeadlessEntityId cardId,
        Dictionary<string, object?> metadata)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(metadata);
        if (cardId.IsEmpty || !repository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        // AS-IS permanent.DP = the EFFECTIVE value (typed modifiers + continuous effects). Fold like the
        // battle/DP-zero readers: base (instance, else definition) + typed DpModifiers + ContinuousDpGate.
        int? baseDp = ReadIntNullable(record.Metadata, BattleResolver.DpKey);
        if (baseDp is null && context is not null
            && context.CardRepository.TryGetCard(record.DefinitionId, out CardRecord? dpDefinition) && dpDefinition is not null)
        {
            baseDp = ReadIntNullable(dpDefinition.Metadata, BattleResolver.DpKey);
        }

        if (baseDp is int printedDp)
        {
            IReadOnlyList<DpModifier> modifiers =
                record.Metadata.TryGetValue(BattleResolver.DpModifiersKey, out object? rawMods) && rawMods is IEnumerable<DpModifier> typed
                    ? typed.ToArray()
                    : Array.Empty<DpModifier>();
            int staticDp = DpCalculator.ComputeDp(printedDp, modifiers);
            metadata[DpJustBeforeRemoveFieldKey] = context is null ? staticDp : ContinuousDpGate.ResolveDp(context, cardId, staticDp);
        }

        if (context is not null)
        {
            var permanent = new Assets.Scripts.Script.CardEffectCommons.Permanent(context, cardId, record.OwnerId);
            Assets.Scripts.Script.CardEffectCommons.CardSource topCard = permanent.TopCard;

            if (topCard.HasLevel)
            {
                metadata[LevelJustBeforeRemoveFieldKey] = permanent.Level;
            }

            if (topCard.HasPlayCost)
            {
                metadata[CostJustBeforeRemoveFieldKey] = topCard.GetCostItself;
            }

            metadata[CardNamesJustBeforeRemoveFieldKey] = topCard.CardNames.ToArray();
            metadata[CardTraitsJustBeforeRemoveFieldKey] = topCard.CardTraits.ToArray();
        }
        else if (record.Metadata.TryGetValue(DeDigivolveHelpers.LevelKey, out object? rawLevel) && rawLevel is int level)
        {
            // Context-less fallback: level only when the instance itself carries one. Cost/names/traits need
            // the card definition views (context-only); every production deletion path carries a context.
            metadata[LevelJustBeforeRemoveFieldKey] = level;
        }

        // Permanent identity — the TOP instance id — onto the top's metadata AND every digivolution source
        // (AS-IS stamps permanent onto each cardSource of the stack; "same permanent" gates compare identity).
        metadata[PermanentJustBeforeRemoveFieldKey] = cardId.Value;
        foreach (HeadlessEntityId sourceId in DeletionReplacementGate.ReadSourceIds(record.Metadata))
        {
            if (repository.TryGetInstance(sourceId, out CardInstanceRecord? source) && source is not null)
            {
                var sourceMetadata = new Dictionary<string, object?>(source.Metadata, StringComparer.Ordinal)
                {
                    [PermanentJustBeforeRemoveFieldKey] = cardId.Value,
                };
                repository.Upsert(source with { Metadata = sourceMetadata });
            }
        }
    }

    // --- (R2-P1-3) reader helpers (AS-IS JustBeforeRemoveField property defaults) --------------------------

    /// <summary>AS-IS <c>Permanent.DPJustBeforeRemoveField</c> (default -1 when never recorded).</summary>
    public static int DpJustBeforeRemoveField(IReadOnlyDictionary<string, object?> metadata) =>
        ReadIntOrDefault(metadata, DpJustBeforeRemoveFieldKey);

    /// <summary>AS-IS <c>Permanent.LevelJustBeforeRemoveField</c> (default -1; consumers gate on &gt; 0).</summary>
    public static int LevelJustBeforeRemoveField(IReadOnlyDictionary<string, object?> metadata) =>
        ReadIntOrDefault(metadata, LevelJustBeforeRemoveFieldKey);

    /// <summary>AS-IS <c>Permanent.CostJustBeforeRemoveField</c> (default -1).</summary>
    public static int CostJustBeforeRemoveField(IReadOnlyDictionary<string, object?> metadata) =>
        ReadIntOrDefault(metadata, CostJustBeforeRemoveFieldKey);

    /// <summary>AS-IS <c>Permanent.CardNamesJustBeforeRemoveField</c> (default empty).</summary>
    public static IReadOnlyList<string> CardNamesJustBeforeRemoveField(IReadOnlyDictionary<string, object?> metadata) =>
        ReadStrings(metadata, CardNamesJustBeforeRemoveFieldKey);

    /// <summary>AS-IS <c>Permanent.CardTraitsJustBeforeRemoveField</c> (default empty).</summary>
    public static IReadOnlyList<string> CardTraitsJustBeforeRemoveField(IReadOnlyDictionary<string, object?> metadata) =>
        ReadStrings(metadata, CardTraitsJustBeforeRemoveFieldKey);

    /// <summary>AS-IS <c>CardSource.PermanentJustBeforeRemoveField</c> as an identity id (default empty =
    /// null reference). Two cards belonged to the same leaving permanent iff both ids are non-empty and equal.</summary>
    public static HeadlessEntityId PermanentJustBeforeRemoveField(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(PermanentJustBeforeRemoveFieldKey, out object? raw) && raw is string id && !string.IsNullOrEmpty(id)
            ? new HeadlessEntityId(id)
            : default;

    private static int ReadIntOrDefault(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is int value ? value : -1;

    private static int? ReadIntNullable(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is int value ? value : null;

    private static IReadOnlyList<string> ReadStrings(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is IEnumerable<string> values
            ? values.ToArray()
            : Array.Empty<string>();
}
