using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// (R4RL-01 — RL rollout blocker batch) Witnesses for the three latent "the agent followed the legal
// table / the automatic path and the engine threw" bugs the R4 shadow harnesses surfaced:
//   * RD-S3D-01  — the dispatcher's single-pick ResolveChoice table ignored the request's combination
//                  validator (AS-IS: the confirm button only activates when CanEndSelect passes, so
//                  "on the table = executable" is the AS-IS contract).
//   * RD-R4P4-02 — the ScriptedChoiceProvider fallback (the auto-select seat) confirmed a selection the
//                  validator rejects (AS-IS AI auto-select retries until _canEndSelectCondition passes) —
//                  the ST1_15 seed-303 throw ([Main] "delete up to 2", MinCount 0 + non-empty gate).
//   * RD-R4P4-01 — DcgoMatch.StepAsync/ApplyActionAsync did not self-scope AmbientMatchContext, so a bare
//                  consumer's block-choice resolve (BlockTiming.GetBlockerCandidates -> Permanent
//                  .HasCollision -> CEntity_EffectController.GetCardEffects reads GManager.instance) NRE'd.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("(RD-S3D-01) the pending-choice action table lists ONLY validator-passing single picks, and a listed pick resolves", DispatcherFiltersValidatorFailingPicks),
    ("(RD-S3D-01) a validator-less pending choice keeps the full candidate table (no behavior change)", DispatcherKeepsValidatorlessTable),
    ("(RD-R4P4-02) ScriptedChoiceProvider auto-select confirms a validator-passing set (AS-IS AI retry), not an invalid MinCount pick", AutoSelectHonorsValidator),
    ("(RD-R4P4-02) ScriptedChoiceProvider without a validator keeps the historical MinCount pick byte-for-byte", AutoSelectKeepsValidatorlessFallback),
    ("(RD-R4P4-02 e2e) ST1_15 [Main] activates through the auto-select path: both low-DP Digimon deleted, no validator throw", St115AutoSelectActivates),
    ("(RD-R4P4-01) a BARE consumer (no external ambient wrap) resolves a block-with-collision choice through the match surface without NRE", BareBlockResolveDoesNotNre),
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

// --- RD-S3D-01: dispatcher single-pick table vs the combination validator --------------------------

async Task DispatcherFiltersValidatorFailingPicks()
{
    (DcgoMatch match, HeadlessEntityId good1, HeadlessEntityId poison, HeadlessEntityId good2) =
        await PendingValidatorChoiceMatchAsync(seed: 41);

    // The AS-IS confirm gate ("End Selection" activates only when CanEndSelect passes): the size-1 set
    // {poison} fails the validator, so the poison candidate must NOT be on the ResolveChoice table.
    IReadOnlyList<LegalAction> table = match.GetLegalActions(P1);
    AssertTrue(table.All(a => a.ActionType == HeadlessActionTypes.ResolveChoice), "pending choice exposes ResolveChoice only");
    AssertEqual(2, table.Count, "only the two validator-passing candidates are listed (no skip: CanSkip=false)");
    AssertTrue(TableSelects(table, good1), "good1 is listed");
    AssertTrue(TableSelects(table, good2), "good2 is listed");
    AssertTrue(!TableSelects(table, poison), "the validator-failing candidate is NOT listed (RD-S3D-01)");

    // "On the table = executable": applying a listed action resolves the choice (no resolve-time throw).
    LegalAction pick = table.First(a => SelectsCandidate(a, good1));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();
    AssertTrue(!match.HasPendingChoice(), "the listed pick resolved the choice");
    AssertTrue(match.Context.ChoiceController.Current.IsResolved, "controller reports resolved");
    AssertEqual(good1.Value, match.Context.ChoiceController.Current.SelectedIds.Single().Value, "the picked candidate landed");
}

async Task DispatcherKeepsValidatorlessTable()
{
    (DcgoMatch match, _, _, _) = await PendingValidatorChoiceMatchAsync(seed: 43, withValidator: false);
    IReadOnlyList<LegalAction> table = match.GetLegalActions(P1);
    AssertEqual(3, table.Count, "a validator-less request keeps one action per selectable candidate");
}

async Task<(DcgoMatch Match, HeadlessEntityId Good1, HeadlessEntityId Poison, HeadlessEntityId Good2)>
    PendingValidatorChoiceMatchAsync(int seed, bool withValidator = true)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: seed);
    DcgoMatch match = new(context, new EngineTrace(), actionLegality: new LegalActionSetValidator());
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed));

    var good1 = new HeadlessEntityId("p2:battle:GOOD1");
    var poison = new HeadlessEntityId("p2:battle:POISON");
    var good2 = new HeadlessEntityId("p2:battle:GOOD2");

    // A single-pick-capable request carrying an AS-IS combination gate (CanEndSelect rides the request as
    // the SelectionValidator — the established SelectPermanentEffect route): non-empty AND not the poison id.
    var request = new ChoiceRequest(
        ChoiceType.Permanent, P1, "pick a target", minCount: 0, maxCount: 2, canSkip: false,
        ChoiceZone.BattleArea,
        new[]
        {
            new ChoiceCandidate(good1, good1.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: P2),
            new ChoiceCandidate(poison, poison.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: P2),
            new ChoiceCandidate(good2, good2.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: P2),
        });
    if (withValidator)
    {
        request = request with
        {
            SelectionValidator = ids => ids.Count > 0 && !ids.Contains(poison),
        };
    }

    context.ChoiceController.RequestChoice(request, new HeadlessEntityId("r4rl01:test-choice"));
    return (match, good1, poison, good2);
}

static bool TableSelects(IReadOnlyList<LegalAction> table, HeadlessEntityId candidateId) =>
    table.Any(a => SelectsCandidate(a, candidateId));

static bool SelectsCandidate(LegalAction action, HeadlessEntityId candidateId) =>
    action.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceSelectedIds, out object? raw)
    && raw is HeadlessEntityId[] ids && ids.Length == 1 && ids[0] == candidateId;

// --- RD-R4P4-02: the auto-select (ScriptedChoiceProvider fallback) seat ----------------------------

async Task AutoSelectHonorsValidator()
{
    // The ST1_15 request shape: MinCount 0 (canEndNotMax), MaxCount 2, no skip, and the non-empty
    // combination gate. The old fallback confirmed the empty MinCount pick -> ThrowIfInvalid threw
    // (the seed-303 shadow halt). AS-IS auto-select (SelectPermanentEffect.cs :629-684 AI branch)
    // confirms only a _canEndSelectCondition-passing set of _maxCount size.
    var a = new HeadlessEntityId("cand:A");
    var b = new HeadlessEntityId("cand:B");
    var c = new HeadlessEntityId("cand:C");
    var request = new ChoiceRequest(
        ChoiceType.Permanent, P1, "delete up to 2", minCount: 0, maxCount: 2, canSkip: false,
        ChoiceZone.BattleArea,
        new[] { a, b, c }.Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: P2)).ToArray())
    {
        SelectionValidator = ids => ids.Count > 0,
    };

    ChoiceResult result = await new ScriptedChoiceProvider().ChooseAsync(request);
    AssertTrue(!result.IsSkipped, "auto-select confirmed a selection (no skip surface: CanSkip=false)");
    AssertEqual(2, result.SelectedIds.Count, "the AS-IS AI set size: maxCount candidates");
    AssertTrue(request.SelectionValidator!(result.SelectedIds), "the confirmed set passes the combination validator");
}

async Task AutoSelectKeepsValidatorlessFallback()
{
    var a = new HeadlessEntityId("cand:A");
    var request = new ChoiceRequest(
        ChoiceType.Permanent, P1, "optional pick", minCount: 0, maxCount: 1, canSkip: false,
        ChoiceZone.BattleArea,
        new[] { new ChoiceCandidate(a, a.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: P2) });

    ChoiceResult result = await new ScriptedChoiceProvider().ChooseAsync(request);
    AssertEqual(0, result.SelectedIds.Count, "validator-less requests keep the historical MinCount pick");
}

async Task St115AutoSelectActivates()
{
    // The real seed-303 frame: ST1_15's [Main] ActivateCoroutine -> SelectPermanentEffect (canNoSelect=false,
    // canEndNotMax=true, CanEndSelectCondition=non-empty) -> the default ScriptedChoiceProvider. Before the
    // fix the auto path confirmed the empty selection and threw the combination-validator error.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 303);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);   // player order for the mirror GameContext scans
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(context);

    var low1 = new HeadlessEntityId("p2:battle:LOW1");
    var high = new HeadlessEntityId("p2:battle:HIGH");
    var low2 = new HeadlessEntityId("p2:battle:LOW2");
    await PlaceDigimon(context, P2, low1, dp: 3000);
    await PlaceDigimon(context, P2, high, dp: 5000);
    await PlaceDigimon(context, P2, low2, dp: 4000);

    var optionId = new HeadlessEntityId("p1:hand:ST1_15");
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(
        new HeadlessEntityId("DEF:ST1_15"), "ST1_15", "Jamming...", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Option"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(optionId, new HeadlessEntityId("DEF:ST1_15"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, optionId, ChoiceZone.None, ChoiceZone.Hand));

    var card = new Cec.CardSource(context, optionId, P1);
    var effect = (Cec.ActivateICardEffect)new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST1.Red.ST1_15()
        .CardEffects(Cec.EffectTiming.OptionSkill, card).Single();

    await effect.Activate(new Hashtable());

    AssertTrue(InZone(context, P2, ChoiceZone.Trash, low1), "the 3000-DP Digimon was deleted");
    AssertTrue(InZone(context, P2, ChoiceZone.Trash, low2), "the 4000-DP Digimon was deleted (up to 2 = the AS-IS AI max set)");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, high), "the 5000-DP Digimon is above the threshold and untouched");
}

async Task PlaceDigimon(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, int dp)
{
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(
        defId, defId.Value, id.Value, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 3 }, CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp };
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

static bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

// --- RD-R4P4-01: bare block-with-collision resolve through the match surface -----------------------

async Task BareBlockResolveDoesNotNre()
{
    // Staging (an explicit ambient wrap is fixture plumbing only — the AsyncLocal scope is restored before
    // the act): an unguarded OLD match with a staged attacker and an unsuspended <Blocker> defender, the
    // attack declared and the block window opened, exactly the G3.5-A3 / PumpBlockerBattle convention.
    EngineContext context = EngineContext.CreateDefault(randomSeed: 47);
    DcgoMatch match = new(context, new EngineTrace());
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 47));

    var attackerId = new HeadlessEntityId("p1:battle:ATK");
    var blockerId = new HeadlessEntityId("p2:battle:BLK");
    await PlaceDigimon(context, P1, attackerId, dp: 3000);
    await PlaceDigimon(context, P2, blockerId, dp: 5000);
    SetInstanceMetadata(context, blockerId, BlockTiming.HasBlockerKey, true);
    SetInstanceMetadata(context, blockerId, BlockTiming.IsSuspendedKey, false);

    using (AmbientMatchContext.Scope _staging = AmbientMatchContext.Enter(context))
    {
        context.AttackController.DeclareAttack(P1, attackerId, P2, targetId: null, isDirectAttack: true);
        new BlockTiming().RequestBlockChoice(context);
    }

    AssertTrue(match.HasPendingChoice(), "the block window opened as a pending choice");
    AssertEqual(ChoiceType.Blocker, match.Context.ChoiceController.PendingRequest!.Type, "blocker choice pending");

    // ACT — completely BARE: no AmbientMatchContext wrap around the match surface. The blocker resolve runs
    // MetadataActionProcessor.ResolveChoiceAsync -> BlockTiming.ResolveBlockChoice -> GetBlockerCandidates ->
    // Permanent.HasCollision -> EffectList(OnCounterTiming) -> CEntity_EffectController.GetCardEffects, which
    // reads GManager.instance — before the fix this NRE'd out of StepAsync (RD-R4P4-01; the P4 shadow
    // harness had to wrap every step). StepAsync/ApplyActionAsync now self-scope the ambient match.
    LegalAction pick = match.GetLegalActions(P2)
        .First(a => a.ActionType == HeadlessActionTypes.ResolveChoice && SelectsCandidate(a, blockerId));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();

    // The residual attack stages complete flow-side within the same step (the PumpBlockerBattle seam), so
    // the observable outcome is the AS-IS blocked battle: SwitchDefender suspended the blocker, the 3000-DP
    // attacker lost the DP compare against the 5000-DP blocker, and the attack state was cleared.
    AssertTrue(!match.HasPendingChoice(), "the block choice resolved");
    AssertTrue(ReadBoolMetadata(context, blockerId, BlockTiming.IsSuspendedKey), "blocking SUSPENDED the blocker (AS-IS SwitchDefender Tap)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, attackerId), "the attacker LOST the DP compare and was deleted to the trash");
    AssertTrue(InZone(context, P2, ChoiceZone.BattleArea, blockerId), "the higher-DP blocker survived on the field");
    AssertTrue(!match.Context.AttackController.Current.IsPending, "the attack fully resolved (no pending attack left)");
}

static void SetInstanceMetadata(EngineContext context, HeadlessEntityId cardId, string key, object? value)
{
    if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing instance '{cardId}'.");
    }

    var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [key] = value };
    context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

static bool ReadBoolMetadata(EngineContext context, HeadlessEntityId cardId, string key)
{
    return context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record)
        && record is not null
        && record.Metadata.TryGetValue(key, out object? raw) && raw is bool flag && flag;
}

// --- Asserts ---------------------------------------------------------------------------------------

static void AssertTrue(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"expected TRUE: {message}");
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
}
