// PRIM special-play: Jogress by levels — AddJogressLevelsEffect makes a card count as extra level(s) when used
// as a Jogress/DNA material against the digivolving card (AS-IS AddJogressLevelsClass). BT20_025: "also treated
// as level 6 when the digivolving card is Examon". Verified via CardSource.JogressLevelsAgainst.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("printed level only when no AddJogressLevels effect is active", PrintedOnly),
    ("gains the extra level when the digivolving card matches (Examon)", GainsWhenMatch),
    ("does NOT gain the extra level when the digivolving card does not match", NoGainWhenNoMatch),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task PrintedOnly()
{
    EngineContext ctx = Ctx();
    var mat = Card(ctx, "MAT", level: 4);
    var examon = Card(ctx, "Examon", level: 7);
    await PlaceOnField(ctx, mat);
    // (P7 test-fix) CanUse recurses into CanTrigger/CanActivate, which (AS-IS) read live game state through
    // the process-global GManager.instance (mirror: AmbientMatchContext) — scope the match for the duration
    // of the live scan, matching the pattern NewModelContinuousScan's public entry points already use.
    using var _ambientScope1 = AmbientMatchContext.Enter(ctx);
    var levels = new CardSource(ctx, mat, P1).JogressLevelsAgainst(new CardSource(ctx, examon, P1));
    AssertSeq(new[] { 4 }, levels, "printed level 4 only (no effect registered)");
}

async Task GainsWhenMatch()
{
    EngineContext ctx = Ctx();
    var mat = Card(ctx, "MAT", level: 4);
    var examon = Card(ctx, "Examon", level: 7);
    await PlaceOnField(ctx, mat);
    RegisterJogressLevels(ctx, mat, (jc, _) => jc.CardNames.Contains("Examon") ? new List<int> { 6 } : new List<int>());
    // (P7 test-fix) CanUse recurses into CanTrigger/CanActivate, which (AS-IS) read live game state through
    // the process-global GManager.instance (mirror: AmbientMatchContext) — scope the match for the duration
    // of the live scan, matching the pattern NewModelContinuousScan's public entry points already use.
    using var _ambientScope1 = AmbientMatchContext.Enter(ctx);
    var levels = new CardSource(ctx, mat, P1).JogressLevelsAgainst(new CardSource(ctx, examon, P1));
    AssertSeq(new[] { 4, 6 }, levels, "printed 4 + treated-as 6 for Examon");
}

async Task NoGainWhenNoMatch()
{
    EngineContext ctx = Ctx();
    var mat = Card(ctx, "MAT", level: 4);
    var other = Card(ctx, "Other", level: 7);
    await PlaceOnField(ctx, mat);
    RegisterJogressLevels(ctx, mat, (jc, _) => jc.CardNames.Contains("Examon") ? new List<int> { 6 } : new List<int>());
    using var _ambientScope2 = AmbientMatchContext.Enter(ctx);
    var levels = new CardSource(ctx, mat, P1).JogressLevelsAgainst(new CardSource(ctx, other, P1));
    AssertSeq(new[] { 4 }, levels, "printed 4 only (digivolving card is not Examon)");
}

// --- Harness ---
EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 74);
    // (P7 test-fix) CanTrigger/CanUse gate on DoneStartGame (mirror proxy: phase past None/Setup).
    ctx.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}
HeadlessEntityId Card(EngineContext ctx, string cardNumber, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{cardNumber}"), cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"1:{cardNumber}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{cardNumber}"), P1, Metadata: new Dictionary<string, object?>()));
    return id;
}

// AS-IS CardSource.JogressLevelsAgainst (Permanent.cs:3554-3605, mirror CardSource.cs:512-545) scans every
// FIELD permanent's (battle + breeding area) live EffectList(EffectTiming.None) for an IAddJogressLevelsEffect
// (Player.GetFieldPermanents) — the material card must actually be on the field for the scan to see its
// granted effect.
async Task PlaceOnField(EngineContext ctx, HeadlessEntityId id) =>
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));

// Live-scan Jogress-levels declaration (the AddJogressLevelsClass new-model kind-class continuous surface).
// AS-IS CardSource.JogressLevelsAgainst scans live EffectList(EffectTiming.None) — there is no EffectRegistry
// fallback (same "flip's live enumeration replaces the retired registry-key fold" shift as CardSource
// .LinkConditionOf). So the fixture attaches the AddJogressLevelsClass via the card's
// cEntity_EffectController.cEntity_Effect probe instead of registering an EffectBinding the live scan never
// reads.
void RegisterJogressLevels(EngineContext ctx, HeadlessEntityId matId, Func<CardSource, Permanent, List<int>> getLevels)
{
    var card = new CardSource(ctx, matId, P1);
    var effect = CardEffectFactory.AddJogressLevelsEffect(card, getLevels);
    card.cEntity_EffectController.cEntity_Effect = new JogressLevelsProbe(effect);
}

void AssertSeq(int[] expected, IReadOnlyList<int> actual, string label)
{
    if (!expected.OrderBy(x => x).SequenceEqual(actual.OrderBy(x => x)))
        throw new InvalidOperationException($"{label}: expected [{string.Join(",", expected)}], actual [{string.Join(",", actual)}].");
}

sealed class JogressLevelsProbe : CEntity_Effect
{
    readonly ICardEffect _effect;
    public JogressLevelsProbe(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) =>
        timing == EffectTiming.None ? new List<ICardEffect> { _effect } : new List<ICardEffect>();
}
