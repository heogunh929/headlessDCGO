using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (W6-T) trigger-gate commons — 1:1 mirrors of the AS-IS CanTriggerX(hashtable, ...) gates, reading the
// ENRICHED resolve context (subject = TriggerEntityId, event metadata under "event.<key>" —
// GameFlowProcessor.EnrichWithEventSubject). Verbatim AS-IS bodies verified (primitive_w6_design.md W6-T).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("OnPlay vs WhenDigivolving: the digivolve timing flips the pair of gates", PlayVsDigivolve),
    ("subject containment: the gate fires for the subject's UNDER-card too (cardSources.Contains mirror)", UnderCardContainment),
    ("OptionMain/Security gate: only the resolving card itself", OptionMainGate),
    ("permanent-predicate forms evaluate the view predicate over the SUBJECT", PermanentPredicateForms),
    ("rootCondition filters by the event's from-zone", RootCondition),
    ("IsByEffect: deletion stamps (flag + causing source) drive the gate 1:1", ByEffectGate),
    ("IsJogress reads the event flag", JogressFlag),
    ("CanActivateOnDeletion: true deletion (top in trash) vs bounce; token unconditional", ActivateOnDeletion),
    ("CanTriggerWhenLoseSecurity: the losing player's condition over the moved card's owner", LoseSecurity),
    ("(롱테일) WouldPlay/WouldDigivolve: BeforePayCost isEvolution + target predicate", WouldGates),
    ("(롱테일) WhenLinked / OnAddDigivolutionCard: link-card and added-cards predicates", LinkAndSourcesGates),
    ("(롱테일) IsByBattle + hashtable accessors + Tamer/Security predicates", ByBattleAndAccessors),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task PlayVsDigivolve()
{
    EngineContext ctx = Ctx();
    var card = await Place(ctx, P1, "SELF");
    CardSource view = V(ctx, card);

    var playCtx = ResolveCtx(ctx, subject: card, values: new() { ["event.fromZone"] = "Hand" });
    AssertTrue(CardEffectCommons.CanTriggerOnPlay(playCtx, view), "a play event passes OnPlay");
    AssertTrue(!CardEffectCommons.CanTriggerWhenDigivolving(playCtx, view), "a play event fails WhenDigivolving");

    var evoCtx = ResolveCtx(ctx, subject: card, values: new()
    {
        [$"{GameFlowProcessor.EventValuePrefix}{AutoProcessingTriggerCollector.TriggerTimingKey}"] = TriggerTimings.WhenDigivolving,
    });
    AssertTrue(!CardEffectCommons.CanTriggerOnPlay(evoCtx, view), "a digivolve event fails OnPlay");
    AssertTrue(CardEffectCommons.CanTriggerWhenDigivolving(evoCtx, view), "a digivolve event passes WhenDigivolving");
}

async Task UnderCardContainment()
{
    EngineContext ctx = Ctx();
    var top = await Place(ctx, P1, "TOP");
    var under = await Place(ctx, P1, "UNDER");
    SetMeta(ctx, top, HeadlessDCGO.Engine.Headless.State.DigivolutionStackReader.SourceIdsKey, new[] { under.Value });

    var atkCtx = ResolveCtx(ctx, subject: top, values: new());
    AssertTrue(CardEffectCommons.CanTriggerOnAttack(atkCtx, V(ctx, top)), "the subject top card matches");
    AssertTrue(CardEffectCommons.CanTriggerOnAttack(atkCtx, V(ctx, under)), "an UNDER-card of the subject matches (cardSources.Contains mirror)");
    var other = await Place(ctx, P1, "OTHER");
    AssertTrue(!CardEffectCommons.CanTriggerOnAttack(atkCtx, V(ctx, other)), "an unrelated card does not");
    AssertTrue(CardEffectCommons.CanTriggerOnEndAttack(atkCtx, V(ctx, top)), "OnEndAttack delegates to the attack gate");
}

async Task OptionMainGate()
{
    EngineContext ctx = Ctx();
    var option = await Place(ctx, P1, "OPT");
    var other = await Place(ctx, P1, "OTHER");
    var mainCtx = ResolveCtx(ctx, subject: option, values: new());
    AssertTrue(CardEffectCommons.CanTriggerOptionMainEffect(mainCtx, V(ctx, option)), "the resolving card passes");
    AssertTrue(!CardEffectCommons.CanTriggerOptionMainEffect(mainCtx, V(ctx, other)), "another card fails");
    AssertTrue(CardEffectCommons.CanTriggerSecurityEffect(mainCtx, V(ctx, option)), "the security gate delegates");
}

async Task PermanentPredicateForms()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF");
    var subject = await Place(ctx, P2, "SUBJ", level: 6);
    var evt = ResolveCtx(ctx, subject: subject, values: new());
    AssertTrue(CardEffectCommons.CanTriggerOnPermanentAttack(evt, V(ctx, self), p => p.Level == 6), "predicate over the subject passes");
    AssertTrue(!CardEffectCommons.CanTriggerOnPermanentAttack(evt, V(ctx, self), p => p.Level == 3), "predicate mismatch fails");
    AssertTrue(CardEffectCommons.CanTriggerWhenPermanentSuspends(evt, V(ctx, self), p => p.Level == 6), "suspend gate (battle-area subject) passes");
}

async Task RootCondition()
{
    EngineContext ctx = Ctx();
    var card = await Place(ctx, P1, "SELF");
    var fromHand = ResolveCtx(ctx, subject: card, values: new() { ["event.fromZone"] = "Hand" });
    var fromTrash = ResolveCtx(ctx, subject: card, values: new() { ["event.fromZone"] = "Trash" });
    AssertTrue(CardEffectCommons.CanTriggerOnPlay(fromHand, V(ctx, card), root => root == ChoiceZone.Hand), "Hand root passes the Hand filter");
    AssertTrue(!CardEffectCommons.CanTriggerOnPlay(fromTrash, V(ctx, card), root => root == ChoiceZone.Hand), "Trash root fails the Hand filter");
}

async Task ByEffectGate()
{
    EngineContext ctx = Ctx();
    var dead = await Place(ctx, P1, "DEAD");
    var deleter = await Place(ctx, P2, "DELETER");
    var self = await Place(ctx, P1, "SELF");
    var evt = ResolveCtx(ctx, subject: dead, values: new());

    AssertTrue(!CardEffectCommons.IsByEffect(evt, V(ctx, self)), "no deletion stamps -> not by effect (battle deletion shape)");
    SetMeta(ctx, dead, MatchStateMutationSink.DeletedByEffectKey, true);
    SetMeta(ctx, dead, MatchStateMutationSink.DeletedBySourceEntityIdKey, deleter.Value);
    AssertTrue(CardEffectCommons.IsByEffect(evt, V(ctx, self)), "the stamped flag passes with no condition");
    AssertTrue(CardEffectCommons.IsByEffect(evt, V(ctx, self), src => src.Owner == P2), "the causing-source predicate is evaluated 1:1");
    AssertTrue(!CardEffectCommons.IsByEffect(evt, V(ctx, self), src => src.Owner == P1), "predicate mismatch fails");
}

async Task JogressFlag()
{
    EngineContext ctx = Ctx();
    var card = await Place(ctx, P1, "SELF");
    var dna = ResolveCtx(ctx, subject: card, values: new() { [$"{GameFlowProcessor.EventValuePrefix}isJogress"] = true });
    var plain = ResolveCtx(ctx, subject: card, values: new());
    AssertTrue(CardEffectCommons.IsJogress(dna) && !CardEffectCommons.IsJogress(plain), "the DNA flag drives IsJogress");
}

async Task ActivateOnDeletion()
{
    EngineContext ctx = Ctx();
    var dead = await Place(ctx, P1, "DEAD");
    var self = await Place(ctx, P1, "SELF");
    var evt = ResolveCtx(ctx, subject: dead, values: new());

    AssertTrue(!CardEffectCommons.CanActivateOnDeletion(evt, V(ctx, dead)), "still on the battle area (bounce shape) -> false");
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, dead, ChoiceZone.BattleArea, ChoiceZone.Trash));
    AssertTrue(CardEffectCommons.CanActivateOnDeletion(evt, V(ctx, dead)), "top card in trash (true deletion) -> true");
    AssertTrue(!CardEffectCommons.CanActivateOnDeletion(evt, V(ctx, self)), "an unrelated card -> false");

    var token = await Place(ctx, P1, "TOK");
    SetMeta(ctx, token, "isToken", true);
    AssertTrue(CardEffectCommons.CanActivateOnDeletion(evt, V(ctx, token)), "a token activates unconditionally (AS-IS :115)");
}

async Task LoseSecurity()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF");
    var secCard = await Place(ctx, P2, "SEC");
    var evt = ResolveCtx(ctx, subject: secCard, values: new());
    AssertTrue(CardEffectCommons.CanTriggerWhenLoseSecurity(evt, V(ctx, self), p => p == P2), "the losing player (card owner) matches");
    AssertTrue(!CardEffectCommons.CanTriggerWhenLoseSecurity(evt, V(ctx, self), p => p == P1), "the other player does not");
}

async Task WouldGates()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF");
    var playing = await Place(ctx, P1, "PLAYING", level: 5);
    var target = await Place(ctx, P1, "EVOTGT", level: 4);

    var wouldPlay = ResolveCtx(ctx, subject: playing, values: new() { [$"{GameFlowProcessor.EventValuePrefix}isEvolution"] = false });
    AssertTrue(CardEffectCommons.CanTriggerWhenPermanentWouldPlay(wouldPlay, V(ctx, self), cs => cs.Level == 5), "would-PLAY passes with the card predicate");
    AssertTrue(!CardEffectCommons.CanTriggerWhenPermanentWouldDigivolve(wouldPlay, V(ctx, self)), "a play is not a would-digivolve");

    var wouldEvo = ResolveCtx(ctx, subject: playing, values: new()
    {
        [$"{GameFlowProcessor.EventValuePrefix}isEvolution"] = true,
        [$"{GameFlowProcessor.EventValuePrefix}targetCardId"] = target.Value,
    });
    AssertTrue(CardEffectCommons.CanTriggerWhenPermanentWouldDigivolve(wouldEvo, V(ctx, self), p => p.Level == 4, cs => cs.Level == 5),
        "would-DIGIVOLVE evaluates the target permanent AND the digivolving card");
    AssertTrue(!CardEffectCommons.CanTriggerWhenPermanentWouldPlay(wouldEvo, V(ctx, self)), "an evolution is not a would-play");
}

async Task LinkAndSourcesGates()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF");
    var host = await Place(ctx, P1, "HOST", level: 5);
    var link = await Place(ctx, P1, "LINKC", level: 3);

    var linked = ResolveCtx(ctx, subject: host, values: new() { [$"{GameFlowProcessor.EventValuePrefix}linkCardId"] = link.Value });
    AssertTrue(CardEffectCommons.CanTriggerWhenLinked(linked, V(ctx, self), p => p.Level == 5, cs => cs.Level == 3),
        "host + link-card predicates both evaluated");
    AssertTrue(!CardEffectCommons.CanTriggerWhenLinked(linked, V(ctx, self), p => p.Level == 5, cs => cs.Level == 9), "link mismatch fails");

    var added = ResolveCtx(ctx, subject: host, values: new() { [$"{GameFlowProcessor.EventValuePrefix}addedCardIds"] = link.Value });
    AssertTrue(CardEffectCommons.CanTriggerOnAddDigivolutionCard(added, V(ctx, self), p => p.Level == 5, null, cs => cs.Level == 3),
        "added-source predicate hits");
}

async Task ByBattleAndAccessors()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF");
    var dead = await Place(ctx, P2, "DEAD");
    var evt = ResolveCtx(ctx, subject: dead, values: new());

    AssertTrue(!CardEffectCommons.IsByBattle(evt, V(ctx, self)), "no battle marker -> false");
    SetMeta(ctx, dead, BattleResolver.DeletedByBattleKey, true);
    AssertTrue(CardEffectCommons.IsByBattle(evt, V(ctx, self)), "battle-deletion marker -> true");

    AssertTrue(CardEffectCommons.GetPermanentFromHashtable(evt, V(ctx, self))?.InstanceId == dead, "GetPermanentFromHashtable = subject view");
    AssertTrue(CardEffectCommons.GetCardFromHashtable(evt, V(ctx, self))?.InstanceId == dead, "GetCardFromHashtable = subject card");
    AssertTrue(CardEffectCommons.GetPermanentsFromHashtable(evt, V(ctx, self)).Count == 1, "GetPermanents = single-subject list");

    var tamer = await Place(ctx, P1, "TAMER", cardType: "Tamer");
    AssertTrue(CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaTamer(new Permanent(ctx, tamer, P1), V(ctx, self)), "owner battle-area Tamer");
    var sec = await Put(ctx, P1, "SEC", ChoiceZone.Security);
    AssertTrue(CardEffectCommons.HasMatchConditionOwnersSecurity(V(ctx, self), cs => cs.InstanceId == sec), "security predicate hits");
}

// --- Harness ---

CardEffectResolveContext ResolveCtx(EngineContext ctx, HeadlessEntityId subject, Dictionary<string, object?> values)
{
    var effectContext = new EffectContext(P1, P1, new HeadlessEntityId("gate-src"), triggerEntityId: subject,
        targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
    return new CardEffectResolveContext(new EffectRequest(new HeadlessEntityId("gate-req"), P1, "Trigger", effectContext));
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 968);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone, int level = 4, string cardType = "Digimon")
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level = 4, string cardType = "Digimon")
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
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
