using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT17.Red;
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX6.White;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons; // extension methods (JogressConditionOf)
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using Scr = HeadlessDCGO.Engine.Assets.Scripts.Script;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// DNATEMP — behavior witnesses for the TEMP-MATERIAL DNA family (RD-W3 DNA-temp / RD-S3-BT17_095): the last
// frame-dependent family. Witness card = BT17_095 <Delay> (user-selected). Target Omnimon = AD1_025 (real
// jogress condition: Lv.6 [Greymon] + Lv.6 [Garurumon], each slot wrapped in IsPermanentExistsOnOwnerBattle-
// AreaDigimon by GetJogressConditionClass).
//
// DESIGN DECISION (OPTION 2): the AS-IS entity-less transient `new Permanent(new List<CardSource>(){src})` +
// `FieldPermanents[0]=transient` sparse-injection maps 1:1 to a read-only mirror Permanent VIEW over the
// candidate card's OWN instance id carrying snapshotZone:BattleArea — TopCard resolves to the card (every
// TopCard-property predicate is identical) and the field-membership sub-check the jogress wrapper gates on
// answers TRUE from the snapshot, with NO zone mutation / trigger. Tests 1 proves it (with-snapshot passes,
// without-snapshot fails — the snapshot is load-bearing exactly where the AS-IS slot-write was).
// Harness = FRAME-Write.Witness 정본 (DcgoMatch.CreatePumpDriven + PolicyChoiceProvider).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("SnapshotZone transient joint-eval (design decision Option 2): a HAND Lv.6 Garurumon evaluated via new Permanent(id, owner, snapshotZone:BattleArea) SATISFIES AD1_025's field-membership-wrapped jogress element; WITHOUT the snapshot it FAILS", SnapshotTransientEval),
    ("CanJogressFromTargetPermanent (singular, ported): AD1_025 + a battle-area Lv.6 Greymon root → true; a non-jogress-root (Agumon Lv.3) → false", SingularJogressCheck),
    ("BT17_095 <Delay> end-to-end: delete self → temp hand-material co-eval → materialize → jogress collapse runs to completion (MIG4-DISCARDEVOROOTS landed) — AD1_025 surviving with BOTH roots stacked underneath (full collapse WRITE)", DelayEndToEndToMig4),
    ("Rollback half: CardObjectController.AddHandCard(material, false) un-materializes a battle-area temp material back to its owner's hand", RollbackUnmaterialize),
    ("EX6_072 helper gate — CanJogressWithHandOrTrash (ported co-eval): AD1_025 in hand WITH a field Lv.6 [Greymon] + a hand Lv.6 [Garurumon] passes; missing the hand material FAILS; a false targetCardCondition FAILS (control)", Ex6072HelperGate),
    ("EX6_072 [Main] end-to-end (raw DNADigivolveWithHandOrTrash helper): DNA-target select → From-Battle-Area branch → field-root + temp hand-material co-eval → materialize → jogress collapse completes — AD1_025 surviving with BOTH roots stacked underneath (full collapse WRITE)", Ex6072MainEndToEndToMig4),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try
    {
        await body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st)
        {
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(12)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════ Test 1: the design-decision crux ═══════════════════════════════

async Task SnapshotTransientEval()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 8101);
    await ReachMainWaitAsync(match);

    HeadlessEntityId omni = Stage(match, P1, "AD1_025", "1:hand:omni", zone: ChoiceZone.Hand, register: true);
    // The temp material lives in HAND (not battle) — that is the whole point of the sparse-injection.
    HeadlessEntityId garuHand = StageSynthetic(match, P1, "MGARU", dp: 6000, level: 6, "1:hand:mgaru", name: "MetalGarurumon", zone: ChoiceZone.Hand);
    HeadlessEntityId greyHand = StageSynthetic(match, P1, "WGREY", dp: 6000, level: 6, "1:hand:wgrey", name: "WarGreymon", zone: ChoiceZone.Hand);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var omniCard = new Cec.CardSource(match.Context, omni, P1);

    var jogress = omniCard.JogressConditionOf();
    AssertTrue(jogress.Count > 0, "AD1_025 exposes a jogress condition (GetJogressConditionClass registered)");

    Cec.JogressConditionElement[] elements = jogress[0].elements;
    AssertEqual(2, elements.Length, "the Omnimon DNA recipe has two material slots");

    // slot[1] = Lv.6 [Garurumon]; slot[0] = Lv.6 [Greymon] — each wrapped in IsPermanentExistsOnOwnerBattleAreaDigimon.
    // WITH the snapshot (the faithful mirror of FieldPermanents[0]=transient): battle-area-resident for the gate.
    var garuSnap = new Cec.Permanent(match.Context, garuHand, P1, ChoiceZone.BattleArea);
    var greySnap = new Cec.Permanent(match.Context, greyHand, P1, ChoiceZone.BattleArea);
    AssertTrue(elements[1].EvoRootCondition(garuSnap),
        "snapshot Garurumon view satisfies slot[1] (field-membership answered from the snapshot; TopCard = the hand card)");
    AssertTrue(elements[0].EvoRootCondition(greySnap),
        "snapshot Greymon view satisfies slot[0]");
    AssertTrue(!elements[0].EvoRootCondition(garuSnap),
        "the Garurumon view does NOT satisfy the Greymon slot (TopCard-name predicate still discriminates)");

    // WITHOUT the snapshot: a plain hand-card view reads the LIVE zone → not battle-area-resident → gate fails.
    var garuLive = new Cec.Permanent(match.Context, garuHand, P1);
    AssertTrue(!elements[1].EvoRootCondition(garuLive),
        "without the snapshot the same card FAILS the field-membership-wrapped predicate — the snapshot is load-bearing exactly where the AS-IS slot-write was");
}

// ═══════════════════════════════ Test 2: singular capacity check ═══════════════════════════════

async Task SingularJogressCheck()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 8201);
    await ReachMainWaitAsync(match);

    HeadlessEntityId omni = Stage(match, P1, "AD1_025", "1:hand:omni", zone: ChoiceZone.Hand, register: true);
    // CanPlayJogress needs a full ordered pair on the battle area (both DNA roots present — the state the real
    // flow reaches AFTER the temp material is materialized). Stage both roots.
    HeadlessEntityId grey = StageSynthetic(match, P1, "WGREY", dp: 6000, level: 6, "1:battle:wgrey", name: "WarGreymon");
    HeadlessEntityId garu = StageSynthetic(match, P1, "MGARU", dp: 6000, level: 6, "1:battle:mgaru", name: "MetalGarurumon");
    HeadlessEntityId agu = StageSynthetic(match, P1, "AGU", dp: 3000, level: 3, "1:battle:agu", name: "Agumon");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var omniCard = new Cec.CardSource(match.Context, omni, P1);
    var pGrey = new Cec.Permanent(match.Context, grey, P1);
    var pAgu = new Cec.Permanent(match.Context, agu, P1);

    AssertTrue(omniCard.CanJogressFromTargetPermanent(pGrey, false),
        "AD1_025 + a battle-area Lv.6 Greymon root satisfies the singular jogress-root check (owner battle-area Digimon + CanPlayJogress + a matching element + !CanNotEvolve)");
    AssertTrue(!omniCard.CanJogressFromTargetPermanent(pAgu, false),
        "negative: Agumon Lv.3 matches no jogress element → false");
}

// ═══════════════════════════════ Test 3: the full <Delay> flow to the MIG4 boundary ═══════════════════════════════

async Task DelayEndToEndToMig4()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 8301);
    await ReachMainWaitAsync(match);

    // BT17_095 is its OWN delay permanent (deleted by the ActivateCoroutine wrapper before SuccessProcess runs).
    HeadlessEntityId self = Stage(match, P1, "BT17_095", "1:battle:self", zone: ChoiceZone.BattleArea, register: false);
    // The field root: a Lv.6 [Greymon] flagged willBeRemoveField (IsOwnerPermanentToBeDeletedCondition).
    HeadlessEntityId grey = StageSynthetic(match, P1, "WGREY", dp: 6000, level: 6, "1:battle:wgrey", name: "WarGreymon");
    // The Omnimon DNA target (in hand) + the temp hand material (Lv.6 [Garurumon], in hand).
    HeadlessEntityId omni = Stage(match, P1, "AD1_025", "1:hand:omni", zone: ChoiceZone.Hand, register: true);
    HeadlessEntityId garu = StageSynthetic(match, P1, "MGARU", dp: 6000, level: 6, "1:hand:mgaru", name: "MetalGarurumon", zone: ChoiceZone.Hand);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);

    // ample memory so the jogress cost gate passes end-to-end (the collapse now completes — see below).
    match.Context.MemoryController.Set(10);

    // flag the field Greymon as would-leave-field (the AS-IS delay premise).
    new Cec.Permanent(match.Context, grey, P1).willBeRemoveField = true;

    // Each DNA selection is unambiguous (exactly one SELECTABLE candidate) — pick the first selectable one; the
    // hand also holds drawn BT1_028 deck cards that the canTargetCondition marks non-selectable.
    policy.On(_ => true, req =>
    {
        ChoiceCandidate? sel = req.Candidates.FirstOrDefault(c => c.IsSelectable);
        if (sel is not null)
        {
            return ChoiceResult.Select(sel.Id);
        }

        return req.CanSkip ? ChoiceResult.Skip() : (req.Candidates.Count > 0 ? ChoiceResult.Select(req.Candidates[0].Id) : ChoiceResult.Skip());
    }, oneShot: false);

    var selfCard = new Cec.CardSource(match.Context, self, P1);
    var delay = new BT17_095().CardEffects(Cec.EffectTiming.WhenRemoveField, selfCard)
        .OfType<Cec.ActivateICardEffect>().First();

    // (MIG4-DISCARDEVOROOTS-PUTTOTRASH LANDED — see FRAME-Write.Witness Test 3) the jogress bare-detach leaf is
    // now a live 1:1 port, so the <Delay> SuccessProcess runs the temp-material DNA END-TO-END instead of hitting
    // the old MIG4 STOP: co-eval the hand material → materialize it → jogress-collapse the field root + the
    // materialized material into AD1_025.
    await delay.Activate(new Hashtable());

    List<Cec.Permanent> field = new Cec.Player(match.Context, P1).GetFieldPermanents();
    Cec.Permanent? omniPerm = field.FirstOrDefault(p => p.InstanceId == omni);
    AssertTrue(omniPerm is not null,
        "AD1_025 is the surviving jogress permanent (the field Greymon + the materialized hand Garurumon collapsed into it)");
    IReadOnlyList<Cec.CardSource> omniSources = omniPerm!.DigivolutionCards;
    AssertTrue(omniSources.Any(s => s.InstanceId == grey) && omniSources.Any(s => s.InstanceId == garu),
        "both roots — the field Greymon AND the temp hand-material Garurumon (materialized then consumed) — are stacked underneath AD1_025 (full collapse WRITE; the temp material became a real digivolution source)");
    AssertTrue(field.All(p => p.InstanceId != grey && p.InstanceId != garu),
        "neither root survives as a standalone field permanent (bare-detached into the jogress stack)");
}

// ═══════════════════════════════ Test 4: the rollback half ═══════════════════════════════

async Task RollbackUnmaterialize()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 8401);
    await ReachMainWaitAsync(match);

    // A temp material sitting on the battle area (as if just materialized by CreateNewPermanent).
    HeadlessEntityId garu = StageSynthetic(match, P1, "MGARU", dp: 6000, level: 6, "1:battle:mgaru", name: "MetalGarurumon");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var zones = (IZoneStateReader)match.Context.ZoneMover;
    AssertTrue(zones.GetCards(P1, ChoiceZone.BattleArea).Contains(garu), "precondition: the material is on the battle area");

    var material = new Cec.CardSource(match.Context, garu, P1);
    await Scr.CardObjectController.AddHandCard(material, false);

    AssertTrue(new Cec.Player(match.Context, P1).HandCards.Any(c => c.InstanceId == garu),
        "AddHandCard(material, false) returned the temp material to the owner's hand (the AS-IS rollback un-play)");
    AssertTrue(!zones.GetCards(P1, ChoiceZone.BattleArea).Contains(garu),
        "the material no longer occupies the battle area (it was withdrawn from the field)");
}

// ═══════════════════════════════ Test 5: EX6_072 helper gate (ported co-eval) ═══════════════════════════════

async Task Ex6072HelperGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 8501);
    await ReachMainWaitAsync(match);

    HeadlessEntityId omni = Stage(match, P1, "AD1_025", "1:hand:omni", zone: ChoiceZone.Hand, register: true);
    HeadlessEntityId grey = StageSynthetic(match, P1, "WGREY", dp: 6000, level: 6, "1:battle:wgrey", name: "WarGreymon");
    HeadlessEntityId garuHand = StageSynthetic(match, P1, "MGARU", dp: 6000, level: 6, "1:hand:mgaru", name: "MetalGarurumon", zone: ChoiceZone.Hand);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var omniCard = new Cec.CardSource(match.Context, omni, P1);
    var owner = new Cec.Player(match.Context, P1);

    // hand→hand DNA: one root is a field permanent, the other a hand card (isWithHandCard/isIntoHandCard = true).
    // The recipe co-eval runs PermanentFulfillsRequirement/CardFulfillsRequirement over the SnapshotZone temp view.
    AssertTrue(Cec.CardEffectCommons.CanJogressWithHandOrTrash(omniCard, owner, isWithHandCard: true, isIntoHandCard: true, targetCardCondition: _ => true),
        "AD1_025 in hand + a field Lv.6 Greymon root + a hand Lv.6 Garurumon material satisfies the ported recipe co-eval");

    AssertTrue(!Cec.CardEffectCommons.CanJogressWithHandOrTrash(omniCard, owner, isWithHandCard: true, isIntoHandCard: true, targetCardCondition: _ => false),
        "control: a false targetCardCondition gates the whole helper off");

    // Withdraw the hand material — no hand card fills the remaining root → the recipe cannot be completed.
    await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, garuHand, ChoiceZone.Hand, ChoiceZone.None));
    AssertTrue(!Cec.CardEffectCommons.CanJogressWithHandOrTrash(omniCard, owner, isWithHandCard: true, isIntoHandCard: true, targetCardCondition: _ => true),
        "control: with the hand Garurumon material gone, no hand card fills the second root → the gate is false");
}

// ═══════════════════════════════ Test 6: EX6_072 [Main] full flow to the MIG4 boundary ═══════════════════════════════

async Task Ex6072MainEndToEndToMig4()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 8601);
    await ReachMainWaitAsync(match);

    HeadlessEntityId ex6 = Stage(match, P1, "EX6_072", "1:battle:ex6", zone: ChoiceZone.BattleArea, register: false);
    HeadlessEntityId omni = Stage(match, P1, "AD1_025", "1:hand:omni", zone: ChoiceZone.Hand, register: true);
    HeadlessEntityId grey = StageSynthetic(match, P1, "WGREY", dp: 6000, level: 6, "1:battle:wgrey", name: "WarGreymon");
    HeadlessEntityId garu = StageSynthetic(match, P1, "MGARU", dp: 6000, level: 6, "1:hand:mgaru", name: "MetalGarurumon", zone: ChoiceZone.Hand);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);

    // The jogress pays cost (EX6_072 passes payCost:true) — give the owner ample memory so CanPlayJogress' cost gate passes.
    match.Context.MemoryController.Set(10);

    // Each selection is unambiguous after the canTargetCondition filter — pick the first selectable candidate
    // (the DNA target = AD1_025; the From-Battle-Area bool branch = index 0; the field root; the hand material).
    policy.On(_ => true, req =>
    {
        ChoiceCandidate? sel = req.Candidates.FirstOrDefault(c => c.IsSelectable);
        if (sel is not null)
        {
            return ChoiceResult.Select(sel.Id);
        }

        return req.CanSkip ? ChoiceResult.Skip() : (req.Candidates.Count > 0 ? ChoiceResult.Select(req.Candidates[0].Id) : ChoiceResult.Skip());
    }, oneShot: false);

    var ex6Card = new Cec.CardSource(match.Context, ex6, P1);
    var main = new EX6_072().CardEffects(Cec.EffectTiming.OptionSkill, ex6Card)
        .OfType<Cec.ActivateICardEffect>().First();

    // The raw helper runs the full temp-material DNA flow END-TO-END (MIG4-DISCARDEVOROOTS landed — the jogress
    // collapse completes): DNA-target select → From-Battle-Area branch → field root + temp hand-material co-eval →
    // materialize (CreateNewPermanent) → CanJogressFromTargetPermanents → PlayCardClass.SetJogress → collapse WRITE.
    await main.Activate(new Hashtable());

    List<Cec.Permanent> field = new Cec.Player(match.Context, P1).GetFieldPermanents();
    Cec.Permanent? omniPerm = field.FirstOrDefault(p => p.InstanceId == omni);
    AssertTrue(omniPerm is not null,
        "AD1_025 is the surviving jogress permanent (EX6_072's [Main] collapsed the field Greymon + the materialized hand Garurumon into it)");
    IReadOnlyList<Cec.CardSource> omniSources = omniPerm!.DigivolutionCards;
    AssertTrue(omniSources.Any(s => s.InstanceId == grey) && omniSources.Any(s => s.InstanceId == garu),
        "both roots — the field Greymon AND the temp hand-material Garurumon (materialized via CreateNewPermanent, then consumed) — are stacked underneath AD1_025 (full collapse WRITE through the raw DNADigivolveWithHandOrTrash helper)");
    AssertTrue(field.All(p => p.InstanceId != grey && p.InstanceId != garu),
        "neither root survives as a standalone field permanent (bare-detached into the jogress stack)");
}

// ═══════════════════════════════ assertions ═══════════════════════════════

static void AssertTrue(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException($"Assertion failed: {message}"); }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Assertion failed: {message} (expected {expected}, got {actual})");
    }
}

// ═══════════════════════════════════ harness (FRAME-Write.Witness 정본) ═══════════════════════════════════

PlayerDeckSetup[] MonoDecks() => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        MonoDecks(), firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    await match.InitializeAsync(config);
    return (match, policy);
}

async Task ReachMainWaitAsync(DcgoMatch match)
{
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.BreedingDecision
                || match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else
        {
            await StepOnceAsync(match);
        }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException($"drive did not reach main-wait — phase:{t.Phase}/{t.StepCursor} player:{t.TurnPlayerId}");
    }
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser)
            .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }

    if (action is null)
    {
        throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    }

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

HeadlessEntityId Stage(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, string instanceId, ChoiceZone zone = ChoiceZone.BattleArea, bool register = false)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId(cardNumber);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? existing) || existing is null)
    {
        throw new InvalidOperationException($"definition {cardNumber} not found in the loaded card database");
    }

    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    if (register)
    {
        Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    }

    return id;
}

HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null, string[]? colors = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}:{instanceId}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
    }

    if (colors is { Length: > 0 })
    {
        meta["colors"] = colors;
    }

    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name ?? number, meta, CardType: cardType, PlayCost: null));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level, ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

// ═══════════════════════════════ providers/context (FRAME-Write.Witness 정본) ═══════════════════════════════

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < _handlers.Count; i++)
        {
            (Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot) = _handlers[i];
            if (applies(request))
            {
                ChoiceResult result = answer(request);
                result.ThrowIfInvalid(request);
                if (oneShot)
                {
                    _handlers.RemoveAt(i);
                }

                return Task.FromResult(result);
            }
        }

        return _fallback.ChooseAsync(request, cancellationToken);
    }
}

static class ContextFactory
{
    public static EngineContext CreateWithProvider(IChoiceProvider provider, int randomSeed)
    {
        var randomSource = new GameRandomSource(randomSeed);
        var cardInstanceRepository = new InMemoryCardInstanceRepository();
        var logSink = new NullLogSink();
        var zoneMover = new InMemoryZoneMover(randomSource);
        var memoryController = new InMemoryHeadlessMemoryController();
        var gameEventQueue = new GameEventQueue();
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, gameEventQueue,
                    currentTurnPlayer: () => selfRef?.TurnController.Current.TurnPlayerId,
                    context: selfRef),
                strictUnbound: false));

        var choiceController = new InMemoryHeadlessChoiceController();
        var context = new EngineContext(
            provider,
            randomSource,
            new CardDatabase(),
            cardInstanceRepository,
            zoneMover,
            new InMemoryRuleQueryService(),
            new InMemoryHeadlessTurnController(),
            choiceController,
            new InMemoryHeadlessAttackController(),
            memoryController,
            logSink,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(),
            effectScheduler,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
