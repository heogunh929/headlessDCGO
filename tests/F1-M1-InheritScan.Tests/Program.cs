// F1-M1-INHERITSCAN: the digivolution-SOURCE inherited activated reactor scan.
//
// Gap (design item F1-M1-INHERITSCAN): the activated bridge scanned only each battle-area TOP permanent's own
// effect class and NEVER iterated the digivolution-source cards under it, so an INHERITED activated reactor sitting
// on a source (AS-IS SetIsInheritedEffect(true)) was missed — a structural gap common to EVERY activated bridge
// timing (OnLoseSecurity ~28, OnMove EX10_004, OnAddHand BT9_021, OnAddSecurity BT9_003 …), latent because all
// inherited reactors were skeleton stubs.
//
// Fix: WindowResolverWiring.ScanZones now also yields each battle-area permanent's ACTIVE inherited sources (AS-IS
// Permanent.EffectList_ForCard membership: non-flipped sources of a Digimon permanent — reusing the C-3 continuous
// scan's DigivolutionStackReader + InheritedEffectHelpers.ActiveInheritedSources), tagged Inherited=true. The
// resolver (HasActivatedEffectsAt / CanCollectAt / CanActivateAt / ResolveAsync) applies the AS-IS membership split:
// a SOURCE scan runs ONLY the card's IsInheritedEffect activated effects; a TOP scan only the non-inherited ones.
//
// Witness (REAL card, dispatched by class-name = card-number): BT9_021 (BT9/Blue), OnAddHand INHERITED:
//   [Your Turn][Once Per Turn] "When an effect adds a card to your hand, return 1 of your opponent's level 3 Digimon
//   to its owner's hand." SetIsInheritedEffect(true). Ported as a uniform ActivatedEffect (SelectBody Mode.Bounce,
//   isInheritedEffect: true). It rides the OnAddHand EventBroadcast bridge (M1 timing, already live).
//
// END-TO-END (effect-driven hand add via IZoneMover + GameFlowProcessor.RunToStableAsync). Asserts:
//   * FIRES when BT9_021 is a non-flipped digivolution SOURCE under a Digimon host (inherited scan reaches it).
//   * NO FIRE when BT9_021 is a TOP permanent (membership: a top card contributes only its non-inherited effects).
//   * NO FIRE when the source is FLIPPED (face-down source contributes nothing).
//   * NO FIRE when the HOST is a non-Digimon (Tamer) permanent (inherited effects need a Digimon permanent).
//   * host-context: the reactor's IsExistOnBattleArea/IsOwnerTurn gate resolves through the SOURCE's
//     PermanentOfThisCard() = the HOST, and the once-per-turn cap holds (a 2nd effect-add the same turn no-ops).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // controls the reactor (turn player)
HeadlessPlayerId P2 = new(2);   // the opponent (holds the level-3 Digimon to bounce)

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT9_021 FIRES as a non-flipped SOURCE under a Digimon host: effect-add bounces opponent's lvl3", SourceUnderDigimonFires),
    ("BT9_021 NO FIRE as a TOP permanent (membership: top card = non-inherited only)", TopPermanentNoFire),
    ("BT9_021 NO FIRE when the source is FLIPPED (face-down source contributes nothing)", FlippedSourceNoFire),
    ("BT9_021 NO FIRE when the host is a NON-Digimon (Tamer) permanent", NonDigimonHostNoFire),
    ("BT9_021 player-scope negative: an add to the OPPONENT'S hand does NOT fire the P1 source reactor", OpponentHandNoFire),
    ("BT9_021 cause negative: a NON-effect hand add (no cause) does NOT fire (CardEffect!=null)", NonEffectAddNoFire),
    ("BT9_021 [Once Per Turn] cap via host-context: a 2nd effect-add the same turn does NOT bounce again", OncePerTurnCap),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task SourceUnderDigimonFires()
{
    EngineContext ctx = Setup(seed: 200);
    var (_, _) = await Tuck(ctx, P1, "HOST", hostIsDigimon: true, sourceFlipped: false);
    var foe = await Foe(ctx, "F3", level: 3);
    var cause = await Battle(ctx, P1, "CAUSE", "Digimon");
    var card = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(foe)); // the mandatory bounce target
    await ctx.ZoneMover.AddToHandAsync(P1, card, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(!InBattleArea(ctx, P2, foe),
        "the inherited SOURCE reactor fired and bounced the opponent's level-3 Digimon");
    AssertTrue(InHand(ctx, P2, foe), "the bounced Digimon is in its owner's (P2) hand");
}

async Task TopPermanentNoFire()
{
    EngineContext ctx = Setup(seed: 201);
    // BT9_021 as its OWN top permanent (not a source). AS-IS a top card contributes only NON-inherited effects.
    await Battle(ctx, P1, "BT9_021", "Digimon"); // dispatched by class-name = card number
    var foe = await Foe(ctx, "F3", level: 3);
    var cause = await Battle(ctx, P1, "CAUSE", "Digimon");
    var card = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(foe)); // consumed only on a spurious fire
    await ctx.ZoneMover.AddToHandAsync(P1, card, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InBattleArea(ctx, P2, foe),
        "a TOP-permanent BT9_021 did NOT fire its inherited OnAddHand effect (membership) — foe not bounced");
}

async Task FlippedSourceNoFire()
{
    EngineContext ctx = Setup(seed: 202);
    await Tuck(ctx, P1, "HOST", hostIsDigimon: true, sourceFlipped: true);
    var foe = await Foe(ctx, "F3", level: 3);
    var cause = await Battle(ctx, P1, "CAUSE", "Digimon");
    var card = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(foe));
    await ctx.ZoneMover.AddToHandAsync(P1, card, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InBattleArea(ctx, P2, foe),
        "a FLIPPED source contributes nothing (AS-IS !cardSource.IsFlipped) — foe not bounced");
}

async Task NonDigimonHostNoFire()
{
    EngineContext ctx = Setup(seed: 203);
    await Tuck(ctx, P1, "HOST", hostIsDigimon: false, sourceFlipped: false);
    var foe = await Foe(ctx, "F3", level: 3);
    var cause = await Battle(ctx, P1, "CAUSE", "Digimon");
    var card = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(foe));
    await ctx.ZoneMover.AddToHandAsync(P1, card, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InBattleArea(ctx, P2, foe),
        "an inherited effect needs a DIGIMON permanent (a Tamer host contributes no inherited effect) — foe not bounced");
}

async Task OpponentHandNoFire()
{
    EngineContext ctx = Setup(seed: 204);
    await Tuck(ctx, P1, "HOST", hostIsDigimon: true, sourceFlipped: false);
    var foe = await Foe(ctx, "F3", level: 3);
    var cause = await Battle(ctx, P2, "CAUSE2", "Digimon");
    var card = await ZoneCard(ctx, P2, "H1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(foe));
    // The add goes to the OPPONENT'S (P2's) hand -> subject owner (P2) != reactor owner (P1) -> no fire.
    await ctx.ZoneMover.AddToHandAsync(P2, card, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InBattleArea(ctx, P2, foe),
        "the player-scope gate (player == card.Owner) rejected the opponent-hand add — foe not bounced");
}

async Task NonEffectAddNoFire()
{
    EngineContext ctx = Setup(seed: 205);
    await Tuck(ctx, P1, "HOST", hostIsDigimon: true, sourceFlipped: false);
    var foe = await Foe(ctx, "F3", level: 3);
    var card = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(foe));
    // No cause id (a turn/mulligan draw shape) -> the AS-IS cardEffect != null gate rejects it.
    await ctx.ZoneMover.AddToHandAsync(P1, card, ctx.NextAddHandBatchId(), causeEffectId: null);
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InBattleArea(ctx, P2, foe),
        "the CardEffect!=null gate rejected the non-effect hand add — foe not bounced");
}

async Task OncePerTurnCap()
{
    EngineContext ctx = Setup(seed: 206);
    await Tuck(ctx, P1, "HOST", hostIsDigimon: true, sourceFlipped: false);
    var foeA = await Foe(ctx, "FA", level: 3);
    var foeB = await Foe(ctx, "FB", level: 3);
    var cause = await Battle(ctx, P1, "CAUSE", "Digimon");
    var c1 = await ZoneCard(ctx, P1, "H1", ChoiceZone.Library);
    var c2 = await ZoneCard(ctx, P1, "H2", ChoiceZone.Library);

    Enqueue(ctx, ChoiceResult.Select(foeA));
    await ctx.ZoneMover.AddToHandAsync(P1, c1, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertTrue(!InBattleArea(ctx, P2, foeA), "first effect-add bounced foeA (host-context gate + inherited scan)");

    Enqueue(ctx, ChoiceResult.Select(foeB)); // consumed only on a wrong second fire
    await ctx.ZoneMover.AddToHandAsync(P1, c2, ctx.NextAddHandBatchId(), cause);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertTrue(InBattleArea(ctx, P2, foeB), "the [Once Per Turn] cap blocked the second bounce the same turn");
}

// --- Helpers -------------------------------------------------------------

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    return ctx;
}

// Place a card as its OWN top permanent (dispatched by class-name = cardNumber).
async Task<HeadlessEntityId> Battle(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{cardNumber}");
    cards.Upsert(new CardRecord(def, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{cardNumber}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// Place a HOST top permanent with BT9_021 tucked underneath as an active (or flipped) digivolution source.
async Task<(HeadlessEntityId Host, HeadlessEntityId Source)> Tuck(
    EngineContext ctx, HeadlessPlayerId owner, string hostTag, bool hostIsDigimon, bool sourceFlipped)
{
    var cards = (CardDatabase)ctx.CardRepository;

    // The BT9_021 source (dispatched by class-name = "BT9_021").
    var srcDef = new HeadlessEntityId($"DEF:{owner.Value}:BT9_021src");
    cards.Upsert(new CardRecord(srcDef, "BT9_021", "BT9_021src",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000, ["level"] = 3 }, CardType: "Digimon"));
    var source = new HeadlessEntityId($"{owner.Value}:src:BT9_021src");
    var srcMeta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (sourceFlipped) srcMeta["isFlipped"] = true;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(source, srcDef, owner, Metadata: srcMeta));

    // The host top permanent (Digimon or Tamer), with the source tucked via sourceIds.
    var hostDef = new HeadlessEntityId($"DEF:{owner.Value}:{hostTag}");
    cards.Upsert(new CardRecord(hostDef, hostTag, hostTag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 },
        CardType: hostIsDigimon ? "Digimon" : "Tamer"));
    var host = new HeadlessEntityId($"{owner.Value}:battle:{hostTag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, hostDef, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dp"] = 5000,
            ["level"] = 4,
            ["isSuspended"] = false,
            ["sourceIds"] = new[] { source.Value },
        }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, host, ChoiceZone.None, ChoiceZone.BattleArea));
    return (host, source);
}

async Task<HeadlessEntityId> Foe(EngineContext ctx, string tag, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:foe:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"2:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = level, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> ZoneCard(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

void Enqueue(EngineContext ctx, ChoiceResult choice) => ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(choice);

bool InBattleArea(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.BattleArea).Contains(id);

bool InHand(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.Hand).Contains(id);

void AssertTrue(bool cond, string what)
{
    if (!cond) throw new Exception($"expected true: {what}");
}
