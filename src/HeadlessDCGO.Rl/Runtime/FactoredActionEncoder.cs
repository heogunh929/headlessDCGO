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
    // v2 (B5-3, 설계 §B5.7): +1 Confirm slot appended LAST (599→600 at default capacities); every
    // pre-existing offset is unchanged. The 16 ResolveChoice candidate slots double as the
    // multi-select session's toggle lanes (설계 핀 1) — same index shape, context-dependent meaning
    // resolved deterministically from the pending-choice state (see MapAction).
    public const int Version = 2;

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
        // D-6: single-slot lanes for the breeding-step decisions (appended last to keep prior offsets stable).
        HatchDigitamaOffset = offset; offset += 1;
        MoveBreedingOffset = offset; offset += 1;
        // (G8-006) Special play (DigiXros / DNA / Blast) — one slot per hand card (the recipe selects the
        // materials). Appended last to keep all prior offsets stable.
        SpecialPlayOffset = offset; offset += maxHand;
        // (B5-3, 설계 §B5.5/§B5.7) Multi-select session Confirm — ONE slot: the session's Confirm action
        // (a ResolveChoice carrying the current partial set) is a single lane regardless of which
        // candidates are picked; the per-candidate structure lives on the toggle lanes (= the reused
        // ResolveChoice candidate slots). Appended last so every v1 offset stays stable.
        ConfirmChoiceOffset = offset; offset += 1;
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

    public int SpecialPlayOffset { get; }

    public int ConfirmChoiceOffset { get; }

    public int HatchDigitamaOffset { get; }

    public int MoveBreedingOffset { get; }

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
        IReadOnlyList<HeadlessEntityId> choiceCandidates,
        bool multiSelectSessionActive = false)
    {
        _zoneResolver = zoneResolver ?? throw new ArgumentNullException(nameof(zoneResolver));
        _choiceCandidates = choiceCandidates ?? throw new ArgumentNullException(nameof(choiceCandidates));
        MultiSelectSessionActive = multiSelectSessionActive;
    }

    /// <summary>
    /// (B5-3, 설계 핀 1) True while the pending choice runs as a multi-select partial-selection
    /// session — the dispatcher's session-open condition (<c>Type != Count &amp;&amp; MaxCount &gt; 1</c>,
    /// HeadlessLegalActionDispatcher.BuildChoiceResolutionActions) recomputed from the same state, so
    /// the candidate-lane meaning ("size-1 resolution" vs "session toggle") and the ResolveChoice
    /// mapping ("candidate lane" vs "Confirm slot") are deterministic from the choice state alone.
    /// </summary>
    public bool MultiSelectSessionActive { get; }

    public static FactoredPositionContext FromContext(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ChoiceRequest? request = context.ChoiceController.PendingRequest;
        IReadOnlyList<HeadlessEntityId> candidates = request is not null
            ? request.Candidates.Select(candidate => candidate.Id).ToArray()
            : Array.Empty<HeadlessEntityId>();

        // Mirror of the dispatcher's session-open boundary (B5-2): non-Count, MaxCount>1. The
        // unsatisfiable-forced demotion (§B5.6) never opens a pending choice, so a pending request
        // matching this shape IS a live session.
        bool sessionActive = request is not null &&
            request.Type != ChoiceType.Count &&
            request.MaxCount > 1;

        Func<HeadlessPlayerId, ChoiceZone, IReadOnlyList<HeadlessEntityId>> resolver =
            context.ZoneMover is IZoneStateReader zones
                ? zones.GetCards
                : static (_, _) => Array.Empty<HeadlessEntityId>();

        return new FactoredPositionContext(resolver, candidates, sessionActive);
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

            // D-6: breeding-step decisions occupy fixed single slots.
            case HeadlessActionTypes.NormalizedHatchDigitama:
                return (schema.HatchDigitamaOffset, "HatchDigitama");
            case HeadlessActionTypes.NormalizedMoveBreedingToBattle:
                return (schema.MoveBreedingOffset, "MoveBreedingToBattle");

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

            // (G8-006) Special play is indexed by the played (top) card's hand slot; the recipe selects the
            // materials, so no separate material encoding is needed.
            case HeadlessActionTypes.NormalizedSpecialPlay:
            {
                int hand = positions.HandIndex(action.PlayerId, ReadId(action, HeadlessActionParameterKeys.CardId));
                return (LaneIndex(schema.SpecialPlayOffset, hand, schema.MaxHand), "SpecialPlay");
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

            // (B5-3, 설계 핀 1) A session toggle occupies the candidate's ResolveChoice lane slot — the
            // candidate lane doubles as the toggle lane. No collision with ResolveChoice mappings: while
            // a session is active the only non-skip ResolveChoice on the table is the Confirm, which maps
            // to its own dedicated slot below.
            case HeadlessActionTypes.NormalizedToggleChoiceCandidate:
            {
                int candidate = positions.ChoiceIndex(ReadId(action, HeadlessActionParameterKeys.ChoiceCandidateId));
                return (LaneIndex(schema.ResolveChoiceOffset, candidate, schema.MaxChoice), "ToggleChoiceCandidate");
            }

            case HeadlessActionTypes.NormalizedResolveChoice:
            {
                if (ReadBool(action, HeadlessActionParameterKeys.ChoiceSkipped))
                {
                    return (schema.ResolveChoiceOffset + schema.MaxChoice, "ResolveChoice");
                }

                // (B5-3) While a multi-select session is active, the dispatcher's only non-skip
                // ResolveChoice is the Confirm carrying the CURRENT partial set — a single action per
                // state, so it gets the single dedicated Confirm slot (설계 §B5.5 Confirm lane). Outside a
                // session the lane keeps its v1 meaning: a complete size-1 resolution on the candidate's
                // slot. The two readings never overlap because the session flag is derived from the same
                // pending-choice state the dispatcher enumerates from (deterministic, 설계 핀 1).
                if (positions.MultiSelectSessionActive)
                {
                    return (schema.ConfirmChoiceOffset, "ConfirmChoice");
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
