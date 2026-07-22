namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
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
    public const string RequestIdPrefix = "deletion-replacement";
    public const char Delimiter = '#';

    // (C-Del 3c-3 RETIRED, 2026-07-16) AscensionOption — the last POST gate option — is retired. AS-IS [Ascension]
    // now fires through the AS-IS OnDestroyedAnyone cut-in window (printed AscensionSelfEffect ActivateClass ->
    // AscensionProcess), the CanActivateOnDeletion identity gate now satisfied because the deletion paths CHARGE the
    // PermanentJustBeforeRemoveField store (RD-P6C3-A3 resolved). Keeping the gate AND the window would double-fire.
    // (G-clean) With every POST keyword gone, the POST-choice scaffolding (PostOptions / PostResolvedKey / the
    // Priority-3 loop / the isPost branch) was physically deleted. See keyword_rehoming_design_2026-07-15.md §5.
    // (PRIM-P0-timing batch 4) PRE, no built-in sub — a card-registered WhenPermanentWouldBeDeleted effect.
    // Activating it runs the card's own effect body (which prevents/replaces via ClearDeletion). Any target/cost
    // sub-pick the effect needs is handled inside the effect's own resolution.
    public const string CustomWouldBeDeletedOption = "customwouldbedeleted";
    // (C-Del 3c-2b RETIRED) The 8 PRE keyword options (Evade/Barrier/ArmorPurge/Scapegoat/Fragment/Decode/
    // Partition/Decoy) + their two-step target machinery (PendingOptionKey/FragmentRemainingKey/NeedsTarget) +
    // the cross-card sacrifice keys (SacrificeAwaitingKey/DecoyEligibleKey) are removed — those keywords fire
    // through the AS-IS PRE cut-in window. Only CustomWouldBeDeletedOption (PRE) remains (3c-3 retired the last POST
    // option, Ascension), and it needs no two-step sub-target. See keyword_rehoming_design_2026-07-15.md §5 / RD-3C2B.

    // --- PRE option set (shared with the sink's defer decision) --------------

    /// <summary>The optional PRE (would-be-deleted) replacements currently available on the card.
    /// (C-Del 3c-2b) The 8 keyword replacements (Evade/Barrier/ArmorPurge/Scapegoat/Fragment/Decode/Partition/
    /// Decoy) are RETIRED from the gate — they fire through the AS-IS PRE cut-in window (sink/BattleResolver/
    /// SecurityResolver StackSkillInfos → GetSkillInfos → printed / granted ActivateClass). Only the retained
    /// generic bridge (CustomWouldBeDeletedOption: a card-registered WhenPermanentWouldBeDeleted effect in the
    /// invented EffectRegistry, disjoint from the window's EffectList collection) surfaces here.</summary>
    public static IReadOnlyList<string> PreOptions(ICardInstanceRepository repository, IZoneStateReader zones, CardInstanceRecord record, bool byBattle)
    {
        var options = new List<string>();

        // (RC-4) the registry CustomWouldBeDeleted PRE bridge is retired — producer 0 (no card ever
        // registers a WhenPermanentWouldBeDeleted binding; the AS-IS cut-in window collects printed/granted
        // ActivateClass effects instead). PreOptions is empty pending the registry type deletion.
        return options;
    }

    /// <summary>Whether a deletion of this card should be DEFERRED for an optional PRE replacement decision.</summary>
    public static bool HasPreOption(ICardInstanceRepository repository, IZoneStateReader zones, CardInstanceRecord record, bool byBattle) =>
        PreOptions(repository, zones, record, byBattle).Count > 0;

    /// <summary>(#3) Context-aware PRE options. (C-Del 3c-2b) With the 8 keyword replacements retired to the
    /// AS-IS PRE cut-in window, only the retained generic bridge (CustomWouldBeDeletedOption, a card-registered
    /// WhenPermanentWouldBeDeleted effect) surfaces — the same set as the static overload.</summary>
    private static IReadOnlyList<string> PreOptions(EngineContext context, IZoneStateReader zones, CardInstanceRecord record, bool byBattle)
    {
        var options = new List<string>();

        // (RC-4) registry PRE bridge retired — see static overload.
        return options;
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
            PreOptions(context, zones, record, ByBattle(record)).Count > 0;
    }

    // (C-Del 3c-2b RETIRED) The Decode/Partition source-candidate helpers (FindDecodeSourceCandidates /
    // DecodeSourceConditionOf) and the Save target helper are retired with the two-step machinery — those
    // keywords now play/place their source(s) inside the printed / granted ActivateClass resolved by the AS-IS
    // window. See keyword_rehoming_design_2026-07-15.md §5 / RD-3C2B.

    // --- Window open --------------------------------------------------------

    public bool RequestChoice(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.ChoiceController.Current.IsPending || context.ZoneMover is not IZoneStateReader zones)
        {
            return false;
        }

        // (C-Del 3c-2b) The former Priority 1 (two-step sub-target select for Scapegoat/Fragment/Decoy/Decode/
        // Partition) is retired — the only surviving option (CustomWouldBeDeleted PRE; Ascension POST retired in
        // 3c-3) needs no sub-target, so surfacing is a single keyword step.

        // Priority 2: PRE step-1 (would-be-deleted) choices.
        foreach (HeadlessEntityId cardId in ScanBattleArea(context, IsPreStep1Awaiting))
        {
            CardInstanceRecord record = context.CardInstanceRepository.TryGetInstance(cardId, out var r) && r is not null ? r : null!;
            return OpenKeywordChoice(context, record, PreOptions(context, zones, record, ByBattle(record)), "would be deleted");
        }

        // (G-clean) The former Priority 3 (POST on-deletion) loop is deleted — every POST keyword (Save/Decode/
        // Partition/Ascension) was retired or moved to the PRE window in wave3, so PostOptions surfaced nothing.
        // The PRE CustomWouldBeDeleted bridge above is the only surviving window this gate opens.
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

    private static ChoiceCandidate Candidate(CardInstanceRecord record, string id, string label) =>
        new(new HeadlessEntityId(id), label, ChoiceZone.BattleArea, IsSelectable: true, ownerId: record.OwnerId);

    private static void OpenRequest(EngineContext context, CardInstanceRecord record, string message, bool canSkip, IReadOnlyList<ChoiceCandidate> candidates)
    {
        var request = new ChoiceRequest(
            ChoiceType.DeletionReplacement, record.OwnerId, message,
            minCount: canSkip ? 0 : 1, maxCount: 1, canSkip, ChoiceZone.BattleArea, candidates);
        context.ChoiceController.RequestChoice(request, new HeadlessEntityId($"{RequestIdPrefix}:{record.InstanceId.Value}"));
    }

    // (C-Del 3c-2b RETIRED) GetTargets + the whole Partition colour-group machinery (PartitionConditionsOf /
    // PartitionActivatable / PartitionPickCandidates / FindPartitionGroupCandidates / MatchesPartitionCondition)
    // are retired with the two-step sub-target select — the AS-IS PartitionClass.Partition performs the per-colour
    // source picks inside the printed / granted ActivateClass resolved by the window. See RD-3C2B.

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

        try
        {
            context.ChoiceController.ResolveChoice(result);
        }
        catch (InvalidOperationException ex)
        {
            return DeletionReplacementResolveResult.Failure(ex.Message);
        }

        // (C-Del 3c-2b) The former two-step (isStep2 → ResolveTargetStep) is retired — the surviving option
        // (CustomWouldBeDeleted PRE; Ascension POST retired in 3c-3) resolves in a single keyword step with no sub-target.
        return await ResolveKeywordStep(context, cardId, result).ConfigureAwait(false);
    }

    private async Task<DeletionReplacementResolveResult> ResolveKeywordStep(EngineContext context, HeadlessEntityId cardId, ChoiceResult result)
    {
        // (G-clean) The isPost branch is deleted: the only surviving option (CustomWouldBeDeleted PRE) is always
        // pending-deletion, so isPost was always false. A declined/failed replacement marks ReplacementDeclinedKey.
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            Mark(context, cardId, ReplacementDeclinedKey);
            return DeletionReplacementResolveResult.Declined(cardId);
        }

        string option = Segment(result.SelectedIds[0], 1);
        // (C-Del 3c-2b) No surviving option needs a two-step sub-target — apply directly.
        if (!await ApplyNoTarget(context, cardId, option).ConfigureAwait(false))
        {
            Mark(context, cardId, ReplacementDeclinedKey);
            return DeletionReplacementResolveResult.Declined(cardId);
        }

        return DeletionReplacementResolveResult.Activated(cardId, option);
    }

    // (C-Del 3c-2b RETIRED) ResolveTargetStep + the cross-card sacrifice pipeline (ApplySacrificeAsync /
    // SettleAwaitingSacrifices / AllyOwner / SacrificeAwaitingKey) are retired with the two-step machinery. AS-IS
    // Scapegoat/Decoy resolve their sacrifice through the printed / granted ActivateClass's own DeletePermanent
    // (which opens the sacrificed ally's OWN PRE cut-in window), inside the AS-IS deletion window — no invented
    // holder-park is needed. See keyword_rehoming_design_2026-07-15.md §5 / RD-3C2B.

    // --- Apply --------------------------------------------------------------

    private async Task<bool> ApplyNoTarget(EngineContext context, HeadlessEntityId cardId, string option)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return false;
        }

        switch (option)
        {
            // (C-Del 3c-2b/3c-3 RETIRED) Evade / Barrier / ArmorPurge (PRE) and Ascension (POST) cases removed —
            // those keywords fire through the AS-IS cut-in window (PRE: printed / granted ActivateClass sets
            // willBeRemoveField=false / trashes the top; POST Ascension: printed AscensionSelfEffect -> AscensionProcess,
            // the store-charge (RD-P6C3-A3) now satisfies CanActivateOnDeletion). Only the PRE CustomWouldBeDeleted
            // bridge remains.
            // (RC-4) CustomWouldBeDeletedOption case retired with the registry PRE bridge (never surfaced).
            default:
                return false;
        }
    }

    // (C-Del 3c-2b RETIRED) ApplyWithTarget + ApplyPartitionSource + ApplyFragmentSource + GetMetadata are retired
    // with the two-step sub-target machinery — Scapegoat/Decoy/Fragment/Decode/Partition apply their sacrifice /
    // source-trash / play-for-free inside the printed / granted ActivateClass resolved by the AS-IS PRE cut-in
    // window. See keyword_rehoming_design_2026-07-15.md §5 / RD-3C2B.

    // --- Metadata helpers ---------------------------------------------------

    private static void ClearDeletion(EngineContext context, HeadlessEntityId cardId)
    {
        if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        {
            return;
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        metadata[GameFlowProcessor.PendingDeletionKey] = false;
        metadata.Remove(DeletionReplacementGate.DeletedByEffectKey);
        context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
    }

    // (C-Del 3c-2b RETIRED) SetPendingOption / ClearPendingOption / ClearFragmentRemaining / FragmentCost /
    // SourceIds / ReadInt are retired with the two-step machinery (no surviving option needs a pending sub-option
    // or a source count). See keyword_rehoming_design_2026-07-15.md §5 / RD-3C2B.

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
