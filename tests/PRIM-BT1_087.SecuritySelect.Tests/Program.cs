// (이연③-d) Witness for BT1_087 [On Play] after retiring the invented
// SecuritySelectToHandColorRecoveryShuffleEffect. AS-IS inline: SelectCardEffect(root: Security, mode: AddHand)
// reveals+adds the picked security card to hand; if it is Yellow, <Recovery +1 (Deck)> puts the top deck card
// onto security; then the security stack is shuffled. Two arms: (1) a Yellow pick -> to hand AND recovery fires;
// (2) a non-Yellow pick -> to hand, NO recovery (negative colour control).
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Yellow security card picked -> to hand AND <Recovery +1> fires (deck top -> security)", YellowPickRecovers),
    ("Non-Yellow security card picked -> to hand, NO recovery (negative colour control)", NonYellowPickNoRecovery),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests --------------------------------------------------------------------

async Task YellowPickRecovers()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId host = Battle(ctx, "BT1_087", "Yellow Tamer");
    HeadlessEntityId secYellow = Security(ctx, "SEC_Y", "Yellow");
    HeadlessEntityId secBlue = Security(ctx, "SEC_B", "Blue");
    HeadlessEntityId deckTop = Library(ctx, "DECK1");

    Script(ctx, ChoiceResult.Select(secYellow));
    await DriveOnPlay(ctx, host);

    AssertTrue(InZone(ctx, P1, ChoiceZone.Hand, secYellow), "the picked Yellow security card was added to hand");
    AssertFalse(InZone(ctx, P1, ChoiceZone.Hand, secBlue), "the unchosen security card stayed put");
    AssertTrue(InZone(ctx, P1, ChoiceZone.Security, deckTop), "<Recovery +1> moved the deck-top card onto security");
    AssertFalse(InZone(ctx, P1, ChoiceZone.Library, deckTop), "the recovered card left the library");
}

async Task NonYellowPickNoRecovery()
{
    EngineContext ctx = NewContext();
    HeadlessEntityId host = Battle(ctx, "BT1_087", "Yellow Tamer");
    HeadlessEntityId secYellow = Security(ctx, "SEC_Y", "Yellow");
    HeadlessEntityId secBlue = Security(ctx, "SEC_B", "Blue");
    HeadlessEntityId deckTop = Library(ctx, "DECK1");

    Script(ctx, ChoiceResult.Select(secBlue));
    await DriveOnPlay(ctx, host);

    AssertTrue(InZone(ctx, P1, ChoiceZone.Hand, secBlue), "the picked Blue security card was added to hand");
    AssertTrue(InZone(ctx, P1, ChoiceZone.Library, deckTop), "no recovery: the deck-top card stayed in the library");
    AssertFalse(InZone(ctx, P1, ChoiceZone.Security, deckTop), "no recovery: nothing added to security from the deck");
}

// --- Harness ------------------------------------------------------------------

async Task DriveOnPlay(EngineContext ctx, HeadlessEntityId host)
{
    using var scope = AmbientMatchContext.Enter(ctx);
    var card = new CardSource(ctx, host, P1);
    var effects = new BT1_087().CardEffects(EffectTiming.OnEnterFieldAnyone, card);
    var ht = new System.Collections.Hashtable();
    foreach (ICardEffect e in effects)
    {
        if (e is ActivateICardEffect ae && e.CanActivate(ht))
        {
            await ae.Activate(ht);
        }
    }
}

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 1087);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

void Script(EngineContext ctx, params ChoiceResult[] choices)
{
    var p = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    p.Clear();
    foreach (var c in choices) { p.Enqueue(c); }
}

HeadlessEntityId Battle(EngineContext ctx, string number, string name)
{
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, name,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));
    var id = new HeadlessEntityId($"p1:{number}:battle");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId Security(EngineContext ctx, string number, string colour)
{
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, number,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { colour } }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:{number}:sec");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Security)).GetAwaiter().GetResult();
    return id;
}

HeadlessEntityId Library(EngineContext ctx, string number)
{
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(new HeadlessEntityId(number), number, number,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" } }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"p1:{number}:lib");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.Library)).GetAwaiter().GetResult();
    return id;
}

bool InZone(EngineContext ctx, HeadlessPlayerId p, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(p, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
