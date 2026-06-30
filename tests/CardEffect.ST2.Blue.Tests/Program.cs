using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Card group CardEffect/ST2/Blue — all 12 cards (group-standard project; see card_group_standard.md).
// Continuous (ST2_01/08), trigger (ST2_11/12), activated select/trash/memory/restrict/bounce
// (ST2_03/06/09/13/14/16), the effectClass alias ST2_07 (-> ST1_06), and the deferred play-from-under
// ST2_15. Activated effects are resolved imperatively (BuildRequest -> scripted answer -> Apply).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessPlayerId[] Both = { new(1), new(2) };

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST2_07: effectClass alias resolves to ST1_06 (<Blocker>)", ST2_07_Alias),
    ("ST2_01: inherited DP +1000 while opponent has a no-evo Digimon (owner turn)", ST2_01_Dp),
    ("ST2_08: inherited Security Attack +1 while opponent has a no-evo Digimon", ST2_08_SecurityAttack),
    ("ST2_03: [When Attacking] only opponent Digimon lvl<=5 with sources are candidates; trashes 1", ST2_03_Trash),
    ("ST2_06: [When Attacking] trashes 1 bottom digivolution card of the chosen opponent Digimon", ST2_06_Trash),
    ("ST2_09: [When Digivolving] trashes 2 bottom digivolution cards", ST2_09_Trash),
    ("ST2_11: [When Attacking] unsuspends this Digimon", ST2_11_Unsuspend),
    ("ST2_12: [Start of Your Turn] gains 1 memory when opponent has a no-evo Digimon", ST2_12_Memory),
    ("ST2_13: [Main] +1 memory / [Security] +2 memory", ST2_13_Memory),
    ("ST2_14: [Main] makes the chosen opponent Digimon unable to attack/block", ST2_14_Restrict),
    ("ST2_15: [Main] plays a Digimon under-card as a new Digimon; [Security] reuses Main", ST2_15_PlayFromUnder),
    ("ST2_16: [Main] returns the chosen opponent Digimon to its owner's hand", ST2_16_Bounce),
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

async Task ST2_07_Alias()
{
    EngineContext context = Context();
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST2_07def"), "ST2_07", "Grizzlymon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["effectClass"] = "ST1_06" }, CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:battle:ST2_07");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("ST2_07def"), P1));

    AssertTrue(CardEffectRegistrar.RegisterCard(context, id, P1), "ST2_07 registered via its effectClass alias");
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Blocker").Count >= 1, "ST2_07 gained <Blocker> from ST1_06");
    await Task.CompletedTask;
}

async Task ST2_01_Dp()
{
    // (a) battling a no-evo opponent -> +1000.
    EngineContext a = Context();
    (HeadlessEntityId topA, HeadlessEntityId srcA) = await SelfStack(a, P1, new HeadlessEntityId("p1:battle:TOP01a"));
    var foeA = new HeadlessEntityId("p2:battle:FOEa");
    await PlaceDigimon(a, P2, foeA, level: 4, sources: 0);
    RegisterContinuous(a, new ST2_01(), "ST2_01", srcA);
    a.AttackController.DeclareAttack(P1, topA, P2, foeA);
    AssertEqual(3000, ContinuousDpGate.ResolveDp(a, topA, baseDp: 2000), "battling a no-evo opponent: +1000");

    // (b) battling an opponent WITH digivolution cards -> no buff.
    EngineContext b = Context();
    (HeadlessEntityId topB, HeadlessEntityId srcB) = await SelfStack(b, P1, new HeadlessEntityId("p1:battle:TOP01b"));
    var foeB = new HeadlessEntityId("p2:battle:FOEb");
    await PlaceDigimon(b, P2, foeB, level: 4, sources: 1);
    RegisterContinuous(b, new ST2_01(), "ST2_01", srcB);
    b.AttackController.DeclareAttack(P1, topB, P2, foeB);
    AssertEqual(2000, ContinuousDpGate.ResolveDp(b, topB, baseDp: 2000), "battling an evo opponent: no buff");

    // (c) NOT in a battle — even with a no-evo opponent on the board -> no buff (battle-specific, not "any opponent").
    EngineContext c = Context();
    (HeadlessEntityId topC, HeadlessEntityId srcC) = await SelfStack(c, P1, new HeadlessEntityId("p1:battle:TOP01c"));
    await PlaceDigimon(c, P2, new HeadlessEntityId("p2:battle:FOEc"), level: 4, sources: 0);
    RegisterContinuous(c, new ST2_01(), "ST2_01", srcC);
    AssertEqual(2000, ContinuousDpGate.ResolveDp(c, topC, baseDp: 2000), "not battling: no buff");
}

async Task ST2_08_SecurityAttack()
{
    EngineContext context = Context();
    (HeadlessEntityId top, HeadlessEntityId source) = await SelfStack(context, P1, new HeadlessEntityId("p1:battle:TOP08"));
    await PlaceDigimon(context, P2, new HeadlessEntityId("p2:battle:NOEVO8"), level: 3, sources: 0);
    RegisterContinuous(context, new ST2_08(), "ST2_08", source);

    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(context, top, baseSecurityAttack: 1), "opponent has a no-evo Digimon: SA +1");
}

async Task ST2_03_Trash()
{
    EngineContext context = Context();
    var ok = new HeadlessEntityId("p2:battle:OK");        // lvl 4, has sources -> candidate
    var tooHigh = new HeadlessEntityId("p2:battle:HIGH");  // lvl 6 -> excluded
    var noSrc = new HeadlessEntityId("p2:battle:NOSRC");   // lvl 4, no sources -> excluded
    var prot = new HeadlessEntityId("p2:battle:PROT");     // lvl 4, only source is trash-protected -> excluded
    await PlaceDigimon(context, P2, ok, level: 4, sources: 2);
    await PlaceDigimon(context, P2, tooHigh, level: 6, sources: 2);
    await PlaceDigimon(context, P2, noSrc, level: 4, sources: 0);
    await PlaceDigimon(context, P2, prot, level: 4, sources: 1);
    ProtectSource(context, P2, prot, 0);

    var effect = (ActivatedSelectTrashDigivolutionEffect)Activated(new ST2_03(), context, EffectTiming.OnAllyAttack);
    ChoiceRequest request = effect.BuildRequest(Both);
    AssertEqual(1, request.Candidates.Count, "only the lvl<=5 Digimon with a trashable source is a candidate (protected/no-source/lvl6 excluded)");

    var sink = Sink(context);
    effect.Apply(sink, new[] { ok });
    await sink.FlushAsync();
    AssertEqual(1, TrashCount(context, P2), "1 digivolution card trashed");
}

async Task ST2_06_Trash()
{
    EngineContext context = Context();
    var target = new HeadlessEntityId("p2:battle:T06");
    await PlaceDigimon(context, P2, target, level: 6, sources: 1); // no level gate on ST2_06
    var effect = (ActivatedSelectTrashDigivolutionEffect)Activated(new ST2_06(), context, EffectTiming.OnAllyAttack);
    ChoiceRequest request = effect.BuildRequest(Both);
    AssertEqual(1, request.Candidates.Count, "the opponent Digimon is a candidate regardless of level");

    var sink = Sink(context);
    effect.Apply(sink, new[] { target });
    await sink.FlushAsync();
    AssertEqual(1, TrashCount(context, P2), "1 digivolution card trashed");
}

async Task ST2_09_Trash()
{
    EngineContext context = Context();
    var target = new HeadlessEntityId("p2:battle:T09");
    var prot = new HeadlessEntityId("p2:battle:T09P");   // only source protected -> excluded
    await PlaceDigimon(context, P2, target, level: 4, sources: 2);
    await PlaceDigimon(context, P2, prot, level: 4, sources: 1);
    ProtectSource(context, P2, prot, 0);

    var effect = (ActivatedSelectTrashDigivolutionEffect)Activated(new ST2_09(), context, EffectTiming.OnEnterFieldAnyone);
    ChoiceRequest request = effect.BuildRequest(Both);
    AssertEqual(1, request.Candidates.Count, "only the Digimon with a trashable source is a candidate");

    var sink = Sink(context);
    effect.Apply(sink, new[] { target });
    await sink.FlushAsync();
    AssertEqual(2, TrashCount(context, P2), "2 digivolution cards trashed");
}

async Task ST2_11_Unsuspend()
{
    EngineContext context = Context();
    var id = new HeadlessEntityId("p1:battle:T11");
    await PlaceDigimon(context, P1, id, level: 4, sources: 0);
    IReadOnlyList<EffectBinding> bindings = RegisterContinuous(context, new ST2_11(), "ST2_11", id);
    EffectBinding binding = bindings.Single(b => string.Equals(b.Request.Timing, "OnAllyAttack", StringComparison.Ordinal));

    // [Once Per Turn] must be declared on the effect so the live trigger loop's OnceFlag gate enforces it.
    AssertTrue(binding.Effect!.Definition.MaxCountPerTurn == 1, "once-per-turn cap (maxCountPerTurn=1) declared");

    // Turn 1, attack #1: the once-flag gate passes -> the effect fires -> unsuspended.
    await Suspend(context, id);
    AssertTrue(await GateAndResolve(context, binding), "1st attack this turn: effect fires");
    AssertTrue(!IsSuspended(context, id), "1st attack: unsuspended");

    // Turn 1, attack #2: once-per-turn cap BLOCKS the gate -> effect does NOT fire -> stays suspended.
    await Suspend(context, id);
    AssertTrue(!await GateAndResolve(context, binding), "2nd attack same turn: blocked by once-per-turn");
    AssertTrue(IsSuspended(context, id), "2nd attack: NOT unsuspended (gate blocked)");

    // Next turn: the per-turn cap resets -> the effect fires again.
    context.OnceFlags.ResetForTurn(2, P1);
    await Suspend(context, id);
    AssertTrue(await GateAndResolve(context, binding), "next turn: effect fires again");
    AssertTrue(!IsSuspended(context, id), "next turn: unsuspended");
}

async Task Suspend(EngineContext context, HeadlessEntityId id)
{
    var sink = Sink(context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.SuspendKind, id,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = id.Value }));
    await sink.FlushAsync();
}

// Mirror the live trigger loop (GameFlowProcessor): consult the OnceFlag gate; resolve only if allowed.
async Task<bool> GateAndResolve(EngineContext context, EffectBinding binding)
{
    if (!context.OnceFlags.TryActivate(binding.Request, binding.Effect!.Definition.MaxCountPerTurn))
    {
        return false;
    }

    var sink = Sink(context);
    await binding.Effect!.ResolveAsync(new CardEffectResolveContext(binding.Request), sink);
    await sink.FlushAsync();
    return true;
}

async Task ST2_12_Memory()
{
    EngineContext context = Context();
    var tamer = new HeadlessEntityId("p1:battle:T12");
    await PlaceDigimon(context, P1, tamer, level: 0, sources: 0);
    await PlaceDigimon(context, P2, new HeadlessEntityId("p2:battle:NOEVO12"), level: 4, sources: 0);

    IReadOnlyList<EffectBinding> bindings = RegisterContinuous(context, new ST2_12(), "ST2_12", tamer);
    context.MemoryController.Set(0);
    await ResolveTrigger(context, bindings, "OnStartTurn");
    AssertEqual(1, context.MemoryController.Current.Current, "gained 1 memory at start of turn");

    // [Security] "Play this Tamer": revealed to the trash, then played onto the battle area cost-free.
    EngineContext sec = Context();
    CardDatabase scards = (CardDatabase)sec.CardRepository;
    scards.Upsert(new CardRecord(new HeadlessEntityId("ST2_12def"), "ST2_12", "Tamer", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));
    var revealed = new HeadlessEntityId("p1:trash:ST2_12T");
    sec.CardInstanceRepository.Upsert(new CardInstanceRecord(revealed, new HeadlessEntityId("ST2_12def"), P1));
    await sec.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, revealed, ChoiceZone.None, ChoiceZone.Trash));
    var play = (PlayThisCardToBattleEffect)new ST2_12().CardEffects(EffectTiming.SecuritySkill, new CardSource(sec, revealed, P1)).Single();
    var playSink = Sink(sec);
    play.Apply(playSink);
    await playSink.FlushAsync();
    AssertTrue(InZone(sec, P1, ChoiceZone.BattleArea, revealed), "[Security] played the Tamer onto the battle area");
}

async Task ST2_13_Memory()
{
    EngineContext context = Context();
    var opt = new HeadlessEntityId("p1:trash:OPT13");

    var main = (ActivatedMemoryEffect)new ST2_13().CardEffects(EffectTiming.OptionSkill, Source(context, opt)).Single();
    var sink = Sink(context);
    context.MemoryController.Set(2);
    main.Apply(sink);
    await sink.FlushAsync();
    AssertEqual(3, context.MemoryController.Current.Current, "[Main] +1 memory");

    var sec = (ActivatedMemoryEffect)new ST2_13().CardEffects(EffectTiming.SecuritySkill, Source(context, opt)).Single();
    var sink2 = Sink(context);
    context.MemoryController.Set(2);
    sec.Apply(sink2);
    await sink2.FlushAsync();
    AssertEqual(4, context.MemoryController.Current.Current, "[Security] +2 memory");
}

async Task ST2_14_Restrict()
{
    EngineContext context = Context();
    var target = new HeadlessEntityId("p2:battle:T14");
    await PlaceDigimon(context, P2, target, level: 4, sources: 0); // no-evo -> a candidate

    var effect = (ActivatedTargetRestrictionEffect)Activated(new ST2_14(), context, EffectTiming.OptionSkill);
    ChoiceRequest request = effect.BuildRequest(Both);
    AssertEqual(1, request.Candidates.Count, "the no-evo opponent Digimon is the only candidate");
    effect.ApplyRestriction(new[] { target });

    IReadOnlyList<EffectRequest> requests = context.EffectRegistry.GetRestrictionEffects(
        new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: target));
    var restrictions = RestrictionHelpers.ReadRestrictions(effectRequests: requests);
    AssertTrue(RestrictionHelpers.CannotAttack(target, restrictions).IsRestricted, "target can't attack");
    AssertTrue(RestrictionHelpers.CannotBlock(target, restrictions).IsRestricted, "target can't block");
    await Task.CompletedTask;
}

async Task ST2_15_PlayFromUnder()
{
    EngineContext context = Context();
    var opt = new HeadlessEntityId("p1:trash:OPT15");
    // An owner Digimon with a Digimon digivolution card under it.
    var host = new HeadlessEntityId("p1:battle:H15");
    await PlaceDigimon(context, P1, host, level: 4, sources: 1);
    var under = new HeadlessEntityId($"{host.Value}:src0");

    var main = (ActivatedPlayFromUnderEffect)new ST2_15().CardEffects(EffectTiming.OptionSkill, Source(context, opt)).Single();
    ChoiceRequest request = main.BuildRequest(Both);
    AssertEqual(1, request.Candidates.Count, "the Digimon under-card is a candidate");

    var sink = Sink(context);
    main.Apply(sink, new[] { under });
    await sink.FlushAsync();

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, under), "[Main] the under-card is now its own battle-area Digimon");
    AssertTrue(CardEffectCommons.HasNoDigivolutionCards(new CardSource(context, host, P1), host), "host lost that digivolution source");

    // [Security] reuses the Main option.
    ICardEffect security = new ST2_15().CardEffects(EffectTiming.SecuritySkill, Source(context, opt)).Single();
    AssertTrue(security is ReuseMainOptionEffect, "[Security] reuses the Main option");
}

async Task ST2_16_Bounce()
{
    EngineContext context = Context();
    var target = new HeadlessEntityId("p2:battle:T16");
    await PlaceDigimon(context, P2, target, level: 4, sources: 0);

    var effect = (ActivatedSelectEffect)Activated(new ST2_16(), context, EffectTiming.OptionSkill);
    ChoiceRequest request = effect.BuildRequest(Both);
    AssertEqual(1, request.Candidates.Count, "the opponent Digimon is a candidate");

    var sink = Sink(context);
    effect.Apply(sink, new[] { target });
    await sink.FlushAsync();
    AssertTrue(InZone(context, P2, ChoiceZone.Hand, target), "the opponent Digimon was returned to its owner's hand");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, target), "the opponent Digimon left the battle area");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 2);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

CardSource Source(EngineContext context, HeadlessEntityId id) => new(context, id, P1);

ICardEffect Activated(CEntity_Effect card, EngineContext context, EffectTiming timing) =>
    card.CardEffects(timing, new CardSource(context, new HeadlessEntityId("p1:trash:ACT"), P1)).Single();

IReadOnlyList<EffectBinding> RegisterContinuous(EngineContext context, CEntity_Effect effect, string number, HeadlessEntityId source) =>
    CardEffectRegistrar.RegisterOnEnterPlay(context, effect, number, new CardSource(context, source, P1));

async Task<(HeadlessEntityId Top, HeadlessEntityId Source)> SelfStack(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId top)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TOPDEF"), "TOPDEF", "Top", new Dictionary<string, object?>(), CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("SRCDEF"), "SRCDEF", "Src", new Dictionary<string, object?>(), CardType: "Digimon"));
    var src = new HeadlessEntityId($"{top.Value}:src0");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(src, new HeadlessEntityId("SRCDEF"), owner));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["sourceIds"] = new List<string> { src.Value } };
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(top, new HeadlessEntityId("TOPDEF"), owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, top, ChoiceZone.None, ChoiceZone.BattleArea));
    return (top, src);
}

// Mark the host's source #index as trash-protected (CanNotTrashFromDigivolutionCards).
void ProtectSource(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId host, int index)
{
    var defId = new HeadlessEntityId($"DEF:{host.Value}");
    var sid = new HeadlessEntityId($"{host.Value}:src{index}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(sid, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["cannotTrashFromDigivolution"] = true }));
}

async Task PlaceDigimon(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, int level, int sources)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    cards.Upsert(new CardRecord(defId, defId.Value, id.Value, new Dictionary<string, object?>(), CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level };
    if (sources > 0)
    {
        var sourceIds = new List<string>();
        for (int i = 0; i < sources; i++)
        {
            var sid = new HeadlessEntityId($"{id.Value}:src{i}");
            context.CardInstanceRepository.Upsert(new CardInstanceRecord(sid, defId, owner));
            sourceIds.Add(sid.Value);
        }

        meta["sourceIds"] = sourceIds;
    }

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

async Task ResolveTrigger(EngineContext context, IReadOnlyList<EffectBinding> bindings, string timing)
{
    EffectBinding binding = bindings.Single(b => string.Equals(b.Request.Timing, timing, StringComparison.Ordinal));
    AssertTrue(binding.Effect is not null, $"trigger binding for {timing} carries an effect body");
    var sink = Sink(context);
    await binding.Effect!.ResolveAsync(new CardEffectResolveContext(binding.Request), sink);
    await sink.FlushAsync();
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController, context.EffectRegistry);

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

int TrashCount(EngineContext context, HeadlessPlayerId player) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Trash).Count;

bool IsSuspended(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
    && inst.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
