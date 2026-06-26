namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// Fixed factored action space (G3.5-RL-A3, fixes P0-2). Each concrete legal action maps to a
/// distinct, position-derived index in a fixed-size space, so a policy can tell apart
/// "play the card in hand slot 0" from "play the card in hand slot 3" — unlike the type-only
/// <see cref="ActionEncoder"/> where same-type actions collapse into one mask slot.
/// The fixed size + per-index mask is what MaskablePPO / MultiDiscrete masking expect.
/// </summary>
public sealed record FactoredActionSchema
{
    public const int Version = 1;

    public FactoredActionSchema(
        int maxHand = 16,
        int maxField = 16,
        int maxChoice = 16)
    {
        if (maxHand <= 0 || maxField <= 0 || maxChoice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHand), "Factored action capacities must be positive.");
        }

        MaxHand = maxHand;
        MaxField = maxField;
        MaxChoice = maxChoice;

        // Lane layout (offset, capacity). Order is stable; offsets are cumulative.
        int offset = 0;
        NoOpOffset = offset; offset += 1;
        PassOffset = offset; offset += 1;
        AdvancePhaseOffset = offset; offset += 1;
        EndTurnOffset = offset; offset += 1;
        PlayCardOffset = offset; offset += maxHand;
        ActivateOptionOffset = offset; offset += maxHand;
        DigivolveOffset = offset; offset += maxHand * maxField;
        DeclareAttackOffset = offset; offset += maxField * (maxField + 1);
        ResolveChoiceOffset = offset; offset += maxChoice + 1;
        TotalSize = offset;
    }

    public static FactoredActionSchema Default { get; } = new();

    public int MaxHand { get; }

    public int MaxField { get; }

    public int MaxChoice { get; }

    public int NoOpOffset { get; }

    public int PassOffset { get; }

    public int AdvancePhaseOffset { get; }

    public int EndTurnOffset { get; }

    public int PlayCardOffset { get; }

    public int ActivateOptionOffset { get; }

    public int DigivolveOffset { get; }

    public int DeclareAttackOffset { get; }

    public int ResolveChoiceOffset { get; }

    public int TotalSize { get; }
}

/// <summary>One legal action placed at its factored index.</summary>
public sealed record FactoredAction(int Index, string Lane, LegalAction Action);

/// <summary>
/// The factored mask: a fixed-size vector with a 1 at every legal action's index, the placed
/// actions, and any actions that could not be mapped (e.g. a hand larger than the configured
/// capacity). Unmapped actions are surfaced, never silently dropped.
/// </summary>
public sealed class FactoredActionMask
{
    private readonly Dictionary<int, FactoredAction> _byIndex;

    public FactoredActionMask(
        FactoredActionSchema schema,
        IReadOnlyList<FactoredAction> actions,
        IReadOnlyList<LegalAction> unmapped)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        Unmapped = unmapped ?? throw new ArgumentNullException(nameof(unmapped));
        _byIndex = actions.ToDictionary(action => action.Index);
    }

    public FactoredActionSchema Schema { get; }

    public IReadOnlyList<FactoredAction> Actions { get; }

    public IReadOnlyList<LegalAction> Unmapped { get; }

    public int Size => Schema.TotalSize;

    public double[] ToMaskVector()
    {
        double[] vector = new double[Schema.TotalSize];
        foreach (FactoredAction action in Actions)
        {
            vector[action.Index] = 1d;
        }

        return vector;
    }

    public bool TryGetAction(int index, out LegalAction action)
    {
        if (_byIndex.TryGetValue(index, out FactoredAction? factored))
        {
            action = factored.Action;
            return true;
        }

        action = null!;
        return false;
    }
}

/// <summary>Resolves the board positions (hand slot, field slot, choice candidate slot) used to
/// derive a factored index. Built from the live engine zone/choice state.</summary>
public sealed class FactoredPositionContext
{
    private readonly Func<HeadlessPlayerId, ChoiceZone, IReadOnlyList<HeadlessEntityId>> _zoneResolver;
    private readonly IReadOnlyList<HeadlessEntityId> _choiceCandidates;

    public FactoredPositionContext(
        Func<HeadlessPlayerId, ChoiceZone, IReadOnlyList<HeadlessEntityId>> zoneResolver,
        IReadOnlyList<HeadlessEntityId> choiceCandidates)
    {
        _zoneResolver = zoneResolver ?? throw new ArgumentNullException(nameof(zoneResolver));
        _choiceCandidates = choiceCandidates ?? throw new ArgumentNullException(nameof(choiceCandidates));
    }

    public static FactoredPositionContext FromContext(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<HeadlessEntityId> candidates = context.ChoiceController.PendingRequest is { } request
            ? request.Candidates.Select(candidate => candidate.Id).ToArray()
            : Array.Empty<HeadlessEntityId>();

        Func<HeadlessPlayerId, ChoiceZone, IReadOnlyList<HeadlessEntityId>> resolver =
            context.ZoneMover is IZoneStateReader zones
                ? zones.GetCards
                : static (_, _) => Array.Empty<HeadlessEntityId>();

        return new FactoredPositionContext(resolver, candidates);
    }

    public int HandIndex(HeadlessPlayerId player, HeadlessEntityId cardId) => IndexIn(player, ChoiceZone.Hand, cardId);

    public int FieldIndex(HeadlessPlayerId player, HeadlessEntityId cardId) => IndexIn(player, ChoiceZone.BattleArea, cardId);

    public int ChoiceIndex(HeadlessEntityId cardId)
    {
        for (int i = 0; i < _choiceCandidates.Count; i++)
        {
            if (_choiceCandidates[i] == cardId)
            {
                return i;
            }
        }

        return -1;
    }

    private int IndexIn(HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId)
    {
        IReadOnlyList<HeadlessEntityId> cards = _zoneResolver(player, zone);
        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i] == cardId)
            {
                return i;
            }
        }

        return -1;
    }
}

public static class FactoredActionEncoder
{
    public static FactoredActionMask Encode(
        IReadOnlyList<LegalAction> legalActions,
        FactoredPositionContext positions,
        FactoredActionSchema? schema = null)
    {
        ArgumentNullException.ThrowIfNull(legalActions);
        ArgumentNullException.ThrowIfNull(positions);

        FactoredActionSchema effectiveSchema = schema ?? FactoredActionSchema.Default;
        var used = new HashSet<int>();
        var placed = new List<FactoredAction>();
        var unmapped = new List<LegalAction>();

        foreach (LegalAction action in legalActions)
        {
            (int index, string lane) = MapAction(action, positions, effectiveSchema);

            // Out of range, position not found, or a slot collision -> surface as unmapped.
            if (index < 0 || index >= effectiveSchema.TotalSize || !used.Add(index))
            {
                unmapped.Add(action);
                continue;
            }

            placed.Add(new FactoredAction(index, lane, action));
        }

        return new FactoredActionMask(effectiveSchema, placed, unmapped);
    }

    private static (int Index, string Lane) MapAction(
        LegalAction action,
        FactoredPositionContext positions,
        FactoredActionSchema schema)
    {
        switch (HeadlessActionTypes.Normalize(action.ActionType))
        {
            case HeadlessActionTypes.NormalizedNoOp:
                return (schema.NoOpOffset, "NoOp");
            case HeadlessActionTypes.NormalizedPass:
                return (schema.PassOffset, "Pass");
            case HeadlessActionTypes.NormalizedAdvancePhase:
                return (schema.AdvancePhaseOffset, "AdvancePhase");
            case HeadlessActionTypes.NormalizedEndTurn:
                return (schema.EndTurnOffset, "EndTurn");

            case HeadlessActionTypes.NormalizedPlayCard:
            {
                int hand = positions.HandIndex(action.PlayerId, ReadId(action, HeadlessActionParameterKeys.CardId));
                return (LaneIndex(schema.PlayCardOffset, hand, schema.MaxHand), "PlayCard");
            }

            case HeadlessActionTypes.NormalizedActivateOption:
            {
                int hand = positions.HandIndex(action.PlayerId, ReadId(action, HeadlessActionParameterKeys.CardId));
                return (LaneIndex(schema.ActivateOptionOffset, hand, schema.MaxHand), "ActivateOption");
            }

            case HeadlessActionTypes.NormalizedDigivolve:
            {
                int hand = positions.HandIndex(action.PlayerId, ReadId(action, HeadlessActionParameterKeys.CardId));
                int target = positions.FieldIndex(action.PlayerId, ReadId(action, HeadlessActionParameterKeys.TargetCardId));
                if (hand < 0 || target < 0 || hand >= schema.MaxHand || target >= schema.MaxField)
                {
                    return (-1, "Digivolve");
                }

                return (schema.DigivolveOffset + (hand * schema.MaxField) + target, "Digivolve");
            }

            case HeadlessActionTypes.NormalizedDeclareAttack:
            {
                int attacker = positions.FieldIndex(action.PlayerId, ReadId(action, HeadlessActionParameterKeys.AttackerId));
                if (attacker < 0 || attacker >= schema.MaxField)
                {
                    return (-1, "DeclareAttack");
                }

                bool direct = ReadBool(action, HeadlessActionParameterKeys.IsDirectAttack);
                int targetSlot;
                if (direct)
                {
                    targetSlot = schema.MaxField; // the dedicated "attack the player" slot
                }
                else
                {
                    HeadlessPlayerId defender = ReadPlayer(action, HeadlessActionParameterKeys.DefendingPlayerId, action.PlayerId);
                    targetSlot = positions.FieldIndex(defender, ReadId(action, HeadlessActionParameterKeys.AttackTargetId));
                    if (targetSlot < 0 || targetSlot >= schema.MaxField)
                    {
                        return (-1, "DeclareAttack");
                    }
                }

                return (schema.DeclareAttackOffset + (attacker * (schema.MaxField + 1)) + targetSlot, "DeclareAttack");
            }

            case HeadlessActionTypes.NormalizedResolveChoice:
            {
                if (ReadBool(action, HeadlessActionParameterKeys.ChoiceSkipped))
                {
                    return (schema.ResolveChoiceOffset + schema.MaxChoice, "ResolveChoice");
                }

                HeadlessEntityId first = FirstSelectedId(action);
                int candidate = first.IsEmpty ? -1 : positions.ChoiceIndex(first);
                return (LaneIndex(schema.ResolveChoiceOffset, candidate, schema.MaxChoice), "ResolveChoice");
            }

            default:
                return (-1, action.ActionType);
        }
    }

    private static int LaneIndex(int offset, int localSlot, int capacity)
    {
        return localSlot < 0 || localSlot >= capacity ? -1 : offset + localSlot;
    }

    private static HeadlessEntityId ReadId(LegalAction action, string key)
    {
        if (!action.Parameters.TryGetValue(key, out object? raw) || raw is null)
        {
            return default;
        }

        return raw switch
        {
            HeadlessEntityId entityId => entityId,
            string text when !string.IsNullOrWhiteSpace(text) => new HeadlessEntityId(text),
            _ => default
        };
    }

    private static HeadlessEntityId FirstSelectedId(LegalAction action)
    {
        if (action.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceSelectedIds, out object? raw) &&
            raw is IEnumerable<HeadlessEntityId> ids)
        {
            return ids.FirstOrDefault();
        }

        return default;
    }

    private static bool ReadBool(LegalAction action, string key)
    {
        return action.Parameters.TryGetValue(key, out object? raw) && raw is bool value && value;
    }

    private static HeadlessPlayerId ReadPlayer(LegalAction action, string key, HeadlessPlayerId fallback)
    {
        if (!action.Parameters.TryGetValue(key, out object? raw) || raw is null)
        {
            return fallback;
        }

        return raw switch
        {
            HeadlessPlayerId playerId => playerId,
            int intValue => new HeadlessPlayerId(intValue),
            long longValue when longValue >= int.MinValue && longValue <= int.MaxValue => new HeadlessPlayerId((int)longValue),
            string text when int.TryParse(text, out int parsed) => new HeadlessPlayerId(parsed),
            _ => fallback
        };
    }
}
