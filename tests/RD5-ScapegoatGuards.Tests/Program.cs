using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-5) AS-IS Scapegoat.cs — the sacrifice candidate must be an owner-battle-area DIGIMON that isn't the
// holder (CanSelectPermanentCondition, :53), and Scapegoat does NOT trigger when the deletion was caused by
// the owner's OWN effect (CanUseCondition, :65-73). Previously the headless port accepted any battle-area
// card (Tamer/Option included) and offered Scapegoat on own-effect deletions too.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

async Task<(EngineContext ctx, HeadlessEntityId holder, HeadlessEntityId digimonAlly, HeadlessEntityId tamerAlly)> Setup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 53);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("DIGI"), "DIGI", "Digi",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("TAMER"), "TAMER", "Tamer",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));

    var holder = new HeadlessEntityId("p1:HOLDER");
    var digimonAlly = new HeadlessEntityId("p1:DIGI");
    var tamerAlly = new HeadlessEntityId("p1:TAMER");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(holder, new HeadlessEntityId("DIGI"), P1,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { [DeletionReplacementGate.HasScapegoatKey] = true }));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(digimonAlly, new HeadlessEntityId("DIGI"), P1));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(tamerAlly, new HeadlessEntityId("TAMER"), P1));
    foreach (var id in new[] { holder, digimonAlly, tamerAlly })
    {
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));
    }
    return (ctx, holder, digimonAlly, tamerAlly);
}

// --- 1. Candidate filter: Digimon ally qualifies, Tamer ally does not. ---
{
    var (ctx, holder, digimonAlly, tamerAlly) = await Setup();
    var zones = (IZoneStateReader)ctx.ZoneMover;
    ctx.CardInstanceRepository.TryGetInstance(holder, out CardInstanceRecord? holderRec);

    IReadOnlyList<HeadlessEntityId> candidates = DeletionReplacementGate.FindScapegoatSacrificeCandidates(
        ctx.CardInstanceRepository, zones, holderRec!, candidateCondition: null, effectRegistry: ctx.EffectRegistry, context: ctx);

    Check(candidates.Contains(digimonAlly), "a Digimon ally is a valid Scapegoat sacrifice");
    Check(!candidates.Contains(tamerAlly), "a Tamer ally is NOT a valid Scapegoat sacrifice (Digimon-only)");
    Check(!candidates.Contains(holder), "the holder itself is never a sacrifice candidate");
    Check(candidates.Count == 1, "exactly the one Digimon ally qualifies");
}

// --- 2. Own-effect gate: Scapegoat is offered for a non-own-effect (opponent/battle) deletion, suppressed
//        when the deletion was by the owner's own effect. ---
{
    var timing = new DeletionReplacementTiming();

    // Mark the holder pending-deletion (the state IsPreAwaiting requires) with the given cause flags.
    void MarkPendingDeletion(EngineContext ctx, HeadlessEntityId holder, bool byOwnEffect)
    {
        ctx.CardInstanceRepository.TryGetInstance(holder, out CardInstanceRecord? rec);
        var meta = new Dictionary<string, object?>(rec!.Metadata, StringComparer.Ordinal)
        {
            [GameFlowProcessor.PendingDeletionKey] = true,
        };
        if (byOwnEffect)
        {
            meta[DeletionReplacementGate.DeletedByOwnEffectKey] = true;
        }

        ctx.CardInstanceRepository.Upsert(rec with { Metadata = meta });
    }

    var (ctx1, holder1, _, _) = await Setup();
    MarkPendingDeletion(ctx1, holder1, byOwnEffect: false);
    Check(timing.IsPreAwaiting(ctx1, holder1),
        "Scapegoat pre-window is offered when NOT deleted by the owner's own effect");

    var (ctx2, holder2, _, _) = await Setup();
    MarkPendingDeletion(ctx2, holder2, byOwnEffect: true);
    Check(!timing.IsPreAwaiting(ctx2, holder2),
        "Scapegoat pre-window is SUPPRESSED when deleted by the owner's own effect");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-5 Scapegoat-guard checks passed.");
