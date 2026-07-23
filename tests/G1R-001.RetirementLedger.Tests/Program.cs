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
    // (R6-Db D4) `PlaySelfAtEndOfBattleSecurityEffect` row RETIRED — the LAST active ledger row; its removal takes
    // the ledger to 0 ROWS. The symbol (+ its one-shot `PlaySelfAtEndOfBattleTriggerEffect` carrier) was DELETED
    // in its owning batch (D4): RD-P6C3-B2 RE-ADJUDICATED and RESOLVED — the AS-IS [Security] end-of-battle
    // deferred play is now landed 1:1 in CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect against the
    // A1b-landed Player.UntilEndBattleEffects bucket (OnEndBattle-sampled) + the Permanent.UntilOpponentTurnEndEffects
    // bucket + CardController.DestroyPermanentsClass for the delete branch. The invented EffectRegistry
    // OnEndBattle-trigger substitute (body + trigger + the ActivatedEffectResolver switch case) was consumer-0
    // (diversified grep: no live constructor/cast/switch — only comment tombstones), so all three were removed
    // outright; the live consumers EX10_029/P_165 keep calling the (now-landed) factory, and G9-031/FAILa-10 were
    // re-aimed onto the UntilEndBattleEffects mechanism (assertions preserved). Retirement guard = the deleted
    // symbol no longer exists.
    // (uniform-사멸 flip) `ActivatedSelectEffect` row RETIRED — the symbol was DELETED in its owning batch: the
    // EX8_074 RD-R6-07 STOP was resolved by the earlier inline re-port, the B5 uniform fixtures
    // (TfxCappedSelectSuspend/TfxOptionalSelectSuspend) were retired with their suite, the internal
    // SelectDestroyThenTrashSecurityBody wrapper was consumer-0 (AD1_025 re-ported) and died with it, and the
    // stale ST1.Red/ST2.Blue white-box casts were re-targeted onto the live ActivateClass surface. The whole
    // uniform core (`ActivatedEffect` 437-line file + resolver uniform seats + OnceFlagController/OnceFlagHelpers
    // + SuspendCostReductionEffect + BeforePayCostAvailabilityReduction) died in the same batch.
    // (R6-Db) `ActivatedSelectBounceAndDiscardSourcesEffect` row RETIRED — the symbol was DELETED in R6-Db: it was
    // consumer-0 in production (no card producer), and its ONLY remaining consumer, the green C3-Witness case (9),
    // was re-targeted onto the REAL substrate it composed (DigivolutionStackHelpers.TrashSourcesAsync(honorProtection:
    // false) + SelectPermanentEffect Mode.Bounce, AS-IS HandBounceClaass.Bounce order). C3-Witness stays 10/10 green.
    // (A6 / ST2.Blue disposal) `ActivatedSelectTrashDigivolutionEffect` row RETIRED — the symbol was DELETED in its
    // owning batch (A6): its factory helper (SelectAndTrashDigivolutionEffect) was already gone, its former cards
    // (ST2_03/06/09) are re-ported to the inline AS-IS ActivateClass + SelectPermanentEffect Mode.Custom +
    // TrashDigivolutionCardsFromTopOrBottom, and its only remaining consumers — the 3 stale ST2.Blue white-box casts
    // — were re-targeted onto that live ActivateClass surface (CardEffect.ST2.Blue suite now 12/12 truthful green).
    // No resolver switch case existed. (ActivatedPlayFromUnderEffect was RE-CONFIRMED NOT consumer-0 by the A6
    // diversified grep — BT23.PrimTranche4 / G9-060 / FAILa-11 / CardEffectFactory.PlayMindLinkTamerFromDigivolutionCards
    // still construct/cast it — so it is RETAINED, not deleted, this batch.)
    // (R6-Da'-5) `SelectAndDeDigivolveEffect` (factory helper) + `ActivatedSelectAndDeDigivolveEffect` (body) rows
    // RETIRED — both symbols were DELETED in their owning batch (Da'-5): census-0 producer (only the [Obsolete]
    // factory helper constructed the body, only the G9-046 DeDigivolve white-box test consumed the helper). The
    // resolver-switch `case ActivatedSelectAndDeDigivolveEffect` collapsed with them. De-digivolve is covered live
    // by CardEffectCommons.DeDigivolvePermanent + the SelectDeDigivolve / MassDeDigivolve primitives.

    // (RD-IDFLIP-01 / id-surface flip campaign batch 4) `SetAttackOptions` + `SetCanEndSelectCondition` rows
    // RETIRED — both symbols were DELETED in their owning batch (batch 4): the invented id-form select-effect
    // authoring surfaces (the 8-param `SetUp` overload, `SetAttackOptions(bool, Func<HeadlessEntityId,bool>)`,
    // `SetCanEndSelectCondition(Func<IReadOnlyList<HeadlessEntityId>,bool>)` on SelectPermanentEffect) were
    // physically removed once their sole consumer — the G3.5-CVA2 suite — was re-written onto the CANONICAL
    // AS-IS route: the 11-param Func<Permanent,bool> `SetUp` (via GManager.instance.GetComponent under an
    // AmbientMatchContext scope), the AS-IS `SetCanNotAttackPlayer()` + `SetDefenderCondition(Permanent)` pair
    // (both former SetAttackOptions call-sites set canAttackPlayer:false, mapping 1:1 onto the AS-IS
    // unidirectional flag-down), and the 11-param `canEndSelectCondition` param (consulted through the retained
    // IsValidSelection). CVA2 stays 12/12 green with every behavioural assertion preserved. Retirement guard =
    // the deleted symbols no longer exist. This removal takes the ledger to 0 ROWS.
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
