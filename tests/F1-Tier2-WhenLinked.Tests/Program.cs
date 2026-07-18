// F1-Tier2: WhenLinked activated-bridge expansion (second F-1 Tier2 unit).
//
// AS-IS fires EffectTiming.WhenLinked via Permanent.AddLinkCard (Permanent.cs:1237-1291): once per successful link
// attach, payload {Permanent = the HOST that received the link, Card = the link card, CardEffect, isFromDigimon}. It
// is PER-CARD (linking N cards = N AddLinkCard calls = N emits). Two gate shapes: CanTriggerWhenLinked (host
// perspective — permanentCondition on the host, sourceCondition on the link card; self is permanent ==
// PermanentOfThisCard) and CanTriggerWhenLinking (link-card-self, off-field → NOT covered by this EventBroadcast wire,
// design item C2-01). Headless: the WhenLinked emit ALREADY EXISTS (LinkHelpers.cs:139, D-1); this golf's only wire is
// registering WhenLinked in ActivatedBridgeTimings.EventBroadcast. The EventBroadcast field+source scan covers the
// host top AND its digivolution sources, so an inherited [When Linked] reactor under the host fires too.
//
// End-to-end: LinkHelpers.AddLinkCardAsync (emits WhenLinked) + GameFlowProcessor.RunToStableAsync (drains).
//
// Witness (REAL card): BT22_003 (BT22/Yellow) — [Your Turn][Once Per Turn][INHERITED] "When this Digimon gets linked,
// 1 opponent Digimon gets -2000 DP for the turn." Digimon-self host gate, select → ChangeDigimonDP. Proves the shared
// inherited-source scan reaches WhenLinked (the WhenLinked analogue of BT9_062). Tfx probe: host-self memory counter.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // controls the linked host / witness
HeadlessPlayerId P2 = new(2);   // the opponent

var tests = new (string Name, Func<Task> Body)[]
{
    // --- infra (Tfx host-self probe) ---
    ("infra host-self fire: a host receiving a link fires its uncapped WhenLinked reactor (+1 memory, single fire)", TfxHostSelfFires),
    ("infra non-host no-fire: a permanent that did NOT receive the link does NOT fire (+0 for it; host still +1)", TfxNonHostNoFire),
    ("infra per-card 2 links: two AddLinkCard calls fire WhenLinked TWICE (+2, per-card, not a batch collapse)", TfxPerCardTwoLinks),
    // --- BT22_003 (inherited, Digimon-self) ---
    ("BT22_003 inherited fires: an inherited WhenLinked reactor under the linked host gives an opponent Digimon -2000 DP", Bt22003InheritedFires),
    ("BT22_003 top-permanent no-fire: BT22_003 as a TOP permanent does NOT fire its INHERITED effect (membership)", Bt22003TopPermanentNoFire),
    ("BT22_003 once-per-turn cap: a SECOND link the same turn does NOT re-apply -2000 DP (OPT cap)", Bt22003OncePerTurnCap),
    ("BT22_003 opponent-turn no-fire: the [Your Turn] gate rejects a link that lands on the opponent's turn", Bt22003OpponentTurnNoFire),
    // --- BT22_035 (host-self TOP, real card — this wire's live proof for a top-instance reactor) ---
    ("BT22_035 host-self top fires: linking onto BT22_035 plays a <=4-cost Appmon from hand for free ([When Linked])", Bt22035WhenLinkedPlaysAppmon),
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

async Task TfxHostSelfFires()
{
    EngineContext ctx = Setup(seed: 90);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "TfxWhenLinkedCounter", "TFX", "Digimon");
    var link = await PlaceLink(ctx, P1, "L1");

    await Link(ctx, host, link);

    // P1 is the turn player, so +1 reads +1. EXACTLY +1 = the host reactor fired ONCE via the activated bridge.
    AssertEqual(1, ctx.MemoryController.Current.Current, "the linked host's WhenLinked reactor gained exactly +1 (single fire)");
}

async Task TfxNonHostNoFire()
{
    EngineContext ctx = Setup(seed: 91);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "TfxWhenLinkedCounter", "HOST", "Digimon");     // receives the link
    await Place(ctx, P1, "TfxWhenLinkedCounter", "OTHER", "Digimon");               // a reactor that gets NO link
    var link = await PlaceLink(ctx, P1, "L1");

    await Link(ctx, host, link);

    AssertEqual(1, ctx.MemoryController.Current.Current,
        "only the LINKED host fired (+1); the un-linked reactor's host self-gate rejected it (not +2)");
}

async Task TfxPerCardTwoLinks()
{
    EngineContext ctx = Setup(seed: 92);
    ctx.MemoryController.Set(0);
    var host = await Place(ctx, P1, "TfxWhenLinkedCounter", "TFX", "Digimon");
    SetMeta(ctx, host, LinkHelpers.LinkedMaxKey, 2);   // room for both (default LinkedMax 1 would trim the oldest)
    var l1 = await PlaceLink(ctx, P1, "L1");
    var l2 = await PlaceLink(ctx, P1, "L2");

    await Link(ctx, host, l1);
    await Link(ctx, host, l2);

    AssertEqual(2, ctx.MemoryController.Current.Current,
        "two AddLinkCard calls fired WhenLinked twice (+2) — per-card, not a batch collapse");
    ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? hostRec);
    AssertEqual(2, LinkHelpers.ReadLinkedCardIds(hostRec!.Metadata).Count, "both link cards retained (LinkedMax 2)");
}

async Task Bt22003InheritedFires()
{
    EngineContext ctx = Setup(seed: 93);
    var (host, _) = await Tuck(ctx, srcNumber: "BT22_003", hostDispatch: "HOSTA", hostName: "Host", hostIsDigimon: true, sourceFlipped: false);
    var foe = await Foe(ctx, "FOE", playCost: 5);   // opponent battle-area Digimon, base dp 3000
    var link = await PlaceLink(ctx, P1, "L1");

    Enqueue(ctx, ChoiceResult.Select(foe));         // mandatory select (canNoSelect:false)

    await Link(ctx, host, link);
    AssertEqual(1000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, foe).DP,
        "the inherited BT22_003 (under the linked host) applied -2000 DP to the opponent Digimon (3000 -> 1000)");
}

async Task Bt22003TopPermanentNoFire()
{
    EngineContext ctx = Setup(seed: 94);
    // BT22_003 as its OWN top permanent — the INHERITED effect must NOT fire from the top card (membership).
    var host = await Place(ctx, P1, "BT22_003", "BT22_003top", "Digimon");
    var foe = await Foe(ctx, "FOE", playCost: 5);
    var link = await PlaceLink(ctx, P1, "L1");

    Enqueue(ctx, ChoiceResult.Select(foe));         // would be consumed if it wrongly fired

    await Link(ctx, host, link);

    AssertEqual(3000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, foe).DP,
        "BT22_003 as a TOP permanent did NOT fire its inherited WhenLinked (membership) — foe DP unchanged");
}

async Task Bt22003OncePerTurnCap()
{
    EngineContext ctx = Setup(seed: 95);
    var (host, _) = await Tuck(ctx, srcNumber: "BT22_003", hostDispatch: "HOSTA", hostName: "Host", hostIsDigimon: true, sourceFlipped: false);
    SetMeta(ctx, host, LinkHelpers.LinkedMaxKey, 2);   // room for both links
    var foe = await Foe(ctx, "FOE", playCost: 5);
    var l1 = await PlaceLink(ctx, P1, "L1");
    var l2 = await PlaceLink(ctx, P1, "L2");

    Enqueue(ctx, ChoiceResult.Select(foe));
    await Link(ctx, host, l1);
    AssertEqual(1000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, foe).DP, "first link fired -2000 DP (3000 -> 1000)");

    // Second link the SAME turn — the [Once Per Turn] cap blocks the re-fire (would stack to -4000 otherwise).
    Enqueue(ctx, ChoiceResult.Select(foe));
    await Link(ctx, host, l2);
    AssertEqual(1000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, foe).DP,
        "second link the same turn did NOT re-apply -2000 (Once Per Turn cap) — foe stays at 1000, not -4000-clamped");
}

async Task Bt22003OpponentTurnNoFire()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 96);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P2);   // P2's turn — the host (P1) is off-turn
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate
    var (host, _) = await Tuck(ctx, srcNumber: "BT22_003", hostDispatch: "HOSTA", hostName: "Host", hostIsDigimon: true, sourceFlipped: false);
    var foe = await Foe(ctx, "FOE", playCost: 5);
    var link = await PlaceLink(ctx, P1, "L1");

    Enqueue(ctx, ChoiceResult.Select(foe));   // would be consumed if it wrongly fired

    await Link(ctx, host, link);

    AssertEqual(3000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(ctx, foe).DP,
        "the [Your Turn] gate (IsOwnerTurn) rejected the link on the opponent's turn — foe DP unchanged");
}

async Task Bt22035WhenLinkedPlaysAppmon()
{
    EngineContext ctx = Setup(seed: 97);
    var host = await Place(ctx, P1, "BT22_035", "BT22_035", "Digimon");   // real card, host-self TOP reactor
    var appmon = await HandAppmon(ctx, P1, "APP");                        // a <=4-cost [Appmon] in hand
    var link = await PlaceLink(ctx, P1, "L1");

    // BT22_035's [When Linked] is an OPTIONAL battle-area trigger (isOptional=true), so AS-IS opens the
    // "Will you use ...?" prompt (OptionalSkill.SelectOptional) FIRST — its sole candidate is the effect's own
    // source card (the host). It is NOT the [Hand] fast-path that skips the prompt (MultipleSkills
    // IsOnlyHandEffectStacked needs an in-hand "[Hand]" effect). Answer it (activate), then pick the Appmon.
    Enqueue(ctx, ChoiceResult.Select(host));     // optional-activation prompt: use the [When Linked] effect
    Enqueue(ctx, ChoiceResult.Select(appmon));   // BT22_035's [When Linked] optional play — pick the Appmon

    await Link(ctx, host, link);

    AssertTrue(InBattleArea(ctx, P1, appmon),
        "BT22_035's now-live [When Linked] played the <=4-cost Appmon from hand for free (top-instance reactor fires via the wire)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Setup(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main); // (harness triage) DoneStartGame gate: new-model CanTrigger needs a live phase
    return ctx;
}

// Attach a link card onto a host (emits WhenLinked) then drain the activated windows.
async Task Link(EngineContext ctx, HeadlessEntityId host, HeadlessEntityId link)
{
    await LinkHelpers.AddLinkCardAsync(
        ctx.CardInstanceRepository, ctx.ZoneMover, host, link, ChoiceZone.Hand, ctx.GameEventQueue, context: ctx);
    await new GameFlowProcessor().RunToStableAsync(ctx);
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string cardNumber, string tag, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(def, cardNumber, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// A plain link card in hand (instance-only, dispatches no effects).
async Task<HeadlessEntityId> PlaceLink(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(
        id, new HeadlessEntityId(id.Value), owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

// A HOST top permanent (Name = hostName, dispatched by generic class hostDispatch) with srcNumber tucked underneath
// as an active (or flipped) digivolution source via the sourceIds metadata.
async Task<(HeadlessEntityId Host, HeadlessEntityId Source)> Tuck(
    EngineContext ctx, string srcNumber, string hostDispatch, string hostName, bool hostIsDigimon, bool sourceFlipped)
{
    var cards = (CardDatabase)ctx.CardRepository;

    var srcDef = new HeadlessEntityId($"DEF:{P1.Value}:{srcNumber}src");
    cards.Upsert(new CardRecord(srcDef, srcNumber, $"{srcNumber}src",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000, ["level"] = 3 }, CardType: "Digimon"));
    var source = new HeadlessEntityId($"{P1.Value}:src:{srcNumber}src");
    var srcMeta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (sourceFlipped) srcMeta["isFlipped"] = true;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(source, srcDef, P1, Metadata: srcMeta));

    var hostDef = new HeadlessEntityId($"DEF:{P1.Value}:{hostDispatch}");
    cards.Upsert(new CardRecord(hostDef, hostDispatch, hostName,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 },
        CardType: hostIsDigimon ? "Digimon" : "Tamer"));
    var host = new HeadlessEntityId($"{P1.Value}:battle:{hostDispatch}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, hostDef, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dp"] = 5000,
            ["level"] = 4,
            ["isSuspended"] = false,
            ["sourceIds"] = new[] { source.Value },
        }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));
    return (host, source);
}

async Task<HeadlessEntityId> Foe(EngineContext ctx, string tag, int playCost)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:foe:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 },
        PlayCost: playCost, CardType: "Digimon"));
    var id = new HeadlessEntityId($"2:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// A <=4-cost [Appmon] Digimon card in hand (for BT22_035's [When Linked] free-play).
async Task<HeadlessEntityId> HandAppmon(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000, ["level"] = 3, ["traits"] = new[] { "Appmon" } },
        PlayCost: 3, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:hand:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 2000, ["level"] = 3, ["traits"] = new[] { "Appmon" } }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}

void SetMeta(EngineContext ctx, HeadlessEntityId id, string key, object? value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with
    {
        Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { [key] = value }
    });
}

bool InBattleArea(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, ChoiceZone.BattleArea).Contains(id);

void AssertTrue(bool cond, string what)
{
    if (!cond) throw new Exception($"expected true: {what}");
}

void Enqueue(EngineContext ctx, ChoiceResult choice) => ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(choice);

void AssertEqual(int expected, int actual, string what)
{
    if (expected != actual) throw new Exception($"{what}: expected {expected}, got {actual}");
}
