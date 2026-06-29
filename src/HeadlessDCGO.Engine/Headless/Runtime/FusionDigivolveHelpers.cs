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

    /// <summary>
    /// Fuse <paramref name="materials"/> under <paramref name="topCardId"/> (moved from
    /// <paramref name="topFromZone"/> onto the battle area). Returns the merged source id list, or an
    /// empty list when the inputs are invalid.
    /// </summary>
    /// <param name="enteredThisTurnOverride">Summoning-sickness flag for the fused top. Null (default) =
    /// not sick (Jogress/Xros exempt). A single-target free digivolve (D-6 Blast/Arts) passes the value
    /// inherited from its target instead.</param>
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
        CancellationToken cancellationToken = default)
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

        // Move the new top onto the field; move each material off-field (it becomes a source).
        await zoneMover.MoveAsync(
            new ZoneMoveRequest(top.OwnerId, topCardId, topFromZone, ChoiceZone.BattleArea, FaceUp: true),
            cancellationToken).ConfigureAwait(false);

        foreach (CardInstanceRecord material in validMaterials)
        {
            await zoneMover.MoveAsync(
                new ZoneMoveRequest(material.OwnerId, material.InstanceId, materialFromZone, ChoiceZone.None),
                cancellationToken).ConfigureAwait(false);
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

        if (gameEventQueue is not null)
        {
            // F-8.5: tag the digivolve so IsJogress / IsDigiXros conditions can read it.
            IReadOnlyDictionary<string, object?>? tags = kind switch
            {
                FusionKind.DnaDigivolve => new Dictionary<string, object?>(StringComparer.Ordinal) { [SpecialConditionHelpers.IsJogressKey] = true },
                FusionKind.DigiXros => new Dictionary<string, object?>(StringComparer.Ordinal) { [SpecialConditionHelpers.IsDigiXrosKey] = true },
                _ => null,
            };
            TriggerEventEmitter.Emit(gameEventQueue, TriggerTimings.WhenDigivolving, actor: current.OwnerId, subject: topCardId, extraMetadata: tags);
        }

        return merged.Select(id => new HeadlessEntityId(id)).ToArray();
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
