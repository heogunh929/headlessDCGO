using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// F-6.8 — re-entrant would-be-deleted replacement window (AS-IS optionality restored).
// An OPTIONAL replacement keyword (here Evade, effect-deletion path) no longer auto-applies; the deletion
// is DEFERRED (pendingDeletion) and the owner decides via a DeletionReplacement choice. Activate → pay
// cost + survive; decline → the state-based sweep finishes the deletion. (Mirrors the AS-IS "you may".)

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Effect deletion of an Evade Digimon opens a replacement choice (not auto-applied)", DeferOpensChoice),
    ("Activating Evade in the window suspends it and cancels the deletion", ActivateEvadeSurvives),
    ("Declining the window lets the deletion finish (swept to trash)", DeclineGetsDeleted),
    ("Ascension opens a post-deletion choice once the card is in the trash", AscensionOpensPostChoice),
    ("Activating Ascension places the deleted card into security", ActivateAscension),
    ("Declining Ascension leaves the card in the trash", DeclineAscension),
    ("Scapegoat is a two-step choice: activate, then pick which ally to sacrifice", ScapegoatTwoStep),
    ("Fragment is a two-step choice: activate, then pick which source to trash", FragmentTwoStep),
    ("Decoy is a two-step choice: activate, then pick which Decoy ally to sacrifice", DecoyTwoStep),
    ("(B1) Armor Purge is a WOULD-BE-DELETED replacement: top trashed, permanent survives", ArmorPurgePostChoice),
    ("Save is a post-deletion two-step choice: activate, then pick the permanent", SaveTwoStep),
    ("Battle: activating Barrier in the window trashes a security and survives", BarrierBattleChoice),
    ("Battle: activating Evade in the window suspends and survives", EvadeBattleChoice),
    ("Retaliation: the targeted opponent may Evade the retaliation (window re-opens)", RetaliationOpponentCanEvade),
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

async Task DeferOpensChoice()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete((DeletionReplacementGate.HasEvadeKey, true));

    AssertTrue(ReadFlag(match, card, GameFlowProcessor.PendingDeletionKey), "deletion deferred (pendingDeletion)");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, card), "card not yet trashed");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a replacement choice is open");
    AssertEqual(ChoiceType.DeletionReplacement, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertEqual(P2, match.Context.ChoiceController.PendingRequest!.PlayerId, "owner decides");
}

async Task ActivateEvadeSurvives()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete((DeletionReplacementGate.HasEvadeKey, true));

    LegalAction activate = ResolveActions(match, P2).Single(a => a.Id.Value.Contains("#evade", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, card), "evade cancels the deletion");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, card), "card not trashed");
    AssertTrue(ReadFlag(match, card, DeletionReplacementGate.IsSuspendedKey), "suspended as the cost");
    AssertTrue(ReadFlag(match, card, DeletionReplacementGate.EvadedKey), "evaded marker");
    AssertFalse(ReadFlag(match, card, GameFlowProcessor.PendingDeletionKey), "pendingDeletion cleared");
}

async Task DeclineGetsDeleted()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete((DeletionReplacementGate.HasEvadeKey, true));

    LegalAction decline = ResolveActions(match, P2).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(decline);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, card), "declining lets the deletion finish");
    AssertFalse(InZone(match, P2, ChoiceZone.BattleArea, card), "card left the field");
}

// --- Ascension (post-deletion) ------------------------------------------

async Task AscensionOpensPostChoice()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete((DeletionReplacementGate.HasAscensionKey, true));

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, card), "ascension card was deleted to the trash first");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "a post-deletion choice is open");
    AssertEqual(ChoiceType.DeletionReplacement, match.Context.ChoiceController.PendingRequest!.Type, "choice type");
}

async Task ActivateAscension()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete((DeletionReplacementGate.HasAscensionKey, true));

    LegalAction activate = ResolveActions(match, P2).Single(a => a.Id.Value.Contains("#ascension", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Security, card), "activated ascension places the card into security");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, card), "card left the trash");
    // (K2) AS-IS AscensionProcess: AddSecurityCard(card, true) = the TOP of security (index 0), not the bottom.
    var zones = (IZoneStateReader)match.Context.ZoneMover;
    AssertEqual(card, zones.GetCards(P2, ChoiceZone.Security)[0], "the card is the TOP security card (AS-IS toTop)");
}

async Task DeclineAscension()
{
    (DcgoMatch match, HeadlessEntityId card) = await SetupAndDelete((DeletionReplacementGate.HasAscensionKey, true));

    LegalAction decline = ResolveActions(match, P2).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(decline);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, card), "declining ascension leaves the card in the trash");
    AssertFalse(InZone(match, P2, ChoiceZone.Security, card), "card not placed into security");
}

// --- Scapegoat (two-step sub-selection) ---------------------------------

async Task ScapegoatTwoStep()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId ally = HandCard(match, P2, 2);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, ally, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, holder, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.HasScapegoatKey] = true });

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = holder.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // step 1 window opens

    // Step 1: activate Scapegoat (candidate "{holder}#scapegoat", no target segment yet).
    LegalAction activate = ResolveActions(match, P2).Single(a =>
        a.Id.Value.Contains("#scapegoat", StringComparison.Ordinal) && !a.Id.Value.Contains(ally.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step 2 window opens (pick the ally)

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "step-2 target choice is open");

    // Step 2: pick the ally to sacrifice.
    LegalAction pickAlly = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(ally.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pickAlly);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, holder), "scapegoat holder survives");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, holder), "holder not trashed");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, ally), "the chosen ally is sacrificed");
}

async Task FragmentTwoStep()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId top = HandCard(match, P2, 1);
    HeadlessEntityId src1 = new("P2-FS1");
    HeadlessEntityId src2 = new("P2-FS2");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(src1, new HeadlessEntityId("def"), P2));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(src2, new HeadlessEntityId("def"), P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, top, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, top, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasFragmentKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { src1.Value, src2.Value }
    });

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = top.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // step 1

    LegalAction activate = ResolveActions(match, P2).Single(a =>
        a.Id.Value.Contains("#fragment", StringComparison.Ordinal) &&
        !a.Id.Value.Contains(src1.Value, StringComparison.Ordinal) && !a.Id.Value.Contains(src2.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step 2

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "step-2 source choice is open");

    LegalAction pickSrc = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(src1.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pickSrc);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, top), "fragment top survives");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, top), "top not trashed");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, src1), "the chosen source is trashed as the cost");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, src2), "the unchosen source stays");
}

async Task DecoyTwoStep()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId holder = HandCard(match, P2, 1);
    HeadlessEntityId decoy = HandCard(match, P2, 2);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, holder, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, decoy, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, decoy, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.HasDecoyKey] = true });

    HeadlessEntityId deleter = new("P1-Deleter");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(deleter, new HeadlessEntityId("def"), P1));
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, deleter,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = holder.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // step 1

    LegalAction activate = ResolveActions(match, P2).Single(a =>
        a.Id.Value.Contains("#decoy", StringComparison.Ordinal) && !a.Id.Value.Contains(decoy.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step 2

    LegalAction pickDecoy = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(decoy.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pickDecoy);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, holder), "the protected target survives");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, holder), "target not trashed");
    AssertTrue(InZone(match, P2, ChoiceZone.Trash, decoy), "the chosen Decoy ally is sacrificed");
}

async Task ArmorPurgePostChoice()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId top = HandCard(match, P2, 1);
    HeadlessEntityId src = new("P2-APSrc");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(src, new HeadlessEntityId("def"), P2));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, top, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, top, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [DeletionReplacementGate.HasArmorPurgeKey] = true,
        [DeletionReplacementGate.SourceIdsKey] = new[] { src.Value }
    });

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = top.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // (B1) deletion DEFERRED — Armor Purge is a would-be-deleted replacement

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, top), "the top is still on the battle area (deletion deferred)");
    AssertTrue(ReadFlag(match, top, GameFlowProcessor.PendingDeletionKey), "pendingDeletion set (PRE window)");
    LegalAction activate = ResolveActions(match, P2).Single(a => a.Id.Value.Contains("#armorpurge", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, top), "ONLY the top card was trashed (AS-IS ArmorPurgeProcess)");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, src), "the under-source is promoted — the permanent survived");
    AssertFalse(ReadFlag(match, top, DeletionReplacementGate.DeletedByEffectKey),
        "the trashed top is NOT a deleted permanent (no OnDeletion / no POST windows)");
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no follow-up POST window for the purged top");
}

async Task SaveTwoStep()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId saveCard = HandCard(match, P2, 1);
    HeadlessEntityId host = HandCard(match, P2, 2);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, saveCard, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, host, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, saveCard, new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.HasSaveKey] = true });

    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = saveCard.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // saveCard trashed, POST window opens

    AssertTrue(InZone(match, P2, ChoiceZone.Trash, saveCard), "save card was deleted to the trash first");
    LegalAction activate = ResolveActions(match, P2).Single(a =>
        a.Id.Value.Contains("#save", StringComparison.Ordinal) && !a.Id.Value.Contains(host.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step 2: pick the host permanent

    LegalAction pickHost = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(host.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pickHost);
    await match.StepAsync();

    AssertFalse(InZone(match, P2, ChoiceZone.Trash, saveCard), "saved card left the trash");
    AssertFalse(InZone(match, P2, ChoiceZone.BattleArea, saveCard), "saved card is no longer a standalone permanent");
    AssertTrue(SourceIds(match, host).Contains(saveCard.Value), "saved card attached under the chosen permanent");
}

// --- Battle PRE-path (deferred via AttackPhase.DeletionReplacement) -------

async Task BarrierBattleChoice()
{
    (DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId defender) =
        await BattleSetup((DeletionReplacementGate.HasBarrierKey, true), defenderSuspended: true);
    int securityBefore = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Security).Count;
    AssertTrue(securityBefore >= 1, "defender has security to spend");

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    await match.StepAsync();   // pipeline → Combat → defer (park) → Barrier window opens

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "battle Barrier window is open");
    LegalAction activate = ResolveActions(match, P2).Single(a => a.Id.Value.Contains("#barrier", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // finalize the battle

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, defender), "barrier defender survives the battle");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, defender), "defender not trashed");
    int securityAfter = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Security).Count;
    AssertEqual(securityBefore - 1, securityAfter, "one security trashed as the Barrier cost");
}

async Task EvadeBattleChoice()
{
    (DcgoMatch match, HeadlessEntityId attacker, HeadlessEntityId defender) =
        await BattleSetup((DeletionReplacementGate.HasEvadeKey, true), defenderSuspended: false);

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    await match.StepAsync();   // → Combat → defer → Evade window

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "battle Evade window is open");
    LegalAction activate = ResolveActions(match, P2).Single(a => a.Id.Value.Contains("#evade", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, defender), "evade defender survives the battle");
    AssertTrue(ReadFlag(match, defender, DeletionReplacementGate.IsSuspendedKey), "suspended as the cost");
    AssertTrue(ReadFlag(match, defender, DeletionReplacementGate.EvadedKey), "evaded marker");
}

async Task RetaliationOpponentCanEvade()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    // Attacker (P1) has Retaliation and LOSES the battle (5000 < 8000); defender (P2) wins but has Evade.
    HeadlessEntityId attacker = HandCard(match, P1, 1);
    HeadlessEntityId defender = HandCard(match, P2, 1);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, defender, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, attacker, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dp"] = 5000,
        [BattleResolver.HasRetaliationKey] = true
    });
    SetMetadata(match, defender, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dp"] = 8000,
        [DeletionReplacementGate.IsSuspendedKey] = false,
        [DeletionReplacementGate.HasEvadeKey] = true
    });

    match.Context.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    await match.StepAsync();   // attacker loses → confirmed → Retaliation flags defender → Evade window opens

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "the retaliated opponent's Evade window opened");
    LegalAction activate = ResolveActions(match, P2).Single(a => a.Id.Value.Contains("#evade", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // finalize

    AssertTrue(InZone(match, P1, ChoiceZone.Trash, attacker), "the retaliation holder (loser) is deleted");
    AssertTrue(InZone(match, P2, ChoiceZone.BattleArea, defender), "the opponent Evaded the retaliation and survives");
    AssertTrue(ReadFlag(match, defender, DeletionReplacementGate.EvadedKey), "opponent evaded marker");
}

async Task<(DcgoMatch Match, HeadlessEntityId Attacker, HeadlessEntityId Defender)> BattleSetup(
    (string Key, bool Value) defenderFlag, bool defenderSuspended)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);

    HeadlessEntityId attacker = HandCard(match, P1, 1);
    HeadlessEntityId defender = HandCard(match, P2, 1);
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, attacker, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, defender, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, attacker, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 9000 });
    SetMetadata(match, defender, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dp"] = 7000,
        [DeletionReplacementGate.IsSuspendedKey] = defenderSuspended,
        [defenderFlag.Key] = defenderFlag.Value
    });
    return (match, attacker, defender);
}

// --- Harness -------------------------------------------------------------

async Task<(DcgoMatch Match, HeadlessEntityId Card)> SetupAndDelete(params (string Key, bool Value)[] cardFlags)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
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
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("deleter"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.Value }));
    await sink.FlushAsync();
    await match.StepAsync();   // RunToStable opens the deletion-replacement choice
    return (match, card);
}

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
    for (var attempt = 0; attempt < 10 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(player).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
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
