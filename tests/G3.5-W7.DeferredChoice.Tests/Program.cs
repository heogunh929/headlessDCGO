using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W7: effect-driven choices route to the agent. When an effect asks the DeferredChoiceProvider
// for an unanswered choice it registers a pending choice on the controller (surfaced to the agent via
// A2 ResolveChoice) and suspends — the scheduler leaves the effect queued (Suspended status). After
// the agent answers, the effect re-runs from the start, replaying supplied answers, and completes.

HeadlessPlayerId P1 = new(1);

// (④) The scheduler-driven suspend/resume cycle tests (SingleChoiceCycle / SuspendKeepsEffectQueued /
// MultiChoiceCycle / SkipAnswerFlows) were removed: they drove a bound fake IHeadlessCardEffect through
// CardEffectSchedulerResolver.Create(registry, …) — the scheduler resolve-a-bound-effect-body path was
// permanently DELETED (the resolver now always returns Unbound), so the effect body never ran. The surviving
// standalone DeferredChoiceProvider replay behavior (pending register + BeginResolution harvest + replay) is
// retained below.
var tests = new (string Name, Func<Task> Body)[]
{
    ("Standalone provider replays a supplied answer instead of re-deferring", ProviderReplaysAnswer),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

Task ProviderReplaysAnswer()
{
    var controller = new InMemoryHeadlessChoiceController();
    var provider = new DeferredChoiceProvider(controller);
    ChoiceRequest request = BuildRequest(P1, "pick", new HeadlessEntityId("cand0"), canSkip: false);

    // No answer yet -> registers a pending choice and throws the deferral signal.
    AssertThrows<DeferredChoicePendingException>(() => provider.ChooseAsync(request).GetAwaiter().GetResult());
    AssertTrue(controller.Current.IsPending, "pending choice registered");

    // Agent answers; BeginResolution harvests it; the next ChooseAsync replays it.
    controller.ResolveChoice(ChoiceResult.Select(new HeadlessEntityId("cand0")));
    provider.BeginResolution();
    ChoiceResult replayed = provider.ChooseAsync(request).GetAwaiter().GetResult();

    AssertFalse(replayed.IsSkipped, "replayed answer is a selection");
    AssertEqual("cand0", replayed.SelectedIds.Single().Value, "replayed the agent's selection");
    return Task.CompletedTask;
}

// --- Shared request builder ----------------------------------------------

static ChoiceRequest BuildRequest(HeadlessPlayerId player, string message, HeadlessEntityId candidate, bool canSkip)
{
    return new ChoiceRequest(
        ChoiceType.Card,
        player,
        message,
        minCount: canSkip ? 0 : 1,
        maxCount: 1,
        canSkip,
        ChoiceZone.BattleArea,
        new[] { new ChoiceCandidate(candidate, candidate.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: player) });
}

// --- Harness -------------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

static void AssertThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} to be thrown.");
}
