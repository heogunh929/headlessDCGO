using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// FAIL-b: ImmuneFromDeDigivolveClass was inert — the "cannot be de-digivolved" flag had a consumer but NO
// producer (nothing could grant it). ImmuneFromDeDigivolveStaticEffect now registers the continuous restriction,
// and the sink's DeDigivolve handler skips an immune target (AS-IS Permanent.ImmuneFromDeDigivolve()).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("no immunity: de-digivolve removes the top source", () => Run(immune: false, expectRemoved: true)),
    ("ImmuneFromDeDigivolve: the stack is untouched", () => Run(immune: true, expectRemoved: false)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Run(bool immune, bool expectRemoved)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 920);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;

    // Host with level 5 (above the rookie floor) and one under-source, so a de-digivolve WOULD remove the top.
    cards.Upsert(new CardRecord(new HeadlessEntityId("H"), "H", "Host", new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 5 }, CardType: "Digimon"));
    var host = new HeadlessEntityId("p1:H");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, new HeadlessEntityId("H"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));

    cards.Upsert(new CardRecord(new HeadlessEntityId("U"), "U", "Under", new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon"));
    var under = new HeadlessEntityId("p1:U");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(under, new HeadlessEntityId("U"), P1));
    SetMeta(ctx, host, DigivolutionStackReader.SourceIdsKey, new[] { under.Value });

    if (immune)
    {
        // ImmuneFromDeDigivolveStaticEffect is declared to return the abstract ICardEffect base (its concrete
        // result is one of the OLD-model ContinuousSelfRestrictionEffect/ContinuousPlayerScopeRestrictionEffect,
        // which DO implement ToBinding — just not through the ICardEffect static type), so ToBinding is reached
        // via the LegacyBindingBridge reflective dispatch rather than a direct call.
        ICardEffect immunity = CardEffectFactory.ImmuneFromDeDigivolveStaticEffect(
            permanentCondition: null, isInheritedEffect: false, new CardSource(ctx, host, P1), condition: null);
        if (!LegacyBindingBridge.TryToBinding(immunity, "imm:host", out EffectBinding? immunityBinding) || immunityBinding is null)
        {
            throw new InvalidOperationException("expected a legacy ToBinding-capable effect from ImmuneFromDeDigivolveStaticEffect");
        }
        ctx.EffectRegistry.Register(immunityBinding);
    }

    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController, ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeDigivolveKind, new HeadlessEntityId("p2:cause"),
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
            [MatchStateMutationSink.CountKey] = 1,
        }));
    await sink.FlushAsync();

    // De-digivolve trashes the TOP card (H) and promotes the under-source (U). So "removed" == H left the battle
    // area; when immune, H stays with U still under it.
    var zones = (IZoneStateReader)ctx.ZoneMover;
    bool topRemoved = !zones.GetCards(P1, ChoiceZone.BattleArea).Contains(host);
    AssertTrue(topRemoved == expectRemoved, $"top card de-digivolved (left battle area) == {expectRemoved}");
}

void SetMeta(EngineContext ctx, HeadlessEntityId id, string key, object? value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with { Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { [key] = value } });
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
