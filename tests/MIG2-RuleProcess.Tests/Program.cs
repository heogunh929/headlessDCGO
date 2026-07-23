using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using AutoProcessing = HeadlessDCGO.Engine.Assets.Scripts.Script.AutoProcessing;
using CardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;
using Permanent = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;

// (MIG2) Migration goal 2 — the five AS-IS rule processes that were missing headless-side, asserted through
// the mirror AutoProcessing.RuleProcess (AS-IS AutoProcessing.cs:282-567):
//   2 TrashNonDigimonPermanentProcess — a breeding non-Digimon is trashed (a Digi-Egg is NOT: IsDigimon
//     includes eggs, Permanent.cs:3447).
//   8 CardFaceDownProcess — a face-down battle-area top is trashed (sources first, DiscardEvoRoots shape).
//   6 DigimonLackLinkConditionProcess — a link that fails its condition is trashed via ITrashLinkCards
//     (ONE batch OnLinkCardDiscarded); a link whose condition holds survives.
//   7 DigimonLackLinkMaxCountProcess — over-max links open the OWNER'S SELECTION (AS-IS
//     Permanent.RemoveLinkedCard(null, count) SelectCardEffect — NOT oldest-first); resolving the choice
//     trashes exactly the selected card through ITrashLinkCards.
//   5 BattleWithoutDigimon — a non-Digimon attacker is force-ended (IsEndAttack) when a rule pass opens
//     (the predicate itself is INERT in AS-IS DoRuleProcess — :352-356 commented out — so the test opens
//     the pass with coincident face-down work, exactly the only way AS-IS reaches this stage).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("breeding non-Digimon (Tamer) is trashed with its sources; a Digi-Egg survives", BreedingNonDigimonTrashed),
    ("face-down battle-area top is trashed", FaceDownTopTrashed),
    ("link failing its condition is trashed via ITrashLinkCards (one batch window); a valid link survives", LinkConditionTrim),
    ("over-max links open the owner's trim SELECTION; the selected card is trashed", LinkMaxTrimSelection),
    ("non-Digimon attacker is force-ended (IsEndAttack) when a rule pass opens", TamerAttackerForceEnd),
    ("both players losing in the same pass is a DRAW (EndGame(null)); a single loser's enemy wins", EndGameDrawAndWin),
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

async Task BreedingNonDigimonTrashed()
{
    EngineContext context = Board();
    HeadlessEntityId tamer = Def(context, "TAMER", "Tamer", dp: null);
    HeadlessEntityId egg = Def(context, "EGG", "Digitama", dp: null);
    HeadlessEntityId source = Def(context, "SRC", "Digimon", dp: 3000);

    await Place(context, tamer, P1, ChoiceZone.BreedingArea);
    await Place(context, egg, P1, ChoiceZone.BreedingArea);
    // an off-field digivolution source tracked under the tamer (DiscardEvoRoots must trash it first).
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(source, new HeadlessEntityId("DEF:SRC"), P1));
    Mutate(context, tamer, m => m[DeletionReplacementGate.SourceIdsKey] = new[] { source.Value });

    bool progressed = await AutoProcessing.For(context).RuleProcess();

    AssertTrue(progressed, "rule pass progressed");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, tamer), "breeding Tamer trashed");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, source), "its digivolution source trashed (DiscardEvoRoots)");
    AssertTrue(InZone(context, P1, ChoiceZone.BreedingArea, egg), "Digi-Egg survives (IsDigimon includes eggs)");
}

async Task FaceDownTopTrashed()
{
    EngineContext context = Board();
    HeadlessEntityId mon = Def(context, "MON", "Digimon", dp: 4000);
    await Place(context, mon, P1, ChoiceZone.BattleArea, dp: 4000);
    Mutate(context, mon, m => m["isFlipped"] = true);

    bool progressed = await AutoProcessing.For(context).RuleProcess();

    AssertTrue(progressed, "rule pass progressed");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, mon), "face-down top trashed");
}

async Task LinkConditionTrim()
{
    EngineContext context = Board();
    HeadlessEntityId host = Def(context, "HOST", "Digimon", dp: 5000);
    HeadlessEntityId badLink = Def(context, "BADLINK", "Digimon", dp: 3000);
    HeadlessEntityId host2 = Def(context, "HOST2", "Digimon", dp: 5000);
    HeadlessEntityId goodLink = Def(context, "GOODLINK", "Digimon", dp: 3000);
    await Place(context, host, P1, ChoiceZone.BattleArea, dp: 5000);
    await Place(context, host2, P1, ChoiceZone.BattleArea, dp: 5000);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(badLink, new HeadlessEntityId("DEF:BADLINK"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(goodLink, new HeadlessEntityId("DEF:GOODLINK"), P1));
    Link(context, host, badLink);
    Link(context, host2, goodLink);
    // badLink's declared condition FAILS against its host (the AS-IS trim shape — a condition-LESS link is a
    // fixture-only state the rule guards out, since AS-IS CanLink gates every attach on a declared condition).
    DeclareLinkCondition(context, badLink, _ => false);
    DeclareLinkCondition(context, goodLink, _ => true);
    context.GameEventQueue.DrainPending();

    bool progressed = await AutoProcessing.For(context).RuleProcess();

    AssertTrue(progressed, "rule pass progressed");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, badLink), "condition-failing link trashed");
    AssertEqual(0, LinkHelpers.ReadLinkedCardIds(Instance(context, host).Metadata).Count, "host link list cleared");
    AssertEqual(1, LinkHelpers.ReadLinkedCardIds(Instance(context, host2).Metadata).Count, "valid link survives");
    var discardEvents = context.GameEventQueue.DrainPending()
        .Where(e => TriggerTimingMap.Derive(e).Contains(TriggerTimings.OnLinkCardDiscarded))
        .ToList();
    AssertEqual(1, discardEvents.Count, "ONE batch OnLinkCardDiscarded window (ITrashLinkCards)");
}

async Task LinkMaxTrimSelection()
{
    EngineContext context = Board();
    HeadlessEntityId host = Def(context, "LHOST", "Digimon", dp: 5000);
    HeadlessEntityId l1 = Def(context, "L1", "Digimon", dp: 3000);
    HeadlessEntityId l2 = Def(context, "L2", "Digimon", dp: 3000);
    await Place(context, host, P1, ChoiceZone.BattleArea, dp: 5000);
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(l1, new HeadlessEntityId("DEF:L1"), P1));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(l2, new HeadlessEntityId("DEF:L2"), P1));
    // both links VALID (the condition trim must not eat them first); two links over the default max 1.
    Link(context, host, l2);
    Link(context, host, l1); // newest first: [l1, l2]
    DeclareLinkCondition(context, l1, _ => true);
    DeclareLinkCondition(context, l2, _ => true);
    context.GameEventQueue.DrainPending();

    bool progressed = await AutoProcessing.For(context).RuleProcess();

    AssertTrue(progressed, "rule pass progressed");
    HeadlessChoiceState choice = context.ChoiceController.Current;
    AssertTrue(choice.IsPending, "trim selection opened (AS-IS RemoveLinkedCard(null, count) SelectCardEffect)");
    AssertTrue(choice.RequestId?.Value.StartsWith(AutoProcessing.LinkTrimRequestIdPrefix, StringComparison.Ordinal) == true,
        "request id carries the link-trim prefix");
    AssertEqual(2, choice.CandidateIds.Count, "both link cards offered");

    // The OWNER picks the OLDEST (l2) — oldest-first auto-enforcement would have taken it without asking;
    // the point is the pick decides.
    var processor = new MetadataActionProcessor();
    await processor.ProcessAsync(HeadlessActionFactory.ResolveChoice(P1, ChoiceResult.Select(l2)), context);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, l2), "selected link trashed (via ITrashLinkCards)");
    var linked = LinkHelpers.ReadLinkedCardIds(Instance(context, host).Metadata);
    AssertEqual(1, linked.Count, "one link remains");
    AssertTrue(linked.Contains(l1), "the unselected link survives");
}

async Task TamerAttackerForceEnd()
{
    EngineContext context = Board();
    HeadlessEntityId tamer = Def(context, "ATKTAMER", "Tamer", dp: null);
    HeadlessEntityId target = Def(context, "TGT", "Digimon", dp: 3000);
    HeadlessEntityId flipped = Def(context, "FLIP", "Digimon", dp: 4000);
    await Place(context, tamer, P1, ChoiceZone.BattleArea);
    await Place(context, target, P2, ChoiceZone.BattleArea, dp: 3000);
    // coincident rule work — the ONLY way AS-IS reaches BattleWithoutDigimon (its DoRuleProcess check is inert).
    await Place(context, flipped, P1, ChoiceZone.BattleArea, dp: 4000);
    Mutate(context, flipped, m => m["isFlipped"] = true);

    context.AttackController.DeclareAttack(P1, tamer, P2, target, isDirectAttack: false);
    AttackProcess attack = AttackProcess.For(context);
    AssertTrue(!attack.IsEndAttack, "attack not ended before the rule pass");

    bool progressed = await AutoProcessing.For(context).RuleProcess();

    AssertTrue(progressed, "rule pass progressed");
    AssertTrue(attack.IsEndAttack, "non-Digimon attacker force-ended (BattleWithoutDigimon)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, flipped), "coincident face-down work handled in the same pass");
}

async Task EndGameDrawAndWin()
{
    // Both lose -> DRAW (AS-IS EndGameProcess :397-400 EndGame(null); review P1-1).
    EngineContext both = Board();
    both.PlayerStatusController.MarkLose(P1, "test");
    both.PlayerStatusController.MarkLose(P2, "test");
    bool progressed = await AutoProcessing.For(both).RuleProcess();
    AssertTrue(progressed, "both-lose pass progressed");
    AssertTrue(both.RuleQueryService.IsTerminal(), "both-lose is terminal");
    AssertTrue(((ITerminalOutcomeSink)both.RuleQueryService).TryGetTerminalOutcome(out TerminalOutcome? drawOutcome)
        && drawOutcome is { IsDraw: true, WinnerPlayerId: null }, "both losing = DRAW with no winner");

    // Single loser -> the enemy wins (AS-IS :392-395).
    EngineContext single = Board();
    single.PlayerStatusController.MarkLose(P2, "test");
    await AutoProcessing.For(single).RuleProcess();
    AssertTrue(((ITerminalOutcomeSink)single.RuleQueryService).TryGetTerminalOutcome(out TerminalOutcome? winOutcome)
        && winOutcome is { IsDraw: false } && winOutcome.WinnerPlayerId == P1, "single loser: the enemy wins");
}

// --- Helpers -------------------------------------------------------------

EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 17);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main); // (harness) DoneStartGame gate: CanTrigger needs a live phase (not None)
    return context;
}

HeadlessEntityId Def(EngineContext context, string tag, string cardType, int? dp)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (dp.HasValue)
    {
        meta["dp"] = dp.Value;
    }

    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag, meta, CardType: cardType));
    return new HeadlessEntityId($"card:{tag}");
}

async Task Place(EngineContext context, HeadlessEntityId id, HeadlessPlayerId owner, ChoiceZone zone, int? dp = null)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (dp.HasValue)
    {
        meta[BattleResolver.DpKey] = dp.Value;
    }

    string tag = id.Value["card:".Length..];
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
}

// Attach a link card DIRECTLY on the host metadata (bypassing AddLinkCardAsync's own max enforcement so an
// over-max board can exist for the rule to fix).
void Link(EngineContext context, HeadlessEntityId host, HeadlessEntityId link)
{
    Mutate(context, host, m =>
    {
        var linked = LinkHelpers.ReadLinkedCardIds(Instance(context, host).Metadata).Select(x => x.Value).ToList();
        linked.Insert(0, link.Value);
        m[LinkHelpers.LinkedCardIdsKey] = linked.ToArray();
    });
}

// Live-scan link condition declaration (the AddLinkConditionClass new-model kind-class continuous surface).
// AS-IS CardSource.LinkConditionOf() (CardSource.cs:2727-2741, mirror CardSource.cs:728) scans THIS card's
// live EffectList(EffectTiming.None) for an IAddLinkConditionEffect — there is no EffectRegistry fallback
// any more (the "pre-flip dispatch-first/registry-fallback split is superseded by the flip's enumeration
// model", CardSource.cs comment above LinkConditionOf). So the fixture attaches the AddLinkConditionClass
// via the card's cEntity_EffectController.cEntity_Effect probe — the same live-scan idiom used elsewhere in
// this migration (cardSource.cEntity_EffectController.cEntity_Effect = <probe>) — instead of registering an
// EffectBinding the live scan never reads.
void DeclareLinkCondition(EngineContext context, HeadlessEntityId cardId, Func<Permanent, bool> condition)
{
    var cardSource = new CardSource(context, cardId, P1);
    var linkConditionEffect = CardEffectFactory.AddSelfLinkConditionStaticEffect(condition, linkCost: 0, cardSource);
    cardSource.cEntity_EffectController.cEntity_Effect = new LinkConditionProbe(linkConditionEffect);
}

void Mutate(EngineContext context, HeadlessEntityId id, Action<Dictionary<string, object?>> mutate)
{
    CardInstanceRecord record = Instance(context, id);
    var meta = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
    mutate(meta);
    context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

CardInstanceRecord Instance(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out var r) && r is not null ? r : throw new InvalidOperationException($"missing {id}");

bool InZone(EngineContext context, HeadlessPlayerId owner, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(owner, zone).Contains(cardId);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

sealed class LinkConditionProbe : CEntity_Effect
{
    readonly ICardEffect _effect;
    public LinkConditionProbe(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) =>
        timing == EffectTiming.None ? new List<ICardEffect> { _effect } : new List<ICardEffect>();
}
