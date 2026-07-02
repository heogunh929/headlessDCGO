using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GR-006: the end-of-turn effect-driven-attack window for <Vortex> (and <Overclock>). Vortex had its
// resolution machinery but no live trigger. EndOfTurnEffectAttack.TryOpen now offers it. AS-IS Vortex:
// attack an opponent's DIGIMON (any, incl. unsuspended), NOT the player; the EndTurn action opens this
// window before handing over the turn.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Vortex offers an attack on the opponent's Digimon — NOT the player (AS-IS)", VortexTargetsDigimonNotPlayer),
    ("Vortex can target an UNSUSPENDED opponent Digimon (isVortex)", VortexHitsUnsuspended),
    ("A suspended Vortex Digimon opens no window (its attack would suspend it)", SuspendedVortexNoWindow),
    ("EndTurn opens the Vortex window before handover; re-EndTurn then ends the turn", EndTurnOpensWindowLive),
    ("(K1) VortexCanAttackPlayers marker -> the PLAYER becomes a Vortex target", MarkerAllowsPlayerTarget),
    ("(K1) marker with no opponent Digimon -> the window still opens (player-only)", MarkerOpensPlayerOnlyWindow),
    ("(K1) marker attackerCondition NOT matching the attacker -> player stays untargetable", MarkerAttackerConditionHonored),
    ("(K1) the marker alone does NOT grant Vortex (un-flatten): no end-of-turn window", MarkerAloneIsNotVortex),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task VortexTargetsDigimonNotPlayer()
{
    EngineContext context = Context();
    var vortex = await PlaceDigimon(context, P1, "VTX", dp: 4000, suspended: false);
    var foe = await PlaceDigimon(context, P2, "FOE", dp: 3000, suspended: true);
    RegisterVortex(context, vortex, P1);

    AssertTrue(EndOfTurnEffectAttack.TryOpen(context, P1), "the Vortex window opened");
    var req = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.EffectAttack, req.Type, "an effect-attack choice is pending");
    AssertTrue(req.Candidates.Any(c => c.Label.Contains(foe.Value, StringComparison.Ordinal)), "the opponent Digimon is a target");
    AssertTrue(!req.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "the PLAYER is NOT a Vortex target (AllowPlayerTarget:false)");
}

async Task VortexHitsUnsuspended()
{
    EngineContext context = Context();
    var vortex = await PlaceDigimon(context, P1, "VTX", dp: 4000, suspended: false);
    var foe = await PlaceDigimon(context, P2, "FOE", dp: 3000, suspended: false); // UNsuspended
    RegisterVortex(context, vortex, P1);

    AssertTrue(EndOfTurnEffectAttack.TryOpen(context, P1), "the Vortex window opened");
    AssertTrue(context.ChoiceController.PendingRequest!.Candidates.Any(c => c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "an unsuspended opponent Digimon is a valid Vortex target (TargetUnsuspended)");
}

async Task SuspendedVortexNoWindow()
{
    EngineContext context = Context();
    var vortex = await PlaceDigimon(context, P1, "VTX", dp: 4000, suspended: true); // already suspended
    await PlaceDigimon(context, P2, "FOE", dp: 3000, suspended: true);
    RegisterVortex(context, vortex, P1);

    AssertTrue(!EndOfTurnEffectAttack.TryOpen(context, P1), "a suspended Vortex Digimon opens no window");
}

async Task EndTurnOpensWindowLive()
{
    var match = new DcgoMatch(EngineContext.CreateDefault(), new EngineTrace(), actionLegality: new LegalActionSetValidator());
    var env = new HeadlessRlEnvironment(match);
    await env.InitializeAsync(BuildMatchConfig());
    EngineContext context = match.Context;

    var vortex = await PlaceDigimon(context, P1, "VTX", dp: 4000, suspended: false);
    await PlaceDigimon(context, P2, "FOE", dp: 3000, suspended: true);
    RegisterVortex(context, vortex, P1);
    context.TurnController.SetPhase(HeadlessPhase.MemoryPass);

    // 1) EndTurn opens the window instead of ending the turn.
    LegalAction endTurn = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.EndTurn);
    await match.ApplyActionAsync(endTurn);
    await match.StepAsync();
    AssertTrue(context.ChoiceController.Current.IsPending, "EndTurn opened a pending end-of-turn window");
    AssertEqual(P1.Value, match.GetObservation().Turn.TurnPlayerId?.Value ?? 0, "the turn did NOT hand over yet");

    // 2) Decline the window, then EndTurn again -> the turn ends (Digimon is marked used).
    LegalAction skip = match.GetLegalActions(P1).First(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    await match.ApplyActionAsync(skip);
    await match.StepAsync();
    LegalAction endTurn2 = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.EndTurn);
    await match.ApplyActionAsync(endTurn2);
    await match.StepAsync();
    AssertEqual(P2.Value, match.GetObservation().Turn.TurnPlayerId?.Value ?? 0, "the turn handed over after the window closed");
}

// (K1) AS-IS CanActivateVortex: `... || PermanentHasVortexCanAttackPlayers(...)` — an active
// IVortexCanAttackPlayersEffect makes the PLAYER a legal Vortex target (evaluated once per offer).

async Task MarkerAllowsPlayerTarget()
{
    EngineContext context = Context();
    var vortex = await PlaceDigimon(context, P1, "VTX", dp: 4000, suspended: false);
    var foe = await PlaceDigimon(context, P2, "FOE", dp: 3000, suspended: true);
    RegisterVortex(context, vortex, P1);
    RegisterMarker(context, vortex, attackerCondition: null);

    AssertTrue(EndOfTurnEffectAttack.TryOpen(context, P1), "the Vortex window opened");
    var req = context.ChoiceController.PendingRequest!;
    AssertTrue(req.Candidates.Any(c => c.Label.Contains(foe.Value, StringComparison.Ordinal)), "the opponent Digimon is still a target");
    AssertTrue(req.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "the PLAYER is a Vortex target while the marker is active");
}

async Task MarkerOpensPlayerOnlyWindow()
{
    EngineContext context = Context();
    var vortex = await PlaceDigimon(context, P1, "VTX", dp: 4000, suspended: false);
    RegisterVortex(context, vortex, P1);
    RegisterMarker(context, vortex, attackerCondition: null);

    AssertTrue(EndOfTurnEffectAttack.TryOpen(context, P1), "no opponent Digimon, but canAttackPlayers -> window opens");
    AssertTrue(context.ChoiceController.PendingRequest!.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "the player is the offered target");
}

async Task MarkerAttackerConditionHonored()
{
    EngineContext context = Context();
    var vortex = await PlaceDigimon(context, P1, "VTX", dp: 4000, suspended: false); // level 4
    await PlaceDigimon(context, P2, "FOE", dp: 3000, suspended: true);
    RegisterVortex(context, vortex, P1);
    RegisterMarker(context, vortex, attackerCondition: p => p.Level == 5); // attacker is Lv4 -> no match

    AssertTrue(EndOfTurnEffectAttack.TryOpen(context, P1), "the Vortex window opened (Digimon target exists)");
    AssertTrue(!context.ChoiceController.PendingRequest!.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "attackerCondition not matching -> the player is NOT a target (predicate honored)");
}

async Task MarkerAloneIsNotVortex()
{
    EngineContext context = Context();
    var plain = await PlaceDigimon(context, P1, "PLAIN", dp: 4000, suspended: false);
    await PlaceDigimon(context, P2, "FOE", dp: 3000, suspended: true);
    RegisterMarker(context, plain, attackerCondition: null); // marker only — NO Vortex grant

    AssertTrue(!EndOfTurnEffectAttack.TryOpen(context, P1),
        "VortexCanAttackPlayers does not grant Vortex — no end-of-turn window (flatten regression)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp, bool suspended)
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

static void RegisterVortex(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId controller)
{
    var effect = KeywordBaseBatch2Factory.Create(KeywordBaseBatch2Kind.Vortex, cardId, targetEntityId: null, isInherited: false, isLinked: false);
    var binding = KeywordBaseBatch2Factory.ToBinding(effect, controller, new EffectContext(controller, cardId));
    context.EffectRegistry.Register(binding);
}

void RegisterMarker(EngineContext context, HeadlessEntityId sourceId, Func<Permanent, bool>? attackerCondition) =>
    context.EffectRegistry.Register(CardEffectFactory.VortexCanAttackPlayersStaticEffect(
        attackerCondition, false, new CardSource(context, sourceId, P1), null).ToBinding($"vcap:{sourceId.Value}"));

static MatchConfig BuildMatchConfig()
{
    HeadlessPlayerId[] players = { new(1), new(2) };
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(new HeadlessPlayerId(1), "P1"), BuildDeck(new HeadlessPlayerId(2), "P2") },
        firstPlayerId: new HeadlessPlayerId(1));
    return MatchConfig.Create(players, randomSeed: 17, setup: setup);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
