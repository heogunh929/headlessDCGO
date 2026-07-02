using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (W6-G) Gain-keyword commons batch — 1:1 mirrors of the AS-IS KeyWordEffects Gain* family (16 wrappers on
// one target-locked duration-tagged grant; verbatim bodies verified, primitive_w6_design.md W6-G).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("GainBlocker grants the live keyword to the TARGET only, expires at the duration boundary", GrantAndExpire),
    ("the grant is live-gated on the target staying in play", LiveGate),
    ("a CanNotBeAffected target refuses the grant (AS-IS CanUse guard)", RefusedByImmunity),
    ("all 16 keyword wrappers register their keyword", AllWrappers),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task GrantAndExpire()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var target = await Place(ctx, P1, "TGT");
    var bystander = await Place(ctx, P1, "OTHER");

    AssertTrue(CardEffectCommons.GainBlocker(Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, V(ctx, src)), "grant registered");
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Blocker), "TARGET gained Blocker");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, bystander, ContinuousKeywordGate.Blocker), "bystander did not");

    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Blocker), "expired at the opponent's turn end");
}

async Task LiveGate()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var target = await Place(ctx, P1, "TGT");
    CardEffectCommons.GainRush(Perm(ctx, target), EffectDuration.UntilEachTurnEnd, V(ctx, src));
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Rush), "granted while in play");
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.BattleArea, ChoiceZone.Trash));
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Rush), "leaving play turns the grant off (live CanUse mirror)");
}

async Task RefusedByImmunity()
{
    EngineContext ctx = Ctx();
    var enemySrc = await Place(ctx, P2, "ENEMYSRC");
    var target = await Place(ctx, P1, "TGT");
    ctx.EffectRegistry.Register(CardEffectFactory.CanNotAffectedStaticEffect(
        null, null, false, V(ctx, target), null).ToBinding($"cna:{target.Value}"));

    AssertTrue(!CardEffectCommons.GainJamming(Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, V(ctx, enemySrc)),
        "an immune target refuses an opponent's grant");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Jamming), "nothing registered");
}

async Task AllWrappers()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC");
    var wrappers = new (string Keyword, Func<Permanent, EffectDuration, CardSource, bool> Gain)[]
    {
        (ContinuousKeywordGate.Blocker, CardEffectCommons.GainBlocker),
        (ContinuousKeywordGate.Rush, CardEffectCommons.GainRush),
        (ContinuousKeywordGate.Piercing, CardEffectCommons.GainPierce),
        (ContinuousKeywordGate.Retaliation, CardEffectCommons.GainRetaliation),
        (ContinuousKeywordGate.Collision, CardEffectCommons.GainCollision),
        (ContinuousKeywordGate.Jamming, CardEffectCommons.GainJamming),
        (ContinuousKeywordGate.Reboot, CardEffectCommons.GainReboot),
        (ContinuousKeywordGate.Alliance, CardEffectCommons.GainAlliance),
        (ContinuousKeywordGate.Evade, CardEffectCommons.GainEvade),
        (ContinuousKeywordGate.Raid, CardEffectCommons.GainRaid),
        (ContinuousKeywordGate.Vortex, CardEffectCommons.GainVortex),
        (ContinuousKeywordGate.Execute, CardEffectCommons.GainExecute),
        (ContinuousKeywordGate.Fortitude, CardEffectCommons.GainFortitude),
        (ContinuousKeywordGate.Iceclad, CardEffectCommons.GainIceclad),
        (ContinuousKeywordGate.Barrier, CardEffectCommons.GainBarrier),
    };
    int index = 0;
    foreach (var (keyword, gain) in wrappers)
    {
        var target = await Place(ctx, P1, $"T{index++}");
        AssertTrue(gain(Perm(ctx, target), EffectDuration.UntilEachTurnEnd, V(ctx, src)), $"Gain{keyword} registered");
        AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, target, keyword), $"{keyword} live on target");
    }

    var blitzTarget = await Place(ctx, P1, "TBLITZ");
    AssertTrue(CardEffectCommons.GainBlitz(Perm(ctx, blitzTarget), EffectDuration.UntilEachTurnEnd, V(ctx, src), isWhenDigivolving: true), "GainBlitz registered");
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, blitzTarget, ContinuousKeywordGate.Blitz), "Blitz live");
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 967);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
