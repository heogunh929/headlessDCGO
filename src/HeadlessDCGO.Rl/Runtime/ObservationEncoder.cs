namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

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

            // GPT-#2: the RNG seed is a debug/diagnostic field, not a learnable signal — an
            // unnormalized, episode-arbitrary integer that only adds noise / leaks determinism to the
            // policy. Excluded from the default trainer observation; opt in via IncludeRandomSeed.
            if (_options.IncludeRandomSeed)
            {
                features.Add(new ObservationFeature("runtime.randomSeed.known", Bool(snapshot.RandomSeed.HasValue)));
                features.Add(new ObservationFeature("runtime.randomSeed", snapshot.RandomSeed ?? 0));
            }

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

        // (M2, 설계 §5-2) Candidate identity for the pending choice — candidate order matches the
        // ResolveChoice factored lane (both come from the choice request's candidate list).
        if (_options.IncludeChoiceCandidates)
        {
            AddChoiceCandidateFeatures(features, snapshot);
        }

        if (_options.IncludeAttackState)
        {
            AddAttackFeatures(features, snapshot.Attack);
        }

        // (M2, 설계 §5-4) Attack participants matched to board slots via CardObservation.InstanceId.
        if (_options.IncludeAttackSlots)
        {
            AddAttackSlotFeatures(features, snapshot);
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

    // 룰상 카드 주인 본인도 정체·순서를 모르는 존(정보집합 밖 — 설계 §5 정정). 스냅샷은 자기
    // 히든존을 노출하므로, per-card 정체 인코딩 요청이 들어오면 조용히 새는 대신 즉시 실패한다.
    private static readonly ChoiceZone[] HiddenToOwnerZones =
    {
        ChoiceZone.Library,
        ChoiceZone.Security,
        ChoiceZone.DigitamaLibrary
    };

    // G3.5-RL-A4b: fixed per-card feature slots for visible cards in the configured zones.
    // Empty slots are zero-filled so the vector stays a fixed size.
    // (M2) Per-zone capacity overrides + card-identity (vocab id) + digivolution-source identity.
    private void AddCardFeatures(
        List<ObservationFeature> features,
        string playerPrefix,
        PlayerObservation player)
    {
        foreach (ChoiceZone zone in _options.CardFeatureZones)
        {
            if (HiddenToOwnerZones.Contains(zone))
            {
                throw new InvalidOperationException(
                    $"Zone {zone} is hidden to its owner by the game rules; per-card identity encoding " +
                    "would leak information outside the agent's information set (design §5).");
            }

            ZoneObservation? zoneSnapshot = player.FindZone(zone);
            IReadOnlyList<CardObservation> cards = zoneSnapshot?.Cards ?? Array.Empty<CardObservation>();
            int capacity = ZoneCapacity(zone);

            for (int slot = 0; slot < capacity; slot++)
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

                if (_options.Vocabulary is not null)
                {
                    features.Add(new ObservationFeature($"{prefix}.cardId", CardIdOf(card)));
                }

                if (_options.IncludeStackIdentity)
                {
                    AddUnderCardFeatures(features, prefix, card);
                }
            }

            // No silent caps: identities beyond the slot capacity are not encoded — surface how many.
            features.Add(new ObservationFeature(
                $"{playerPrefix}.zone.{zone}.identityOverflow",
                Math.Max(0, cards.Count - capacity)));
        }
    }

    private void AddUnderCardFeatures(
        List<ObservationFeature> features,
        string cardPrefix,
        CardObservation? card)
    {
        IReadOnlyList<StackedCard> underCards = card?.UnderCards ?? Array.Empty<StackedCard>();
        for (int index = 0; index < _options.MaxStackPerCard; index++)
        {
            StackedCard? under = index < underCards.Count ? underCards[index] : null;
            string prefix = $"{cardPrefix}.under.{index}";

            features.Add(new ObservationFeature($"{prefix}.present", Bool(under is not null)));
            features.Add(new ObservationFeature($"{prefix}.level", under?.Level ?? 0));
            if (_options.Vocabulary is not null)
            {
                features.Add(new ObservationFeature(
                    $"{prefix}.cardId",
                    under is null || under.CardNumber.Length == 0 ? 0 : _options.Vocabulary.GetId(under.CardNumber)));
            }
        }
    }

    private void AddChoiceCandidateFeatures(
        List<ObservationFeature> features,
        ObservationSnapshot snapshot)
    {
        IReadOnlyList<HeadlessEntityId> candidates = snapshot.Choice.CandidateIds;
        Dictionary<HeadlessEntityId, CardObservation> lookup = BuildCardLookup(snapshot);

        for (int index = 0; index < _options.MaxChoiceCandidates; index++)
        {
            HeadlessEntityId? id = index < candidates.Count ? candidates[index] : null;
            CardObservation? card = null;
            if (id is { } candidateId && lookup.TryGetValue(candidateId, out CardObservation? found))
            {
                card = found;
            }

            string prefix = $"choice.candidate.{index}";
            features.Add(new ObservationFeature($"{prefix}.present", Bool(id is not null)));
            features.Add(new ObservationFeature($"{prefix}.known", Bool(card is not null)));
            features.Add(new ObservationFeature($"{prefix}.level", card?.Level ?? 0));
            features.Add(new ObservationFeature($"{prefix}.dp", card?.Dp ?? 0));
            if (_options.Vocabulary is not null)
            {
                features.Add(new ObservationFeature($"{prefix}.cardId", CardIdOf(card)));
            }
        }

        features.Add(new ObservationFeature(
            "choice.candidates.overflow",
            Math.Max(0, candidates.Count - _options.MaxChoiceCandidates)));
    }

    private static void AddAttackSlotFeatures(
        List<ObservationFeature> features,
        ObservationSnapshot snapshot)
    {
        AddAttackParticipantSlot(features, "attack.attacker", snapshot, snapshot.Attack.AttackerId);
        AddAttackParticipantSlot(features, "attack.target", snapshot, snapshot.Attack.TargetId);
        AddAttackParticipantSlot(features, "attack.blocker", snapshot, snapshot.Attack.BlockerId);
    }

    private static void AddAttackParticipantSlot(
        List<ObservationFeature> features,
        string name,
        ObservationSnapshot snapshot,
        HeadlessEntityId? participantId)
    {
        int playerValue = -1;
        int slot = -1;

        if (participantId is { } id)
        {
            foreach (PlayerObservation player in snapshot.Players)
            {
                IReadOnlyList<CardObservation> cards =
                    player.FindZone(ChoiceZone.BattleArea)?.Cards ?? Array.Empty<CardObservation>();
                for (int index = 0; index < cards.Count; index++)
                {
                    if (cards[index].InstanceId == id)
                    {
                        playerValue = player.PlayerId.Value;
                        slot = index;
                        break;
                    }
                }

                if (slot >= 0)
                {
                    break;
                }
            }
        }

        features.Add(new ObservationFeature($"{name}.slot.known", Bool(slot >= 0)));
        features.Add(new ObservationFeature($"{name}.slot.playerId", playerValue));
        features.Add(new ObservationFeature($"{name}.slot.index", slot));
    }

    private static Dictionary<HeadlessEntityId, CardObservation> BuildCardLookup(ObservationSnapshot snapshot)
    {
        Dictionary<HeadlessEntityId, CardObservation> lookup = new();
        foreach (PlayerObservation player in snapshot.Players)
        {
            foreach (ZoneObservation zone in player.Zones)
            {
                foreach (CardObservation card in zone.Cards)
                {
                    lookup[card.InstanceId] = card;
                }
            }
        }

        return lookup;
    }

    private double CardIdOf(CardObservation? card)
    {
        if (_options.Vocabulary is null || card is null || card.CardNumber.Length == 0)
        {
            return CardVocabulary.PadId;
        }

        return _options.Vocabulary.GetId(card.CardNumber);
    }

    private int ZoneCapacity(ChoiceZone zone)
    {
        return _options.CardCapacityOverrides is not null &&
               _options.CardCapacityOverrides.TryGetValue(zone, out int capacity)
            ? capacity
            : _options.MaxCardsPerZone;
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

    /// <summary>
    /// (M2, 설계 §5) 정보집합(information-set) 인코딩 프리셋: 본인 손패·양쪽 공개존(BattleArea/
    /// Breeding/Trash) per-card 정체 + 카드-ID(vocab) + choice 후보 정체 + attack 슬롯 매칭 +
    /// 진화 스택 소재. 본인 시큐리티/덱은 count-only 유지(룰상 모르는 정보).
    /// 손패/필드 슬롯 용량은 factored 스키마와 정렬된다(관측 슬롯 ↔ 액션 레인 불변식).
    /// 상대 손패 정체는 시점 필터가 스냅샷 단계에서 비우므로 슬롯은 zero-fill로 남는다 —
    /// 반드시 좌석 시점(perspective) 스냅샷과 함께 쓸 것.
    /// </summary>
    public static ObservationEncodingOptions InformationSet(
        CardVocabulary vocabulary,
        FactoredActionSchema? schema = null)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        FactoredActionSchema effective = schema ?? FactoredActionSchema.Default;

        return new ObservationEncodingOptions
        {
            Vocabulary = vocabulary,
            CardFeatureZones = new[]
            {
                ChoiceZone.Hand,
                ChoiceZone.BattleArea,
                ChoiceZone.BreedingArea,
                ChoiceZone.Trash
            },
            CardCapacityOverrides = new Dictionary<ChoiceZone, int>
            {
                [ChoiceZone.Hand] = effective.MaxHand,        // 관측 손패 슬롯 i == PlayCard 레인 i
                [ChoiceZone.BattleArea] = effective.MaxField, // 관측 필드 슬롯 i == Digivolve/Attack 레인 i
                [ChoiceZone.BreedingArea] = 2,
                [ChoiceZone.Trash] = 16
            },
            IncludeChoiceCandidates = true,
            MaxChoiceCandidates = effective.MaxChoice,
            IncludeAttackSlots = true,
            IncludeStackIdentity = true
        };
    }

    public bool IncludeStepIndex { get; init; } = true;

    public bool IncludeRuntimeFlags { get; init; } = true;

    /// <summary>(GPT-#2) Include the raw RNG seed in the observation. Off by default — the seed is a
    /// debug/diagnostic field, not a learnable feature. Enable for debugging/full observations only.</summary>
    public bool IncludeRandomSeed { get; init; }

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

    // ---- (M2) 정보집합 인코딩 옵션 — 기본값은 전부 기존 동작 유지(무회귀) ----

    /// <summary>카드-ID 임베딩용 vocab. null이면 cardId 피처를 내지 않는다(기존 스탯-only 벡터).</summary>
    public CardVocabulary? Vocabulary { get; init; }

    /// <summary>존별 per-card 슬롯 용량 오버라이드(미지정 존은 MaxCardsPerZone). 손패/필드는
    /// factored 스키마 용량과 일치시켜야 관측 슬롯 ↔ 액션 레인 정렬이 성립한다.</summary>
    public IReadOnlyDictionary<ChoiceZone, int>? CardCapacityOverrides { get; init; }

    /// <summary>(설계 §5-2) pending choice 후보 정체 인코딩.</summary>
    public bool IncludeChoiceCandidates { get; init; }

    public int MaxChoiceCandidates { get; init; } = 16;

    /// <summary>(설계 §5-4) attack 참여 카드(공격자/타깃/블록커)의 보드 슬롯 매칭 인코딩.</summary>
    public bool IncludeAttackSlots { get; init; }

    /// <summary>(설계 §5-3) 진화 스택 소재 정체 인코딩.</summary>
    public bool IncludeStackIdentity { get; init; }

    public int MaxStackPerCard { get; init; } = 6;
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
