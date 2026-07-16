using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// FAIL-c (mapping remediation): ReturnToLibraryBottomDigivolutionCards was a "trigger not fired" case — the
// OnDigivolutionCardReturnToDeckBottom timing did not exist / was never emitted, and the AS-IS
// TopCard.CanNotBeAffected(CardEffect) guard was dropped. Now the return-to-deck path emits the timing and honours
// the host's general effect immunity.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("returning digivolution cards to the deck fires OnDigivolutionCardReturnToDeckBottom", () => Run(immune: false, expectReturned: true, expectEvent: true)),
    ("a host immune to the opponent's effect keeps its sources (no return, no event)", () => Run(immune: true, expectReturned: false, expectEvent: false)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Run(bool immune, bool expectReturned, bool expectEvent)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 917);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);   // (B군 P0-1) CanUse -> DoneStartGame for the live immunity scan
    var cards = (CardDatabase)ctx.CardRepository;

    // Host Digimon (P1) on the battle area with one digivolution source under it.
    cards.Upsert(new CardRecord(new HeadlessEntityId("H"), "H", "Host", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var host = new HeadlessEntityId("p1:H");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(host, new HeadlessEntityId("H"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, host, ChoiceZone.None, ChoiceZone.BattleArea));

    cards.Upsert(new CardRecord(new HeadlessEntityId("U"), "U", "Under", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var under = new HeadlessEntityId("p1:U");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(under, new HeadlessEntityId("U"), P1));
    SetMeta(ctx, host, DigivolutionStackReader.SourceIdsKey, new[] { under.Value });

    // Causing effect source: an OPPONENT (P2) Digimon, so DigimonEffectImmunity would block it.
    cards.Upsert(new CardRecord(new HeadlessEntityId("S"), "S", "Src", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var src = new HeadlessEntityId("p2:S");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(src, new HeadlessEntityId("S"), P2));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, src, ChoiceZone.None, ChoiceZone.BattleArea));

    if (immune)
    {
        // "This Digimon is unaffected by the opponent's Digimon effects."
        // (B군 P0-1) The return-sources gate (MatchStateMutationSink.cs:1935) now reads the LIVE
        // CardSource.CanNotBeAffected scan, not the retired ContinuousImmunityGate registry. Grant the host a live
        // CanNotAffectedClass on its effect list (opponent's Digimon effects only — the AS-IS DigimonEffectImmunity
        // shape), the seam a ported card uses.
        var hostCard = new CardSource(ctx, host, P1);
        ICardEffect immunity = CardEffectFactory.CanNotAffectedStaticEffect(
            permanentCondition: null,
            skillCondition: e => e.EffectSourceCard is not null && e.EffectSourceCard.Owner != P1 && e.EffectSourceCard.IsDigimon,
            isInheritedEffect: false, card: hostCard, condition: null);
        hostCard.cEntity_EffectController.cEntity_Effect = new LiveImmunityEntityEffect(immunity);
    }

    ctx.GameEventQueue.DrainPending();

    using var _ambient = AmbientMatchContext.Enter(ctx);
    var sink = new MatchStateMutationSink(
        ctx.CardInstanceRepository, ctx.LogSink, ctx.ZoneMover, ctx.MemoryController,
        ctx.EffectRegistry, ctx.GameEventQueue, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.ReturnDigivolutionCardsKind, src,
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
            [MatchStateMutationSink.CountKey] = 1,
            [MatchStateMutationSink.ToDeckKey] = true,
            [MatchStateMutationSink.FromBottomKey] = true,
        }));
    await sink.FlushAsync();

    var zones = (IZoneStateReader)ctx.ZoneMover;
    bool returned = zones.GetCards(P1, ChoiceZone.Library).Contains(under);
    bool stillUnder = ReadSources(ctx, host).Contains(under.Value);
    bool eventFired = ctx.GameEventQueue.DrainPending().Any(e =>
        e.Metadata.TryGetValue(AutoProcessingTriggerCollector.TriggerTimingKey, out object? t)
        && t as string == TriggerTimings.OnDigivolutionCardReturnToDeckBottom);

    AssertTrue(returned == expectReturned, $"source returned to deck == {expectReturned}");
    AssertTrue(stillUnder == !expectReturned, $"source still under host == {!expectReturned}");
    AssertTrue(eventFired == expectEvent, $"OnDigivolutionCardReturnToDeckBottom emitted == {expectEvent}");
}

IReadOnlyList<string> ReadSources(EngineContext ctx, HeadlessEntityId id)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    if (r!.Metadata.TryGetValue(DigivolutionStackReader.SourceIdsKey, out object? raw) && raw is System.Collections.IEnumerable items and not string)
    {
        return items.Cast<object?>().Select(x => x?.ToString() ?? "").ToList();
    }

    return new List<string>();
}

void SetMeta(EngineContext ctx, HeadlessEntityId id, string key, object? value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    ctx.CardInstanceRepository.Upsert(r! with
    {
        Metadata = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { [key] = value }
    });
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

// Returns the supplied CanNotAffectedClass from the host's live effect list (the seam a ported card definition uses).
sealed class LiveImmunityEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public LiveImmunityEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
