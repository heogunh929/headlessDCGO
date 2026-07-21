// G1R-001 (R6-Da'-1): RETIREMENT LEDGER scan — retirement-confirmed symbols carried over to a later batch
// must not GAIN references. Each ledger row pins the reference-count baseline measured at the R6-Da'-1 batch
// close (whole-word occurrences across src/ + tests/ *.cs, excluding bin/obj and this project). A count ABOVE
// baseline means someone wired a NEW consumer onto a retired symbol (the ExpireAttackEnd-precedent accident
// this suite exists to catch) -> FAIL. A count AT or BELOW baseline passes (shrinking is the goal); a drop to
// 0 is reported as "ready for deletion". Update a baseline ONLY when a disposal batch intentionally lowers it.
// The source-level guard is the paired [Obsolete("RD-RETIRE-DA1: ...")] attributes on the same symbols.

using System.Text.RegularExpressions;

// symbol, pinned baseline, owning disposal batch (where the symbol actually dies).
var ledger = new (string Symbol, int Baseline, string Batch)[]
{
    ("AsUniformActivated", 8, "Da'-3/6 (buff/restriction factory seats flip)"),
    ("ActivatedSelectEffect", 22, "Da'-5 / corpus deletion (EX8_074 RD-R6-07 STOP + fixtures + white-box casts)"),
    ("ActivatedSelectBounceAndDiscardSourcesEffect", 7, "corpus deletion R6-Db (re-target C3-Witness case (9) first)"),
    ("ActivatedSelectTrashDigivolutionEffect", 6, "A6 (deleted with the ST2.Blue disposal)"),
    ("SelectAndDeDigivolveEffect", 4, "Da'-5 (helper dies with the body)"),
    ("ActivatedSelectAndDeDigivolveEffect", 7, "Da'-5 (resolver-switch collapse)"),
};

string root = FindRepoRoot();
Console.WriteLine($"repo root: {root}");

List<string> files = new();
foreach (string top in new[] { "src", "tests" })
{
    string dir = Path.Combine(root, top);
    if (!Directory.Exists(dir))
    {
        continue;
    }

    foreach (string file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
    {
        string norm = file.Replace('\\', '/');
        if (norm.Contains("/bin/", StringComparison.Ordinal) || norm.Contains("/obj/", StringComparison.Ordinal) ||
            norm.Contains("/.claude/", StringComparison.Ordinal) || norm.Contains("G1R-001.RetirementLedger.Tests", StringComparison.Ordinal))
        {
            continue;
        }

        files.Add(file);
    }
}

Console.WriteLine($"scanned files: {files.Count}");

var failures = new List<string>();
foreach ((string symbol, int baseline, string batch) in ledger)
{
    var regex = new Regex($@"\b{Regex.Escape(symbol)}\b", RegexOptions.Compiled);
    int count = 0;
    foreach (string file in files)
    {
        count += regex.Matches(File.ReadAllText(file)).Count;
    }

    string state = count == 0 ? "READY-FOR-DELETION" : count <= baseline ? "OK" : "GREW";
    string line = $"{symbol}: refs={count} baseline={baseline} [{state}] -> {batch}";
    if (count > baseline)
    {
        failures.Add(line);
        Console.Error.WriteLine($"FAIL {line}");
        Console.Error.WriteLine($"     A retirement-confirmed symbol GAINED references — new wiring onto retired symbols is prohibited (RD-RETIRE-DA1). Remove the new consumer or take the symbol's disposal batch over.");
    }
    else
    {
        Console.WriteLine($"PASS {line}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\n{failures.Count} ledger row(s) grew.");
    Environment.Exit(1);
}

Console.WriteLine($"\n{ledger.Length} ledger row(s) at or below baseline.");

static string FindRepoRoot()
{
    // Walk up from the test binary (or cwd) to the directory that holds src/HeadlessDCGO.Engine.
    foreach (string start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
    {
        DirectoryInfo? dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "HeadlessDCGO.Engine")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }
    }

    throw new InvalidOperationException("repo root (containing src/HeadlessDCGO.Engine) not found");
}
