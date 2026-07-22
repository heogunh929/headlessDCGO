// R2 (C+D 2nd adversarial review) — deletion-pipeline cluster remediation witnesses.
//
// R2-P1-1: AS-IS DestroyPermanentsClass.Destroy() processes the WHOLE target list through ONE sequence
//   (willBeRemoveField on ALL → one PRE cut-in over the LIST → fix survivors → trash loop), so when card A of
//   a 2-card delete has a PRE option (Evade), card B is NOT trashed until A's decision resolved. The former
//   per-card defer split one Destroy() into two drains (B's [On Deletion] before A's decision + a double
//   reactor fire). Now: any PRE option defers the WHOLE sink batch; the sweep finalizes batch-mates together.
// R2-P1-2: the security-battle deletion finisher stamps the delete-BATCH id like the other three finishers
//   (AS-IS: same IBattle → DestroyPermanentsClass.Destroy path, CardController.cs:4179→4705).
// R2-P1-4: OnDestroyedAnyone/OnLeaveFieldAnyone derive ONLY from a deletion-marked (or genuine-departure)
//   move — a top-swap trash (Armor Purge: AS-IS ArmorPurge.cs:63 willBeRemoveField=false, only
//   WhenTopCardTrashed) no longer misfires the deletion reactors.
// R2-P1-3: the AS-IS "record parameters just before deletion" block (CardController.cs:3762-3783) is
//   snapshotted at the same seam as the keyword snapshot (DP/Level/Cost/Names/Traits/permanent identity).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("P1-1: 2-card delete where only A has Evade parks BOTH (B waits); decline finalizes both in ONE batch (reactor x1)", BatchDeferHoldsOptionlessMate),
    ("P1-1: 2-card delete, Evade ACTIVATED — A survives, B alone finalizes (reactor x1)", BatchDeferSavedMate),
    ("P1-1 regression: an option-less 2-card delete moves immediately at flush (reactor x1)", OptionlessBatchImmediate),
    ("P1-2: the security-battle deletion move carries a delete-batch id and fires the leave reactor", SecurityBattleDeletionStampsBatch),
    ("P1-4: Armor Purge survival (top-swap trash) fires NO deletion/leave reactor", ArmorPurgeTopSwapNoDeletionFire),
    ("P1-4 unit: field→Trash derives OnDeletion/OnLeaveField only with the deletion marker", TimingMapDeletionMarkerUnit),
    ("P1-3: record-parameters snapshot (DP/Level/Cost/Names/Traits/permanent id) at deletion", RecordParametersSnapshot),
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

// --- R2-P1-1 --------------------------------------------------------------

async Task BatchDeferHoldsOptionlessMate()
{
    (DcgoMatch match, HeadlessEntityId a, HeadlessEntityId b) = await SetupTwoCardDelete(aHasEvade: true);

    // The window is open for A; B (no option) must WAIT parked — not be trashed in a separate drain.
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE window open for A");
    AssertEqual(ChoiceType.OptionalEffect, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, b), "B parked on the field while A decides (batch-atomic defer)");
    AssertTrue(ReadFlag(match, b, GameFlowProcessor.PendingDeletionKey), "B pendingDeletion");
    AssertEqual(0, match.Context.MemoryController.Current.Current, "no reactor fired before the batch finalized");

    LegalAction decline = ResolveActions(match, P2).Single(x => x.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(decline);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, a), "A deleted after decline");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, b), "B deleted with A (same pass)");
    // ONE batch id on both finalize moves => the (reactor, batch-id) collapse fires the uncapped
    // OnLeaveFieldAnyone reactor ONCE. The pre-fix split drains fired it twice (-2).
    AssertEqual(-1, match.Context.MemoryController.Current.Current, "leave reactor fired ONCE for the 2-card batch");
}

async Task BatchDeferSavedMate()
{
    (DcgoMatch match, HeadlessEntityId a, HeadlessEntityId b) = await SetupTwoCardDelete(aHasEvade: true);

    LegalAction activate = AcceptWindow(match, P2, a);
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, a), "A survives via the accepted replacement");
    AssertFalse(ReadFlag(match, a, GameFlowProcessor.PendingDeletionKey), "A's pendingDeletion cleared (survived)");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, b), "B still deleted (its own deletion was never replaced)");
    AssertEqual(-1, match.Context.MemoryController.Current.Current, "leave reactor fired ONCE (B only)");
}

async Task OptionlessBatchImmediate()
{
    (DcgoMatch match, HeadlessEntityId a, HeadlessEntityId b) = await SetupTwoCardDelete(aHasEvade: false, step: false);

    // No PRE option anywhere in the batch => the immediate path: both moved at FLUSH, before RunToStable.
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, a), "A trashed at flush (immediate path unchanged)");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, b), "B trashed at flush (immediate path unchanged)");

    await match.StepAsync();
    AssertEqual(-1, match.Context.MemoryController.Current.Current, "leave reactor fired ONCE for the one batch");
}

// --- R2-P1-2 --------------------------------------------------------------

async Task SecurityBattleDeletionStampsBatch()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 41);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.MemoryController.Set(0);
    HeadlessEntityId reactor = await AddLeaveReactor(ctx);
    HeadlessEntityId attacker = await AddFieldDigimon(ctx, P1, "ATK", dp: 3000);

    // One P2 security Digimon with 9000 DP: the attacker loses the security battle and is deleted.
    var cards = (CardDatabase)ctx.CardRepository;
    var secDef = new HeadlessEntityId("DEF:SEC");
    cards.Upsert(new CardRecord(secDef, "SEC", "SEC", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var sec = new HeadlessEntityId("2:security:SEC");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(sec, secDef, P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 9000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, sec, ChoiceZone.None, ChoiceZone.Security));

    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    SecurityResolutionResult result = await new SecurityResolver().ResolveAsync(ctx);
    AssertTrue(result.IsSuccess, "security check resolved");
    AssertTrue(((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.Trash).Contains(attacker), "attacker deleted by the security battle");

    // The finisher's field→Trash move must carry the delete-batch id (like sink/battle/sweep).
    GameEvent move = ctx.ZoneMover.Events.Last(e =>
        e.Type == GameEventType.CardMoved && Equals(e.Subject, attacker) && e.ZoneTo == ChoiceZone.Trash);
    AssertTrue(move.Metadata.TryGetValue(MatchStateMutationSink.DeletionBatchIdKey, out object? raw) && raw is long id && id != 0,
        "security-battle deletion move stamped with a non-zero delete-batch id");

    // (B6-Db item 3 — RD-R4B6-P1-2 REAL GAP, marking preserved) The batch-id is stamped (assertion above
    // passes), but the security-battle finisher's departure does NOT feed RunToStable's OnLeaveFieldAnyone
    // collection the way the sink/field-battle finishers do — so the uncapped leave reactor stays silent.
    // Fixing the security-check drain's trigger dispatch to parity is beyond a small re-pin (needs AS-IS
    // trigger-queue analysis of the SecurityResolver finisher vs BattleResolver); left marked, not repaired.
    await new GameFlowProcessor().RunToStableAsync(ctx);
    AssertEqual(-1, ctx.MemoryController.Current.Current, "leave reactor fired once for the security-battle deletion");
    _ = reactor;
}

// --- R2-P1-4 --------------------------------------------------------------

async Task ArmorPurgeTopSwapNoDeletionFire()
{
    // A P2 Digimon with <Armor Purge> and one digivolution source; P1 fields the uncapped leave reactor.
    (DcgoMatch match, HeadlessEntityId a, HeadlessEntityId source) = await SetupArmorPurgeDelete();

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE window open (Armor Purge offer)");
    AssertEqual(ChoiceType.OptionalEffect, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    LegalAction activate = AcceptWindow(match, P2, a);
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, a), "the top card was trashed by Armor Purge");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, source), "the under-source was promoted (permanent survives)");
    // The top-swap trash is NOT a deletion/departure: no OnDeletion, no OnLeaveFieldAnyone. Pre-fix the raw
    // field→Trash move derived both and the reactor misfired (-1).
    AssertEqual(0, match.Context.MemoryController.Current.Current, "no deletion/leave reactor fired on the top-swap");
}

Task TimingMapDeletionMarkerUnit()
{
    static GameEvent Move(ChoiceZone from, ChoiceZone to, bool stamped)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (stamped)
        {
            metadata[MatchStateMutationSink.DeletionBatchIdKey] = 7L;
        }

        return new GameEvent(1, GameEventType.CardMoved, "move", metadata) { ZoneFrom = from, ZoneTo = to };
    }

    IReadOnlyList<string> unstamped = TriggerTimingMap.Derive(Move(ChoiceZone.BattleArea, ChoiceZone.Trash, stamped: false));
    AssertFalse(unstamped.Contains(TriggerTimings.OnDeletion), "unstamped field→Trash does NOT derive OnDeletion");
    AssertFalse(unstamped.Contains(TriggerTimings.OnLeaveField), "unstamped field→Trash does NOT derive OnLeaveField");
    AssertFalse(unstamped.Contains(TriggerTimings.WhenRemoveField), "unstamped field→Trash does NOT derive WhenRemoveField");

    IReadOnlyList<string> stamped = TriggerTimingMap.Derive(Move(ChoiceZone.BattleArea, ChoiceZone.Trash, stamped: true));
    AssertTrue(stamped.Contains(TriggerTimings.OnDeletion), "stamped field→Trash derives OnDeletion");
    AssertTrue(stamped.Contains(TriggerTimings.OnLeaveField), "stamped field→Trash derives OnLeaveField");

    IReadOnlyList<string> bounce = TriggerTimingMap.Derive(Move(ChoiceZone.BattleArea, ChoiceZone.Hand, stamped: false));
    AssertTrue(bounce.Contains(TriggerTimings.OnLeaveField), "a hand bounce still derives OnLeaveField without a stamp");
    AssertFalse(bounce.Contains(TriggerTimings.OnDeletion), "a hand bounce is not a deletion");

    IReadOnlyList<string> tokenRemove = TriggerTimingMap.Derive(Move(ChoiceZone.BattleArea, ChoiceZone.None, stamped: false));
    AssertFalse(tokenRemove.Contains(TriggerTimings.OnLeaveField), "a field→None removal (token top-swap) derives no leave timing");
    return Task.CompletedTask;
}

// --- R2-P1-3 --------------------------------------------------------------

async Task RecordParametersSnapshot()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 41);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (수리-2 repair) the continuous DP fold (Permanent.DP) only fires past game-start (ICardEffect.CanTrigger
    // gates on DoneStartGame — same fix documented by FAILa-02); set the phase so the +1000 modifier folds.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);

    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId("DEF:AGU");
    cards.Upsert(new CardRecord(def, "BT1-XXX", "Agumon",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["level"] = 4,
            ["traits"] = new[] { "Reptile" },
        },
        CardType: "Digimon", PlayCost: 3));

    var top = new HeadlessEntityId("2:battle:AGU");
    var source = new HeadlessEntityId("2:src:S1");
    var srcDef = new HeadlessEntityId("DEF:SRC");
    cards.Upsert(new CardRecord(srcDef, "SRC", "SRC", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(source, srcDef, P2, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(top, def, P2,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dp"] = 5000,
            ["sourceIds"] = new[] { source.Value },
        }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, top, ChoiceZone.None, ChoiceZone.BattleArea));
    // (수리-2 repair) The old BattleResolver.DpModifiersKey metadata array is not folded by Permanent.DP (the
    // snapshot seat, CardLeavePlayCleanup.cs:198-200, reads Permanent.DP). Express the +1000 as a live
    // ChangeDPClass (IChangeDPEffect) via the AS-IS ChangeDigimonDP, which Permanent.DP folds → effective 6000.
    CardEffectCommons.ChangeDigimonDP(
        new Permanent(ctx, top, P2), 1000, EffectDuration.UntilEachTurnEnd, new CardSource(ctx, source, P2));

    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, log: null, ctx.ZoneMover, ctx.MemoryController,
        ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("effect:deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = top.Value }));
    await sink.FlushAsync();

    AssertTrue(ctx.CardInstanceRepository.TryGetInstance(top, out CardInstanceRecord? dead) && dead is not null, "dead card readable");
    AssertEqual(6000, CardLeavePlayCleanup.DpJustBeforeRemoveField(dead!.Metadata), "recorded DP = effective 6000 (base 5000 + modifier)");
    AssertEqual(4, CardLeavePlayCleanup.LevelJustBeforeRemoveField(dead.Metadata), "recorded level = 4");
    AssertEqual(3, CardLeavePlayCleanup.CostJustBeforeRemoveField(dead.Metadata), "recorded cost = printed play cost 3");
    AssertTrue(CardLeavePlayCleanup.CardNamesJustBeforeRemoveField(dead.Metadata).Contains("Agumon"), "recorded names contain the live name");
    AssertTrue(CardLeavePlayCleanup.CardTraitsJustBeforeRemoveField(dead.Metadata).Contains("Reptile"), "recorded traits contain the live trait");
    AssertEqual(top, CardLeavePlayCleanup.PermanentJustBeforeRemoveField(dead.Metadata), "the top carries its permanent identity");

    AssertTrue(ctx.CardInstanceRepository.TryGetInstance(source, out CardInstanceRecord? src) && src is not null, "source readable");
    AssertEqual(top, CardLeavePlayCleanup.PermanentJustBeforeRemoveField(src!.Metadata),
        "the digivolution source carries the SAME permanent identity (AS-IS per-cardSource stamp)");

    // A card never recorded reads the AS-IS defaults.
    AssertEqual(-1, CardLeavePlayCleanup.DpJustBeforeRemoveField(src.Metadata), "unrecorded DP default -1");
    AssertEqual(-1, CardLeavePlayCleanup.LevelJustBeforeRemoveField(src.Metadata), "unrecorded level default -1");
}

// --- Harness ---------------------------------------------------------------

// Two P2 field Digimon deleted in ONE sink flush (one batch); P1 fields the uncapped OnLeaveFieldAnyone
// reactor (-1 memory per fire, no cap) so a split batch shows -2 and a collapsed one -1.
async Task<(DcgoMatch Match, HeadlessEntityId A, HeadlessEntityId B)> SetupTwoCardDelete(bool aHasEvade, bool step = true)
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext ctx = match.Context;

    HeadlessEntityId a = HandCard(match, P2, 1);
    HeadlessEntityId b = HandCard(match, P2, 2);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, a, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, b, ChoiceZone.Hand, ChoiceZone.BattleArea));
    // (B6-Db item 3 re-pin — 수리-2 C5 판례) The retired HasEvadeKey metadata gate-key is replaced by the
    // current-model canon: a card-registered OPTIONAL [WhenPermanentWouldBeDeleted] survival replacement
    // (TfxWouldBeDeletedInteractive — the window form of the "you may" Evade keyword), surfaced through
    // RegisterCard, NOT a metadata flag. The batch-atomic DEFER / single-reactor-collapse rule assertions are
    // preserved; the invented DeletionReplacement gate type + '#evade' id + Evade suspend-cost are dropped.
    SetMetadata(match, a, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.IsSuspendedKey] = false, ["dp"] = 4000 });
    if (aHasEvade)
    {
        GiveWouldBeDeleted(ctx, a, P2, "TfxWouldBeDeletedInteractive");
    }

    ctx.MemoryController.Set(0);

    // ONE effect resolution (one sink with the context => one real batch id) deletes BOTH cards. The card-
    // registered [WhenPermanentWouldBeDeleted] discovery (TfxWouldBeDeletedInteractive.CanUseCondition reads
    // CardEffectCommons) requires the ambient match scope (C-Del-3C1 substrate pattern).
    using (AmbientMatchContext.Enter(ctx))
    {
        var sink = new MatchStateMutationSink(
            ctx.CardInstanceRepository, log: null, ctx.ZoneMover, ctx.MemoryController,
            ctx.GameEventQueue, context: ctx);
        foreach (HeadlessEntityId target in new[] { a, b })
        {
            sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("effect:deleter"),
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
        }

        await sink.FlushAsync();
    }

    if (step)
    {
        await match.StepAsync();
    }

    return (match, a, b);
}

async Task<(DcgoMatch Match, HeadlessEntityId A, HeadlessEntityId Source)> SetupArmorPurgeDelete()
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext ctx = match.Context;

    HeadlessEntityId a = HandCard(match, P2, 1);
    HeadlessEntityId source = HandCard(match, P2, 2);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, a, ChoiceZone.Hand, ChoiceZone.BattleArea));
    // The source sits under A (out of every zone list, referenced by sourceIds).
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, source, ChoiceZone.Hand, ChoiceZone.Trash));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, source, ChoiceZone.Trash, ChoiceZone.None));
    // (B6-Db item 3 re-pin — 수리-2 C5 판례) The retired HasArmorPurgeKey metadata gate-key is replaced by the
    // current-model canon: a card-registered OPTIONAL [WhenPermanentWouldBeDeleted] Armor-Purge top-swap
    // replacement (TfxArmorPurgeWouldBeDeleted composing the real DeDigivolveHelpers.ArmorPurgeTopAsync),
    // surfaced through RegisterCard. The invented '#armorpurge' gate id is dropped; the top-swap / no-departure-
    // reactor rule assertions are preserved.
    SetMetadata(match, a, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.IsSuspendedKey] = false,
        ["dp"] = 4000,
        ["sourceIds"] = new[] { source.Value },
    });
    GiveWouldBeDeleted(ctx, a, P2, "TfxArmorPurgeWouldBeDeleted");
    ctx.MemoryController.Set(0);

    using (AmbientMatchContext.Enter(ctx))
    {
        var sink = new MatchStateMutationSink(
            ctx.CardInstanceRepository, log: null, ctx.ZoneMover, ctx.MemoryController,
            ctx.GameEventQueue, context: ctx);
        sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("effect:deleter"),
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = a.Value }));
        await sink.FlushAsync();
    }

    await match.StepAsync();
    return (match, a, source);
}

async Task<DcgoMatch> CreateMatchAsync()
{
    // deferredChoice: the interactive [WhenPermanentWouldBeDeleted] PRE cut-in surfaces its "will you use it?"
    // pause through the deferred-choice provider (C-Del-3C1 substrate pattern).
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73, deferredChoice: true);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    await AddLeaveReactor(context);
    return match;
}

// The uncapped [All Turns] OnLeaveFieldAnyone reactor (-1 memory per fire, NO cap) — the D-1/D-2 witness
// fixture: a batch of N simultaneous leaves costs -1 iff the collapse fired once, -N on a split/over-fire.
async Task<HeadlessEntityId> AddLeaveReactor(EngineContext ctx)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TfxOnLeaveFieldCounter"), "TfxOnLeaveFieldCounter", "LeaveCounter",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var reactor = new HeadlessEntityId("1:battle:LEAVECTR");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(reactor, new HeadlessEntityId("TfxOnLeaveFieldCounter"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, reactor, ChoiceZone.None, ChoiceZone.BattleArea));
    ctx.RegisterEnteredCardEffects(reactor, P1);
    return reactor;
}

async Task<HeadlessEntityId> AddFieldDigimon(EngineContext ctx, HeadlessPlayerId owner, string tag, int dp)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

IEnumerable<LegalAction> ResolveActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(x => x.ActionType == HeadlessActionTypes.ResolveChoice);

// (B6-Db item 3 — 수리-2 C5 판례) Give a card a card-registered [WhenPermanentWouldBeDeleted] survival
// replacement — the current-model canon for the retired HasEvadeKey metadata gate-key. The effect surfaces
// through RegisterCard (dispatch-discoverable TestFixtures ActivateClass), NOT through a metadata flag; the
// instance metadata (IsSuspended etc.) is preserved via the def-only retype.
void GiveWouldBeDeleted(EngineContext context, HeadlessEntityId card, HeadlessPlayerId owner, string tfxNumber)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId("def:" + tfxNumber);
    cards.Upsert(new CardRecord(defId, tfxNumber, tfxNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = 4 }, CardType: "Digimon"));
    if (context.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? record) && record is not null)
    {
        context.CardInstanceRepository.Upsert(record with { DefinitionId = defId });
    }

    CardEffectRegistrar.RegisterCard(context, card, owner);
}

// (B6-Db item 3 — 수리-2 C5 판례) Accept the OptionalEffect PRE would-be-deleted window: the non-skip
// candidate keyed by the replacement holder's own instance id (the Candidates[0].Id convention the green
// sibling C-Del-3C1C resolves against) — replaces the torn-down invented "#<keyword>" gate ids.
LegalAction AcceptWindow(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId holder) =>
    ResolveActions(match, player).Single(a =>
        a.Id.Value.Contains(holder.Value, StringComparison.Ordinal)
        && !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    if (hand.Length < index) throw new InvalidOperationException($"hand short: {hand.Length} < {index}");
    return hand[index - 1];
}

// (B6-Dc currency-drain) Reach Main via the pump's natural Active->Draw->Breeding->Main auto-flow; the OLD
// AdvancePhase step currency is retired. The deletion pipeline / would-be-deleted PRE window require the match
// past the early phases at Main — observed Main arrival is asserted (assertion strength unchanged).
async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, player));

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
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
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingAsync(DcgoMatch match, bool skip)
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
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing {cardId}.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}
