using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// FAIL-a #6/#7 (mapping remediation): DigivolveInto* IgnoreRequirement must be the AS-IS enum (None/All/Level/
// Color), not a bool. Level waives the LEVEL requirement but KEEPS color; Color waives color but keeps level;
// None enforces both. The port previously collapsed to a bool that waived the whole requirement, so a
// Level-ignore effect would wrongly allow a wrong-COLOR digivolution. Verified at the cost/requirement gate
// (DigivolutionCostHelpers.TryResolveCost with ignoreLevel/ignoreColor).

var tests = new (string Name, Func<bool> Body)[]
{
    // Evolving card requires base = Red@5. Target is Red level 4 (color OK, level mismatch).
    ("None: level mismatch -> ineligible", () => !Resolve(ReqColor("Red", 5), Target("Red", 4), ignoreLevel: false, ignoreColor: false)),
    ("Level: level ignored, color Red matches -> eligible", () => Resolve(ReqColor("Red", 5), Target("Red", 4), ignoreLevel: true, ignoreColor: false)),
    // Evolving card requires base = Blue@4. Target is Red level 4 (level OK, color mismatch).
    ("Level: color still checked (Blue != Red) -> ineligible", () => !Resolve(ReqColor("Blue", 4), Target("Red", 4), ignoreLevel: true, ignoreColor: false)),
    ("Color: color ignored, level 4 matches -> eligible", () => Resolve(ReqColor("Blue", 4), Target("Red", 4), ignoreLevel: false, ignoreColor: true)),
    ("None: color mismatch -> ineligible", () => !Resolve(ReqColor("Blue", 4), Target("Red", 4), ignoreLevel: false, ignoreColor: false)),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { if (t.Body()) Console.WriteLine($"PASS {t.Name}"); else { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}"); } }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

static bool Resolve(CardRecord evolving, CardRecord target, bool ignoreLevel, bool ignoreColor) =>
    DigivolutionCostHelpers.TryResolveCost(evolving, null, target, null, out _, out _, ignoreLevel: ignoreLevel, ignoreColor: ignoreColor);

// Evolving card whose digivolution requirement is "base must be <color> level <level>", cost 2.
static CardRecord ReqColor(string color, int level)
{
    var req = new Dictionary<string, object?>(StringComparer.Ordinal) { ["cost"] = 2, ["level"] = level, ["targetColor"] = color };
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["digivolutionCosts"] = new object?[] { req },
    };
    return new CardRecord(new HeadlessEntityId($"EVO:{color}:{level}"), "EVO", "Evo", meta, CardType: "Digimon");
}

static CardRecord Target(string color, int level)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["level"] = level,
        ["colors"] = new[] { color },
    };
    return new CardRecord(new HeadlessEntityId($"BASE:{color}:{level}"), "BASE", "Base", meta, CardType: "Digimon");
}
