using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-13) AS-IS OptionalSkill: a "you may ..." effect asks the controller yes/no before it runs; declining
// does nothing. (RD-12) AS-IS registers the once-per-turn use in the effect's OnProcess callback (at
// execution, only when accepted) — not at the gate, so a DECLINED once-per-turn optional can still be used
// later the same turn. Previously the headless ActivatedEffectResolver force-ran a non-interactive optional
// body and consumed the cap at the gate.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

// The activated effect id the fixture produces (ActivatedEffect.EffectId = "{instanceId}:ae:{timing}:{body}").
HeadlessEntityId Card = new("p1:OPT");
HeadlessEntityId EffectId = new($"{Card.Value}:ae:OnEnterFieldAnyone:MemoryBody");

async Task<EngineContext> Setup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 13);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOptionalMemory"), "TfxOptionalMemory", "Opt",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("TfxOptionalMemory"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, Card, ChoiceZone.None, ChoiceZone.BattleArea));
    ctx.MemoryController.Set(0);
    return ctx;
}

Task Resolve(EngineContext ctx) =>
    ActivatedEffectResolver.ResolveAsync(ctx, Card, P1, EffectTiming.OnEnterFieldAnyone);

// --- 1. Decline (no scripted answer -> fallback skip): no memory, no use consumed. ---
{
    EngineContext ctx = await Setup();
    await Resolve(ctx);
    Check(ctx.MemoryController.Current.Current == 0, "declining an optional effect does nothing (memory stays 0)");
}

// --- 2. Accept (scripted 'select the use candidate'): memory +1. ---
{
    EngineContext ctx = await Setup();
    ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(ChoiceResult.Select(EffectId));
    await Resolve(ctx);
    Check(ctx.MemoryController.Current.Current == 1, "accepting an optional effect resolves it (memory +1)");
}

// --- 3. (RD-12) A DECLINED once-per-turn optional consumes NO use -> it can be used later the same turn. ---
{
    EngineContext ctx = await Setup();
    await Resolve(ctx);   // decline (fallback skip) — must NOT consume the once-per-turn cap
    Check(ctx.MemoryController.Current.Current == 0, "precondition: first (declined) attempt gained nothing");

    ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(ChoiceResult.Select(EffectId));
    await Resolve(ctx);   // accept — should still be usable this turn (decline consumed no cap)
    Check(ctx.MemoryController.Current.Current == 1, "a declined once-per-turn optional is still usable later the same turn (+1)");
}

// --- 4. (RD-12) An ACCEPTED once-per-turn optional consumes the use -> a second accept the same turn is a no-op. ---
{
    EngineContext ctx = await Setup();
    ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(ChoiceResult.Select(EffectId));
    await Resolve(ctx);   // accept -> +1, cap consumed
    Check(ctx.MemoryController.Current.Current == 1, "precondition: first accept gained +1");

    ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(ChoiceResult.Select(EffectId));
    await Resolve(ctx);   // capped out -> not offered/resolved
    Check(ctx.MemoryController.Current.Current == 1, "a once-per-turn optional does not fire twice in a turn (stays +1)");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-13/RD-12 optional-gate checks passed.");
