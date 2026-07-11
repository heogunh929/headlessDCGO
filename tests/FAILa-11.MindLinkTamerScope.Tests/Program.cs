using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// FAIL-a #11 (mapping remediation): PlayMindLinkTamerFromDigivolutionCards must mirror AS-IS —
// candidates come ONLY from THIS card's own digivolution stack (card.PermanentOfThisCard().DigivolutionCards),
// NOT every owner Digimon; the select is OPTIONAL (canNoSelect:true). The port previously scanned every owner
// Digimon's under-cards and was mandatory.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Candidates come only from THIS card's own stack (other Digimon's Tamer excluded)", SelfStackOnly),
    ("The select is optional (MinCount 0, CanSkip true)", OptionalSelect),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task SelfStackOnly()
{
    EngineContext ctx = Ctx();
    var ownTop = Battle(ctx, "OWNTOP", "OwnDigi");
    var ownTamer = Instance(ctx, "OWNTAMER", "MindTamer", "Tamer");
    SetSources(ctx, ownTop, ownTamer);
    var otherTop = Battle(ctx, "OTHERTOP", "OtherDigi");
    var otherTamer = Instance(ctx, "OTHERTAMER", "MindTamer", "Tamer");
    SetSources(ctx, otherTop, otherTamer);

    var eff = CardEffectFactory.PlayMindLinkTamerFromDigivolutionCards(new CardSource(ctx, ownTop, P1), "MindTamer", "");
    var req = ((ActivatedPlayFromUnderEffect)((ActivatedEffect)eff).Body).BuildRequest(new[] { P1, P2 });
    var ids = req.Candidates.Select(c => c.Id.Value).ToHashSet();

    AssertTrue(ids.Contains(ownTamer.Value), "own-stack Tamer is a candidate");
    AssertTrue(!ids.Contains(otherTamer.Value), "OTHER Digimon's Tamer is NOT a candidate (self-stack scope)");
    AssertEqual(1, req.Candidates.Count, "exactly one candidate (own stack only)");
    await Task.CompletedTask;
}

async Task OptionalSelect()
{
    EngineContext ctx = Ctx();
    var ownTop = Battle(ctx, "OWNTOP", "OwnDigi");
    var ownTamer = Instance(ctx, "OWNTAMER", "MindTamer", "Tamer");
    SetSources(ctx, ownTop, ownTamer);

    var eff = CardEffectFactory.PlayMindLinkTamerFromDigivolutionCards(new CardSource(ctx, ownTop, P1), "MindTamer", "");
    var req = ((ActivatedPlayFromUnderEffect)((ActivatedEffect)eff).Body).BuildRequest(new[] { P1, P2 });

    AssertEqual(0, req.MinCount, "optional -> MinCount 0");
    AssertTrue(req.CanSkip, "optional -> CanSkip true");
    await Task.CompletedTask;
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 911);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

HeadlessEntityId Battle(EngineContext ctx, string number, string name)
{
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, name,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:{number}:battle");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId Instance(EngineContext ctx, string number, string name, string cardType)
{
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, name,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: cardType));
    var id = new HeadlessEntityId($"p1:{number}:src");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    return id;
}

void SetSources(EngineContext ctx, HeadlessEntityId host, params HeadlessEntityId[] sources)
{
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec);
    ctx.CardInstanceRepository.Upsert(rec! with
    {
        Metadata = new Dictionary<string, object?>(rec!.Metadata, StringComparer.Ordinal)
        {
            [DigivolutionStackReader.SourceIdsKey] = sources.Select(s => s.Value).ToArray(),
        }
    });
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
