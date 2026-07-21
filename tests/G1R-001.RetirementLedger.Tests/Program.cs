// G1R-001 (R6-Da'-1): RETIREMENT LEDGER scan — retirement-confirmed symbols carried over to a later batch
// must not GAIN references. Each ledger row pins the reference-count baseline measured at the R6-Da'-1 batch
// close (whole-word occurrences across src/ + tests/ *.cs, excluding bin/obj and this project). A count ABOVE
// baseline means someone wired a NEW consumer onto a retired symbol (the ExpireAttackEnd-precedent accident
// this suite exists to catch) -> FAIL. A count AT or BELOW baseline passes (shrinking is the goal); a drop to
// 0 is reported as "ready for deletion". Update a baseline ONLY when a disposal batch intentionally lowers it.
// The source-level guard is the paired [Obsolete("RD-RETIRE-DA1: ...")] attributes on the same symbols.

using System.Text.RegularExpressions;

// symbol, pinned baseline, owning disposal batch (where the symbol actually dies).
// (R6-Da'-3 update) `AsUniformActivated` row RETIRED — the symbol was deleted in its owning batch (Da'-3):
// its 6 granted-continuous buff factory seats had 0 card call-sites, so the helper + the 2 invented buff
// bodies were removed outright (retirement-guard-protocol step 3). `PlaySelfAtEndOfBattleSecurityEffect`
// (granted-continuous producer :2573, the sole deferred-trigger of the 6) is added here as a carry-over to
// R6-Db (design §5 D4) — it is [Obsolete]-guarded and must not GAIN consumers before its R6-Db rehome.
var ledger = new (string Symbol, int Baseline, string Batch)[]
{
    ("PlaySelfAtEndOfBattleSecurityEffect", 8, "R6-Db carry (D4: AS-IS UntilEndBattleEffects rehome, RD-P6C3-B2 re-adjudication — live resolver case + factory + FAILa-10)"),
    ("ActivatedSelectEffect", 22, "STAYS — uniform survival core (EX8_074 RD-R6-07/R2-C STOP + live ST1/ST2 cards + fixtures + white-box casts); final deletion after A8 R2-C + Tfx retirement"),
    // (R6-Db) `ActivatedSelectBounceAndDiscardSourcesEffect` row RETIRED — the symbol was DELETED in R6-Db: it was
    // consumer-0 in production (no card producer), and its ONLY remaining consumer, the green C3-Witness case (9),
    // was re-targeted onto the REAL substrate it composed (DigivolutionStackHelpers.TrashSourcesAsync(honorProtection:
    // false) + SelectPermanentEffect Mode.Bounce, AS-IS HandBounceClaass.Bounce order). C3-Witness stays 10/10 green.
    ("ActivatedSelectTrashDigivolutionEffect", 6, "A6 carry (deleted with the full ST2.Blue disposal — the whole CardEffect.ST2.Blue suite is stale-red; the 3 TrashDigivolution casts are dead InvalidCast, providing 0 coverage today)"),
    // (R6-Da'-5) `SelectAndDeDigivolveEffect` (factory helper) + `ActivatedSelectAndDeDigivolveEffect` (body) rows
    // RETIRED — both symbols were DELETED in their owning batch (Da'-5): census-0 producer (only the [Obsolete]
    // factory helper constructed the body, only the G9-046 DeDigivolve white-box test consumed the helper). The
    // resolver-switch `case ActivatedSelectAndDeDigivolveEffect` collapsed with them. De-digivolve is covered live
    // by CardEffectCommons.DeDigivolvePermanent + the SelectDeDigivolve / MassDeDigivolve primitives.
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
