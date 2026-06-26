using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-W7: effect-driven choices route to the agent. When an effect asks the DeferredChoiceProvider
// for an unanswered choice it registers a pending choice on the controller (surfaced to the agent via
// A2 ResolveChoice) and suspends — the scheduler leaves the effect queued (Suspended status). After
// the agent answers, the effect re-runs from the start, replaying supplied answers, and completes.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("A single effect choice suspends, surfaces, then resumes to completion", SingleChoiceCycle),
    ("A suspended effect stays queued and the choice is surfaced", SuspendKeepsEffectQueued),
    ("Two distinct choices take two answer cycles, then complete in order", MultiChoiceCycle),
    ("A skip answer flows through the deferred provider", SkipAnswerFlows),
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

async Task SingleChoiceCycle()
{
    var harness = new Harness(P1, choiceCount: 1);
    harness.EnqueueEffect();

    // Pass 1: the effect asks for choice 0 -> suspends.
    await harness.Scheduler.ResolveAllAsync();
    AssertTrue(harness.Controller.Current.IsPending, "choice surfaced to the agent");
    AssertEqual(0, harness.Effect.CompletionCount, "effect not completed while suspended");
    AssertEqual(1, harness.Scheduler.PendingCount, "effect remains queued");

    // The agent answers the pending choice.
    harness.AgentAnswersPending();

    // Pass 2: the effect re-runs, replays the answer, completes.
    await harness.Scheduler.ResolveAllAsync();
    AssertEqual(1, harness.Effect.CompletionCount, "effect completed after the answer");
    AssertEqual(0, harness.Scheduler.PendingCount, "effect dequeued once resolved");
    AssertFalse(harness.Controller.Current.IsPending, "no pending choice remains");
    AssertEqual(1, harness.Effect.CompletedChoices.Count, "one choice recorded");
}

async Task SuspendKeepsEffectQueued()
{
    var harness = new Harness(P1, choiceCount: 1);
    harness.EnqueueEffect();

    IReadOnlyList<EffectResult> results = await harness.Scheduler.ResolveAllAsync();

    AssertEqual(1, results.Count, "one resolution attempt");
    AssertTrue(results[0].IsSuspended, "result status is Suspended");
    AssertFalse(results[0].Resolved, "suspended result is not Resolved");
    AssertEqual(0, harness.Scheduler.TotalResolvedCount, "nothing counted as resolved yet");
    AssertTrue(harness.Controller.PendingRequest is not null, "pending request is the effect's choice");
}

async Task MultiChoiceCycle()
{
    var harness = new Harness(P1, choiceCount: 2);
    harness.EnqueueEffect();

    await harness.Scheduler.ResolveAllAsync();          // suspend on choice 0
    AssertEqual(0, harness.Effect.CompletionCount, "still running");
    harness.AgentAnswersPending();                       // answer 0

    await harness.Scheduler.ResolveAllAsync();          // replay 0, suspend on choice 1
    AssertEqual(0, harness.Effect.CompletionCount, "second choice still pending");
    AssertTrue(harness.Controller.Current.IsPending, "second choice surfaced");
    harness.AgentAnswersPending();                       // answer 1

    await harness.Scheduler.ResolveAllAsync();          // replay 0 and 1, complete
    AssertEqual(1, harness.Effect.CompletionCount, "effect completed after both answers");
    AssertEqual(2, harness.Effect.CompletedChoices.Count, "two choices recorded in order");
    AssertEqual(0, harness.Scheduler.PendingCount, "effect dequeued");
}

async Task SkipAnswerFlows()
{
    var harness = new Harness(P1, choiceCount: 1, canSkip: true);
    harness.EnqueueEffect();

    await harness.Scheduler.ResolveAllAsync();
    harness.Controller.ResolveChoice(ChoiceResult.Skip());

    await harness.Scheduler.ResolveAllAsync();
    AssertEqual(1, harness.Effect.CompletionCount, "effect completed after a skip");
    AssertTrue(harness.Effect.CompletedChoices[0].IsSkipped, "recorded choice is a skip");
}

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

internal sealed class Harness
{
    private readonly HeadlessPlayerId _player;
    private readonly EffectRequest _request;

    public Harness(HeadlessPlayerId player, int choiceCount, bool canSkip = false)
    {
        _player = player;
        Controller = new InMemoryHeadlessChoiceController();
        Provider = new DeferredChoiceProvider(Controller);
        Effect = new ChoosingEffect("w7-fx", "w7-src", Provider, choiceCount, canSkip, player);

        var registry = new InMemoryEffectRegistry();
        _request = new EffectRequest(
            new HeadlessEntityId("w7-fx"), player, "OnPlay",
            new EffectContext(player, player, new HeadlessEntityId("w7-src"), triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>()));
        registry.Register(new EffectBinding(_request, effect: Effect));

        Scheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                registry,
                sinkFactory: _ => new RecordingEffectMutationSink(),
                choiceCoordinator: Provider));
    }

    public InMemoryHeadlessChoiceController Controller { get; }

    public DeferredChoiceProvider Provider { get; }

    public ChoosingEffect Effect { get; }

    public EffectScheduler Scheduler { get; }

    public void EnqueueEffect() => Scheduler.Enqueue(_request, EffectResolutionMode.MainStack);

    public void AgentAnswersPending()
    {
        ChoiceRequest pending = Controller.PendingRequest
            ?? throw new InvalidOperationException("No pending choice to answer.");

        ChoiceResult answer = pending.CanSkip && pending.MinCount == 0
            ? ChoiceResult.Skip()
            : ChoiceResult.Select(pending.SelectableCandidates.First().Id);

        Controller.ResolveChoice(answer);
    }
}

internal sealed class ChoosingEffect : IHeadlessCardEffect
{
    private readonly IChoiceProvider _provider;
    private readonly int _choiceCount;
    private readonly bool _canSkip;
    private readonly HeadlessPlayerId _player;

    public ChoosingEffect(string effectId, string sourceId, IChoiceProvider provider, int choiceCount, bool canSkip, HeadlessPlayerId player)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId), new HeadlessEntityId(sourceId), name: effectId, timing: "OnPlay");
        _provider = provider;
        _choiceCount = choiceCount;
        _canSkip = canSkip;
        _player = player;
    }

    public CardEffectDefinition Definition { get; }

    public int CompletionCount { get; private set; }

    public IReadOnlyList<ChoiceResult> CompletedChoices { get; private set; } = Array.Empty<ChoiceResult>();

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public async ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        // Choose-then-apply: collect every choice into a LOCAL list and only commit to instance state
        // once all choices are answered, so a re-run after a suspension never double-applies.
        var local = new List<ChoiceResult>();
        for (int index = 0; index < _choiceCount; index++)
        {
            var candidate = new HeadlessEntityId($"cand{index}");
            ChoiceRequest request = new(
                ChoiceType.Card,
                _player,
                $"choice {index}",
                minCount: _canSkip ? 0 : 1,
                maxCount: 1,
                _canSkip,
                ChoiceZone.BattleArea,
                new[] { new ChoiceCandidate(candidate, candidate.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: _player) });

            local.Add(await _provider.ChooseAsync(request, cancellationToken).ConfigureAwait(false));
        }

        CompletedChoices = local;
        CompletionCount++;
        return EffectResult.Success("all choices resolved");
    }
}
