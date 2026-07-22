// DIGIBURST-Witness — end-to-end witness for the AS-IS Digi-Burst keyword mechanism, driven through the
// printed card ST4_13 (Green):
//     [Security] Pierce.
//     [Main] <Digi-Burst 2> (Trash 2 of this Digimon's Digivolution cards to activate the effect below.) -
//            Suspend 1 of your opponent's Digimon.
//
// ST4_13's [Main] inlines the AS-IS `new IDigiBurst(permanent, 2, activateClass)` idiom (CardController.cs:2114):
//   CanUse gate  = IDigiBurst.CanDigiBurst()  — needs >= 2 trashable digivolution sources.
//   Activate     = IDigiBurst.DigiBurst()     — SELECT 2 sources -> open the OnUseDigiburst window
//                  (StackSkillInfos) -> ITrashDigivolutionCards trashes them -> then the inner effect
//                  (SelectPermanentEffect Mode.Tap) suspends 1 opponent Digimon.
//
// The two trigger seats this mechanism feeds — CardEffectCommons.CanTriggerWhenUseDigiBurst (the OnUseDigiburst
// window) and CanTriggerOnTrashBySelfDigiBurst (the self-source-trashed-by-Digi-Burst window) — are asserted
// directly against the hashtable IDigiBurst builds, plus a control for each.
//
// Harness = the PRIM.DigiBurst EngineContext style (SourceIdsKey staging, DigivolutionStackReader read-back,
// default ScriptedChoiceProvider auto-selects the mandated count) + an explicit AmbientMatchContext scope so
// GManager.instance resolves the SelectCardEffect / SelectPermanentEffect components for the direct
// ActivateClass invocation (the ST4_13 / BT5_056 PILOT-S4 direct-drive precedent).
using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using Cfx = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST4_13 <Digi-Burst 2>: 2 sources + an opponent Digimon -> CanUse true; Activate trashes 2 sources and suspends the opponent", BurstTrashesAndSuspends),
    ("ST4_13 <Digi-Burst 2> control: only 1 source -> CanUse false (Digi-Burst cost gate), no trash", GateBlocksBelowCost),
    ("Digi-Burst trigger seats: CanTriggerWhenUseDigiBurst and CanTriggerOnTrashBySelfDigiBurst fire for the live burst window (with controls)", TriggerSeatsFire),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ── Tests ────────────────────────────────────────────────────────────────────────────────────────────────

async Task BurstTrashesAndSuspends()
{
    EngineContext ctx = Ctx();
    var sources = new[] { MakeCard(ctx, P1, "S1", "Digimon"), MakeCard(ctx, P1, "S2", "Digimon") };
    HeadlessEntityId host = await StageHostWithSources(ctx, sources);
    HeadlessEntityId opp = await StageBattleDigimon(ctx, P2, "OPP");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var card = new Cec.CardSource(ctx, host, P1);
    Cfx.ActivateClass burst = DeclareBurst(card);

    AssertTrue(burst.CanUse(new Hashtable()), "CanUse is true with 2 trashable digivolution sources");

    await burst.Activate(new Hashtable());

    var stack = DigivolutionStackReader.Read(ctx.CardInstanceRepository, ctx.CardRepository, host);
    AssertEqual(0, stack.UnderCards.Count, "both digivolution sources were trashed as the <Digi-Burst 2> cost");
    AssertTrue(new Cec.Permanent(ctx, opp, P2).IsSuspended, "the inner effect resolved: 1 opponent Digimon was suspended");
}

async Task GateBlocksBelowCost()
{
    EngineContext ctx = Ctx();
    var sources = new[] { MakeCard(ctx, P1, "S1", "Digimon") };                 // only 1 — below the Digi-Burst 2 cost
    HeadlessEntityId host = await StageHostWithSources(ctx, sources);
    await StageBattleDigimon(ctx, P2, "OPP");                                    // an eligible target exists

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var card = new Cec.CardSource(ctx, host, P1);
    Cfx.ActivateClass burst = DeclareBurst(card);

    AssertTrue(!burst.CanUse(new Hashtable()), "CanUse is false: CanDigiBurst gate requires >= 2 trashable sources");

    var stack = DigivolutionStackReader.Read(ctx.CardInstanceRepository, ctx.CardRepository, host);
    AssertEqual(1, stack.UnderCards.Count, "the single source was NOT trashed (the burst never fired)");
}

async Task TriggerSeatsFire()
{
    EngineContext ctx = Ctx();
    var sources = new[] { MakeCard(ctx, P1, "S1", "Digimon"), MakeCard(ctx, P1, "S2", "Digimon") };
    HeadlessEntityId host = await StageHostWithSources(ctx, sources);
    HeadlessEntityId foreign = MakeCard(ctx, P1, "FOREIGN", "Digimon");         // never a source of `host`

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    var card = new Cec.CardSource(ctx, host, P1);
    Cfx.ActivateClass burst = DeclareBurst(card);                               // EffectDiscription contains "Digi-Burst", EffectSourceCard == host card

    var hostPermanent = new Cec.Permanent(ctx, host, P1);

    // The hashtable IDigiBurst.DigiBurst() builds for the OnUseDigiburst window (CardController.cs:2218-2228).
    Hashtable useWindow = new() { { "Permanent", hostPermanent }, { "CardEffect", burst } };
    AssertTrue(Cec.CardEffectCommons.CanTriggerWhenUseDigiBurst(useWindow, permanentCondition: null, cardEffectCondition: null),
        "CanTriggerWhenUseDigiBurst fires for a live {Permanent, Digi-Burst CardEffect} window");
    AssertTrue(!Cec.CardEffectCommons.CanTriggerWhenUseDigiBurst(new Hashtable { { "CardEffect", burst } }, null, null),
        "control: with no Permanent in the hashtable the window does NOT fire");

    // The trash window: a digivolution source of `host` trashed by this Digi-Burst (OnTrashBySelfDigiBurst).
    var trashedSource = new Cec.CardSource(ctx, sources[0], P1);
    Hashtable trashWindow = new()
    {
        { "Permanent", hostPermanent },
        { "CardEffect", burst },
        { "DiscardedCards", new List<Cec.CardSource> { trashedSource } },
    };
    AssertTrue(Cec.CardEffectCommons.CanTriggerOnTrashBySelfDigiBurst(trashWindow, trashedSource),
        "CanTriggerOnTrashBySelfDigiBurst fires for a self-source trashed by this Digimon's <Digi-Burst>");

    var foreignSource = new Cec.CardSource(ctx, foreign, P1);
    Hashtable foreignTrash = new()
    {
        { "Permanent", hostPermanent },
        { "CardEffect", burst },
        { "DiscardedCards", new List<Cec.CardSource> { foreignSource } },
    };
    AssertTrue(!Cec.CardEffectCommons.CanTriggerOnTrashBySelfDigiBurst(foreignTrash, foreignSource),
        "control: a card that is NOT one of this Digimon's digivolution sources does NOT fire the window");

    await Task.CompletedTask;
}

// ── Harness ──────────────────────────────────────────────────────────────────────────────────────────────

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 41);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);   // past None -> DoneStartGame true (ICardEffect.CanTrigger gate)
    return ctx;
}

HeadlessEntityId MakeCard(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:card:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false }));
    return id;
}

async Task<HeadlessEntityId> StageHostWithSources(EngineContext ctx, HeadlessEntityId[] sources)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId("DEF:ST4_13");
    cards.Upsert(new CardRecord(defId, "ST4_13", "ST4_13",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 5 }, CardType: "Digimon"));
    var id = new HeadlessEntityId("1:battle:ST4_13");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["isSuspended"] = false,
            [DigivolutionStackReader.SourceIdsKey] = sources.Select(x => x.Value).ToArray(),
        }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> StageBattleDigimon(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    HeadlessEntityId id = MakeCard(ctx, owner, tag, "Digimon");
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

Cfx.ActivateClass DeclareBurst(Cec.CardSource card)
{
    List<Cec.ICardEffect> effects = new ST4_13().CardEffects(Cec.EffectTiming.OnDeclaration, card);
    return (Cfx.ActivateClass)effects.First(e => e is Cfx.ActivateClass);
}

static void AssertTrue(bool v, string l) { if (!v) throw new InvalidOperationException($"{l}: expected true."); }
static void AssertEqual<T>(T e, T a, string l) { if (!EqualityComparer<T>.Default.Equals(e, a)) throw new InvalidOperationException($"{l}: expected '{e}', actual '{a}'."); }
