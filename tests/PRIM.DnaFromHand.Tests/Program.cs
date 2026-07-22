// PRIM special-play: effect-driven DNA Digivolution from hand/trash — DNA-digivolve into a hand card by fusing
// a battle-area permanent with a hand material (AS-IS DNADigivolveWithHandOrTrashCardIntoHandOrTrash).
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("into-card + permanent + hand material present: fuses into the hand card", DnaPlays),
    ("no eligible permanent: nothing fuses (into stays in hand)", NoPermNoPlay),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task DnaPlays()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, "TfxDnaFromHand", "Digimon", ChoiceZone.BattleArea);
    var into = await Put(ctx, "INTO", "Digimon", ChoiceZone.Hand);
    var perm = await Put(ctx, "PERM", "Digimon", ChoiceZone.BattleArea);
    var mat = await Put(ctx, "MAT", "Digimon", ChoiceZone.Hand);

    await ActivatedEffectResolver.ResolveAsync(ctx, src, P1, EffectTiming.OptionSkill);

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, into), "the into-card is on the battle area");
    AssertTrue(!InZone(ctx, ChoiceZone.Hand, mat), "the hand material left the hand (fused)");
    AssertTrue(!InZone(ctx, ChoiceZone.BattleArea, perm), "the permanent left the battle area (fused)");
    var stack = DigivolutionStackReader.Read(ctx.CardInstanceRepository, ctx.CardRepository, into);
    AssertTrue(stack.UnderCards.Any(s => s.InstanceId == perm) && stack.UnderCards.Any(s => s.InstanceId == mat),
        "the permanent and material are sources under the into-card");
}

async Task NoPermNoPlay()
{
    EngineContext ctx = Ctx();
    var src = await Put(ctx, "TfxDnaFromHand", "Digimon", ChoiceZone.BattleArea);
    var into = await Put(ctx, "INTO", "Digimon", ChoiceZone.Hand);
    await Put(ctx, "MAT", "Digimon", ChoiceZone.Hand);   // no PERM on battle
    await ActivatedEffectResolver.ResolveAsync(ctx, src, P1, EffectTiming.OptionSkill);
    AssertTrue(InZone(ctx, ChoiceZone.Hand, into), "no permanent -> the into-card stayed in hand (no fusion)");
}

// --- Harness ---
EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 76);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);   // past None -> DoneStartGame true (AS-IS ICardEffect.CanTrigger gate)
    return ctx;
}
async Task<HeadlessEntityId> Put(EngineContext ctx, string cardNumber, string cardType, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId($"DEF:{cardNumber}"), cardNumber, cardNumber, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: cardType));
    var id = new HeadlessEntityId($"1:{zone}:{cardNumber}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{cardNumber}"), P1, Metadata: new Dictionary<string, object?>()));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone));
    return id;
}
bool InZone(EngineContext ctx, ChoiceZone z, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, z).Contains(id);
static void AssertTrue(bool v, string l) { if (!v) throw new InvalidOperationException($"{l}: expected true."); }
