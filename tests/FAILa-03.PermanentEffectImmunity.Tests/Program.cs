using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #3 (mapping remediation): PermanentEffectFactory.DigimonEffectImmunity / OptionEffectImmunity must
// mirror AS-IS — "immune to the OPPONENT's DIGIMON (resp. OPTION) effects", protecting only this permanent.
// The earlier binding-rule port flattened both to the SAME blanket effect-immunity (ignoring the causing
// effect's owner and type). The AS-IS-shaped factory builds a ContinuousImmunityEffect with the correct
// SkillCondition (opponent + type) and TargetPredicate (this permanent).
//
// (이연④-a) RE-AIMED off the retired ContinuousImmunityGate.BlocksOpponentEffect registry probe onto the LIVE
// AS-IS CardSource.CanNotBeAffected scan (real-driving witness, no registry direct-read — P0-1 lesson). The
// factory's ContinuousImmunityEffect is old-model (registry-only; ToBinding deleted) and therefore NOT yet
// visible to the live ICanNotAffectedEffect scan — so this test LOWERS the factory's own SkillCondition /
// TargetPredicate / Condition into a live CanNotAffectedClass (the exact flip the factory itself will absorb,
// open campaign item RD-IMM-01) and asserts the observable blocking behaviour. This exercises the FACTORY's
// real encoded predicates through the live getter. Presence of the factory's ContinuousImmunityEffect on real
// cards is separately covered by EXEMPLAR-T2B.

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

    // (이연④-a) Lower the AS-IS factory immunity into a LIVE CanNotAffectedClass — the seam CardSource.CanNotBeAffected
    // reads — feeding it the factory's OWN predicates (real-driving; not a registry/predicate direct-read):
    //   CardCondition = TargetPredicate (which permanent is protected), over the protected CardSource;
    //   SkillCondition = SkillCondition (which causing effects), over the causing effect's SOURCE card;
    //   CanUse        = Condition (the factory's live existence gate).
    var protectedCard = new CardSource(ctx, protectedId, P1);
    ICardEffect live = CardEffectFactory.CanNotAffectedStaticEffect(
        permanentCondition: p => immunity.TargetPredicate!(p.TopCard),
        skillCondition: e => e.EffectSourceCard is not null && immunity.SkillCondition!(e.EffectSourceCard),
        isInheritedEffect: immunity.IsInheritedEffect,
        card: immunity.Card,
        condition: immunity.Condition);
    protectedCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(live);

    // AS-IS causing effect: an ActivateClass whose EffectSourceCard is the source card (owner + type decide
    // IsOpponentEffect / IsDigimon, which the factory's SkillCondition keys on).
    var cause = new ActivateClass();
    cause.SetUpICardEffect("cause", _ => true, new CardSource(ctx, sourceId, sourceOwner));

    using var _ = AmbientMatchContext.Enter(ctx);
    bool blocked = protectedCard.CanNotBeAffected(cause);
    AssertTrue(blocked == expectBlocked, $"blocked == {expectBlocked} (grant {grant}, source {(sourceOwner == P1 ? "self" : "opp")} {sourceType})");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 903);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (이연④-a) the live CanNotBeAffected scan calls each immunity's CanUse(null), which gates on
    // TurnStateMachine.DoneStartGame (phase past None/Setup) — set Main so the scan fires.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
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

// Returns the supplied CanNotAffectedClass from the card's live effect list (the seam a ported card definition uses).
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
