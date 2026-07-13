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
    // MIGRATION-NOTE (P7 test-fix): TreatAsDigimonClass (Assets/Scripts/Script/CardEffects/
    // TreatAsDigimonClass.cs) is a new-model kind-class with no ToBinding/EffectRegistry bridge (stage-B RED,
    // docs/audit/rebuild_p6_stageA_notes.md). ContinuousKeywordGate.IsDigimon reads only the substrate
    // EffectRegistry binding path, not this kind-class's ITreatAsDigimonEffect interface (the engine's stage-B
    // live is-scan serves real ported cards, not a synthetic fixture card), so there is no buildable way to make this grant observable yet. This VERIFY
    // probe has no assertions (it only prints LIVE/DEAD/SEAL diagnostics), so the un-registered call below is
    // expected to print "DEAD/SEAL" until stage B lands — tracked, not silently weakened.
    CardEffectFactory.TreatAsDigimonStaticEffect(
        permanentCondition: null, isInheritedEffect: false, new CardSource(ctx, id, P1), condition: null);
    results.Add(("TreatAsDigimon -> IsDigimon", before, ContinuousKeywordGate.IsDigimon(ctx, id)));
}

// 2. VortexCanAttackPlayers: HasKeyword should flip false -> true.
{
    var (ctx, id) = Card("V", "Digimon");
    bool before = ContinuousKeywordGate.HasKeyword(ctx, id, ContinuousKeywordGate.VortexCanAttackPlayers);
    // MIGRATION-NOTE (P7 test-fix): VortexCanAttackPlayersClass (Assets/Scripts/Script/CardEffects/
    // VortexCanAttackPlayersClass.cs) is a new-model kind-class with no ToBinding/EffectRegistry bridge
    // (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). ContinuousKeywordGate.HasKeyword reads only the
    // substrate EffectRegistry binding path for this keyword, not this kind-class's
    // IVortexCanAttackPlayersEffect interface (the engine's stage-B live is-scan serves real ported cards, not a
    // synthetic fixture card), so there is no buildable way to make this grant observable yet. This VERIFY probe has no assertions (it only prints
    // LIVE/DEAD/SEAL diagnostics), so the un-registered call below is expected to print "DEAD/SEAL" until
    // stage B lands — tracked, not silently weakened.
    CardEffectFactory.VortexCanAttackPlayersStaticEffect(
        attackerCondition: null, isInheritedEffect: false, new CardSource(ctx, id, P1), condition: null, effectName: "VortexCanAttackPlayers");
    results.Add(("VortexCanAttackPlayers -> HasKeyword", before, ContinuousKeywordGate.HasKeyword(ctx, id, ContinuousKeywordGate.VortexCanAttackPlayers)));
}

// 3. Scapegoat: HasKeyword should flip false -> true.
{
    var (ctx, id) = Card("S", "Digimon");
    bool before = ContinuousKeywordGate.HasKeyword(ctx, id, ContinuousKeywordGate.Scapegoat);
    // MIGRATION-NOTE (P7 test-fix): ScapegoatClass (Assets/Scripts/Script/CardEffects/ScapegoatClass.cs) is a
    // new-model kind-class with no ToBinding/EffectRegistry bridge (stage-B RED,
    // docs/audit/rebuild_p6_stageA_notes.md). ContinuousKeywordGate.HasKeyword reads only the substrate
    // EffectRegistry binding path for this keyword, not this kind-class's IScapegoatEffect interface
    // (the engine's stage-B live is-scan serves real ported cards, not a synthetic fixture card), so there is no buildable way to make this grant
    // observable yet. This VERIFY probe has no assertions (it only prints LIVE/DEAD/SEAL diagnostics), so the
    // un-registered call below is expected to print "DEAD/SEAL" until stage B lands — tracked, not silently
    // weakened.
    CardEffectFactory.ScapegoatStaticEffect(
        permanentCondition: null, isInheritedEffect: false, new CardSource(ctx, id, P1), condition: null);
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
