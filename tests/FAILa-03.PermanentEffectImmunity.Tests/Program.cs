using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #3 (mapping remediation): PermanentEffectFactory.DigimonEffectImmunity / OptionEffectImmunity must
// mirror AS-IS — "immune to the OPPONENT's DIGIMON (resp. OPTION) effects", protecting only this permanent.
// The earlier binding-rule port flattened both to the SAME blanket effect-immunity (ignoring the causing
// effect's owner and type). The AS-IS-shaped overloads build a ContinuousImmunityEffect with the correct
// SkillCondition (opponent + type) and TargetPredicate (this permanent).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("DigimonEffectImmunity blocks OPPONENT's DIGIMON effect", () => Check(Grant.Digimon, sourceOwner: P2, sourceType: "Digimon", expectBlocked: true)),
    ("DigimonEffectImmunity does NOT block opponent's OPTION effect", () => Check(Grant.Digimon, sourceOwner: P2, sourceType: "Option", expectBlocked: false)),
    ("DigimonEffectImmunity does NOT block OWN Digimon effect", () => Check(Grant.Digimon, sourceOwner: P1, sourceType: "Digimon", expectBlocked: false)),
    ("OptionEffectImmunity blocks OPPONENT's OPTION effect", () => Check(Grant.Option, sourceOwner: P2, sourceType: "Option", expectBlocked: true)),
    ("OptionEffectImmunity does NOT block opponent's DIGIMON effect", () => Check(Grant.Option, sourceOwner: P2, sourceType: "Digimon", expectBlocked: false)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Check(Grant grant, HeadlessPlayerId sourceOwner, string sourceType, bool expectBlocked)
{
    EngineContext ctx = Ctx();
    var protectedId = await Place(ctx, P1, "PROT", "Digimon");
    var sourceId = await Place(ctx, sourceOwner, "SRC", sourceType);

    var permanent = new Permanent(ctx, protectedId, P1);
    ContinuousImmunityEffect immunity = grant == Grant.Digimon
        ? PermanentEffectFactory.DigimonEffectImmunity(permanent)
        : PermanentEffectFactory.OptionEffectImmunity(permanent);
    ctx.EffectRegistry.Register(immunity.ToBinding($"imm:{protectedId.Value}"));

    bool blocked = ContinuousImmunityGate.BlocksOpponentEffect(
        ctx.EffectRegistry, ctx.CardInstanceRepository, protectedId, sourceId, ctx);
    AssertTrue(blocked == expectBlocked, $"blocked == {expectBlocked} (grant {grant}, source {(sourceOwner == P1 ? "self" : "opp")} {sourceType})");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 903);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, string cardType)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

enum Grant { Digimon, Option }
