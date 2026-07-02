using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// CV-A2 / F-2: selection → action framework. SelectPermanentEffect (AS-IS mirror) enumerates the live
// board filtered by a target predicate, builds a Permanent ChoiceRequest honouring the original
// max/canNoSelect/canEndNotMax count rules, and maps the selection Mode to per-target mutations
// (Tap/UnTap/Destroy/Bounce/PutLibrary/PutSecurity) reusing the MatchStateMutationSink vocabulary.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId A1 = new("p1:main:A1");
HeadlessEntityId B1 = new("p2:main:B1");
HeadlessEntityId B2 = new("p2:main:B2");
HeadlessPlayerId[] Both = { P1, P2 };

var tests = new (string Name, Func<Task> Body)[]
{
    ("BuildRequest filters by predicate; exact pick requires min==max, not skippable", ExactPick),
    ("canNoSelect makes the request skippable with min 0", CanNoSelect),
    ("canEndNotMax allows finishing below max (min 1)", CanEndNotMax),
    ("Destroy mode: enumerate → resolve → apply trashes the selected target", DestroyEndToEnd),
    ("Tap mode marks the selected target suspended", TapApplies),
    ("Bounce mode returns the selected target to hand", BounceApplies),
    ("BuildMutations maps each Mode to the matching mutation kind", ModeMapping),
    ("(B5) Degenerate mode de-digivolves the selected permanent (AS-IS IDegeneration)", DegenerateApplies),
    ("(B5) Attack mode honours defenderCondition and canAttackPlayer (AS-IS SelectAttackEffect.SetUp)", AttackModeConditions),
    ("(B5) the AS-IS combination gate (canEndSelectCondition) validates selection SETS", CombinationGate),
    ("(P2) the combination gate rides the ChoiceRequest: an illegal SET is rejected at resolve (retry)", CombinationGateRejectsAtResolve),
    ("(P3) multi-attacker Attack mode queues sequentially: declining #1 opens #2 (AS-IS foreach)", MultiAttackerQueue),
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

// --- Tests ---------------------------------------------------------------

async Task ExactPick()
{
    EngineContext context = await SetupBoard();
    var sel = new SelectPermanentEffect();
    // Target only the opponent's (P2) permanents, max 2, must pick exactly 2.
    sel.SetUp(P1, id => id.Value.StartsWith("p2", StringComparison.Ordinal), maxCount: 2,
        canNoSelect: false, canEndNotMax: false, SelectPermanentEffect.Mode.Destroy, new HeadlessEntityId("src"));

    ChoiceRequest request = sel.BuildRequest(Zones(context), Both);

    AssertEqual(2, request.Candidates.Count, "only P2's two permanents are candidates");
    AssertTrue(request.Candidates.All(c => c.Id.Value.StartsWith("p2", StringComparison.Ordinal)), "A1 (P1) excluded");
    AssertEqual(2, request.MinCount, "exact pick min");
    AssertEqual(2, request.MaxCount, "exact pick max");
    AssertFalse(request.CanSkip, "exact pick not skippable");
}

async Task CanNoSelect()
{
    EngineContext context = await SetupBoard();
    var sel = new SelectPermanentEffect();
    sel.SetUp(P1, _ => true, maxCount: 2, canNoSelect: true, canEndNotMax: false,
        SelectPermanentEffect.Mode.Tap, new HeadlessEntityId("src"));

    ChoiceRequest request = sel.BuildRequest(Zones(context), Both);

    AssertEqual(0, request.MinCount, "canNoSelect → min 0");
    AssertTrue(request.CanSkip, "canNoSelect → skippable");
}

async Task CanEndNotMax()
{
    EngineContext context = await SetupBoard();
    var sel = new SelectPermanentEffect();
    sel.SetUp(P1, _ => true, maxCount: 3, canNoSelect: false, canEndNotMax: true,
        SelectPermanentEffect.Mode.Tap, new HeadlessEntityId("src"));

    ChoiceRequest request = sel.BuildRequest(Zones(context), Both);

    AssertEqual(1, request.MinCount, "canEndNotMax → min 1");
    AssertEqual(3, request.Candidates.Count, "three permanents available");
    AssertEqual(3, request.MaxCount, "max clamps to available (3)");
    AssertFalse(request.CanSkip, "not skippable without canNoSelect");
}

async Task DestroyEndToEnd()
{
    EngineContext context = await SetupBoard();
    MatchStateMutationSink sink = Sink(context);
    var sel = new SelectPermanentEffect();
    sel.SetUp(P1, id => id.Value.StartsWith("p2", StringComparison.Ordinal), maxCount: 1,
        canNoSelect: false, canEndNotMax: false, SelectPermanentEffect.Mode.Destroy, new HeadlessEntityId("src"));

    ChoiceRequest request = sel.BuildRequest(Zones(context), Both);
    var provider = new ScriptedChoiceProvider();
    provider.Enqueue(ChoiceResult.Select(B1));
    ChoiceResult result = await provider.ChooseAsync(request);

    sel.Apply(sink, result.SelectedIds);
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, B1), "selected B1 trashed");
    AssertFalse(InZone(context, P2, ChoiceZone.BattleArea, B1), "B1 left the battle area");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, B2), "unselected B2 untouched");
}

async Task TapApplies()
{
    EngineContext context = await SetupBoard();
    MatchStateMutationSink sink = Sink(context);
    var sel = new SelectPermanentEffect();
    sel.SetUp(P1, _ => true, maxCount: 1, canNoSelect: false, canEndNotMax: false,
        SelectPermanentEffect.Mode.Tap, new HeadlessEntityId("src"));

    sel.Apply(sink, new[] { B1 });
    await sink.FlushAsync();

    AssertTrue(ReadFlag(context, B1, MatchStateMutationSink.SuspendedFlagKey), "B1 marked suspended");
}

async Task BounceApplies()
{
    EngineContext context = await SetupBoard();
    MatchStateMutationSink sink = Sink(context);
    var sel = new SelectPermanentEffect();
    sel.SetUp(P1, _ => true, maxCount: 1, canNoSelect: false, canEndNotMax: false,
        SelectPermanentEffect.Mode.Bounce, new HeadlessEntityId("src"));

    sel.Apply(sink, new[] { B1 });
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Hand, B1), "B1 returned to hand");
    AssertFalse(InZone(context, P2, ChoiceZone.BattleArea, B1), "B1 left the battle area");
}

Task ModeMapping()
{
    var expectations = new (SelectPermanentEffect.Mode Mode, string Kind)[]
    {
        (SelectPermanentEffect.Mode.Tap, MatchStateMutationSink.SuspendKind),
        (SelectPermanentEffect.Mode.UnTap, MatchStateMutationSink.UnsuspendKind),
        (SelectPermanentEffect.Mode.Destroy, MatchStateMutationSink.DeleteKind),
        (SelectPermanentEffect.Mode.Bounce, MatchStateMutationSink.ReturnToHandKind),
        (SelectPermanentEffect.Mode.PutLibraryTop, MatchStateMutationSink.ReturnToDeckTopKind),
        (SelectPermanentEffect.Mode.PutLibraryBottom, MatchStateMutationSink.ReturnToDeckBottomKind),
        (SelectPermanentEffect.Mode.PutSecurityTop, MatchStateMutationSink.AddToSecurityKind),
        (SelectPermanentEffect.Mode.PutSecurityBottom, MatchStateMutationSink.AddToSecurityKind),
    };

    foreach ((SelectPermanentEffect.Mode mode, string kind) in expectations)
    {
        var sel = new SelectPermanentEffect();
        sel.SetUp(P1, _ => true, maxCount: 1, canNoSelect: false, canEndNotMax: false, mode, new HeadlessEntityId("src"));
        IReadOnlyList<EffectMutation> mutations = sel.BuildMutations(new[] { B1 });
        AssertEqual(1, mutations.Count, $"{mode} yields one mutation");
        AssertEqual(kind, mutations[0].Kind, $"{mode} → {kind}");
    }

    // Attack / Custom yield no built-in mutation.
    foreach (SelectPermanentEffect.Mode mode in new[] { SelectPermanentEffect.Mode.Attack, SelectPermanentEffect.Mode.Custom })
    {
        var sel = new SelectPermanentEffect();
        sel.SetUp(P1, _ => true, maxCount: 1, canNoSelect: false, canEndNotMax: false, mode, new HeadlessEntityId("src"));
        AssertEqual(0, sel.BuildMutations(new[] { B1 }).Count, $"{mode} yields no mutation");
    }

    return Task.CompletedTask;
}

// --- Helpers -------------------------------------------------------------

// --- (B5) -----------------------------------------------------------------

async Task DegenerateApplies()
{
    EngineContext context = await SetupBoard();
    var source = new HeadlessEntityId("p2:src:B1u");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(source, new HeadlessEntityId(source.Value), P2));
    SetSources(context, B1, source);

    var sel = new SelectPermanentEffect();
    sel.SetUp(P1, id => id == B1, maxCount: 1, canNoSelect: false, canEndNotMax: false,
        SelectPermanentEffect.Mode.Degenerate, new HeadlessEntityId("src"));
    sel.SetDegenerationCount(1);

    MatchStateMutationSink sink = Sink(context);
    sel.Apply(sink, new[] { B1 });
    await sink.FlushAsync();

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, B1), "the selected top card was de-digivolved to the trash");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, source), "the under-source was promoted");
}

async Task AttackModeConditions()
{
    EngineContext context = await SetupBoard();
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)context.CardRepository;
    foreach (HeadlessEntityId id in new[] { A1, B1, B2 })
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId(id.Value), id.Value, id.Value,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
        Suspend(context, id, id != A1);   // defenders suspended (normal attack targeting)
    }

    var sel = new SelectPermanentEffect();
    sel.SetUp(P1, id => id == A1, maxCount: 1, canNoSelect: false, canEndNotMax: false,
        SelectPermanentEffect.Mode.Attack, new HeadlessEntityId("src"));
    sel.SetAttackOptions(canAttackPlayer: false, defenderCondition: id => id == B1);

    AssertTrue(sel.TryOpenAttack(context, new[] { A1 }), "the attack target choice opened for the selected attacker");
    var candidates = context.ChoiceController.PendingRequest!.Candidates;
    AssertTrue(candidates.Any(c => c.Id == B1), "the defenderCondition-matching Digimon is a target");
    AssertFalse(candidates.Any(c => c.Id == B2), "the non-matching Digimon is NOT a target (predicate honoured)");
    AssertFalse(candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "canAttackPlayer:false removes the direct-attack option");
}

async Task CombinationGate()
{
    EngineContext context = await SetupBoard();
    var sel = new SelectPermanentEffect();
    sel.SetUp(P1, _ => true, maxCount: 2, canNoSelect: false, canEndNotMax: false,
        SelectPermanentEffect.Mode.Tap, new HeadlessEntityId("src"));
    // "the two picks must have different owners" (an AS-IS combination-style constraint).
    sel.SetCanEndSelectCondition(selection =>
        selection.Count == 2 && selection[0].Value[..2] != selection[1].Value[..2]);

    AssertTrue(sel.IsValidSelection(new[] { A1, B1 }), "a cross-owner pair passes the gate");
    AssertFalse(sel.IsValidSelection(new[] { B1, B2 }), "a same-owner pair fails the gate (AS-IS CanEndSelect)");
}

async Task CombinationGateRejectsAtResolve()
{
    EngineContext context = await SetupBoard();
    var sel = new SelectPermanentEffect();
    sel.SetUp(P1, _ => true, maxCount: 2, canNoSelect: false, canEndNotMax: false,
        SelectPermanentEffect.Mode.Tap, new HeadlessEntityId("src"));
    sel.SetCanEndSelectCondition(selection =>
        selection.Count == 2 && selection[0].Value[..2] != selection[1].Value[..2]);

    ChoiceRequest request = sel.BuildRequest(Zones(context), Both);
    context.ChoiceController.RequestChoice(request, new HeadlessEntityId("p2-test"));

    bool rejected = false;
    try { context.ChoiceController.ResolveChoice(ChoiceResult.Select(B1, B2)); }
    catch (InvalidOperationException) { rejected = true; }
    AssertTrue(rejected, "the same-owner pair is rejected at resolve (AS-IS CanEndSelect gate)");
    AssertTrue(context.ChoiceController.Current.IsPending, "the choice stays pending for a retry");

    context.ChoiceController.ResolveChoice(ChoiceResult.Select(A1, B1));
    AssertTrue(!context.ChoiceController.Current.IsPending, "the legal combination resolves");
}

async Task MultiAttackerQueue()
{
    EngineContext context = await SetupBoard();
    context.TurnController.Initialize(new[] { P2, P1 }, P2);   // P2 is the turn player: B1/B2 attack, A1 defends
    var cards = (CardDatabase)context.CardRepository;
    foreach (HeadlessEntityId id in new[] { A1, B1, B2 })
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId(id.Value), id.Value, id.Value,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000 }, CardType: "Digimon"));
        Suspend(context, id, id == A1);   // the defender is suspended, the attackers are not
    }

    var sel = new SelectPermanentEffect();
    sel.SetUp(P2, id => id.Value.StartsWith("p2", StringComparison.Ordinal), maxCount: 2,
        canNoSelect: false, canEndNotMax: false, SelectPermanentEffect.Mode.Attack, new HeadlessEntityId("src"));
    sel.SetAttackOptions(canAttackPlayer: false);

    AssertTrue(sel.TryOpenAttack(context, new[] { B1, B2 }), "attacker #1's target choice opened");
    // Decline #1 -> the AS-IS sequential loop moves to attacker #2.
    AssertTrue(HeadlessDCGO.Engine.Headless.Runtime.EffectDrivenAttack.ResolveChoice(context, ChoiceResult.Skip()),
        "attacker #1 declined");
    AssertTrue(context.ChoiceController.Current.IsPending, "attacker #2's target choice opened automatically");
}

void Suspend(EngineContext context, HeadlessEntityId id, bool suspended)
{
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    context.CardInstanceRepository.Upsert(r! with
    {
        Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { ["isSuspended"] = suspended }
    });
}

void SetSources(EngineContext context, HeadlessEntityId host, HeadlessEntityId source)
{
    context.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? r);
    context.CardInstanceRepository.Upsert(r! with
    {
        Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { ["sourceIds"] = new[] { source.Value } }
    });
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);

IZoneStateReader Zones(EngineContext context) => (IZoneStateReader)context.ZoneMover;

async Task<EngineContext> SetupBoard()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    await Place(context, P1, A1);
    await Place(context, P2, B1);
    await Place(context, P2, B2);
    return context;
}

async Task Place(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id)
{
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(id.Value), owner));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out var r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
