using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// VERIFY (b): are the "producer stub" keyword grants (audit) actually live end-to-end, or dead?
HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var results = new List<(string, bool, bool)>(); // (name, before, after)

// 1. TreatAsDigimon on a TAMER: IsDigimon should flip false -> true.
{
    var (ctx, id) = Card("TAMER", "Tamer");
    bool before = ContinuousKeywordGate.IsDigimon(ctx, id);
    ctx.EffectRegistry.Register(CardEffectFactory.TreatAsDigimonStaticEffect(
        permanentCondition: null, isInheritedEffect: false, new CardSource(ctx, id, P1), condition: null).ToBinding($"t:{id.Value}"));
    results.Add(("TreatAsDigimon -> IsDigimon", before, ContinuousKeywordGate.IsDigimon(ctx, id)));
}

// 2. VortexCanAttackPlayers: HasKeyword should flip false -> true.
{
    var (ctx, id) = Card("V", "Digimon");
    bool before = ContinuousKeywordGate.HasKeyword(ctx, id, ContinuousKeywordGate.VortexCanAttackPlayers);
    ctx.EffectRegistry.Register(CardEffectFactory.VortexCanAttackPlayersStaticEffect(
        attackerCondition: null, isInheritedEffect: false, new CardSource(ctx, id, P1), condition: null).ToBinding($"v:{id.Value}"));
    results.Add(("VortexCanAttackPlayers -> HasKeyword", before, ContinuousKeywordGate.HasKeyword(ctx, id, ContinuousKeywordGate.VortexCanAttackPlayers)));
}

// 3. Scapegoat: HasKeyword should flip false -> true.
{
    var (ctx, id) = Card("S", "Digimon");
    bool before = ContinuousKeywordGate.HasKeyword(ctx, id, ContinuousKeywordGate.Scapegoat);
    ctx.EffectRegistry.Register(CardEffectFactory.ScapegoatStaticEffect(
        permanentCondition: null, isInheritedEffect: false, new CardSource(ctx, id, P1), condition: null).ToBinding($"s:{id.Value}"));
    results.Add(("Scapegoat -> HasKeyword", before, ContinuousKeywordGate.HasKeyword(ctx, id, ContinuousKeywordGate.Scapegoat)));
}

foreach (var (name, before, after) in results)
{
    Console.WriteLine($"{name}: before={before} after={after}  => {(!before && after ? "LIVE" : "DEAD/SEAL")}");
}

(EngineContext, HeadlessEntityId) Card(string tag, string type)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 919);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(tag), tag, tag, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: type));
    var id = new HeadlessEntityId($"p1:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(tag), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return (ctx, id);
}
