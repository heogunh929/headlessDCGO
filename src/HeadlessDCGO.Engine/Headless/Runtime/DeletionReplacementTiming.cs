namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using CardSourceView = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;

/// <summary>
/// (F-6.8) The re-entrant deletion-replacement windows — sibling of <see cref="BlockTiming"/>. Restores
/// AS-IS optionality for keywords whose auto-resolve changed game rules (docs/audit/asis_fidelity_audit.md).
/// Surfaces <see cref="ChoiceType.DeletionReplacement"/> agent choices via the common loop:
/// <list type="bullet">
/// <item><b>PRE</b> (would-be-deleted, deferred via <c>pendingDeletion</c>): the owner may activate a
/// replacement to survive (Evade …), or decline so the state-based sweep finishes the deletion.</item>
/// <item><b>POST</b> (on-deletion, card already trashed): the owner may activate a post-deletion keyword
/// (Ascension …).</item>
/// </list>
/// Some replacements need a SECOND agent decision (which ally / source / target) — a two-step choice:
/// step 1 picks the keyword, step 2 picks its target (tracked by <c>pendingReplacementOption</c>).
/// Cost/effect bodies reuse <see cref="DeletionReplacementGate"/>. Migrated: Evade (pre), Scapegoat (pre,
/// sub-select), Ascension (post). Extended keyword by keyword.
/// </summary>
public sealed class DeletionReplacementTiming
{
    public const string ReplacementDeclinedKey = "replacementDeclined";
    public const string PostResolvedKey = "postDeletionResolved";
    public const string PendingOptionKey = "pendingReplacementOption";
    public const string RequestIdPrefix = "deletion-replacement";
    public const char Delimiter = '#';

    public const string FragmentRemainingKey = "fragmentRemaining";

    /// <summary>(P6) the holder is waiting for its chosen sacrifice's OWN would-be-deleted decision (value =
    /// the sacrificed ally's id). AS-IS resolves the sacrifice through DeletePermanent — the ally's Evade/
    /// Barrier can save it, and the holder is spared only when the sacrifice ACTUALLY resolved
    /// (successProcess).</summary>
    public const string SacrificeAwaitingKey = "sacrificeAwaiting";

    public const string EvadeOption = "evade";          // PRE, no sub
    public const string BarrierOption = "barrier";      // PRE, by-battle, no sub (trash top security)
    public const string ScapegoatOption = "scapegoat";  // PRE, sub = which ally to sacrifice
    public const string FragmentOption = "fragment";    // PRE, sub = which source(s) to trash (repeated)
    public const string AscensionOption = "ascension";  // POST, no sub
    public const string ArmorPurgeOption = "armorpurge"; // (B1) PRE, no sub — deletion CANCELLED: trash top only, promote under-source
    public const string DecoyOption = "decoy";          // PRE, effect/enemy-gated, sub = which Decoy ally
    public const string DecoyEligibleKey = "decoyEligible";
    public const string SaveOption = "save";            // POST, sub = which permanent to place this under
    public const string DecodeOption = "decode";        // POST, effect-deletion only, sub = which source to play free
    public const string PartitionOption = "partition";  // POST, effect-deletion only, sub = play 2 sources free (repeated)

    private static bool NeedsTarget(string option) => option is ScapegoatOption or FragmentOption or DecoyOption or SaveOption or DecodeOption or PartitionOption;

    // --- PRE option set (shared with the sink's defer decision) --------------

    /// <summary>The optional PRE (would-be-deleted) replacements currently available on the card.</summary>
    public static IReadOnlyList<string> PreOptions(ICardInstanceRepository repository, IZoneStateReader zones, CardInstanceRecord record, bool byBattle, EffectRegistry? effectRegistry = null)
    {
        var options = new List<string>();
        // (S3) recognise the LIVE keyword (metadata flag OR HasKeyword) so keyword-granted replacements surface.
        if (DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasEvadeKey, ContinuousKeywordGate.Evade, effectRegistry) &&
            !ReadFlag(record.Metadata, DeletionReplacementGate.IsSuspendedKey))
        {
            options.Add(EvadeOption);
        }

        // Barrier is by-battle only (AS-IS IsByBattle): trash the top security card to survive.
        if (byBattle &&
            DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasBarrierKey, ContinuousKeywordGate.Barrier, effectRegistry) &&
            zones.GetCards(record.OwnerId, ChoiceZone.Security).Count >= 1)
        {
            options.Add(BarrierOption);
        }

        // (B1) AS-IS Armor Purge is a WOULD-BE-DELETED replacement (ArmorPurge.cs:63 willBeRemoveField=false),
        // not a POST response: trash only the top card, promote the under-source, the permanent survives.
        // Gate mirrors CanActivateArmorPurge (battle area + DigivolutionCards >= 1).
        if (DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasArmorPurgeKey, ContinuousKeywordGate.ArmorPurge, effectRegistry) &&
            SourceIds(record.Metadata).Count >= 1)
        {
            options.Add(ArmorPurgeOption);
        }

        if (DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasScapegoatKey, ContinuousKeywordGate.Scapegoat, effectRegistry) &&
            DeletionReplacementGate.FindScapegoatSacrificeCandidates(repository, zones, record, null, effectRegistry).Count > 0)
        {
            options.Add(ScapegoatOption);
        }

        if (DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasFragmentKey, ContinuousKeywordGate.Fragment, effectRegistry) &&
            SourceIds(record.Metadata).Count >= DeletionReplacementGate.FragmentCostOf(record, effectRegistry))
        {
            options.Add(FragmentOption);
        }

        // Decoy: offered only when the deferring deletion marked it enemy-eligible AND a Decoy ally exists.
        // (D1) thread the registry so a keyword-only (production) Decoy grant is visible on this static
        // superset path too — previously only the metadata-flag holder counted here.
        if (ReadFlag(record.Metadata, DecoyEligibleKey) &&
            DeletionReplacementGate.FindDecoyRedirectCandidates(repository, zones, record, null, effectRegistry).Count > 0)
        {
            options.Add(DecoyOption);
        }

        return options;
    }

    /// <summary>Whether a deletion of this card should be DEFERRED for an optional PRE replacement decision.</summary>
    public static bool HasPreOption(ICardInstanceRepository repository, IZoneStateReader zones, CardInstanceRecord record, bool byBattle, EffectRegistry? effectRegistry = null) =>
        PreOptions(repository, zones, record, byBattle, effectRegistry).Count > 0;

    /// <summary>(#3) Context-aware PRE options: identical to the static overload but the Scapegoat/Decoy
    /// candidate counts respect any card-specific condition (<see cref="IDeletionReplacementCandidateConditions"/>),
    /// so an option is surfaced only when a condition-passing target actually exists. The static overload
    /// (used by the sink's defer decision) stays generic — a safe superset, because the state-based sweep
    /// finishes a card whose only option turns out to have no legal target.</summary>
    private static IReadOnlyList<string> PreOptions(EngineContext context, IZoneStateReader zones, CardInstanceRecord record, bool byBattle)
    {
        var options = new List<string>();
        if (DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasEvadeKey, ContinuousKeywordGate.Evade, context.EffectRegistry) &&
            !ReadFlag(record.Metadata, DeletionReplacementGate.IsSuspendedKey))
        {
            options.Add(EvadeOption);
        }

        if (byBattle &&
            DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasBarrierKey, ContinuousKeywordGate.Barrier, context.EffectRegistry) &&
            zones.GetCards(record.OwnerId, ChoiceZone.Security).Count >= 1)
        {
            options.Add(BarrierOption);
        }

        // (B1) PRE Armor Purge — see the static overload.
        if (DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasArmorPurgeKey, ContinuousKeywordGate.ArmorPurge, context.EffectRegistry) &&
            SourceIds(record.Metadata).Count >= 1)
        {
            options.Add(ArmorPurgeOption);
        }

        if (DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasScapegoatKey, ContinuousKeywordGate.Scapegoat, context.EffectRegistry) &&
            DeletionReplacementGate.FindScapegoatSacrificeCandidates(
                context.CardInstanceRepository, zones, record, ResolveCondition(context, record, ScapegoatOption), context.EffectRegistry).Count > 0)
        {
            options.Add(ScapegoatOption);
        }

        if (DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasFragmentKey, ContinuousKeywordGate.Fragment, context.EffectRegistry) &&
            SourceIds(record.Metadata).Count >= DeletionReplacementGate.FragmentCostOf(record, context.EffectRegistry))
        {
            options.Add(FragmentOption);
        }

        if (ReadFlag(record.Metadata, DecoyEligibleKey) &&
            DeletionReplacementGate.FindDecoyRedirectCandidates(
                context.CardInstanceRepository, zones, record, ResolveCondition(context, record, DecoyOption), context.EffectRegistry, context).Count > 0)
        {
            options.Add(DecoyOption);
        }

        return options;
    }

    /// <summary>(#3) Resolves the card-specific candidate predicate for the holder's option from the
    /// context-registered <see cref="IDeletionReplacementCandidateConditions"/>; null (generic) when none
    /// is registered or the card imposes no condition.</summary>
    private static Func<CardInstanceRecord, bool>? ResolveCondition(EngineContext context, CardInstanceRecord holder, string option)
    {
        IDeletionReplacementCandidateConditions conditions =
            context.TryGetService(out IDeletionReplacementCandidateConditions? registered) && registered is not null
                ? registered
                : NoDeletionReplacementCandidateConditions.Instance;
        return conditions.Resolve(holder, option);
    }

    // --- Window awaiting sets -----------------------------------------------

    public bool IsPreAwaiting(EngineContext context, HeadlessEntityId cardId)
    {
        if (context.ZoneMover is not IZoneStateReader zones ||
            !context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return false;
        }

        return ReadFlag(record.Metadata, GameFlowProcessor.PendingDeletionKey) &&
            !ReadFlag(record.Metadata, ReplacementDeclinedKey) &&
            (HasPendingOption(record) || PreOptions(context, zones, record, ByBattle(record)).Count > 0);
    }

    private static bool HasPendingOption(CardInstanceRecord record) =>
        record.Metadata.TryGetValue(PendingOptionKey, out object? raw) && raw is string s && !string.IsNullOrEmpty(s);

    private static IReadOnlyList<string> PostOptions(EngineContext context, IZoneStateReader zones, CardInstanceRecord record)
    {
        var options = new List<string>();
        if (DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasAscensionKey, ContinuousKeywordGate.Ascension, context.EffectRegistry))
        {
            options.Add(AscensionOption);
        }

        if (HasSaveTarget(context, zones, record))
        {
            options.Add(SaveOption);
        }

        // C-13 Decode: effect-deletion only (AS-IS !IsByBattle), once per removal (decoded guard), offered
        // only when a playable Digimon source remains.
        if ((ReadFlag(record.Metadata, DeletionReplacementGate.HasDecodeKey)
                || ContinuousKeywordGate.HasKeyword(context, record.InstanceId, ContinuousKeywordGate.Decode)) && // GR-005 C-group seal
            !ReadFlag(record.Metadata, DeletionReplacementGate.DeletedByBattleKey) &&
            !ReadFlag(record.Metadata, DeletionReplacementGate.DecodedKey) &&
            FindDecodeSourceCandidates(context, record, ResolveCondition(context, record, DecodeOption)).Count > 0)
        {
            options.Add(DecodeOption);
        }

        // C-14 Partition: effect-deletion only, once per removal, offered with >= 2 playable Digimon sources
        // (AS-IS DigivolutionCards.Count >= 2). Plays two sources free as new permanents.
        // (A4) with stored PartitionConditions the AS-IS activation gate applies instead: EACH colour group
        // must be non-empty (CanActivateCondition, Partition.cs:145-159).
        if ((ReadFlag(record.Metadata, DeletionReplacementGate.HasPartitionKey)
                || ContinuousKeywordGate.HasKeyword(context, record.InstanceId, ContinuousKeywordGate.Partition)) && // GR-005 C-group seal
            !ReadFlag(record.Metadata, DeletionReplacementGate.DeletedByBattleKey) &&
            // (S6) AS-IS: "leave other than by one of YOUR effects or in battle" — exclude own-effect leaves.
            !ReadFlag(record.Metadata, DeletionReplacementGate.DeletedByOwnEffectKey) &&
            !ReadFlag(record.Metadata, DeletionReplacementGate.PartitionedKey) &&
            PartitionActivatable(context, record))
        {
            options.Add(PartitionOption);
        }

        return options;
    }

    private static bool HasSaveTarget(EngineContext context, IZoneStateReader zones, CardInstanceRecord record) =>
        DeletionReplacementGate.HasReplacementKeyword(record, DeletionReplacementGate.HasSaveKey, ContinuousKeywordGate.Save, context.EffectRegistry) &&
        SaveTargets(context, zones, record, ResolveCondition(context, record, SaveOption)).Count > 0;

    /// <summary>(C-13 Decode) The leaving card's digivolution sources eligible to be played for free — the
    /// source must resolve to a Digimon definition (AS-IS <c>source.IsDigimon</c>) and pass any card-specific
    /// colour condition (the #3 candidate-condition seam; null = any Digimon source). Sources stay in
    /// <see cref="ChoiceZone.None"/> referenced by the (trashed) card's <c>sourceIds</c>.</summary>
    private static IReadOnlyList<HeadlessEntityId> FindDecodeSourceCandidates(
        EngineContext context, CardInstanceRecord record, Func<CardInstanceRecord, bool>? condition)
    {
        var candidates = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId sourceId in SourceIds(record.Metadata))
        {
            if (!context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? source) || source is null)
            {
                continue;
            }

            if (!context.CardRepository.TryGetCard(source.DefinitionId, out CardRecord? definition) ||
                definition is null ||
                !definition.IsCardType("Digimon"))
            {
                continue;
            }

            if (condition is null || condition(source))
            {
                candidates.Add(sourceId);
            }
        }

        return candidates;
    }

    /// <summary>(C-22 Save) Permanents the deleted card may be placed under — the owner's battle-area cards,
    /// filtered by any card-specific condition (#3 porting-readiness; null = generic).</summary>
    private static IReadOnlyList<HeadlessEntityId> SaveTargets(
        EngineContext context, IZoneStateReader zones, CardInstanceRecord record, Func<CardInstanceRecord, bool>? condition = null) =>
        zones.GetCards(record.OwnerId, ChoiceZone.BattleArea)
            .Where(id => id != record.InstanceId)
            .Where(id => condition is null ||
                (context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? candidate) && candidate is not null && condition(candidate)))
            .ToArray();

    // --- Window open --------------------------------------------------------

    public bool RequestChoice(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ChoiceController.Current.IsPending || context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        // Priority 1: a card mid two-step selection (step 2 = pick the target). The card may be on the
        // battle area (PRE: Scapegoat/Fragment/Decoy) or in the trash (POST: Save).
        foreach (CardInstanceRecord record in context.CardInstanceRepository.Snapshot())
        {
            if (!HasPendingOption(record) ||
                (!zones.GetCards(record.OwnerId, ChoiceZone.BattleArea).Contains(record.InstanceId) &&
                 !zones.GetCards(record.OwnerId, ChoiceZone.Trash).Contains(record.InstanceId)))
            {
                continue;
            }

            string option = (string)record.Metadata[PendingOptionKey]!;
            IReadOnlyList<HeadlessEntityId> targets = GetTargets(context, zones, record, option);
            if (targets.Count == 0)
            {
                // No legal target after all -> abandon the sub-selection, decline the deletion.
                ClearPendingOption(context, record.InstanceId);
                Mark(context, record.InstanceId, ReplacementDeclinedKey);
                continue;
            }

            return OpenTargetChoice(context, record, option, targets);
        }

        // Priority 2: PRE step-1 (would-be-deleted) choices.
        foreach (HeadlessEntityId cardId in ScanBattleArea(context, IsPreStep1Awaiting))
        {
            CardInstanceRecord record = context.CardInstanceRepository.TryGetInstance(cardId, out var r) && r is not null ? r : null!;
            return OpenKeywordChoice(context, record, PreOptions(context, zones, record, ByBattle(record)), "would be deleted");
        }

        // Priority 3: POST (on-deletion) choices.
        foreach (CardInstanceRecord record in context.CardInstanceRepository.Snapshot())
        {
            bool wasDeleted = ReadFlag(record.Metadata, DeletionReplacementGate.DeletedByEffectKey) ||
                ReadFlag(record.Metadata, DeletionReplacementGate.DeletedByBattleKey);
            if (wasDeleted && !ReadFlag(record.Metadata, PostResolvedKey) &&
                zones.GetCards(record.OwnerId, ChoiceZone.Trash).Contains(record.InstanceId) &&
                PostOptions(context, zones, record).Count > 0)
            {
                return OpenKeywordChoice(context, record, PostOptions(context, zones, record), "was deleted");
            }
        }

        return false;
    }

    private bool IsPreStep1Awaiting(EngineContext context, HeadlessEntityId cardId)
    {
        if (context.ZoneMover is not IZoneStateReader zones ||
            !context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return false;
        }

        return ReadFlag(record.Metadata, GameFlowProcessor.PendingDeletionKey) &&
            !ReadFlag(record.Metadata, ReplacementDeclinedKey) &&
            !HasPendingOption(record) &&
            // (P6) a holder awaiting its sacrifice's fate does not reopen its own window.
            !record.Metadata.ContainsKey(SacrificeAwaitingKey) &&
            PreOptions(context, zones, record, ByBattle(record)).Count > 0;
    }

    private static bool OpenKeywordChoice(EngineContext context, CardInstanceRecord record, IReadOnlyList<string> options, string phrase)
    {
        if (options.Count == 0)
        {
            return false;
        }

        ChoiceCandidate[] candidates = options
            .Select(option => Candidate(record, $"{record.InstanceId.Value}{Delimiter}{option}", option))
            .ToArray();

        OpenRequest(context, record, $"'{record.InstanceId.Value}' {phrase}: choose a replacement effect or decline.", canSkip: true, candidates);
        return true;
    }

    private static bool OpenTargetChoice(EngineContext context, CardInstanceRecord record, string option, IReadOnlyList<HeadlessEntityId> targets)
    {
        ChoiceCandidate[] candidates = targets
            .Select(target => Candidate(record, $"{record.InstanceId.Value}{Delimiter}{option}{Delimiter}{target.Value}", target.Value))
            .ToArray();

        // The sub-selection is mandatory once the keyword is activated (AS-IS canNoSelect:false) —
        // EXCEPT Save (C6): AS-IS SaveProcess selects with canNoSelect:true (the owner may still back out).
        OpenRequest(context, record, $"'{record.InstanceId.Value}' {option}: choose a target.", canSkip: option == SaveOption, candidates);
        return true;
    }

    private static ChoiceCandidate Candidate(CardInstanceRecord record, string id, string label) =>
        new(new HeadlessEntityId(id), label, ChoiceZone.BattleArea, IsSelectable: true, ownerId: record.OwnerId);

    private static void OpenRequest(EngineContext context, CardInstanceRecord record, string message, bool canSkip, IReadOnlyList<ChoiceCandidate> candidates)
    {
        var request = new ChoiceRequest(
            ChoiceType.DeletionReplacement, record.OwnerId, message,
            minCount: canSkip ? 0 : 1, maxCount: 1, canSkip, ChoiceZone.BattleArea, candidates);
        context.ChoiceController.RequestChoice(request, new HeadlessEntityId($"{RequestIdPrefix}:{record.InstanceId.Value}"));
    }

    private IReadOnlyList<HeadlessEntityId> GetTargets(EngineContext context, IZoneStateReader zones, CardInstanceRecord record, string option) =>
        option switch
        {
            ScapegoatOption => DeletionReplacementGate.FindScapegoatSacrificeCandidates(
                context.CardInstanceRepository, zones, record, ResolveCondition(context, record, ScapegoatOption), context.EffectRegistry),
            FragmentOption => SourceIds(record.Metadata),   // remaining digivolution sources to trash
            DecoyOption => DeletionReplacementGate.FindDecoyRedirectCandidates(
                context.CardInstanceRepository, zones, record, ResolveCondition(context, record, DecoyOption), context.EffectRegistry, context),
            SaveOption => SaveTargets(context, zones, record, ResolveCondition(context, record, SaveOption)),
            DecodeOption => FindDecodeSourceCandidates(context, record, ResolveCondition(context, record, DecodeOption)),
            PartitionOption => PartitionPickCandidates(context, record),
            _ => Array.Empty<HeadlessEntityId>(),
        };

    // --- (A4) Partition colour groups -----------------------------------------------------------------

    /// <summary>The holder's stored AS-IS <c>PartitionCondition</c> pair — from the sink's deletion-time
    /// metadata snapshot (the grant binding is dropped when the card leaves play) or the live grant binding.
    /// Null = the grant carries none (legacy flat behaviour).</summary>
    private static IReadOnlyList<PartitionCondition>? PartitionConditionsOf(EngineContext context, CardInstanceRecord record)
    {
        if (record.Metadata.TryGetValue(PartitionCondition.PartitionConditionsKey, out object? snap)
            && snap is IReadOnlyList<PartitionCondition> snapshot && snapshot.Count == 2)
        {
            return snapshot;
        }

        foreach (EffectBinding binding in context.EffectRegistry.GetKeywordEffects(ContinuousKeywordGate.Partition))
        {
            EffectContext effectContext = binding.Request.Context;
            if (effectContext.SourceEntityId != record.InstanceId && !effectContext.TargetEntityIds.Contains(record.InstanceId))
            {
                continue;
            }

            if (effectContext.Values.TryGetValue(PartitionCondition.PartitionConditionsKey, out object? raw)
                && raw is IReadOnlyList<PartitionCondition> conditions && conditions.Count == 2)
            {
                return conditions;
            }
        }

        return null;
    }

    // (A4) AS-IS CanActivateCondition (Partition.cs:145-159): with conditions, EACH group must be non-empty;
    // without (legacy grant), the flat >= 2 Digimon-source gate stands.
    private static bool PartitionActivatable(EngineContext context, CardInstanceRecord record)
    {
        IReadOnlyList<PartitionCondition>? conditions = PartitionConditionsOf(context, record);
        if (conditions is null)
        {
            return FindDecodeSourceCandidates(context, record, ResolveCondition(context, record, PartitionOption)).Count >= 2;
        }

        return FindPartitionGroupCandidates(context, record, conditions, groupIndex: 0, applyMutualExclusion: false).Count > 0
            && FindPartitionGroupCandidates(context, record, conditions, groupIndex: 1, applyMutualExclusion: false).Count > 0;
    }

    // (A4) pick #1 (remaining == 2) draws from group [0], pick #2 (remaining == 1) from group [1]. The pick-1
    // pool mirrors the AS-IS pre-adjust (Partition.cs:161-170 `sourceOneCard.Except(sourceTwoCard)` when the
    // other group has exactly one card); pick #2 exclusion is implicit — TryPartitionPlaySourceAsync already
    // removed the first pick from sourceIds.
    private IReadOnlyList<HeadlessEntityId> PartitionPickCandidates(EngineContext context, CardInstanceRecord record)
    {
        IReadOnlyList<PartitionCondition>? conditions = PartitionConditionsOf(context, record);
        if (conditions is null)
        {
            return FindDecodeSourceCandidates(context, record, ResolveCondition(context, record, PartitionOption));
        }

        int remaining = ReadInt(record.Metadata, FragmentRemainingKey, 1);
        int groupIndex = remaining >= 2 ? 0 : 1;
        return FindPartitionGroupCandidates(context, record, conditions, groupIndex, applyMutualExclusion: groupIndex == 0);
    }

    // (A4) AS-IS group filter (Partition.cs:66-117): one-colour = HasCardColor(colour) && HasLevel &&
    // Level == condition.Level (EXACT); two-colour = either colour + exact level; by-name = EqualsCardName
    // (level ignored). Level/colour read through the (A3) folded view.
    private static IReadOnlyList<HeadlessEntityId> FindPartitionGroupCandidates(
        EngineContext context, CardInstanceRecord record, IReadOnlyList<PartitionCondition> conditions, int groupIndex, bool applyMutualExclusion)
    {
        PartitionCondition condition = conditions[groupIndex];
        var candidates = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId sourceId in SourceIds(record.Metadata))
        {
            if (MatchesPartitionCondition(context, record, sourceId, condition))
            {
                candidates.Add(sourceId);
            }
        }

        if (applyMutualExclusion && groupIndex == 0)
        {
            // AS-IS pre-adjust: when group 2 has exactly one candidate, that card is reserved for group 2.
            List<HeadlessEntityId> other = SourceIds(record.Metadata)
                .Where(id => MatchesPartitionCondition(context, record, id, conditions[1]))
                .ToList();
            if (other.Count == 1)
            {
                candidates.Remove(other[0]);
            }
        }

        return candidates;
    }

    private static bool MatchesPartitionCondition(EngineContext context, CardInstanceRecord record, HeadlessEntityId sourceId, PartitionCondition condition)
    {
        var source = new CardSourceView(context, sourceId, record.OwnerId);
        if (condition.HasOneColour)
        {
            return condition.Color is not null && source.HasCardColor(condition.Color)
                && source.HasLevel && source.Level == condition.Level;
        }

        if (condition.hasTwoColor)
        {
            return ((condition.Color is not null && source.HasCardColor(condition.Color))
                    || (condition.Color2 is not null && source.HasCardColor(condition.Color2)))
                && source.HasLevel && source.Level == condition.Level;
        }

        if (condition.hasName)
        {
            return condition.Name is not null && source.EqualsCardName(condition.Name);
        }

        return false; // AS-IS: an unrecognised condition nulls the group.
    }

    // --- Resolve ------------------------------------------------------------

    public async Task<DeletionReplacementResolveResult> ResolveChoice(EngineContext context, ChoiceResult result)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(result);

        if (!context.ChoiceController.Current.IsPending ||
            context.ChoiceController.PendingRequest is not ChoiceRequest request ||
            request.Type != ChoiceType.DeletionReplacement)
        {
            return DeletionReplacementResolveResult.Failure("No pending deletion-replacement choice.");
        }

        HeadlessEntityId cardId = ParseCard(request);
        bool isStep2 = context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? rec) && rec is not null && HasPendingOption(rec);

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException ex)
        {
            return DeletionReplacementResolveResult.Failure(ex.Message);
        }

        return isStep2
            ? await ResolveTargetStep(context, cardId, result).ConfigureAwait(false)
            : await ResolveKeywordStep(context, cardId, result).ConfigureAwait(false);
    }

    private async Task<DeletionReplacementResolveResult> ResolveKeywordStep(EngineContext context, HeadlessEntityId cardId, ChoiceResult result)
    {
        bool isPost = !IsPendingDeletion(context, cardId);

        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            Mark(context, cardId, isPost ? PostResolvedKey : ReplacementDeclinedKey);
            return DeletionReplacementResolveResult.Declined(cardId);
        }

        string option = Segment(result.SelectedIds[0], 1);
        if (NeedsTarget(option))
        {
            // Defer to step 2: record the chosen keyword; the next window opens the target choice.
            SetPendingOption(context, cardId, option);
            if (option == FragmentOption && context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? fr) && fr is not null)
            {
                Upsert(context, cardId, m => m[FragmentRemainingKey] = DeletionReplacementGate.FragmentCostOf(fr, context.EffectRegistry));
            }

            // C-14 Partition plays exactly two sources (one per AS-IS colour group) — a repeated single-select
            // sharing the Fragment "remaining" counter.
            if (option == PartitionOption)
            {
                Upsert(context, cardId, m => m[FragmentRemainingKey] = 2);
            }

            return DeletionReplacementResolveResult.Activated(cardId, option);
        }

        if (!await ApplyNoTarget(context, cardId, option).ConfigureAwait(false))
        {
            Mark(context, cardId, isPost ? PostResolvedKey : ReplacementDeclinedKey);
            return DeletionReplacementResolveResult.Declined(cardId);
        }

        if (isPost)
        {
            Mark(context, cardId, PostResolvedKey);
        }

        return DeletionReplacementResolveResult.Activated(cardId, option);
    }

    private async Task<DeletionReplacementResolveResult> ResolveTargetStep(EngineContext context, HeadlessEntityId cardId, ChoiceResult result)
    {
        string option = context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null && HasPendingOption(r)
            ? (string)r.Metadata[PendingOptionKey]!
            : string.Empty;

        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            ClearPendingOption(context, cardId);
            ClearFragmentRemaining(context, cardId);
            Mark(context, cardId, ReplacementDeclinedKey);
            // (C6) a declined POST sub-selection (Save's optional target) closes the POST window — else it
            // would reopen every loop iteration.
            if (!IsPendingDeletion(context, cardId))
            {
                Mark(context, cardId, PostResolvedKey);
            }

            return DeletionReplacementResolveResult.Declined(cardId);
        }

        var target = new HeadlessEntityId(Segment(result.SelectedIds[0], 2));
        (bool success, bool complete) = await ApplyWithTarget(context, cardId, option, target).ConfigureAwait(false);
        if (!success)
        {
            ClearPendingOption(context, cardId);
            ClearFragmentRemaining(context, cardId);
            Mark(context, cardId, ReplacementDeclinedKey);
            return DeletionReplacementResolveResult.Declined(cardId);
        }

        // A multi-target replacement (Fragment) keeps its pending option until all targets are chosen, so
        // the next loop re-opens the target window; a single-target one finishes now.
        if (complete)
        {
            ClearPendingOption(context, cardId);
            ClearFragmentRemaining(context, cardId);
        }

        return DeletionReplacementResolveResult.Activated(cardId, option);
    }

    // --- (P6) sacrifice through the delete pipeline ---------------------------------------------------

    private async Task<(bool Success, bool Complete)> ApplySacrificeAsync(EngineContext context, HeadlessEntityId holderId, HeadlessEntityId allyId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(allyId, out CardInstanceRecord? ally) || ally is null ||
            // recursion guard: an ally that is itself mid-deletion / mid-sacrifice cannot be consumed.
            ReadFlag(ally.Metadata, GameFlowProcessor.PendingDeletionKey) ||
            ally.Metadata.ContainsKey(SacrificeAwaitingKey))
        {
            return (false, false);
        }

        var sink = new MatchStateMutationSink(
            context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
            context.EffectRegistry, context.GameEventQueue, context: context);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DeleteKind,
            holderId,   // the cause is the holder's own keyword effect (same owner -> own-effect deletion)
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = allyId.Value }));
        await sink.FlushAsync().ConfigureAwait(false);

        if (context.ZoneMover is IZoneStateReader zones &&
            zones.GetCards(AllyOwner(context, allyId), ChoiceZone.Trash).Contains(allyId))
        {
            // The sacrifice resolved immediately (no PRE replacement) — the holder survives (AS-IS successProcess).
            ClearDeletion(context, holderId);
            return (true, true);
        }

        if (context.CardInstanceRepository.TryGetInstance(allyId, out CardInstanceRecord? deferred) && deferred is not null &&
            ReadFlag(deferred.Metadata, GameFlowProcessor.PendingDeletionKey))
        {
            // The ally's own would-be-deleted window opened: park the holder until the ally's fate settles.
            Upsert(context, holderId, m => m[SacrificeAwaitingKey] = allyId.Value);
            return (true, true);
        }

        // Deletion prevented outright (cannotBeDeleted / continuous prevention) — the sacrifice failed.
        return (false, false);
    }

    /// <summary>(P6) settle sacrifice-awaiting holders: once the sacrificed ally's own window resolved, the
    /// holder is spared when the ally actually died, or resumes dying when the ally survived. Returns true
    /// when any holder settled (the loop keeps iterating).</summary>
    public static bool SettleAwaitingSacrifices(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        bool settled = false;
        foreach (CardInstanceRecord holder in context.CardInstanceRepository.Snapshot())
        {
            if (!holder.Metadata.TryGetValue(SacrificeAwaitingKey, out object? raw) || raw is not string allyValue)
            {
                continue;
            }

            var allyId = new HeadlessEntityId(allyValue);
            bool allyStillDeciding = context.CardInstanceRepository.TryGetInstance(allyId, out CardInstanceRecord? ally) && ally is not null &&
                ReadFlag(ally.Metadata, GameFlowProcessor.PendingDeletionKey);
            if (allyStillDeciding)
            {
                continue;   // wait for the ally's window / sweep
            }

            bool allyDied = ally is null || zones.GetCards(ally.OwnerId, ChoiceZone.Trash).Contains(allyId);
            Upsert(context, holder.InstanceId, m => m.Remove(SacrificeAwaitingKey));
            if (allyDied)
            {
                ClearDeletion(context, holder.InstanceId);   // spared (AS-IS successProcess)
            }
            else
            {
                // The ally saved itself — the sacrifice failed; the holder's own deletion proceeds.
                Mark(context, holder.InstanceId, ReplacementDeclinedKey);
            }

            settled = true;
        }

        return settled;
    }

    private static HeadlessPlayerId AllyOwner(EngineContext context, HeadlessEntityId allyId) =>
        context.CardInstanceRepository.TryGetInstance(allyId, out CardInstanceRecord? ally) && ally is not null
            ? ally.OwnerId
            : default;

    // --- Apply --------------------------------------------------------------

    private async Task<bool> ApplyNoTarget(EngineContext context, HeadlessEntityId cardId, string option)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return false;
        }

        switch (option)
        {
            case EvadeOption:
                if (!DeletionReplacementGate.TryEvade(context.CardInstanceRepository, record, context.EffectRegistry))
                {
                    return false;
                }

                ClearDeletion(context, cardId);
                return true;
            case BarrierOption:
                if (!await DeletionReplacementGate.TryBarrierAsync(context, record).ConfigureAwait(false))
                {
                    return false;
                }

                ClearDeletion(context, cardId);
                return true;
            case AscensionOption:
                return await DeletionReplacementGate
                    .TryAscensionAsync(context.CardInstanceRepository, context.ZoneMover, cardId, cancellationToken: default, effectRegistry: context.EffectRegistry).ConfigureAwait(false);
            case ArmorPurgeOption:
                // (B1) PRE replacement — the deletion is CANCELLED (AS-IS willBeRemoveField=false): only the
                // top card is trashed and the under-source is promoted; the permanent never leaves play.
                return await DeDigivolveHelpers.ArmorPurgeTopAsync(
                    context.CardInstanceRepository, context.ZoneMover, cardId, context.GameEventQueue).ConfigureAwait(false);
            default:
                return false;
        }
    }

    private async Task<(bool Success, bool Complete)> ApplyWithTarget(EngineContext context, HeadlessEntityId cardId, string option, HeadlessEntityId target)
    {
        switch (option)
        {
            case ScapegoatOption:
            case DecoyOption:
                // (P6) AS-IS resolves the sacrifice through the FULL delete pipeline (Scapegoat.cs:416
                // DeletePeremanentAndProcessAccordingToResult): the ally's own would-be-deleted replacements
                // (Evade/Barrier …) may fire, and the holder survives only when the sacrifice actually
                // resolved. Route through the sink's Delete mutation; when the ally's window defers, park
                // the holder as sacrifice-awaiting — the common loop settles it once the ally's fate is known.
                return await ApplySacrificeAsync(context, cardId, target).ConfigureAwait(false);
            case FragmentOption:
                return await ApplyFragmentSource(context, cardId, target).ConfigureAwait(false);
            case SaveOption:
                // POST: place the deleted card under the chosen permanent (AS-IS AddDigivolutionCardsBottom).
                await DigivolutionStackHelpers.AddSourcesBottomAsync(
                    context.CardInstanceRepository, context.ZoneMover, target, new[] { cardId }, ChoiceZone.Trash).ConfigureAwait(false);
                return (true, true);
            case DecodeOption:
                // POST: play the chosen digivolution source as a new permanent for free (AS-IS DecodeProcess).
                return (await DeletionReplacementGate.TryDecodePlaySourceAsync(
                    context.CardInstanceRepository, context.ZoneMover, cardId, target).ConfigureAwait(false), true);
            case PartitionOption:
                return await ApplyPartitionSource(context, cardId, target).ConfigureAwait(false);
            default:
                return (false, false);
        }
    }

    /// <summary>(C-14 Partition) Plays the chosen source as a new free permanent; after the second source the
    /// partition completes (marks <c>partitioned</c>). The play-for-free + detach is the shared Decode
    /// primitive; the repeated count reuses the Fragment "remaining" counter (initialised to 2).</summary>
    private async Task<(bool Success, bool Complete)> ApplyPartitionSource(EngineContext context, HeadlessEntityId cardId, HeadlessEntityId source)
    {
        if (!await DeletionReplacementGate.TryPartitionPlaySourceAsync(
                context.CardInstanceRepository, context.ZoneMover, cardId, source).ConfigureAwait(false))
        {
            return (false, false);
        }

        int remaining = ReadInt(GetMetadata(context, cardId), FragmentRemainingKey, 1) - 1;
        Upsert(context, cardId, m => m[FragmentRemainingKey] = remaining);
        if (remaining <= 0)
        {
            Mark(context, cardId, DeletionReplacementGate.PartitionedKey);
            return (true, true);
        }

        return (true, false);
    }

    private static IReadOnlyDictionary<string, object?> GetMetadata(EngineContext context, HeadlessEntityId cardId) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
            ? r.Metadata
            : new Dictionary<string, object?>(StringComparer.Ordinal);

    /// <summary>(C-11 Fragment) Trashes the chosen source and removes it from the holder's stack; survives
    /// once the required number of sources have been paid.</summary>
    private async Task<(bool Success, bool Complete)> ApplyFragmentSource(EngineContext context, HeadlessEntityId cardId, HeadlessEntityId source)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return (false, false);
        }

        List<string> sources = SourceIds(record.Metadata).Select(id => id.Value).ToList();
        if (!sources.Remove(source.Value))
        {
            return (false, false);
        }

        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(record.OwnerId, source, ChoiceZone.None, ChoiceZone.Trash, FaceUp: true)).ConfigureAwait(false);

        int remaining = ReadInt(record.Metadata, FragmentRemainingKey, 1) - 1;
        Upsert(context, cardId, m =>
        {
            if (sources.Count > 0)
            {
                m[DeletionReplacementGate.SourceIdsKey] = sources.ToArray();
            }
            else
            {
                m.Remove(DeletionReplacementGate.SourceIdsKey);
            }

            m[FragmentRemainingKey] = remaining;
        });

        if (remaining <= 0)
        {
            ClearDeletion(context, cardId);   // the top survives in a lower form
            return (true, true);
        }

        return (true, false);   // more sources still to pay -> re-open the target window
    }

    // --- Metadata helpers ---------------------------------------------------

    private static bool IsPendingDeletion(EngineContext context, HeadlessEntityId cardId) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null &&
        ReadFlag(r.Metadata, GameFlowProcessor.PendingDeletionKey);

    private static void ClearDeletion(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        metadata[GameFlowProcessor.PendingDeletionKey] = false;
        metadata.Remove(DeletionReplacementGate.DeletedByEffectKey);
        metadata.Remove(DecoyEligibleKey);
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    private static void SetPendingOption(EngineContext context, HeadlessEntityId cardId, string option) =>
        Upsert(context, cardId, m => m[PendingOptionKey] = option);

    private static void ClearPendingOption(EngineContext context, HeadlessEntityId cardId) =>
        Upsert(context, cardId, m => m.Remove(PendingOptionKey));

    private static void ClearFragmentRemaining(EngineContext context, HeadlessEntityId cardId) =>
        Upsert(context, cardId, m => m.Remove(FragmentRemainingKey));

    private static int FragmentCost(IReadOnlyDictionary<string, object?> metadata) =>
        Math.Max(1, ReadInt(metadata, DeletionReplacementGate.FragmentCostKey, 1));

    private static IReadOnlyList<HeadlessEntityId> SourceIds(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue(DeletionReplacementGate.SourceIdsKey, out object? raw) || raw is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        return raw switch
        {
            IEnumerable<HeadlessEntityId> ids => ids.ToArray(),
            IEnumerable<string> strings => strings.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => new HeadlessEntityId(v)).ToArray(),
            string text => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(v => new HeadlessEntityId(v)).ToArray(),
            _ => Array.Empty<HeadlessEntityId>()
        };
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> metadata, string key, int defaultValue)
    {
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return defaultValue;
        }

        return raw switch
        {
            int v => v,
            long v when v >= int.MinValue && v <= int.MaxValue => (int)v,
            string s when int.TryParse(s, out int p) => p,
            _ => defaultValue
        };
    }

    private static void Mark(EngineContext context, HeadlessEntityId cardId, string key) =>
        Upsert(context, cardId, m => m[key] = true);

    private static void Upsert(EngineContext context, HeadlessEntityId cardId, Action<Dictionary<string, object?>> mutate)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        mutate(metadata);
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    // --- Scan / parse -------------------------------------------------------

    private IReadOnlyList<HeadlessEntityId> ScanBattleArea(EngineContext context, Func<EngineContext, HeadlessEntityId, bool> predicate)
    {
        if (context.ZoneMover is not IZoneStateReader zones)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var matched = new List<HeadlessEntityId>();
        foreach (CardInstanceRecord record in context.CardInstanceRepository.Snapshot())
        {
            if (predicate(context, record.InstanceId) &&
                zones.GetCards(record.OwnerId, ChoiceZone.BattleArea).Contains(record.InstanceId))
            {
                matched.Add(record.InstanceId);
            }
        }

        HeadlessPlayerId turnPlayer = context.TurnController.Current.TurnPlayerId ?? default;
        if (!turnPlayer.IsEmpty)
        {
            matched.Sort((a, b) => Rank(context, a, turnPlayer).CompareTo(Rank(context, b, turnPlayer)));
        }

        return matched;
    }

    private static int Rank(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId turnPlayer) =>
        context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null && r.OwnerId == turnPlayer ? 0 : 1;

    private static HeadlessEntityId ParseCard(ChoiceRequest request)
    {
        ChoiceCandidate? first = request.SelectableCandidates.FirstOrDefault();
        return first is null ? default : new HeadlessEntityId(Segment(first.Id, 0));
    }

    /// <summary>Reads segment <paramref name="index"/> of a "card#option[#target]" candidate id.</summary>
    private static string Segment(HeadlessEntityId candidateId, int index)
    {
        string[] parts = candidateId.Value.Split(Delimiter);
        return index < parts.Length ? parts[index] : string.Empty;
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key) =>
        metadata.TryGetValue(key, out object? raw) && raw is bool value && value;

    /// <summary>Whether the (deferred) deletion was a battle deletion — gates by-battle-only options
    /// (Barrier). Set by BattleResolver via the deletedByBattle flag at defer time.</summary>
    private static bool ByBattle(CardInstanceRecord record) =>
        ReadFlag(record.Metadata, DeletionReplacementGate.DeletedByBattleKey);
}

public sealed record DeletionReplacementResolveResult(
    bool IsSuccess,
    HeadlessEntityId CardId,
    string Option,
    bool WasActivated,
    string FailureReason)
{
    public static DeletionReplacementResolveResult Activated(HeadlessEntityId cardId, string option) =>
        new(true, cardId, option, WasActivated: true, string.Empty);

    public static DeletionReplacementResolveResult Declined(HeadlessEntityId cardId) =>
        new(true, cardId, string.Empty, WasActivated: false, string.Empty);

    public static DeletionReplacementResolveResult Failure(string reason) =>
        new(false, default, string.Empty, WasActivated: false, reason ?? string.Empty);
}
