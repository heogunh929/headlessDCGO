using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// K5 (G9-060): MindLink un-seal. AS-IS MindLinkClass is an OnDeclaration PROCESS (not a keyword): select 1
// of the owner's battle-area permanents with NO Tamer among its digivolution cards (non-token, matching
// digimonCondition) and place the Tamer PERMANENT at the bottom of its digivolution cards. The reverse
// (PlayMindLinkTamerFromDigivolutionCards) plays the TAMER under-card back out — the previous Digimon-only
// candidate filter could never surface a tamer (fixed here too).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("CanSelectPermanentCondition mirrors AS-IS (token / tamer-under / condition / other-owner excluded)", CandidateFilter),
    ("BuildRequest is optional (canSkip, min 0, max 1) for the tamer's owner", RequestShape),
    ("MindLink places the tamer at the BOTTOM of the Digimon's digivolution cards and it leaves play", PlacesTamerBottom),
    ("A tamer with its own under-card re-parents the whole stack (order preserved)", ReparentsTamerStack),
    ("The tamer's registered bindings are removed on leaving play", RemovesBindings),
    ("PlayMindLinkTamerFromDigivolutionCards surfaces the linked TAMER (name-narrowed) and plays it back", PlaysTamerBack),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task CandidateFilter()
{
    EngineContext ctx = Ctx();
    var tamer = await Place(ctx, P1, "TAMER", cardType: "Tamer");
    var ok = await Place(ctx, P1, "OK", level: 4);
    var lowLevel = await Place(ctx, P1, "LOW", level: 3);
    var token = await Place(ctx, P1, "TOKEN", level: 4, isToken: true);
    var linked = await Place(ctx, P1, "LINKED", level: 4);
    var otherTamer = await Place(ctx, P1, "OTHERTAMER", cardType: "Tamer");
    await PlaceUnder(ctx, linked, otherTamer);          // already has a Tamer under it
    var enemy = await Place(ctx, P2, "ENEMY", level: 4);

    var mindLink = new MindLinkClass(new Permanent(ctx, tamer, P1), p => p.Level == 4, null);
    IReadOnlyList<HeadlessEntityId> candidates = mindLink.Candidates();

    AssertTrue(candidates.Contains(ok), "a matching Digimon is a candidate");
    AssertTrue(!candidates.Contains(lowLevel), "digimonCondition is honored (Lv3 excluded)");
    AssertTrue(!candidates.Contains(token), "a token is excluded (AS-IS !IsToken)");
    AssertTrue(!candidates.Contains(linked), "a permanent already holding a Tamer under-card is excluded");
    AssertTrue(!candidates.Contains(enemy), "only the tamer owner's battle area is scanned");
}

async Task RequestShape()
{
    EngineContext ctx = Ctx();
    var tamer = await Place(ctx, P1, "TAMER", cardType: "Tamer");
    await Place(ctx, P1, "OK", level: 4);

    ChoiceRequest request = new MindLinkClass(new Permanent(ctx, tamer, P1), null, null).BuildRequest();
    AssertTrue(request.CanSkip, "the selection is optional (AS-IS canNoSelect: true)");
    AssertTrue(request.MinCount == 0 && request.MaxCount == 1, "min 0 / max 1");
    AssertTrue(request.PlayerId == P1, "the tamer's owner selects");
}

async Task PlacesTamerBottom()
{
    EngineContext ctx = Ctx();
    var tamer = await Place(ctx, P1, "TAMER", cardType: "Tamer");
    var digimon = await Place(ctx, P1, "DIGIMON", level: 4);
    var existingSource = await PlaceUnder(ctx, digimon, await OffField(ctx, P1, "SRC"));

    bool applied = await new MindLinkClass(new Permanent(ctx, tamer, P1), null, null).MindLink(digimon);
    AssertTrue(applied, "MindLink applied");

    var zones = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(!zones.GetCards(P1, ChoiceZone.BattleArea).Contains(tamer), "the tamer left the battle area");
    IReadOnlyList<HeadlessEntityId> sources = Sources(ctx, digimon);
    AssertTrue(sources.Count == 2 && sources[^1] == tamer, "the tamer is the BOTTOM digivolution card");
    AssertTrue(sources[0] == existingSource, "the existing source order is preserved");
}

async Task ReparentsTamerStack()
{
    EngineContext ctx = Ctx();
    var tamer = await Place(ctx, P1, "TAMER", cardType: "Tamer");
    var tamerUnder = await PlaceUnder(ctx, tamer, await OffField(ctx, P1, "TUNDER"));
    var digimon = await Place(ctx, P1, "DIGIMON", level: 4);

    AssertTrue(await new MindLinkClass(new Permanent(ctx, tamer, P1), null, null).MindLink(digimon), "MindLink applied");

    IReadOnlyList<HeadlessEntityId> sources = Sources(ctx, digimon);
    AssertTrue(sources.Count == 2 && sources[0] == tamer && sources[1] == tamerUnder,
        "the tamer permanent's whole stack was re-parented in order (tamer, then its under-card)");
    AssertTrue(Sources(ctx, tamer).Count == 0, "the tamer no longer owns its old under-card");
}

async Task RemovesBindings()
{
    EngineContext ctx = Ctx();
    var tamer = await Place(ctx, P1, "TAMER", cardType: "Tamer");
    var digimon = await Place(ctx, P1, "DIGIMON", level: 4);
    ctx.EffectRegistry.Register(CardEffectFactory.BlockerSelfStaticEffect(false, new CardSource(ctx, tamer, P1), null)
        .ToBinding($"tamer-eff:{tamer.Value}"));
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, tamer, ContinuousKeywordGate.Blocker), "precondition: the binding is live");

    AssertTrue(await new MindLinkClass(new Permanent(ctx, tamer, P1), null, null).MindLink(digimon), "MindLink applied");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, tamer, ContinuousKeywordGate.Blocker),
        "the tamer's registered binding was removed on leaving play");
}

async Task PlaysTamerBack()
{
    EngineContext ctx = Ctx();
    var tamer = await Place(ctx, P1, "TAMER", cardType: "Tamer");
    var digimon = await Place(ctx, P1, "DIGIMON", level: 4);
    AssertTrue(await new MindLinkClass(new Permanent(ctx, tamer, P1), null, null).MindLink(digimon), "MindLink applied");

    var playBack = (ActivatedPlayFromUnderEffect)CardEffectFactory.PlayMindLinkTamerFromDigivolutionCards(
        new CardSource(ctx, tamer, P1), cardName: "TAMER", effectDescription: "");
    ChoiceRequest request = playBack.BuildRequest(new[] { P1 });
    AssertTrue(request.Candidates.Any(c => c.Id == tamer), "the linked TAMER under-card is a candidate (Digimon-only filter fixed)");

    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null, ctx.EffectRegistry);
    playBack.Apply(sink, new[] { tamer });
    await sink.FlushAsync();

    var zones = (IZoneStateReader)ctx.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(tamer), "the tamer is back on the battle area");
    AssertTrue(Sources(ctx, digimon).Count == 0, "the tamer left the digivolution stack");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 960);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level = 4, string cardType = "Digimon", bool isToken = false)
{
    HeadlessEntityId id = await OffField(ctx, owner, tag, level, cardType, isToken);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

Task<HeadlessEntityId> OffField(EngineContext ctx, HeadlessPlayerId owner, string tag, int level = 4, string cardType = "Digimon", bool isToken = false)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = level }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false };
    if (isToken)
    {
        metadata["isToken"] = true;
    }

    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: metadata));
    return Task.FromResult(id);
}

// Appends `under` to `host`'s digivolution sources (bottom) — staged through the trash because the
// zone mover rejects a None->None move.
async Task<HeadlessEntityId> PlaceUnder(EngineContext ctx, HeadlessEntityId host, HeadlessEntityId under)
{
    if (!ctx.CardInstanceRepository.TryGetInstance(under, out CardInstanceRecord? rec) || rec is null)
    {
        throw new InvalidOperationException($"no record for {under.Value}");
    }

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(rec.OwnerId, under, ChoiceZone.None, ChoiceZone.Trash));
    await DigivolutionStackHelpers.AddSourcesBottomAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, host, new[] { under }, ChoiceZone.Trash);
    return under;
}

IReadOnlyList<HeadlessEntityId> Sources(EngineContext ctx, HeadlessEntityId host)
{
    if (!ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec) || rec is null ||
        !rec.Metadata.TryGetValue(DigivolutionStackHelpers.SourceIdsKey, out object? raw) || raw is null)
    {
        return Array.Empty<HeadlessEntityId>();
    }

    return raw switch
    {
        IEnumerable<HeadlessEntityId> ids => ids.ToArray(),
        IEnumerable<string> strings => strings.Select(v => new HeadlessEntityId(v)).ToArray(),
        _ => Array.Empty<HeadlessEntityId>(),
    };
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
