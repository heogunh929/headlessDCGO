using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-D3: simultaneously-collected triggers are ORDERED before resolving — turn-player triggers first, then
// non-turn (AS-IS MultipleSkills' TurnPlayerSkillInfos/NonTurnPlayerSkillInfos split, MultipleSkills.cs:125-145).
// (Stage 5) among ONE player's simultaneous triggers the controlling player CHOOSES the order (RD-14/15), and an
// optional is confirmed yes/no when picked (RD-13); both still fire when accepted.
//
// (수리-2 re-aim) The old harness registered raw EffectRegistry bindings under fake timing strings ("T"/"M"/"O")
// and published a synthetic GameEvent — the RETIRED old-model collector seam (AutoProcessingTriggerCollector has
// no live caller since the window cutover; SkillWindowSupply drops non-EffectTiming strings). Reconstructed on the
// LIVE seam: new-model ActivateClass probes surfaced via each card's CEntity_EffectController (the FAILa-02
// pinning idiom), collected+resolved through the real window drive — AutoProcessing.StackSkillInfos(OnEndTurn) +
// AutoProcessCheck (the AS-IS EndTurnProcess pair, same drive as the green C-EoT2 suite). Both ordering rule
// assertions are preserved unchanged.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
List<string> resolveOrder = new();

var tests = new (string Name, Func<Task> Body)[]
{
    ("Turn-player triggers resolve before non-turn-player triggers", TurnPlayerPriority),
    ("Mandatory triggers resolve before optional, and optionals still fire", MandatoryBeforeOptional),
};

var failures = new List<string>();
foreach (var test in tests)
{
    resolveOrder.Clear();
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task TurnPlayerPriority()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);

    // Register the NON-turn player's probe FIRST; the MultipleSkills split must still resolve the turn
    // player's (P1) trigger before the non-turn player's (P2).
    PlaceProbe(context, P2, "fx-P2", optional: false);
    PlaceProbe(context, P1, "fx-P1", optional: false);

    await FireEndTurnWindowAsync(context);

    AssertOrder("fx-P1", "fx-P2");
}

async Task MandatoryBeforeOptional()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);

    PlaceProbe(context, P1, "fx-opt", optional: true);
    PlaceProbe(context, P1, "fx-mand", optional: false);

    // (Stage 5) the two simultaneous P1 triggers are the player's to ORDER — the window opens a choice rather
    // than draining them in a fixed order. Drive the agent picking the mandatory first, then accepting the
    // optional at its yes/no confirm; both fire, in the chosen order.
    await FireEndTurnWindowAsync(context);
    AssertTrue(context.ChoiceController.Current.IsPending, "the window opened an order choice for the two simultaneous triggers");

    await ResolveWindowChoiceAsync(context, PickCandidateContaining(context, "fx-mand"));
    for (int i = 0; i < 6 && context.ChoiceController.Current.IsPending; i++)
    {
        // The remaining optional surfaces its pick / yes-no confirm — accept (non-skip candidate).
        await ResolveWindowChoiceAsync(context, FirstNonSkipCandidate(context));
    }

    AssertOrder("fx-mand", "fx-opt");
}

// --- Live-seam drive ------------------------------------------------------

// The AS-IS end-of-turn window pair (EndTurnProcess → StackSkillInfos(OnEndTurn) + AutoProcessCheck). An
// interactive pause (the Stage-5 order choice / optional confirm) parks as a pending agent choice.
async Task FireEndTurnWindowAsync(EngineContext context)
{
    var autoProcessing = AutoProcessing.For(context);
    await autoProcessing.StackSkillInfos(new Hashtable(), EffectTiming.OnEndTurn);
    try { await autoProcessing.AutoProcessCheck(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* parked */ }
}

// Resolve the pending window choice through the ACTION PROCESSOR (the agent seat) — it owns the full
// record-answer + resume protocol for the parked MultipleSkills continuation.
async Task ResolveWindowChoiceAsync(EngineContext context, ChoiceResult answer)
{
    HeadlessPlayerId chooser = context.ChoiceController.PendingRequest!.PlayerId;
    var result = await new MetadataActionProcessor().ProcessAsync(
        HeadlessActionFactory.ResolveChoice(chooser, answer), context);
    if (!result.IsSuccess)
    {
        throw new InvalidOperationException($"ResolveChoice failed: {result.Message}");
    }
}

ChoiceResult PickCandidateContaining(EngineContext context, string token)
{
    ChoiceRequest request = context.ChoiceController.PendingRequest
        ?? throw new InvalidOperationException("no pending choice");
    ChoiceCandidate candidate = request.Candidates.FirstOrDefault(c => c.Id.Value.Contains(token, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"no candidate containing '{token}' among [{string.Join(", ", request.Candidates.Select(c => c.Id.Value))}]");
    return ChoiceResult.Select(candidate.Id);
}

ChoiceResult FirstNonSkipCandidate(EngineContext context)
{
    ChoiceRequest request = context.ChoiceController.PendingRequest
        ?? throw new InvalidOperationException("no pending choice");
    ChoiceCandidate? candidate = request.Candidates.FirstOrDefault(c => !c.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    return candidate is null ? ChoiceResult.Skip() : ChoiceResult.Select(candidate.Id);
}

// --- Harness --------------------------------------------------------------

EngineContext NewContext()
{
    // deferredChoice: interactive window pauses surface as agent choices (the C-Del-3C1C context shape).
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74, deferredChoice: true);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

// Place a battle-area Digimon whose CEntity_EffectController carries an order-recording OnEndTurn ActivateClass
// (the FAILa-02 pinning idiom — a live new-model effect the GetSkillInfos field scan collects).
void PlaceProbe(EngineContext context, HeadlessPlayerId owner, string name, bool optional)
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"def:{name}");
    cards.Upsert(new CardRecord(defId, name, name,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{name}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea))
        .GetAwaiter().GetResult();

    var card = new CardSource(context, id, owner);
    card.cEntity_EffectController.cEntity_Effect = new D3OrderProbe(name, optional, resolveOrder);
}

void AssertOrder(params string[] expected)
{
    if (resolveOrder.Count != expected.Length || !resolveOrder.SequenceEqual(expected))
    {
        throw new InvalidOperationException(
            $"resolution order: expected [{string.Join(", ", expected)}], got [{string.Join(", ", resolveOrder)}].");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

// A dispatch-less CEntity_Effect exposing ONE OnEndTurn ActivateClass that records its resolution order.
internal sealed class D3OrderProbe : CEntity_Effect
{
    private readonly string _name;
    private readonly bool _optional;
    private readonly List<string> _order;

    public D3OrderProbe(string name, bool optional, List<string> order)
    {
        _name = name;
        _optional = optional;
        _order = order;
    }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var cardEffects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEndTurn)
        {
            var activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(_name, _ => true, card);
            activateClass.SetUpActivateClass(
                _ => true,
                _ => { _order.Add(_name); return Task.CompletedTask; },
                -1, _optional, _name);
            activateClass.SetIsInheritedEffect(false);
            cardEffects.Add(activateClass);
        }

        return cardEffects;
    }
}
