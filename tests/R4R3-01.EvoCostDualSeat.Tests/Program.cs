using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// (RD-R3-01) Dual-seat digivolution-cost equivalence witness. Review 3 P1-1: the legal-action table seat
// (DigivolveAction -> DigivolutionCostHelpers.ReadRequirements) scanned only the {digivolutionCosts,
// evolutionCosts, evoCosts} metadata keys while the loader stores the printed conditions as
// metadata["evolutionConditions"] / CardRecord.EvolutionCondition — every condition card degraded to the
// Any(EvolutionCost) fallback, so multi-path cards (BT10_026 = Blue@4:4 / Blue@5:2) got the WRONG cost in
// the table. The remediation makes DigivolutionCostHelpers.ParseEvolutionCondition the single canonical
// token parser consumed by BOTH seats (helper requirement table AND mirror CardSource.PrintedEvoCosts).
// This witness pins:
//   1. the token grammar itself (multi-token, prefixes, :Cost fallback, identity tokens);
//   2. FULL-CORPUS: helper ReadRequirements == an independent re-parse of the embedded cards.json;
//   3. FULL-CORPUS dual-seat behaviour: for every card x adversarial target shape, the helper cost table
//      (Evaluate matched set / min) == the mirror printed seat (CardSource.CostList);
//   4. BT10_026 behaviour: the action table declares cost 2 onto a Blue Lv.5 and cost 4 onto a Blue Lv.4;
//   5. identity-token ("definition:X") behaviour end to end.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ParseEvolutionCondition grammar pin (tokens, prefixes, :Cost fallback, identity)", GrammarPin),
    ("FULL CORPUS: helper ReadRequirements == independent cards.json re-parse", CorpusParsePin),
    ("FULL CORPUS: helper cost table == mirror CardSource.CostList (dual-seat behaviour)", CorpusDualSeat),
    ("BT10_026: Blue Lv.5 target -> declared cost 2 in the action table, digivolve green", Bt10026Lv5Cost2),
    ("BT10_026: Blue Lv.4 target -> declared cost 4; Red Lv.5 -> no action; claimed 4 onto Lv.5 rejected", Bt10026OtherPaths),
    ("identity token (definition:X): gated on the target definition, printed EvolutionCost applies", IdentityToken),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- 1. grammar pin -------------------------------------------------------

Task GrammarPin()
{
    // BT10_026 shape: two Color@Level:Cost tokens, ';' separated.
    var multi = DigivolutionCostHelpers.ParseEvolutionCondition("Blue@4:4;Blue@5:2", 4);
    AssertEqual(2, multi.Count, "two tokens parse");
    AssertReq(multi[0], "Blue", 4, 4, null, "token 0 = Blue@4:4");
    AssertReq(multi[1], "Blue", 5, 2, null, "token 1 = Blue@5:2");

    // no :Cost suffix -> the printed EvolutionCost (?? 0) applies.
    var fallback = DigivolutionCostHelpers.ParseEvolutionCondition("Red@3", 2);
    AssertEqual(1, fallback.Count, "single token parses");
    AssertReq(fallback[0], "Red", 3, 2, null, "cost falls back to printed EvolutionCost");
    var fallbackZero = DigivolutionCostHelpers.ParseEvolutionCondition("Red@3", null);
    AssertReq(fallbackZero[0], "Red", 3, 0, null, "cost falls back to 0 without a printed EvolutionCost");

    // ',' and '|' separators + whitespace trim (same split set as mirror/MatchesEvolutionCondition).
    var separators = DigivolutionCostHelpers.ParseEvolutionCondition(" Red@3:1 , Blue@4:2 | Green@5:3 ", null);
    AssertEqual(3, separators.Count, "',' ';' '|' all separate tokens");
    AssertReq(separators[2], "Green", 5, 3, null, "third separated token");

    // definition:/from: prefixes strip; a non-Color@Level token is an identity condition at printed cost.
    var identity = DigivolveParse("definition:Agumon;from:BT1_010;Greymon", 3);
    AssertEqual(3, identity.Count, "three identity tokens");
    AssertReq(identity[0], null, null, 3, "Agumon", "definition: prefix stripped");
    AssertReq(identity[1], null, null, 3, "BT1_010", "from: prefix stripped");
    AssertReq(identity[2], null, null, 3, "Greymon", "bare identity token");

    // empty/whitespace conditions parse to nothing.
    AssertEqual(0, DigivolveParse(null, 3).Count, "null condition -> empty");
    AssertEqual(0, DigivolveParse("  ", 3).Count, "whitespace condition -> empty");
    return Task.CompletedTask;

    static IReadOnlyList<DigivolutionCostRequirement> DigivolveParse(string? condition, int? printed) =>
        DigivolutionCostHelpers.ParseEvolutionCondition(condition, printed);
}

// --- 2. full-corpus parse pin ---------------------------------------------

Task CorpusParsePin()
{
    // cards.json carries duplicate cardNumbers (alt-art reprints); the loader Upserts in order, so the LAST
    // occurrence wins in the CardDatabase — mirror that here for both sides of the comparison.
    List<CardDto> dtos = ReadCorpusDtos()
        .Where(dto => !string.IsNullOrWhiteSpace(dto.CardNumber))
        .GroupBy(dto => dto.CardNumber, StringComparer.Ordinal)
        .Select(group => group.Last())
        .ToList();
    var records = new Dictionary<string, CardRecord>(StringComparer.Ordinal);
    foreach (CardRecord record in CardBaseEntityLoader.ReadEmbedded())
    {
        records[record.CardNumber] = record;
    }

    AssertTrue(dtos.Count > 8000, $"corpus loaded ({dtos.Count} cards)");

    int conditionCards = 0, multiConditionCards = 0, anyFallbackCards = 0;
    var mismatches = new List<string>();
    foreach (CardDto dto in dtos)
    {
        if (string.IsNullOrWhiteSpace(dto.CardNumber) || !records.TryGetValue(dto.CardNumber, out CardRecord? record))
        {
            continue;
        }

        IReadOnlyList<DigivolutionCostRequirement> actual = DigivolutionCostHelpers.ReadRequirements(record);
        List<string> actualKeys = actual
            .Select(req => $"{req.TargetColor ?? "*"}@{req.TargetLevel?.ToString() ?? "*"}:{req.MemoryCost}:{req.TargetIdentity ?? "-"}")
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        List<string> expectedKeys;
        if (dto.EvolutionConditions is { Count: > 0 })
        {
            conditionCards++;
            if (dto.EvolutionConditions.Count > 1) multiConditionCards++;
            expectedKeys = dto.EvolutionConditions
                .Select(cond => $"{cond.Color}@{cond.Level}:{cond.Cost}:-")
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
        }
        else if (dto.EvolutionCost is int printed)
        {
            anyFallbackCards++;
            expectedKeys = new List<string> { $"*@*:{printed}:-" };
        }
        else
        {
            expectedKeys = new List<string>();
        }

        if (!actualKeys.SequenceEqual(expectedKeys, StringComparer.Ordinal))
        {
            mismatches.Add($"{dto.CardNumber}: expected [{string.Join(", ", expectedKeys)}] got [{string.Join(", ", actualKeys)}]");
        }
    }

    Console.WriteLine($"  corpus: {dtos.Count} cards; {conditionCards} with printed conditions ({multiConditionCards} multi-path); {anyFallbackCards} Any-fallback; {mismatches.Count} mismatches");
    AssertTrue(conditionCards > 1000, "printed-condition cards present in corpus");
    AssertTrue(multiConditionCards > 1000, "multi-path cards present in corpus");
    if (mismatches.Count > 0)
    {
        throw new InvalidOperationException($"{mismatches.Count} parse mismatches; first: {mismatches[0]}");
    }

    return Task.CompletedTask;
}

// --- 3. full-corpus dual-seat behaviour ------------------------------------

Task CorpusDualSeat()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 301);
    var cards = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(cards);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);

    IReadOnlyList<CardRecord> corpus = CardBaseEntityLoader.ReadEmbedded();

    // Adversarial target shapes per card: each printed (color, level) exactly; level +1 / -1; an off-color at
    // the same level; and universal probes for Any-fallback / no-cost cards.
    var shapeSet = new HashSet<(string Color, int Level)>();
    var perCardShapes = new Dictionary<string, List<(string Color, int Level)>>(StringComparer.Ordinal);
    foreach (CardRecord record in corpus)
    {
        var shapes = new List<(string Color, int Level)>();
        foreach (DigivolutionCostRequirement req in DigivolutionCostHelpers.ReadRequirements(record))
        {
            if (req.TargetColor is not { } color || req.TargetLevel is not { } level)
            {
                continue;
            }

            string altColor = string.Equals(color, "Red", StringComparison.Ordinal) ? "Blue" : "Red";
            shapes.Add((color, level));
            shapes.Add((color, level + 1));
            if (level > 0) shapes.Add((color, level - 1));
            shapes.Add((altColor, level));
        }

        shapes.Add(("Red", 3));
        shapes.Add(("Blue", 5));
        List<(string, int)> distinct = shapes.Distinct().ToList();
        perCardShapes[record.CardNumber] = distinct!;
        foreach (var shape in distinct) shapeSet.Add(shape);
    }

    // One off-zone target instance per shape (the mirror Permanent view evaluates the printed gates off the
    // instance/definition; an empty field keeps the added-requirement scans silent).
    var shapeTargets = new Dictionary<(string, int), (HeadlessEntityId InstanceId, CardRecord Definition)>();
    foreach ((string color, int level) in shapeSet)
    {
        var defId = new HeadlessEntityId($"ZZTGT_{color}_{level}");
        var record = new CardRecord(defId, defId.Value, defId.Value,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["color"] = color,
                ["colors"] = new[] { color },
                ["level"] = level,
                ["dp"] = 3000,
            },
            CardType: "Digimon");
        cards.Upsert(record);
        var instanceId = new HeadlessEntityId($"zztgt:{color}:{level}");
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(instanceId, defId, P1,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level, ["dp"] = 3000 }));
        shapeTargets[(color, level)] = (instanceId, record);
    }

    // The probe definition is a per-card CLONE without effectClass/dispatch identity, so the mirror seat
    // carries ONLY the printed path (no ported IAddDigivolutionRequirementEffect noise) — the exact printed
    // dual-seat comparison this witness pins. EvolutionCondition/EvolutionCost/metadata ride along 1:1.
    var probeDefId = new HeadlessEntityId("ZZPROBE");
    var probeInstanceId = new HeadlessEntityId("p1:hand:zzprobe");

    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
    long comparisons = 0;
    var mismatches = new List<string>();
    foreach (CardRecord record in corpus)
    {
        var probeMetadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in record.Metadata)
        {
            if (!string.Equals(pair.Key, "effectClass", StringComparison.Ordinal))
            {
                probeMetadata[pair.Key] = pair.Value;
            }
        }

        var probeDef = new CardRecord(probeDefId, probeDefId.Value, record.Name, probeMetadata,
            CardType: record.CardType,
            PlayCost: record.PlayCost,
            EvolutionCost: record.EvolutionCost,
            EvolutionCondition: record.EvolutionCondition);
        cards.Upsert(probeDef);
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(probeInstanceId, probeDefId, P1,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));

        Dictionary<string, int> costById = DigivolutionCostHelpers.ReadRequirements(probeDef)
            .ToDictionary(req => req.Id, req => req.MemoryCost, StringComparer.Ordinal);
        var mirrorCard = new Cec.CardSource(context, probeInstanceId, P1, P1);

        foreach ((string color, int level) in perCardShapes[record.CardNumber])
        {
            (HeadlessEntityId targetInstanceId, CardRecord targetDef) = shapeTargets[(color, level)];

            // helper seat: matched requirement multiset + min (the DigivolveAction cost table).
            DigivolutionCostResult helperResult = DigivolutionCostHelpers.Evaluate(new DigivolutionCostRequest(probeDef, targetDef));
            List<int> helperCosts = helperResult.Values.TryGetValue("matchedRequirementIds", out object? idsRaw) && idsRaw is string[] ids
                ? ids.Select(id => costById[id]).OrderBy(cost => cost).ToList()
                : new List<int>();

            // mirror seat: the printed CostList (empty field + effect-free probe => printed paths only).
            var targetPermanent = new Cec.Permanent(context, targetInstanceId, P1);
            List<int> mirrorCosts = mirrorCard.CostList(targetPermanent, ignoreLevel: false, checkAvailability: false)
                .OrderBy(cost => cost)
                .ToList();

            comparisons++;
            bool setsEqual = helperCosts.SequenceEqual(mirrorCosts);
            bool outcomesEqual = helperResult.IsSuccess == (mirrorCosts.Count > 0);
            bool minEqual = !helperResult.IsSuccess || (mirrorCosts.Count > 0 && helperResult.Cost == mirrorCosts[0]);
            if (!setsEqual || !outcomesEqual || !minEqual)
            {
                mismatches.Add(
                    $"{record.CardNumber} vs {color}@{level}: helper [{string.Join(",", helperCosts)}] success={helperResult.IsSuccess} cost={helperResult.Cost}; mirror [{string.Join(",", mirrorCosts)}]");
                if (mismatches.Count >= 10)
                {
                    break;
                }
            }
        }

        if (mismatches.Count >= 10)
        {
            break;
        }
    }

    Console.WriteLine($"  dual-seat: {corpus.Count} cards, {comparisons} (card x target-shape) comparisons, {mismatches.Count} mismatches");
    if (mismatches.Count > 0)
    {
        throw new InvalidOperationException($"dual-seat mismatch ({mismatches.Count}+): {mismatches[0]}");
    }

    return Task.CompletedTask;
}

// --- 4. BT10_026 behaviour --------------------------------------------------

async Task Bt10026Lv5Cost2()
{
    EngineContext context = LoadedContext();
    context.MemoryController.Set(10);
    HeadlessEntityId target = await PlaceBase(context, "B5BASE", color: "Blue", level: 5);
    HeadlessEntityId evo = await PlaceHand(context, "BT10_026");

    // the legal-action table must declare the Lv.5 path cost (Blue@5:2), not the Lv.4 cost or EvolutionCost.
    int declared = DeclaredCost(context, evo, target);
    AssertEqual(2, declared, "action table declares the Blue@5 path cost 2");

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);
    AssertTrue(result.IsSuccess, $"digivolve onto Blue Lv.5 at declared cost 2 succeeds ({result.Message})");
}

async Task Bt10026OtherPaths()
{
    // Blue Lv.4 -> the Blue@4:4 path.
    EngineContext context = LoadedContext();
    context.MemoryController.Set(10);
    HeadlessEntityId lv4 = await PlaceBase(context, "B4BASE", color: "Blue", level: 4);
    HeadlessEntityId evo = await PlaceHand(context, "BT10_026");
    AssertEqual(4, DeclaredCost(context, evo, lv4), "action table declares the Blue@4 path cost 4");

    // Red Lv.5 -> no printed path, no action.
    EngineContext context2 = LoadedContext();
    context2.MemoryController.Set(10);
    HeadlessEntityId red5 = await PlaceBase(context2, "R5BASE", color: "Red", level: 5);
    HeadlessEntityId evo2 = await PlaceHand(context2, "BT10_026");
    AssertTrue(FindDigivolve(context2, evo2, red5) is null, "no digivolve action onto a Red Lv.5");

    // claimed cost 4 onto the Lv.5 target contradicts the declared Blue@5 cost 2 -> rejected.
    EngineContext context3 = LoadedContext();
    context3.MemoryController.Set(10);
    HeadlessEntityId lv5 = await PlaceBase(context3, "B5BASE", color: "Blue", level: 5);
    HeadlessEntityId evo3 = await PlaceHand(context3, "BT10_026");
    ActionProcessResult wrongClaim = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo3, lv5, memoryCost: 4), context3);
    AssertTrue(!wrongClaim.IsSuccess, "claiming the Lv.4 cost onto the Lv.5 target is rejected");
}

// --- 5. identity token -------------------------------------------------------

async Task IdentityToken()
{
    EngineContext context = LoadedContext();
    context.MemoryController.Set(10);
    var cards = (CardDatabase)context.CardRepository;

    HeadlessEntityId host = await PlaceBase(context, "ZZHOSTDEF", color: "Red", level: 4);
    HeadlessEntityId other = await PlaceBase(context, "ZZOTHERDEF", color: "Red", level: 4);

    var evoDefId = new HeadlessEntityId("ZZEVOID");
    cards.Upsert(new CardRecord(evoDefId, evoDefId.Value, evoDefId.Value,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" }, ["level"] = 5, ["dp"] = 6000 },
        CardType: "Digimon",
        EvolutionCost: 3,
        EvolutionCondition: "definition:ZZHOSTDEF"));
    var evo = new HeadlessEntityId("p1:hand:ZZEVOID");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(evo, evoDefId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, evo, ChoiceZone.None, ChoiceZone.Hand));

    AssertEqual(3, DeclaredCost(context, evo, host), "identity token: printed EvolutionCost 3 onto the named definition");
    AssertTrue(FindDigivolve(context, evo, other) is null, "identity token: no action onto a different definition");

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, host, memoryCost: 3), context);
    AssertTrue(result.IsSuccess, $"identity-token digivolve succeeds at printed cost ({result.Message})");
}

// --- helpers -----------------------------------------------------------------

EngineContext LoadedContext()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 926);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceBase(EngineContext context, string tag, string color, int level)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["color"] = color, ["colors"] = new[] { color }, ["level"] = level, ["dp"] = 3000 },
        CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level, ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceHand(EngineContext context, string definitionId)
{
    var id = new HeadlessEntityId($"p1:hand:{definitionId}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(definitionId), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

LegalAction? FindDigivolve(EngineContext context, HeadlessEntityId cardId, HeadlessEntityId targetId)
{
    foreach (LegalAction action in new DigivolveAction().GetLegalActions(context, P1))
    {
        if (DigivolveActionPayload.TryRead(action, out DigivolveActionPayload? payload, out _)
            && payload!.CardId == cardId
            && payload.TargetCardId == targetId)
        {
            return action;
        }
    }

    return null;
}

int DeclaredCost(EngineContext context, HeadlessEntityId cardId, HeadlessEntityId targetId)
{
    LegalAction action = FindDigivolve(context, cardId, targetId)
        ?? throw new InvalidOperationException($"no digivolve action for {cardId} -> {targetId}");
    AssertTrue(DigivolveActionPayload.TryRead(action, out DigivolveActionPayload? payload, out _), "digivolve payload readable");
    return payload!.MemoryCost;
}

static List<CardDto> ReadCorpusDtos()
{
    Assembly assembly = typeof(CardBaseEntityLoader).Assembly;
    string resource = assembly.GetManifestResourceNames().First(name => name.EndsWith("cards.json", StringComparison.Ordinal));
    using Stream stream = assembly.GetManifestResourceStream(resource)!;
    return JsonSerializer.Deserialize<List<CardDto>>(stream) ?? new List<CardDto>();
}

static void AssertReq(
    DigivolutionCostRequirement requirement,
    string? color, int? level, int cost, string? identity, string label)
{
    AssertEqual(color ?? "-", requirement.TargetColor ?? "-", $"{label}: color");
    AssertEqual(level?.ToString() ?? "-", requirement.TargetLevel?.ToString() ?? "-", $"{label}: level");
    AssertEqual(cost, requirement.MemoryCost, $"{label}: cost");
    AssertEqual(identity ?? "-", requirement.TargetIdentity ?? "-", $"{label}: identity");
}

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

sealed class CardDto
{
    [JsonPropertyName("cardNumber")] public string CardNumber { get; set; } = string.Empty;
    [JsonPropertyName("evolutionCost")] public int? EvolutionCost { get; set; }
    [JsonPropertyName("evolutionConditions")] public List<CardConditionDto> EvolutionConditions { get; set; } = new();
}

sealed class CardConditionDto
{
    [JsonPropertyName("color")] public string Color { get; set; } = string.Empty;
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("cost")] public int Cost { get; set; }
}
