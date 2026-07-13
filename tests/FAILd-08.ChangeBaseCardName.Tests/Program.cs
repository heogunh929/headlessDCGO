using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: ChangeBaseCardName (AS-IS BT14_097 "original name is [Sukamon]") was MISSING. It REPLACES the printed
// name for all name matching (CardSource.CardNames / EqualsCardName), mirroring the BaseCardColors two-stage fold.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var (context, card) = Setup();
var cs = new CardSource(context, card, P1);

// Capture the BEFORE state eagerly (before the effect is registered).
bool beforeOriginal = cs.EqualsCardName("Original");
bool beforeSukamon = cs.EqualsCardName("Sukamon");

// Apply ChangeBaseCardName -> Sukamon (replace).
// ChangeBaseCardNameStaticEffect builds a NEW-model kind-class (CardEffects.ChangeBaseCardNameClass) which,
// post-rebuild, has no ToBinding/EffectRegistry bridge (stage-B RED elsewhere). BUT the gate this test actually
// exercises (CardSource.EqualsCardName -> CardNames -> BaseCardNames) is a genuine AS-IS LIVE SCAN over
// cEntity_EffectController.GetCardEffects (CardSource.cs / CEntity_EffectController.cs), not the substrate
// EffectRegistry — so it is directly wireable via the already-ported `cEntity_Effect` settable property (the
// same seam every AS-IS card definition class, e.g. `class BT1_001 : CEntity_Effect`, uses), with no engine change.
ICardEffect changeNameEffect = CardEffectFactory.ChangeBaseCardNameStaticEffect("Sukamon", cs);
cs.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(changeNameEffect);

var after = new (string Name, Func<bool> Body)[]
{
    ("before: matched its printed name 'Original'", () => beforeOriginal),
    ("before: did NOT match 'Sukamon'", () => !beforeSukamon),
    ("after: matches the new name 'Sukamon'", () => cs.EqualsCardName("Sukamon")),
    ("after: the printed name 'Original' is REPLACED (no longer matches)", () => !cs.EqualsCardName("Original")),
    ("after: ContainsCardName fragment 'Suka' matches", () => cs.ContainsCardName("Suka")),
};

var failures = new List<string>();
foreach (var t in after)
{
    try { if (t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{after.Length} test(s) passed.");

(EngineContext, HeadlessEntityId) Setup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 928);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId("ORIG"), "ORIG", "Original",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:ORIG");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("ORIG"), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return (ctx, id);
}

// Minimal AS-IS-shaped CEntity_Effect: the same seam every ported card definition class (e.g. `class BT1_001 :
// CEntity_Effect`) uses to surface its printed effect list to CardSource.EffectList/EffectList_ExceptAddedEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;

    public TestCardEntityEffect(ICardEffect effect)
    {
        _effect = effect;
    }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
