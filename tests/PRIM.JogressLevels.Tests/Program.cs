// PRIM special-play: Jogress by levels — AddJogressLevelsEffect makes a card count as extra level(s) when used
// as a Jogress/DNA material against the digivolving card (AS-IS AddJogressLevelsClass). BT20_025: "also treated
// as level 6 when the digivolving card is Examon". Verified via CardSource.JogressLevelsAgainst.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Action Body)[]
{
    ("printed level only when no AddJogressLevels effect is active", PrintedOnly),
    ("gains the extra level when the digivolving card matches (Examon)", GainsWhenMatch),
    ("does NOT gain the extra level when the digivolving card does not match", NoGainWhenNoMatch),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

void PrintedOnly()
{
    EngineContext ctx = Ctx();
    var mat = Card(ctx, "MAT", level: 4);
    var examon = Card(ctx, "Examon", level: 7);
    var levels = new CardSource(ctx, mat, P1).JogressLevelsAgainst(new CardSource(ctx, examon, P1));
    AssertSeq(new[] { 4 }, levels, "printed level 4 only (no effect registered)");
}

void GainsWhenMatch()
{
    EngineContext ctx = Ctx();
    var mat = Card(ctx, "MAT", level: 4);
    var examon = Card(ctx, "Examon", level: 7);
    RegisterJogressLevels(ctx, mat, jc => jc.CardNames.Contains("Examon") ? new[] { 6 } : Array.Empty<int>());
    var levels = new CardSource(ctx, mat, P1).JogressLevelsAgainst(new CardSource(ctx, examon, P1));
    AssertSeq(new[] { 4, 6 }, levels, "printed 4 + treated-as 6 for Examon");
}

void NoGainWhenNoMatch()
{
    EngineContext ctx = Ctx();
    var mat = Card(ctx, "MAT", level: 4);
    var other = Card(ctx, "Other", level: 7);
    RegisterJogressLevels(ctx, mat, jc => jc.CardNames.Contains("Examon") ? new[] { 6 } : Array.Empty<int>());
    var levels = new CardSource(ctx, mat, P1).JogressLevelsAgainst(new CardSource(ctx, other, P1));
    AssertSeq(new[] { 4 }, levels, "printed 4 only (digivolving card is not Examon)");
}

// --- Harness ---
EngineContext Ctx() => EngineContext.CreateDefault(randomSeed: 74);
HeadlessEntityId Card(EngineContext ctx, string cardNumber, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{cardNumber}"), cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:{cardNumber}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{cardNumber}"), P1, Metadata: new Dictionary<string, object?>()));
    return id;
}
void RegisterJogressLevels(EngineContext ctx, HeadlessEntityId matId, Func<CardSource, IReadOnlyList<int>> getLevels)
{
    var effect = CardEffectFactory.AddJogressLevelsEffect(new CardSource(ctx, matId, P1), getLevels);
    ctx.EffectRegistry.Register(effect.ToBinding($"{matId.Value}:jogresslevels"));
}
void AssertSeq(int[] expected, IReadOnlyList<int> actual, string label)
{
    if (!expected.OrderBy(x => x).SequenceEqual(actual.OrderBy(x => x)))
        throw new InvalidOperationException($"{label}: expected [{string.Join(",", expected)}], actual [{string.Join(",", actual)}].");
}
