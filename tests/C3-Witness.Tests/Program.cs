using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-3 witness (BT9_109 "X Antibody", Option): AS-IS CardSource.CanNotTrashFromDigivolutionCards
// (CardSource.cs:2478-2520) is a candidate filter for EFFECT-trashing (ITrashDigivolutionCards / DigiBurst)
// ONLY. The DELETION path (Permanent.DiscardEvoRoots, Permanent.cs:106-142) trashes evo roots UNCONDITIONALLY.
//
// (C-3 재상환 P1-C — retopologised) BT9_109's protection grant (BT9_109.cs:132-167) is an INHERITED effect
// (SetIsInheritedEffect(true)), so the AS-IS effect-list membership (Permanent.EffectList_ForCard,
// Permanent.cs:1497-1546) exposes it to the field scan ONLY while BT9_109 is a NON-TOP (tucked) digivolution
// source of a Digimon permanent:
//   * tucked under a battle-area Digimon  -> protection ACTIVE (protects X-Antibody-named sources, itself included);
//   * as the TOP card of a permanent      -> protection INACTIVE (a top card contributes only NON-inherited effects);
//   * tucked under a non-Digimon (Tamer)  -> INACTIVE (non-top sources contribute only when the permanent IsDigimon);
//   * granter flipped                     -> INACTIVE (a flipped source contributes nothing);
//   * host off the battle area            -> INACTIVE (grant CanUse gate + membership both lapse).
// The earlier revision of this witness registered BT9_109 as a TOP card and asserted the protection active —
// a topology in which AS-IS grants NOTHING. These tests pin the corrected membership.

HeadlessPlayerId P1 = new(1); // turn player
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(1) tucked BT9_109: DELETION trashes it UNCONDITIONALLY (AS-IS DiscardEvoRoots, no keyword check)", CaseDeletionIgnoresProtection),
    ("(2) tucked BT9_109: EFFECT-trash is BLOCKED on itself and on a sibling X-Antibody source", CaseEffectTrashProtectsXAntibody),
    ("(3) tucked BT9_109: EFFECT-trash still trashes a NON-X-Antibody sibling (protection is by name)", CaseEffectTrashNonXAntibodyUnprotected),
    ("(4) protection LAPSES when the granting host leaves the battle area (CanUse gate + membership)", CaseHostOffFieldReleasesProtection),
    ("(5) tucked BT9_109: DigiBurst-style select-trash also honours the protection (TrashSpecificSources filter)", CaseSelectTrashProtects),
    ("(6) REVERSE topology: BT9_109 as TOP card grants NOTHING (inherited effect excluded from the top card)", CaseTopCardGrantsNothing),
    ("(7) membership: BT9_109 tucked under a NON-Digimon (Tamer) grants NOTHING", CaseNonDigimonHostGrantsNothing),
    ("(8) membership: a FLIPPED tucked BT9_109 grants NOTHING (sibling X-Antibody source is trashed)", CaseFlippedGranterGrantsNothing),
    ("(9) BOUNCE path (ST4_16 shape): DiscardEvoRoots is unconditional — the protected tucked BT9_109 is trashed", CaseBounceIgnoresProtection),
    ("(10) effect-trash applies the ACE-Overflow pass to a leaving un-flipped ACE source (AS-IS ITrashDigivolutionCards:5219)", CaseEffectTrashAceOverflow),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

// (1) Core contrast: DELETION path (DeleteKind -> DeletionSourceTrash.TrashEvoSourcesAsync, honorProtection:false)
// trashes the protected tucked BT9_109 anyway.
async Task CaseDeletionIgnoresProtection()
{
    EngineContext context = Context();
    HeadlessEntityId host = PlaceHostDigimon(context, P1);
    HeadlessEntityId xAntibody = PlaceBT9109(context, P1);
    SetSources(context, host, xAntibody);

    await DeleteHost(context, host);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, xAntibody), "tucked BT9_109 trashed on DELETION despite its own protection");
}

// (2) EFFECT-trash: the tucked BT9_109 protects itself AND a sibling X-Antibody-named source.
async Task CaseEffectTrashProtectsXAntibody()
{
    EngineContext context = Context();
    HeadlessEntityId host = PlaceHostDigimon(context, P1);
    HeadlessEntityId xAntibody = PlaceBT9109(context, P1);
    HeadlessEntityId sibling = PlaceSource(context, P1, "XA2", "X Antibody");
    SetSources(context, host, sibling, xAntibody);

    HeadlessEntityId trasher = PlaceTrasher(context, P2);
    await EffectTrash(context, trasher, host, count: 2); // window = both

    AssertTrue(!InZone(context, P1, ChoiceZone.Trash, xAntibody), "tucked BT9_109 NOT trashed by an effect (protects itself)");
    AssertTrue(!InZone(context, P1, ChoiceZone.Trash, sibling), "sibling X-Antibody source NOT trashed (protected by name)");
    AssertTrue(Sources(context, host).Contains(xAntibody.Value), "BT9_109 still under the host");
    AssertTrue(Sources(context, host).Contains(sibling.Value), "sibling X-Antibody source still under the host");
}

// (3) Conditional: a non-X-Antibody sibling in the same window IS trashed (protection is by name).
async Task CaseEffectTrashNonXAntibodyUnprotected()
{
    EngineContext context = Context();
    HeadlessEntityId host = PlaceHostDigimon(context, P1);
    HeadlessEntityId normal = PlaceSource(context, P1, "NM", "Agumon"); // top of the under-stack
    HeadlessEntityId xAntibody = PlaceBT9109(context, P1);             // bottom
    SetSources(context, host, normal, xAntibody);

    HeadlessEntityId trasher = PlaceTrasher(context, P2);
    await EffectTrash(context, trasher, host, count: 2); // window = both

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, normal), "non-X-Antibody source trashed by the effect");
    AssertTrue(!InZone(context, P1, ChoiceZone.Trash, xAntibody), "tucked BT9_109 in the same window still protected");
}

// (4) The grant's CanUse gate (host IsExistOnBattleArea) AND the effect-list membership both re-evaluate live:
// move the host off the battle area and the protection lapses (AS-IS cardEffect.CanUse(null) = false, and the
// granter is no longer part of any field permanent).
async Task CaseHostOffFieldReleasesProtection()
{
    EngineContext context = Context();
    HeadlessEntityId host = PlaceHostDigimon(context, P1);
    HeadlessEntityId xAntibody = PlaceBT9109(context, P1);
    SetSources(context, host, xAntibody);

    // Host leaves the battle area (bare zone move — no deletion source-trash). sourceIds are untouched.
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.BattleArea, ChoiceZone.Hand));

    HeadlessEntityId trasher = PlaceTrasher(context, P2);
    await EffectTrash(context, trasher, host, count: 1);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, xAntibody), "protection lapsed once host left the battle area");
}

// (5) DigiBurst-style select-trash (TrashSpecificSourcesAsync via SelectedCardIdsKey) honours the protection too
// (AS-IS ITrashDigivolutionCards re-filters). The explicit pick of the tucked BT9_109 is dropped.
async Task CaseSelectTrashProtects()
{
    EngineContext context = Context();
    HeadlessEntityId host = PlaceHostDigimon(context, P1);
    HeadlessEntityId xAntibody = PlaceBT9109(context, P1);
    SetSources(context, host, xAntibody);

    HeadlessEntityId trasher = PlaceTrasher(context, P2);
    await EffectTrashSelected(context, trasher, host, xAntibody);

    AssertTrue(!InZone(context, P1, ChoiceZone.Trash, xAntibody), "explicitly-selected tucked BT9_109 still protected");
}

// (6) REVERSE (the AS-IS-correct negative): BT9_109 as the TOP card of a battle-area permanent grants NOTHING —
// an inherited effect is excluded from the top card (Permanent.cs:1531-1545), so an X-Antibody-named source
// under it is trashed by an effect. The earlier witness asserted the OPPOSITE in this topology.
async Task CaseTopCardGrantsNothing()
{
    EngineContext context = Context();
    HeadlessEntityId top = PlaceBT9109(context, P1);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, top, ChoiceZone.None, ChoiceZone.BattleArea));
    HeadlessEntityId under = PlaceSource(context, P1, "XA2", "X Antibody");
    SetSources(context, top, under);

    HeadlessEntityId trasher = PlaceTrasher(context, P2);
    await EffectTrash(context, trasher, top, count: 1);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, under),
        "X-Antibody source under a TOP-CARD BT9_109 is trashed — the inherited grant does not apply from the top");
}

// (7) Membership: a non-top source contributes effects only when the permanent IsDigimon
// (Permanent.cs:1512-1518) — tucked under a Tamer, BT9_109 grants nothing and is itself trashable.
async Task CaseNonDigimonHostGrantsNothing()
{
    EngineContext context = Context();
    HeadlessEntityId tamer = PlaceHostCard(context, P1, "TAMER", "Taichi Yagami", "Tamer");
    HeadlessEntityId xAntibody = PlaceBT9109(context, P1);
    SetSources(context, tamer, xAntibody);

    HeadlessEntityId trasher = PlaceTrasher(context, P2);
    await EffectTrash(context, trasher, tamer, count: 1);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, xAntibody),
        "BT9_109 tucked under a NON-Digimon grants nothing — trashed by the effect");
}

// (8) Membership: a FLIPPED source contributes nothing (Permanent.cs:1507) — the flipped tucked BT9_109's grant
// is skipped, so the sibling X-Antibody source is trashed. (The sibling discriminates MEMBERSHIP: for BT9_109
// itself the joint's own !IsFlipped guard would also block.)
async Task CaseFlippedGranterGrantsNothing()
{
    EngineContext context = Context();
    HeadlessEntityId host = PlaceHostDigimon(context, P1);
    HeadlessEntityId xAntibody = PlaceBT9109(context, P1);
    SetFlipped(context, xAntibody);
    HeadlessEntityId sibling = PlaceSource(context, P1, "XA2", "X Antibody");
    SetSources(context, host, sibling, xAntibody);

    HeadlessEntityId trasher = PlaceTrasher(context, P2);
    await EffectTrash(context, trasher, host, count: 2);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, sibling),
        "sibling X-Antibody source trashed — the flipped granter contributes no protection");
}

// (9) (C-3 재상환 P1-A) The BOUNCE path — AS-IS HandBounceClaass.Bounce (CardController.cs:2603/2793) runs
// permanent.DiscardEvoRoots() with NO CanNotTrashFromDigivolutionCards check, so an ST4_16-shaped
// bounce+source-discard must trash even a protected source. Before the fix this path was
// wired honorProtection:true, which would have kept BT9_109 out of the trash.
async Task CaseBounceIgnoresProtection()
{
    EngineContext context = Context();
    HeadlessEntityId host = PlaceHostDigimon(context, P1);
    HeadlessEntityId xAntibody = PlaceBT9109(context, P1);
    SetSources(context, host, xAntibody);
    // Sanity: the protection IS active against a plain effect-trash in this exact topology.
    HeadlessEntityId trasher = PlaceTrasher(context, P2);
    await EffectTrash(context, trasher, host, count: 1);
    AssertTrue(!InZone(context, P1, ChoiceZone.Trash, xAntibody), "precondition: tucked BT9_109 is effect-trash-protected");

    // The opponent's bounce effect (ST4_16 shape) targets the host: sources discard, then the bounce.
    // (R6-Db) Re-targeted off the invented ActivatedSelectBounceAndDiscardSourcesEffect wrapper onto the REAL
    // substrate it composed — DigivolutionStackHelpers.TrashSourcesAsync(honorProtection:false) THEN
    // SelectPermanentEffect(Mode.Bounce) — so the corpus class can be deleted while this exact rule (AS-IS
    // HandBounceClaass.Bounce runs DiscardEvoRoots UNCONDITIONALLY, ignoring CanNotTrashFromDigivolutionCards)
    // stays asserted through the same non-invented carriers, in the same AS-IS order (discard BEFORE bounce).
    var causer = new CardSource(context, trasher, P2, P2);
    var sink = Sink(context);
    await DigivolutionStackHelpers.TrashSourcesAsync(
        context.CardInstanceRepository, context.ZoneMover, host,
        int.MaxValue, fromBottom: true, CancellationToken.None, gameEventQueue: null, honorProtection: false);
    // (RD-IDFLIP-01 batch 4) canonical 11-param Func<Permanent,bool> SetUp; the retired id-form overload's
    // sourceEntityId is supplied as the bare cause (causer.InstanceId), preserving the mutation's AS-IS cause.
    var bounce = new SelectPermanentEffect();
    bounce.SetUp(
        selectPlayer: causer.Owner,
        canTargetCondition: p => p.InstanceId == host,
        canTargetCondition_ByPreSelecetedList: null,
        canEndSelectCondition: null,
        maxCount: 1,
        canNoSelect: false,
        canEndNotMax: false,
        selectPermanentCoroutine: null,
        afterSelectPermanentCoroutine: null,
        mode: SelectPermanentEffect.Mode.Bounce,
        cardEffect: BareCauseEffect.For(context, causer.InstanceId));
    bounce.Apply(sink, new[] { host });
    await sink.FlushAsync();

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, xAntibody),
        "tucked BT9_109 trashed by the bounce's DiscardEvoRoots despite its protection (AS-IS unconditional)");
    AssertTrue(!InZone(context, P1, ChoiceZone.BattleArea, host), "the host left the battle area (bounced)");
}

// (10) (C-3 재상환 P2-1) AS-IS ITrashDigivolutionCards runs `new AceOverflowClass(trashTargets).Overflow()`
// immediately before the trashed sources move (CardController.cs:5219): an un-flipped ACE digivolution source
// trashed BY AN EFFECT costs its owner the printed Overflow memory (turn-relative: owner == turn player => -N).
async Task CaseEffectTrashAceOverflow()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    HeadlessEntityId host = PlaceHostDigimon(context, P1); // P1 = turn player
    HeadlessEntityId ace = PlaceSource(context, P1, "ACE", "AceSource");
    CardInstanceRecord aceRecord = Instance(context, ace);
    context.CardInstanceRepository.Upsert(aceRecord with
    {
        Metadata = new Dictionary<string, object?>(aceRecord.Metadata, StringComparer.Ordinal)
        {
            [AceOverflowGate.IsAceKey] = true,
            [AceOverflowGate.OverflowMemoryKey] = 3,
        }
    });
    SetSources(context, host, ace);

    HeadlessEntityId trasher = PlaceTrasher(context, P2);
    await EffectTrash(context, trasher, host, count: 1);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, ace), "the ACE source was trashed by the effect");
    AssertTrue(context.MemoryController.Current.Current == 5 - 3,
        $"the owner (turn player) paid the printed Overflow 3 (memory 5 -> {context.MemoryController.Current.Current})");
}

// --- Harness -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 42);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main); // past Setup -> DoneStartGame true (ICardEffect.CanTrigger gate)
    // (R3-W3c-4) the R1-e live scan CardSource.CanNotTrashFromDigivolutionCards evaluates each granting effect's
    // CanUse -> CanActivate -> IsDisabled, which reads GManager.instance (the ambient match scope). In production
    // the game loop provides it; this direct-drive witness enters it explicitly (the G9-057 / W3c-1 precedent).
    // Cases run sequentially, so scoping the latest context for the remainder of the flow is sufficient.
    AmbientMatchContext.Enter(context);
    return context;
}

// A plain battle-area Digimon host (no effect class) whose stack will hold the tucked BT9_109.
HeadlessEntityId PlaceHostDigimon(EngineContext context, HeadlessPlayerId owner) =>
    PlaceHostCard(context, owner, "HOST", "Dorugoramon", "Digimon");

HeadlessEntityId PlaceHostCard(EngineContext context, HeadlessPlayerId owner, string tag, string name, string cardType)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, $"NUM-{tag}", name,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 9000, ["level"] = 6 },
        CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 9000 }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

// BT9_109 — the real Option card "X Antibody" — created OFF-FIELD (ChoiceZone.None, the digivolution-source
// zone) and registered so its continuous grants are live. Registration is zone-independent (G8-002); the SCAN's
// membership rules decide from the card's live stack position whether the inherited grant applies.
HeadlessEntityId PlaceBT9109(EngineContext context, HeadlessPlayerId owner)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:BT9-109");
    cards.Upsert(new CardRecord(defId, "BT9-109", "X Antibody",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["effectClass"] = "BT9_109" },
        CardType: "Option"));
    var id = new HeadlessEntityId($"{owner.Value}:BT9-109");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    context.RegisterEnteredCardEffects(id, owner);
    return id;
}

// A digivolution source sitting off-field (ChoiceZone.None), tracked by the host's sourceIds. Its printed name
// drives CardNames (the X-Antibody protection is name-scoped).
HeadlessEntityId PlaceSource(EngineContext context, HeadlessPlayerId owner, string tag, string name)
{
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(defId, $"NUM-{tag}", name,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    return id;
}

// The card whose effect does the trashing — the scan's causing-effect source (resolvable instance, on field).
HeadlessEntityId PlaceTrasher(EngineContext context, HeadlessPlayerId owner)
{
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:TRASHER");
    var cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(defId, "NUM-TRASHER", "Trasher",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:TRASHER");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

void SetSources(EngineContext context, HeadlessEntityId host, params HeadlessEntityId[] sources)
{
    CardInstanceRecord h = Instance(context, host);
    context.CardInstanceRepository.Upsert(h with
    {
        Metadata = new Dictionary<string, object?>(h.Metadata, StringComparer.Ordinal)
        {
            ["sourceIds"] = sources.Select(s => s.Value).ToArray(),
        }
    });
}

void SetFlipped(EngineContext context, HeadlessEntityId cardId)
{
    CardInstanceRecord record = Instance(context, cardId);
    context.CardInstanceRepository.Upsert(record with
    {
        Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { ["isFlipped"] = true }
    });
}

string[] Sources(EngineContext context, HeadlessEntityId host) =>
    Instance(context, host).Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray()
        : Array.Empty<string>();

MatchStateMutationSink Sink(EngineContext context) =>
    new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController,
        context.GameEventQueue, currentTurnPlayer: () => P1, context: context);

async Task DeleteHost(EngineContext context, HeadlessEntityId host)
{
    var sink = Sink(context);
    var values = new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = host.Value };
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("del-src"), values));
    await sink.FlushAsync();
}

async Task EffectTrash(EngineContext context, HeadlessEntityId causer, HeadlessEntityId host, int count)
{
    var sink = Sink(context);
    var values = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
        [MatchStateMutationSink.CountKey] = count,
        [MatchStateMutationSink.FromBottomKey] = true,
    };
    sink.Apply(new EffectMutation(MatchStateMutationSink.TrashDigivolutionCardsKind, causer, values));
    await sink.FlushAsync();
}

async Task EffectTrashSelected(EngineContext context, HeadlessEntityId causer, HeadlessEntityId host, params HeadlessEntityId[] picks)
{
    var sink = Sink(context);
    var values = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
        [MatchStateMutationSink.SelectedCardIdsKey] = string.Join(",", picks.Select(p => p.Value)),
    };
    sink.Apply(new EffectMutation(MatchStateMutationSink.TrashDigivolutionCardsKind, causer, values));
    await sink.FlushAsync();
}

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

bool InZone(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(owner, zone).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
