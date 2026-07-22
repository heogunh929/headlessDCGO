using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (W6 tail sweep) representative coverage for the final commons-parity batches: gate tail, predicate
// tail, player-scope grants, tokens, process fragments, on-deletion filters. All mirrors are verbatim-dump
// verified (primitive_w6_design.md).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("gate tail: trash-hand/discard-library/use-option(cost)/battle-result winners", GateTail),
    ("predicate tail: IsMinCost/IsExistLinked/unique colours/breeding digimon", PredicateTail),
    ("player-scope grant: CanNotUnsuspend player-wide (registry) expires at turn end", PlayerScopeGrants),
    ("tokens: quantity Diaboromon to owner, Petrification to the OPPONENT, effect-class alias", Tokens),
    ("Save: deletion-window pick puts the dead card under a Tamer", SaveFragment),
    ("AddSelfDeleteEffect: own-turn-end marker promotes and the loop deletes it", TurnEndSelfDelete),
    ("OnDeletionWith filters: card-name contains / trait / save-text snapshot", OnDeletionFilters),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task GateTail()
{
    EngineContext ctx = Ctx();
    var self = await Put(ctx, P1, "SELF", ChoiceZone.BattleArea);
    var discarded = await Put(ctx, P1, "DISC", ChoiceZone.Trash);

    // (F1-Tier1) AS-IS CanTriggerOnTrashHand/Security require CardEffect != null (OnTrashHand.cs:19-21) — the
    // headless mirror threads the cause effect's source id as event.discardCauseEffectId, so an effect-driven
    // discard event carries it. (Library has no CardEffect check, so it is unaffected by the cause.)
    var evt = ResolveCtx(ctx, subject: discarded, values: new()
    {
        [$"{GameFlowProcessor.EventValuePrefix}{MatchStateMutationSink.DiscardCauseEffectIdKey}"] = self.Value,
    });
    AssertTrue(CardEffectCommons.CanTriggerOnTrashSelfHand(evt, V(ctx, discarded)), "self hand-trash gate hits on the subject");
    AssertTrue(!CardEffectCommons.CanTriggerOnTrashSelfHand(evt, V(ctx, self)), "unrelated card misses");
    AssertTrue(CardEffectCommons.CanTriggerWhenSelfDiscardLibrary(evt, V(ctx, discarded)), "self library-discard gate");
    // (F1-Tier1) a NON-effect discard (no cause id) is rejected even on a matching subject — AS-IS CardEffect==null.
    var noCauseEvt = ResolveCtx(ctx, subject: discarded, values: new());
    AssertTrue(!CardEffectCommons.CanTriggerOnTrashSelfHand(noCauseEvt, V(ctx, discarded)), "non-effect hand-trash misses (CardEffect==null)");

    var option = await Put(ctx, P1, "OPT", ChoiceZone.Trash, cardType: "Option");
    var useEvt = ResolveCtx(ctx, subject: option, values: new() { [$"{GameFlowProcessor.EventValuePrefix}cost"] = 3 });
    AssertTrue(CardEffectCommons.CanTriggerWhenOwnerUseOption(useEvt, V(ctx, self), null, cost => cost >= 3), "owner option-use with cost predicate");
    AssertTrue(!CardEffectCommons.CanTriggerWhenOwnerUseOption(useEvt, V(ctx, self), null, cost => cost >= 4), "cost predicate miss");

    var winner = await Put(ctx, P1, "WIN", ChoiceZone.BattleArea, level: 6);
    var loser = await Put(ctx, P2, "LOSE", ChoiceZone.Trash);
    var battleEvt = ResolveCtx(ctx, subject: loser, values: new()
    {
        [$"{GameFlowProcessor.EventValuePrefix}winnerIds"] = winner.Value,
        [$"{GameFlowProcessor.EventValuePrefix}loserIds"] = loser.Value,
        [$"{GameFlowProcessor.EventValuePrefix}loserRealIds"] = loser.Value,
    });
    AssertTrue(CardEffectCommons.CanTriggerWhenDeleteOpponentDigimonByBattle(
        battleEvt, V(ctx, self), p => p.Level == 6, null, isOnlyWinnerSurvive: false), "battle-result winner predicate");
    AssertTrue(CardEffectCommons.CanTriggerWhenWinBattle(battleEvt, V(ctx, winner)), "the winner's own WhenWinBattle");
}

async Task PredicateTail()
{
    EngineContext ctx = Ctx();
    var cheap = await Put(ctx, P1, "CHEAP", ChoiceZone.BattleArea, playCost: 3);
    var pricey = await Put(ctx, P1, "PRICEY", ChoiceZone.BattleArea, playCost: 8);
    AssertTrue(CardEffectCommons.IsMinCost(Perm(ctx, cheap), P1, IsDigimonOnly: true), "min printed cost");
    AssertTrue(CardEffectCommons.IsMaxCost(Perm(ctx, pricey), P1, IsDigimonOnly: true), "max printed cost");
    AssertTrue(CardEffectCommons.GetNonMaxCostPermanents(V(ctx, cheap), P1).Single().InstanceId == cheap, "non-max set");

    var host = await Put(ctx, P1, "HOST", ChoiceZone.BattleArea);
    var link = await Put(ctx, P1, "LNK", ChoiceZone.Hand);
    await LinkHelpers.AddLinkCardAsync(ctx.CardInstanceRepository, ctx.ZoneMover, host, link, ChoiceZone.Hand);
    AssertTrue(CardEffectCommons.IsExistLinked(V(ctx, link)), "linked card detected");

    AssertTrue(CardEffectCommons.GetUniqueColourCountOnOwnerBattleArea(V(ctx, cheap), _ => true) >= 1, "unique colours counted");
    var egg = await Put(ctx, P1, "EGG", ChoiceZone.BreedingArea);
    AssertTrue(CardEffectCommons.IsExistOnBreedingAreaDigimon(V(ctx, egg)), "breeding digimon");
}

async Task PlayerScopeGrants()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var small = await Put(ctx, P1, "SMALL", ChoiceZone.BattleArea, level: 3);

    // (G-clean-2) The former player-scope [Blocker] grant assertion tested the deleted GainKeywordToPermanent /
    // GainBlockerPlayerEffect(CardSource) funnel (a ContinuousKeywordGate.Blocker registry marker). The AS-IS
    // player-scope Blocker grant (AddEffectToPlayer None bucket -> Permanent.HasBlocker) is now witnessed in
    // G9-067.W6GainCommons. CanNotUnsuspend below still rides the surviving GainToPlayerScope registry funnel.
    SetMeta(ctx, small, "isSuspended", true);
    // (J-2) hold a match scope so the AS-IS interface reader (Permanent.CanUnsuspend) can resolve GManager.instance.
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    CardEffectCommons.GainCanNotUnsuspendPlayerEffect(null, EffectDuration.UntilOpponentTurnEnd, V(ctx, src));
    AssertTrue(!new Permanent(ctx, small).CanUnsuspend, "player-wide unsuspend lock");
    // (J-2) the player-scope grant now lives in the owning player's UntilOpponentTurnEnd bucket (AS-IS
    // AddEffectToPlayer), not the retired EffectRegistry — expire it the AS-IS way (`= new List<>()`).
    new Player(ctx, P1).UntilOpponentTurnEndEffects = new();
    AssertTrue(new Permanent(ctx, small).CanUnsuspend, "expired");
}

async Task Tokens()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    IReadOnlyList<HeadlessEntityId> played = await CardEffectCommons.PlayDiaboromonToken(V(ctx, src), quantity: 2);
    AssertTrue(played.Count == 2 && played.All(id => InZone(ctx, P1, ChoiceZone.BattleArea, id)), "2 Diaboromon tokens on the OWNER's board");
    ctx.CardInstanceRepository.TryGetInstance(played[0], out CardInstanceRecord? tok);
    AssertTrue(tok!.Metadata.TryGetValue("isToken", out object? t) && t is true, "isToken marked");
    AssertTrue(new CardSource(ctx, played[0], P1, P1).EqualsCardName("Diaboromon"), "spec name applied");

    IReadOnlyList<HeadlessEntityId> stone = await CardEffectCommons.PlayPetrificationToken(V(ctx, src));
    AssertTrue(stone.Count == 1 && InZone(ctx, P2, ChoiceZone.BattleArea, stone[0]), "Petrification lands on the OPPONENT's board (AS-IS isOwnerPermanent:false)");
}

async Task SaveFragment()
{
    EngineContext ctx = Ctx();
    var tamer = await Put(ctx, P1, "TAMER", ChoiceZone.BattleArea, cardType: "Tamer");
    var dead = await Put(ctx, P1, "DEAD", ChoiceZone.Trash);
    var evt = ResolveCtx(ctx, subject: dead, values: new());

    AssertTrue(CardEffectCommons.CanActivateSave(evt, V(ctx, dead), p => p.TopCard.IsTamer), "save gate: dead top in trash + receiving Tamer");
    ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(ChoiceResult.Select(tamer));
    await CardEffectCommons.SaveProcess(evt, V(ctx, dead), p => p.TopCard.IsTamer);

    ctx.CardInstanceRepository.TryGetInstance(tamer, out CardInstanceRecord? host);
    var sources = (host!.Metadata[HeadlessDCGO.Engine.Headless.State.DigivolutionStackReader.SourceIdsKey] as IEnumerable<string>)?.ToArray() ?? Array.Empty<string>();
    AssertTrue(sources.Contains(dead.Value), "the dead card went UNDER the Tamer");
}

async Task TurnEndSelfDelete()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var doomed = await Put(ctx, P1, "DOOMED", ChoiceZone.BattleArea);
    CardEffectCommons.AddSelfDeleteEffect(Perm(ctx, doomed), "own", V(ctx, src));

    // Promote (the end-turn cleanup for P1's turn) then run the loop sweep.
    new HeadlessEndTurnCleanupFlow().Cleanup(ctx, ctx.TurnController.Current);
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertTrue(InZone(ctx, P1, ChoiceZone.Trash, doomed), "the marked permanent self-deleted at its owner's turn end");
}

// (G-clean-2) The BecomeDigimonThatCantDigivolve sub-test is retired here — it drove the deleted
// BecomeDigimonThatCantDigivolve(...CardSource) substrate (TreatAsDigimon via the GainKeywordToPermanent
// registry funnel + registry-based ExpireTurnEnd). The AS-IS grant (three StaticEffects in the None bucket ->
// ContinuousKeywordGate.IsDigimon) is now witnessed in G9-067.W6GainCommons (TreatAsDigimonGrant).

async Task OnDeletionFilters()
{
    EngineContext ctx = Ctx();
    var self = await Put(ctx, P1, "SELF", ChoiceZone.BattleArea);
    var dead = await Put(ctx, P1, "DEADGREY", ChoiceZone.Trash, name: "WarGreymon", traits: "Dragonkin");
    SetMeta(ctx, dead, DeletionReplacementGate.HasSaveKey, true);
    var evt = ResolveCtx(ctx, subject: dead, values: new());

    AssertTrue(CardEffectCommons.CanActivateSelfOnDeletionWithContainingCardName(evt, "Greymon", V(ctx, dead)), "name-contains filter");
    AssertTrue(!CardEffectCommons.CanActivateSelfOnDeletionWithContainingCardName(evt, "Garurumon", V(ctx, dead)), "name miss");
    AssertTrue(CardEffectCommons.CanActivateSelfOnDeletionWithContainingTrait(evt, "Dragon", V(ctx, dead)), "trait-contains filter");
    AssertTrue(CardEffectCommons.CanActivateSelefOnDeletionWithSaveText(evt, V(ctx, dead)), "the P1/A4 Save snapshot drives the save-text filter");
}

// --- Harness ---

CardEffectResolveContext ResolveCtx(EngineContext ctx, HeadlessEntityId subject, Dictionary<string, object?> values)
{
    var effectContext = new EffectContext(P1, P1, new HeadlessEntityId("tail-src"), triggerEntityId: subject,
        targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
    return new CardEffectResolveContext(new EffectRequest(new HeadlessEntityId("tail-req"), P1, "Trigger", effectContext));
}

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 974);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (J-2) CanNotUnsuspend now surfaces through the AS-IS interface reader, which gates on DoneStartGame (phase
    // past None) via ICardEffect.CanUse — start the game like the other interface-scan restriction harnesses.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> Put(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone,
    string cardType = "Digimon", int dp = 5000, int level = 4, int? playCost = null, string? name = null, string? traits = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level, ["colors"] = new[] { "Red" } };
    if (traits is not null) { meta["traits"] = new[] { traits }; }
    cards.Upsert(new CardRecord(defId, tag, name ?? tag, meta, CardType: cardType, PlayCost: playCost));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;
bool InZone(EngineContext ctx, HeadlessPlayerId p, ChoiceZone z, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(p, z).Contains(id);

void SetMeta(EngineContext ctx, HeadlessEntityId id, string key, object? value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with
    {
        Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { [key] = value }
    });
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
