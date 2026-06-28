namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
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
    public const string HasEvadeKey = "hasEvade";
    public const string HasBarrierKey = "hasBarrier";
    public const string HasDecoyKey = "hasDecoy";
    public const string HasFortitudeKey = "hasFortitude";
    public const string HasArmorPurgeKey = "hasArmorPurge";
    public const string HasFragmentKey = "hasFragment";
    public const string FragmentCostKey = "fragmentCost";
    public const string HasAscensionKey = "hasAscension";
    public const string HasScapegoatKey = "hasScapegoat";
    public const string HasSaveKey = "hasSave";
    public const string HasDecodeKey = "hasDecode";
    public const string HasPartitionKey = "hasPartition";
    public const string IsSuspendedKey = "isSuspended";
    public const string CannotBeDeletedKey = "cannotBeDeleted";
    public const string SourceIdsKey = "sourceIds";
    public const string EnteredThisTurnKey = "enteredThisTurn";
    public const string DeletedByBattleKey = "deletedByBattle";
    public const string DeletedByEffectKey = "deletedByEffect";
    public const string EvadedKey = "evaded";
    public const string BarrieredKey = "barriered";
    public const string DecoyRedirectKey = "decoyRedirect";
    public const string FortitudeReplayedKey = "fortitudeReplayed";
    public const string ArmorPurgedKey = "armorPurged";
    public const string FragmentedKey = "fragmented";
    public const string AscendedKey = "ascended";
    public const string ScapegoatSacrificeKey = "scapegoatSacrifice";
    public const string SavedKey = "saved";
    public const string DecodedKey = "decoded";
    public const string PartitionedKey = "partitioned";

    /// <summary>
    /// (C-7 Evade) An unsuspended Digimon that would leave the battle area suspends itself to survive.
    /// Mirrors <c>EvadeProcess</c> (SuspendPermanentsClass.Tap + willBeRemoveField = false). The suspend
    /// IS the cost, so an already-suspended Digimon cannot evade (AS-IS
    /// <c>CanActivatePermanentSuspendCostEffect</c>). Applies to both battle and effect deletion —
    /// <c>CanTriggerEvade</c> has no by-battle filter. Returns true when the deletion is replaced.
    /// </summary>
    public static bool TryEvade(ICardInstanceRepository repository, CardInstanceRecord record)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(record);

        if (!ReadFlag(record.Metadata, HasEvadeKey) || ReadFlag(record.Metadata, IsSuspendedKey))
        {
            return false;
        }

        repository.Upsert(record with
        {
            Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
            {
                [IsSuspendedKey] = true,
                [EvadedKey] = true,
            }
        });
        return true;
    }

    /// <summary>
    /// (C-5 Barrier) A Digimon that would be deleted trashes the top card of its owner's security stack
    /// to survive. Mirrors <c>BarrierProcess</c> (IDestroySecurity fromTop:1 + willBeRemoveField = false),
    /// gated by <c>CanActivateBarrier</c> (owner has >= 1 security). The AS-IS trigger is battle-only
    /// (<c>IsByBattle</c>), so only the battle path calls this. Returns true when the deletion is replaced.
    /// </summary>
    public static async Task<bool> TryBarrierAsync(
        EngineContext context,
        CardInstanceRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(record);

        if (!ReadFlag(record.Metadata, HasBarrierKey) ||
            context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> security = zoneReader.GetCards(record.OwnerId, ChoiceZone.Security);
        if (security.Count < 1)
        {
            return false;
        }

        // Trash the top security card (security[0] — the same end SecurityResolver reveals first), face up.
        await context.ZoneMover.MoveAsync(
            new ZoneMoveRequest(record.OwnerId, security[0], ChoiceZone.Security, ChoiceZone.Trash, FaceUp: true),
            cancellationToken).ConfigureAwait(false);

        context.CardInstanceRepository.Upsert(record with
        {
            Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
            {
                [BarrieredKey] = true,
            }
        });
        return true;
    }

    /// <summary>
    /// (C-4 Decoy) When <paramref name="target"/> (a battle-area Digimon) would be deleted by an
    /// ENEMY-owned effect, its controller may sacrifice one of its OTHER battle-area Decoy Digimon to
    /// prevent that deletion. Returns the Decoy to sacrifice, or null when no redirect applies. Mirrors
    /// AS-IS <c>DecoyProcess</c> (delete the Decoy, then <c>willBeRemoveField = false</c> on the protected
    /// Digimon) gated by <c>CanActivateDecoy</c> (the Decoy can be deleted) and the by-enemy-effect trigger
    /// (<c>cardEffect.EffectSourceCard.Owner == card.Owner.Enemy</c>). Effect-deletion only — the AS-IS
    /// trigger is <c>IsByEffect</c>, so battle deletion does not redirect.
    ///
    /// LIMITATION: picks the first eligible Decoy deterministically rather than surfacing the AS-IS
    /// "select 1" choice (consistent with the auto-resolved replacements above).
    /// </summary>
    public static HeadlessEntityId? FindDecoyRedirect(
        ICardInstanceRepository repository,
        IZoneStateReader zones,
        CardInstanceRecord target,
        HeadlessEntityId deleterId,
        Func<CardInstanceRecord, bool>? candidateCondition = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(target);

        // By enemy effect: the deleter must resolve to an instance owned by the target's opponent.
        if (deleterId.IsEmpty ||
            !repository.TryGetInstance(deleterId, out CardInstanceRecord? deleter) ||
            deleter is null ||
            deleter.OwnerId == target.OwnerId)
        {
            return null;
        }

        IReadOnlyList<HeadlessEntityId> battleArea = zones.GetCards(target.OwnerId, ChoiceZone.BattleArea);
        if (!battleArea.Contains(target.InstanceId))
        {
            return null;
        }

        foreach (HeadlessEntityId candidateId in battleArea)
        {
            if (candidateId == target.InstanceId ||
                !repository.TryGetInstance(candidateId, out CardInstanceRecord? decoy) ||
                decoy is null ||
                !ReadFlag(decoy.Metadata, HasDecoyKey) ||
                ReadFlag(decoy.Metadata, CannotBeDeletedKey) ||
                (candidateCondition is not null && !candidateCondition(decoy)))
            {
                continue;
            }

            return candidateId;
        }

        return null;
    }

    /// <summary>
    /// (C-6 Fortitude) AFTER a Digimon that had at least one digivolution source is deleted, it is played
    /// back from the trash as a new permanent for free. Mirrors AS-IS <c>FortitudeProcess</c>
    /// (PlayPermanentCards from Trash, payCost:false, activateETB:true) gated by <c>CanActivateFortitude</c>
    /// (in trash + the deleted stack had >= 1 source). Unlike Evade/Barrier this is a post-deletion replay
    /// (OnDestroyed), not a would-be-deleted prevention — the card IS deleted, then returns. Both deletion
    /// paths call this once the card has reached the trash. The replayed Digimon enters anew (summoning
    /// sick, no sources), so its <c>sourceIds</c> and deletion markers are cleared.
    /// </summary>
    public static async Task<bool> TryFortitudeReplayAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? record) ||
            record is null ||
            !ReadFlag(record.Metadata, HasFortitudeKey) ||
            SourceCount(record.Metadata) < 1)
        {
            return false;
        }

        // The card must have reached the trash (it was just deleted) before it can be replayed from there.
        if (zoneMover is IZoneStateReader zones &&
            !zones.GetCards(record.OwnerId, ChoiceZone.Trash).Contains(cardId))
        {
            return false;
        }

        await zoneMover.MoveAsync(
            new ZoneMoveRequest(record.OwnerId, cardId, ChoiceZone.Trash, ChoiceZone.BattleArea, FaceUp: true),
            cancellationToken).ConfigureAwait(false);

        // Re-enters as a fresh permanent: clear the digivolution stack + deletion markers, mark summoning
        // sickness, and stamp the replay marker.
        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        metadata.Remove(SourceIdsKey);
        metadata.Remove(DeletedByBattleKey);
        metadata.Remove(DeletedByEffectKey);
        metadata[EnteredThisTurnKey] = true;
        metadata[FortitudeReplayedKey] = true;
        repository.Upsert(record with { Metadata = metadata });
        return true;
    }

    /// <summary>
    /// (C-21 Armor Purge) AFTER a Digimon with at least one digivolution source would be deleted, it sheds
    /// its TOP card to the trash and the immediate under-source becomes the new top, so the permanent
    /// survives in a lower form. Mirrors AS-IS <c>ArmorPurgeClass.ArmorPurge</c> (trash the top card,
    /// promote the next, <c>willBeRemoveField = false</c>) gated by <c>CanActivateArmorPurge</c>
    /// (DigivolutionCards.Count >= 1). Like Fortitude this runs once the purged top has reached the trash;
    /// the promoted source is pulled back onto the battle area with the remaining stack beneath it.
    /// Effect + battle deletion (AS-IS <c>CanTriggerWhenRemoveField</c>).
    /// </summary>
    public static async Task<bool> TryArmorPurgeAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? record) ||
            record is null ||
            !ReadFlag(record.Metadata, HasArmorPurgeKey))
        {
            return false;
        }

        IReadOnlyList<HeadlessEntityId> sources = ReadSourceIds(record.Metadata);
        if (sources.Count < 1)
        {
            return false;
        }

        // The purged top must have reached the trash (it was just deleted) before its source is promoted.
        if (zoneMover is IZoneStateReader zones &&
            !zones.GetCards(record.OwnerId, ChoiceZone.Trash).Contains(cardId))
        {
            return false;
        }

        HeadlessEntityId promotedId = sources[0];
        if (!repository.TryGetInstance(promotedId, out CardInstanceRecord? promoted) || promoted is null)
        {
            return false;
        }

        // The immediate under-source becomes the new top of the surviving permanent.
        await zoneMover.MoveAsync(
            new ZoneMoveRequest(record.OwnerId, promotedId, ChoiceZone.None, ChoiceZone.BattleArea, FaceUp: true),
            cancellationToken).ConfigureAwait(false);

        var metadata = new Dictionary<string, object?>(promoted.Metadata, StringComparer.Ordinal);
        string[] remaining = sources.Skip(1).Select(id => id.Value).ToArray();
        if (remaining.Length > 0)
        {
            metadata[SourceIdsKey] = remaining;
        }
        else
        {
            metadata.Remove(SourceIdsKey);
        }

        // The permanent persists: carry its tap state onto the new top, clear deletion markers, mark purge.
        metadata[IsSuspendedKey] = ReadFlag(record.Metadata, IsSuspendedKey);
        metadata.Remove(DeletedByBattleKey);
        metadata.Remove(DeletedByEffectKey);
        metadata[ArmorPurgedKey] = true;
        repository.Upsert(promoted with { Metadata = metadata });
        return true;
    }

    /// <summary>
    /// (C-11 Fragment) A would-be-deleted Digimon trashes <c>fragmentCost</c> (default 1) of its
    /// digivolution sources to survive instead (AS-IS FragmentProcess: trash N sources, then
    /// <c>willBeRemoveField = false</c>) gated by <c>CanActivateFragment</c> (DigivolutionCards.Count &gt;=
    /// N). The TOP card stays; only the under-sources are paid. Like Evade/Barrier this is a skip-deletion
    /// replacement consulted in both deletion paths.
    /// LIMITATION: auto-pays the DEEPEST sources rather than surfacing the AS-IS "select N" choice.
    /// </summary>
    public static bool CanFragment(CardInstanceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        int cost = Math.Max(1, ReadInt(record.Metadata, FragmentCostKey, 1));
        return ReadFlag(record.Metadata, HasFragmentKey) && SourceCount(record.Metadata) >= cost;
    }

    public static async Task ApplyFragmentAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null || !CanFragment(record))
        {
            return;
        }

        int cost = Math.Max(1, ReadInt(record.Metadata, FragmentCostKey, 1));
        IReadOnlyList<HeadlessEntityId> sources = ReadSourceIds(record.Metadata);
        // Auto-pick the deepest `cost` sources to trash (the recent evolution line stays underneath).
        foreach (HeadlessEntityId source in sources.Skip(sources.Count - cost))
        {
            await zoneMover.MoveAsync(
                new ZoneMoveRequest(record.OwnerId, source, ChoiceZone.None, ChoiceZone.Trash, FaceUp: true),
                cancellationToken).ConfigureAwait(false);
        }

        var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
        string[] remaining = sources.Take(sources.Count - cost).Select(id => id.Value).ToArray();
        if (remaining.Length > 0)
        {
            metadata[SourceIdsKey] = remaining;
        }
        else
        {
            metadata.Remove(SourceIdsKey);
        }

        metadata[FragmentedKey] = true;
        repository.Upsert(record with { Metadata = metadata });
    }

    public static async Task<bool> TryFragmentAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        CardInstanceRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!CanFragment(record))
        {
            return false;
        }

        await ApplyFragmentAsync(repository, zoneMover, record.InstanceId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// (C-17 Ascension) AFTER a Digimon is deleted, its controller may place the deleted card into the
    /// security stack (AS-IS AscensionProcess: AddSecurityCard on deletion). Post-deletion response like
    /// Fortitude; both deletion paths call it once the card has reached the trash.
    /// </summary>
    public static async Task<bool> TryAscensionAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? record) ||
            record is null ||
            !ReadFlag(record.Metadata, HasAscensionKey))
        {
            return false;
        }

        if (zoneMover is IZoneStateReader zones &&
            !zones.GetCards(record.OwnerId, ChoiceZone.Trash).Contains(cardId))
        {
            return false;
        }

        await zoneMover.MoveAsync(
            new ZoneMoveRequest(record.OwnerId, cardId, ChoiceZone.Trash, ChoiceZone.Security),
            cancellationToken).ConfigureAwait(false);

        repository.Upsert(record with
        {
            Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
            {
                [AscendedKey] = true,
            }
        });
        return true;
    }

    /// <summary>
    /// (C-19 Scapegoat) When <paramref name="holder"/> (a battle-area Digimon) would be deleted, its
    /// controller may delete one of its OTHER battle-area Digimon instead, so the holder survives (AS-IS
    /// ScapegoatProcess: delete a matching permanent, then <c>willBeRemoveField = false</c> on this one).
    /// The inverse of Decoy. Returns the ally to sacrifice, or null. The caller trashes the ally and skips
    /// the holder's deletion.
    /// LIMITATION: picks the first eligible ally rather than surfacing the AS-IS "select 1" choice.
    /// </summary>
    public static HeadlessEntityId? FindScapegoatSacrifice(
        ICardInstanceRepository repository,
        IZoneStateReader zones,
        CardInstanceRecord holder,
        Func<CardInstanceRecord, bool>? candidateCondition = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(holder);

        if (!ReadFlag(holder.Metadata, HasScapegoatKey))
        {
            return null;
        }

        IReadOnlyList<HeadlessEntityId> battleArea = zones.GetCards(holder.OwnerId, ChoiceZone.BattleArea);
        if (!battleArea.Contains(holder.InstanceId))
        {
            return null;
        }

        foreach (HeadlessEntityId candidateId in battleArea)
        {
            if (candidateId == holder.InstanceId ||
                !repository.TryGetInstance(candidateId, out CardInstanceRecord? ally) ||
                ally is null ||
                ReadFlag(ally.Metadata, CannotBeDeletedKey) ||
                (candidateCondition is not null && !candidateCondition(ally)))
            {
                continue;
            }

            return candidateId;
        }

        return null;
    }

    /// <summary>(C-4 Decoy) All of the owner's Decoy allies that could be sacrificed to spare the target —
    /// the agent picks one (F-6.8 sub-selection). The by-enemy-effect gating is checked at defer time
    /// (the deleter is known then) and recorded as a marker; this only enumerates the eligible decoys.</summary>
    public static IReadOnlyList<HeadlessEntityId> FindDecoyRedirectCandidates(
        ICardInstanceRepository repository,
        IZoneStateReader zones,
        CardInstanceRecord target,
        Func<CardInstanceRecord, bool>? candidateCondition = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(target);

        IReadOnlyList<HeadlessEntityId> battleArea = zones.GetCards(target.OwnerId, ChoiceZone.BattleArea);
        if (!battleArea.Contains(target.InstanceId))
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var candidates = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId candidateId in battleArea)
        {
            if (candidateId != target.InstanceId &&
                repository.TryGetInstance(candidateId, out CardInstanceRecord? decoy) && decoy is not null &&
                ReadFlag(decoy.Metadata, HasDecoyKey) &&
                !ReadFlag(decoy.Metadata, CannotBeDeletedKey) &&
                (candidateCondition is null || candidateCondition(decoy)))
            {
                candidates.Add(candidateId);
            }
        }

        return candidates;
    }

    /// <summary>(C-19 Scapegoat) All allies eligible to be sacrificed for the holder — the agent picks one
    /// (F-6.8 sub-selection), rather than the engine auto-choosing the first.</summary>
    public static IReadOnlyList<HeadlessEntityId> FindScapegoatSacrificeCandidates(
        ICardInstanceRepository repository,
        IZoneStateReader zones,
        CardInstanceRecord holder,
        Func<CardInstanceRecord, bool>? candidateCondition = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(holder);

        if (!ReadFlag(holder.Metadata, HasScapegoatKey))
        {
            return Array.Empty<HeadlessEntityId>();
        }

        IReadOnlyList<HeadlessEntityId> battleArea = zones.GetCards(holder.OwnerId, ChoiceZone.BattleArea);
        if (!battleArea.Contains(holder.InstanceId))
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var candidates = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId candidateId in battleArea)
        {
            if (candidateId != holder.InstanceId &&
                repository.TryGetInstance(candidateId, out CardInstanceRecord? ally) && ally is not null &&
                !ReadFlag(ally.Metadata, CannotBeDeletedKey) &&
                (candidateCondition is null || candidateCondition(ally)))
            {
                candidates.Add(candidateId);
            }
        }

        return candidates;
    }

    /// <summary>Marks a card deleted-by-effect and moves it to the trash — the shared sacrifice used by the
    /// Decoy/Scapegoat redirects.</summary>
    public static async Task SacrificeAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? card) || card is null)
        {
            return;
        }

        repository.Upsert(card with
        {
            Metadata = new Dictionary<string, object?>(card.Metadata, StringComparer.Ordinal)
            {
                [DeletedByEffectKey] = true,
            }
        });
        await zoneMover.AddToTrashAsync(card.OwnerId, cardId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// (C-13 Decode) AFTER a Digimon left the field by an effect (not battle), its controller may play one
    /// of its digivolution sources as a new permanent for free. Mirrors AS-IS <c>DecodeProcess</c>
    /// (select 1 matching source from the leaving card's stack → <c>PlayPermanentCards(payCost:false,
    /// activateETB:true)</c>) gated by <c>CanActivateDecode</c>. The sources remain in <c>ChoiceZone.None</c>
    /// referenced by the trashed card's <c>sourceIds</c> (same as ArmorPurge/Save), so this runs once the
    /// card is in the trash. The chosen source enters anew (summoning sick, no stack of its own); it is
    /// detached from the dead card's <c>sourceIds</c> and the dead card is marked <c>decoded</c> so the
    /// window does not re-offer. The source's Digimon/colour eligibility is enforced by the caller's
    /// candidate filter (the free play needs no cost gate).
    /// </summary>
    public static Task<bool> TryDecodePlaySourceAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId deadCardId,
        HeadlessEntityId chosenSourceId,
        CancellationToken cancellationToken = default)
    {
        // Decode marks the dead card 'decoded' so its single-use POST window does not re-offer.
        return PlaySourceForFreeAsync(repository, zoneMover, deadCardId, chosenSourceId, DecodedKey, cancellationToken);
    }

    /// <summary>
    /// (C-14 Partition) AFTER a Digimon left the field by an effect (not battle, >= 2 sources), its
    /// controller plays two of its digivolution sources as new permanents for free. Mirrors AS-IS
    /// <c>PartitionClass.Partition</c> (select one source per colour group → PlayPermanentCards payCost:false).
    /// Shares the Decode play-for-free primitive; the two picks are driven as a repeated single-select by
    /// <see cref="DeletionReplacementTiming"/> (the 'partitioned' completion marker is stamped there).
    /// </summary>
    public static Task<bool> TryPartitionPlaySourceAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId deadCardId,
        HeadlessEntityId chosenSourceId,
        CancellationToken cancellationToken = default)
    {
        return PlaySourceForFreeAsync(repository, zoneMover, deadCardId, chosenSourceId, markKey: null, cancellationToken);
    }

    /// <summary>Plays one of a (trashed) card's digivolution sources as a fresh free permanent: moves it
    /// <see cref="ChoiceZone.None"/> → battle area (emitting the OnEnterField ETB), resets it to a fresh
    /// permanent (summoning sick, no stack/deletion markers), detaches it from the dead card's
    /// <c>sourceIds</c>, and optionally stamps <paramref name="markKey"/> on the dead card.</summary>
    private static async Task<bool> PlaySourceForFreeAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId deadCardId,
        HeadlessEntityId chosenSourceId,
        string? markKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(deadCardId, out CardInstanceRecord? deadCard) || deadCard is null)
        {
            return false;
        }

        List<string> sources = ReadSourceIds(deadCard.Metadata).Select(id => id.Value).ToList();
        if (!sources.Remove(chosenSourceId.Value))
        {
            return false;
        }

        await zoneMover.MoveAsync(
            new ZoneMoveRequest(deadCard.OwnerId, chosenSourceId, ChoiceZone.None, ChoiceZone.BattleArea, FaceUp: true),
            cancellationToken).ConfigureAwait(false);

        if (repository.TryGetInstance(chosenSourceId, out CardInstanceRecord? source) && source is not null)
        {
            var sourceMetadata = new Dictionary<string, object?>(source.Metadata, StringComparer.Ordinal);
            sourceMetadata.Remove(SourceIdsKey);
            sourceMetadata.Remove(DeletedByBattleKey);
            sourceMetadata.Remove(DeletedByEffectKey);
            sourceMetadata[EnteredThisTurnKey] = true;
            repository.Upsert(source with { Metadata = sourceMetadata });
        }

        var deadMetadata = new Dictionary<string, object?>(deadCard.Metadata, StringComparer.Ordinal);
        if (sources.Count > 0)
        {
            deadMetadata[SourceIdsKey] = sources.ToArray();
        }
        else
        {
            deadMetadata.Remove(SourceIdsKey);
        }

        if (markKey is not null)
        {
            deadMetadata[markKey] = true;
        }

        repository.Upsert(deadCard with { Metadata = deadMetadata });
        return true;
    }

    /// <summary>
    /// (C-22 Save) AFTER a card is deleted, its controller may place it under one of their other battle-area
    /// permanents as a digivolution source (AS-IS SaveProcess: AddDigivolutionCardsBottom onto a Tamer).
    /// Post-deletion response like Ascension; both deletion paths call it once the card is in the trash.
    /// LIMITATION: attaches to the first eligible permanent rather than surfacing the AS-IS "select 1".
    /// </summary>
    public static async Task<bool> TrySaveAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId cardId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);

        if (!repository.TryGetInstance(cardId, out CardInstanceRecord? record) ||
            record is null ||
            !ReadFlag(record.Metadata, HasSaveKey) ||
            zoneMover is not IZoneStateReader zones ||
            !zones.GetCards(record.OwnerId, ChoiceZone.Trash).Contains(cardId))
        {
            return false;
        }

        HeadlessEntityId? target = null;
        foreach (HeadlessEntityId candidate in zones.GetCards(record.OwnerId, ChoiceZone.BattleArea))
        {
            if (candidate != cardId)
            {
                target = candidate;
                break;
            }
        }

        if (target is not HeadlessEntityId targetId)
        {
            return false;
        }

        await DigivolutionStackHelpers.AddSourcesBottomAsync(
            repository, zoneMover, targetId, new[] { cardId }, ChoiceZone.Trash, cancellationToken).ConfigureAwait(false);

        if (repository.TryGetInstance(cardId, out CardInstanceRecord? moved) && moved is not null)
        {
            repository.Upsert(moved with
            {
                Metadata = new Dictionary<string, object?>(moved.Metadata, StringComparer.Ordinal)
                {
                    [SavedKey] = true,
                }
            });
        }

        return true;
    }

    private static int ReadInt(IReadOnlyDictionary<string, object?> metadata, string key, int defaultValue)
    {
        if (!metadata.TryGetValue(key, out object? raw) || raw is null)
        {
            return defaultValue;
        }

        return raw switch
        {
            int value => value,
            long value when value >= int.MinValue && value <= int.MaxValue => (int)value,
            string text when int.TryParse(text, out int parsed) => parsed,
            _ => defaultValue
        };
    }

    private static int SourceCount(IReadOnlyDictionary<string, object?> metadata) => ReadSourceIds(metadata).Count;

    private static IReadOnlyList<HeadlessEntityId> ReadSourceIds(IReadOnlyDictionary<string, object?> metadata)
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
