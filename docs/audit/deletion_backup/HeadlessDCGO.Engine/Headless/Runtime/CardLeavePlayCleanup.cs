// ============================================================================
// ⛔ DELETION-TARGET · DO-NOT-REFERENCE
// 원장(docs/audit/filelist/merged_files_no_cards.csv): 삭제대상여부=Y · 결함여부=Y
// 분류: substrate 오배치(other)
// 미러 원가(재이관 대상): Script/CardController.cs::DestroyPermanentsClass.Destroy() record-parameters block@3762
// 이 파일은 AS-IS 원본에 동일-경로 대응이 없는 오배치/발명 코드다.
// 규칙 로직은 위 미러 원가로 재이관 후 이 파일은 삭제 예정.
// 서브에이전트/포팅 작업 시: 이 파일의 심볼을 참조·모방·확장하지 말 것.
// ============================================================================
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
        EngineContext? context,
        HeadlessEntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (cardId.IsEmpty)
        {
            return;
        }

        if (repository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null)
        {
            var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
            SnapshotPostReplacementKeywords(context, cardId, metadata, repository);
            repository.Upsert(record with { Metadata = metadata });
        }

        OnLeftPlay(cardId);
    }

    /// <summary>Non-deletion departure (bounce / deck / stack placement). (③-B) The registry binding drop is
    /// RETIRED — the EffectRegistry producer is 0, so it was a dead write; the live continuous scan is over on-field
    /// permanents, so a departed card is no longer scanned. The seam is kept (its four call-sites remain wired) for
    /// the AS-IS "leaving the field ends the card's effects" contract.</summary>
    public static void OnLeftPlay(HeadlessEntityId cardId)
    {
        _ = cardId;
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
        EngineContext? context,
        HeadlessEntityId cardId,
        Dictionary<string, object?> metadata,
        ICardInstanceRepository? repository = null)
    {
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
            // (RC-5) context-only: the registry-only HasKeyword fallback is retired (keyword-binding producer 0);
            // the live keyword state is the AS-IS interface scan reached through the EngineContext overload.
            bool has = context is not null && ContinuousKeywordGate.HasKeyword(context, cardId, keyword);
            if (has)
            {
                metadata[flagKey] = true;
            }
        }

        // (RC-5) The Partition colour-group / Decode sourceCondition binding-metadata snapshot loops are RETIRED:
        // no keyword binding exists (producer 0), so there is nothing to copy. The live Partition/Decode data path
        // is the printed/granted ActivateClass collected by the PRE cut-in window (design item RD-RC-03).
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

        // (R1-a) AS-IS permanent.DP = the EFFECTIVE value: base printed DP folded LIVE with every
        // IChangeDPEffect / LinkedDP / Boost (Permanent.DP). Stamp the just-before-remove-field snapshot from it.
        // When no live context is available (bare-repository cleanup), fall back to the raw printed base.
        int? baseDp = ReadIntNullable(record.Metadata, BattleResolver.DpKey);
        if (baseDp is null && context is not null
            && context.CardRepository.TryGetCard(record.DefinitionId, out CardRecord? dpDefinition) && dpDefinition is not null)
        {
            baseDp = ReadIntNullable(dpDefinition.Metadata, BattleResolver.DpKey);
        }

        if (baseDp is int printedDp)
        {
            metadata[DpJustBeforeRemoveFieldKey] = context is null
                ? printedDp
                : new Assets.Scripts.Script.CardEffectCommons.Permanent(context, cardId, record.OwnerId).DP;
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

            // (RD-P6C3-A3) AS-IS `cardSource.PermanentJustBeforeRemoveField = permanent` (CardController.cs:3781;
            // mirror faithful DestroyPermanentsClass.Destroy() :3534), 1:1: charge the per-match service store
            // (CardSource.PermanentJustBeforeRemoveField, keyed by instance) on the TOP card AND every digivolution
            // source, all pointing at the SAME leaving permanent (mirror `Permanent` identity = the top instance).
            // This is the surface the AS-IS OnDeletion identity gate reads — CanActivateOnDeletion (OnDeletion.cs:141)
            // requires `card.PermanentJustBeforeRemoveField == TopCard.PermanentJustBeforeRemoveField`, so without
            // this charge a printed [Ascension]/[On Deletion] response collects (GetSkillInfos) but CanActivate=false
            // (the divergent CardLeavePlayCleanup metadata key was NOT the surface CanActivateOnDeletion reads). The
            // metadata key below is the persistent-identity echo; both are stamped. AS-IS ORDER: this snapshot runs
            // AFTER the OnDestroyedAnyone/OnLeaveFieldAnyone stacks were built (collect-before-removal) and BEFORE the
            // trash move — so the store is charged before the window RESOLVES (main-loop AutoProcessCheck), when the
            // cards are already in the trash (IsExistOnTrash true). Context-only: the store is a live view keyed off
            // the mirror CardSource/EngineContext; every production deletion path carries a context.
            topCard.PermanentJustBeforeRemoveField = permanent;
            foreach (HeadlessEntityId sourceId in DeletionReplacementGate.ReadSourceIds(record.Metadata))
            {
                new Assets.Scripts.Script.CardEffectCommons.CardSource(context, sourceId, record.OwnerId)
                    .PermanentJustBeforeRemoveField = permanent;
            }
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
