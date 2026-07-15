using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using CardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;
using Permanent = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;

// (MIG4 goal-4 slice 1) The AS-IS Permanent instance-method surface added to the mirror Permanent class,
// each delegating to a verified headless helper: DiscardEvoRoots, AddDigivolutionCardsTop/Bottom, AddLinkCard,
// RemoveCardSource. Smoke coverage for the common (single-zone, hand-origin) paths — the surface is otherwise
// unwired (no current caller), so these tests are its only coverage until card ports call it.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("AddDigivolutionCardsTop moves a hand card under the top (now a digivolution source)", AddTop),
    ("AddDigivolutionCardsBottom appends a hand card to the stack bottom", AddBottom),
    ("AddLinkCard attaches a hand card as a link card", AddLink),
    ("RemoveCardSource detaches a source without trashing it (bare removal)", RemoveSource),
    ("DiscardEvoRoots trashes the permanent's digivolution sources", DiscardRoots),
    ("AddDigivolutionCardsBottom(isFacedown:true) buries the source face-down (IsFlipped)", FacedownBuriesSource),
    ("a FACE-DOWN source's inherited effects are excluded from EffectList; a FACE-UP source's are included", FacedownFlipGate),
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

async Task AddTop()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId src = PlaceHand(context, "SRC");

    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsTop(new List<CardSource> { new(context, src, P1, P1) }, causeEffectSourceId: null);

    AssertTrue(SourceIds(context, host).Contains(src), "SRC is now a digivolution source of HOST");
    AssertFalse(InZone(context, P1, ChoiceZone.Hand, src), "SRC left the hand");
    AssertTrue(new Permanent(context, host, P1).DigivolutionCards.Any(c => c.InstanceId == src), "DigivolutionCards view includes SRC");
}

async Task AddBottom()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId src = PlaceHand(context, "SRC");

    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsBottom(new List<CardSource> { new(context, src, P1, P1) }, causeEffectSourceId: null);

    AssertTrue(SourceIds(context, host).Contains(src), "SRC is now a digivolution source of HOST");
    AssertFalse(InZone(context, P1, ChoiceZone.Hand, src), "SRC left the hand");
}

async Task AddLink()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId link = PlaceHand(context, "LINK");

    var permanent = new Permanent(context, host, P1);
    await permanent.AddLinkCard(new CardSource(context, link, P1, P1), causeEffectSourceId: null);

    var linked = LinkHelpers.ReadLinkedCardIds(Instance(context, host).Metadata);
    AssertTrue(linked.Contains(link), "LINK is now a link card of HOST");
    AssertFalse(InZone(context, P1, ChoiceZone.Hand, link), "LINK left the hand");
}

async Task RemoveSource()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId src = PlaceHand(context, "SRC");
    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsTop(new List<CardSource> { new(context, src, P1, P1) }, causeEffectSourceId: null);
    AssertTrue(SourceIds(context, host).Contains(src), "precondition: SRC is a source");

    await permanent.RemoveCardSource(new CardSource(context, src, P1, P1));

    AssertFalse(SourceIds(context, host).Contains(src), "SRC detached from the stack");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, src), "bare removal did NOT trash SRC");
}

async Task DiscardRoots()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId src = PlaceHand(context, "SRC");
    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsTop(new List<CardSource> { new(context, src, P1, P1) }, causeEffectSourceId: null);
    AssertTrue(SourceIds(context, host).Contains(src), "precondition: SRC is a source");

    await permanent.DiscardEvoRoots();

    AssertFalse(SourceIds(context, host).Contains(src), "source removed from the stack");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, src), "source trashed");
}

// (P-FD / MIG4-ADDDIGI-FACEDOWN) The face-down digivolution-source primitive: AS-IS
// AddDigivolutionCardsBottom(isFacedown: true) calls cardSource.SetReverse() (Permanent.cs:1194-1197), setting
// the shared IsFlipped instance flag; the inherited-scan gate (EffectList_ForCard, Permanent.cs:1508 / mirror
// :2029 `if (!cardSource.IsFlipped)`) then contributes NOTHING from a flipped source.

async Task FacedownBuriesSource()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId src = PlaceHand(context, "SRC");

    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsBottom(
        new List<CardSource> { new(context, src, P1, P1) }, causeEffectSourceId: null, isFacedown: true);

    AssertTrue(SourceIds(context, host).Contains(src), "SRC is now a digivolution source of HOST");
    AssertTrue(new CardSource(context, src, P1, P1).IsFlipped, "the buried source is face-down (IsFlipped) — AS-IS SetReverse()");
}

async Task FacedownFlipGate()
{
    EngineContext context = Board();
    HeadlessEntityId host = PlaceDigimon(context, "HOST", dp: 4000);
    HeadlessEntityId down = PlaceHand(context, "DOWN");
    HeadlessEntityId up = PlaceHand(context, "UP");

    // Each source carries ONE inherited effect on its controller (the settable cEntity_Effect seam every ported
    // card definition uses); the flip gate is the only thing that should keep the face-down one out of EffectList.
    ICardEffect downEffect = GiveInheritedEffect(context, down);
    ICardEffect upEffect = GiveInheritedEffect(context, up);

    var permanent = new Permanent(context, host, P1);
    await permanent.AddDigivolutionCardsBottom(
        new List<CardSource> { new(context, down, P1, P1) }, causeEffectSourceId: null, isFacedown: true);
    await permanent.AddDigivolutionCardsBottom(
        new List<CardSource> { new(context, up, P1, P1) }, causeEffectSourceId: null, isFacedown: false);

    var sources = SourceIds(context, host);
    AssertTrue(sources.Contains(down) && sources.Contains(up), "both sources are in the stack");
    AssertFalse(new CardSource(context, up, P1, P1).IsFlipped, "the non-facedown source stayed face-up — AS-IS SetFace()");

    List<ICardEffect> effects = new Permanent(context, host, P1).EffectList(EffectTiming.None);
    AssertFalse(effects.Contains(downEffect), "the FACE-DOWN source's inherited effect is EXCLUDED (flip gate)");
    AssertTrue(effects.Contains(upEffect), "the FACE-UP source's inherited effect is INCLUDED");
}

// Attach a single inherited effect to a card's persistent controller; return the effect instance (reference
// identity is how the test locates it in EffectList).
ICardEffect GiveInheritedEffect(EngineContext context, HeadlessEntityId cardId)
{
    var effect = new FakeInheritedEffect();
    effect.SetIsInheritedEffect(true);
    new CardSource(context, cardId, P1, P1).cEntity_EffectController.cEntity_Effect =
        new TestInheritedEntityEffect(new List<ICardEffect> { effect });
    return effect;
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 41);
    context.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    return context;
}

HeadlessEntityId PlaceDigimon(EngineContext context, string tag, int dp)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [BattleResolver.DpKey] = dp }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId PlaceHand(EngineContext context, string tag)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 1000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

// Read the sources via the public mirror Permanent.DigivolutionCards view (the same accessor card ports use).
IReadOnlyList<HeadlessEntityId> SourceIds(EngineContext context, HeadlessEntityId host) =>
    new Permanent(context, host, P1).DigivolutionCards.Select(c => c.InstanceId).ToList();

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

bool InZone(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(owner, zone).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }

// A minimal inherited ICardEffect — no gates, no body; only its IsInheritedEffect flag and reference identity
// matter to the flip-gate witness.
sealed class FakeInheritedEffect : ICardEffect { }

// The settable per-card effect component (the same seam ported card definition classes use, cf. FAILb-01).
sealed class TestInheritedEntityEffect : CEntity_Effect
{
    private readonly List<ICardEffect> _effects;
    public TestInheritedEntityEffect(List<ICardEffect> effects) { _effects = effects; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => _effects;
}
