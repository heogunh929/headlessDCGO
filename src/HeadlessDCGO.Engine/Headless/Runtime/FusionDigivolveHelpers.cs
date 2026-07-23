namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>(F-8.5) Which fusion a <see cref="FusionDigivolveHelpers.FuseAsync"/> represents, so the
/// WhenDigivolving event can be tagged for IsJogress / IsDigiXros conditions.</summary>
public enum FusionKind
{
    None = 0,
    DnaDigivolve = 1,
    DigiXros = 2,
}

/// <summary>
/// (D-5 DNA Digivolve / Jogress / DigiXros) Fuses several materials into a single permanent. Generalises
/// the single-target <see cref="DigivolveAction"/> attach: the new top card is placed on the battle area
/// and EVERY material (plus each material's own existing digivolution sources) is merged, in order, into
/// the new top's source stack. Materials leave their zone (they live off-field as sources).
///
/// - DNA Digivolve (Jogress): the materials are two battle-area Digimon (with their stacks).
/// - DigiXros: the materials are named cards from hand/field.
///
/// Source ordering matches <c>DigivolveAction.AttachTargetAsSource</c> (top→bottom, newest first):
/// material0, material0's sources, material1, material1's sources, ..., then the top card's own sources.
/// Jogress/Xros paths are summoning-sickness exempt (AS-IS EnterFieldTurnCount = -1), so the fused
/// Digimon is not marked entered-this-turn. Opens <see cref="TriggerTimings.WhenDigivolving"/>.
/// </summary>
public static class FusionDigivolveHelpers
{
    public const string SourceIdsKey = "sourceIds";
    public const string EnteredThisTurnKey = "enteredThisTurn";

    // (C3) F-8.5 fusion-digivolve event tags — stamped onto the WhenDigivolving event so the AS-IS IsJogress /
    // IsDigiXros condition reads (CardEffectCommons.IsJogress(ctx) → "{EventValuePrefix}isJogress";
    // GetFromHashtable.IsJogress(Hashtable) → "isJogress") see the DNA/Xros flag. Values are load-bearing string
    // keys and MUST stay "isJogress"/"isDigiXros". Relocated here (their sole producer) from the retired
    // SpecialConditionHelpers, whose duplicate dict-form predicates had zero live consumers.
    public const string IsJogressKey = "isJogress";
    public const string IsDigiXrosKey = "isDigiXros";

    /// <summary>
    /// Fuse <paramref name="materials"/> under <paramref name="topCardId"/> (moved from
    /// <paramref name="topFromZone"/> onto the battle area). Returns the merged source id list, or an
    /// empty list when the inputs are invalid.
    /// </summary>
    /// <param name="enteredThisTurnOverride">Summoning-sickness flag for the fused top. Null (default) =
    /// not sick (Jogress/Xros exempt). A single-target free digivolve (D-6 Blast/Arts) passes the value
    /// inherited from its target instead.</param>
    /// <param name="permanentContinuity">(RD-R3-02, 리뷰3 이월) True for a single-target FREE digivolve
    /// (D-6 Blast/Arts): AS-IS runs it through the normal evolution arm (`permanent = _targetPermanent;
    /// permanent.AddCardSource(card)`, CardController.cs:1372-1376) — the SAME AS-IS Permanent object
    /// persists across the top swap, so both swap moves carry
    /// <see cref="HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.PermanentContinuityKey"/> and this op owns the
    /// <see cref="HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ReKey"/> (the DigivolveAction/DeDigivolveHelpers seat
    /// convention). False (default) for Jogress/DigiXros: AS-IS creates a NEW Permanent
    /// (`permanent = new Permanent(...)`, CardController.cs:1497) — the chokepoint CREATE/DIE Resets are
    /// the correct lifetime.</param>
    public static async Task<IReadOnlyList<HeadlessEntityId>> FuseAsync(
        ICardInstanceRepository repository,
        IZoneMover zoneMover,
        HeadlessEntityId topCardId,
        ChoiceZone topFromZone,
        IReadOnlyList<HeadlessEntityId> materials,
        ChoiceZone materialFromZone = ChoiceZone.BattleArea,
        GameEventQueue? gameEventQueue = null,
        bool? enteredThisTurnOverride = null,
        FusionKind kind = FusionKind.None,
        CancellationToken cancellationToken = default,
        Bridge.EngineContext? context = null,
        bool permanentContinuity = false)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(zoneMover);
        ArgumentNullException.ThrowIfNull(materials);

        if (materials.Count == 0 ||
            !repository.TryGetInstance(topCardId, out CardInstanceRecord? top) || top is null)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        // Build the merged source list: each material then its existing sources, in selection order.
        var merged = new List<string>();
        var validMaterials = new List<CardInstanceRecord>();
        foreach (HeadlessEntityId materialId in materials)
        {
            if (materialId == topCardId ||
                !repository.TryGetInstance(materialId, out CardInstanceRecord? material) || material is null)
            {
                continue;
            }

            validMaterials.Add(material);
            merged.Add(materialId.Value);
            merged.AddRange(ReadSourceIds(material.Metadata).Select(id => id.Value));
        }

        if (validMaterials.Count == 0)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        // The top card's own prior sources sit at the very bottom.
        merged.AddRange(ReadSourceIds(top.Metadata).Select(id => id.Value));
        merged = merged.Distinct().Where(id => id != topCardId.Value).ToList();

        // (RD-R3-02) a free single-target digivolve is a TOP SWAP of a persisting AS-IS Permanent — mark
        // both halves so the zone-mover lifetime chokepoint does not Reset the bookkeeping (this op ReKeys
        // below). Only meaningful for the exactly-one-material D-6 shape; a fusion keeps the CREATE path.
        bool topSwap = permanentContinuity && validMaterials.Count == 1;

        // Move the new top onto the field; move each material off-field (it becomes a source).
        await zoneMover.MoveAsync(
            new ZoneMoveRequest(top.OwnerId, topCardId, topFromZone, ChoiceZone.BattleArea, FaceUp: true,
                Metadata: topSwap ? HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ContinuityMoveMetadata : null),
            cancellationToken).ConfigureAwait(false);

        // (max-trash / max-under-Tamer DigiXros) materials may come from DIFFERENT zones (field + trash) OR from
        // UNDER a Tamer (a digivolution source, in no zone). Move zone materials from their ACTUAL zone; for an
        // under-Tamer source, detach it from its host's source stack instead of a zone move.
        var reader = zoneMover as IZoneStateReader;
        foreach (CardInstanceRecord material in validMaterials)
        {
            ChoiceZone? from = ResolveMaterialZone(reader, material.OwnerId, material.InstanceId);
            if (from is ChoiceZone zone)
            {
                await zoneMover.MoveAsync(
                    new ZoneMoveRequest(material.OwnerId, material.InstanceId, zone, ChoiceZone.None,
                        // (RD-R3-02) the leaving half of the D-6 top swap — see the topSwap marker above.
                        Metadata: topSwap ? HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ContinuityMoveMetadata : null),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Under-Tamer material: it is already off-field (a source under a Tamer). Detach it from the host.
                DetachUnderTamerSource(repository, reader, material.OwnerId, material.InstanceId);
            }
        }

        // Re-read the top (the move may have touched state) and write the merged stack.
        CardInstanceRecord current = repository.TryGetInstance(topCardId, out CardInstanceRecord? latest) && latest is not null ? latest : top;
        var metadata = new Dictionary<string, object?>(current.Metadata, StringComparer.Ordinal)
        {
            [SourceIdsKey] = merged.ToArray(),
            // Jogress/Xros: the fused Digimon is not summoning sick (AS-IS EnterFieldTurnCount = -1). A
            // free single-target digivolve (D-6) instead inherits its target's entered-this-turn state.
            [EnteredThisTurnKey] = enteredThisTurnOverride ?? false,
        };
        repository.Upsert(current with { Metadata = metadata });

        // (RD-R3-02) D-6 free digivolve: the persisting permanent's just-after bookkeeping follows the swap
        // to the new top (AS-IS: the SAME Permanent object, CardController.cs:1372-1376) — the seat convention
        // of DigivolveAction.AttachTargetAsSource / DeDigivolveHelpers' promotes.
        if (topSwap)
        {
            HeadlessDCGO.Engine.Headless.State.PermanentBookkeepingStore.ReKey(
                repository, validMaterials[0].InstanceId, topCardId);
        }

        // (B-3 tuck reset) AS-IS resets the per-turn use counts of the fused stack's cards: Jogress resets EVERY
        // DigivolutionCard of the new permanent (CardController.cs:1509-1512), DigiXros resets each tucked
        // material (SelectDigiXrosClass.cs:923); the new top's own reset rides its play path (RegisterCard).
        if (context is not null)
        {
            foreach (string sourceValue in merged)
            {
                var sourceId = new HeadlessEntityId(sourceValue);
                HeadlessPlayerId owner = repository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null
                    ? src.OwnerId
                    : current.OwnerId;
                // (R6-Da'-6 D3) AS-IS Jogress/DNA merged-source cap reset (CardController.cs:1509-1512) on the
                // CEntity_EffectController store.
                Assets.Scripts.Script.CardEffectCommons.CEntity_EffectControllerStore.ResetUseCountForCard(context, sourceId);
            }
        }

        if (gameEventQueue is not null)
        {
            // F-8.5: tag the digivolve so IsJogress / IsDigiXros conditions can read it.
            IReadOnlyDictionary<string, object?>? tags = kind switch
            {
                FusionKind.DnaDigivolve => new Dictionary<string, object?>(StringComparer.Ordinal) { [IsJogressKey] = true },
                FusionKind.DigiXros => new Dictionary<string, object?>(StringComparer.Ordinal) { [IsDigiXrosKey] = true },
                _ => null,
            };
            TriggerEventEmitter.Emit(gameEventQueue, TriggerTimings.WhenDigivolving, actor: current.OwnerId, subject: topCardId, extraMetadata: tags);
        }

        return merged.Select(id => new HeadlessEntityId(id)).ToArray();
    }

    /// <summary>(max-trash DigiXros) The zone a material is CURRENTLY in — battle area / trash / hand (all valid
    /// DigiXros sources). Returns null when it is in no zone (an under-Tamer digivolution source).</summary>
    private static ChoiceZone? ResolveMaterialZone(IZoneStateReader? reader, HeadlessPlayerId owner, HeadlessEntityId material)
    {
        if (reader is null)
        {
            return ChoiceZone.BattleArea;
        }

        foreach (ChoiceZone zone in new[] { ChoiceZone.BattleArea, ChoiceZone.Trash, ChoiceZone.Hand })
        {
            if (reader.GetCards(owner, zone).Contains(material))
            {
                return zone;
            }
        }

        return null;
    }

    /// <summary>(max-under-Tamer DigiXros) Detach <paramref name="material"/> from the digivolution-source stack of
    /// whichever battle-area permanent currently holds it (its Tamer host), so it can be re-attached under the new
    /// DigiXros top. AS-IS: a Tamer's digivolution card selected as a DigiXros material leaves that Tamer.</summary>
    private static void DetachUnderTamerSource(ICardInstanceRepository repository, IZoneStateReader? reader, HeadlessPlayerId owner, HeadlessEntityId material)
    {
        if (reader is null)
        {
            return;
        }

        foreach (HeadlessEntityId hostId in reader.GetCards(owner, ChoiceZone.BattleArea))
        {
            if (!repository.TryGetInstance(hostId, out CardInstanceRecord? host) || host is null)
            {
                continue;
            }

            IReadOnlyList<HeadlessEntityId> sources = ReadSourceIds(host.Metadata);
            if (!sources.Contains(material))
            {
                continue;
            }

            var remaining = sources.Where(id => id != material).ToArray();
            var metadata = new Dictionary<string, object?>(host.Metadata, StringComparer.Ordinal);
            if (remaining.Length > 0)
            {
                metadata[SourceIdsKey] = remaining.Select(id => id.Value).ToArray();
            }
            else
            {
                metadata.Remove(SourceIdsKey);
            }

            repository.Upsert(host with { Metadata = metadata });
            return;
        }
    }

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
            _ => Array.Empty<HeadlessEntityId>(),
        };
    }
}
