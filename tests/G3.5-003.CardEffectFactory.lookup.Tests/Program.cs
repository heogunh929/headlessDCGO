using System.Reflection;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

var root = FindRepositoryRoot();

var tests = new (string Name, Func<Task> Body)[]
{
    ("Player/context Lookup overload returns matched rules", LookupOverloadReturnsMatchedRules),
    ("Bind carries the requesting player's controller id", BindCarriesRequestingPlayerController),
    ("Lookup overload uses the supplied controller, not hardcoded player 1", LookupUsesSuppliedController),
    ("Hardcoded two-argument Lookup overload was removed (B-01)", LegacyLookupIsObsolete),
    ("Lookup matches rules with an explicit controller (B-01 hardcode removed)", LegacyLookupStillMatches),
    ("Lookup overload validates the context argument", LookupOverloadValidatesContext),
    ("Binding source has no Unity dependency or placeholder", BindingSourceHasNoUnityOrPlaceholder),
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

Task LookupOverloadReturnsMatchedRules()
{
    CardEffectFactoryBindingRegistry registry = CreateRegistry();
    CardRecord card = CreateCard("BT-CARD");
    var player = new HeadlessPlayerId(2);

    IReadOnlyList<CardEffectFactoryBindingRule> rules = registry.Lookup(
        card,
        "OnPlay",
        player,
        new EffectContext(player, card.Id));

    AssertEqual(1, rules.Count, "matched rule count");
    AssertEqual("rule-blocker", rules[0].Id, "matched rule id");
    return Task.CompletedTask;
}

Task BindCarriesRequestingPlayerController()
{
    CardEffectFactoryBindingRegistry registry = CreateRegistry();
    CardRecord card = CreateCard("BT-CARD");

    foreach (int playerNumber in new[] { 1, 2 })
    {
        var player = new HeadlessPlayerId(playerNumber);
        CardEffectFactoryBindingResult result = registry.Bind(
            new CardEffectFactoryBindingRequest(card, "OnPlay", card.Id, player, new EffectContext(player, card.Id)));

        AssertTrue(result.IsSuccess, $"bind success p{playerNumber}");
        AssertEqual(playerNumber, result.Bindings[0].Request.ControllerId.Value, $"controller id p{playerNumber}");
    }

    return Task.CompletedTask;
}

Task LookupUsesSuppliedController()
{
    CardEffectFactoryBindingRegistry registry = CreateRegistry();
    CardRecord card = CreateCard("BT-CARD");
    var player = new HeadlessPlayerId(2);
    var context = new EffectContext(player, card.Id);

    // The rule list is player-agnostic, but the matched rule's factory must produce a
    // player-2 binding when bound through the same request the overload would build.
    IReadOnlyList<CardEffectFactoryBindingRule> rules = registry.Lookup(card, "OnPlay", player, context);
    AssertEqual(1, rules.Count, "matched rule count");

    CardEffectFactoryBindingResult result = registry.Bind(
        new CardEffectFactoryBindingRequest(card, "OnPlay", card.Id, player, context));
    AssertEqual(2, result.Bindings[0].Request.ControllerId.Value, "controller id from overload context");
    return Task.CompletedTask;
}

Task LegacyLookupIsObsolete()
{
    // B-01: the player-1-hardcoded two-argument Lookup(card, trigger) overload has been removed.
    MethodInfo? method = typeof(CardEffectFactoryBindingRegistry).GetMethod(
        "Lookup",
        new[] { typeof(CardRecord), typeof(string) });

    AssertTrue(method is null, "two-argument hardcoded overload no longer exists");
    return Task.CompletedTask;
}

Task LegacyLookupStillMatches()
{
    CardEffectFactoryBindingRegistry registry = CreateRegistry();
    CardRecord card = CreateCard("BT-CARD");

    // B-01: the player-1-hardcoded overload was removed; lookups now pass the controller explicitly.
    var controller = new HeadlessPlayerId(1);
    IReadOnlyList<CardEffectFactoryBindingRule> rules =
        registry.Lookup(card, "OnPlay", controller, new EffectContext(controller, card.Id));

    AssertEqual(1, rules.Count, "matched rule count");
    AssertEqual("rule-blocker", rules[0].Id, "matched rule id");
    return Task.CompletedTask;
}

Task LookupOverloadValidatesContext()
{
    CardEffectFactoryBindingRegistry registry = CreateRegistry();
    CardRecord card = CreateCard("BT-CARD");

    ExpectThrows<ArgumentNullException>(() => registry.Lookup(card, "OnPlay", new HeadlessPlayerId(2), null!));
    return Task.CompletedTask;
}

Task BindingSourceHasNoUnityOrPlaceholder()
{
    string path = Path.Combine(
        root,
        "src",
        "HeadlessDCGO.Engine",
        "Headless",
        "Effects",
        "CardEffectFactoryBinding.cs");

    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Binding source was not found: {path}");
    }

    string text = File.ReadAllText(path);
    AssertTrue(!text.Contains("UnityEngine", StringComparison.Ordinal), "no Unity dependency");
    AssertTrue(!text.Contains("TODO", StringComparison.Ordinal), "no TODO marker");
    AssertTrue(!text.Contains("NotImplementedException", StringComparison.Ordinal), "no NotImplementedException");
    // B-01: the player-1 hardcode is fully removed — no controller is assumed anywhere in the source.
    AssertTrue(!text.Contains("new HeadlessPlayerId(1)", StringComparison.Ordinal), "no hardcoded controller");
    return Task.CompletedTask;
}

static CardEffectFactoryBindingRegistry CreateRegistry()
{
    return CardEffectFactoryBinding.CreateRegistry(new[]
    {
        CardEffectFactoryBinding.BindKeywordBaseBatch1(
            "rule-blocker",
            new[] { "BT-CARD" },
            "OnPlay",
            KeywordBaseBatch1Kind.Blocker),
    });
}

static CardRecord CreateCard(string cardNumber)
{
    return new CardRecord(
        new HeadlessEntityId("card-entity"),
        cardNumber,
        "Lookup Test Card",
        new Dictionary<string, object?>(),
        CardType: "Digimon",
        PlayCost: 3,
        EffectBindingKey: null);
}

static void ExpectThrows<TException>(Action action)
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

static void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Assertion failed: {label}");
    }
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Assertion failed: {label}. Expected '{expected}', got '{actual}'.");
    }
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "src"))
            && Directory.Exists(Path.Combine(directory.FullName, "docs")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Repository root with 'src' and 'docs' was not found.");
}
