using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// FAIL-a #10 (R6-Db D4 re-aim): PlaySelfDigimonAfterBattleSecurityEffect must mirror AS-IS
// (CardEffectFactory.cs:285) — the [Security] effect does NOT play immediately; it registers a deferred play
// into the owner's `UntilEndBattleEffects` bucket, sampled at OnEndBattle, that plays the card cost-free at the
// end of the battle. When deleteDigimon != UntilEndBattle the played Digimon gets an OnEndTurn self-delete
// registered into its `UntilOpponentTurnEndEffects` bucket.
//
// The former mirror-invented `PlaySelfAtEndOfBattleSecurityEffect` / `PlaySelfAtEndOfBattleTriggerEffect`
// carriers (a parallel EffectRegistry OnEndBattle-trigger + DeleteAtTurnEnd metadata marker) were DELETED; this
// suite is re-aimed onto the landed factory + the AS-IS Player.UntilEndBattleEffects / Permanent
// .UntilOpponentTurnEndEffects buckets. Assertions preserved (defer / play-at-end / delete-marker).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("[Security] resolution DEFERS: card not played immediately, OnEndBattle play registered into UntilEndBattleEffects", DefersToEndOfBattle),
    ("The OnEndBattle-sampled UntilEndBattleEffects entry plays the card from the executing area to the battle area", TriggerPlays),
    ("deleteDigimon=UntilOwnerTurnEnd registers an OnEndTurn self-delete on the played Digimon (UntilOpponentTurnEndEffects)", DeleteMarker),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task DefersToEndOfBattle()
{
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var card = await Exec(ctx, "DIG");

    // AS-IS: a [Security] effect resolves via the ActivateClass flow (ActivateClass.Activate) — drive it that way.
    var effect = (ActivateClass)CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(new CardSource(ctx, card, P1));
    await effect.Activate(new Hashtable());

    AssertTrue(!InBattle(ctx, card), "card NOT played immediately (deferred to end of battle)");
    AssertTrue(InZone(ctx, card, ChoiceZone.Execution), "card still in the executing area after the [Security] resolution");
    AssertTrue(OnEndBattlePlayEffect(ctx, P1) is not null,
        "a play effect was registered into the owner's UntilEndBattleEffects (fires at OnEndBattle)");
}

async Task TriggerPlays()
{
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var card = await Exec(ctx, "DIG");

    var effect = (ActivateClass)CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(new CardSource(ctx, card, P1));
    await effect.Activate(new Hashtable());

    // Sample the OnEndBattle bucket (as the battle-end flow does) and fire the deferred play.
    ActivateClass play = OnEndBattlePlayEffect(ctx, P1) ?? throw new InvalidOperationException("no OnEndBattle play effect registered");
    await play.Activate(new Hashtable());

    AssertTrue(InBattle(ctx, card), "card played into the battle area at end of battle");
}

async Task DeleteMarker()
{
    EngineContext ctx = Ctx();
    using var _ = AmbientMatchContext.Enter(ctx);
    var card = await Exec(ctx, "DIG");

    var effect = (ActivateClass)CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect(
        new CardSource(ctx, card, P1), EffectDuration.UntilOwnerTurnEnd);
    await effect.Activate(new Hashtable());

    ActivateClass play = OnEndBattlePlayEffect(ctx, P1) ?? throw new InvalidOperationException("no OnEndBattle play effect registered");
    await play.Activate(new Hashtable());
    AssertTrue(InBattle(ctx, card), "card played");

    // AS-IS: the played Digimon gets an OnEndTurn delete registered into its UntilOpponentTurnEndEffects bucket.
    var playedDigimon = new Permanent(ctx, card, P1);
    bool hasDelete = playedDigimon.UntilOpponentTurnEndEffects
        .Any(get => get(EffectTiming.OnEndTurn) is not null);
    AssertTrue(hasDelete, "played Digimon has an OnEndTurn self-delete registered (UntilOpponentTurnEndEffects)");
}

// --- Helpers ---

// The owner's UntilEndBattleEffects entry that yields a play ActivateClass at OnEndBattle (null if none).
ActivateClass? OnEndBattlePlayEffect(EngineContext ctx, HeadlessPlayerId owner) =>
    new Player(ctx, owner).EffectList(EffectTiming.OnEndBattle).OfType<ActivateClass>().FirstOrDefault();

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 910);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // AS-IS effects gate on a live phase — stamp Main so CanUse/CanActivate scans evaluate as in-match.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    ctx.MemoryController.Set(5);
    return ctx;
}

// Stage a Digimon in the executing area — the transient position a revealed security card occupies during a
// security check (AS-IS IsExistOnExecutingArea). The deferred play fires from here at battle end.
async Task<HeadlessEntityId> Exec(EngineContext ctx, string tag)
{
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(tag), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:exec:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(tag), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Execution));
    return id;
}

bool InBattle(EngineContext ctx, HeadlessEntityId id) => InZone(ctx, id, ChoiceZone.BattleArea);
bool InZone(EngineContext ctx, HeadlessEntityId id, ChoiceZone zone) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
