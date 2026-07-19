using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-d: CanNotMove (AS-IS ICanNotMoveEffect / Permanent.CanMove) was MISSING. CanNotMoveStaticEffect now
// registers a continuous restriction the breeding move-gate honours.
//
// (4b A-1) RE-POINTED to the pump-dispatch seat (§3.1f/§3.4c c4 judgment): the OLD direct dispatcher call
// (the retired HeadlessLegalActionDispatcher OLD arm, §1.3 항11) is replaced by the pump. Under the cutover pump the
// breeding move is offered through the AS-IS ChoiceType.BreedingDecision choice-pause, which gates on
// Player.CanMove -> Permanent.CanMove (the AS-IS-literal ICanNotMoveEffect scan). With the breeding area
// occupied by B (so CanHatch is false), the decision opens IFF the move is legal — so "move offered" is
// observed as the BreedingDecision opening (candidate label "move"), and "move NOT offered" as the pump
// auto-flowing past breeding to the main wait. No dispatcher OLD-arm / AdvancePhase currency.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task<bool>> Body)[]
{
    ("no restriction: the breeding Digimon may move", async () => await MoveOffered(restrict: false)),
    ("CanNotMove: the move is NOT offered", async () => !await MoveOffered(restrict: true)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { if (await t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task<bool> MoveOffered(bool restrict)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 923);
    var cards = (CardDatabase)ctx.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(ctx, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 923, setup: setup));
    await ExpStepOnce(match);

    // Drive to the first pump pause (StartGame Mulligan), then stage B as a live, movable breeding-area Digimon.
    for (int i = 0; i < 32 && !match.HasPendingChoice() && !AtMainWait(match, P1); i++)
    {
        await ExpStepOnce(match);
    }

    var b = new HeadlessEntityId("p1:B");
    // (R3-W3c-4c D-1) the move restriction is the AS-IS kind-class CanNotMoveClass carried on B's LIVE EffectList;
    // Permanent.CanMove is the AS-IS-literal ICanNotMoveEffect scan (CanNotMove(candidate, causing), causing null).
    // B dispatches to the TfxCanNotMove fixture by CardNumber, whose static Predicate slot is the joint AS-IS
    // predicate CanNotMove(candidate, causing).
    TfxCanNotMove.Predicate = restrict ? (candidate, _) => candidate.InstanceId == b : null;
    cards.Upsert(new CardRecord(new HeadlessEntityId("B"), restrict ? "TfxCanNotMove" : "B", "B",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 4 }, CardType: "Digimon"));
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(b, new HeadlessEntityId("B"), P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, b, ChoiceZone.None, ChoiceZone.BreedingArea));

    // Auto-skip any prior pump choice (mulligans); stop at the breeding decision (= move offered) OR the main
    // wait (= the pump auto-flowed past breeding because the move was not legal).
    for (int i = 0; i < 64; i++)
    {
        if (match.HasPendingChoice())
        {
            HeadlessChoiceState choice = match.Context.ChoiceController.Current;
            if (choice.Type == ChoiceType.BreedingDecision)
            {
                // The breeding area is occupied (CanHatch is false), so the decision opens only when a MOVE is
                // legal — its sole candidate is the move.
                return match.Context.ChoiceController.PendingRequest!.Candidates.Any(c => c.Label == "move");
            }

            await ExpApply(match, SkipAction(match, match.Context.ChoiceController.PendingRequest!.PlayerId));
        }
        else if (AtMainWait(match, P1))
        {
            return false; // pump flowed past breeding without offering a move
        }
        else
        {
            await ExpStepOnce(match);
        }
    }

    throw new InvalidOperationException("pump did not reach a breeding decision or the main wait");
}

// --- Harness (pump) ------------------------------------------------------

LegalAction SkipAction(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player)
        .First(a => a.ActionType == HeadlessActionTypes.ResolveChoice && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
}

static bool AtMainWait(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

static async Task ExpStepOnce(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static async Task ExpApply(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());
