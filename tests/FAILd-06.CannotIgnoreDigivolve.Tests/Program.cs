using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: CannotIgnoreDigivolutionCondition (AS-IS BT8_059 "Players can't ignore digivolution requirements") was
// MISSING. It must NEGATE the ignore-colour/ignore-level grants: a digivolve that would be legal by ignoring the
// colour requirement becomes illegal again while the CannotIgnore effect is active.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("baseline: wrong colour + ignore-colour grant -> digivolve is legal", () => Run(grantIgnoreColor: true, cannotIgnore: false, expectLegal: true)),
    ("no ignore grant: wrong colour -> illegal", () => Run(grantIgnoreColor: false, cannotIgnore: false, expectLegal: false)),
    ("CannotIgnore active: the ignore-colour grant is negated -> illegal again", () => Run(grantIgnoreColor: true, cannotIgnore: true, expectLegal: false)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Run(bool grantIgnoreColor, bool cannotIgnore, bool expectLegal)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 926);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    context.MemoryController.Set(5);
    // (R3-W3c-4b D) the CanIgnoreDigivolutionRequirement veto scan gates each effect with CanUse (→ GManager),
    // so drive the digivolve under an ambient match scope (A1/W3c-1 precedent).
    using var _ambient = AmbientMatchContext.Enter(context);

    // Target = Blue Lv4; the evolving card requires Red@4 (colour mismatch). (R3-W3c-4b D) When the CannotIgnore
    // lock is under test, the target battle-area card carries the effect via its definition (the
    // TfxCannotIgnoreDigivolution fixture, dispatched by CardNumber) — the AS-IS-literal live veto scan
    // Player.CanIgnoreDigivolutionRequirement walks the field permanents' EffectList(None); the effect is no
    // longer a registry binding.
    var target = await PlaceBase(context, "BLUEBASE", "Blue", 4, cannotIgnore ? "TfxCannotIgnoreDigivolution" : null);
    var evo = await PlaceEvolve(context, "EVO", "Red@4", 2);

    if (grantIgnoreColor)
    {
        // ignore the COLOUR part of the requirement (level 4 still matches) — makes the digivolve legal.
        context.EffectRegistry.Register(new ContinuousSelfRestrictionEffect(
            new CardSource(context, evo, P1), DigivolveAction.IgnoreColorRequirementKey, false, null).ToBinding("ignore-color"));
    }

    ActionProcessResult result = await new DigivolveAction()
        .ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);

    if (result.IsSuccess != expectLegal)
        throw new InvalidOperationException($"digivolve legal expected {expectLegal}, got {result.IsSuccess} ({result.Message})");
}

async Task<HeadlessEntityId> PlaceBase(EngineContext context, string tag, string color, int level, string? effectCardNumber = null)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, effectCardNumber ?? tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { color }, ["level"] = level, ["dp"] = 3000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceEvolve(EngineContext context, string tag, string requirement, int cost)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId(tag);
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 6000, ["level"] = 5, ["fixedDigivolutionCost"] = cost },
        CardType: "Digimon", EvolutionCondition: requirement));
    var id = new HeadlessEntityId($"p1:hand:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = cost }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Hand));
    return id;
}
