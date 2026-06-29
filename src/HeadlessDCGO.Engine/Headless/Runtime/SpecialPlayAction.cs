namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>(G6-004) The special play a <see cref="SpecialPlayAction"/> performs.</summary>
public enum SpecialPlayKind
{
    /// <summary>DigiXros: named materials (hand/field) fuse under the new top.</summary>
    DigiXros,

    /// <summary>DNA Digivolution (Jogress): two battle-area Digimon fuse under the new top.</summary>
    DnaDigivolve,

    /// <summary>Blast Digivolve: a single battle-area target is digivolved into for free.</summary>
    Blast,
}

// (G6-004) Special plays that put a card onto the battle area by consuming materials, rather than the
// normal Hand->BattleArea play: DigiXros / DNA Digivolution (materials -> sources, via
// FusionDigivolveHelpers) and Blast Digivolve (a single target, via FreeDigivolveHelpers). Connects those
// D-5/D-6 helpers to an executable action: pay the (reduced/zero) cost, fuse, then auto-register the new
// top's effects (G6-001) and open the WhenDigivolving window.
//
// Material SELECTION (which materials satisfy a card's DigiXros / DNA requirement) comes from the card's
// own condition (per-card effect data); this action takes the chosen materials explicitly, so the driver
// / a future legal-action enumerator supplies them.
public sealed class SpecialPlayAction
{
    public const string MaterialsKey = "materials";
    public const string FusionKindKey = "specialPlayKind";

    /// <summary>(G7-003) Enumerate the special plays legal right now. DigiXros/DNA recipes (which materials
    /// satisfy a card's requirement) live in per-card effect data (AddDigiXrosConditionClass), which is not
    /// modelled yet — so this returns nothing until that recipe data exists. The action itself is fully
    /// pipeline-routable (MetadataActionProcessor), so a driver/recipe source can construct and run one via
    /// <see cref="Create"/>. Kept here so the dispatcher lights up automatically once recipes are added.</summary>
    public IReadOnlyList<LegalAction> GetLegalActions(EngineContext context, HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Array.Empty<LegalAction>();
    }

    public static LegalAction Create(
        HeadlessPlayerId playerId,
        HeadlessEntityId topCardId,
        IReadOnlyList<HeadlessEntityId> materials,
        int memoryCost,
        SpecialPlayKind kind)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [HeadlessActionParameterKeys.CardId] = topCardId.Value,
            [HeadlessActionParameterKeys.MemoryCost] = memoryCost,
            [MaterialsKey] = string.Join(",", materials.Select(m => m.Value)),
            [FusionKindKey] = kind.ToString(),
        };
        return HeadlessActionFactory.Create(HeadlessActionTypes.SpecialPlay, playerId, actionId: null, parameters);
    }

    public async Task<ActionProcessResult> ProcessAsync(
        LegalAction action,
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryRead(action, out HeadlessEntityId topCardId, out IReadOnlyList<HeadlessEntityId> materials, out int memoryCost, out SpecialPlayKind kind, out string? error))
        {
            return ActionProcessResult.Failure(error ?? "Invalid SpecialPlay payload.", BaseMetadata(action));
        }

        var zones = (IZoneStateReader)context.ZoneMover;
        if (!context.CardInstanceRepository.TryGetInstance(topCardId, out CardInstanceRecord? top) || top is null || top.OwnerId != action.PlayerId)
        {
            return ActionProcessResult.Illegal(action, $"Top card '{topCardId}' not found or not owned by player.", BaseMetadata(action));
        }

        if (!zones.GetCards(action.PlayerId, ChoiceZone.Hand).Contains(topCardId))
        {
            return ActionProcessResult.Illegal(action, $"Top card '{topCardId}' is not in hand.", BaseMetadata(action));
        }

        if (materials.Count == 0)
        {
            return ActionProcessResult.Illegal(action, "Special play requires at least one material.", BaseMetadata(action));
        }

        foreach (HeadlessEntityId material in materials)
        {
            if (!zones.GetCards(action.PlayerId, ChoiceZone.BattleArea).Contains(material))
            {
                return ActionProcessResult.Illegal(action, $"Material '{material}' is not on the player's battle area.", BaseMetadata(action));
            }
        }

        if (memoryCost < 0 || !context.MemoryController.CanPay(memoryCost))
        {
            return ActionProcessResult.Illegal(action, $"Cannot pay special-play cost {memoryCost}.", BaseMetadata(action));
        }

        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.BeforePayCost, actor: action.PlayerId, subject: topCardId);
        HeadlessMemoryState paid = context.MemoryController.Pay(memoryCost);
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.AfterPayCost, actor: action.PlayerId, subject: topCardId);
        EffectDurationExpiry.ExpireFixedCostCalc(context.EffectRegistry);

        bool performed;
        if (kind == SpecialPlayKind.Blast)
        {
            performed = await FreeDigivolveHelpers.DigivolveFreeAsync(
                context.CardInstanceRepository, context.ZoneMover, topCardId, materials[0], ChoiceZone.Hand, context.GameEventQueue, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            FusionKind fusion = kind == SpecialPlayKind.DnaDigivolve ? FusionKind.DnaDigivolve : FusionKind.DigiXros;
            IReadOnlyList<HeadlessEntityId> merged = await FusionDigivolveHelpers.FuseAsync(
                context.CardInstanceRepository, context.ZoneMover, topCardId, ChoiceZone.Hand, materials,
                materialFromZone: ChoiceZone.BattleArea, gameEventQueue: context.GameEventQueue, kind: fusion, cancellationToken: cancellationToken).ConfigureAwait(false);
            performed = merged.Count > 0;
        }

        if (!performed)
        {
            return ActionProcessResult.Failure("Special play could not be performed (invalid materials).", BaseMetadata(action));
        }

        // G6-001: the fused top entered play — auto-register its effects.
        CardEffectRegistrar.RegisterCard(context, topCardId, action.PlayerId);
        // W1: open the WhenDigivolving window for the new top.
        TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.WhenDigivolving, actor: action.PlayerId, subject: topCardId);

        Dictionary<string, object?> metadata = BaseMetadata(action);
        metadata[HeadlessActionParameterKeys.CardId] = topCardId.Value;
        metadata[HeadlessActionParameterKeys.Memory] = paid.Current;
        metadata[FusionKindKey] = kind.ToString();
        metadata["materialCount"] = materials.Count;
        return ActionProcessResult.Success("Special play resolved.", metadata);
    }

    private static bool TryRead(
        LegalAction action,
        out HeadlessEntityId topCardId,
        out IReadOnlyList<HeadlessEntityId> materials,
        out int memoryCost,
        out SpecialPlayKind kind,
        out string? error)
    {
        topCardId = default;
        materials = Array.Empty<HeadlessEntityId>();
        memoryCost = 0;
        kind = SpecialPlayKind.DigiXros;
        error = null;

        if (!action.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? rawTop) || rawTop?.ToString() is not { Length: > 0 } topValue)
        {
            error = "Missing top card id.";
            return false;
        }

        topCardId = new HeadlessEntityId(topValue);
        memoryCost = action.Parameters.TryGetValue(HeadlessActionParameterKeys.MemoryCost, out object? rawCost) && rawCost is int c ? c : 0;

        if (action.Parameters.TryGetValue(MaterialsKey, out object? rawMaterials) && rawMaterials?.ToString() is { Length: > 0 } materialsValue)
        {
            materials = materialsValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(id => new HeadlessEntityId(id))
                .ToArray();
        }

        if (action.Parameters.TryGetValue(FusionKindKey, out object? rawKind) && Enum.TryParse(rawKind?.ToString(), out SpecialPlayKind parsedKind))
        {
            kind = parsedKind;
        }

        return true;
    }

    private static Dictionary<string, object?> BaseMetadata(LegalAction action) => new()
    {
        [HeadlessActionParameterKeys.ActionId] = action.Id.Value,
        [HeadlessActionParameterKeys.PlayerId] = action.PlayerId.Value,
        [HeadlessActionParameterKeys.ActionType] = action.ActionType,
    };
}
