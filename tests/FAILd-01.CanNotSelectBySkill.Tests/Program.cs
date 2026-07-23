using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
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
    // (R3-W3c-4c D-1) the live getter Permanent.CanSelectBySkill gates each effect with CanUse (→ CanTrigger, which
    // requires DoneStartGame i.e. a live phase; → IsDisabled → GManager), so build the request under an ambient
    // match scope in a live phase (FAILd-06 precedent).
    ctx.TurnController.SetPhase(HeadlessDCGO.Engine.Headless.Runtime.HeadlessPhase.Main);
    using var _ambient = AmbientMatchContext.Enter(ctx);
    var cards = (CardDatabase)ctx.CardRepository;

    var b = new HeadlessEntityId("p2:B");      // B is the one we protect (id used in the predicate below)

    // (R3-W3c-4c D-1) the untargetability is now the AS-IS kind-class CanNotSelectBySkillClass carried on the
    // PROTECTOR's LIVE EffectList (no registry binding); SelectPermanentEffect's untargetability gate is the
    // AS-IS-literal live scan Permanent.CanSelectBySkill over field permanents' EffectList(None). The protector
    // card dispatches to the TfxCanNotSelectBySkill fixture by CardNumber, whose static Predicate slot the harness
    // sets to the joint AS-IS predicate CanNotSelectBySkill(candidate, skillSource) before placing.
    TfxCanNotSelectBySkill.Predicate = restrict switch
    {
        RestrictKind.GatedOpponentOnly => (candidate, skill) => candidate.InstanceId == b && skill.Owner == P2,
        RestrictKind.Ungated => (candidate, skill) => candidate.InstanceId == b,
        _ => null,
    };

    HeadlessEntityId Mk(HeadlessPlayerId owner, string tag, string? cardNumber = null)
    {
        cards.Upsert(new CardRecord(new HeadlessEntityId(tag), cardNumber ?? tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
        var id = new HeadlessEntityId($"{(owner == P1 ? "p1" : "p2")}:{tag}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(tag), owner));
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
        return id;
    }

    var a = Mk(P1, "A");
    _ = Mk(P2, "B");                                                   // B is the one we protect
    var protector = Mk(P1, "PROT", cardNumber: "TfxCanNotSelectBySkill"); // grants the untargetability (live)
    _ = protector;

    // The selecting skill's source instance (owned by skillOwner).
    var skillId = new HeadlessEntityId(skillOwner == 1 ? "p1:A" : "p2:B");

    // (RD-IDFLIP-01 batch 4) canonical AS-IS route: GManager.GetComponent (AttachContext under the ambient scope
    // opened above) + the 11-param Func<Permanent,bool> SetUp. The selecting skill is a bare cause resolving
    // skillId to its owner — exactly what the retired id-form SetUp built internally for the untargetability scan.
    var select = GManager.instance!.GetComponent<SelectPermanentEffect>();
    select.SetUp(
        selectPlayer: P1,
        canTargetCondition: _ => true,
        canTargetCondition_ByPreSelecetedList: null,
        canEndSelectCondition: null,
        maxCount: 2,
        canNoSelect: true,
        canEndNotMax: true,
        selectPermanentCoroutine: null,
        afterSelectPermanentCoroutine: null,
        mode: SelectPermanentEffect.Mode.Custom,
        cardEffect: BareCauseEffect.For(ctx, skillId));
    ChoiceRequest request = select.BuildRequest((IZoneStateReader)ctx.ZoneMover, new[] { P1, P2 });
    return request.Candidates;
}

enum RestrictKind { None, Ungated, GatedOpponentOnly }
