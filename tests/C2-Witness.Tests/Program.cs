using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-2 WITNESS — the REAL card BT22_035 (Entermon, Appmon) drives the link-host branch through the ported effect
// path (ActivatedEffectResolver over OnEnterFieldAnyone / WhenDigivolving), not synthetic metadata. It proves:
//   (1) [On Play] links a level-4-or-lower [Appmon] Digimon from HAND onto THIS Digimon; a level-5 Appmon is
//       excluded (the SharedIsAppMon Level<=4 predicate is evaluated).
//   (2) C-2 INTEGRATION (the golf's purpose): a host that carries a link card, when DELETED, routes the link
//       card to the trash (LinkHelpers detach) — and an un-flipped ACE link card overflows its owner's memory
//       FIRST (C-2 DeletionSourceTrash / DiscardEvoRoots order), all reached through the REAL link attach.
//   (3) [When Digivolving] links a level-4 Appmon from THIS card's DIGIVOLUTION CARDS (source detach → link).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(1) [On Play] links a level-4 Appmon from hand; a level-5 Appmon is NOT activatable (Level<=4 filter)", OnPlayLinksFromHand),
    ("(2) C-2 witness: deleting the host trashes the (ACE) link card + overflows owner memory FIRST", DeleteHostTrashesLinkOverflow),
    ("(3) [When Digivolving] links a level-4 Appmon from this card's digivolution sources (detach → link)", WhenDigivolvingLinksFromSource),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task OnPlayLinksFromHand()
{
    EngineContext context = Context();
    HeadlessEntityId host = await PlaceHost(context, P1, "host1");
    HeadlessEntityId lv4 = HandAppmon(context, P1, "onp-lv4", level: 4, ace: false);
    HeadlessEntityId lv5 = HandAppmon(context, P1, "onp-lv5", level: 5, ace: false);

    // BT22_035's [On Play] link is OPTIONAL (isOptional=true), so AS-IS opens the "Will you use ...?" prompt
    // (OptionalSkill.SelectOptional) first — its sole candidate is the effect's own source card (the host).
    // Then enqueue the level-4 pick; the level-5 is filtered from the candidate pool (SharedIsAppMon Level<=4).
    Enqueue(context, ChoiceResult.Select(host));
    Enqueue(context, ChoiceResult.Select(lv4));
    await ActivatedEffectResolver.ResolveAsync(context, host, P1, EffectTiming.OnEnterFieldAnyone);

    AssertTrue(LinkedCards(context, host).Contains(lv4), "the level-4 Appmon was linked onto the host");
    AssertTrue(!InZone(context, P1, ChoiceZone.Hand, lv4), "the linked card left the hand");
    AssertTrue(!LinkedCards(context, host).Contains(lv5), "the level-5 Appmon was NOT linked");

    // Level filter: with ONLY the level-5 in hand, the effect is not even activatable (no candidate passes).
    EngineContext ctx5 = Context();
    HeadlessEntityId host5 = await PlaceHost(ctx5, P1, "host5");
    HeadlessEntityId only5 = HandAppmon(ctx5, P1, "onp5-lv5", level: 5, ace: false);
    await ActivatedEffectResolver.ResolveAsync(ctx5, host5, P1, EffectTiming.OnEnterFieldAnyone);
    AssertTrue(LinkedCards(ctx5, host5).Count == 0, "a level-5-only hand yields no link (Level<=4 gates activation)");
    AssertTrue(InZone(ctx5, P1, ChoiceZone.Hand, only5), "the level-5 Appmon stays in hand");
}

async Task DeleteHostTrashesLinkOverflow()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    HeadlessEntityId host = await PlaceHost(context, P1, "host2");
    HeadlessEntityId aceLink = HandAppmon(context, P1, "del-ace", level: 4, ace: true);

    // Real link attach through the card's [On Play] — answer the optional-activation prompt (the host), then pick.
    Enqueue(context, ChoiceResult.Select(host));
    Enqueue(context, ChoiceResult.Select(aceLink));
    await ActivatedEffectResolver.ResolveAsync(context, host, P1, EffectTiming.OnEnterFieldAnyone);
    AssertTrue(LinkedCards(context, host).Contains(aceLink), "the ACE Appmon is linked onto the host before deletion");

    // C-2: delete the host — its link card overflows the owner (5 -> 2) then is trashed.
    await DeleteHost(context, host, P1);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, aceLink), "C-2: the link card was routed to the trash on host deletion");
    AssertEqual(2, context.MemoryController.Current.Current, "C-2: the un-flipped ACE link card overflowed memory (5 -> 2)");
    AssertTrue(LinkedCards(context, host).Count == 0, "the host's link list is cleared");
}

async Task WhenDigivolvingLinksFromSource()
{
    EngineContext context = Context();
    HeadlessEntityId host = await PlaceHost(context, P1, "host3");
    // A level-4 Appmon sitting as a digivolution SOURCE of the host (off-field, tracked by sourceIds).
    HeadlessEntityId src = Appmon(context, P1, "wd-src", level: 4, ace: false);
    SetSources(context, host, src);

    // Drive the WhenDigivolving branch (isEvolution event so CanTriggerWhenDigivolving passes, not On Play).
    // The [When Digivolving] link is likewise optional — answer the optional-activation prompt (the host) first.
    Enqueue(context, ChoiceResult.Select(host));
    Enqueue(context, ChoiceResult.Select(src));
    var evolve = new GameEvent(1, GameEventType.StateChanged, "wd",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["isEvolution"] = true }) { Subject = host };
    await ActivatedEffectResolver.ResolveAsync(context, host, P1, EffectTiming.OnEnterFieldAnyone, drivingEvent: evolve);

    AssertTrue(LinkedCards(context, host).Contains(src), "the source was attached as a link card");
    AssertTrue(!SourceIds(context, host).Contains(src.Value), "the linked source was detached from the digivolution stack");
}

// --- Harness -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 202);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // Past phase None so TurnStateMachine.DoneStartGame is true — ICardEffect.CanTrigger gates the activated
    // [On Play]/[When Digivolving] effects on it (the setup sequence having completed in a live match).
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

// BT22_035 (Entermon) on the battle area — an [Appmon] Digimon so a candidate's link condition matches it.
async Task<HeadlessEntityId> PlaceHost(EngineContext context, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("def:BT22_035");
    cards.Upsert(new CardRecord(defId, "BT22_035", "Entermon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4, ["dp"] = 4000, ["traits"] = new[] { "Appmon" } },
        CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [AceOverflowGate.IsAceKey] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// A BT22_035-typed Appmon card instance (so it carries the AS-IS <Link> condition matching an Appmon host).
HeadlessEntityId Appmon(EngineContext context, HeadlessPlayerId owner, string tag, int level, bool ace)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"def:appmon:{level}");
    cards.Upsert(new CardRecord(defId, "BT22_035", "Entermon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level, ["dp"] = 3000, ["traits"] = new[] { "Appmon" } },
        CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [AceOverflowGate.IsAceKey] = ace,
        [AceOverflowGate.OverflowMemoryKey] = 3,
        [AceOverflowGate.IsFlippedKey] = false,
        [LinkHelpers.LinkDpKey] = 0,
    };
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    return id;
}

HeadlessEntityId HandAppmon(EngineContext context, HeadlessPlayerId owner, string tag, int level, bool ace)
{
    HeadlessEntityId id = Appmon(context, owner, tag, level, ace);
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

void SetSources(EngineContext context, HeadlessEntityId host, params HeadlessEntityId[] sources)
{
    CardInstanceRecord h = Instance(context, host);
    context.CardInstanceRepository.Upsert(h with
    {
        Metadata = new Dictionary<string, object?>(h.Metadata, StringComparer.Ordinal) { ["sourceIds"] = sources.Select(s => s.Value).ToArray() }
    });
}

async Task DeleteHost(EngineContext context, HeadlessEntityId host, HeadlessPlayerId turnPlayer)
{
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController,
        context.GameEventQueue, currentTurnPlayer: () => turnPlayer);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = host.Value }));
    await sink.FlushAsync();
}

IReadOnlyList<HeadlessEntityId> LinkedCards(EngineContext context, HeadlessEntityId host) =>
    LinkHelpers.ReadLinkedCardIds(Instance(context, host).Metadata);

IReadOnlyList<string> SourceIds(EngineContext context, HeadlessEntityId host) =>
    Instance(context, host).Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray() : Array.Empty<string>();

void Enqueue(EngineContext context, ChoiceResult choice) =>
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(choice);

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

bool InZone(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(owner, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T e, T a, string label) { if (!EqualityComparer<T>.Default.Equals(e, a)) throw new InvalidOperationException($"{label}: expected '{e}', got '{a}'."); }
