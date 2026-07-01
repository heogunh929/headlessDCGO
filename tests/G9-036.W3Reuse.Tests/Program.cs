using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W3 (G9-036): the reuse/subsystem-wrapper primitives, verified end-to-end through the mutation sink:
//  ChangeSAttackStatic (player-scope SA modifier), ReturnToLibraryBottomDigivolutionCards, Training (C-24),
//  MaterialSave (C-23), ReplaceBottomSecurity.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ChangeSAttackStatic +2 -> owner's Digimon security attack raised", ChangeSAttack),
    ("Training -> self suspended + a deck card added to its stack", Training),
    ("MaterialSave -> sources re-parented to another Digimon", MaterialSave),
    ("ReturnToLibraryBottom -> host's sources leave its stack", ReturnToLibraryBottom),
    ("ReplaceBottomSecurity -> bottom security to hand + self face-up security", ReplaceBottomSecurity),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task ChangeSAttack()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC", ChoiceZone.BattleArea);
    var ally = await Place(context, P1, "ALLY", ChoiceZone.BattleArea);
    int before = ContinuousModifierGate.ResolveSecurityAttack(context, ally, 1);
    context.EffectRegistry.Register(CardEffectFactory.ChangeSAttackStaticEffect(null, 2, false, new CardSource(context, src, P1), null).ToBinding($"sa:{src.Value}"));
    int after = ContinuousModifierGate.ResolveSecurityAttack(context, ally, 1);
    AssertEqual(before + 2, after, "SA +2 on owner's Digimon");
}

async Task Training()
{
    EngineContext context = Context();
    var self = await Place(context, P1, "SELF", ChoiceZone.BattleArea);
    var top = await Place(context, P1, "DECKTOP", ChoiceZone.Library);
    await Apply(context, sink => new TrainingActivatedEffect(new CardSource(context, self, P1), "train").Apply(sink));
    AssertTrue(ReadBool(context, self, "isSuspended"), "self suspended (training cost)");
    AssertTrue(Sources(context, self).Contains(top.Value), "deck top card became a digivolution source");
}

async Task MaterialSave()
{
    EngineContext context = Context();
    var from = await Place(context, P1, "FROM", ChoiceZone.BattleArea);
    var to = await Place(context, P1, "TO", ChoiceZone.BattleArea);
    var mat = await Place(context, P1, "MAT", ChoiceZone.None);
    SetSources(context, from, mat.Value);
    await Apply(context, sink => new MaterialSaveActivatedEffect(new CardSource(context, from, P1), to, 1, "save").Apply(sink));
    AssertTrue(!Sources(context, from).Contains(mat.Value), "source left the from-host");
    AssertTrue(Sources(context, to).Contains(mat.Value), "source re-parented to the to-host");
}

async Task ReturnToLibraryBottom()
{
    EngineContext context = Context();
    var host = await Place(context, P1, "HOST", ChoiceZone.BattleArea);
    var mat = await Place(context, P1, "MAT", ChoiceZone.None);
    SetSources(context, host, mat.Value);
    await Apply(context, sink => new ReturnSelfDigivolutionCardsToDeckEffect(new CardSource(context, host, P1), 1, "return").Apply(sink));
    AssertTrue(!Sources(context, host).Contains(mat.Value), "source returned off the host's stack");
}

async Task ReplaceBottomSecurity()
{
    EngineContext context = Context();
    var s1 = await Place(context, P1, "SEC1", ChoiceZone.Security);
    var s2 = await Place(context, P1, "SEC2", ChoiceZone.Security); // bottom (last)
    var self = await Place(context, P1, "SELF", ChoiceZone.Hand);
    await Apply(context, sink => new ReplaceBottomSecurityWithFaceUpEffect(new CardSource(context, self, P1), "replace").Apply(sink));
    AssertTrue(Zone(context, P1, ChoiceZone.Hand).Contains(s2), "bottom security card went to hand");
    AssertTrue(Zone(context, P1, ChoiceZone.Security).Contains(self), "self placed into security");
}

// --- Helpers -------------------------------------------------------------

async Task Apply(EngineContext context, Action<MatchStateMutationSink> apply)
{
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);
    apply(sink);
    await sink.FlushAsync();
}

IReadOnlyList<string> Sources(EngineContext context, HeadlessEntityId id)
{
    if (context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null &&
        r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids)
        return ids.ToArray();
    return Array.Empty<string>();
}

void SetSources(EngineContext context, HeadlessEntityId host, params string[] sources)
{
    context.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? r);
    context.CardInstanceRepository.Upsert(r! with { Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { ["sourceIds"] = sources } });
}

bool ReadBool(EngineContext context, HeadlessEntityId id, string key) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null &&
    r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

IReadOnlyList<HeadlessEntityId> Zone(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone) =>
    context.ZoneMover is IZoneStateReader reader ? reader.GetCards(owner, zone) : Array.Empty<HeadlessEntityId>();

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 936);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false, ["canSuspend"] = true }));
    if (zone != ChoiceZone.None)
    {
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    }

    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T e, T a, string label) { if (!EqualityComparer<T>.Default.Equals(e, a)) throw new InvalidOperationException($"{label}: expected '{e}', got '{a}'."); }

