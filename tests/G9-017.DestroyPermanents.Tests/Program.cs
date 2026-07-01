using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// BT-PRE-A3 (G9-017): DestroyPermanentsEffect — the headless mirror of the original DestroyPermanentsClass
// ("directly delete this pre-computed list of permanents"). Resolved through the activation flow, it stages a
// Delete sink mutation per target; the sink's centralised gates (opponent immunity, cannotBeDeleted /
// deletion-prevention) filter. TfxDestroy targets all of the opponent's battle-area Digimon.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Delete all opponent Digimon: both foes trashed, own ally untouched", DeleteAllOpponents),
    ("A cannotBeDeleted foe is NOT deleted (centralised deletion-prevention)", PreventedFoeSurvives),
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

// --- Tests ---------------------------------------------------------------

async Task DeleteAllOpponents()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "TfxDestroy", "SRC", dp: 4000, prevented: false);
    var ally = await Place(context, P1, "ALLY", "ALLY", dp: 4000, prevented: false);
    var foe1 = await Place(context, P2, "FOE1", "FOE1", dp: 3000, prevented: false);
    var foe2 = await Place(context, P2, "FOE2", "FOE2", dp: 5000, prevented: false);

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, foe1), "foe1 deleted");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, foe2), "foe2 deleted");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, foe1) && !InZone(context, P2, ChoiceZone.BattleArea, foe2), "both foes left the battle area");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, ally), "own ally untouched (opponent-only target list)");
}

async Task PreventedFoeSurvives()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "TfxDestroy", "SRC", dp: 4000, prevented: false);
    var foe = await Place(context, P2, "FOE", "FOE", dp: 3000, prevented: true); // cannotBeDeleted

    await ActivatedEffectResolver.ResolveAsync(context, src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, foe), "cannotBeDeleted foe stayed on the battle area");
    AssertTrue(!InZone(context, P2, ChoiceZone.Trash, foe), "cannotBeDeleted foe was NOT trashed");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 917);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string cardNumber, string tag, int dp, bool prevented)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(cardNumber == "TfxDestroy" ? "TfxDestroy" : $"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 }, CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false };
    if (prevented)
    {
        meta["cannotBeDeleted"] = true;
    }

    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

bool InZone(EngineContext context, HeadlessPlayerId p, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(p, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
