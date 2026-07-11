using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// B-5 (P1-8): the per-shape activated effects (select / buff / …) are now composable IEffectBody bodies of the
// uniform ActivatedEffect, so the SHARED once-per-turn cap + optional yes/no gate applies to them. Before B-5 the
// resolver's per-shape select CASE had no cap and no optional prompt (and those cases are now removed — every
// per-shape effect must flow through the uniform gate). These tests exercise a per-shape SELECT body (Mode.Tap =
// suspend) wrapped as a uniform ActivatedEffect and prove: (a) a maxCountPerTurn:1 cap suspends only ONCE per turn
// and resets next turn; (b) an isOptional:true "you may" declines to a no-op and accepts to run the body.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("a CAPPED per-shape select body suspends only once per turn (cap gate now applies), and resets next turn", CappedSelectCapsPerTurn),
    ("an OPTIONAL per-shape select body: declining is a no-op; accepting runs the select", OptionalSelectDeclineAndAccept),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task CappedSelectCapsPerTurn()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 85);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var self = await Declarer(context, "TfxCappedSelectSuspend");
    var t1 = await Foe(context, "T1");
    var t2 = await Foe(context, "T2");

    // (1) First OptionSkill resolve: suspends the chosen opponent Digimon, consuming the once-per-turn cap.
    Enqueue(context, ChoiceResult.Select(t1));
    await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OptionSkill);
    AssertTrue(IsSuspended(context, t1), "1st resolve suspends the chosen opponent Digimon");

    // (2) Second resolve THIS turn: the cap is spent, so the resolver never offers the select (no choice is even
    // requested — nothing enqueued). Before B-5 the per-shape select had no cap and would suspend t2.
    await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OptionSkill);
    AssertTrue(!IsSuspended(context, t2), "2nd resolve is CAPPED — the per-shape body honours the once-per-turn cap");

    // (3) A new turn resets the per-turn cap (AS-IS InitUseCountThisTurn) — the select fires again.
    context.OnceFlags.ResetForTurn(turnSequence: 1, turnPlayerId: P1);
    Enqueue(context, ChoiceResult.Select(t2));
    await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OptionSkill);
    AssertTrue(IsSuspended(context, t2), "after cap reset the select fires again (proving it was the cap, not another gate)");
}

async Task OptionalSelectDeclineAndAccept()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 86);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var self = await Declarer(context, "TfxOptionalSelectSuspend");
    var t1 = await Foe(context, "T1");

    // (1) DECLINE: nothing enqueued -> the optional yes/no request (canSkip:true) falls back to Skip -> the effect
    // does nothing. Before B-5 the per-shape select could not present this "you may" gate at all.
    await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OptionSkill);
    AssertTrue(!IsSuspended(context, t1), "declining the optional 'you may' is a no-op (no suspend)");

    // (2) ACCEPT: answer the optional yes/no (its single candidate is the effect's stable EffectId), then the select.
    var effectId = new HeadlessEntityId($"{self.Value}:ae:{EffectTiming.None}:{nameof(ActivatedSelectEffect)}");
    Enqueue(context, ChoiceResult.Select(effectId));   // yes, use the optional effect
    Enqueue(context, ChoiceResult.Select(t1));          // then pick the target
    await ActivatedEffectResolver.ResolveAsync(context, self, P1, EffectTiming.OptionSkill);
    AssertTrue(IsSuspended(context, t1), "accepting the optional then selecting runs the per-shape body");
}

// --- Helpers -------------------------------------------------------------

async Task<HeadlessEntityId> Declarer(EngineContext context, string fixtureNumber)
{
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"{fixtureNumber}def"), fixtureNumber, "SELF",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var self = new HeadlessEntityId("1:battle:SELF");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(self, new HeadlessEntityId($"{fixtureNumber}def"), P1,
        Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, self, ChoiceZone.None, ChoiceZone.BattleArea));
    return self;
}

async Task<HeadlessEntityId> Foe(EngineContext context, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"FOE{tag}def"), $"FOE{tag}", tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"2:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"FOE{tag}def"), P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

void Enqueue(EngineContext context, ChoiceResult choice) =>
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(choice);

bool IsSuspended(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
    && inst.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
