using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// joint-migration Phase 1: JointRestrictionEffect + RestrictionScan proven end-to-end via CannotAttack, using a
// NON-SEPARABLE predicate f(attacker, defender) — "cannot attack a defender with the SAME owner as the attacker"
// — which couples both arguments and CANNOT be expressed by the legacy split (attacker-scope ∧ defender-predicate).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<bool> Body)[]
{
    ("no restriction: attacker may attack any defender", () => !Restricted(joint: false, sameOwnerDefender: false)),
    ("joint (same-owner) restriction + different-owner defender: NOT restricted", () => !Restricted(joint: true, sameOwnerDefender: false)),
    ("joint (same-owner) restriction + same-owner defender: RESTRICTED (coupled predicate)", () => Restricted(joint: true, sameOwnerDefender: true)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { if (t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

bool Restricted(bool joint, bool sameOwnerDefender)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 930);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;

    HeadlessEntityId Mk(HeadlessPlayerId owner, string tag)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId(tag), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000 }, CardType: "Digimon"));
        var id = new HeadlessEntityId($"{owner.Value}:{tag}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(tag), owner));
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
        return id;
    }

    var attacker = Mk(P1, "ATK");
    var defender = Mk(sameOwnerDefender ? P1 : P2, "DEF");

    if (joint)
    {
        // coupled: cannot attack a defender whose owner equals the attacker's owner.
        ctx.EffectRegistry.Register(CardEffectFactory.CanNotAttackJointStaticEffect(
            (atk, def) => def is not null && def.Owner == atk.Owner, new CardSource(ctx, attacker, P1)).ToBinding("joint-cna"));
    }

    return ContinuousRestrictionGate.EvaluateAttack(ctx, attacker, defender).IsRestricted;
}
