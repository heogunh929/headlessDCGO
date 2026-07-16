using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (R3-W3b) ProgressProcess witness — RD-R2-01 stale-STOP retirement proof. The AS-IS ProgressProcess
// (KeyWordEffects/Progress.cs:62) appends an UntilEndAttack "Isn't affected by opponent's Digimon's effect"
// CanNotAffectedClass to the ATTACKING Progress Digimon's duration bucket; the mirror bucket
// (Permanent.UntilEndAttackEffects, W3) now exists and is read back by the AS-IS-literal
// CardSource.CanNotBeAffected EffectList scan. Asserted here end-to-end:
//   1. attack in flight -> ProgressProcess grants -> attacker is unaffected by an OPPONENT-sourced effect;
//   2. the grant is opponent-only (AS-IS SkillCondition IsOpponentEffect): an OWN-sourced effect still affects;
//   3. no attack in flight -> CanActivateProgress false -> ProgressProcess no-ops (no immunity).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Attacking Progress Digimon becomes unaffected by an opponent effect (bucket granted + live scan read)", OpponentEffectBlocked),
    ("The Progress immunity is opponent-only — an own-sourced effect still affects (AS-IS SkillCondition)", OwnEffectNotBlocked),
    ("No attack in flight -> CanActivateProgress false -> ProgressProcess no-ops", NoAttackNoGrant),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} test(s) passed.");

async Task OpponentEffectBlocked()
{
    EngineContext ctx = Ctx();
    using var _scope = AmbientMatchContext.Enter(ctx);
    var attacker = await Place(ctx, P1, "PRG", level: 5);
    var defender = await Place(ctx, P2, "DEF", level: 5);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);

    var attackerCard = new CardSource(ctx, attacker, P1);
    var progressActivate = CardEffectFactory.ProgressStaticEffect(
        isInheritedEffect: false, card: attackerCard, condition: () => true);
    await CardEffectCommons.ProgressProcess(attackerCard, progressActivate);

    ICardEffect opponentProbe = OpponentProbe(ctx, defender);
    AssertTrue(new CardSource(ctx, attacker, P1).CanNotBeAffected(opponentProbe),
        "attacking Progress Digimon is unaffected by an opponent-sourced effect");
}

async Task OwnEffectNotBlocked()
{
    EngineContext ctx = Ctx();
    using var _scope = AmbientMatchContext.Enter(ctx);
    var attacker = await Place(ctx, P1, "PRG", level: 5);
    var defender = await Place(ctx, P2, "DEF", level: 5);
    var friendly = await Place(ctx, P1, "OWN", level: 3);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);

    var attackerCard = new CardSource(ctx, attacker, P1);
    var progressActivate = CardEffectFactory.ProgressStaticEffect(
        isInheritedEffect: false, card: attackerCard, condition: () => true);
    await CardEffectCommons.ProgressProcess(attackerCard, progressActivate);

    ICardEffect ownProbe = OpponentProbe(ctx, friendly);   // OWN player's source — not an opponent effect.
    AssertTrue(!new CardSource(ctx, attacker, P1).CanNotBeAffected(ownProbe),
        "an own-sourced effect still affects the Progress attacker (opponent-only SkillCondition)");
}

async Task NoAttackNoGrant()
{
    EngineContext ctx = Ctx();
    using var _scope = AmbientMatchContext.Enter(ctx);
    var digimon = await Place(ctx, P1, "PRG", level: 5);
    var enemy = await Place(ctx, P2, "DEF", level: 5);

    var card = new CardSource(ctx, digimon, P1);
    var progressActivate = CardEffectFactory.ProgressStaticEffect(
        isInheritedEffect: false, card: card, condition: () => true);
    await CardEffectCommons.ProgressProcess(card, progressActivate);   // no attack in flight

    ICardEffect opponentProbe = OpponentProbe(ctx, enemy);
    AssertTrue(!new CardSource(ctx, digimon, P1).CanNotBeAffected(opponentProbe),
        "without an attack CanActivateProgress is false and no immunity is granted");
}

// A probe ICardEffect whose EffectSourceCard is the given field card — the AS-IS "causing effect" the
// CanNotBeAffected scan hands to the granted immunity's SkillCondition (IsOpponentEffect checks its source owner).
ICardEffect OpponentProbe(EngineContext ctx, HeadlessEntityId sourceId)
{
    var owner = FindOwner(ctx, sourceId);
    var source = new CardSource(ctx, sourceId, owner);
    return CardEffectFactory.ProgressStaticEffect(isInheritedEffect: false, card: source, condition: () => true);
}

HeadlessPlayerId FindOwner(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out var record) && record is not null
        ? record.OwnerId
        : throw new InvalidOperationException($"unknown instance {id.Value}");

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3101);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // ICardEffect.CanTrigger/CanUse gate on TurnStateMachine.DoneStartGame (phase past None/Setup) —
    // the same harness convention G9-054 documents.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
