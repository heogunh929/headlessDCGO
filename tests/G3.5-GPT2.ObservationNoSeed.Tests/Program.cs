using HeadlessDCGO.Engine.Headless.Runtime;

// GPT-#2: the RNG seed must NOT be in the default RL observation (it is a debug/diagnostic field, not
// a learnable signal). It is gated behind ObservationEncodingOptions.IncludeRandomSeed (default off).

const string SeedFeature = "runtime.randomSeed";
const string SeedKnownFeature = "runtime.randomSeed.known";

ObservationSnapshot snapshot = ObservationSnapshot.Empty with { RandomSeed = 1234567 };

var tests = new (string Name, Action Body)[]
{
    ("Default observation excludes the random seed", DefaultExcludesSeed),
    ("IncludeRandomSeed opt-in adds the seed features", OptInIncludesSeed),
    ("Opt-in is exactly two features longer than default", OptInAddsTwoFeatures),
    ("Other runtime flags remain in the default observation", OtherRuntimeFlagsRemain),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { test.Body(); Console.WriteLine($"PASS {test.Name}"); }
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

void DefaultExcludesSeed()
{
    IReadOnlyList<string> names = FeatureNames(ObservationEncodingOptions.Default);
    AssertFalse(names.Contains(SeedFeature), "default observation has no runtime.randomSeed");
    AssertFalse(names.Contains(SeedKnownFeature), "default observation has no runtime.randomSeed.known");
}

void OptInIncludesSeed()
{
    IReadOnlyList<string> names = FeatureNames(ObservationEncodingOptions.Default with { IncludeRandomSeed = true });
    AssertTrue(names.Contains(SeedFeature), "opt-in observation has runtime.randomSeed");
    AssertTrue(names.Contains(SeedKnownFeature), "opt-in observation has runtime.randomSeed.known");
}

void OptInAddsTwoFeatures()
{
    int defaultLen = new ObservationEncoder(ObservationEncodingOptions.Default).Encode(snapshot).ToVector().Length;
    int withSeedLen = new ObservationEncoder(ObservationEncodingOptions.Default with { IncludeRandomSeed = true })
        .Encode(snapshot).ToVector().Length;
    AssertEqual(defaultLen + 2, withSeedLen, "seed opt-in adds exactly two features");
}

void OtherRuntimeFlagsRemain()
{
    IReadOnlyList<string> names = FeatureNames(ObservationEncodingOptions.Default);
    // Removing the seed must not drop the rest of the runtime-flags block.
    AssertTrue(names.Contains("runtime.isTerminal"), "isTerminal still present");
    AssertTrue(names.Contains("runtime.lastActionSucceeded.known"), "lastActionSucceeded still present");
    AssertTrue(names.Contains("runtime.cardInstanceCount"), "cardInstanceCount still present");
}

// --- Helpers -------------------------------------------------------------

IReadOnlyList<string> FeatureNames(ObservationEncodingOptions options) =>
    new ObservationEncoder(options).Encode(snapshot).Features.Select(f => f.Name).ToArray();

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
