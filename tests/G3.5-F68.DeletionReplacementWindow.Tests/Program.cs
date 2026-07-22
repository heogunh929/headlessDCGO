// F-6.8 — re-entrant would-be-deleted replacement window (AS-IS optionality restored).
// (C-Del 3c-2b) The invented DeletionReplacementGate firing-half is RETIRED: the PRE replacement keywords fire
// ONLY through the AS-IS PRE cut-in window (WhenPermanentWouldBeDeleted → WhenRemoveField; the universal
// effect-delete sink / BattleResolver opens it, GetSkillInfos collects the printed ActivateClass,
// TriggeredSkillProcess resolves; an interactive optional promotes-to-defer and parks — 3c-1). This suite drives
// the REAL ported keyword cards (BT13_023 <Evade> / EX8_061 <Scapegoat> / EX8_051 <Fragment <3>> /
// BT14_035 <Barrier>) and the real-factory-shape fixtures (TfxDecoy / TfxArmorPurge — no real card ported yet)
// through that window. An OPTIONAL replacement surfaces as the window's OptionalEffect choice ("Will you use …?",
// activate = the non-skip resolve); activate → pay cost + survive; decline → the state-based sweep finishes the
// deletion. (Mirrors the AS-IS "you may".) Ascension (C-Del 3c-3) and Save (3a) both now fire via the
// OnDestroyedAnyone window — the invented POST gate options are retired (bare-marker no-op cases here; live
// window firing witnessed in tests/C-Del-POST).
//
// The Retaliation→Evade chain case was QUARANTINED to tests/G3.5-F68R.RetaliationEvadeChain.Tests
// (authorized-red: blocked by RD-CBTL-01 — battle Retaliation firing needs the live IBattle).
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Effect deletion of an Evade Digimon opens a replacement choice (not auto-applied)", DeferOpensChoice),
    ("Activating Evade in the window suspends it and cancels the deletion", ActivateEvadeSurvives),
    ("Declining the window lets the deletion finish (swept to trash)", DeclineGetsDeleted),
    ("Ascension (C-Del 3c-3 RETIRED): a bare hasAscension marker opens NO POST option (fires via the OnDestroyedAnyone window)", AscensionGateRetired),
    ("Scapegoat is a two-step choice: activate, then pick which ally to sacrifice", ScapegoatTwoStep),
    ("Fragment is a two-step choice: activate, then pick which sources to trash", FragmentTwoStep),
    ("Decoy: the Decoy holder deletes itself to save the marked ally (live-cardEffect Destroy path)", DecoySavesAlly),
    ("(B1) Armor Purge is a WOULD-BE-DELETED replacement: top trashed, permanent survives", ArmorPurgePostChoice),
    ("Save (C-Del 3a RETIRED): a bare hasSave marker opens NO POST option (fires via the OnDestroyedAnyone window)", SaveGateRetired),
    ("Battle: activating Barrier in the window trashes a security and survives", BarrierBattleChoice),
    ("Battle: activating Evade in the window suspends and survives", EvadeBattleChoice),
    ("(P6) the sacrificed ally EVADES -> the sacrifice fails and the holder dies", () => ScapegoatAllyEvade(allyEvades: true)),
    ("(P6) the sacrificed ally declines its Evade -> it dies and the holder is spared", () => ScapegoatAllyEvade(allyEvades: false)),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Evade (real BT13_023, effect deletion via the sink window) -----------

async Task DeferOpensChoice()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId card = await PlaceRealCard(match, ctx, P2, "BT13_023");

    await DeleteByEffect(match, ctx, card);

    AssertTrue(ReadFlag(match, card, GameFlowProcessor.PendingDeletionKey), "deletion deferred (pendingDeletion, promote-to-defer)");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, card), "card not yet trashed");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a replacement choice is open");
    AssertEqual(P2, match.Context.ChoiceController.PendingRequest!.PlayerId, "owner decides");
    AssertTrue(ResolveActions(match, P2).Any(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal)), "the optional decline is offered (you MAY)");
}

async Task ActivateEvadeSurvives()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId card = await PlaceRealCard(match, ctx, P2, "BT13_023");

    await DeleteByEffect(match, ctx, card);

    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, card), "evade cancels the deletion");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, card), "card not trashed");
    AssertTrue(ReadFlag(match, card, DeletionReplacementGate.IsSuspendedKey), "suspended as the cost (AS-IS EvadeProcess)");
    AssertFalse(ReadFlag(match, card, GameFlowProcessor.PendingDeletionKey), "pendingDeletion cleared");
    AssertFalse(ReadFlag(match, card, "willBeRemoveField"), "willBeRemoveField reset for the spared survivor");
}

async Task DeclineGetsDeleted()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId card = await PlaceRealCard(match, ctx, P2, "BT13_023");

    await DeleteByEffect(match, ctx, card);

    LegalAction decline = ResolveActions(match, P2).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(decline);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, card), "declining lets the deletion finish");
    AssertFalse(InZone(match, P2, ChoiceZone.BattleArea, card), "card left the field");
    AssertFalse(ReadFlag(match, card, DeletionReplacementGate.IsSuspendedKey), "not suspended (Evade cost not paid)");
}

// --- Ascension (C-Del 3c-3 RETIRED; fires via the OnDestroyedAnyone window) -------

async Task AscensionGateRetired()
{
    // (C-Del 3c-3 RETIRED) The invented Ascension POST option (DeletionReplacementTiming AscensionOption ->
    // DeletionReplacementGate.TryAscensionAsync) is retired: AS-IS [Ascension] now fires through the
    // OnDestroyedAnyone cut-in window from a printed AscensionSelfEffect, now that the deletion paths CHARGE the
    // PermanentJustBeforeRemoveField store so CanActivateOnDeletion is satisfied (RD-P6C3-A3 resolved; witnessed
    // live in tests/C-Del-POST). A BARE hasAscension metadata marker — no printed effect — no longer surfaces any
    // POST agent choice, and the deleted card simply stays in the trash (single-fire: window only).
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete((DeletionReplacementGate.HasAscensionKey, true));

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, card), "ascension card was deleted to the trash");
    AssertFalse(ResolveActions(match, P2).Any(a => a.Id.Value.Contains("#ascension", StringComparison.Ordinal)),
        "the retired Ascension POST option surfaces NO agent choice (single-fire: window only)");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, card), "the deleted card stays in the trash (bare marker, no printed Ascension effect)");
    AssertFalse(InZone(match, P2, ChoiceZone.Security, card), "the bare marker was NOT placed in security by the retired gate");
}

// --- Scapegoat (real EX8_061, two-step sub-selection) ---------------------

async Task ScapegoatTwoStep()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = await PlaceRealCard(match, ctx, P2, "EX8_061");
    // TWO eligible allies -> the AS-IS ScapegoatProcess SelectPermanentEffect is a real interactive pick
    // (a single candidate is auto-taken, AS-IS single-candidate mandatory select).
    HeadlessEntityId ally = await PlacePlain(match, ctx, P2, 2);
    HeadlessEntityId ally2 = await PlacePlain(match, ctx, P2, 3);

    await DeleteByEffect(match, ctx, holder);

    // Step 1: activate Scapegoat (the window's OptionalEffect, non-skip resolve).
    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step 2 opens (pick the ally)

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "step-2 target choice is open");

    // Step 2: pick WHICH ally to sacrifice (AS-IS ScapegoatProcess SelectPermanentEffect).
    LegalAction pickAlly = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(ally.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pickAlly);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, holder), "scapegoat holder survives");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, holder), "holder not trashed");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, ally), "the chosen ally is sacrificed");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, ally2), "the unchosen ally stays");
}

// (P6) AS-IS ScapegoatProcess resolves the sacrifice through DeletePermanent — the ally's OWN
// would-be-deleted replacement (its printed Evade) can fire; the holder is spared only when the ally actually died
// (successProcess).
async Task ScapegoatAllyEvade(bool allyEvades)
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId holder = await PlaceRealCard(match, ctx, P2, "EX8_061");
    HeadlessEntityId ally = await PlaceRealCard(match, ctx, P2, "BT13_023", handIndex: 2);

    await DeleteByEffect(match, ctx, holder);

    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();
    // The single eligible ally is auto-taken by the mandatory pick (AS-IS single-candidate select);
    // the ALLY's own would-be-deleted (printed Evade) window is now open.
    await match.StepAsync();
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "the sacrificed ally's own Evade window opened");
    if (allyEvades)
    {
        LegalAction evade = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
        await match.ApplyActionAsync(evade);
        await match.StepAsync();

        AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, ally), "the ally EVADED the sacrifice (suspends, survives)");
        AssertTrue(ReadFlag(match, ally, DeletionReplacementGate.IsSuspendedKey), "the ally suspended as the Evade cost");
        AssertTrue(InZone(match, P2, ChoiceZone.Trash, holder), "the sacrifice failed -> the holder's deletion proceeds");
    }
    else
    {
        LegalAction decline = ResolveActions(match, P2).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
        await match.ApplyActionAsync(decline);
        await match.StepAsync();

        AssertTrue(InZone(match, P2, ChoiceZone.Trash, ally), "the ally declined and was sacrificed");
        AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, holder), "the holder is spared (AS-IS successProcess)");
    }
}

// --- Fragment (real EX8_051 <Fragment <3>>, two-step source trash) --------

async Task FragmentTwoStep()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId top = await PlaceRealCard(match, ctx, P2, "EX8_051");
    // Fragment <3>: CanActivateFragment requires >= 3 digivolution sources; the process trashes exactly 3.
    HeadlessEntityId s1 = MakeSource(ctx, P2, "f68-fs1");
    HeadlessEntityId s2 = MakeSource(ctx, P2, "f68-fs2");
    HeadlessEntityId s3 = MakeSource(ctx, P2, "f68-fs3");
    HeadlessEntityId s4 = MakeSource(ctx, P2, "f68-fs4");
    SetMetadata(match, top, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.SourceIdsKey] = new[] { s1.Value, s2.Value, s3.Value, s4.Value }
    });

    await DeleteByEffect(match, ctx, top);

    // Step 1: activate Fragment.
    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "step-2 source choice is open");

    // Step 2: pick 3 of the 4 sources (AS-IS FragmentProcess SelectCardEffect, maxCount 3, canEndNotMax false —
    // a MULTI-select Card request, min=max=3, resolved as ONE 3-id answer through the window seam).
    ChoiceRequest sourcePick = match.Context.ChoiceController.PendingRequest!;
    AssertEqual(3, sourcePick.MinCount, "Fragment <3> requires exactly 3 sources (min)");
    AssertEqual(3, sourcePick.MaxCount, "Fragment <3> requires exactly 3 sources (max)");
    HeadlessEntityId[] picks = new[] { s1, s2, s3 }
        .Select(id => sourcePick.Candidates.Single(c => c.Id.Value.Contains(id.Value, StringComparison.Ordinal)).Id)
        .ToArray();
    match.Context.ChoiceController.ResolveChoice(ChoiceResult.Select(picks));
    using (AmbientMatchContext.Enter(match.Context))
    {
        try { await AutoProcessing.ForCutIn(match.Context).ResumeSuspendedWindowsAsync(); }
        catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { }
    }

    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, top), "fragment top survives");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, top), "top not trashed");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, s1) && InZone(match, P2, ChoiceZone.Trash, s2) && InZone(match, P2, ChoiceZone.Trash, s3),
        "the 3 chosen sources are trashed as the cost");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, s4), "the unchosen source stays");
    AssertTrue(SourceIds(match, top).Contains(s4.Value), "the unchosen source stays attached");
}

// --- Decoy (TfxDecoy — real DecoySelfEffect factory shape) ----------------

// AS-IS Decoy: when one of the owner's OTHER Digimon would be deleted by an OPPONENT'S EFFECT, the Decoy holder
// may delete ITSELF to prevent that deletion. The IsByEffect gate needs the LIVE causing cardEffect (design item
// RD-3C2B-02: the universal sink threads none), so this drives the deletion through the faithful mirror
// DestroyPermanentsClass — the AS-IS card path (BT9_081 idiom: new DestroyPermanentsClass(targets,
// CardEffectHashtable(activateClass)).Destroy()) that threads the causing ActivateClass onto the PRE cut-in.
async Task DecoySavesAlly()
{
    EngineContext ctx = NewBareContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    HeadlessEntityId target = await PlaceBare(ctx, P2, "PLAIN", "2:battle:Target");
    HeadlessEntityId decoy = await PlaceBare(ctx, P2, "TfxDecoy", "2:battle:Decoy");
    HeadlessEntityId foe = await PlaceBare(ctx, P1, "FOE", "1:battle:Foe");
    CardEffectRegistrar.RegisterCard(ctx, decoy, P2);

    // The deleting card's live effect (the AS-IS Destroy() cardEffect payload): a real ActivateClass whose
    // EffectSourceCard is the OPPONENT's card — Decoy's IsByEffect(enemy-owner) gate evaluates it for real.
    var foeCard = new CardSource(ctx, foe, P1);
    var deleterEffect = new ActivateClass();
    deleterEffect.SetUpICardEffect("Tfx deleter", (System.Collections.Hashtable _) => true, foeCard);

    var targetPerm = new Permanent(ctx, target, P2);
    try
    {
        await new DestroyPermanentsClass(
            new List<Permanent> { targetPerm },
            CardEffectCommons.CardEffectHashtable(deleterEffect)).Destroy();
    }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException)
    {
        // The optional Decoy window parked — resolved below through the ForCutIn pool.
    }

    // Step 1: "Will you use Decoy?" — activate.
    AssertTrue(ctx.ChoiceController.Current.IsPending, "the Decoy optional window is open");
    ChoiceRequest optional = ctx.ChoiceController.PendingRequest!;
    AssertEqual(P2, optional.PlayerId, "the Decoy holder's owner decides");
    await ResolveWindowChoice(ctx, ChoiceResult.Select(optional.Candidates[0].Id));

    // Step 2 (if the single marked-ally pick is surfaced rather than auto-taken): pick the protected target.
    if (ctx.ChoiceController.Current.IsPending)
    {
        ChoiceRequest pick = ctx.ChoiceController.PendingRequest!;
        ChoiceCandidate targetCandidate = pick.Candidates.Single(c => c.Id.Value.Contains(target.Value, StringComparison.Ordinal));
        await ResolveWindowChoice(ctx, ChoiceResult.Select(targetCandidate.Id));
    }

    AssertTrue(InZoneBare(ctx, P2, ChoiceZone.BattleArea, target), "the protected target survives (deletion prevented)");
    AssertFalse(InZoneBare(ctx, P2, ChoiceZone.Trash, target), "target not trashed");
    AssertTrue(InZoneBare(ctx, P2, ChoiceZone.Trash, decoy), "the Decoy holder deleted itself as the cost");
    AssertFalse(ReadFlagBare(ctx, target, "willBeRemoveField"), "the saved ally's willBeRemoveField was cleared");
}

// --- Armor Purge (TfxArmorPurge — real ArmorPurgeEffect factory shape) ----

async Task ArmorPurgePostChoice()
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();
    HeadlessEntityId top = await PlaceRealCard(match, ctx, P2, "TfxArmorPurge");
    HeadlessEntityId src = MakeSource(ctx, P2, "f68-apsrc");
    SetMetadata(match, top, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.SourceIdsKey] = new[] { src.Value }
    });

    await DeleteByEffect(match, ctx, top);   // (B1) deletion DEFERRED — Armor Purge is a would-be-deleted replacement

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, top), "the top is still on the battle area (deletion deferred)");
    AssertTrue(ReadFlag(match, top, GameFlowProcessor.PendingDeletionKey), "pendingDeletion set (PRE window)");
    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, top), "ONLY the top card was trashed (AS-IS ArmorPurgeProcess)");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, src), "the under-source is promoted — the permanent survived");
    AssertFalse(ReadFlag(match, top, DeletionReplacementGate.DeletedByEffectKey),
        "the trashed top is NOT a deleted permanent (no OnDeletion / no POST windows)");
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no follow-up POST window for the purged top");
}

// --- Save (RETIRED gate option — 3a) ---------------------------------------

async Task SaveGateRetired()
{
    // (C-Del 3a RETIRED) The invented Save POST option (DeletionReplacementTiming SaveOption two-step) is
    // retired: AS-IS [Save] now fires through the OnDestroyedAnyone cut-in window from a printed SaveEffect
    // (witnessed live in tests/C-Del-POST). A BARE hasSave metadata marker — no printed effect — no longer
    // surfaces any POST agent choice, and the deleted card simply stays in the trash.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId saveCard = HandCard(match, P2, 1);
    HeadlessEntityId host = HandCard(match, P2, 2);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, saveCard, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, host, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, saveCard, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.HasSaveKey] = true });

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = saveCard.Value }));
    await sink.FlushAsync();
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, saveCard), "save card was deleted to the trash");
    AssertFalse(ResolveActions(match, P2).Any(a => a.Id.Value.Contains("#save", StringComparison.Ordinal)),
        "the retired Save POST option surfaces NO agent choice (single-fire: window only)");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, saveCard), "the deleted card stays in the trash (bare marker, no printed Save effect)");
    AssertFalse(SourceIds(match, host).Contains(saveCard.Value), "the card was NOT attached under any permanent by the retired gate");
}

// --- Battle PRE-path (real cards through the attack pipeline) -------------

async Task BarrierBattleChoice()
{
    (DcgoMatch match, EngineContext ctx, HeadlessEntityId attacker, HeadlessEntityId defender) =
        await BattleSetup("BT14_035", defenderSuspended: true);
    int securityBefore = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Security).Count;
    AssertTrue(securityBefore >= 1, "defender has security to spend");

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    await match.StepAsync();   // pipeline → Combat → promote-to-defer (park) → Barrier window opens

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "battle Barrier window is open");
    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // finalize the battle

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, defender), "barrier defender survives the battle");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, defender), "defender not trashed");
    int securityAfter = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Security).Count;
    AssertEqual(securityBefore - 1, securityAfter, "one security trashed as the Barrier cost");
}

async Task EvadeBattleChoice()
{
    (DcgoMatch match, EngineContext ctx, HeadlessEntityId attacker, HeadlessEntityId defender) =
        await BattleSetup("BT13_023", defenderSuspended: false);

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    await match.StepAsync();   // → Combat → promote-to-defer → Evade window

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "battle Evade window is open");
    LegalAction activate = ResolveActions(match, P2).Single(a => !a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, defender), "evade defender survives the battle");
    AssertTrue(ReadFlag(match, defender, DeletionReplacementGate.IsSuspendedKey), "suspended as the cost");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, defender), "defender not trashed");
}

// --- Setup helpers ---------------------------------------------------------

async Task<(DcgoMatch Match, EngineContext Ctx, HeadlessEntityId Attacker, HeadlessEntityId Defender)> BattleSetup(
    string defenderCardNumber, bool defenderSuspended)
{
    (DcgoMatch match, EngineContext ctx) = await StartedMatch();

    HeadlessEntityId attacker = HandCard(match, P1, 1);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, attacker, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 9000 });

    HeadlessEntityId defender = await PlaceRealCard(match, ctx, P2, defenderCardNumber);
    SetMetadata(match, defender, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dp"] = 7000,
        [DeletionReplacementGate.IsSuspendedKey] = defenderSuspended,
    });
    return (match, ctx, attacker, defender);
}

// A match-dealt hand card re-pointed at a real ported card number, moved to the battle area and registered
// through the real dispatch (CardEffectRegistrar) so its printed keyword ActivateClass is window-collectible.
async Task<HeadlessEntityId> PlaceRealCard(DcgoMatch match, EngineContext ctx, HeadlessPlayerId owner, string cardNumber, int handIndex = 1)
{
    HeadlessEntityId card = HandCard(match, owner, handIndex);
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"def:{cardNumber}");
    cards.Upsert(new CardRecord(defId, cardNumber, cardNumber,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Blue" }, ["level"] = 5, ["dp"] = 5000 }, CardType: "Digimon"));
    if (!ctx.CardInstanceRepository.TryGetInstance(card, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"missing {card}");
    ctx.CardInstanceRepository.Upsert(record with { DefinitionId = defId });

    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, card, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, card, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.IsSuspendedKey] = false });
    CardEffectRegistrar.RegisterCard(ctx, card, owner);
    return card;
}

async Task<HeadlessEntityId> PlacePlain(DcgoMatch match, EngineContext ctx, HeadlessPlayerId owner, int handIndex)
{
    HeadlessEntityId card = HandCard(match, owner, handIndex);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, card, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, card, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.IsSuspendedKey] = false });
    return card;
}

HeadlessEntityId MakeSource(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"def:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Blue" }, ["level"] = 4, ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}-{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    return id;
}

async Task DeleteByEffect(DcgoMatch match, EngineContext ctx, HeadlessEntityId cardId)
{
    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // RunToStable pauses on the parked window's pending choice
}

async Task<(DcgoMatch, EngineContext)> StartedMatch()
{
    // (C-Del 3c-2b) deferredChoice:true — the PRE keywords fire ONLY through the AS-IS cut-in window now, which
    // PARKS the interactive replacement as a pending choice (the default ScriptedChoiceProvider auto-skips it).
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 73, deferredChoice: true);
    var cards = (CardDatabase)ctx.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(ctx);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);
    (ctx.ChoiceProvider as DeferredChoiceProvider)?.CompleteResolution();
    return (match, ctx);
}

// Ascension/Save legacy harness (the RETAINED gate option needs no window drive).
async Task<(DcgoMatch Match, HeadlessEntityId Card)> SetupAndDelete(params (string Key, bool Value)[] cardFlags)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
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

    HeadlessEntityId card = HandCard(match, P2, 1);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, card, ChoiceZone.Hand, ChoiceZone.BattleArea));
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.IsSuspendedKey] = false
    };
    foreach ((string key, bool value) in cardFlags)
    {
        metadata[key] = value;
    }

    SetMetadata(match, card, metadata);

    // An effect deletes the card -> deferred (F-6.8), then the loop opens the replacement window.
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // RunToStable opens the deletion-replacement choice
    return (match, card);
}

// --- Bare-context harness (Decoy live-cardEffect Destroy drive) -----------

EngineContext NewBareContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 17, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> PlaceBare(EngineContext ctx, HeadlessPlayerId owner, string num, string instance)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    cards.Upsert(new CardRecord(defId, num, num,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId(instance);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// Resolve the parked PRE cut-in choice (ForCutIn pool): record the answer + resume; the deferred provider
// replays it. Same shape as the C-Del-3C1B witness.
async Task ResolveWindowChoice(EngineContext ctx, ChoiceResult answer)
{
    ctx.ChoiceController.ResolveChoice(answer);
    try { await AutoProcessing.ForCutIn(ctx).ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* re-parked */ }
}

bool InZoneBare(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlagBare(EngineContext ctx, HeadlessEntityId cardId, string key) =>
    ctx.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

// --- Shared match helpers --------------------------------------------------

IEnumerable<LegalAction> ResolveActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice);

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    if (hand.Length < index) throw new InvalidOperationException($"hand short: {hand.Length} < {index}");
    return hand[index - 1];
}

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

string[] SourceIds(DcgoMatch match, HeadlessEntityId cardId) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray() : Array.Empty<string>();

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

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
