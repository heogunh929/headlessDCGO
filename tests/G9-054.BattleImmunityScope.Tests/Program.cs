using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// M-2 #3 verification (G9-054): CanNotBeDestroyedByBattleStaticEffect is NOT condition-less — its
// permanentCondition (EX8_068: "your DS Digimon") DOES gate battle-deletion immunity. Only the separate 4-arg
// canNotBeDestroyedByBattleCondition (permanent == attacker || defender) is omitted, and that is trivially true
// for any battle-deletion candidate (only the battle's participants are ever "deleted by battle").

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Matching (predicate) Digimon IS battle-immune", () => Immune(match: true)),
    ("Non-matching Digimon is NOT battle-immune (permanentCondition enforced, not condition-less)", () => Immune(match: false)),
    ("Self form (permanentCondition=null) grants self battle immunity", SelfForm),
    ("condition gate (e.g. memory>=1) is honoured LIVE — immunity off when false, on when true", ConditionGate),
    ("(AD1-G) GainCanNotBeDeletedByBattle: timed grant protects the TARGET, expires at opponent turn end", GainTimedGrant),
    ("(AD1-G) the grant is LIVE-gated on the target staying in play (leave -> off)", GainLiveGate),
    ("(AD1-G) a CanNotBeAffected target refuses the grant (AS-IS CanUse guard)", GainRefusedByImmunity),
    ("(AD1-G) the stored 4-arg battle predicate gates the immunity against the current attack", GainBattlePredicate),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Immune(bool match)
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var subject = await Place(ctx, P1, "SUBJ", level: match ? 5 : 4);
    // "Your Level-5 Digimon cannot be deleted by battle" (permanentCondition = Level==5); the 4-arg battle
    // condition is trivially true for a participant, so it is omitted 1:1.
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
        canNotBeDestroyedByBattleCondition: null, permanentCondition: p => p.Level == 5, isInheritedEffect: false,
        card: new CardSource(ctx, src, P1), condition: null).ToBinding($"cbdb:{src.Value}"));

    bool immune = BattleDeletionGate.PreventsBattleDeletion(ctx, subject);
    AssertTrue(immune == match, $"battle-immune == {match} (permanentCondition gates it — NOT condition-less)");
}

async Task SelfForm()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF", level: 4);
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
        canNotBeDestroyedByBattleCondition: null, permanentCondition: null, isInheritedEffect: false,
        card: new CardSource(ctx, self, P1), condition: null).ToBinding($"cbdb:{self.Value}"));
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, self), "self is battle-immune");
}

async Task ConditionGate()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF", level: 4);
    // Mirror EX8_068's CanUseCondition gate: immunity active only while the owner's memory >= 1. Evaluated LIVE.
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
        canNotBeDestroyedByBattleCondition: null, permanentCondition: null, isInheritedEffect: false,
        card: new CardSource(ctx, self, P1), condition: () => ctx.MemoryController.Current.Current >= 1).ToBinding($"cbdb:{self.Value}"));

    ctx.MemoryController.Set(0);
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, self), "memory 0 -> NOT immune (condition false)");
    ctx.MemoryController.Set(1);
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, self), "memory 1 -> immune (condition true)");
    ctx.MemoryController.Set(0);
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, self), "memory back to 0 -> immune turns OFF (re-evaluated live)");
}

// (AD1-G) AS-IS GainCanNotBeDeletedByBattle (GiveEffect/CanNotBeDeletedByBattle.cs:11-54): a timed,
// target-locked battle immunity built on the same factory family, added to a duration bucket.

async Task GainTimedGrant()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var target = await Place(ctx, P1, "TGT", level: 5);
    var bystander = await Place(ctx, P1, "OTHER", level: 5);

    AssertTrue(CardEffectCommons.GainCanNotBeDeletedByBattle(
        new Permanent(ctx, target, P1), null, EffectDuration.UntilOpponentTurnEnd,
        new CardSource(ctx, src, P1), "test-grant"), "grant registered");

    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, target), "the TARGET is battle-immune");
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, bystander), "the grant is target-locked (a bystander is not)");

    // The opponent's turn ends -> the duration bucket expires (AS-IS UntilOpponentTurnEnd).
    HeadlessDCGO.Engine.Headless.Effects.EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, target), "immunity expired at the opponent's turn end");
}

async Task GainLiveGate()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var target = await Place(ctx, P1, "TGT", level: 5);
    CardEffectCommons.GainCanNotBeDeletedByBattle(
        new Permanent(ctx, target, P1), null, EffectDuration.UntilOpponentTurnEnd,
        new CardSource(ctx, src, P1), "test-live");

    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, target), "immune while in play");
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.BattleArea, ChoiceZone.Trash));
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, target), "leaving the battle area turns the grant off (live CanUse mirror)");
}

async Task GainRefusedByImmunity()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P2, "ENEMYSRC", level: 4);   // OPPONENT grants -> blocked by CanNotBeAffected
    var target = await Place(ctx, P1, "TGT", level: 5);
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotAffectedStaticEffect(
        null, null, false, new CardSource(ctx, target, P1), null).ToBinding($"cna:{target.Value}"));

    AssertTrue(!CardEffectCommons.GainCanNotBeDeletedByBattle(
        new Permanent(ctx, target, P1), null, EffectDuration.UntilOpponentTurnEnd,
        new CardSource(ctx, src, P2), "test-refused"), "an immune target refuses the grant");
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, target), "nothing registered");
}

async Task GainBattlePredicate()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var target = await Place(ctx, P1, "TGT", level: 5);
    var attacker = await Place(ctx, P2, "ATK", level: 5);
    // AS-IS AD1_011 shape: protected only when THIS permanent is a battle participant vs a named attacker —
    // here: only when the attacker's top card is "ATK".
    CardEffectCommons.GainCanNotBeDeletedByBattle(
        new Permanent(ctx, target, P1),
        (self, atk, def, defCard) => atk is not null && atk.TopCard.EqualsCardName("ATK"),
        EffectDuration.UntilOpponentTurnEnd, new CardSource(ctx, src, P1), "test-pred");

    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, target), "no attack in flight -> predicate false -> not immune");
    ctx.AttackController.DeclareAttack(P2, attacker, P1, target, isDirectAttack: false);
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, target), "attacked by the named attacker -> predicate true -> immune");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 954);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
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
