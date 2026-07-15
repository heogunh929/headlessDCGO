using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// GR-006 (rewritten for C-EoT-2): <Vortex> end-of-turn firing is RE-HOUSED to the AS-IS OnEndTurn window and
// RETIRED from the invented EndOfTurnEffectAttack gate.
//
// AS-IS Vortex (VortexProcess / CanActivateVortex, KeyWordEffects/Vortex.cs): the printed VortexSelfEffect
// ActivateClass (returned by the card's CardEffects(OnEndTurn)) is collected by AutoProcessing.GetSkillInfos
// and resolved by MultipleSkills -> VortexProcess -> SelectAttackEffect, offering an attack on an opponent's
// Digimon it CanAttackTargetDigimon(isVortex) (a SUSPENDED Digimon; isVortex does NOT lift the suspended-defender
// gate — only isExecute does, Permanent.cs:2311), and the PLAYER only while an IVortexCanAttackPlayersEffect
// accepts this attacker (CanActivateVortex `|| PermanentHasVortexCanAttackPlayers`).
//
// These tests drive the WINDOW (not the retired gate) and assert:
//   * the gate no longer opens a <Vortex> window (single-fire: window XOR gate);
//   * the window offers a SUSPENDED opponent Digimon, and NOT the player without an enabler;
//   * an IVortexCanAttackPlayersEffect on another permanent makes the PLAYER a target (K1), honoring its
//     attackerCondition;
//   * an UNSUSPENDED-only board opens no window (AS-IS CanAttackTargetDigimon: isVortex != isExecute — the old
//     gate's invented TargetUnsuspended:true for <Vortex> was a divergence, now corrected);
//   * the enabler alone does NOT grant Vortex.
// The full printed+granted firing (both keywords) is witnessed in tests/C-EoT2.

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
    EngineContext context = Context();
    using var scope = AmbientMatchContext.Enter(context);
    var vortex = await Place(context, P1, "TfxVortex", suspended: false);
    await Place(context, P2, "FOE", suspended: true);
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    AssertTrue(AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Any(si => si.CardEffect is ActivateICardEffect),
        "the OnEndTurn window collects the printed Vortex ActivateClass");
    // (G-clean) The invented EndOfTurnEffectAttack gate is physically deleted — single-fire is now proven
    // structurally (the gate class no longer exists); the OnEndTurn window is the sole <Vortex> firing path.
}

async Task WindowTargetsSuspendedNotPlayer()
{
    EngineContext context = Context();
    using var scope = AmbientMatchContext.Enter(context);
    var vortex = await Place(context, P1, "TfxVortex", suspended: false);
    var foe = await Place(context, P2, "FOE", suspended: true);
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    ChoiceRequest attack = await DriveToAttackSelect(context);
    AssertTrue(attack.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "the suspended opponent Digimon is an offered Vortex target");
    AssertTrue(!attack.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "the PLAYER is NOT a Vortex target without a VortexCanAttackPlayers effect");
}

async Task UnsuspendedOnlyNoWindow()
{
    EngineContext context = Context();
    using var scope = AmbientMatchContext.Enter(context);
    var vortex = await Place(context, P1, "TfxVortex", suspended: false);
    await Place(context, P2, "FOE", suspended: false); // UNsuspended only -> not a legal Vortex target
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    await DriveWindow(context);
    AssertTrue(!context.ChoiceController.Current.IsPending,
        "no window opens — CanActivateVortex is false (isVortex does not lift the suspended-defender gate)");
}

async Task SuspendedVortexNoWindow()
{
    EngineContext context = Context();
    using var scope = AmbientMatchContext.Enter(context);
    var vortex = await Place(context, P1, "TfxVortex", suspended: true); // already suspended -> cannot attack
    await Place(context, P2, "FOE", suspended: true);
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    await DriveWindow(context);
    AssertTrue(!context.ChoiceController.Current.IsPending, "a suspended Vortex Digimon opens no window");
}

async Task EnablerAllowsPlayerTarget()
{
    EngineContext context = Context();
    using var scope = AmbientMatchContext.Enter(context);
    var vortex = await Place(context, P1, "TfxVortex", suspended: false);
    var foe = await Place(context, P2, "FOE", suspended: true);
    await PlaceEnabler(context, P1, "ENABLER", attackerCondition: null); // accepts any attacker
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    ChoiceRequest attack = await DriveToAttackSelect(context);
    AssertTrue(attack.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "the opponent Digimon is still a target");
    AssertTrue(attack.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "the PLAYER is a Vortex target while an IVortexCanAttackPlayersEffect accepts the attacker");
}

async Task EnablerAttackerConditionHonored()
{
    EngineContext context = Context();
    using var scope = AmbientMatchContext.Enter(context);
    var vortex = await Place(context, P1, "TfxVortex", suspended: false); // level 4
    var foe = await Place(context, P2, "FOE", suspended: true);
    await PlaceEnabler(context, P1, "ENABLER", attackerCondition: p => p.Level == 5); // attacker is Lv4 -> no match
    CardEffectRegistrar.RegisterCard(context, vortex, P1);

    ChoiceRequest attack = await DriveToAttackSelect(context);
    AssertTrue(attack.Candidates.Any(c => c.Id == foe || c.Label.Contains(foe.Value, StringComparison.Ordinal)),
        "the opponent Digimon is a target (window opened via the Digimon)");
    AssertTrue(!attack.Candidates.Any(c => c.Label.Contains("player", StringComparison.OrdinalIgnoreCase)),
        "attackerCondition not matching -> the player is NOT a target (predicate honored)");
}

async Task EnablerAloneIsNotVortex()
{
    EngineContext context = Context();
    using var scope = AmbientMatchContext.Enter(context);
    var plain = await Place(context, P1, "PLAIN", suspended: false); // no Vortex
    await Place(context, P2, "FOE", suspended: true);
    await PlaceEnabler(context, P1, "ENABLER", attackerCondition: null);
    CardEffectRegistrar.RegisterCard(context, plain, P1);

    AssertEqual(0, AutoProcessing.GetSkillInfos(new Hashtable(), EffectTiming.OnEndTurn).Count,
        "a VortexCanAttackPlayers effect does not grant Vortex — nothing collected");
    await DriveWindow(context);
    AssertTrue(!context.ChoiceController.Current.IsPending, "no end-of-turn window (VortexCanAttackPlayers != Vortex)");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 71, deferredChoice: true);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main); // DoneStartGame true
    return context;
}

async Task DriveWindow(EngineContext context)
{
    AutoProcessing ap = AutoProcessing.For(context);
    try
    {
        await ap.StackSkillInfos(null, EffectTiming.OnEndTurn);
        await ap.AutoProcessCheck();
    }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* parked */ }
}

// Drive the window to the AS-IS "Will you use Vortex?" optional, answer "yes", resume, and return the
// VortexProcess -> SelectAttackEffect target-select choice.
async Task<ChoiceRequest> DriveToAttackSelect(EngineContext context)
{
    await DriveWindow(context);
    AssertTrue(context.ChoiceController.Current.IsPending, "the window opened the Vortex optional");
    ChoiceRequest opt = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.OptionalEffect, opt.Type, "the pending choice is the AS-IS Vortex optional");
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(opt.Candidates[0].Id)); // "yes"
    AutoProcessing ap = AutoProcessing.For(context);
    try { await ap.ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* re-parked on the attack select */ }
    AssertTrue(context.ChoiceController.Current.IsPending, "answering 'yes' opened the SelectAttackEffect target select");
    return context.ChoiceController.PendingRequest!;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string number, bool suspended)
{
    var cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId(number);
    cards.Upsert(new CardRecord(def, number, number,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{number}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = suspended }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// A plain permanent that carries ONLY an IVortexCanAttackPlayersEffect (AS-IS VortexCanAttackPlayersStaticEffect)
// via its cEntity — a separate card that lets a Vortex attacker target the player (K1). The Vortex attacker keeps
// its own cEntity (printed Vortex), so this does not disturb the printed effect.
async Task PlaceEnabler(EngineContext context, HeadlessPlayerId owner, string number, Func<Permanent, bool>? attackerCondition)
{
    var id = await Place(context, owner, number, suspended: false);
    var cs = new CardSource(context, id, owner);
    ICardEffect built = CardEffectFactory.VortexCanAttackPlayersStaticEffect(
        attackerCondition!, false, cs, null!, "VortexCanAttackPlayers");
    cs.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

// Minimal AS-IS-shaped CEntity_Effect: the seam every ported card definition class uses to surface its printed
// effect list. Returns the single effect at every timing (the continuous VortexCanAttackPlayers is read at None).
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
