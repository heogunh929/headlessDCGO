using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// M2: 정보집합 관측 + 카드-ID 계약 테스트 (rl_development_roadmap M2 종료조건).
//  - 관측 슬롯 ↔ 액션 레인 정렬 불변식 (리뷰 🔴2): 관측 hand slot i == factored PlayCard 레인 slot i.
//  - choice 후보 정체: chooser에게만 노출, 다른 시점은 count-only 유지.
//  - 인코더 정보집합 가드: 룰상 본인도 모르는 존(Security/Library/DigitamaLibrary)은 per-card 인코딩 거부.
//  - 카드-ID vocab: canonical 규칙(Python dcgo_rl.cards와 동일) + append-only 확장.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Vocab canonical rule mirrors Python (hyphen/variant/promo)", () => Pure(VocabCanonicalParity)),
    ("Vocab extension is append-only", () => Pure(VocabExtendAppendOnly)),
    ("Unknown card number fails explicitly on encode", () => Pure(UnknownCardFailsExplicitly)),
    ("Hidden-to-owner zones are rejected for per-card encoding", () => Pure(HiddenZoneGuardThrows)),
    ("InformationSet encodes own hand identity, never security identity", () => Pure(InfoSetEncodesHandNotSecurity)),
    ("Digivolution under-card identities are encoded", () => Pure(UnderCardIdentityEncoded)),
    ("Choice candidate identity is encoded for the chooser", () => Pure(ChoiceCandidateIdentityEncoded)),
    ("Attack participants are matched to board slots by InstanceId", () => Pure(AttackSlotMatching)),
    ("Choice controller exposes candidate ids in request order", () => Pure(ControllerExposesCandidateIds)),
    ("Non-chooser perspective strips candidate ids, keeps count", ChoicePerspectiveStripInMatch),
    ("Observation hand slots align with factored hand lanes (live match)", HandLaneAlignmentInLiveMatch),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\n{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine($"\n{tests.Length} test(s) passed.");

static Task Pure(Action body)
{
    body();
    return Task.CompletedTask;
}

// --- Vocab ----------------------------------------------------------------

void VocabCanonicalParity()
{
    AssertEqual("BT1_020", CardVocabulary.CanonicalCardNumber(" bt1-020 "), "hyphen + trim + upper");
    AssertEqual("ST1_01", CardVocabulary.CanonicalCardNumber("ST1-01_P1"), "variant collapse");
    AssertEqual("BT10_005", CardVocabulary.CanonicalCardNumber("BT10_005_P0"), "P0 variant collapse");
    AssertEqual("P_016", CardVocabulary.CanonicalCardNumber("P-016"), "promo number is not a variant suffix");

    // 같은 카드는 같은 정체 (FR-3.3): 변형이 섞여 들어와도 1개 슬롯.
    CardVocabulary vocab = CardVocabulary.Build(new[] { "ST1-02", "ST1_02_P1", "st1-02" });
    AssertEqual(1, vocab.Count, "variants collapse to one vocab entry");
    AssertEqual(1, vocab.GetId("ST1_02"), "ids start at 1 (0 = PAD)");
}

void VocabExtendAppendOnly()
{
    CardVocabulary v1 = CardVocabulary.Build(new[] { "A_001", "B_002" }, version: "v1");
    CardVocabulary v2 = v1.Extend(new[] { "C_003", "A_001" }, version: "v2");

    AssertEqual(v1.GetId("A_001"), v2.GetId("A_001"), "existing id preserved");
    AssertEqual(v1.GetId("B_002"), v2.GetId("B_002"), "existing id preserved");
    AssertEqual(3, v2.GetId("C_003"), "new card appended after max id");
    AssertEqual(2, v1.Count, "original vocabulary untouched");
}

void UnknownCardFailsExplicitly()
{
    CardVocabulary vocab = CardVocabulary.Build(new[] { "ST1_02" });
    ObservationSnapshot snapshot = SyntheticSnapshot(
        p1Zones: new[] { HandZone(Card("h0", "ST9_999")) });

    var encoder = new ObservationEncoder(InfoSetOptions(vocab));
    AssertThrows<KeyNotFoundException>(() => encoder.Encode(snapshot), "unknown card must not map silently");
}

// --- Information-set guard --------------------------------------------------

void HiddenZoneGuardThrows()
{
    CardVocabulary vocab = CardVocabulary.Build(new[] { "ST1_02" });
    var options = InfoSetOptions(vocab) with
    {
        CardFeatureZones = new[] { ChoiceZone.Security }
    };

    ObservationSnapshot snapshot = SyntheticSnapshot(
        p1Zones: new[] { new ZoneObservation(ChoiceZone.Security, 1, new[] { Id("s0") }, new[] { Card("s0", "ST1_02") }) });

    AssertThrows<InvalidOperationException>(
        () => new ObservationEncoder(options).Encode(snapshot),
        "security per-card encoding must be rejected");
}

void InfoSetEncodesHandNotSecurity()
{
    CardVocabulary vocab = CardVocabulary.Build(new[] { "ST1_02", "ST1_03" });
    ObservationSnapshot snapshot = SyntheticSnapshot(
        p1Zones: new[]
        {
            HandZone(Card("h0", "ST1_02"), Card("h1", "ST1_03")),
            // 스냅샷은 자기 시큐리티 정체를 노출하지만(엔진 원본 그대로), 인코더 정보집합이 걸러야 한다.
            new ZoneObservation(ChoiceZone.Security, 2, new[] { Id("s0"), Id("s1") },
                new[] { Card("s0", "ST1_02"), Card("s1", "ST1_03") })
        });

    EncodedObservation encoded = new ObservationEncoder(InfoSetOptions(vocab)).Encode(snapshot);
    var names = encoded.FeatureNames.ToHashSet(StringComparer.Ordinal);

    AssertEqual(
        vocab.GetId("ST1_02"),
        (int)FeatureValue(encoded, "player.1.zone.Hand.card.0.cardId"),
        "own hand slot 0 identity");
    AssertEqual(
        vocab.GetId("ST1_03"),
        (int)FeatureValue(encoded, "player.1.zone.Hand.card.1.cardId"),
        "own hand slot 1 identity");
    AssertTrue(!names.Any(n => n.Contains("zone.Security.card.", StringComparison.Ordinal)),
        "no per-card security features");
    AssertTrue(names.Contains("player.1.zone.Hand.identityOverflow"), "identity overflow surfaced");
    AssertEqual(2, (int)FeatureValue(encoded, "player.1.zone.Security.count"), "security stays count-only");
}

void UnderCardIdentityEncoded()
{
    CardVocabulary vocab = CardVocabulary.Build(new[] { "ST1_06", "ST1_03" });
    CardObservation top = Card("f0", "ST1_06") with
    {
        UnderCards = new[] { new StackedCard(Id("u0"), "ST1_03", StackRole.DigiEgg, 2, 0) }
    };
    ObservationSnapshot snapshot = SyntheticSnapshot(
        p1Zones: new[] { new ZoneObservation(ChoiceZone.BattleArea, 1, new[] { Id("f0") }, new[] { top }) });

    EncodedObservation encoded = new ObservationEncoder(InfoSetOptions(vocab)).Encode(snapshot);
    AssertEqual(1, (int)FeatureValue(encoded, "player.1.zone.BattleArea.card.0.under.0.present"), "under card present");
    AssertEqual(
        vocab.GetId("ST1_03"),
        (int)FeatureValue(encoded, "player.1.zone.BattleArea.card.0.under.0.cardId"),
        "under card identity");
}

void ChoiceCandidateIdentityEncoded()
{
    CardVocabulary vocab = CardVocabulary.Build(new[] { "ST1_02", "ST1_03" });
    HeadlessChoiceState choice = HeadlessChoiceState.Empty with
    {
        IsPending = true,
        PlayerId = P1,
        CandidateIds = new[] { Id("h1"), Id("missing") }
    };
    ObservationSnapshot snapshot = SyntheticSnapshot(
        p1Zones: new[] { HandZone(Card("h0", "ST1_02"), Card("h1", "ST1_03")) },
        choice: choice);

    EncodedObservation encoded = new ObservationEncoder(InfoSetOptions(vocab)).Encode(snapshot);
    AssertEqual(1, (int)FeatureValue(encoded, "choice.candidate.0.present"), "candidate 0 present");
    AssertEqual(1, (int)FeatureValue(encoded, "choice.candidate.0.known"), "candidate 0 resolved in snapshot");
    AssertEqual(
        vocab.GetId("ST1_03"),
        (int)FeatureValue(encoded, "choice.candidate.0.cardId"),
        "candidate 0 identity");
    AssertEqual(1, (int)FeatureValue(encoded, "choice.candidate.1.present"), "candidate 1 present");
    AssertEqual(0, (int)FeatureValue(encoded, "choice.candidate.1.known"), "unresolvable candidate stays unknown");
    AssertEqual(0, (int)FeatureValue(encoded, "choice.candidates.overflow"), "no overflow");
}

void AttackSlotMatching()
{
    CardVocabulary vocab = CardVocabulary.Build(new[] { "ST1_06", "ST2_06" });
    ObservationSnapshot snapshot = SyntheticSnapshot(
        p1Zones: new[]
        {
            new ZoneObservation(ChoiceZone.BattleArea, 2, new[] { Id("f0"), Id("f1") },
                new[] { Card("f0", "ST1_06"), Card("f1", "ST1_06") })
        },
        p2Zones: new[]
        {
            new ZoneObservation(ChoiceZone.BattleArea, 1, new[] { Id("e0") }, new[] { Card("e0", "ST2_06") })
        },
        attack: HeadlessAttackState.Empty with
        {
            IsPending = true,
            AttackerId = Id("f1"),
            TargetId = Id("e0")
        });

    EncodedObservation encoded = new ObservationEncoder(InfoSetOptions(vocab)).Encode(snapshot);
    AssertEqual(1, (int)FeatureValue(encoded, "attack.attacker.slot.known"), "attacker matched");
    AssertEqual(1, (int)FeatureValue(encoded, "attack.attacker.slot.playerId"), "attacker owner");
    AssertEqual(1, (int)FeatureValue(encoded, "attack.attacker.slot.index"), "attacker board slot");
    AssertEqual(2, (int)FeatureValue(encoded, "attack.target.slot.playerId"), "target owner");
    AssertEqual(0, (int)FeatureValue(encoded, "attack.target.slot.index"), "target board slot");
    AssertEqual(0, (int)FeatureValue(encoded, "attack.blocker.slot.known"), "no blocker");
}

// --- Choice candidate wiring (engine) ----------------------------------------

void ControllerExposesCandidateIds()
{
    var controller = new InMemoryHeadlessChoiceController();
    var request = new ChoiceRequest(
        ChoiceType.HandCard,
        P1,
        "pick",
        minCount: 1,
        maxCount: 1,
        canSkip: false,
        sourceZone: ChoiceZone.Hand,
        candidates: new[]
        {
            new ChoiceCandidate(Id("c0"), "c0", ChoiceZone.Hand, true),
            new ChoiceCandidate(Id("c1"), "c1", ChoiceZone.Hand, true)
        });

    HeadlessChoiceState state = controller.RequestChoice(request);
    AssertEqual(2, state.CandidateIds.Count, "candidate ids exposed");
    AssertEqual(Id("c0"), state.CandidateIds[0], "request order preserved (= ResolveChoice lane order)");
    AssertEqual(Id("c1"), state.CandidateIds[1], "request order preserved");

    controller.ClearChoice();
    AssertEqual(0, controller.Current.CandidateIds.Count, "cleared state has no candidates");
}

async Task ChoicePerspectiveStripInMatch()
{
    (DcgoMatch match, _) = await CreateStarterMatchAsync(seed: 11);

    IZoneStateReader zones = (IZoneStateReader)match.Context.ZoneMover;
    IReadOnlyList<HeadlessEntityId> hand = zones.GetCards(P1, ChoiceZone.Hand);
    AssertTrue(hand.Count >= 2, "P1 has hand cards to choose from");

    match.Context.ChoiceController.RequestChoice(new ChoiceRequest(
        ChoiceType.HandCard,
        P1,
        "discard one",
        minCount: 1,
        maxCount: 1,
        canSkip: false,
        sourceZone: ChoiceZone.Hand,
        candidates: hand.Take(2).Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, true)).ToArray()));

    HeadlessChoiceState chooserView = match.GetObservation(P1).Choice;
    HeadlessChoiceState opponentView = match.GetObservation(P2).Choice;
    HeadlessChoiceState godView = match.GetObservation().Choice;

    AssertEqual(2, chooserView.CandidateIds.Count, "chooser sees candidate ids");
    AssertEqual(0, opponentView.CandidateIds.Count, "non-chooser gets candidates stripped");
    AssertEqual(2, opponentView.CandidateCount, "candidate count survives the strip");
    AssertEqual(2, godView.CandidateIds.Count, "null perspective (debug) keeps full view");
}

// --- Alignment invariant (live match) ----------------------------------------

async Task HandLaneAlignmentInLiveMatch()
{
    (DcgoMatch match, _) = await CreateStarterMatchAsync(seed: 23);
    FactoredActionSchema schema = FactoredActionSchema.Default;
    var rng = new Random(23);
    var players = new[] { P1, P2 };

    int handChecks = 0, fieldChecks = 0;
    for (int step = 0; step < 400 && !match.IsTerminal(); step++)
    {
        HeadlessPlayerId? mover = players.FirstOrDefault(p => match.GetLegalActions(p).Count > 0);
        if (mover is not { } current || match.GetLegalActions(current).Count == 0)
        {
            break;
        }

        FactoredActionMask mask = match.EncodeFactoredActionMask(schema);
        ObservationSnapshot observation = match.GetObservation(current);

        foreach (FactoredAction placed in mask.Actions)
        {
            PlayerObservation? owner = observation.Players.FirstOrDefault(p => p.PlayerId == placed.Action.PlayerId);
            if (owner is null)
            {
                continue;
            }

            // 관측 hand slot i의 카드 == PlayCard/ActivateOption/SpecialPlay 레인 slot i의 대상 (리뷰 🔴2).
            int handLaneSlot = placed.Lane switch
            {
                "PlayCard" => placed.Index - schema.PlayCardOffset,
                "ActivateOption" => placed.Index - schema.ActivateOptionOffset,
                "SpecialPlay" => placed.Index - schema.SpecialPlayOffset,
                _ => -1
            };
            if (handLaneSlot >= 0 && placed.Action.PlayerId == current)
            {
                IReadOnlyList<CardObservation> handCards =
                    owner.FindZone(ChoiceZone.Hand)?.Cards ?? Array.Empty<CardObservation>();
                AssertTrue(handLaneSlot < handCards.Count, $"hand lane slot {handLaneSlot} exists in observation");
                AssertEqual(
                    ReadCardId(placed.Action, HeadlessActionParameterKeys.CardId),
                    handCards[handLaneSlot].InstanceId,
                    $"hand slot {handLaneSlot} identity == lane target ({placed.Lane})");
                handChecks++;
            }

            // DeclareAttack 공격자 슬롯 == 관측 BattleArea 슬롯.
            if (placed.Lane == "DeclareAttack" && placed.Action.PlayerId == current)
            {
                int local = placed.Index - schema.DeclareAttackOffset;
                int attackerSlot = local / (schema.MaxField + 1);
                IReadOnlyList<CardObservation> fieldCards =
                    owner.FindZone(ChoiceZone.BattleArea)?.Cards ?? Array.Empty<CardObservation>();
                AssertTrue(attackerSlot < fieldCards.Count, $"attacker slot {attackerSlot} exists in observation");
                AssertEqual(
                    ReadCardId(placed.Action, HeadlessActionParameterKeys.AttackerId),
                    fieldCards[attackerSlot].InstanceId,
                    $"field slot {attackerSlot} identity == attack lane attacker");
                fieldChecks++;
            }
        }

        IReadOnlyList<LegalAction> legal = match.GetLegalActions(current);
        await match.ApplyActionAsync(legal[rng.Next(legal.Count)]);
        await match.StepAsync();
    }

    AssertTrue(handChecks > 0, $"alignment exercised on hand lanes (checked {handChecks})");
    Console.WriteLine($"      (hand lane checks: {handChecks}, attack lane checks: {fieldChecks})");
}

// --- Helpers ------------------------------------------------------------------

async Task<(DcgoMatch Match, EngineContext Context)> CreateStarterMatchAsync(int seed)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);

    StarterDecks.StarterDeck d1 = StarterDecks.Get("ST1");
    StarterDecks.StarterDeck d2 = StarterDecks.Get("ST2");
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions)
        },
        firstPlayerId: P1);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    var match = new DcgoMatch(context, new EngineTrace(), actionLegality: new LegalActionSetValidator());
    await match.InitializeAsync(config);
    return (match, context);
}

ObservationEncodingOptions InfoSetOptions(CardVocabulary vocab)
{
    return ObservationEncodingOptions.InformationSet(vocab);
}

ObservationSnapshot SyntheticSnapshot(
    IReadOnlyList<ZoneObservation>? p1Zones = null,
    IReadOnlyList<ZoneObservation>? p2Zones = null,
    HeadlessChoiceState? choice = null,
    HeadlessAttackState? attack = null)
{
    return new ObservationSnapshot(
        StepIndex: 1,
        IsTerminal: false,
        PendingActionCount: 0,
        HasPendingEffects: false,
        CardInstanceCount: 0,
        RandomSeed: null,
        LastActionType: null,
        LastActionSucceeded: null,
        LastActionMessage: null,
        Turn: HeadlessTurnState.Empty,
        Choice: choice ?? HeadlessChoiceState.Empty,
        Attack: attack ?? HeadlessAttackState.Empty,
        Effects: HeadlessEffectState.Empty,
        Memory: HeadlessMemoryState.Default,
        Players: new[]
        {
            new PlayerObservation(P1, p1Zones ?? Array.Empty<ZoneObservation>()),
            new PlayerObservation(P2, p2Zones ?? Array.Empty<ZoneObservation>())
        });
}

ZoneObservation HandZone(params CardObservation[] cards)
{
    return new ZoneObservation(
        ChoiceZone.Hand,
        cards.Length,
        cards.Select(card => card.InstanceId).ToArray(),
        cards);
}

static CardObservation Card(string instanceId, string cardNumber)
{
    return new CardObservation(Id(instanceId), cardNumber, "Digimon", 1000, 3, 3, 1, false, true, 0);
}

static HeadlessEntityId Id(string value) => new(value);

static double FeatureValue(EncodedObservation encoded, string name)
{
    ObservationFeature? feature = encoded.Features.FirstOrDefault(f => f.Name == name);
    if (feature is null)
    {
        throw new InvalidOperationException($"feature '{name}' is missing from the encoded observation.");
    }

    return feature.Value;
}

static HeadlessEntityId ReadCardId(LegalAction action, string key)
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

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}

static void AssertThrows<TException>(Action body, string label)
    where TException : Exception
{
    try
    {
        body();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"{label}: expected {typeof(TException).Name}.");
}
