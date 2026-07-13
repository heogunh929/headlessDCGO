// PRIM-P0-timing: the new card-facing EffectTiming members fire LIVE. A tiny probe effect returns
// AddMemoryTriggerEffect(+1) on the timing under test; the test drives the REAL game action that emits the timing
// and asserts the probe resolved (memory gained). OnEndBattle = a new enum member bound against an ALREADY-emitted
// engine timing (BattleResolver); OnStartMainPhase shares its mechanism (enum member + AllTimings vs an existing
// emit at MetadataActionProcessor).
//
// (B-2 / P1-5) OnDeclaration NOTE: attack declaration NO LONGER emits OnDeclaration. That attack-time emit was a
// stopgap for the missing "[Main] skill declaration" action; AS-IS OnDeclaration is a permanent's own [Main]
// declared-skill timing (TurnStateMachine.cs:1174-1195), not an attack broadcast. The real action now exists
// (MainSkillActivateAction), so OnDeclaration_NotFiredByAttack guards that the proxy stays removed; the POSITIVE
// [Main]-declaration coverage (offer → resolve → cap → reset) lives in B2-MainSkillDeclare.Tests.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId Player = new(1);
HeadlessPlayerId Opponent = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId BlockerId = new("p2:main:002:P2-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("CONTROL OnAllyAttack (known-good timing) fires in this harness", OnAllyAttack_Control),
    ("OnDeclaration NOT fired by attack (B-2 proxy removed); it is the [Main]-declaration action's timing", OnDeclaration_NotFiredByAttack),
    ("OnEndBattle fires: resolving a battle resolves the attacker's OnEndBattle effect", OnEndBattle_Fires),
    ("batch-2 enum members register a binding under their emitted timing string", Batch2_RegistersUnderEmittedString),
    ("WhenRemoveField (derived) fires: a field card leaving the battle area resolves its effect", WhenRemoveField_Fires),
    ("OnEndAttack fires: an attack ending resolves the attacker's OnEndAttack effect", OnEndAttack_Fires),
    ("OnDigivolutionCardDiscarded fires: trashing a source card resolves the host's effect", OnDigivolutionCardDiscarded_Fires),
    ("OnAttackTargetChanged fires: a raid switching the target resolves the attacker's effect", OnAttackTargetChanged_Fires),
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

async Task OnAllyAttack_Control()
{
    (DcgoMatch match, EngineContext context) = await BuildMatchInMain();
    Register(context, new TimingProbe(EffectTiming.OnAllyAttack), "PROBE", AttackerId);
    context.MemoryController.Set(0);

    LegalAction declare = match.GetLegalActions(Player).Single(a =>
        a.ActionType == HeadlessActionTypes.DeclareAttack &&
        (a.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackTargetId, out object? raw) ? raw?.ToString() : null) == TargetId.Value);
    await match.ApplyActionAsync(declare);
    await match.StepAsync();
    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(1, context.MemoryController.Current.Current, "CONTROL: known-good OnAllyAttack probe gained 1 memory");
}

async Task OnDeclaration_NotFiredByAttack()
{
    // (B-2 / P1-5) Regression guard: attack declaration MUST NOT fire OnDeclaration. The attack-time proxy emit
    // (a stopgap for the then-missing [Main]-declaration action) was removed — AS-IS OnDeclaration is a
    // permanent's own [Main] declared-skill timing, never triggered by attacking. The POSITIVE path (a [Main]
    // skill declared via MainSkillActivateAction resolves + consumes its OnDeclaration effect) is covered by
    // B2-MainSkillDeclare.Tests; here we only prove attacking no longer leaks into that timing.
    (DcgoMatch match, EngineContext context) = await BuildMatchInMain();

    Register(context, new TimingProbe(EffectTiming.OnDeclaration), "PROBE", AttackerId);
    context.MemoryController.Set(0);

    LegalAction declare = match.GetLegalActions(Player).Single(a =>
        a.ActionType == HeadlessActionTypes.DeclareAttack &&
        (a.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackTargetId, out object? raw) ? raw?.ToString() : null) == TargetId.Value);
    await match.ApplyActionAsync(declare);
    await match.StepAsync();
    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(0, context.MemoryController.Current.Current, "attacking does NOT fire the attacker's OnDeclaration effect (proxy removed)");
}

async Task OnEndBattle_Fires()
{
    (DcgoMatch match, EngineContext context) = await BuildMatchInMain();

    // Register the probe on the attacker; OnEndBattle is emitted (actor = attacker owner) after the battle
    // is resolved. With no blocker, StepAsync resolves the declared attack to battle end, emitting OnEndBattle.
    Register(context, new TimingProbe(EffectTiming.OnEndBattle), "PROBE", AttackerId);
    context.MemoryController.Set(0);                    // set before declaring so the battle-end gain is observed

    LegalAction declare = match.GetLegalActions(Player).Single(a =>
        a.ActionType == HeadlessActionTypes.DeclareAttack &&
        (a.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackTargetId, out object? raw) ? raw?.ToString() : null) == TargetId.Value);
    await match.ApplyActionAsync(declare);
    await match.StepAsync();                             // resolves the battle -> emits OnEndBattle
    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(1, context.MemoryController.Current.Current, "attacker's OnEndBattle effect gained 1 memory when the battle resolved");
}

// batch-2 timings are already emitted by the engine (verified emit sites, exercised by the full suite) and
// share OnEndBattle's proven fire mechanism. What THIS change adds is the card-facing enum member + AllTimings
// entry, so a card returning an effect on the timing registers a binding keyed by the emitted string. This
// test verifies exactly that half: enum name -> emitted timing string -> the collector can find the binding.
async Task Batch2_RegistersUnderEmittedString()
{
    var batch2 = new (EffectTiming Timing, string EmittedString)[]
    {
        (EffectTiming.OnTappedAnyone, "OnTappedAnyone"),
        (EffectTiming.OnUnTappedAnyone, "OnUnTappedAnyone"),
        (EffectTiming.OnCounterTiming, "OnCounterTiming"),
        (EffectTiming.WhenLinked, "WhenLinked"),
        (EffectTiming.OnLinkCardDiscarded, "OnLinkCardDiscarded"),
        (EffectTiming.OnAddDigivolutionCards, "OnAddDigivolutionCards"),
        (EffectTiming.OnUseOption, "OnUseOption"),
        (EffectTiming.OnDiscardSecurity, "OnDiscardSecurity"),
        (EffectTiming.AfterPayCost, "AfterPayCost"),
        (EffectTiming.WhenTopCardTrashed, "WhenTopCardTrashed"),
        (EffectTiming.OnFaceUpSecurityIncreased, "OnFaceUpSecurityIncreased"),
        // batch 3a (derived from CardMoved / SecurityCheck) — keyed by the same enum ToString value.
        (EffectTiming.WhenRemoveField, "WhenRemoveField"),
        (EffectTiming.OnLoseSecurity, "OnLoseSecurity"),
        (EffectTiming.OnDiscardHand, "OnDiscardHand"),
        (EffectTiming.OnAddHand, "OnAddHand"),
        (EffectTiming.OnDiscardLibrary, "OnDiscardLibrary"),
        (EffectTiming.OnAddSecurity, "OnAddSecurity"),
        (EffectTiming.WhenReturntoHandAnyone, "WhenReturntoHandAnyone"),
        (EffectTiming.WhenReturntoLibraryAnyone, "WhenReturntoLibraryAnyone"),
        (EffectTiming.OnSecurityCheck, "OnSecurityCheck"),
        (EffectTiming.OnReturnCardsToHandFromTrash, "OnReturnCardsToHandFromTrash"),
        (EffectTiming.OnPermamemtReturnedToHand, "OnPermamemtReturnedToHand"),
        (EffectTiming.OnRemovedField, "OnRemovedField"),
        (EffectTiming.OnLeaveFieldAnyone, "OnLeaveFieldAnyone"),
        (EffectTiming.OnReturnCardsToLibraryFromTrash, "OnReturnCardsToLibraryFromTrash"),
    };

    foreach ((EffectTiming timing, string emitted) in batch2)
    {
        EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
        context.TurnController.Initialize(new[] { Player, Opponent }, Player);
        var id = new HeadlessEntityId($"1:battle:{timing}");
        ((CardDatabase)context.CardRepository).Upsert(Digimon($"DEF-{timing}"));
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF-{timing}"), Player, Metadata: new Dictionary<string, object?>()));
        Register(context, new TimingProbe(timing), $"P-{timing}", id);

        int hits = ((EffectRegistry)context.EffectRegistry).GetEffectsForTiming(emitted).Count();
        AssertEqual(1, hits, $"{timing} registers exactly one binding findable under emitted string \"{emitted}\"");
    }
}

// OnDigivolutionCardDiscarded is emitted by DigivolutionStackHelpers when an effect trashes a Digimon's
// digivolution SOURCE (under) cards (AS-IS ITrashDigivolutionCards). Broadcast, so the host's effect fires.
async Task OnDigivolutionCardDiscarded_Fires()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 21);
    context.TurnController.Initialize(new[] { Player, Opponent }, Player);
    var hostDef = new HeadlessEntityId("DEF:HOST");
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(hostDef, hostDef.Value, "HOST", new Dictionary<string, object?>(), CardType: "Digimon"));
    var host = new HeadlessEntityId("1:battle:HOST");
    var src = new HeadlessEntityId("1:src:SRC1");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(host, hostDef, Player, Metadata: new Dictionary<string, object?> { ["sourceIds"] = new[] { src.Value } }));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(src, hostDef, Player, Metadata: new Dictionary<string, object?>()));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, host, ChoiceZone.None, ChoiceZone.BattleArea));

    Register(context, new TimingProbe(EffectTiming.OnDigivolutionCardDiscarded), "PROBE", host);
    context.MemoryController.Set(0);

    await DigivolutionStackHelpers.TrashSpecificSourcesAsync(
        context.CardInstanceRepository, context.ZoneMover, host, new[] { src }, default, context.GameEventQueue);
    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(1, context.MemoryController.Current.Current, "host's OnDigivolutionCardDiscarded effect gained 1 memory when a source was trashed");
}

// OnAttackTargetChanged is emitted when the attack's defender switches (RaidAttackSwitch.ResolveChoice ->
// AttackController.SwitchDefender). Broadcast; the attacker's effect self-gates on the subject.
async Task OnAttackTargetChanged_Fires()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 31);
    context.TurnController.Initialize(new[] { Player, Opponent }, Player);
    HeadlessEntityId attacker = await PlaceBattler(context, Player, "ATK", dp: 4000, new Dictionary<string, object?> { [RaidAttackSwitch.HasRaidKey] = true, [RaidAttackSwitch.IsSuspendedKey] = false });
    await PlaceBattler(context, Opponent, "LOW", dp: 3000, new Dictionary<string, object?> { [RaidAttackSwitch.IsSuspendedKey] = false });
    HeadlessEntityId high = await PlaceBattler(context, Opponent, "HIGH", dp: 8000, new Dictionary<string, object?> { [RaidAttackSwitch.IsSuspendedKey] = false });

    Register(context, new TimingProbe(EffectTiming.OnAttackTargetChanged), "PROBE", attacker);
    context.AttackController.DeclareAttack(Player, attacker, Opponent, targetId: null, isDirectAttack: true);
    context.MemoryController.Set(0);

    AssertEqual(true, RaidAttackSwitch.RequestChoice(context), "raid opens the target-switch choice");
    RaidAttackSwitch.ResolveChoice(context, ChoiceResult.Select(high));   // switches defender -> emits OnAttackTargetChanged
    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(1, context.MemoryController.Current.Current, "attacker's OnAttackTargetChanged effect gained 1 memory when the raid switched the target");
}

async Task<HeadlessEntityId> PlaceBattler(EngineContext context, HeadlessPlayerId owner, string tag, int dp, Dictionary<string, object?> extra)
{
    var def = new HeadlessEntityId($"DEF:{tag}");
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(def, def.Value, tag, new Dictionary<string, object?> { ["dp"] = dp }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    var meta = new Dictionary<string, object?>(extra, StringComparer.Ordinal) { [BattleResolver.DpKey] = dp };
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// OnEndAttack is collected by EndAttackTriggerHook at AttackPipeline.AdvanceEndAttackAsync (end of a single
// attack), not TriggerEventEmitter — driving a full attack to resolution runs the hook and fires the effect.
async Task OnEndAttack_Fires()
{
    (DcgoMatch match, EngineContext context) = await BuildMatchInMain();
    Register(context, new TimingProbe(EffectTiming.OnEndAttack), "PROBE", AttackerId);
    context.MemoryController.Set(0);

    LegalAction declare = match.GetLegalActions(Player).Single(a =>
        a.ActionType == HeadlessActionTypes.DeclareAttack &&
        (a.Parameters.TryGetValue(HeadlessActionParameterKeys.AttackTargetId, out object? raw) ? raw?.ToString() : null) == TargetId.Value);
    await match.ApplyActionAsync(declare);
    await match.StepAsync();                             // advances the attack through resolution -> end-attack hook
    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(1, context.MemoryController.Current.Current, "attacker's OnEndAttack effect gained 1 memory when the attack ended");
}

// A derived timing (WhenRemoveField is produced by TriggerTimingMap from a field->non-field CardMoved, not an
// explicit emit). This proves the derived-timing -> new enum member -> fire path end-to-end via a real zone move.
async Task WhenRemoveField_Fires()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11);
    context.TurnController.Initialize(new[] { Player, Opponent }, Player);
    var def = new HeadlessEntityId("DEF:RF");
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(def, def.Value, "RF", new Dictionary<string, object?> { ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId("1:battle:RF");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, Player, Metadata: new Dictionary<string, object?> { ["dp"] = 3000 }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, id, ChoiceZone.None, ChoiceZone.BattleArea));

    Register(context, new TimingProbe(EffectTiming.WhenRemoveField), "PROBE", id);
    context.MemoryController.Set(0);

    // Field -> Hand leaves the battle area, so TriggerTimingMap derives WhenRemoveField for this CardMoved.
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, id, ChoiceZone.BattleArea, ChoiceZone.Hand));
    await new GameFlowProcessor().RunToStableAsync(context);

    AssertEqual(1, context.MemoryController.Current.Current, "WhenRemoveField effect gained 1 memory when the card left the field");
}

// --- Harness -------------------------------------------------------------

async Task<(DcgoMatch, EngineContext)> BuildMatchInMain()
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
        new[] { Deck(Player, "P1"), Deck(Opponent, "P2") },
        firstPlayerId: Player, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, Player);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    // Attacker unsuspended (can declare); target suspended (a legal attack target).
    SetMetadata(match, AttackerId, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["isSuspended"] = false, [BattleResolver.DpKey] = 4000,
    });
    SetMetadata(match, TargetId, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["isSuspended"] = true, [BattleResolver.DpKey] = 3000,
    });
    return (match, context);
}

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(playerId).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void Register(EngineContext context, CEntity_Effect effect, string number, HeadlessEntityId source) =>
    CardEffectRegistrar.RegisterOnEnterPlay(context, effect, number, new CardSource(context, source, Player));

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}

// A minimal ported-style card effect that gains 1 memory when the timing under test fires. Used only to
// observe that the new timing reaches a registered card effect end-to-end.
public sealed class TimingProbe : CEntity_Effect
{
    private readonly EffectTiming _fireOn;
    public TimingProbe(EffectTiming fireOn) => _fireOn = fireOn;

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == _fireOn)
        {
            // Non-optional gain-memory trigger (isOptional:false) — resolves without a player choice, so the
            // test observes the timing firing directly rather than pausing on a yes/no memory-gain choice.
            effects.Add(new TriggeredGainMemoryEffect(card, _fireOn, amount: 1, $"[probe] gain 1 memory on {_fireOn}."));
        }
        return effects;
    }
}
