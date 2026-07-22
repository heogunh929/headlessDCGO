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
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Diagnostics;
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
    ("batch-2 enum members map to their emitted trigger-name string (registry registration surface retired, ③-B)", Batch2_RegistersUnderEmittedString),
    ("WhenRemoveField (derived) fires: a field card leaving the battle area resolves its effect", WhenRemoveField_Fires),
    ("OnEndAttack fires: an attack ending resolves the attacker's OnEndAttack effect", OnEndAttack_Fires),
    ("OnDigivolutionCardDiscarded fires: trashing a source card resolves the host's effect", OnDigivolutionCardDiscarded_Fires),
    // (G-clean) "OnAttackTargetChanged fires via raid switch" RETIRED — it drove the now-deleted invented
    // RaidAttackSwitch gate. OnAttackTargetChanged's emit (AttackProcess.SwitchDefender) is covered by the block
    // path in tests/F1-Tier2-OnAttackTargetChanged; the printed-Raid window path is covered by tests/C-Atk-Raid.
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
    await DriveAttack(match, declare, context);

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
    await DriveAttack(match, declare, context);

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
    await DriveAttack(match, declare, context);

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
        // (③-B) The old REGISTRY-BINDING registration half (Register a BindingProbe, then find it via the retired
        // IEffectQueryService.GetEffectsForTiming(emitted)) is RETIRED — the EffectRegistry producer reached 0, and
        // trigger collection scans the field via AutoProcessingTriggerCollector, not a registry timing query. The
        // surviving contract is the enum -> emitted-string keying the collector matches emitted TriggerTimings on:
        // EffectTimings.ToTriggerName(timing) is the string an effect on this timing is filed under, and it must
        // equal the engine's emitted TriggerTimings value.
        AssertEqual(emitted, EffectTimings.ToTriggerName(timing),
            $"{timing} keys under emitted trigger string \"{emitted}\"");
    }
}

// OnDigivolutionCardDiscarded is emitted by DigivolutionStackHelpers when an effect trashes a Digimon's
// digivolution SOURCE (under) cards (AS-IS ITrashDigivolutionCards). Broadcast, so the host's effect fires.
async Task OnDigivolutionCardDiscarded_Fires()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 21);
    context.TurnController.Initialize(new[] { Player, Opponent }, Player);
    context.TurnController.SetPhase(HeadlessPhase.Main);   // (R3-C2b-2) DoneStartGame gate: new-model ActivateClass only fires past Setup.
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
    await DriveAttack(match, declare, context);

    AssertEqual(1, context.MemoryController.Current.Current, "attacker's OnEndAttack effect gained 1 memory when the attack ended");
}

// A derived timing (WhenRemoveField is produced by TriggerTimingMap from a field->non-field CardMoved, not an
// explicit emit). This proves the derived-timing -> new enum member -> fire path end-to-end via a real zone move.
async Task WhenRemoveField_Fires()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 11);
    context.TurnController.Initialize(new[] { Player, Opponent }, Player);
    context.TurnController.SetPhase(HeadlessPhase.Main);   // (R3-C2b-2) DoneStartGame gate: new-model ActivateClass only fires past Setup.
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

    // (4b B6 re-pin) OLD `new DcgoMatch(context)` + AdvanceToMain (AdvancePhase currency) -> CreatePumpDriven +
    // pump reach-main. The pump owns the deal, so its dealt hand/security are cleared and the attacker/target are
    // staged explicitly (the OLD dealt-hand instance ids do not exist on a pump match). Driver-only change; every
    // assertion (incl. the OnEndBattle / WhenRemoveField real-debt reds) is unchanged.
    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(Player, "P1"), Deck(Opponent, "P2") },
        firstPlayerId: Player, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { Player, Opponent }, randomSeed: 73, setup: setup));
    await DriveUntilMainWait(match, Player);

    var reader = (IZoneStateReader)context.ZoneMover;
    foreach (HeadlessPlayerId owner in new[] { Player, Opponent })
    {
        foreach (ChoiceZone dealtZone in new[] { ChoiceZone.Security, ChoiceZone.Hand })
        {
            foreach (HeadlessEntityId dealt in reader.GetCards(owner, dealtZone).ToArray())
            {
                await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, dealt, dealtZone, ChoiceZone.Library));
            }
        }
    }

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(AttackerId, new HeadlessEntityId("P1-M01"), Player));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(TargetId, new HeadlessEntityId("P2-M01"), Opponent));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Player, AttackerId, ChoiceZone.None, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(Opponent, TargetId, ChoiceZone.None, ChoiceZone.BattleArea));
    // Register the staged synthetics as live permanents so the pump's DeclareAttack legal lane (Cec field-permanent
    // index resolution) recognises them (G3.5-W5 staging idiom).
    CardEffectRegistrar.RegisterCard(context, AttackerId, Player);
    CardEffectRegistrar.RegisterCard(context, TargetId, Opponent);

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

static async Task StepOnce(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static async Task ApplyAndStep(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
}

static bool AtMainWait(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

static async Task DriveUntilMainWait(DcgoMatch match, HeadlessPlayerId player)
{
    for (int i = 0; i < 96 && !AtMainWait(match, player); i++)
    {
        if (match.HasPendingChoice())
        {
            HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
            LegalAction? resolve;
            using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
            {
                resolve = match.GetLegalActions(chooser)
                    .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                        && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal))
                    ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
            }
            if (resolve is null) { await StepOnce(match); }
            else { await ApplyAndStep(match, resolve); }
        }
        else
        {
            await StepOnce(match);
        }
    }

    if (!AtMainWait(match, player))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump did not reach {player.Value}'s main wait — phase:{t.Phase}/{t.StepCursor} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

// Apply the DeclareAttack packet and drive the pump until the battle resolves. Unlike the OLD driver (where a
// single StepAsync applied + resolved), the pump defers the main-phase packet to a later pump step, so the attack
// must be driven to AttackPhase.None before the external flow drain observes the timing.
async Task DriveAttack(DcgoMatch match, LegalAction declare, EngineContext context)
{
    // Declaration-time triggers (OnAllyAttack / OnAttack) are emitted by the retained AttackDeclarationCommons.Declare
    // seam (AttackDeclarationCommons.cs:48), not by AttackController.DeclareAttack — so declare through it (the same
    // retained seam C5-Witness uses), then drive the pump to resolve the battle. `declare`'s existence is already
    // asserted by the caller's Single(...) lane selection.
    using (AmbientMatchContext.Scope _decl = AmbientMatchContext.Enter(match.Context))
    {
        AttackDeclarationCommons.Declare(match.Context, Player, AttackerId, Opponent, targetId: TargetId, isDirectAttack: false);
    }
    bool sawAttack = false;
    for (int i = 0; i < 96; i++)
    {
        if (match.HasPendingChoice() || match.IsTerminal())
        {
            break;
        }
        HeadlessAttackState attack = match.Context.AttackController.Current;
        if (attack.Phase != AttackPhase.None || attack.IsPending)
        {
            sawAttack = true;
        }
        else if (sawAttack)
        {
            break;
        }
        await StepOnce(match);
    }

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(context);
    await new GameFlowProcessor().RunToStableAsync(context);
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
//
// (R3-C2b-2) The engine's invented old-model TriggeredGainMemoryEffect was deleted. This suite drives each timing
// through the live R3 trigger window: RegisterOnEnterPlay sets the card's cEntity_Effect, so the probe surfaces via
// cardSource/permanent EffectList(timing) and AutoProcessing.GetSkillInfos collects it (it filters to
// ActivateICardEffect). The probe is therefore now the AS-IS new-model inline ActivateClass "gain 1 memory"
// (canUse = owner-turn gate, matching the deleted class's TurnPlayerId==Owner resolve gate; body =
// card.Owner.AddMemory(1, activateClass)).
public sealed class TimingProbe : CEntity_Effect
{
    private readonly EffectTiming _fireOn;
    public TimingProbe(EffectTiming fireOn) => _fireOn = fireOn;

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == _fireOn)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, $"[probe] gain 1 memory on {_fireOn}.");
            effects.Add(activateClass);

            bool CanUseCondition(System.Collections.Hashtable hashtable) => CardEffectCommons.IsOwnerTurn(card);

            bool CanActivateCondition(System.Collections.Hashtable hashtable) => true;

            async System.Threading.Tasks.Task ActivateCoroutine(System.Collections.Hashtable _hashtable) =>
                await card.Owner.AddMemory(1, activateClass);
        }
        return effects;
    }
}

// (R3-C2b-2) A test-local OLD-model probe (ICardEffect+IHeadlessCardEffect with ToBinding) used ONLY by the
// Batch2_RegistersUnderEmittedString registry-registration check — it must lower to a registry binding so
// GetEffectsForTiming finds it (the deleted TriggeredGainMemoryEffect's role there). Not used by the window
// fire tests. Replaces the deleted engine primitive without keeping it alive.
public sealed class BindingProbe : CEntity_Effect
{
    private readonly EffectTiming _fireOn;
    public BindingProbe(EffectTiming fireOn) => _fireOn = fireOn;

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == _fireOn)
        {
            effects.Add(new LocalBindingMemoryProbe(card, _fireOn, amount: 1, $"[probe] gain 1 memory on {_fireOn}."));
        }
        return effects;
    }
}

public sealed class LocalBindingMemoryProbe : ICardEffect, IHeadlessCardEffect
{
    public LocalBindingMemoryProbe(CardSource card, EffectTiming timing, int amount, string description)
    {
        Card = card;
        Amount = amount;
        string trigger = EffectTimings.ToTriggerName(timing);
        Definition = new CardEffectDefinition(
            new HeadlessEntityId($"{card.InstanceId.Value}:gainmemoryprobe:{trigger}"), card.InstanceId, description, trigger, isOptional: false);
    }

    public CardSource Card { get; }

    public int Amount { get; }

    public CardEffectDefinition Definition { get; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(CardEffectResolveContext context, IEffectMutationSink mutations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        cancellationToken.ThrowIfCancellationRequested();
        if (Card.Context.TurnController.Current.TurnPlayerId != Card.Owner)
        {
            return ValueTask.FromResult(EffectResult.Success("Not owner's turn; no change."));
        }

        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.AddMemoryKind, Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.AmountKey] = Amount }));
        return ValueTask.FromResult(EffectResult.Success($"Gain {Amount} memory."));
    }

    public EffectBinding ToBinding(string effectId)
    {
        var ctx = new EffectContext(
            Card.Controller, Card.Owner, Card.InstanceId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>());
        return new EffectBinding(
            new EffectRequest(Definition.EffectId, Card.Controller, Definition.Timing, ctx),
            keywords: null, EffectQueryRole.None, Array.Empty<string>(), effect: this, duration: null);
    }
}
