// S3 (G9-058): deletion-replacement keywords un-sealed. (C-Del 3c-2b conversion) The old test proved the
// invented option-gate (DeletionReplacementTiming.PreOptions offering EvadeOption/BarrierOption/FragmentOption,
// FragmentCostOf) surfaced a granted keyword. Those option consts + the gate offer are RETIRED — Evade/Barrier/
// Fragment now fire ONLY through their AS-IS homes: the printed EvadeSelfEffect/BarrierSelfEffect/
// FragmentSelfEffect ActivateClass resolved by the PRE cut-in window, whose bodies CardEffectCommons.
// EvadeProcess/BarrierProcess/FragmentProcess actually produce the AS-IS survival (suspend / trash a security /
// trash <X> digivolution sources -> willBeRemoveField = false). These tests drive those printed Process bodies
// (and their AS-IS CanActivate* gates, including Fragment's trashValue<X> digivolution-count gate) directly, so
// the SAME "the keyword actually saves the Digimon on deletion" behaviour is proven via the AS-IS path. No
// retired option const / FragmentCostOf is referenced.

using System.Collections.Generic;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Evade: the printed keyword suspends this Digimon to survive deletion (un-sealed)", EvadeSurvives),
    ("Evade: an already-suspended Digimon cannot pay the suspend cost (gate control)", EvadeGateControl),
    ("Barrier: the printed keyword trashes the top security to survive deletion", BarrierSurvives),
    ("Barrier: with no security the keyword cannot activate (gate control)", BarrierGateControl),
    ("Fragment <3>: the trashValue gates on digivolution-source count (2 no / 3 yes) and survives", FragmentTrashValueGates),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task EvadeSurvives()
{
    EngineContext ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var id = await Place(ctx, P1, "EVADE");
    Perm(ctx, id).willBeRemoveField = true;

    AssertTrue(CardEffectCommons.CanActivateEvade(Perm(ctx, id)), "an unsuspended Digimon can activate Evade");
    ICardEffect act = CardEffectFactory.EvadeSelfEffect(false, V(ctx, id), null);
    await CardEffectCommons.EvadeProcess(Perm(ctx, id), act);

    AssertTrue(Perm(ctx, id).IsSuspended, "Evade suspended this Digimon (its cost)");
    AssertTrue(!Perm(ctx, id).willBeRemoveField, "Evade cleared willBeRemoveField (survives)");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, id), "the Digimon is still on the battle area");
}

async Task EvadeGateControl()
{
    EngineContext ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var id = await Place(ctx, P1, "EVADE");
    Perm(ctx, id).IsSuspended = true;   // already suspended -> the suspend cost cannot be paid

    AssertTrue(!CardEffectCommons.CanActivateEvade(Perm(ctx, id)),
        "an already-suspended Digimon cannot pay Evade's suspend cost");
}

async Task BarrierSurvives()
{
    EngineContext ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var id = await Place(ctx, P1, "BARRIER");
    await PlaceSecurity(ctx, P1);
    Perm(ctx, id).willBeRemoveField = true;

    AssertTrue(CardEffectCommons.CanActivateBarrier(Perm(ctx, id)), "with a security card Barrier can activate");
    ICardEffect act = CardEffectFactory.BarrierSelfEffect(false, V(ctx, id), null);
    await CardEffectCommons.BarrierProcess(Perm(ctx, id), act);

    AssertTrue(SecurityCount(ctx, P1) == 0, "Barrier trashed the top security (its cost)");
    AssertTrue(!Perm(ctx, id).willBeRemoveField, "Barrier cleared willBeRemoveField (survives)");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, id), "the Digimon is still on the battle area");
}

async Task BarrierGateControl()
{
    EngineContext ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var id = await Place(ctx, P1, "BARRIER");
    Perm(ctx, id).willBeRemoveField = true;   // no security placed

    AssertTrue(!CardEffectCommons.CanActivateBarrier(Perm(ctx, id)), "with no security Barrier cannot activate");
    ICardEffect act = CardEffectFactory.BarrierSelfEffect(false, V(ctx, id), null);
    await CardEffectCommons.BarrierProcess(Perm(ctx, id), act);
    AssertTrue(Perm(ctx, id).willBeRemoveField, "no security -> Barrier is a no-op -> the Digimon still dies");
}

// (C1) AS-IS Fragment <X>: CanActivateFragment(permanent, trashValue, activateClass) gates on
// DigivolutionCards.Count >= X (KeyWordEffects/Fragment.cs:22). The grant's X (previously dropped, collapsing
// every Fragment to 1) is honored.
async Task FragmentTrashValueGates()
{
    foreach ((int sourceCount, bool eligible) in new[] { (2, false), (3, true) })
    {
        EngineContext ctx = Ctx();
        using var scope = AmbientMatchContext.Enter(ctx);
        var id = await Place(ctx, P1, $"FRAG{sourceCount}");
        AttachSources(ctx, id, sourceCount);
        Perm(ctx, id).willBeRemoveField = true;

        ICardEffect act = CardEffectFactory.FragmentSelfEffect(
            false, V(ctx, id), null, trashValue: 3, effectName: "Fragment <3>", effectDiscription: "Fragment <3>");

        AssertTrue(Perm(ctx, id).DigivolutionCards.Count == sourceCount, $"the permanent has {sourceCount} digivolution sources");
        AssertTrue(CardEffectCommons.CanActivateFragment(Perm(ctx, id), 3, act) == eligible,
            $"{sourceCount} sources with Fragment<3> -> can-activate={eligible}");

        if (eligible)
        {
            await CardEffectCommons.FragmentProcess(act, Perm(ctx, id), trashValue: 3);
            AssertTrue(!Perm(ctx, id).willBeRemoveField, "Fragment<3> trashed 3 sources and cleared willBeRemoveField (survives)");
        }
    }
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 958);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, P1);
CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, P1, P1);

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false, ["level"] = 4 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

void AttachSources(EngineContext ctx, HeadlessEntityId topId, int count)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var sourceDef = new HeadlessEntityId("DEF:SRC");
    cards.Upsert(new CardRecord(sourceDef, "SRC", "SRC",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 1000, ["level"] = 3 }, CardType: "Digimon"));
    var sourceIds = new List<string>();
    for (int i = 1; i <= count; i++)
    {
        var src = new HeadlessEntityId($"{topId.Value}:src:{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(src, sourceDef, P1,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 1000, ["level"] = 3 }));
        sourceIds.Add(src.Value);
    }

    ctx.CardInstanceRepository.TryGetInstance(topId, out CardInstanceRecord? top);
    var meta = new Dictionary<string, object?>(top!.Metadata, StringComparer.Ordinal)
    {
        [DigivolutionStackReader.SourceIdsKey] = sourceIds.ToArray(),
    };
    ctx.CardInstanceRepository.Upsert(top with { Metadata = meta });
}

async Task PlaceSecurity(EngineContext ctx, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId("DEF:SEC");
    cards.Upsert(new CardRecord(defId, "SEC", "SEC", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:sec:1");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Security));
}

int SecurityCount(EngineContext ctx, HeadlessPlayerId owner) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Security).Count;

bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
