using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

// (MIG7 goal-7) The mirror SelectCountEffect component (count-picker over the pre-built ChoiceType.Count
// substrate): BuildRequest produces a valid Count request; ReadSelectedCount reads the resolved count.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Action Body)[]
{
    ("BuildRequest without canNoSelect requires 1..max", MandatoryRange),
    ("BuildRequest with canNoSelect allows 0..max", OptionalRange),
    ("ReadSelectedCount returns the resolved count", ReadsCount),
    ("ReadSelectedCount returns 0 for an empty/skip resolution", ReadsZero),
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

void MandatoryRange()
{
    var sel = new SelectCountEffect();
    sel.SetUp(P1, maxCount: 3, canNoSelect: false);
    ChoiceRequest req = sel.BuildRequest();
    AssertTrue(req.Type == ChoiceType.Count, "Count choice type");
    AssertEqual(1, req.MinCount, "min 1 (0 not allowed)");
    AssertEqual(3, req.MaxCount, "max 3");
}

void OptionalRange()
{
    var sel = new SelectCountEffect();
    sel.SetUp(P1, maxCount: 2, canNoSelect: true);
    ChoiceRequest req = sel.BuildRequest();
    AssertEqual(0, req.MinCount, "min 0 (canNoSelect)");
    AssertEqual(2, req.MaxCount, "max 2");
}

void ReadsCount()
{
    AssertEqual(2, SelectCountEffect.ReadSelectedCount(ChoiceResult.SelectCount(2)), "reads 2");
}

void ReadsZero()
{
    AssertEqual(0, SelectCountEffect.ReadSelectedCount(ChoiceResult.Skip()), "skip -> 0");
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
