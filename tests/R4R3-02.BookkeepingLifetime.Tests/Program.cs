using System.Reflection;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// (RD-R3-02) PermanentBookkeepingStore lifetime witness. Review 3 P1-2: the CREATE/DIE Reset seats lived only
// on the legacy direct-call paths (CardObjectController.CreateNewPermanent / RemoveField), so the mainstream
// paths bypassed them — effect-play (sink ApplyPlayCard raw zone move), deletion finalize (sink immediate +
// GameFlowProcessor deferred raw field->Trash moves), bounce/deck-return/security-return — leaving a re-played
// card reading its PREVIOUS life's just-after bookkeeping (AS-IS: a new Permanent object == all defaults) and
// dead tops' entries lingering forever. The remediation centralizes CREATE/DIE at the single physical zone
// chokepoint (InMemoryZoneMover.MoveCard: any change of field-zone membership resets the entry) with top-swap
// moves marked PermanentContinuityKey (their ops ReKey instead), and fixes the ReKey no-op leak (old-top entry
// absent -> the NEW top's stale entry is now removed). This witness pins:
//   1. effect-play re-entry (REAL sink path): play -> stamp -> sink Delete -> sink PlayCard from trash ->
//      all 9 AS-IS fields read defaults;
//   2. bounce -> re-play (sink ReturnToHand then PlayCard from hand) -> defaults;
//   3. deletion finalize (the GameFlowProcessor deferred-finalize raw field->Trash move) -> entry ABSENT;
//   4. top swap continuity: digivolve (Permanent.AddCardSource -> AttachTargetAsSource ReKey) preserves the
//      values on the new top; de-digivolve (DeDigivolveHelpers promote ReKey) carries them back;
//   5. ReKey stale-block: digivolving onto an entry-less permanent clears the new top's previous-life entry;
//   6. breeding promote (field->field) keeps the same permanent's entry (no reset).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("effect-play re-entry via sink Delete + PlayCard(fromZone: Trash): all 9 fields default", EffectPlayReentry),
    ("bounce via sink ReturnToHand + re-play via sink PlayCard(fromZone: Hand): all 9 fields default", BounceReplay),
    ("deletion finalize (GameFlowProcessor deferred raw field->Trash move): entry absent", DeferredDeletionFinalize),
    ("digivolve (AddCardSource ReKey) preserves values; de-digivolve (promote ReKey) carries back", TopSwapContinuity),
    ("digivolve onto entry-less permanent: new top's stale previous-life entry cleared", StaleNewTopCleared),
    ("breeding promote (BreedingArea->BattleArea): same permanent, entry preserved", BreedingPromoteKeeps),
    ("free digivolve (FuseAsync D-6 Blast/Arts top swap) ReKeys: bookkeeping survives on the new top", FreeDigivolveContinuity),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- 1. effect-play re-entry (the REAL sink path) ---------------------------

async Task EffectPlayReentry()
{
    EngineContext context = NewContext();
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
    HeadlessEntityId card = await PlaceOnField(context, "W1CARD");

    StampAllNine(context, card);
    AssertStamped(context, card, "stamps visible before deletion (write path sane)");
    AssertTrue(StoreHasEntry(context.CardInstanceRepository, card), "entry exists after stamping");

    // effect deletion through the REAL sink path (DeleteKind staged + flushed).
    MatchStateMutationSink sink = NewSink(context);
    sink.Apply(Mutation(MatchStateMutationSink.DeleteKind, card));
    await sink.FlushAsync();
    AssertTrue(InZone(context, P1, card, ChoiceZone.Trash), "deleted card is in the trash");
    AssertTrue(!StoreHasEntry(context.CardInstanceRepository, card), "DIE: dead top's entry is gone (no permanent leak)");

    // effect re-play FROM THE TRASH through the REAL sink path (PlayCardKind, fromZone Trash).
    MatchStateMutationSink playSink = NewSink(context);
    playSink.Apply(Mutation(MatchStateMutationSink.PlayCardKind, card, extra: new() { [MatchStateMutationSink.FromZoneKey] = "Trash" }));
    await playSink.FlushAsync();
    AssertTrue(InZone(context, P1, card, ChoiceZone.BattleArea), "re-played card is on the battle area");

    AssertAllNineDefaults(context, card, "re-played card reads AS-IS `new Permanent(...)` defaults, not its previous life");
}

// --- 2. bounce -> re-play ----------------------------------------------------

async Task BounceReplay()
{
    EngineContext context = NewContext();
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
    HeadlessEntityId card = await PlaceOnField(context, "W2CARD");

    StampAllNine(context, card);

    // bounce to hand through the sink (ReturnToHandKind -> AddToHandAsync withdraw+insert).
    MatchStateMutationSink bounceSink = NewSink(context);
    bounceSink.Apply(Mutation(MatchStateMutationSink.ReturnToHandKind, card));
    await bounceSink.FlushAsync();
    AssertTrue(InZone(context, P1, card, ChoiceZone.Hand), "bounced card is in hand");
    AssertTrue(!StoreHasEntry(context.CardInstanceRepository, card), "DIE: bounced top's entry is gone");

    // effect re-play from hand.
    MatchStateMutationSink playSink = NewSink(context);
    playSink.Apply(Mutation(MatchStateMutationSink.PlayCardKind, card, extra: new() { [MatchStateMutationSink.FromZoneKey] = "Hand" }));
    await playSink.FlushAsync();
    AssertTrue(InZone(context, P1, card, ChoiceZone.BattleArea), "re-played card is on the battle area");

    AssertAllNineDefaults(context, card, "re-played (bounced) card reads AS-IS defaults");
}

// --- 3. deletion finalize (GameFlowProcessor deferred path's move op) --------

async Task DeferredDeletionFinalize()
{
    EngineContext context = NewContext();
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
    HeadlessEntityId card = await PlaceOnField(context, "W3CARD");

    StampAllNine(context, card);
    AssertTrue(StoreHasEntry(context.CardInstanceRepository, card), "entry exists after stamping");

    // The deferred-deletion finisher (GameFlowProcessor.RuleProcessAsync :406) and the no-DP trash
    // (:640) finalize with THIS exact raw move — field zone -> Trash on the context zone mover.
    await context.ZoneMover.MoveAsync(
        new ZoneMoveRequest(P1, card, ChoiceZone.BattleArea, ChoiceZone.Trash,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.DeletionBatchIdKey] = context.NextDeletionBatchId(),
            }));

    AssertTrue(InZone(context, P1, card, ChoiceZone.Trash), "finalized card is in the trash");
    AssertTrue(!StoreHasEntry(context.CardInstanceRepository, card), "DIE: the deferred-finalize move resets the entry (no permanent leak)");
}

// --- 4. top swap continuity (ReKey seats preserved) ---------------------------

async Task TopSwapContinuity()
{
    EngineContext context = NewContext();
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
    HeadlessEntityId baseCard = await PlaceOnField(context, "W4BASE");
    HeadlessEntityId evoCard = await PlaceInHand(context, "W4EVO");

    StampAllNine(context, baseCard);

    // digivolve: the executor's top-swap op (old top -> None, new top -> field, AttachTargetAsSource ReKey).
    var basePermanent = new Cec.Permanent(context, baseCard, P1);
    Cec.Permanent evolved = await basePermanent.AddCardSource(new Cec.CardSource(context, evoCard, P1, P1));

    AssertTrue(InZone(context, P1, evoCard, ChoiceZone.BattleArea), "new top is on the battle area");
    AssertTrue(!StoreHasEntry(context.CardInstanceRepository, baseCard), "old top key re-keyed away");
    AssertStamped(context, evoCard, "PERSIST: the permanent's bookkeeping survives the digivolve on the new top");
    _ = evolved;

    // de-digivolve back: the promote's top-swap (top -> Trash, under-card -> field, ReKey).
    int removed = await DeDigivolveHelpers.DeDigivolveAsync(
        context.CardInstanceRepository, context.ZoneMover, evoCard, count: 1);
    AssertEqual(1, removed, "one top de-digivolved");
    AssertTrue(InZone(context, P1, baseCard, ChoiceZone.BattleArea), "promoted under-card is the top again");
    AssertTrue(!StoreHasEntry(context.CardInstanceRepository, evoCard), "trashed top key re-keyed away");
    AssertStamped(context, baseCard, "PERSIST: the bookkeeping carries back to the promoted top");
}

// --- 5. ReKey stale-block ------------------------------------------------------

async Task StaleNewTopCleared()
{
    EngineContext context = NewContext();
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
    HeadlessEntityId baseCard = await PlaceOnField(context, "W5BASE");   // NO bookkeeping entry (e.g. a token life)
    HeadlessEntityId evoCard = await PlaceInHand(context, "W5EVO");

    // a STALE previous-life entry under the future new-top key (the pre-fix ReKey no-op leak shape).
    Cec.PermanentBookkeepingStore.Get(context.CardInstanceRepository, evoCard).LevelJustAfterPlayed = 7;
    AssertTrue(StoreHasEntry(context.CardInstanceRepository, evoCard), "stale entry seeded under the new-top key");
    AssertTrue(!StoreHasEntry(context.CardInstanceRepository, baseCard), "old top has NO entry (the no-op branch)");

    var basePermanent = new Cec.Permanent(context, baseCard, P1);
    await basePermanent.AddCardSource(new Cec.CardSource(context, evoCard, P1, P1));

    AssertTrue(!StoreHasEntry(context.CardInstanceRepository, evoCard), "ReKey stale-block: the new top's previous-life entry is removed");
    AssertAllNineDefaults(context, evoCard, "the persisting (never-stamped) permanent reads AS-IS field defaults");
}

// --- 6. breeding promote is NOT a lifetime boundary ----------------------------

async Task BreedingPromoteKeeps()
{
    EngineContext context = NewContext();
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
    HeadlessEntityId card = await PlaceOnField(context, "W6CARD", breeding: true);

    StampAllNine(context, card);

    IReadOnlyList<HeadlessEntityId> moved = await context.ZoneMover.MoveBreedingToBattleAsync(P1);
    AssertEqual(1, moved.Count, "one card promoted from breeding");
    AssertTrue(InZone(context, P1, card, ChoiceZone.BattleArea), "promoted card is on the battle area");
    AssertStamped(context, card, "field->field move keeps the SAME AS-IS Permanent object's bookkeeping");
}

// --- 7. free digivolve (FuseAsync D-6) is a top swap, not a lifetime boundary ----

async Task FreeDigivolveContinuity()
{
    // (리뷰3 이월) AS-IS Blast/Arts free digivolve runs the NORMAL evolution arm (`permanent =
    // _targetPermanent; permanent.AddCardSource(card)`, CardController.cs:1372-1376, payCost:false) — the
    // SAME AS-IS Permanent object persists. Before the fix the FuseAsync moves carried no continuity marker,
    // so the P1-2 chokepoint fail-safe RESET the entry (observably "no divergence" only because the values
    // were silently discarded); the fix restores the CONTINUITY semantics (marked moves + ReKey).
    EngineContext context = NewContext();
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);
    HeadlessEntityId baseCard = await PlaceOnField(context, "W7BASE");
    HeadlessEntityId evoCard = await PlaceInHand(context, "W7EVO");

    StampAllNine(context, baseCard);
    AssertTrue(StoreHasEntry(context.CardInstanceRepository, baseCard), "entry exists after stamping");

    bool performed = await FreeDigivolveHelpers.DigivolveFreeAsync(
        context.CardInstanceRepository, context.ZoneMover, evoCard, baseCard,
        fromZone: ChoiceZone.Hand, gameEventQueue: context.GameEventQueue);
    AssertTrue(performed, "the free digivolve was performed");
    AssertTrue(InZone(context, P1, evoCard, ChoiceZone.BattleArea), "the new top is on the battle area");
    AssertTrue(!InZone(context, P1, baseCard, ChoiceZone.BattleArea), "the old top left the zone (a source now)");

    AssertTrue(!StoreHasEntry(context.CardInstanceRepository, baseCard), "old top key re-keyed away");
    AssertStamped(context, evoCard, "PERSIST: the permanent's bookkeeping survives the FREE digivolve on the new top");
}

// --- helpers -------------------------------------------------------------------

EngineContext NewContext()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 302);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceOnField(EngineContext context, string tag, bool breeding = false)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["color"] = "Red",
            ["colors"] = new[] { "Red" },
            ["level"] = 4,
            ["dp"] = 4000,
        },
        CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:{(breeding ? "breed" : "battle")}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4, ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(
        P1, id, ChoiceZone.None, breeding ? ChoiceZone.BreedingArea : ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceInHand(EngineContext context, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["color"] = "Red",
            ["colors"] = new[] { "Red" },
            ["level"] = 5,
            ["dp"] = 7000,
        },
        CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 5, ["dp"] = 7000 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

MatchStateMutationSink NewSink(EngineContext context) => new(
    context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
    context.EffectRegistry, context.GameEventQueue, context: context);

EffectMutation Mutation(string kind, HeadlessEntityId target, Dictionary<string, object?>? extra = null)
{
    var values = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [MatchStateMutationSink.TargetEntityIdKey] = target.Value,
    };
    if (extra is not null)
    {
        foreach (KeyValuePair<string, object?> pair in extra) values[pair.Key] = pair.Value;
    }

    return new EffectMutation(kind, new HeadlessEntityId("witness:rd-r3-02"), values);
}

bool InZone(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, ChoiceZone zone) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(owner, zone).Contains(id);

// the 9 AS-IS just-after fields (Permanent.cs:3686-3941), stamped through the mirror Permanent view — the
// same write surface the play/digivolve executor uses (CardController.cs:3547-3586).
void StampAllNine(EngineContext context, HeadlessEntityId id)
{
    var permanent = new Cec.Permanent(context, id, P1);
    permanent.PlayingEffect = new Cec.DeferredCardEffect("RD-R3-02 witness playing effect");
    permanent.DigivolvingEffect = new Cec.DeferredCardEffect("RD-R3-02 witness digivolving effect");
    permanent.LevelJustAfterPlayed = 4;
    permanent.PlayCostJustAfterPlayed = 6;
    permanent.CardNamesJustAfterPlayed = new List<string> { "WitnessName" };
    permanent.CardNamesJustAfterDigivolved = new List<string> { "WitnessEvoName" };
    permanent.TraitsJustAfterPlayed = new List<string> { "WitnessTrait" };
    permanent.IsBurstDigivolved = true;
    permanent.IsAppFusion = true;
}

void AssertStamped(EngineContext context, HeadlessEntityId id, string label)
{
    var permanent = new Cec.Permanent(context, id, P1);
    AssertTrue(permanent.PlayingEffect is not null, $"{label}: PlayingEffect");
    AssertTrue(permanent.DigivolvingEffect is not null, $"{label}: DigivolvingEffect");
    AssertEqual(4, permanent.LevelJustAfterPlayed, $"{label}: LevelJustAfterPlayed");
    AssertEqual(6, permanent.PlayCostJustAfterPlayed, $"{label}: PlayCostJustAfterPlayed");
    AssertEqual("WitnessName", permanent.CardNamesJustAfterPlayed.SingleOrDefault(), $"{label}: CardNamesJustAfterPlayed");
    AssertEqual("WitnessEvoName", permanent.CardNamesJustAfterDigivolved.SingleOrDefault(), $"{label}: CardNamesJustAfterDigivolved");
    AssertEqual("WitnessTrait", permanent.TraitsJustAfterPlayed.SingleOrDefault(), $"{label}: TraitsJustAfterPlayed");
    AssertEqual(true, permanent.IsBurstDigivolved, $"{label}: IsBurstDigivolved");
    AssertEqual(true, permanent.IsAppFusion, $"{label}: IsAppFusion");
}

void AssertAllNineDefaults(EngineContext context, HeadlessEntityId id, string label)
{
    var permanent = new Cec.Permanent(context, id, P1);
    AssertTrue(permanent.PlayingEffect is null, $"{label}: PlayingEffect default (null)");
    AssertTrue(permanent.DigivolvingEffect is null, $"{label}: DigivolvingEffect default (null)");
    AssertEqual(-1, permanent.LevelJustAfterPlayed, $"{label}: LevelJustAfterPlayed default (-1)");
    AssertEqual(-1, permanent.PlayCostJustAfterPlayed, $"{label}: PlayCostJustAfterPlayed default (-1)");
    AssertEqual(0, permanent.CardNamesJustAfterPlayed.Count, $"{label}: CardNamesJustAfterPlayed default (empty)");
    AssertEqual(0, permanent.CardNamesJustAfterDigivolved.Count, $"{label}: CardNamesJustAfterDigivolved default (empty)");
    AssertEqual(0, permanent.TraitsJustAfterPlayed.Count, $"{label}: TraitsJustAfterPlayed default (empty)");
    AssertEqual(false, permanent.IsBurstDigivolved, $"{label}: IsBurstDigivolved default (false)");
    AssertEqual(false, permanent.IsAppFusion, $"{label}: IsAppFusion default (false)");
}

// entry-ABSENCE probe: Get() is create-on-read, so peek the private store via reflection instead.
static bool StoreHasEntry(ICardInstanceRepository repository, HeadlessEntityId id)
{
    FieldInfo field = typeof(Cec.PermanentBookkeepingStore)
        .GetField("_store", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("PermanentBookkeepingStore._store not found.");
    object table = field.GetValue(null)!;
    MethodInfo tryGetValue = table.GetType().GetMethod("TryGetValue")
        ?? throw new InvalidOperationException("ConditionalWeakTable.TryGetValue not found.");
    object?[] args = { repository, null };
    bool found = (bool)tryGetValue.Invoke(table, args)!;
    if (!found || args[1] is not System.Collections.IDictionary entries)
    {
        return false;
    }

    return entries.Contains(id);
}

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}
