using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #10 (mapping remediation): PlaySelfDigimonAfterBattleSecurityEffect must mirror AS-IS — the [Security]
// effect does NOT play immediately; it registers an OnEndBattle trigger that plays the card AT THE END OF THE
// BATTLE, and (when deleteDigimon != UntilEndBattle) marks the played Digimon for a turn-end self-delete.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
string OnEndBattle = TriggerTimings.OnEndBattle;

var tests = new (string Name, Func<Task> Body)[]
{
    ("[Security] resolution DEFERS: card not played immediately, OnEndBattle trigger registered", DefersToEndOfBattle),
    ("OnEndBattle trigger plays the card from security to the battle area", TriggerPlays),
    ("deleteDigimon=UntilOwnerTurnEnd marks the played Digimon for turn-end self-delete (\"own\")", DeleteMarker),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task DefersToEndOfBattle()
{
    EngineContext ctx = Ctx();
    var card = await Sec(ctx, "DIG");
    var eff = (PlaySelfAtEndOfBattleSecurityEffect)CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(new CardSource(ctx, card, P1));
    await ApplySecurity(ctx, eff);

    AssertTrue(!InBattle(ctx, card), "card NOT played immediately (deferred to end of battle)");
    AssertTrue(InZone(ctx, card, ChoiceZone.Security), "card still in security after the [Security] resolution");
    AssertTrue(((EffectRegistry)ctx.EffectRegistry).GetEffects(card, OnEndBattle).Count > 0, "an OnEndBattle trigger was registered");
}

async Task TriggerPlays()
{
    EngineContext ctx = Ctx();
    var card = await Sec(ctx, "DIG");
    await ResolveTrigger(ctx, new PlaySelfAtEndOfBattleTriggerEffect(new CardSource(ctx, card, P1), deleteTiming: null));
    AssertTrue(InBattle(ctx, card), "card played into the battle area at end of battle");
}

async Task DeleteMarker()
{
    EngineContext ctx = Ctx();
    var card = await Sec(ctx, "DIG");
    await ResolveTrigger(ctx, new PlaySelfAtEndOfBattleTriggerEffect(new CardSource(ctx, card, P1), deleteTiming: "own"));
    AssertTrue(InBattle(ctx, card), "card played");
    ctx.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? rec);
    string? marker = rec!.Metadata.TryGetValue(GameFlowProcessor.DeleteAtTurnEndKey, out object? m) ? m?.ToString() : null;
    AssertEqual("own", marker, "played Digimon marked for own-turn-end self-delete");
}

// --- Helpers ---

async Task ApplySecurity(EngineContext ctx, PlaySelfAtEndOfBattleSecurityEffect eff)
{
    var sink = NewSink(ctx);
    eff.Apply(sink);
    await sink.FlushAsync();
}

async Task ResolveTrigger(EngineContext ctx, PlaySelfAtEndOfBattleTriggerEffect trigger)
{
    var sink = NewSink(ctx);
    await ((IHeadlessCardEffect)trigger).ResolveAsync(new CardEffectResolveContext(trigger.ToBinding(trigger.Definition.EffectId.Value).Request), sink);
    await sink.FlushAsync();
}

MatchStateMutationSink NewSink(EngineContext ctx) =>
    new(ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 910);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Sec(EngineContext ctx, string tag)
{
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(tag), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:sec:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(tag), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Security));
    return id;
}

bool InBattle(EngineContext ctx, HeadlessEntityId id) => InZone(ctx, id, ChoiceZone.BattleArea);
bool InZone(EngineContext ctx, HeadlessEntityId id, ChoiceZone zone) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
