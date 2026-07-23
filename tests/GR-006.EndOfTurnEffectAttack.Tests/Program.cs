// GR-006 (rewritten for C-EoT-2, RE-TARGETED 4b B1-α onto the DcgoMatch.CreatePumpDriven pump): <Vortex>
// end-of-turn firing is RE-HOUSED to the AS-IS OnEndTurn window and RETIRED from the invented
// EndOfTurnEffectAttack gate (physically deleted).
//
// AS-IS Vortex (VortexProcess / CanActivateVortex, KeyWordEffects/Vortex.cs): the printed VortexSelfEffect
// ActivateClass (returned by the card's CardEffects(OnEndTurn)) is collected by AutoProcessing.GetSkillInfos
// and resolved by MultipleSkills -> VortexProcess -> SelectAttackEffect, offering an attack on an opponent's
// Digimon it CanAttackTargetDigimon(isVortex) (a SUSPENDED Digimon; isVortex does NOT lift the suspended-defender
// gate — only isExecute does, Permanent.cs:2311), and the PLAYER only while an IVortexCanAttackPlayersEffect
// accepts this attacker (CanActivateVortex `|| PermanentHasVortexCanAttackPlayers`).
//
// DRIVE (4b): the OnEndTurn window is driven by the pump's real turn-end — an explicit P1 Pass runs
// EndPhaseAsync -> EndTurnProcess -> StackSkillInfos(OnEndTurn) + AutoProcessCheck. The AS-IS optional "Will you
// use Vortex?" and its VortexProcess SelectAttackEffect surface at the AGENT SEAT (PolicyChoiceProvider), where
// they are OBSERVED by capturing the ChoiceRequest (the EXEMPLAR-T1/R4S3b precedent; the old throw-record-replay
// unwind is retired). GetSkillInfos(OnEndTurn) collection assertions are the retained substrate, unchanged.
//
// These tests assert:
//   * the OnEndTurn window collects the printed Vortex (single path: the invented gate is deleted);
//   * the window offers a SUSPENDED opponent Digimon, and NOT the player without an enabler;
//   * an IVortexCanAttackPlayersEffect on another permanent makes the PLAYER a target (K1), honoring its
//     attackerCondition;
//   * an UNSUSPENDED-only board opens no window (isVortex != isExecute — the old gate's invented
//     TargetUnsuspended:true for <Vortex> was a divergence, now corrected);
//   * the enabler alone does NOT grant Vortex.
// The full printed+granted firing (both keywords) is witnessed in tests/C-EoT2.

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using ActivateClass = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("The OnEndTurn window collects the printed Vortex (invented gate is deleted — window is the sole path)", GateRetiredWindowCollects),
    ("The window offers a SUSPENDED opponent Digimon — NOT the player (no VortexCanAttackPlayers)", WindowTargetsSuspendedNotPlayer),
    ("An UNSUSPENDED-only opponent board opens no window (isVortex != isExecute — fidelity correction)", UnsuspendedOnlyNoWindow),
    ("A suspended Vortex Digimon opens no window (its attack would suspend it)", SuspendedVortexNoWindow),
    ("(K1) an IVortexCanAttackPlayersEffect enabler -> the PLAYER becomes a Vortex target", EnablerAllowsPlayerTarget),
    ("(K1) enabler attackerCondition NOT matching -> the player stays untargetable", EnablerAttackerConditionHonored),
    ("(K1) the enabler alone does NOT grant Vortex: no OnEndTurn window", EnablerAloneIsNotVortex),
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

async Task GateRetiredWindowCollects()
{
    (DcgoMatch match, _) = await NewPumpMatchAsync(seed: 71);
    var vortex = Place(match, P1, "TfxVortex", suspended: false);
    Place(match, P2, "FOE", suspended: true);
    RegisterVortex(match, vortex);

    using var scope = AmbientMatchContext.Enter(match.Context);
    AssertTrue(AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Any(si => si.CardEffect is ActivateICardEffect),
        "the OnEndTurn window collects the printed Vortex ActivateClass");
    // (G-clean) The invented EndOfTurnEffectAttack gate is physically deleted — single-fire is now proven
    // structurally (the gate class no longer exists); the OnEndTurn window is the sole <Vortex> firing path.
}

async Task WindowTargetsSuspendedNotPlayer()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 71);
    var vortex = Place(match, P1, "TfxVortex", suspended: false);
    var foe = Place(match, P2, "FOE", suspended: true);
    RegisterVortex(match, vortex);

    (ChoiceRequest? opt, ChoiceRequest? attack) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is not null, "the pump turn-end drain opened the Vortex optional");
    AssertEqual(ChoiceType.OptionalEffect, opt!.Type, "the pending choice is the AS-IS Vortex optional");
    AssertTrue(attack is not null, "answering 'yes' opened the SelectAttackEffect target select");
    AssertTrue(attack!.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "the suspended opponent Digimon is an offered Vortex target");
    AssertTrue(!attack.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "the PLAYER is NOT a Vortex target without a VortexCanAttackPlayers effect");
}

async Task UnsuspendedOnlyNoWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 71);
    var vortex = Place(match, P1, "TfxVortex", suspended: false);
    Place(match, P2, "FOE", suspended: false); // UNsuspended only -> not a legal Vortex target
    RegisterVortex(match, vortex);

    // The printed Vortex is COLLECTED (the drain ran), but the window does NOT open — CanActivateVortex is false
    // (isVortex does not lift the suspended-defender gate). This exposes trap #1/#3: the drain is not skipped.
    AssertEqual(1, CollectOnEndTurn(match), "the printed Vortex ActivateClass is collected (the drain ran)");
    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is null, "no window opens — CanActivateVortex is false (isVortex does not lift the suspended-defender gate)");
}

async Task SuspendedVortexNoWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 71);
    var vortex = Place(match, P1, "TfxVortex", suspended: true); // already suspended -> cannot attack
    Place(match, P2, "FOE", suspended: true);
    RegisterVortex(match, vortex);

    AssertEqual(1, CollectOnEndTurn(match), "the printed Vortex ActivateClass is collected (the drain ran)");
    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is null, "a suspended Vortex Digimon opens no window");
}

async Task EnablerAllowsPlayerTarget()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 71);
    var vortex = Place(match, P1, "TfxVortex", suspended: false);
    var foe = Place(match, P2, "FOE", suspended: true);
    PlaceEnabler(match, P1, "ENABLER", attackerCondition: null); // accepts any attacker
    RegisterVortex(match, vortex);

    (ChoiceRequest? opt, ChoiceRequest? attack) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is not null && attack is not null, "the Vortex optional opened and resumed to the attack select");
    AssertTrue(attack!.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "the opponent Digimon is still a target");
    AssertTrue(attack.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "the PLAYER is a Vortex target while an IVortexCanAttackPlayersEffect accepts the attacker");
}

async Task EnablerAttackerConditionHonored()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 71);
    var vortex = Place(match, P1, "TfxVortex", suspended: false); // level 4
    var foe = Place(match, P2, "FOE", suspended: true);
    PlaceEnabler(match, P1, "ENABLER", attackerCondition: p => p.Level == 5); // attacker is Lv4 -> no match
    RegisterVortex(match, vortex);

    (ChoiceRequest? opt, ChoiceRequest? attack) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is not null && attack is not null, "the Vortex optional opened via the Digimon and resumed to the attack select");
    AssertTrue(attack!.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "the opponent Digimon is a target (window opened via the Digimon)");
    AssertTrue(!attack.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "attackerCondition not matching -> the player is NOT a target (predicate honored)");
}

async Task EnablerAloneIsNotVortex()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPumpMatchAsync(seed: 71);
    var plain = Place(match, P1, "PLAIN", suspended: false); // no Vortex
    Place(match, P2, "FOE", suspended: true);
    PlaceEnabler(match, P1, "ENABLER", attackerCondition: null);
    RegisterVortex(match, plain);

    AssertEqual(0, CollectOnEndTurn(match),
        "a VortexCanAttackPlayers effect does not grant Vortex — nothing collected");
    (ChoiceRequest? opt, _) = await FireOnEndTurnAsync(match, policy);
    AssertTrue(opt is null, "no end-of-turn window (VortexCanAttackPlayers != Vortex)");
}

// --- Harness (pump α-cluster retarget scaffold) --------------------------

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewPumpMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    PlayerDeckSetup[] decks =
    {
        new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
        new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
    };
    MatchSetupConfig setup = MatchSetupConfig.Create(decks, firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);
    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    await match.InitializeAsync(config);
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
    return (match, policy);
}

// Register the agent-seat handlers, run the pump turn-end (P1 Pass -> EndPhaseAsync -> EndTurnProcess ->
// StackSkillInfos(OnEndTurn) + AutoProcessCheck) and drive to P2's main wait. Returns the captured OnEndTurn
// optional ("Will you use Vortex?") and the VortexProcess SelectAttackEffect target select.
async Task<(ChoiceRequest? Optional, ChoiceRequest? Attack)> FireOnEndTurnAsync(DcgoMatch match, PolicyChoiceProvider policy)
{
    ChoiceRequest? optional = null;
    ChoiceRequest? attack = null;
    policy.On(req => req.Type == ChoiceType.OptionalEffect,
        req => { optional = req; return ChoiceResult.Select(req.Candidates[0].Id); }, oneShot: false);
    policy.On(req => req.Type is ChoiceType.Card or ChoiceType.Permanent,
        req => { attack ??= req; return req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates[0].Id); }, oneShot: false);
    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());
    return (optional, attack);
}

int CollectOnEndTurn(DcgoMatch match)
{
    using var scope = AmbientMatchContext.Enter(match.Context);
    return AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count;
}

void RegisterVortex(DcgoMatch match, HeadlessEntityId id)
{
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(match.Context, id, P1);
}

HeadlessEntityId Place(DcgoMatch match, HeadlessPlayerId owner, string number, bool suspended)
{
    EngineContext ctx = match.Context;
    var cards = (CardDatabase)ctx.CardRepository;
    var def = new HeadlessEntityId(number);
    cards.Upsert(new CardRecord(def, number, number,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{number}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

// A plain permanent carrying ONLY an IVortexCanAttackPlayersEffect (AS-IS VortexCanAttackPlayersStaticEffect) via
// its cEntity — a separate card that lets a Vortex attacker target the player (K1). The Vortex attacker keeps its
// own cEntity (printed Vortex), so this does not disturb the printed effect.
void PlaceEnabler(DcgoMatch match, HeadlessPlayerId owner, string number, Func<Cec.Permanent, bool>? attackerCondition)
{
    var id = Place(match, owner, number, suspended: false);
    using var scope = AmbientMatchContext.Enter(match.Context);
    var cs = new Cec.CardSource(match.Context, id, owner);
    Cec.ICardEffect built = CardEffectFactory.VortexCanAttackPlayersStaticEffect(
        attackerCondition!, false, cs, null!, "VortexCanAttackPlayers");
    cs.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);
}

async Task PassTurnAsync(DcgoMatch match, HeadlessPlayerId player)
{
    LegalAction pass = Legal(match, player).First(a => a.ActionType == HeadlessActionTypes.Pass);
    await ApplyAsync(match, pass);
}

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
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
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

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

// Minimal AS-IS-shaped CEntity_Effect: the seam every ported card definition class uses to surface its printed
// effect list. Returns the single effect at every timing (the continuous VortexCanAttackPlayers is read at None).
sealed class TestCardEntityEffect : Cec.CEntity_Effect
{
    private readonly Cec.ICardEffect _effect;
    public TestCardEntityEffect(Cec.ICardEffect effect) { _effect = effect; }
    public override List<Cec.ICardEffect> CardEffects(Cec.EffectTiming timing, Cec.CardSource cardSource) => new() { _effect };
}

// ═══════════════════════════ providers/context (EXEMPLAR-T1 precedent) ═══════════════════════════

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();
    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));
    public List<string> Seen { get; } = new();
    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        Seen.Add($"{request.Type}:'{request.Message}'x{request.Candidates.Count}");
        for (int i = 0; i < _handlers.Count; i++)
        {
            var (applies, answer, oneShot) = _handlers[i];
            if (applies(request))
            {
                ChoiceResult result = answer(request);
                result.ThrowIfInvalid(request);
                if (oneShot) _handlers.RemoveAt(i);
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
            provider, randomSource, new CardDatabase(), cardInstanceRepository, zoneMover,
            new InMemoryRuleQueryService(), new InMemoryHeadlessTurnController(), choiceController,
            new InMemoryHeadlessAttackController(), memoryController, logSink,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(), effectScheduler,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
