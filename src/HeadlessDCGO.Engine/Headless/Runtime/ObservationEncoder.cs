namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;

// TODO: Replace this simple feature encoder with the final RL tensor schema.
public sealed class ObservationEncoder(ObservationEncodingOptions? options = null)
{
    private readonly ObservationEncodingOptions _options = options ?? ObservationEncodingOptions.Default;

    public EncodedObservation Encode(ObservationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        List<ObservationFeature> features = new();

        if (_options.IncludeStepIndex)
        {
            features.Add(new ObservationFeature("runtime.stepIndex", snapshot.StepIndex));
        }

        if (_options.IncludeRuntimeFlags)
        {
            features.Add(new ObservationFeature("runtime.isTerminal", Bool(snapshot.IsTerminal)));
            features.Add(new ObservationFeature("runtime.pendingActionCount", EncodeCount(snapshot.PendingActionCount)));
            features.Add(new ObservationFeature("runtime.hasPendingEffects", Bool(snapshot.HasPendingEffects)));
            features.Add(new ObservationFeature("runtime.cardInstanceCount", EncodeCount(snapshot.CardInstanceCount)));
            features.Add(new ObservationFeature("runtime.randomSeed.known", Bool(snapshot.RandomSeed.HasValue)));
            features.Add(new ObservationFeature("runtime.randomSeed", snapshot.RandomSeed ?? 0));
            features.Add(new ObservationFeature("runtime.lastActionSucceeded.known", Bool(snapshot.LastActionSucceeded.HasValue)));
            features.Add(new ObservationFeature("runtime.lastActionSucceeded.value", Bool(snapshot.LastActionSucceeded == true)));
        }

        if (_options.IncludePlayerCount)
        {
            features.Add(new ObservationFeature("runtime.playerCount", snapshot.PlayerCount));
        }

        if (_options.IncludeTurnState)
        {
            AddTurnFeatures(features, snapshot.Turn);
        }

        if (_options.IncludeChoiceState)
        {
            AddChoiceFeatures(features, snapshot.Choice);
        }

        if (_options.IncludeAttackState)
        {
            AddAttackFeatures(features, snapshot.Attack);
        }

        if (_options.IncludeEffectState)
        {
            AddEffectFeatures(features, snapshot.Effects);
        }

        if (_options.IncludeMemoryState)
        {
            AddMemoryFeatures(features, snapshot.Memory);
        }

        if (_options.IncludePlayerZoneCounts)
        {
            foreach (PlayerObservation player in snapshot.Players)
            {
                AddPlayerFeatures(features, player);
            }
        }

        return new EncodedObservation(features.ToArray());
    }

    private static void AddTurnFeatures(
        List<ObservationFeature> features,
        HeadlessTurnState turn)
    {
        features.Add(new ObservationFeature("turn.number", turn.TurnNumber));
        features.Add(new ObservationFeature("turn.phaseIndex", (int)turn.Phase));
        features.Add(new ObservationFeature("turn.isFirstTurn", Bool(turn.IsFirstTurn)));
        features.Add(new ObservationFeature("turn.playerId.known", Bool(turn.TurnPlayerId.HasValue)));
        features.Add(new ObservationFeature("turn.playerId", turn.TurnPlayerId?.Value ?? -1));
        features.Add(new ObservationFeature("turn.nonTurnPlayerId.known", Bool(turn.NonTurnPlayerId.HasValue)));
        features.Add(new ObservationFeature("turn.nonTurnPlayerId", turn.NonTurnPlayerId?.Value ?? -1));

        foreach (HeadlessPhase phase in HeadlessPhaseMapping.ObservationPhaseOrder)
        {
            features.Add(new ObservationFeature(
                $"turn.phase.{phase}",
                Bool(turn.Phase == phase)));
        }
    }

    private static void AddChoiceFeatures(
        List<ObservationFeature> features,
        HeadlessChoiceState choice)
    {
        features.Add(new ObservationFeature("choice.isPending", Bool(choice.IsPending)));
        features.Add(new ObservationFeature("choice.isResolved", Bool(choice.IsResolved)));
        features.Add(new ObservationFeature("choice.isSkipped", Bool(choice.IsSkipped)));
        features.Add(new ObservationFeature("choice.typeIndex", (int)choice.Type));
        features.Add(new ObservationFeature("choice.playerId.known", Bool(choice.PlayerId.HasValue)));
        features.Add(new ObservationFeature("choice.playerId", choice.PlayerId?.Value ?? -1));
        features.Add(new ObservationFeature("choice.minCount", choice.MinCount));
        features.Add(new ObservationFeature("choice.maxCount", choice.MaxCount));
        features.Add(new ObservationFeature("choice.canSkip", Bool(choice.CanSkip)));
        features.Add(new ObservationFeature("choice.sourceZoneIndex", (int)choice.SourceZone));
        features.Add(new ObservationFeature("choice.candidateCount", choice.CandidateCount));
        features.Add(new ObservationFeature("choice.selectedCount.known", Bool(choice.SelectedCount.HasValue)));
        features.Add(new ObservationFeature("choice.selectedCount", choice.SelectedCount ?? choice.SelectedIds.Count));
        features.Add(new ObservationFeature("choice.selectedIds.count", choice.SelectedIds.Count));
    }

    private static void AddAttackFeatures(
        List<ObservationFeature> features,
        HeadlessAttackState attack)
    {
        features.Add(new ObservationFeature("attack.count", attack.AttackCount));
        features.Add(new ObservationFeature("attack.isPending", Bool(attack.IsPending)));
        features.Add(new ObservationFeature("attack.isResolved", Bool(attack.IsResolved)));
        features.Add(new ObservationFeature("attack.isDirect", Bool(attack.IsDirectAttack)));
        features.Add(new ObservationFeature("attack.isBlocked", Bool(attack.IsBlocked)));
        features.Add(new ObservationFeature("attack.attackingPlayerId.known", Bool(attack.AttackingPlayerId.HasValue)));
        features.Add(new ObservationFeature("attack.attackingPlayerId", attack.AttackingPlayerId?.Value ?? -1));
        features.Add(new ObservationFeature("attack.defendingPlayerId.known", Bool(attack.DefendingPlayerId.HasValue)));
        features.Add(new ObservationFeature("attack.defendingPlayerId", attack.DefendingPlayerId?.Value ?? -1));
        features.Add(new ObservationFeature("attack.attackerId.known", Bool(attack.AttackerId.HasValue)));
        features.Add(new ObservationFeature("attack.targetId.known", Bool(attack.TargetId.HasValue)));
        features.Add(new ObservationFeature("attack.blockerId.known", Bool(attack.BlockerId.HasValue)));
    }

    private static void AddEffectFeatures(
        List<ObservationFeature> features,
        HeadlessEffectState effects)
    {
        features.Add(new ObservationFeature("effects.pendingCount", effects.PendingCount));
        features.Add(new ObservationFeature("effects.hasPending", Bool(effects.HasPendingEffects)));
        features.Add(new ObservationFeature("effects.totalEnqueued", effects.TotalEnqueuedCount));
        features.Add(new ObservationFeature("effects.totalResolved", effects.TotalResolvedCount));
        features.Add(new ObservationFeature("effects.lastResolvedCount", effects.LastResolvedCount));
        features.Add(new ObservationFeature("effects.totalUnbound", effects.TotalUnboundCount));
    }

    private void AddMemoryFeatures(
        List<ObservationFeature> features,
        HeadlessMemoryState memory)
    {
        double value = _options.NormalizeMemory
            ? NormalizeMemory(memory.Current, memory)
            : memory.Current;

        features.Add(new ObservationFeature("memory.current", value));
        features.Add(new ObservationFeature("memory.rawCurrent", memory.Current));
        features.Add(new ObservationFeature("memory.minimum", memory.Minimum));
        features.Add(new ObservationFeature("memory.maximum", memory.Maximum));
        features.Add(new ObservationFeature("memory.isAtMinimum", Bool(memory.Current <= memory.Minimum)));
        features.Add(new ObservationFeature("memory.isAtMaximum", Bool(memory.Current >= memory.Maximum)));
    }

    private void AddPlayerFeatures(
        List<ObservationFeature> features,
        PlayerObservation player)
    {
        string playerPrefix = $"player.{player.PlayerId.Value}";

        if (_options.IncludePlayerTotalCardCount)
        {
            features.Add(new ObservationFeature($"{playerPrefix}.totalCardCount", EncodeCount(player.TotalCardCount)));
        }

        foreach (ChoiceZone zone in _options.ZoneOrder)
        {
            ZoneObservation? zoneSnapshot = player.FindZone(zone);
            int count = zoneSnapshot?.Count ?? 0;

            features.Add(new ObservationFeature(
                $"{playerPrefix}.zone.{zone}.count",
                EncodeCount(count)));
        }

        if (_options.IncludeCardFeatures)
        {
            AddCardFeatures(features, playerPrefix, player);
        }
    }

    // G3.5-RL-A4b: fixed per-card feature slots for visible cards in the configured zones.
    // Empty slots are zero-filled so the vector stays a fixed size.
    private void AddCardFeatures(
        List<ObservationFeature> features,
        string playerPrefix,
        PlayerObservation player)
    {
        foreach (ChoiceZone zone in _options.CardFeatureZones)
        {
            ZoneObservation? zoneSnapshot = player.FindZone(zone);
            IReadOnlyList<CardObservation> cards = zoneSnapshot?.Cards ?? Array.Empty<CardObservation>();

            for (int slot = 0; slot < _options.MaxCardsPerZone; slot++)
            {
                CardObservation? card = slot < cards.Count ? cards[slot] : null;
                string prefix = $"{playerPrefix}.zone.{zone}.card.{slot}";

                features.Add(new ObservationFeature($"{prefix}.present", Bool(card is not null)));
                features.Add(new ObservationFeature($"{prefix}.dp", card?.Dp ?? 0));
                features.Add(new ObservationFeature($"{prefix}.level", card?.Level ?? 0));
                features.Add(new ObservationFeature($"{prefix}.playCost", card?.PlayCost ?? 0));
                features.Add(new ObservationFeature($"{prefix}.evolutionCost", card?.EvolutionCost ?? 0));
                features.Add(new ObservationFeature($"{prefix}.suspended", Bool(card?.IsSuspended ?? false)));
                features.Add(new ObservationFeature($"{prefix}.stackDepth", card?.StackDepth ?? 0));
                features.Add(new ObservationFeature($"{prefix}.isDigimon", Bool(IsType(card, "Digimon"))));
                features.Add(new ObservationFeature($"{prefix}.isTamer", Bool(IsType(card, "Tamer"))));
                features.Add(new ObservationFeature($"{prefix}.isOption", Bool(IsType(card, "Option"))));
            }
        }
    }

    private static bool IsType(CardObservation? card, string cardType)
    {
        return card is not null && string.Equals(card.CardType, cardType, StringComparison.OrdinalIgnoreCase);
    }

    private double EncodeCount(int count)
    {
        if (!_options.NormalizeCounts)
        {
            return count;
        }

        double normalizer = _options.CountNormalizer <= 0 ? 1 : _options.CountNormalizer;
        return count / normalizer;
    }

    private static double NormalizeMemory(
        int value,
        HeadlessMemoryState memory)
    {
        int range = memory.Maximum - memory.Minimum;
        if (range <= 0)
        {
            return 0d;
        }

        return (double)(value - memory.Minimum) / range;
    }

    private static double Bool(bool value)
    {
        return value ? 1d : 0d;
    }
}

public sealed record ObservationEncodingOptions
{
    // NOTE: DefaultZoneOrder must be declared before Default. Static field initializers run in
    // textual order, and Default's instance initializer reads DefaultZoneOrder for its ZoneOrder.
    public static IReadOnlyList<ChoiceZone> DefaultZoneOrder { get; } = new[]
    {
        ChoiceZone.Library,
        ChoiceZone.Hand,
        ChoiceZone.Security,
        ChoiceZone.Trash,
        ChoiceZone.Clock,
        ChoiceZone.Recollection,
        ChoiceZone.Execution,
        ChoiceZone.DigivolutionCards,
        ChoiceZone.LinkedCards,
        ChoiceZone.BattleArea,
        ChoiceZone.BreedingArea,
        ChoiceZone.DigitamaLibrary
    };

    // Zones whose visible cards get per-card feature encoding (G3.5-RL-A4b). Defaults to the field.
    // Declared before Default for static-init ordering (Default reads it).
    public static IReadOnlyList<ChoiceZone> DefaultCardFeatureZones { get; } = new[]
    {
        ChoiceZone.BattleArea
    };

    public static ObservationEncodingOptions Default { get; } = new();

    public bool IncludeStepIndex { get; init; } = true;

    public bool IncludeRuntimeFlags { get; init; } = true;

    public bool IncludePlayerCount { get; init; } = true;

    public bool IncludeTurnState { get; init; } = true;

    public bool IncludeChoiceState { get; init; } = true;

    public bool IncludeAttackState { get; init; } = true;

    public bool IncludeEffectState { get; init; } = true;

    public bool IncludeMemoryState { get; init; } = true;

    public bool IncludePlayerZoneCounts { get; init; } = true;

    public bool IncludePlayerTotalCardCount { get; init; } = true;

    public bool NormalizeCounts { get; init; }

    public bool NormalizeMemory { get; init; }

    public double CountNormalizer { get; init; } = 60d;

    public IReadOnlyList<ChoiceZone> ZoneOrder { get; init; } = DefaultZoneOrder;

    // G3.5-RL-A4b: per-card feature encoding for visible cards.
    public bool IncludeCardFeatures { get; init; } = true;

    public IReadOnlyList<ChoiceZone> CardFeatureZones { get; init; } = DefaultCardFeatureZones;

    public int MaxCardsPerZone { get; init; } = 8;
}

public sealed record ObservationFeature(string Name, double Value);

public sealed record EncodedObservation(IReadOnlyList<ObservationFeature> Features)
{
    public static EncodedObservation Empty { get; } = new(Array.Empty<ObservationFeature>());

    public int Length => Features.Count;

    public IReadOnlyList<string> FeatureNames => Features.Select(feature => feature.Name).ToArray();

    public double[] ToVector()
    {
        return Features.Select(feature => feature.Value).ToArray();
    }
}
