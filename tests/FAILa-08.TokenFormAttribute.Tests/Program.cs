using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #8 (mapping remediation, data-fidelity part): a played token must carry its AS-IS Form and Attribute
// (ContinuousController.CreateTokenData) — Diaboromon = Mega / Unknown, Gyuukimon = Ultimate / Virus. The port
// previously never emitted forms/attributes to the token's card record, so a form/attribute-querying effect
// would see nothing. (The AS-IS empty-frame UI-slot guard is a separate field-size-model question, not here.)

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Diaboromon token carries forms=[Mega], attributes=[Unknown]", () => Check("Diaboromon", "Mega", "Unknown")),
    ("Gyuukimon token carries forms=[Ultimate], attributes=[Virus]", () => Check("Gyuukimon", "Ultimate", "Virus")),
    ("Amon token (no form/attr in spec) has neither", () => Check("Amon", null, null)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Check(string tokenKey, string? expectForm, string? expectAttr)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 908);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // A source card owned by P1 to play the token from.
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("SRC"), "SRC", "Src", new Dictionary<string, object?>(), CardType: "Digimon"));
    var srcId = new HeadlessEntityId("p1:battle:SRC");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, new HeadlessEntityId("SRC"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, srcId, ChoiceZone.None, ChoiceZone.BattleArea));

    var spec = CardEffectCommons.TokenSpecs[tokenKey];
    IReadOnlyList<HeadlessEntityId> played = await CardEffectCommons.PlayToken(spec, new CardSource(ctx, srcId, P1), isOwnerPermanent: true, isTapped: false);
    AssertTrue(played.Count == 1, "one token played");

    ctx.CardInstanceRepository.TryGetInstance(played[0], out CardInstanceRecord? inst);
    ctx.CardRepository.TryGetCard(inst!.DefinitionId, out CardRecord? def);
    AssertEqual(expectForm, First(def!, "forms"), $"{tokenKey} forms");
    AssertEqual(expectAttr, First(def!, "attributes"), $"{tokenKey} attributes");
}

static string? First(CardRecord def, string key) =>
    def.Metadata.TryGetValue(key, out object? raw) && raw is string[] arr && arr.Length > 0 ? arr[0] : null;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
