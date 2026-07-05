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

    /// <summary>(PRIM-W2) App Fusion: named [App] material Digimon fuse under the new top — mechanically the
    /// same "materials -> sources" fusion as DigiXros (routed through FusionKind.DigiXros in ProcessAsync).
    /// The recipe (material names + cost) is data-driven via SpecialPlayRecipeRegistry, like DigiXros.</summary>
    AppFusion,

    /// <summary>(PRIM special-play) Burst Digivolution: this hand card digivolves onto a target battle-area
    /// Digimon (recipe material[0]) — like <see cref="Blast"/> — but AS-IS also returns a matching Tamer to the
    /// hand (recipe <c>TamerCondition</c>) and pays the burst cost. AS-IS
    /// <c>CardSource.CanBurstDigivolutionFromTargetPermanent</c> / <c>BurstDigivolutionCondition</c>.</summary>
    Burst,
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
    public const string BurstTamerKey = "burstTamer";

    /// <summary>(G8-006) Enumerate the special plays legal right now: for each hand card with a registered
    /// <see cref="SpecialPlayRecipe"/>, find a distinct battle-area material per required material name and,
    /// if all are satisfied and the cost is payable, offer the special play. Recipes are populated by ported
    /// DigiXros/DNA/Blast cards (per-card effect data); cards with no recipe contribute nothing.</summary>
    public IReadOnlyList<LegalAction> GetLegalActions(EngineContext context, HeadlessPlayerId playerId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (playerId.IsEmpty || context.ZoneMover is not IZoneStateReader zones)
        {
            return Array.Empty<LegalAction>();
        }

        IReadOnlyList<HeadlessEntityId> battle = zones.GetCards(playerId, ChoiceZone.BattleArea);
        var actions = new List<LegalAction>();
        foreach (HeadlessEntityId handCard in zones.GetCards(playerId, ChoiceZone.Hand))
        {
            if (!context.CardInstanceRepository.TryGetInstance(handCard, out CardInstanceRecord? instance) || instance is null
                || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) || def is null)
            {
                continue;
            }

            // (PRIM-W5) A ported special-play card declares its recipe inside its effect code
            // (DigiXrosEffectFromNames / BlastDigivolveEffect / ...). That code only runs when the card enters
            // play, but the recipe is needed HERE (card still in hand) to offer the play. So register it
            // on-demand: run the card's effect declaration (discarding the returned effects — only the
            // special-play factories have a registry side-effect) the first time we consider it.
            EnsureSpecialPlayRecipe(context, def, handCard, playerId);

            if (!SpecialPlayRecipeRegistry.TryGet(def.CardNumber, out SpecialPlayRecipe? recipe) || recipe is null)
            {
                continue;
            }

            // (FR2) honour the card-authored availability condition (AS-IS special-play `condition`) — the play
            // is only offered when it passes, not unconditionally.
            // (max-trash / max-under-Tamer DigiXros) up to recipe.MaxTrashCount slots may be satisfied by TRASH
            // cards, and up to recipe.MaxUnderTamerCount by a Tamer's digivolution-source cards (AS-IS
            // AddMaxTrashCountDigiXros / maxTamerDigivolutionCardsCount). Evaluated against the DigiXros source card.
            var xrosCard = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, handCard, playerId, instance.OwnerId);
            int maxTrash = recipe.MaxTrashCount is null ? 0 : Math.Max(0, recipe.MaxTrashCount(xrosCard));
            int maxUnderTamer = recipe.MaxUnderTamerCount is null ? 0 : Math.Max(0, recipe.MaxUnderTamerCount(xrosCard));
            var cappedPools = new List<(IReadOnlyList<HeadlessEntityId> Pool, int Cap)>();
            if (maxTrash > 0)
            {
                cappedPools.Add((zones.GetCards(playerId, ChoiceZone.Trash), maxTrash));
            }
            if (maxUnderTamer > 0)
            {
                cappedPools.Add((UnderTamerSources(context, zones, playerId), maxUnderTamer));
            }

            if ((recipe.Condition is null || recipe.Condition())
                && TryMatchMaterials(context, battle, cappedPools, recipe.Materials, playerId, out List<HeadlessEntityId> materials)
                && context.MemoryController.CanPay(recipe.MemoryCost))
            {
                // (Burst Digivolution) additionally require a matching Tamer (returned to hand on execute); the
                // play is only legal when such a Tamer exists on the battle area (distinct from the target).
                if (recipe.Kind == SpecialPlayKind.Burst)
                {
                    HeadlessEntityId? tamer = FindBurstTamer(context, battle, materials, recipe.TamerCondition, playerId);
                    if (tamer is HeadlessEntityId burstTamer)
                    {
                        actions.Add(Create(playerId, handCard, materials, recipe.MemoryCost, recipe.Kind, burstTamer));
                    }
                }
                else
                {
                    actions.Add(Create(playerId, handCard, materials, recipe.MemoryCost, recipe.Kind));
                }
            }
        }

        return actions
            .OrderBy(a => a.Id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    // (PRIM-W5) Register a hand card's special-play recipe on demand (idempotent): if none is registered,
    // instantiate the card's effect class and run its declaration across timings so the special-play factory's
    // registry side-effect fires. Returned effects are discarded — only recipe registration happens here.
    private static void EnsureSpecialPlayRecipe(EngineContext context, CardRecord def, HeadlessEntityId cardId, HeadlessPlayerId owner)
    {
        if (SpecialPlayRecipeRegistry.TryGet(def.CardNumber, out _))
        {
            return;
        }

        if (!Assets.Scripts.Script.CardEffectCommons.CardEffectDispatch.TryCreateForCard(def, out Assets.Scripts.Script.CardEffectCommons.CEntity_Effect? effect) || effect is null)
        {
            return;
        }

        var card = new Assets.Scripts.Script.CardEffectCommons.CardSource(context, cardId, owner, owner);
        foreach (Assets.Scripts.Script.CardEffectCommons.EffectTiming timing in Enum.GetValues<Assets.Scripts.Script.CardEffectCommons.EffectTiming>())
        {
            try { _ = effect.CardEffects(timing, card); }
            catch { /* declaration must not throw during enumeration; ignore per-timing failures */ }
        }
    }

    private static bool TryMatchMaterials(
        EngineContext context, IReadOnlyList<HeadlessEntityId> battle,
        IReadOnlyList<(IReadOnlyList<HeadlessEntityId> Pool, int Cap)> cappedPools,
        IReadOnlyList<SpecialPlayMaterial> required, HeadlessPlayerId owner, out List<HeadlessEntityId> materials)
    {
        // (AD1-J) BACKTRACKING assignment, not greedy — AS-IS enumerates PERMUTATIONS
        // (CanPlayJogress, CardSource.cs:2755 ParameterComparer.Enumerate), so a candidate that satisfies
        // two slots must not starve the second slot when another candidate could take the first.
        // (max-trash / max-under-Tamer DigiXros) field candidates are unlimited; each CAPPED pool (trash /
        // under-Tamer) may satisfy up to its Cap slots — a per-pool budget is tracked so no cap is exceeded.
        materials = new List<HeadlessEntityId>();
        var used = new HashSet<HeadlessEntityId>();
        int[] poolUsed = new int[cappedPools.Count];
        return Assign(0, materials, used);

        bool Assign(int slotIndex, List<HeadlessEntityId> assigned, HashSet<HeadlessEntityId> taken)
        {
            if (slotIndex >= required.Count)
            {
                return true;
            }

            SpecialPlayMaterial slot = required[slotIndex];

            bool TrySource(IReadOnlyList<HeadlessEntityId> pool, int poolIndex)
            {
                // poolIndex < 0 = the uncapped field pool.
                if (poolIndex >= 0 && poolUsed[poolIndex] >= cappedPools[poolIndex].Cap)
                {
                    return false;
                }

                foreach (HeadlessEntityId id in pool)
                {
                    // Evaluate the card-authored material predicate against the candidate (1:1 with the original
                    // CanSelectCardCondition) — not a mere card-name equality.
                    if (taken.Contains(id)
                        || !slot.Matches(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, id, owner, owner)))
                    {
                        continue;
                    }

                    taken.Add(id);
                    assigned.Add(id);
                    if (poolIndex >= 0) { poolUsed[poolIndex]++; }
                    if (Assign(slotIndex + 1, assigned, taken))
                    {
                        return true;
                    }
                    if (poolIndex >= 0) { poolUsed[poolIndex]--; }
                    taken.Remove(id);
                    assigned.RemoveAt(assigned.Count - 1);
                }

                return false;
            }

            // Prefer field candidates (unlimited); fall back to each capped pool within its budget.
            if (TrySource(battle, -1))
            {
                return true;
            }
            for (int i = 0; i < cappedPools.Count; i++)
            {
                if (TrySource(cappedPools[i].Pool, i))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>(max-under-Tamer DigiXros) The player's Tamers' digivolution-source cards — candidate DigiXros
    /// materials sourced from under a Tamer (AS-IS <c>permanent.IsTamer &amp;&amp; permanent.DigivolutionCards</c>).</summary>
    private static IReadOnlyList<HeadlessEntityId> UnderTamerSources(EngineContext context, IZoneStateReader zones, HeadlessPlayerId playerId)
    {
        var sources = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId permanentId in zones.GetCards(playerId, ChoiceZone.BattleArea))
        {
            if (!context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? perm) || perm is null
                || !context.CardRepository.TryGetCard(perm.DefinitionId, out CardRecord? def) || def is null
                || !def.IsCardType("Tamer"))
            {
                continue;
            }

            Headless.State.DigivolutionStack stack = Headless.State.DigivolutionStackReader.Read(
                context.CardInstanceRepository, context.CardRepository, permanentId);
            sources.AddRange(stack.UnderCards.Select(sc => sc.InstanceId));
        }

        return sources;
    }

    /// <summary>(Burst Digivolution) The first battle-area Tamer (other than the digivolve target) that satisfies
    /// the recipe's <c>TamerCondition</c> — the Tamer AS-IS returns to the hand as part of the burst.</summary>
    private static HeadlessEntityId? FindBurstTamer(
        EngineContext context, IReadOnlyList<HeadlessEntityId> battle, IReadOnlyList<HeadlessEntityId> target,
        Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool>? tamerCondition, HeadlessPlayerId owner)
    {
        if (tamerCondition is null)
        {
            return null;
        }

        foreach (HeadlessEntityId id in battle)
        {
            if (target.Contains(id))
            {
                continue;
            }

            if (tamerCondition(new Assets.Scripts.Script.CardEffectCommons.CardSource(context, id, owner, owner)))
            {
                return id;
            }
        }

        return null;
    }

    public static LegalAction Create(
        HeadlessPlayerId playerId,
        HeadlessEntityId topCardId,
        IReadOnlyList<HeadlessEntityId> materials,
        int memoryCost,
        SpecialPlayKind kind,
        HeadlessEntityId? burstTamer = null)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [HeadlessActionParameterKeys.CardId] = topCardId.Value,
            [HeadlessActionParameterKeys.MemoryCost] = memoryCost,
            [MaterialsKey] = string.Join(",", materials.Select(m => m.Value)),
            [FusionKindKey] = kind.ToString(),
        };
        if (burstTamer is HeadlessEntityId tamer)
        {
            parameters[BurstTamerKey] = tamer.Value;
        }

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
            // (max-trash / max-under-Tamer DigiXros) a material may be on the battle area, in the trash, OR a
            // digivolution source under one of the player's battle-area permanents (a Tamer).
            if (!zones.GetCards(action.PlayerId, ChoiceZone.BattleArea).Contains(material)
                && !zones.GetCards(action.PlayerId, ChoiceZone.Trash).Contains(material)
                && !UnderTamerSources(context, zones, action.PlayerId).Contains(material))
            {
                return ActionProcessResult.Illegal(action, $"Material '{material}' is not on the player's battle area, in the trash, or under a Tamer.", BaseMetadata(action));
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
        if (kind == SpecialPlayKind.Burst)
        {
            // (Burst Digivolution) return the matched Tamer to the hand, then digivolve this card onto the target
            // (materials[0]) — the burst cost was already paid above.
            if (action.Parameters.TryGetValue(BurstTamerKey, out object? rawTamer) && rawTamer is string tamerId
                && !string.IsNullOrEmpty(tamerId))
            {
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(action.PlayerId, new HeadlessEntityId(tamerId), ChoiceZone.BattleArea, ChoiceZone.Hand, FaceUp: true),
                    cancellationToken).ConfigureAwait(false);
            }

            performed = await FreeDigivolveHelpers.DigivolveFreeAsync(
                context.CardInstanceRepository, context.ZoneMover, topCardId, materials[0], ChoiceZone.Hand, context.GameEventQueue, cancellationToken).ConfigureAwait(false);
        }
        else if (kind == SpecialPlayKind.Blast)
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

/// <summary>(G8-006) A card's special-play requirement: the kind and the material card NAMES that must be
/// present (one battle-area material per name), plus the memory cost. Derived from per-card effect data
/// (e.g. a DigiXros condition "Shoutmon X4 + Beelzemon").</summary>
/// <summary>One material slot of a special-play recipe. <see cref="Matches"/> mirrors the original
/// DigiXros/DNA <c>CanSelectCardCondition(CardSource)</c> — an arbitrary predicate over a candidate, NOT just
/// a card-name equality — so the ported condition is preserved 1:1. <see cref="Label"/> is for logging.</summary>
public sealed record SpecialPlayMaterial(Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool> Matches, string Label);

/// <param name="MaxTrashCount">(max-trash DigiXros) AS-IS <c>AddMaxTrashCountDigiXrosClass.GetMaxTrashCount</c> —
/// up to this many of the card's DigiXros material slots may be satisfied by cards FROM THE TRASH (isTrashCard),
/// not only hand/field. Evaluated per play against the DigiXros source card. Null = 0 (no trash materials).</param>
/// <param name="MaxUnderTamerCount">(max-under-Tamer DigiXros) the parallel <c>maxTamerDigivolutionCardsCount</c>
/// — up to this many slots may be satisfied by a Tamer's digivolution-source cards.</param>
public sealed record SpecialPlayRecipe(
    SpecialPlayKind Kind,
    IReadOnlyList<SpecialPlayMaterial> Materials,
    int MemoryCost,
    Func<bool>? Condition = null,
    Func<Assets.Scripts.Script.CardEffectCommons.CardSource, int>? MaxTrashCount = null,
    Func<Assets.Scripts.Script.CardEffectCommons.CardSource, int>? MaxUnderTamerCount = null,
    Func<Assets.Scripts.Script.CardEffectCommons.CardSource, bool>? TamerCondition = null);

/// <summary>(G8-006) Maps a card number to its special-play recipe. Populated by ported DigiXros / DNA /
/// Blast cards (the recipe registry, analogous to the effect dispatch).</summary>
public static class SpecialPlayRecipeRegistry
{
    private static readonly Dictionary<string, SpecialPlayRecipe> Recipes = new(StringComparer.OrdinalIgnoreCase);

    public static void Register(string cardNumber, SpecialPlayRecipe recipe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardNumber);
        ArgumentNullException.ThrowIfNull(recipe);
        Recipes[cardNumber.Trim()] = recipe;
    }

    public static bool TryGet(string? cardNumber, out SpecialPlayRecipe? recipe)
    {
        recipe = null;
        return !string.IsNullOrWhiteSpace(cardNumber) && Recipes.TryGetValue(cardNumber.Trim(), out recipe);
    }

    public static void Clear() => Recipes.Clear();
}
