using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Card group CardEffect/BT14/Black — ported card tests (group-standard project).
// 카드를 포팅하면 아래 tests 배열에 sub-test를 추가한다 (패턴: tests/CardEffect.ST7.Red.Tests).
// 최소 단언: Register(...) 후 효과가 라이브인지 — 키워드는 ContinuousKeywordGate.HasKeyword,
// 수정자는 ContinuousModifierGate.ResolveDp/ResolveSecurityAttack, 등록 수는 RegisterOnEnterPlay 반환 Count.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // ("<ID>: <효과> is live", () => Pure(MyTest)),
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

static Task Pure(Action body) { body(); return Task.CompletedTask; }

// --- Helpers (카드-공용) ---------------------------------------------------

EngineContext Board(string cardNumber, out HeadlessEntityId cardId, string cardType = "Digimon", int? level = null)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (level is int lv) { meta["level"] = lv; }
    cards.Upsert(new CardRecord(new HeadlessEntityId(cardNumber), cardNumber, cardNumber, meta, CardType: cardType));
    cardId = new HeadlessEntityId($"p1:battle:{cardNumber}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(cardId, new HeadlessEntityId(cardNumber), P1));
    return context;
}

IReadOnlyList<EffectBinding> Register(EngineContext context, CEntity_Effect effect, string cardNumber, HeadlessEntityId cardId) =>
    CardEffectRegistrar.RegisterOnEnterPlay(context, effect, cardNumber, new CardSource(context, cardId, P1));

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }
}
