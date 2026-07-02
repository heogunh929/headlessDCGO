using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// P1 (G9-062): leave-play cleanup on the BATTLE and pending-sweep deletion paths. AS-IS effects die with
// the permanent; the headless battle path previously never dropped the dead card's bindings (a
// battle-deleted card's continuous buff kept applying) and, once dropped, the deletion-time keyword state
// must be snapshotted so keyword-granted POST replacements (Ascension …) still fire.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:atk");
HeadlessEntityId TargetId = new("p2:tgt");
HeadlessEntityId AllyId = new("p2:ally");

var tests = new (string Name, Func<Task> Body)[]
{
    ("(P1) a battle-deleted card's continuous buff STOPS applying (binding drop — was leaking)", BattleDeletionDropsBindings),
    ("(P1) a battle-deleted keyword-granted Ascension still opens its POST window (snapshot)", BattleDeletionSnapshotsKeywords),
    ("(P1) the pending-sweep finish also drops the dead card's bindings", SweepFinishDropsBindings),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task BattleDeletionDropsBindings()
{
    (DcgoMatch match, EngineContext ctx) = await BattleSetup(attackerDp: 5000, targetDp: 3000);

    // The doomed defender grants "+1000 DP to the owner's Digimon" (player-scope) — alive it buffs the ally.
    ctx.EffectRegistry.Register(CardEffectFactory.ChangeDPStaticEffect(
        null, 1000, false, new CardSource(ctx, TargetId, P2), null).ToBinding($"buff:{TargetId.Value}"));
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, AllyId, 3000) == 4000, "precondition: the buff applies while alive");

    await DriveAttackAsync(match);

    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, TargetId), "the defender was deleted by battle");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, AllyId, 3000) == 3000,
        "the dead card's buff no longer applies (bindings dropped — previously leaked)");
}

async Task BattleDeletionSnapshotsKeywords()
{
    (DcgoMatch match, EngineContext ctx) = await BattleSetup(attackerDp: 5000, targetDp: 3000);

    // Keyword-granted Ascension (no metadata flag) — must survive the binding drop via the snapshot.
    ctx.EffectRegistry.Register(CardEffectFactory.AscensionSelfEffect(
        false, new CardSource(ctx, TargetId, P2), null).ToBinding($"asc:{TargetId.Value}"));

    await DriveAttackAsync(match);

    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, TargetId), "the defender was deleted by battle");
    AssertTrue(ReadFlag(ctx, TargetId, DeletionReplacementGate.HasAscensionKey),
        "the deletion-time keyword state was snapshotted");
    AssertTrue(match.Context.ChoiceController.Current.IsPending &&
        match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.DeletionReplacement,
        "the POST (Ascension) window opened for the battle-deleted keyword holder");
}

async Task SweepFinishDropsBindings()
{
    EngineContext ctx = Ctx();
    var holder = await Place(ctx, P1, "HOLDER", dp: 3000);
    var ally = await Place(ctx, P1, "ALLY", dp: 3000);
    ctx.EffectRegistry.Register(CardEffectFactory.ChangeDPStaticEffect(
        null, 1000, false, new CardSource(ctx, holder, P1), null).ToBinding($"buff:{holder.Value}"));
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, ally, 3000) == 4000, "precondition: buff applies");

    // Deferred deletion (pendingDeletion + declined) -> the sweep finishes it.
    SetFlags(ctx, holder, new Dictionary<string, object?>
    {
        [GameFlowProcessor.PendingDeletionKey] = true,
        [DeletionReplacementGate.DeletedByEffectKey] = true,
        [DeletionReplacementTiming.ReplacementDeclinedKey] = true,
    });
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InZone(ctx, P1, ChoiceZone.Trash, holder), "the sweep finished the deferred deletion");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, ally, 3000) == 3000, "the dead card's buff no longer applies");
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 962);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<(DcgoMatch, EngineContext)> BattleSetup(int attackerDp, int targetDp)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 962);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 962, setup: setup));
    await AdvanceToMainAsync(match);

    await PlaceExisting(context, P1, AttackerId, "ATK", attackerDp, suspended: false);
    await PlaceExisting(context, P2, TargetId, "TGT", targetDp, suspended: true);
    await PlaceExisting(context, P2, AllyId, "ALLY", 3000, suspended: false);
    return (match, context);
}

async Task DriveAttackAsync(DcgoMatch match)
{
    LegalAction attack = match.GetLegalActions(P1)
        .Single(a => a.ActionType == HeadlessActionTypes.DeclareAttack &&
            a.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackTargetId, out object? raw) &&
            (raw is HeadlessEntityId id ? id.Value : raw?.ToString()) == TargetId.Value);
    await match.ApplyActionAsync(attack);
    await match.StepAsync();
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int dp)
{
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    await PlaceExisting(ctx, owner, id, tag, dp, suspended: false);
    return id;
}

async Task PlaceExisting(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id, string tag, int dp, bool suspended)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 }, CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = suspended }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

void SetFlags(EngineContext ctx, HeadlessEntityId id, IReadOnlyDictionary<string, object?> values)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    var metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal);
    foreach (var pair in values) metadata[pair.Key] = pair.Value;
    ctx.CardInstanceRepository.Upsert(r with { Metadata = metadata });
}

async Task AdvanceToMainAsync(DcgoMatch match)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }
}

bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(id);

bool ReadFlag(EngineContext ctx, HeadlessEntityId id, string key) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
