using ST1RedTests;

// Card group CardEffect/ST1/Red — all 12 ported cards (group-standard project; see card_group_standard.md).
// Consolidates the former P1-ST1.Red{Wave1,Triggers,Activated,TimedBuff} projects into one, grouped by
// effect family as static test classes.

var groups = new (string Group, (string Name, Func<Task> Body)[] Cases)[]
{
    ("Continuous", Wave1Tests.Cases),
    ("Triggers", TriggerTests.Cases),
    ("Activated", ActivatedTests.Cases),
    ("TimedBuff", TimedBuffTests.Cases),
};

var failures = new List<string>();
int total = 0;
foreach (var (group, cases) in groups)
{
    foreach (var (name, body) in cases)
    {
        total++;
        try { await body(); Console.WriteLine($"PASS {group}/{name}"); }
        catch (Exception ex)
        {
            failures.Add($"{group}/{name}");
            Console.Error.WriteLine($"FAIL {group}/{name}");
            Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
        }
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{total} test(s) passed.");
