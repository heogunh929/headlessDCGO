using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G1E-005 — choice pause/resume contract.
//
// (4b B6 은퇴, §3.4f/§3.4i) The RequestChoice/ResolveChoice/ClearChoice AGENT-ACTION pause subtests were the
// OLD step driver's choice-INJECTION affordance (the await-mode counterpart of the retired throw-replay
// contract): a real match never has the agent inject a synthetic choice — choices are OPENED by effects and
// surfaced by the pump (HasPendingChoice -> ResolveChoice lanes). The pending-choice STATE contract those
// subtests carried is covered live by the pump choice tests (EXEMPLAR/W1b/G3.5-W7 DeferredChoice/G12-002/
// G12-004) — what survives HERE is the driver-agnostic ChoiceController duplicate-pending guard below.
// Retired with their verification target: the goal-row CSV / predecessor-doc / AS-IS read-only reference /
// integration-file sniff assertions (invented test-infra, F62 precedent).

var tests = new (string Name, Func<Task> Body)[]
{
    ("Choice controller rejects direct duplicate request and preserves pending request", ChoiceControllerRejectsDirectDuplicateRequestAndPreservesPendingRequest),
};

var failures = new List<string>();

foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.GetType().Name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Length} test(s) passed.");

Task ChoiceControllerRejectsDirectDuplicateRequestAndPreservesPendingRequest()
{
    var player = new HeadlessPlayerId(1);
    var controller = new InMemoryHeadlessChoiceController();
    ChoiceRequest original = CardRequest(player, "Original");
    ChoiceRequest replacement = CardRequest(player, "Replacement");

    controller.RequestChoice(original, new HeadlessEntityId("choice-original"));

    InvalidOperationException ex = ExpectThrows<InvalidOperationException>(
        () => controller.RequestChoice(replacement, new HeadlessEntityId("choice-replacement")));

    AssertTrue(ex.Message.Contains("another choice is pending", StringComparison.Ordinal), "duplicate message");
    AssertSame(original, controller.PendingRequest, "pending request");
    AssertEqual("choice-original", controller.Current.RequestId?.Value, "request id");
    AssertEqual("Original", controller.Current.Message, "message");
    return Task.CompletedTask;
}

static ChoiceRequest CardRequest(HeadlessPlayerId player, string message)
{
    return new ChoiceRequest(
        ChoiceType.Card,
        player,
        message,
        minCount: 1,
        maxCount: 1,
        canSkip: false,
        ChoiceZone.Hand,
        new[]
        {
            new ChoiceCandidate(new HeadlessEntityId("card-a"), "Card A", ChoiceZone.Hand, IsSelectable: true, player),
            new ChoiceCandidate(new HeadlessEntityId("card-b"), "Card B", ChoiceZone.Hand, IsSelectable: true, player),
        });
}

static TException ExpectThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException ex)
    {
        return ex;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
    }
}

static void AssertSame<T>(T expected, T? actual, string label)
    where T : class
{
    if (!ReferenceEquals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected same reference.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value)
    {
        throw new InvalidOperationException($"{label}: expected true.");
    }
}
