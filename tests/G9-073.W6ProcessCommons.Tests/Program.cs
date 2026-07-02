using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// (W6 process commons) — same-name mirrors of the AS-IS coroutine processes (verbatim verified):
// ChangeDigimonDP/SAttack (timed target modifier), ChangeDigimonDPPlayerEffect (timed player-scope),
// AddThisCardToHand, PlayPermanentCards (filter + sink play), AddEffectToPermanent/Player (duration
// re-registration), DigivolveIntoHandOrTrashCard (recipe mis-map corrected: digivolve INTO from
// hand/trash), SelectTrashDigivolutionCards, DNADigivolvePermanentsIntoHandOrTrashCard.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ChangeDigimonDP/SAttack: timed target modifier folds and expires", TimedStatMods),
    ("ChangeDigimonDPPlayerEffect: player-scope predicate modifier, duration-tagged", PlayerScopeDp),
    ("AddThisCardToHand + PlayPermanentCards: sink-driven moves, option filtered, cost paid", HandAndPlay),
    ("AddEffectToPermanent: any ICardEffect re-registered with a duration on the target", AddEffectTo),
    ("DigivolveIntoHandOrTrashCard: hand pick digivolves ONTO the target; success/failure branch", DigivolveInto),
    ("SelectTrashDigivolutionCards: host pick then source picks, budget respected", SelectTrashSources),
    ("DNADigivolve...: hand pick fuses two battle materials via the DNA pipeline", DnaDigivolve),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task TimedStatMods()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var target = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea, dp: 5000);

    AssertTrue(CardEffectCommons.ChangeDigimonDP(Perm(ctx, target), 2000, EffectDuration.UntilOpponentTurnEnd, V(ctx, src)), "DP grant");
    AssertEqual(7000, ContinuousDpGate.ResolveDp(ctx, target, 5000), "+2000 folded");
    AssertTrue(CardEffectCommons.ChangeDigimonSAttack(Perm(ctx, target), 1, EffectDuration.UntilOpponentTurnEnd, V(ctx, src)), "SA grant");
    AssertEqual(2, ContinuousModifierGate.ResolveSecurityAttack(ctx, target, baseSecurityAttack: 1), "+1 SA folded");

    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);
    AssertEqual(5000, ContinuousDpGate.ResolveDp(ctx, target, 5000), "DP expired at the boundary");
    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(ctx, target, baseSecurityAttack: 1), "SA expired");
}

async Task PlayerScopeDp()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var big = await Put(ctx, P1, "BIG", ChoiceZone.BattleArea, level: 6);
    var small = await Put(ctx, P1, "SMALL", ChoiceZone.BattleArea, level: 3);

    AssertTrue(CardEffectCommons.ChangeDigimonDPPlayerEffect(p => p.Level >= 6, 3000, EffectDuration.UntilOpponentTurnEnd, V(ctx, src)), "grant");
    AssertEqual(8000, ContinuousDpGate.ResolveDp(ctx, big, 5000), "matching digimon buffed");
    AssertEqual(5000, ContinuousDpGate.ResolveDp(ctx, small, 5000), "non-matching untouched (predicate 1:1)");

    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);
    AssertEqual(5000, ContinuousDpGate.ResolveDp(ctx, big, 5000), "expired");
}

async Task HandAndPlay()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var trashCard = await Put(ctx, P1, "TC", ChoiceZone.Trash);
    await CardEffectCommons.AddThisCardToHand(V(ctx, trashCard), V(ctx, src));
    AssertTrue(InZone(ctx, P1, ChoiceZone.Hand, trashCard), "AddThisCardToHand moved it");

    var digimon = await Put(ctx, P1, "PLAYME", ChoiceZone.Trash, playCost: 3);
    var option = await Put(ctx, P1, "OPT", ChoiceZone.Trash, cardType: "Option");
    await CardEffectCommons.PlayPermanentCards(
        new[] { V(ctx, digimon), V(ctx, option) }, V(ctx, src),
        payCost: true, isTapped: true, root: ChoiceZone.Trash, activateETB: true);

    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, digimon), "digimon played from trash");
    AssertTrue(!InZone(ctx, P1, ChoiceZone.BattleArea, option), "option filtered out (CanPlayAsNewPermanent)");
    AssertEqual(2, ctx.MemoryController.Current.Current, "play cost 3 paid");
    ctx.CardInstanceRepository.TryGetInstance(digimon, out CardInstanceRecord? played);
    AssertTrue(played!.Metadata.TryGetValue("isSuspended", out object? tap) && tap is true, "isTapped honoured");
}

async Task AddEffectTo()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var target = await Put(ctx, P1, "TGT", ChoiceZone.BattleArea);

    ICardEffect blocker = CardEffectFactory.BlockerSelfStaticEffect(false, V(ctx, src), null);
    CardEffectCommons.AddEffectToPermanent(Perm(ctx, target), EffectDuration.UntilOpponentTurnEnd, V(ctx, src), blocker, EffectTiming.None);
    AssertTrue(ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Blocker), "the effect was re-targeted at the permanent");
    EffectDurationExpiry.ExpireTurnEnd(ctx.EffectRegistry, P2);
    AssertTrue(!ContinuousKeywordGate.HasKeyword(ctx, target, ContinuousKeywordGate.Blocker), "expired at the duration boundary");
}

async Task DigivolveInto()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(5);
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var target = await Put(ctx, P1, "BASE", ChoiceZone.BattleArea, level: 4);
    var evo = await Put(ctx, P1, "EVO", ChoiceZone.Hand, level: 5, evoCost: 2, evoCondition: "level=4");

    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(evo));
    bool succeeded = false;
    await CardEffectCommons.DigivolveIntoHandOrTrashCard(
        Perm(ctx, target), cardCondition: null, payCost: true,
        reduceCostTuple: (1, null), fixedCostTuple: null, ignoreDigivolutionRequirementFixedCost: -1,
        isHand: true, V(ctx, src),
        successProcess: () => { succeeded = true; return Task.CompletedTask; });

    AssertTrue(succeeded, "success branch ran");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, evo), "the hand card digivolved ONTO the target (mis-map corrected)");
    ctx.CardInstanceRepository.TryGetInstance(evo, out CardInstanceRecord? rec);
    var sources = (rec!.Metadata[DigivolutionStackReader.SourceIdsKey] as IEnumerable<string>)?.ToArray() ?? Array.Empty<string>();
    AssertTrue(sources.Contains(target.Value), "the target folded under");
    AssertEqual(4, ctx.MemoryController.Current.Current, "evolution cost 2 - reduce 1 = 1 paid");

    bool failed = false;
    await CardEffectCommons.DigivolveIntoHandOrTrashCard(
        Perm(ctx, src), cs => cs.EqualsCardName("NOSUCH"), payCost: false,
        null, null, -1, isHand: true, V(ctx, src),
        successProcess: null, failedProcess: () => { failed = true; return Task.CompletedTask; });
    AssertTrue(failed, "no candidate -> failure branch");
}

async Task SelectTrashSources()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var host = await Put(ctx, P2, "HOST", ChoiceZone.BattleArea);
    var u1 = await Put(ctx, P2, "U1", ChoiceZone.Trash);
    var u2 = await Put(ctx, P2, "U2", ChoiceZone.Trash);
    await DigivolutionStackHelpers.AddSourcesBottomAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, new[] { u1, u2 }, ChoiceZone.Trash);

    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(host));
    provider.Enqueue(ChoiceResult.Select(u1));
    Permanent? reportedHost = null;
    await CardEffectCommons.SelectTrashDigivolutionCards(
        permanentCondition: null, cardCondition: null, maxCount: 1, canNoTrash: false,
        isFromOnly1Permanent: true, V(ctx, src),
        afterSelectionCoroutine: (h, picks) => { reportedHost = h; return Task.CompletedTask; });

    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, u1), "the picked source was trashed");
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec);
    var remaining = (rec!.Metadata[DigivolutionStackReader.SourceIdsKey] as IEnumerable<string>)?.ToArray() ?? Array.Empty<string>();
    AssertTrue(remaining.Contains(u2.Value) && !remaining.Contains(u1.Value), "only the pick left the stack");
    AssertTrue(reportedHost?.InstanceId == host, "afterSelection callback saw the host");
}

async Task DnaDigivolve()
{
    SpecialPlayRecipeRegistry.Clear();
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var fused = await Put(ctx, P1, "OMNI", ChoiceZone.Hand, name: "Omnimon", cardNumber: "DNA-1");
    var m1 = await Put(ctx, P1, "WG", ChoiceZone.BattleArea, name: "WarGreymon");
    var m2 = await Put(ctx, P1, "MG", ChoiceZone.BattleArea, name: "MetalGarurumon");

    CardEffectFactory.GetJogressConditionClass(
        p => p.TopCard.EqualsCardName("WarGreymon"), "WG",
        p => p.TopCard.EqualsCardName("MetalGarurumon"), "MG",
        V(ctx, fused));

    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Enqueue(ChoiceResult.Select(fused));
    CardSource? reported = null;
    await CardEffectCommons.DNADigivolvePermanentsIntoHandOrTrashCard(
        canSelectDNACardCondition: null, payCost: true, isHand: true, V(ctx, src),
        successProcess: cs => { reported = cs; return Task.CompletedTask; });

    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, fused), "the DNA card fused onto the battle area");
    AssertTrue(reported?.InstanceId == fused, "success carries the fused card");
    ctx.CardInstanceRepository.TryGetInstance(fused, out CardInstanceRecord? rec);
    var sources = (rec!.Metadata[DigivolutionStackReader.SourceIdsKey] as IEnumerable<string>)?.ToArray() ?? Array.Empty<string>();
    AssertTrue(sources.Contains(m1.Value) && sources.Contains(m2.Value), "both materials folded under");
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 973);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone,
    string cardType = "Digimon", int dp = 5000, int level = 4, int? playCost = null, string? name = null,
    string? cardNumber = null, int? evoCost = null, string? evoCondition = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, cardNumber ?? tag, name ?? tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level },
        CardType: cardType, PlayCost: playCost, EvolutionCost: evoCost, EvolutionCondition: evoCondition));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;
bool InZone(EngineContext ctx, HeadlessPlayerId p, ChoiceZone z, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(p, z).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }
}
