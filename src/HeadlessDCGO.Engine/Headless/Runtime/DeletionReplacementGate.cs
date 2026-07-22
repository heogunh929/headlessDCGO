namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (C-5 Barrier / C-7 Evade) Defense-keyword deletion REPLACEMENTS: when a Digimon would be deleted it
/// may pay a cost to survive instead, mirroring the AS-IS <c>WhenPermanentWouldBeDeleted</c> keyword
/// effects — <c>CardEffectCommons.EvadeProcess</c> (suspend self, then <c>willBeRemoveField = false</c>)
/// and <c>BarrierProcess</c> (trash the top security card, then <c>willBeRemoveField = false</c>). Both
/// deletion paths consult this before moving the card to the trash: the battle path
/// (<see cref="BattleResolver"/>) and the effect path (<see cref="Effects.MatchStateMutationSink"/>'s
/// Delete kind).
///
/// LIMITATION: like the auto-resolved optional end-attack triggers (see <see cref="AttackPipeline"/>),
/// these "you may" replacements are applied automatically whenever affordable rather than surfaced as an
/// agent decision. Surfacing the choice is deferred to the Phase-4 optional-trigger work.
/// </summary>
public static class DeletionReplacementGate
{
    // (C-Del 3c-1, 2026-07-15) The interactive promote-to-defer substrate (RD-3B-INTERACTIVE) is LANDED — the
    // effect-delete sink now finalizes an interactive AS-IS "would be deleted" cut-in through pendingDeletion +
    // the batch-unit sweep finalize (RD-C2-DEFERRED-DELETE-BATCH resolved), and EvadeProcess/BarrierProcess/
    // FragmentProcess/ArmorPurgeProcess own survival again (willBeRemoveField=false restored). But the FIRING-HALF
    // RETIREMENT of the four self-contained keywords (Evade/Barrier/Fragment/ArmorPurge) is a DIRECT STOP:
    //   * design item RD-3C1-BATTLE-PRE-WINDOW — all four fire on BATTLE deletions too (Barrier is battle-ONLY;
    //     Evade/Fragment/ArmorPurge fire on both), but the BATTLE deletion path (BattleResolver.ResolveRoundAsync
    //     / NeedsWindow → HasPreOption(byBattle:true)) has NO AS-IS PRE cut-in window — it parks via THIS gate.
    //     3b opened the PRE cut-in only in the effect-delete sink (BattleResolver never routes through the sink).
    //     Retiring these gate branches would leave a battle deletion with NO replacement path → firing loss (P0).
    //     Barrier, being battle-only, cannot fire through any window at all. The battle-path PRE cut-in transport +
    //     promote-to-defer (the BattleResolver analogue of 3b's sink work) must land first.
    //   * design item RD-3C1-MIXED-BATCH — even limiting retirement to the effect path, a single Destroy that
    //     mixes a retired keyword card with a NON-retired gate keyword card (e.g. [Evade, Scapegoat]) forces the
    //     sink's batch-level DeferAll=true (Scapegoat has a PreOption), so the PRE cut-in window is never opened
    //     and the retired keyword loses its firing. The window (retired) and gate (non-retired) do not compose in
    //     one batch; this dissolves only when the full PRE cluster is retired (3c-2/3c-3 remove the gate entirely).
    // Until both are cleared the gate's Evade/Barrier/Fragment/ArmorPurge firing-half STAYS live (unchanged).

    public const string HasEvadeKey = "hasEvade";
    public const string HasBarrierKey = "hasBarrier";
    public const string HasDecoyKey = "hasDecoy";
    public const string HasFortitudeKey = "hasFortitude";
    public const string HasArmorPurgeKey = "hasArmorPurge";
    public const string HasFragmentKey = "hasFragment";
    public const string HasAscensionKey = "hasAscension";
    public const string HasScapegoatKey = "hasScapegoat";
    public const string HasSaveKey = "hasSave";
    public const string HasDecodeKey = "hasDecode";
    public const string HasPartitionKey = "hasPartition";
    public const string IsSuspendedKey = "isSuspended";
    public const string CannotBeDeletedKey = "cannotBeDeleted";
    public const string SourceIdsKey = "sourceIds";
    // (P0-3/RD-4) Digivolution-source COUNT snapshotted at deletion time — AS-IS CanActivateFortitude reads
    // GetDigivolutionSourcesFromHashtable (the OnDeletion snapshot built BEFORE DiscardEvoRoots), NOT the live
    // stack. So Fortitude eligibility survives the unconditional source-trash: the sources are already in the
    // trash by replay time, exactly like AS-IS.
    public const string SourceCountAtDeletionKey = "sourceCountAtDeletion";
    public const string EnteredThisTurnKey = "enteredThisTurn";
    public const string DeletedByBattleKey = "deletedByBattle";
    public const string DeletedByEffectKey = "deletedByEffect";
    public const string DeletedByOwnEffectKey = "deletedByOwnEffect";
    public const string EvadedKey = "evaded";
    public const string BarrieredKey = "barriered";
    public const string FortitudeReplayedKey = "fortitudeReplayed";
    // (G-clean) DecoyRedirectKey / ArmorPurgedKey / FragmentedKey / AscendedKey / ScapegoatSacrificeKey /
    // SavedKey / DecodedKey / PartitionedKey deleted — write-marker vocabulary of the retired PRE/POST
    // firing-half (Try*Async), reference-0 after wave3. Presence/state markers above are retained.

    // (C-Del 3c-2b RETIRED, 2026-07-15) TryEvade / TryBarrierAsync — the invented PRE would-be-deleted Evade /
    // Barrier firing-half — are retired. AS-IS [Evade] / [Barrier] now fire through the PRE cut-in window
    // (WhenPermanentWouldBeDeleted → WhenRemoveField) opened by the effect-delete sink (3b), BattleResolver (3c-1b)
    // and SecurityResolver (3c-1c): the card's printed CardEffectFactory.EvadeEffect / BarrierEffect ActivateClass
    // (CardEffects) / granted GainEvade / GainBarrier bucket effect is collected by GetSkillInfos and resolved by
    // the window, whose EvadeProcess / BarrierProcess sets willBeRemoveField=false. Keeping this gate firing AND the
    // window would double-fire. Presence markers (HasEvadeKey / HasBarrierKey / ContinuousKeywordGate.Evade|Barrier)
    // are untouched — only the FIRING is retired. See keyword_rehoming_design_2026-07-15.md §5 / design item
    // RD-3C2B (3c-2b landing flip; RD-3C1-BATTLE-PRE-WINDOW + RD-3C1-MIXED-BATCH cleared by the battle/security PRE
    // transport + this whole-cluster gate retirement).

    // (C-Del 3c-2b RETIRED, 2026-07-15) FindDecoyRedirect / FindDecoyRedirectCandidates / HasDecoy /
    // ProtectedTargetIsDigimon — the invented Decoy by-enemy-effect redirect firing-half — are retired. AS-IS
    // [Decoy] now fires through the PRE cut-in window (WhenPermanentWouldBeDeleted): the printed / granted
    // DecoySelfEffect ActivateClass is collected by GetSkillInfos and resolved by the window (DecoyProcess deletes
    // the sacrificed Decoy, then willBeRemoveField=false on the protected Digimon). Keeping this gate AND the
    // window would double-fire. Presence markers (HasDecoyKey / ContinuousKeywordGate.Decoy) untouched.
    // See keyword_rehoming_design_2026-07-15.md §5 / design item RD-3C2B.

    // (C-Del 3a RETIRED, 2026-07-15) TryFortitudeReplayAsync — the invented POST-deletion Fortitude replay
    // firing-half — is retired. AS-IS [Fortitude] now fires through the OnDestroyedAnyone cut-in window
    // (collect-before-removal, sink StackSkillInfos + AutoProcessCheck): the card's printed
    // CardEffectFactory.FortitudeEffect ActivateClass (CardEffects) / granted CardEffectCommons.GainFortitude
    // bucket effect is collected by GetSkillInfos and resolved by the window, exactly like Raid/Alliance/Vortex.
    // Keeping this gate replay AND the window would double-fire. The Fortitude presence marker
    // (ContinuousKeywordGate.Fortitude / HasFortitudeKey) is untouched — only the FIRING is retired. See
    // keyword_rehoming_design_2026-07-15.md §F.3a / cdel_wave3_investigation_2026-07-15.md §F.

    // (B1) (C-21 Armor Purge) is a WOULD-BE-DELETED replacement (AS-IS ArmorPurgeProcess:
    // willBeRemoveField = false — the permanent never leaves play; only the top card is trashed and the
    // under-source promoted). Surfaced as a PRE option in DeletionReplacementTiming and applied by
    // DeDigivolveHelpers.ArmorPurgeTopAsync. The previous POST implementation here (full deletion, then
    // rebuild from the trash) wrongly fired OnDeletion for the survivor, opened stacked POST windows, and
    // never emitted WhenTopCardTrashed.


    // (C-Del 3c-2b RETIRED, 2026-07-15) FragmentCostOf / CanFragment / ApplyFragmentAsync / TryFragmentAsync — the
    // invented PRE would-be-deleted Fragment firing-half — are retired. AS-IS [Fragment] now fires through the PRE
    // cut-in window (WhenPermanentWouldBeDeleted → WhenRemoveField): the printed / granted FragmentSelfEffect
    // ActivateClass is collected by GetSkillInfos and resolved by the window, whose FragmentProcess trashes the
    // chosen N sources then willBeRemoveField=false (the top survives in a lower form). Keeping this gate AND the
    // window would double-fire. Presence markers (HasFragmentKey / ContinuousKeywordGate.Fragment) and the trashValue
    // vocabulary key above are untouched. See keyword_rehoming_design_2026-07-15.md §5 / design item RD-3C2B.

    // (C-Del 3c-3 RETIRED, 2026-07-16) TryAscensionAsync — the invented POST-deletion Ascension firing-half
    // (surfaced by DeletionReplacementTiming.AscensionOption) — is RETIRED. AS-IS [Ascension] now fires through
    // the AS-IS OnDestroyedAnyone cut-in window: the printed AscensionSelfEffect ActivateClass is collected by
    // GetSkillInfos and resolved by the window (AscensionProcess: the "Will you place this card in security?"
    // yes/no, then Trash->Security top face-down). The window's activation gate CanActivateAscension ->
    // CanActivateOnDeletion (OnDeletion.cs:141) reads CardSource.PermanentJustBeforeRemoveField, the per-match
    // service store that the universal sink / battle / security deletion paths now CHARGE (RD-P6C3-A3 resolved:
    // CardLeavePlayCleanup.RecordParametersJustBeforeRemoveField stamps it on the top card + every source, AS-IS
    // CardController.cs:3781 / mirror :3534). Keeping this gate AND the window would DOUBLE-FIRE (the gate's
    // presence scan reads the SAME live keyword state the window collects); charging the store and retiring the
    // gate is ONE atomic batch. Presence markers (HasAscensionKey / ContinuousKeywordGate.Ascension) untouched;
    // AscendedKey / AddToSecurity are now the window's job (AscensionProcess -> CardObjectController.AddSecurityCard).
    // See keyword_rehoming_design_2026-07-15.md §5 (3c-3) / design item RD-3A-01 (now resolved).

    // (C-Del 3c-2b RETIRED, 2026-07-15) FindScapegoatSacrifice / FindScapegoatSacrificeCandidates — the invented
    // PRE would-be-deleted Scapegoat firing-half — are retired. AS-IS [Scapegoat] now fires through the PRE cut-in
    // window (WhenPermanentWouldBeDeleted): the printed / granted ScapegoatSelfEffect ActivateClass is collected by
    // GetSkillInfos and resolved by the window (ScapegoatProcess deletes the chosen ally through the full delete
    // pipeline, then willBeRemoveField=false on the holder when the sacrifice resolved). Keeping this gate AND the
    // window would double-fire. Presence markers (HasScapegoatKey / ContinuousKeywordGate.Scapegoat) untouched.
    // See keyword_rehoming_design_2026-07-15.md §5 / design item RD-3C2B.

    // (M-4) Same seal as Decoy for the other deletion-replacement keywords: the metadata flag is only ever set
    // in tests, and there is no keyword->metadata bridge, so the live keyword grant (Fragment/Scapegoat/Save)
    // must be recognised here for the mechanism to fire in production.
    // (RC-5) The registry-only ContinuousKeywordGate.HasKeyword(EffectRegistry,…) overload is retired
    // (keyword-binding producer 0). The live keyword state is the EngineContext-aware AS-IS interface scan,
    // reached here WITHOUT threading a context parameter through every call site (out of this pass's touch
    // scope — DeletionReplacementTiming.cs is not a *Gate.cs file) via AmbientMatchContext.Current — the same
    // AsyncLocal handle NewModelContinuousScan's own methods self-scope from. A caller that already entered the
    // match's ambient scope (real gameplay, or a test that does so explicitly) is covered for free.
    internal static bool HasReplacementKeyword(CardInstanceRecord record, string metadataFlag, string keyword, EffectRegistry? effectRegistry) =>
        ReadFlag(record.Metadata, metadataFlag)
        || (AmbientMatchContext.Current is EngineContext ambient && ContinuousKeywordGate.HasKeyword(ambient, record.InstanceId, keyword));


    // (C-Del 3c-2b RETIRED, 2026-07-15) FindDecoyRedirectCandidates / FindScapegoatSacrificeCandidates — the
    // invented Decoy / Scapegoat sub-select enumerators — are retired with their firing-half above. The AS-IS
    // DecoyProcess / ScapegoatProcess "select 1" now runs inside the printed / granted ActivateClass resolved by
    // the PRE cut-in window. See keyword_rehoming_design_2026-07-15.md §5 / design item RD-3C2B.

    /// <summary>Marks a card deleted-by-effect and moves it to the trash — the shared sacrifice used by the
    /// Decoy/Scapegoat redirects.</summary>
    /// <summary>Returns false when the ally could not actually be destroyed — (C3) AS-IS resolves the
    /// sacrifice through DeletePermanent (Scapegoat.cs:416 / Decoy's CanBeDestroyedBySkill), and the holder
    /// is spared only on its success.</summary>
    public static async Task<bool> SacrificeAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? card) || card is null)
        {
            return false;
        }

        // (C3) an ally that cannot be destroyed is not consumed. Candidate filters exclude flagged allies
        // up front (CannotBeDeletedKey in Find*Candidates); this is the last-line guard.
        if (ReadFlag(card.Metadata, CannotBeDeletedKey))
        {
            return false;
        }

        repository.Upsert(card with
        {
            Metadata = new Dictionary<string, object?>(card.Metadata, StringComparer.Ordinal)
            {
                [DeletedByEffectKey] = true,
            }
        });
        await zoneMover.AddToTrashAsync(card.OwnerId, cardId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    // (C-Del 3c-2b RETIRED, 2026-07-15) TryDecodePlaySourceAsync / TryPartitionPlaySourceAsync /
    // PlaySourceForFreeAsync — the invented Decode / Partition play-a-source-for-free firing-half — are retired.
    // AS-IS [Decode] / [Partition] now fire through the PRE cut-in window (WhenPermanentWouldBeDeleted →
    // WhenRemoveField): the printed / granted DecodeSelfEffect / PartitionClass ActivateClass is collected by
    // GetSkillInfos and resolved by the window (DecodeProcess / PartitionClass.Partition play the source(s) via
    // PlayPermanentCards(payCost:false); the deletion is NOT cancelled — the card still leaves). Keeping this gate
    // AND the window would double-fire. Presence markers (HasDecodeKey / HasPartitionKey /
    // ContinuousKeywordGate.Decode|Partition) untouched. See keyword_rehoming_design_2026-07-15.md §5 / RD-3C2B.

    // (C-Del 3a RETIRED, 2026-07-15) TrySaveAsync — the invented POST-deletion Save firing-half (surfaced by
    // DeletionReplacementTiming's SaveOption two-step select) — is retired. AS-IS [Save] now fires through the
    // OnDestroyedAnyone cut-in window: the card's printed CardEffectFactory.SaveEffect ActivateClass (its
    // SelectPermanentEffect picks the Tamer to place under, isOptional=true) is collected + resolved by the
    // window. Keeping this gate AND the window would double-fire. The Save presence marker is untouched — only
    // the FIRING is retired. See keyword_rehoming_design_2026-07-15.md §F.3a.

    private static int SourceCount(IReadOnlyDictionary<string, object?> metadata) => ReadSourceIds(metadata).Count;

    /// <summary>(P0-3/RD-4) Source count for a POST-deletion replay/replacement gate: the deletion-time
    /// snapshot (<see cref="SourceCountAtDeletionKey"/>) if present — the sources may already be trashed by the
    /// unconditional DiscardEvoRoots mirror — else the live stack (paths that did not snapshot).</summary>
    public static int SourceCountAtDeletion(IReadOnlyDictionary<string, object?> metadata) =>
        metadata.TryGetValue(SourceCountAtDeletionKey, out object? raw) && raw is int snap ? snap : SourceCount(metadata);

    internal static IReadOnlyList<HeadlessEntityId> ReadSourceIds(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!metadata.TryGetValue(SourceIdsKey, out object? raw) || raw is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        return raw switch
        {
            IEnumerable<HeadlessEntityId> ids => ids.ToArray(),
            IEnumerable<string> strings => strings
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            string text => text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => new HeadlessEntityId(value))
                .ToArray(),
            _ => Array.Empty<HeadlessEntityId>()
        };
    }

    private static bool ReadFlag(IReadOnlyDictionary<string, object?> metadata, string key)
    {
        return metadata.TryGetValue(key, out object? raw) && raw is bool value && value;
    }
}
