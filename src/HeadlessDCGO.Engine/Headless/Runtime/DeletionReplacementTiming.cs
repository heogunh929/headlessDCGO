namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

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

    public const string EvadeOption = "evade";          // PRE, no sub
    public const string BarrierOption = "barrier";      // PRE, by-battle, no sub (trash top security)
    public const string ScapegoatOption = "scapegoat";  // PRE, sub = which ally to sacrifice
    public const string FragmentOption = "fragment";    // PRE, sub = which source(s) to trash (repeated)
    public const string AscensionOption = "ascension";  // POST, no sub
    public const string ArmorPurgeOption = "armorpurge"; // POST, no sub (trash top, promote under-source)
    public const string DecoyOption = "decoy";          // PRE, effect/enemy-gated, sub = which Decoy ally
    public const string DecoyEligibleKey = "decoyEligible";
    public const string SaveOption = "save";            // POST, sub = which permanent to place this under
    public const string DecodeOption = "decode";        // POST, effect-deletion only, sub = which source to play free
    public const string PartitionOption = "partition";  // POST, effect-deletion only, sub = play 2 sources free (repeated)

    private static bool NeedsTarget(string option) => option is ScapegoatOption or FragmentOption or DecoyOption or SaveOption or DecodeOption or PartitionOption;

    // --- PRE option set (shared with the sink's defer decision) --------------

    /// <summary>The optional PRE (would-be-deleted) replacements currently available on the card.</summary>
    public static IReadOnlyList<string> PreOptions(ICardInstanceRepository repository, IZoneStateReader zones, CardInstanceRecord record, bool byBattle)
    {
        var options = new List<string>();
        if (ReadFlag(record.Metadata, DeletionReplacementGate.HasEvadeKey) &&
            !ReadFlag(record.Metadata, DeletionReplacementGate.IsSuspendedKey))
        {
            options.Add(EvadeOption);
        }

        // Barrier is by-battle only (AS-IS IsByBattle): trash the top security card to survive.
        if (byBattle &&
            ReadFlag(record.Metadata, DeletionReplacementGate.HasBarrierKey) &&
            zones.GetCards(record.OwnerId, ChoiceZone.Security).Count >= 1)
        {
            options.Add(BarrierOption);
        }

        if (ReadFlag(record.Metadata, DeletionReplacementGate.HasScapegoatKey) &&
            DeletionReplacementGate.FindScapegoatSacrificeCandidates(repository, zones, record).Count > 0)
        {
            options.Add(ScapegoatOption);
        }

        if (ReadFlag(record.Metadata, DeletionReplacementGate.HasFragmentKey) &&
            SourceIds(record.Metadata).Count >= FragmentCost(record.Metadata))
        {
            options.Add(FragmentOption);
        }

        // Decoy: offered only when the deferring deletion marked it enemy-eligible AND a Decoy ally exists.
        if (ReadFlag(record.Metadata, DecoyEligibleKey) &&
            DeletionReplacementGate.FindDecoyRedirectCandidates(repository, zones, record).Count > 0)
        {
            options.Add(DecoyOption);
        }

        return options;
    }

    /// <summary>Whether a deletion of this card should be DEFERRED for an optional PRE replacement decision.</summary>
    public static bool HasPreOption(ICardInstanceRepository repository, IZoneStateReader zones, CardInstanceRecord record, bool byBattle) =>
        PreOptions(repository, zones, record, byBattle).Count > 0;

    /// <summary>(#3) Context-aware PRE options: identical to the static overload but the Scapegoat/Decoy
    /// candidate counts respect any card-specific condition (<see cref="IDeletionReplacementCandidateConditions"/>),
    /// so an option is surfaced only when a condition-passing target actually exists. The static overload
    /// (used by the sink's defer decision) stays generic — a safe superset, because the state-based sweep
    /// finishes a card whose only option turns out to have no legal target.</summary>
    private static IReadOnlyList<string> PreOptions(EngineContext context, IZoneStateReader zones, CardInstanceRecord record, bool byBattle)
    {
        var options = new List<string>();
        if (ReadFlag(record.Metadata, DeletionReplacementGate.HasEvadeKey) &&
            !ReadFlag(record.Metadata, DeletionReplacementGate.IsSuspendedKey))
        {
            options.Add(EvadeOption);
        }

        if (byBattle &&
            ReadFlag(record.Metadata, DeletionReplacementGate.HasBarrierKey) &&
            zones.GetCards(record.OwnerId, ChoiceZone.Security).Count >= 1)
        {
            options.Add(BarrierOption);
        }

        if (ReadFlag(record.Metadata, DeletionReplacementGate.HasScapegoatKey) &&
            DeletionReplacementGate.FindScapegoatSacrificeCandidates(
                context.CardInstanceRepository, zones, record, ResolveCondition(context, record, ScapegoatOption)).Count > 0)
        {
            options.Add(ScapegoatOption);
        }

        if (ReadFlag(record.Metadata, DeletionReplacementGate.HasFragmentKey) &&
            SourceIds(record.Metadata).Count >= FragmentCost(record.Metadata))
        {
            options.Add(FragmentOption);
        }

        if (ReadFlag(record.Metadata, DecoyEligibleKey) &&
            DeletionReplacementGate.FindDecoyRedirectCandidates(
                context.CardInstanceRepository, zones, record, ResolveCondition(context, record, DecoyOption)).Count > 0)
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
        if (ReadFlag(record.Metadata, DeletionReplacementGate.HasAscensionKey))
        {
            options.Add(AscensionOption);
        }

        if ((ReadFlag(record.Metadata, DeletionReplacementGate.HasArmorPurgeKey)
                || ContinuousKeywordGate.HasKeyword(context, record.InstanceId, ContinuousKeywordGate.ArmorPurge)) // GR-005 C-group seal
            && SourceIds(record.Metadata).Count >= 1)
        {
            options.Add(ArmorPurgeOption);
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
        if ((ReadFlag(record.Metadata, DeletionReplacementGate.HasPartitionKey)
                || ContinuousKeywordGate.HasKeyword(context, record.InstanceId, ContinuousKeywordGate.Partition)) && // GR-005 C-group seal
            !ReadFlag(record.Metadata, DeletionReplacementGate.DeletedByBattleKey) &&
            !ReadFlag(record.Metadata, DeletionReplacementGate.PartitionedKey) &&
            FindDecodeSourceCandidates(context, record, ResolveCondition(context, record, PartitionOption)).Count >= 2)
        {
            options.Add(PartitionOption);
        }

        return options;
    }

    private static bool HasSaveTarget(EngineContext context, IZoneStateReader zones, CardInstanceRecord record) =>
        ReadFlag(record.Metadata, DeletionReplacementGate.HasSaveKey) &&
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
                !string.Equals(definition.CardType, "Digimon", StringComparison.Ordinal))
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

        // The sub-selection is mandatory once the keyword is activated (AS-IS canNoSelect:false).
        OpenRequest(context, record, $"'{record.InstanceId.Value}' {option}: choose a target.", canSkip: false, candidates);
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
                context.CardInstanceRepository, zones, record, ResolveCondition(context, record, ScapegoatOption)),
            FragmentOption => SourceIds(record.Metadata),   // remaining digivolution sources to trash
            DecoyOption => DeletionReplacementGate.FindDecoyRedirectCandidates(
                context.CardInstanceRepository, zones, record, ResolveCondition(context, record, DecoyOption)),
            SaveOption => SaveTargets(context, zones, record, ResolveCondition(context, record, SaveOption)),
            DecodeOption => FindDecodeSourceCandidates(context, record, ResolveCondition(context, record, DecodeOption)),
            PartitionOption => FindDecodeSourceCandidates(context, record, ResolveCondition(context, record, PartitionOption)),
            _ => Array.Empty<HeadlessEntityId>(),
        };

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
                Upsert(context, cardId, m => m[FragmentRemainingKey] = FragmentCost(fr.Metadata));
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
                if (!DeletionReplacementGate.TryEvade(context.CardInstanceRepository, record))
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
                    .TryAscensionAsync(context.CardInstanceRepository, context.ZoneMover, cardId).ConfigureAwait(false);
            case ArmorPurgeOption:
                return await DeletionReplacementGate
                    .TryArmorPurgeAsync(context.CardInstanceRepository, context.ZoneMover, cardId, context.EffectRegistry).ConfigureAwait(false);
            default:
                return false;
        }
    }

    private async Task<(bool Success, bool Complete)> ApplyWithTarget(EngineContext context, HeadlessEntityId cardId, string option, HeadlessEntityId target)
    {
        switch (option)
        {
            case ScapegoatOption:
                await DeletionReplacementGate.SacrificeAsync(context.CardInstanceRepository, context.ZoneMover, target).ConfigureAwait(false);
                ClearDeletion(context, cardId);   // the holder survives
                return (true, true);
            case DecoyOption:
                // Sacrifice the chosen Decoy ally; the card being deleted survives.
                await DeletionReplacementGate.SacrificeAsync(context.CardInstanceRepository, context.ZoneMover, target).ConfigureAwait(false);
                ClearDeletion(context, cardId);
                return (true, true);
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
