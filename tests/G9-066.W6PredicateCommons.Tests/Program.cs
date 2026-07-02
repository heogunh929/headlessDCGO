using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (W6-P) predicate commons batch — 1:1 name mirrors of the AS-IS GameContextDeterminarion.cs /
// CanUseEffects / MinMax helpers (verbatim bodies verified; primitive_w6_design.md). These let a ported
// card's condition closures be copied literally.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("zone predicates: trash/security(face)/battle-digimon/breeding/field", ZonePredicates),
    ("ownership predicates + owner/opponent battle-area (digimon) forms", OwnershipPredicates),
    ("hand/trash/permanent match+count helpers evaluate the view predicate 1:1", MatchAndCount),
    ("IsMin/MaxDP and IsMin/MaxLevel over the owner's battle-area digimon", MinMax),
    ("CanPlayAsNewPermanent: option gate (isPlayOption) + payCost memory gate", CanPlayAsNew),
    ("CanDeclareOptionDelayEffect: not the turn it entered play", DelayGate),
    ("CanUnsuspend: suspended and not unsuspend-locked", CanUnsuspendGate),
    ("CanActivateSuspendCostEffect: unsuspended battle permanent, off when suspended", SuspendCostGate),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task ZonePredicates()
{
    EngineContext ctx = Ctx();
    var trash = await Put(ctx, P1, "T1", ChoiceZone.Trash);
    var sec = await Put(ctx, P1, "S1", ChoiceZone.Security);
    var battle = await Put(ctx, P1, "B1", ChoiceZone.BattleArea);
    var egg = await Put(ctx, P1, "E1", ChoiceZone.BreedingArea, cardType: "DigiEgg");

    AssertTrue(CardEffectCommons.IsExistOnTrash(V(ctx, trash)), "trash card detected");
    AssertTrue(!CardEffectCommons.IsExistOnTrash(V(ctx, battle)), "battle card not in trash");
    AssertTrue(CardEffectCommons.IsExistInSecurity(V(ctx, sec)), "security face-down (default) matches isFlipped:false");
    AssertTrue(!CardEffectCommons.IsExistInSecurity(V(ctx, sec), isFlipped: true), "face state honoured");
    AssertTrue(CardEffectCommons.IsExistOnBattleAreaDigimon(V(ctx, battle)), "battle-area Digimon");
    AssertTrue(CardEffectCommons.IsExistOnBreedingArea(V(ctx, egg)), "breeding-area card");
    AssertTrue(CardEffectCommons.IsExistOnField(V(ctx, egg)) && CardEffectCommons.IsExistOnField(V(ctx, battle)),
        "IsExistOnField covers battle AND breeding");
}

async Task OwnershipPredicates()
{
    EngineContext ctx = Ctx();
    var mine = await Put(ctx, P1, "MINE", ChoiceZone.BattleArea);
    var theirs = await Put(ctx, P2, "THEIRS", ChoiceZone.BattleArea);
    CardSource self = V(ctx, mine);
    Permanent minePerm = Perm(ctx, mine, P1);
    Permanent theirsPerm = Perm(ctx, theirs, P2);

    AssertTrue(CardEffectCommons.IsOwnerPermanent(minePerm, self) && !CardEffectCommons.IsOwnerPermanent(theirsPerm, self), "IsOwnerPermanent");
    AssertTrue(CardEffectCommons.IsOpponentPermanent(theirsPerm, self) && !CardEffectCommons.IsOpponentPermanent(minePerm, self), "IsOpponentPermanent");
    AssertTrue(CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(minePerm, self), "owner battle-area digimon (full check)");
    AssertTrue(CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(theirsPerm, self), "opponent battle-area digimon");
    // Full-check: a trashed permanent is NOT "on the battle area" even if owned.
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, mine, ChoiceZone.BattleArea, ChoiceZone.Trash));
    AssertTrue(!CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(Perm(ctx, mine, P1), self), "left play -> false (not owner-only)");
}

async Task MatchAndCount()
{
    EngineContext ctx = Ctx();
    var self = await Put(ctx, P1, "SELF", ChoiceZone.BattleArea);
    await Put(ctx, P1, "H1", ChoiceZone.Hand, level: 3);
    await Put(ctx, P1, "H2", ChoiceZone.Hand, level: 5);
    await Put(ctx, P1, "TR1", ChoiceZone.Trash, level: 3);
    await Put(ctx, P2, "EN1", ChoiceZone.BattleArea, level: 6);
    CardSource card = V(ctx, self);

    AssertTrue(CardEffectCommons.HasMatchConditionOwnersHand(card, cs => cs.Level == 3), "hand predicate hit");
    AssertEqual(1, CardEffectCommons.MatchConditionOwnersCardCountInHand(card, cs => cs.Level == 3), "hand count");
    AssertTrue(CardEffectCommons.HasMatchConditionOwnersCardInTrash(card, cs => cs.Level == 3), "trash predicate hit");
    AssertEqual(0, CardEffectCommons.MatchConditionOwnersCardCountInTrash(card, cs => cs.Level == 9), "trash count miss");
    AssertTrue(CardEffectCommons.HasMatchConditionOwnersPermanent(card, p => p.TopCard.EqualsCardName("SELF")), "owners permanent hit");
    AssertEqual(1, CardEffectCommons.MatchConditionOpponentsPermanentCount(card, p => p.Level == 6), "opponents permanent count");
    AssertTrue(CardEffectCommons.HasMatchConditionPermanent(card, p => p.Level == 6), "both-players view-predicate overload");
    AssertTrue(CardEffectCommons.HasNoElement(new List<int>()), "HasNoElement");
}

async Task MinMax()
{
    EngineContext ctx = Ctx();
    var small = await Put(ctx, P1, "SMALL", ChoiceZone.BattleArea, dp: 2000, level: 3);
    var big = await Put(ctx, P1, "BIG", ChoiceZone.BattleArea, dp: 9000, level: 6);
    await Put(ctx, P2, "ENEMY", ChoiceZone.BattleArea, dp: 12000, level: 7);   // other owner — excluded

    AssertTrue(CardEffectCommons.IsMinDP(Perm(ctx, small, P1), P1), "small is min DP among OWNER's digimon");
    AssertTrue(!CardEffectCommons.IsMinDP(Perm(ctx, big, P1), P1), "big is not min");
    AssertTrue(CardEffectCommons.IsMaxDP(Perm(ctx, big, P1), P1), "big is max DP");
    AssertTrue(CardEffectCommons.IsMinLevel(Perm(ctx, small, P1), P1) && CardEffectCommons.IsMaxLevel(Perm(ctx, big, P1), P1), "level extremes");
    AssertTrue(!CardEffectCommons.IsMaxDP(Perm(ctx, big, P1), P2), "owner mismatch -> false");
}

async Task CanPlayAsNew()
{
    EngineContext ctx = Ctx();
    ctx.MemoryController.Set(3);
    var digimon = await Put(ctx, P1, "DIGI", ChoiceZone.Hand, playCost: 3);
    var option = await Put(ctx, P1, "OPT", ChoiceZone.Hand, cardType: "Option", playCost: 2);

    AssertTrue(CardEffectCommons.CanPlayAsNewPermanent(V(ctx, digimon), payCost: true, null), "digimon affordable");
    AssertTrue(!CardEffectCommons.CanPlayAsNewPermanent(V(ctx, option), payCost: true, null), "an Option needs isPlayOption (AS-IS :309)");
    AssertTrue(CardEffectCommons.CanPlayAsNewPermanent(V(ctx, option), payCost: false, null, isPlayOption: true), "delay-option placement form");
    AssertTrue(!CardEffectCommons.CanPlayAsNewPermanent(V(ctx, digimon), payCost: true, null, fixedCost: 99), "unaffordable fixed cost");
}

async Task DelayGate()
{
    EngineContext ctx = Ctx();
    var placed = await Put(ctx, P1, "DELAY", ChoiceZone.BattleArea, cardType: "Option");
    SetMeta(ctx, placed, "enteredThisTurn", true);
    AssertTrue(!CardEffectCommons.CanDeclareOptionDelayEffect(V(ctx, placed)), "cannot declare the turn it entered");
    SetMeta(ctx, placed, "enteredThisTurn", false);
    AssertTrue(CardEffectCommons.CanDeclareOptionDelayEffect(V(ctx, placed)), "declarable from the next turn");
}

async Task CanUnsuspendGate()
{
    EngineContext ctx = Ctx();
    var digimon = await Put(ctx, P1, "TAP", ChoiceZone.BattleArea);
    AssertTrue(!CardEffectCommons.CanUnsuspend(Perm(ctx, digimon, P1)), "unsuspended -> false (nothing to unsuspend)");
    SetMeta(ctx, digimon, "isSuspended", true);
    AssertTrue(CardEffectCommons.CanUnsuspend(Perm(ctx, digimon, P1)), "suspended and unlocked -> true");
}

async Task SuspendCostGate()
{
    EngineContext ctx = Ctx();
    var digimon = await Put(ctx, P1, "PAYER", ChoiceZone.BattleArea);
    AssertTrue(CardEffectCommons.CanActivateSuspendCostEffect(V(ctx, digimon)), "unsuspended battle permanent can pay a suspend cost");
    SetMeta(ctx, digimon, "isSuspended", true);
    AssertTrue(!CardEffectCommons.CanActivateSuspendCostEffect(V(ctx, digimon)), "already suspended -> false");
    var handCard = await Put(ctx, P1, "HANDY", ChoiceZone.Hand);
    AssertTrue(!CardEffectCommons.CanActivateSuspendCostEffect(V(ctx, handCard)), "not on the field -> false");
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 966);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone,
    string cardType = "Digimon", int dp = 5000, int level = 4, int? playCost = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level }, CardType: cardType, PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) =>
    new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));

Permanent Perm(EngineContext ctx, HeadlessEntityId id, HeadlessPlayerId owner) => new(ctx, id, owner);

HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

void SetMeta(EngineContext ctx, HeadlessEntityId id, string key, object? value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with
    {
        Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { [key] = value }
    });
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }
}
