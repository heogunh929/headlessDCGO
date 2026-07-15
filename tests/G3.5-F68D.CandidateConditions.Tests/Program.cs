// (C-Del 3c-2b conversion) The retired invented gate enumerators (FindScapegoatSacrificeCandidates /
// FindDecoyRedirectCandidates / FindScapegoatSacrifice) and the pre-wired resolver DI seam
// (IDeletionReplacementCandidateConditions + DeletionReplacementTiming.ScapegoatOption/DecoyOption) were a
// headless-only substitute for the AS-IS per-card `Func<Permanent,bool> permanentCondition` that Scapegoat /
// Decoy take (e.g. "only red allies", "only a Tamer"; null = the generic owner-battle-area-Digimon set). With
// the 8-keyword PRE cluster retired to the AS-IS cut-in window, that intent is embodied 1:1 in the AS-IS
// candidate rule (`CanSelectPermanentCondition` built by CardEffectFactory.ScapegoatEffect/DecoyEffect from the
// card's permanentCondition) exercised through the printed Process bodies (CardEffectCommons.ScapegoatProcess /
// DecoyProcess) and the AS-IS candidate-count helper (MatchConditionPermanentCount). These tests drive that
// AS-IS behaviour directly (which ally is / isn't consumed, whether the holder survives), asserting the same
// filtering the retired gate unit-tests did — no deleted gate/timing symbol is referenced.
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Scapegoat: the supplied condition filters which ally is sacrificed", ScapegoatConditionFiltersSacrifice),
    ("Scapegoat: with no condition both allies are generic candidates", ScapegoatGenericCandidateSetIsBoth),
    ("Decoy: the supplied condition filters which ally is redirect-saved", DecoyConditionFiltersRedirect),
    ("Scapegoat single-pick skips a condition-failing first ally", ScapegoatSinglePickSkipsConditionFailingAlly),
    ("Null condition = generic: an eligible ally is sacrificed through the process", NullConditionDrivesGenericSacrifice),
    ("The candidate predicate accepts a passing ally and rejects a failing / the holder", SuppliedConditionPredicateAcceptsAndRejects),
    ("Decoy: a non-matching condition yields no redirect (predicate honored, not flattened)", ConditionMismatchNoRedirect),
    ("Integration: an instance-scoped condition sacrifices only the allowed ally", IntegrationInstanceScopedSacrifice),
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

// --- AS-IS candidate rules (verbatim from CardEffectFactory.ScapegoatEffect:62 / DecoyEffect:60) ------------

Func<Permanent, bool> ScapegoatCandidateRule(CardSource holder, HeadlessEntityId holderId, Func<Permanent, bool>? condition) =>
    p => CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(p, holder) && p.InstanceId != holderId &&
         (condition is null || condition(p));

Func<Permanent, bool> DecoyCandidateRule(CardSource holder, HeadlessEntityId holderId, Func<Permanent, bool>? condition) =>
    p => p.InstanceId != holderId && CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(p, holder) &&
         (condition is null || condition(p));

// --- Scapegoat ---------------------------------------------------------------------------------------------

async Task ScapegoatConditionFiltersSacrifice()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var holder = await Place(ctx, P1, "HOLDER", level: 4);
    var redAlly = await Place(ctx, P1, "ALLY-RED", level: 4);
    var plainAlly = await Place(ctx, P1, "ALLY-PLAIN", level: 3);
    await Place(ctx, P2, "FOE");
    Perm(ctx, holder).willBeRemoveField = true;

    Func<Permanent, bool> rule = ScapegoatCandidateRule(V(ctx, holder), holder, p => p.Level == 4);
    ICardEffect act = CardEffectFactory.ScapegoatSelfEffect(false, V(ctx, holder), null, "Scapegoat", "Scapegoat");
    await CardEffectCommons.ScapegoatProcess(act, Perm(ctx, holder), rule);

    AssertTrue(Deleted(ctx, P1, redAlly), "the condition-passing (red) ally was sacrificed");
    AssertFalse(Deleted(ctx, P1, plainAlly), "the condition-failing (plain) ally was NOT sacrificed");
    AssertFalse(Perm(ctx, holder).willBeRemoveField, "the holder survives (willBeRemoveField cleared on substitute death)");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, holder), "the holder is still on the battle area");
}

async Task ScapegoatGenericCandidateSetIsBoth()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var holder = await Place(ctx, P1, "HOLDER");
    await Place(ctx, P1, "ALLY-RED", level: 4);
    await Place(ctx, P1, "ALLY-PLAIN", level: 3);
    await Place(ctx, P2, "FOE");

    CardSource holderCard = V(ctx, holder);
    Func<Permanent, bool> generic = ScapegoatCandidateRule(holderCard, holder, null);
    Func<Permanent, bool> filtered = ScapegoatCandidateRule(holderCard, holder, p => p.Level == 4);

    AssertEqual(2, CardEffectCommons.MatchConditionPermanentCount(holderCard, generic),
        "both allies are generic sacrifice candidates when no condition is supplied");
    AssertEqual(1, CardEffectCommons.MatchConditionPermanentCount(holderCard, filtered),
        "only the red ally passes the supplied condition");
}

async Task ScapegoatSinglePickSkipsConditionFailingAlly()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var holder = await Place(ctx, P1, "HOLDER");
    // "A-PLAIN" sorts before "B-RED"; the single pick must still skip the condition-failing plain one.
    var plainAlly = await Place(ctx, P1, "ALLY-A-PLAIN", level: 3);
    var redAlly = await Place(ctx, P1, "ALLY-B-RED", level: 4);
    await Place(ctx, P2, "FOE");
    Perm(ctx, holder).willBeRemoveField = true;

    Func<Permanent, bool> rule = ScapegoatCandidateRule(V(ctx, holder), holder, p => p.Level == 4);
    ICardEffect act = CardEffectFactory.ScapegoatSelfEffect(false, V(ctx, holder), null, "Scapegoat", "Scapegoat");
    await CardEffectCommons.ScapegoatProcess(act, Perm(ctx, holder), rule);

    AssertTrue(Deleted(ctx, P1, redAlly), "the single pick skipped the plain first ally and sacrificed the red one");
    AssertFalse(Deleted(ctx, P1, plainAlly), "the condition-failing first ally was not sacrificed");
}

async Task NullConditionDrivesGenericSacrifice()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var holder = await Place(ctx, P1, "HOLDER");
    var ally = await Place(ctx, P1, "ALLY");
    await Place(ctx, P2, "FOE");
    Perm(ctx, holder).willBeRemoveField = true;

    // A null permanentCondition imposes no card-specific filter — the lone eligible ally IS a candidate.
    Func<Permanent, bool> generic = ScapegoatCandidateRule(V(ctx, holder), holder, null);
    ICardEffect act = CardEffectFactory.ScapegoatSelfEffect(false, V(ctx, holder), null, "Scapegoat", "Scapegoat");
    await CardEffectCommons.ScapegoatProcess(act, Perm(ctx, holder), generic);

    AssertTrue(Deleted(ctx, P1, ally), "the generic (null-condition) candidate ally is sacrificed");
    AssertFalse(Perm(ctx, holder).willBeRemoveField, "the holder survives");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, holder), "the holder is still on the battle area");
}

async Task SuppliedConditionPredicateAcceptsAndRejects()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var holder = await Place(ctx, P1, "HOLDER", level: 4);
    var redAlly = await Place(ctx, P1, "ALLY-RED", level: 4);
    var plainAlly = await Place(ctx, P1, "ALLY-PLAIN", level: 3);
    await Place(ctx, P2, "FOE");

    Func<Permanent, bool> rule = ScapegoatCandidateRule(V(ctx, holder), holder, p => p.Level == 4);

    AssertTrue(rule(Perm(ctx, redAlly)), "the predicate accepts the condition-passing (red) ally");
    AssertFalse(rule(Perm(ctx, plainAlly)), "the predicate rejects the condition-failing (plain) ally");
    AssertFalse(rule(Perm(ctx, holder)), "the holder itself is never a candidate");
}

// --- Decoy -------------------------------------------------------------------------------------------------

async Task DecoyConditionFiltersRedirect()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var decoy = await Place(ctx, P1, "DECOY");
    var redAlly = await Place(ctx, P1, "ALLY-RED", level: 4);
    var plainAlly = await Place(ctx, P1, "ALLY-PLAIN", level: 3);
    await Place(ctx, P2, "FOE");
    Perm(ctx, redAlly).willBeRemoveField = true;
    Perm(ctx, plainAlly).willBeRemoveField = true;

    CardSource decoyCard = V(ctx, decoy);
    Func<Permanent, bool> generic = DecoyCandidateRule(decoyCard, decoy, null);
    AssertEqual(2, CardEffectCommons.MatchConditionPermanentCount(decoyCard, generic),
        "both allies are generic redirect candidates when no condition is supplied");

    Func<Permanent, bool> rule = DecoyCandidateRule(V(ctx, decoy), decoy, p => p.Level == 4);
    ICardEffect act = CardEffectFactory.DecoySelfEffect(false, V(ctx, decoy), null, null, "Decoy", "Decoy");
    await CardEffectCommons.DecoyProcess(act, Perm(ctx, decoy), rule);

    AssertTrue(Deleted(ctx, P1, decoy), "the decoy self was deleted");
    AssertFalse(Perm(ctx, redAlly).willBeRemoveField, "the condition-passing (red) ally was redirect-saved");
    AssertTrue(Perm(ctx, plainAlly).willBeRemoveField, "the condition-failing (plain) ally was NOT saved");
}

async Task ConditionMismatchNoRedirect()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var decoy = await Place(ctx, P1, "DECOY");
    var ally = await Place(ctx, P1, "ALLY", level: 3);
    await Place(ctx, P2, "FOE");
    Perm(ctx, ally).willBeRemoveField = true;

    // The only ally is Level 3; a Level==4 condition must NOT be flattened to accept-all.
    Func<Permanent, bool> rule = DecoyCandidateRule(V(ctx, decoy), decoy, p => p.Level == 4);
    ICardEffect act = CardEffectFactory.DecoySelfEffect(false, V(ctx, decoy), null, null, "Decoy", "Decoy");
    await CardEffectCommons.DecoyProcess(act, Perm(ctx, decoy), rule);

    AssertTrue(Deleted(ctx, P1, decoy), "the decoy self was still deleted");
    AssertTrue(Perm(ctx, ally).willBeRemoveField, "the non-matching ally was NOT redirect-saved (condition honored)");
}

// --- Full integration --------------------------------------------------------------------------------------

async Task IntegrationInstanceScopedSacrifice()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var holder = await Place(ctx, P1, "HOLDER");
    var allyAllowed = await Place(ctx, P1, "ALLY-ALLOWED");
    var allyBlocked = await Place(ctx, P1, "ALLY-BLOCKED");
    await Place(ctx, P2, "FOE");
    Perm(ctx, holder).willBeRemoveField = true;

    // A card-specific condition: only allyAllowed may be sacrificed for Scapegoat.
    Func<Permanent, bool> rule = ScapegoatCandidateRule(V(ctx, holder), holder, p => p.InstanceId == allyAllowed);
    ICardEffect act = CardEffectFactory.ScapegoatSelfEffect(false, V(ctx, holder), null, "Scapegoat", "Scapegoat");
    await CardEffectCommons.ScapegoatProcess(act, Perm(ctx, holder), rule);

    AssertTrue(Deleted(ctx, P1, allyAllowed), "the allowed ally is sacrificed");
    AssertFalse(Deleted(ctx, P1, allyBlocked), "the condition-blocked ally is NOT sacrificed");
    AssertFalse(Perm(ctx, holder).willBeRemoveField, "the holder survives");
}

// --- Harness -----------------------------------------------------------------------------------------------

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 12);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));

HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level = 4)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false, ["level"] = level }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

bool Deleted(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) => !InZone(ctx, owner, ChoiceZone.BattleArea, id);

bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
