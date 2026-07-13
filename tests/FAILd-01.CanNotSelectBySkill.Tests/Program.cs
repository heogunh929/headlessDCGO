using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: CanNotSelectBySkill (untargetability, AS-IS 39 cards) was MISSING — no producer, so a permanent could
// never be made untargetable. CanNotSelectBySkillStaticEffect now registers a candidate-scoped restriction that
// SelectPermanentEffect.BuildRequest excludes from the choice candidate pool (AS-IS Permanent.CanSelectBySkill).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<bool> Body)[]
{
    ("no restriction: B is selectable", () => HasB(Candidates(restrict: RestrictKind.None))),
    ("ungated CanNotSelectBySkill on B: B is excluded from candidates", () => !HasB(Candidates(restrict: RestrictKind.Ungated))),
    ("gated (opponent-only) + selecting skill is the OWNER's: B still selectable", () => HasB(Candidates(restrict: RestrictKind.GatedOpponentOnly, skillOwner: 1))),
    ("gated (opponent-only) + selecting skill is the OPPONENT's: B is excluded", () => !HasB(Candidates(restrict: RestrictKind.GatedOpponentOnly, skillOwner: 2))),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { if (t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

bool HasB(IReadOnlyList<ChoiceCandidate> c) => c.Any(x => x.Id == new HeadlessEntityId("p2:B"));

IReadOnlyList<ChoiceCandidate> Candidates(RestrictKind restrict, int skillOwner = 1)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 921);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;

    HeadlessEntityId Mk(HeadlessPlayerId owner, string tag)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId(tag), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var id = new HeadlessEntityId($"{(owner == P1 ? "p1" : "p2")}:{tag}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(tag), owner));
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
        return id;
    }

    var a = Mk(P1, "A");
    var b = Mk(P2, "B");                       // B is the one we protect
    var protector = Mk(P1, "PROT");            // the card that grants the untargetability

    if (restrict != RestrictKind.None)
    {
        // (true-scan) AS-IS joint predicate CanNotSelectBySkill(candidate, skillSource): B is protected;
        // the gated case additionally requires the selecting skill to be the opponent's.
        Func<CardSource, CardSource, bool> predicate = restrict == RestrictKind.GatedOpponentOnly
            ? (candidate, skill) => candidate.InstanceId == b && skill.Owner == P2
            : (candidate, skill) => candidate.InstanceId == b;
        // CanNotSelectBySkillStaticEffect is declared to return the abstract ICardEffect base (its concrete
        // result is the OLD-model CanNotSelectBySkillEffect, which DOES implement ToBinding — just not through
        // the ICardEffect static type), so ToBinding is reached via the LegacyBindingBridge reflective dispatch
        // rather than a direct call.
        ICardEffect canNotSelectBySkill = CardEffectFactory.CanNotSelectBySkillStaticEffect(
            predicate, new CardSource(ctx, protector, P1), condition: null);
        if (!LegacyBindingBridge.TryToBinding(canNotSelectBySkill, "cnsbs", out HeadlessDCGO.Engine.Headless.Effects.EffectBinding? canNotSelectBinding) || canNotSelectBinding is null)
        {
            throw new InvalidOperationException("expected a legacy ToBinding-capable effect from CanNotSelectBySkillStaticEffect");
        }
        ctx.EffectRegistry.Register(canNotSelectBinding);
    }

    // The selecting skill's source instance (owned by skillOwner).
    var skillId = new HeadlessEntityId(skillOwner == 1 ? "p1:A" : "p2:B");

    var select = new SelectPermanentEffect();
    select.SetUp(P1, _ => true, maxCount: 2, canNoSelect: true, canEndNotMax: true, SelectPermanentEffect.Mode.Custom, skillId, ctx);
    ChoiceRequest request = select.BuildRequest((IZoneStateReader)ctx.ZoneMover, new[] { P1, P2 });
    return request.Candidates;
}

enum RestrictKind { None, Ungated, GatedOpponentOnly }
