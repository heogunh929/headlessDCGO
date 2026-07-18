using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// RD6 (A-2): the [End of Your Turn] effect window must resolve in the ENDING player's still-live frame, BEFORE
// the turn flip (AS-IS AutoProcessing.EndTurnProcess:1511, "step 3" — before the attack loop and the threshold
// re-check at :1528). The live bug: headless emitted OnEndTurn AFTER the flip, so a [End of Your Turn] body that
// gates on `TurnPlayerId == Owner` NO-OPPED entirely in the new turn player's frame. This asserts the effect now
// fires pre-flip against the correct owner (mirrored memory), the multi-effect drain suspends on an order choice,
// and the window resolves BEFORE the end-of-turn attack offer.
//
// RE-TARGETED (4b B4, P-D EndTurn seam): the OLD driver's `HeadlessActionFactory.EndTurn` two-step handover
// (MemoryPass phase → explicit EndTurn action → drain → re-EndTurn → flip) is RETIRED onto
// DcgoMatch.CreatePumpDriven. Under the pump the memory cross auto-runs the AS-IS EndTurnProcess: it drains the
// OnEndTurn window (pre-flip, in the ending player's frame) then the attack loop then the threshold re-check —
// no explicit EndTurn action (§2.1 P-D). Effect-internal choices surface at the agent seat via the
// DeferredChoiceProvider (deferredChoice:true) as ChoiceController pending, resolved by ResolveChoice.
//
// RED-NATURE FINDING (baseline: tests 3+4 red under the OLD half-retired driver, which no longer drained the
// OnEndTurn window and just flipped). Split verdict:
//   * Tests 1+2 (pre-flip MEMORY drain; gain-continues): OLD-DRIVER ARTIFACTS — the pump's real EndTurnProcess
//     drains the memory window pre-flip in the ending player's frame, so they GREEN-FLIP on the faithful retarget
//     (W-EoTFIX corroborates the pump drains permanent/player/interactive OnEndTurn scopes; re-asserted E2E).
//   * Tests 3+4 (multi-effect ORDER choice; end-of-turn ATTACK offer): RETAINED DEBT — kept RED, not forced green.
//     Measured under the pump: (t3) the two TfxEndTurnDraw are NOT collected by the pump's OnEndTurn window
//     (GetSkillInfos == 0; owner-gated draw effects surface 0) so neither draw fires and no SelectProcessOrder
//     choice opens — the turn just drains-and-flips. (t4) only the lose-3 is collected (== 1); the <Vortex>
//     end-of-turn attack is granted here via the OLD EffectRegistry.Register (KeywordBaseBatch2Factory.ToBinding),
//     which the mirror OnEndTurn window (GetSkillInfos / SkillWindowSupply) does not scan, so no attack offer
//     surfaces. Both are genuine drain-order / offer-surfacing gaps, NOT resolvable by a pump re-point in B4:
//     they need (t3) the pump's MultipleSkills to collect+order 2 mandatory same-timing activated effects and
//     (t4) a MIRROR-PATH Vortex grant (A4/GainExecute precedent) + attack-offer surfacing. Filed RD-R4B4-RD6 for
//     a dedicated follow-up. The pre-flip memory drain (the RD6 core fix) IS verified faithful (tests 1,2,+ t4's
//     lose-3 firing to −6/mirrored +6).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("[End of Your Turn] lose-3-memory fires in the ENDING player's frame (pre-flip), not no-op post-flip", EoTFiresPreFlip),
    ("[End of Your Turn] gain memory that lifts the opponent below the threshold CONTINUES the turn (no flip)", EoTGainContinuesTurn),
    ("two activated [End of Your Turn] effects OPEN an order choice (pre-flip drain suspends), then the drain completes and flips", MultiActivatedEoTSuspendsThenReapplies),
    ("the [End of Your Turn] effect window resolves BEFORE the end-of-turn attack offer (AS-IS window-then-attack order)", EoTWindowResolvesBeforeAttack),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task EoTFiresPreFlip()
{
    (DcgoMatch match, EngineContext context) = await NewPumpMatchAsync(seed: 17);
    var cards = (CardDatabase)context.CardRepository;

    // BT1_021 (Red) carries "[End of Your Turn] Lose 3 memory." — place it in P1's battle area and register its
    // effects (the enter-play hook binds the OnEndTurn body).
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxEndTurnLose3Memory"), "TfxEndTurnLose3Memory", "EoTLoser",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var eot = new HeadlessEntityId("p1:field:TfxEoTLose3");
    StageRegistered(match, P1, "TfxEndTurnLose3Memory", eot);

    // A cost-3 play crosses memory 0 -> -3; the pump auto-runs EndTurnProcess. If the OnEndTurn window fires
    // PRE-flip, BT1_021 loses 3 more in P1's frame (-3 -> -6), so P2 inherits the mirrored +6.
    HeadlessEntityId hand = StageHand(match, P1, cost: 3, "p1:hand:VAN");
    context.MemoryController.Set(0);
    await PlayCrossAsync(match, hand);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());

    AssertEqual(P2.Value, context.TurnController.Current.TurnPlayerId?.Value ?? 0, "the turn handed over to P2");
    AssertEqual(6, context.MemoryController.Current.Current,
        "P2 inherits +6: the pre-flip [End of Your Turn] lose-3 fired in P1's frame (bug would leave +3)");
}

async Task EoTGainContinuesTurn()
{
    (DcgoMatch match, EngineContext context) = await NewPumpMatchAsync(seed: 17);
    var cards = (CardDatabase)context.CardRepository;

    // Place a plain Digimon and bind a "[End of Your Turn] gain 3 memory" effect (owner-gated).
    cards.Upsert(new CardRecord(new HeadlessEntityId("GAINER"), "GAINER", "Gainer",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var gainer = new HeadlessEntityId("p1:field:GAINER");
    StagePlain(match, P1, "GAINER", gainer);
    using (AmbientMatchContext.Enter(context))
    {
        var gainSrc = new CardSource(context, gainer, P1, P1);
        CardEffectRegistrar.RegisterOnEnterPlay(context, new EoTGain3Fixture(), "GAINER", gainSrc);
    }

    // A cost-1 play crosses memory 0 -> -1 (the default threshold 1). The pump auto-runs EndTurnProcess: the
    // [End of Your Turn] +3 fires PRE-flip (-1 -> +2), lifting the opponent below the threshold, so the re-check
    // keeps the turn going — no hand-over, the turn continues in Main.
    HeadlessEntityId hand = StageHand(match, P1, cost: 1, "p1:hand:VAN1");
    context.MemoryController.Set(0);
    await PlayCrossAsync(match, hand);

    AssertEqual(P1.Value, context.TurnController.Current.TurnPlayerId?.Value ?? 0, "the turn did NOT hand over — the EoT gain kept it going");
    AssertTrue(context.TurnController.Current.IsMainPlayPhase, "the turn reverted to Main (continues)");
    AssertEqual(2, context.MemoryController.Current.Current, "the EoT +3 lifted memory from -1 to +2 (above the turn-end threshold)");
}

async Task MultiActivatedEoTSuspendsThenReapplies()
{
    (DcgoMatch match, EngineContext context) = await NewPumpMatchAsync(seed: 17);
    var cards = (CardDatabase)context.CardRepository;

    // Two "[End of Your Turn] draw 1" activated fixtures in P1's battle area — at OnEndTurn the window collects TWO
    // mandatory activated markers, so the ending player must ORDER them (a choice that suspends the pre-flip drain).
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxEndTurnDraw"), "TfxEndTurnDraw", "EoTDraw",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    for (int i = 0; i < 2; i++)
    {
        StageRegistered(match, P1, "TfxEndTurnDraw", new HeadlessEntityId($"p1:field:EOTDRAW{i}"));
    }

    HeadlessEntityId hand = StageHand(match, P1, cost: 3, "p1:hand:VAN3");
    context.MemoryController.Set(0);
    // The costed play crosses into the pump turn-end; AS-IS collects the two mandatory activated markers and the
    // ending player ORDERS them (SelectProcessOrder), a choice that suspends the pre-flip drain.
    await PlayCrossAsync(match, hand);
    await StepUntilAsync(match, m => m.Context.ChoiceController.Current.IsPending || AtMainWaitOf(m, P2) || m.IsTerminal());

    // RETAINED DEBT (RD-R4B4-RD6a, kept RED — not forced green): measured under the pump, the two TfxEndTurnDraw are
    // NOT collected by the OnEndTurn window (GetSkillInfos == 0) — the owner-gated draw effects surface nothing —
    // so neither draw fires and no order choice opens; the pump drains-and-flips to P2. The multi-effect ordering
    // is a real AS-IS behavior the pump does not yet reproduce for these fixtures. This assertion stays as the
    // honest red signal.
    AssertEqual(P1.Value, context.TurnController.Current.TurnPlayerId?.Value ?? 0, "the turn did NOT hand over — an EoT order choice is pending");
    AssertTrue(context.ChoiceController.Current.IsPending, "the two activated [End of Your Turn] effects opened a pending order choice");

    // Resolve every pending window choice (order pick + any follow-up); the drain then completes and flips.
    for (int guard = 0; guard < 12 && context.ChoiceController.Current.IsPending; guard++)
    {
        await ResolveFirstPendingAsync(match);
    }
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());

    AssertEqual(P2.Value, context.TurnController.Current.TurnPlayerId?.Value ?? 0,
        "after the order choice resolved, the drain completed and flipped the turn to P2 (no double-fire, no crash)");
}

async Task EoTWindowResolvesBeforeAttack()
{
    (DcgoMatch match, EngineContext context) = await NewPumpMatchAsync(seed: 17);
    var cards = (CardDatabase)context.CardRepository;

    // BT1_021: "[End of Your Turn] lose 3 memory" — a memory effect that resolves in the OnEndTurn window.
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxEndTurnLose3Memory"), "TfxEndTurnLose3Memory", "EoTLoser",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var eot = new HeadlessEntityId("p1:field:TfxEoTLose3");
    StageRegistered(match, P1, "TfxEndTurnLose3Memory", eot);

    // A <Vortex> Digimon (P1, unsuspended) whose end-of-turn attack offer opens AFTER the OnEndTurn window;
    // + an opponent Digimon (P2) so Vortex has a legal target.
    var vortex = await PlaceBareDigimon(context, P1, "VTX", dp: 5000, suspended: false);
    var vfx = KeywordBaseBatch2Factory.Create(KeywordBaseBatch2Kind.Vortex, vortex, targetEntityId: null, isInherited: false, isLinked: false);
    context.EffectRegistry.Register(KeywordBaseBatch2Factory.ToBinding(vfx, P1, new EffectContext(P1, vortex)));
    await PlaceBareDigimon(context, P2, "FOE", dp: 3000, suspended: false);

    HeadlessEntityId hand = StageHand(match, P1, cost: 3, "p1:hand:VANv");
    context.MemoryController.Set(0);
    // The pump turn-end drains BT1_021 (memory -3 -> -6) FIRST, THEN the Vortex attack offer opens.
    await PlayCrossAsync(match, hand);
    await StepUntilAsync(match, m => m.Context.ChoiceController.Current.IsPending || AtMainWaitOf(m, P2) || m.IsTerminal());

    // RETAINED DEBT (RD-R4B4-RD6b, kept RED — not forced green): the pre-flip memory drain IS faithful (the lose-3
    // fires in the OnEndTurn window: memory -3 -> -6, mirrored to +6 at the flip). But the <Vortex> end-of-turn
    // attack does NOT surface as a pre-flip offer: it is granted here via the OLD EffectRegistry.Register
    // (KeywordBaseBatch2Factory.ToBinding), which the mirror OnEndTurn window (GetSkillInfos == 1 = only the lose-3)
    // does not scan — so the turn drains-and-flips to P2 with no attack offer. Faithful reproduction needs a
    // MIRROR-PATH Vortex grant (A4/GainExecute precedent, which DOES surface end-of-turn attacks under the pump)
    // + attack-offer surfacing — a rewrite beyond a B4 re-point. This assertion stays as the honest red signal.
    AssertEqual(P1.Value, context.TurnController.Current.TurnPlayerId?.Value ?? 0, "the turn did NOT hand over — the Vortex attack offer is pending");
    AssertTrue(context.ChoiceController.Current.IsPending, "the end-of-turn Vortex attack offer opened (a pending choice)");
    AssertEqual(-6, context.MemoryController.Current.Current,
        "BT1_021's [End of Your Turn] -3 already fired in the OnEndTurn window BEFORE the attack offer (window-then-attack); pre-fix order would leave -3");
}

// --- Card staging --------------------------------------------------------

// Stage a costed vanilla Digimon into P1's hand (the memory-crossing play card).
HeadlessEntityId StageHand(DcgoMatch match, HeadlessPlayerId owner, int cost, string instanceId)
{
    EngineContext ctx = match.Context;
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"VAN{cost}");
    cards.Upsert(new CardRecord(defId, defId.Value, "Vanilla",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon", PlayCost: cost));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Hand)).GetAwaiter().GetResult();
    return id;
}

// Stage a field permanent whose CardNumber-dispatched effects are registered (RegisterCard).
void StageRegistered(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, HeadlessEntityId id)
{
    EngineContext ctx = match.Context;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(cardNumber), owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
    CardEffectRegistrar.RegisterCard(ctx, id, owner);
}

// Stage a field permanent WITHOUT effect registration (the caller binds effects itself).
void StagePlain(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, HeadlessEntityId id)
{
    EngineContext ctx = match.Context;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(cardNumber), owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
}

static async Task<HeadlessEntityId> PlaceBareDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// --- Pump harness --------------------------------------------------------

async Task<(DcgoMatch Match, EngineContext Context)> NewPumpMatchAsync(int seed)
{
    // deferredChoice:true → effect-internal choices surface at the agent seat as ChoiceController pending
    // (resolved by ResolveChoice), the same pending/resolve surface the OLD RD6 test asserted.
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed, deferredChoice: true);
    var cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup));
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return (match, context);
}

// Apply the costed play that crosses memory (the memory cross is the pump turn-end trigger), then drive the
// pump turn-end to a fixpoint: the memory cross auto-runs EndTurnProcess (drain -> re-check -> flip OR continue).
// Stops early at a pending EoT choice so the test can observe it. Result-independent (settles on an unchanged
// signature), so it never masks a mis-drive as green.
async Task PlayCrossAsync(DcgoMatch match, HeadlessEntityId handCardId)
{
    LegalAction play = Legal(match, P1)
        .Single(x => x.ActionType == HeadlessActionTypes.PlayCard && x.Id.Value.Contains(handCardId.Value, StringComparison.Ordinal));
    await ApplyAsync(match, play);

    string? sig = null;
    int stable = 0;
    for (int i = 0; i < 96; i++)
    {
        if (match.HasPendingChoice() || match.IsTerminal()) return;
        await StepOnceAsync(match);
        HeadlessTurnState t = match.Context.TurnController.Current;
        string cur = $"{t.TurnPlayerId}/{t.Phase}/{t.StepCursor}/{match.Context.MemoryController.Current.Current}";
        if (cur == sig) { if (++stable >= 2) return; }
        else { sig = cur; stable = 0; }
    }
}

// Auto-resolving drive (declines breeding/mulligan, resolves other pending via ResolveChoice) — used to reach a
// settled main wait.
async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else await StepOnceAsync(match);
    }
    if (!condition(match)) throw Stuck(match);
}

// Non-resolving drive — steps the pump but STOPS at a pending choice (so the test can observe it).
async Task StepUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        await StepOnceAsync(match);
    }
    if (!condition(match)) throw Stuck(match);
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    await ApplyAsync(match, action);
}

async Task ResolveFirstPendingAsync(DcgoMatch match)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest?.PlayerId ?? P1;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending order choice");
    await ApplyAsync(match, action);
}

async Task ApplyAsync(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

IReadOnlyList<LegalAction> Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

InvalidOperationException Stuck(DcgoMatch match)
{
    HeadlessTurnState t = match.Context.TurnController.Current;
    return new InvalidOperationException(
        $"pump drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
        $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} " +
        $"controllerPending:{match.Context.ChoiceController.Current.IsPending} terminal:{match.IsTerminal()} memory:{match.Context.MemoryController.Current.Current}");
}

// --- Helpers -------------------------------------------------------------

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

// (R3-C2b-2) A test-fixture "[End of Your Turn] gain 3 memory" as a new-model inline ActivateClass — surfaced via
// EffectList when RegisterOnEnterPlay sets the card's cEntity_Effect.
public sealed class EoTGain3Fixture : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEndTurn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +3", _ => CardEffectCommons.IsOwnerTurn(card), card);
            activateClass.SetUpActivateClass(_ => true, async _ => await card.Owner.AddMemory(3, activateClass), -1, false, "[End of Your Turn] Gain 3 memory.");
            effects.Add(activateClass);
        }
        return effects;
    }
}
