using System.Reflection;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

// CardEffect.Binding.Auto — the porter's automatic gate (strong-model-owned; porter writes no tests).
//
// Reflects over every ported card (a CEntity_Effect subclass under
// ...Assets.Scripts.CardEffect.<SET>.<COLOR>) and asserts it is LIVE, not inert: calling
// CardEffects across all timings yields at least one ICardEffect. Skeleton stubs define no
// class, so reflection naturally skips them. What a card DOES (semantic 1:1 fidelity) is the
// reviewer's job — this gate only proves "the ported body registers a real effect".
//
// A card that yields 0 effects is still OK if its mirror source carries a `// STOP:` marker —
// that is a recorded escalation awaiting the strong model (per-branch STOP contract), not an
// inert port. Such cards are counted as stop-pending, not failures.
//
// Optional arg: a namespace/name substring filter (e.g. "BT1" or "BT1.Red"). With none, all cards.

// Only a non-flag token is a filter; the test runner forwards flags like `--nologo` as app args,
// which must NOT be mistaken for a filter (that would silently match nothing = false pass).
string? filter = args.FirstOrDefault(a => !a.StartsWith('-'));

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

var baseType = typeof(CEntity_Effect);
const string CardNs = "HeadlessDCGO.Engine.Assets.Scripts.CardEffect.";

var cardTypes = SafeTypes(baseType.Assembly)
    .Where(t => t.IsClass && !t.IsAbstract && baseType.IsAssignableFrom(t))
    .Where(t => t.Namespace != null && t.Namespace.StartsWith(CardNs, StringComparison.Ordinal))
    .Where(t => !t.Namespace!.Contains(".TestFixtures", StringComparison.Ordinal))
    .Where(t => filter == null || $"{t.Namespace}.{t.Name}".Contains(filter, StringComparison.OrdinalIgnoreCase))
    .OrderBy(t => t.FullName, StringComparer.Ordinal)
    .ToList();

EffectTiming[] timings = Enum.GetValues<EffectTiming>();

// Timings where ActivatedEffectResolver.ResolveAsync is wired (activation effects actually fire).
// Kept in sync with the ResolveAsync call sites in Headless/Runtime/*.
var ActivationWiredTimings = new HashSet<EffectTiming>
{
    EffectTiming.BeforePayCost,
    EffectTiming.OnEnterFieldAnyone,
    EffectTiming.OptionSkill,
    EffectTiming.SecuritySkill,
    EffectTiming.WhenDigivolving,
};
var failures = new List<string>();
int live = 0;
int stopPending = 0;
int activationPending = 0;

string repoRoot = FindRepositoryRoot();

foreach (var type in cardTypes)
{
    CEntity_Effect instance;
    try { instance = (CEntity_Effect)Activator.CreateInstance(type)!; }
    catch (Exception ex) { failures.Add($"{type.FullName}: cannot instantiate — {Root(ex)}"); continue; }

    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 2);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var card = new CardSource(ctx, new HeadlessEntityId($"p1:auto:{type.Name}"), P1, P1);

    // A card is LIVE only if it returns a FIREABLE effect. IActivatedCardEffect (Draw / Reveal /
    // Select / Digivolve / …) is skipped by CardEffectRegistrar (`is IActivatedCardEffect → continue`)
    // until the interactive activation path is wired, so a card returning ONLY activated effects
    // binds-but-does-not-fire. Counting those as live would be a false pass (Phase D firing model).
    // Correction (firing model): IActivatedCardEffect is NOT dormant — ActivatedEffectResolver
    // fires it, but only for the timings it is wired into (ActivationWiredTimings). At those
    // timings the effect fires (live); at any other timing it binds-but-does-not-fire (pending).
    int fireableCount = 0, activatedPendingCount = 0;
    var errors = new List<string>();
    foreach (EffectTiming timing in timings)
    {
        bool wired = ActivationWiredTimings.Contains(timing);
        try
        {
            IReadOnlyList<ICardEffect>? effects = instance.CardEffects(timing, card);
            if (effects == null) continue;
            foreach (ICardEffect e in effects)
            {
                if (e is not IActivatedCardEffect) fireableCount++;
                else if (wired) fireableCount++;
                else activatedPendingCount++;
            }
        }
        catch (Exception ex) { errors.Add($"{timing}: {Root(ex)}"); }
    }

    if (fireableCount > 0)
    {
        live++;
    }
    else if (errors.Count == 0 && MirrorHasStopMarker(repoRoot, type))
    {
        stopPending++; // recorded escalation (// STOP) — legitimate zero-effect state
    }
    else if (activatedPendingCount > 0 && errors.Count == 0)
    {
        // Faithfully ported but not yet fireable: returns only activation-flow effects, which the
        // registrar skips until the interactive activation path is wired (engine work, not a port
        // defect). Tracked separately so it does not read as either live or a failure.
        activationPending++;
    }
    else
    {
        string why = errors.Count > 0
            ? " threw on every producing timing: " + string.Join(" | ", errors)
            : " returned 0 effects across all timings and has no // STOP marker (inert port).";
        failures.Add($"{type.FullName}{why}");
    }
}

// Cross-check the filesystem: every live (non-skeleton) mirror file must map to a discovered
// type. A mirror that forgets its `namespace ...CardEffect.<SET>.<COLOR>;` declaration lands in
// the global namespace and silently escapes reflection — without this sweep that reads as PASS.
string mirrorRoot = Path.Combine(repoRoot, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "CardEffect");
var discovered = cardTypes.Select(t => $"{t.Namespace}.{t.Name}").ToHashSet(StringComparer.Ordinal);
int undiscovered = 0;
foreach (string file in Directory.EnumerateFiles(mirrorRoot, "*.cs", SearchOption.AllDirectories))
{
    string[] segs = Path.GetRelativePath(mirrorRoot, file).Split(Path.DirectorySeparatorChar);
    if (segs.Length != 3 || segs.Contains("TestFixtures")) continue; // expect <SET>/<COLOR>/<ID>.cs
    string expected = $"{CardNs}{segs[0]}.{segs[1]}.{Path.GetFileNameWithoutExtension(segs[2])}";
    if (filter != null && !expected.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
    if (discovered.Contains(expected)) continue;
    if (File.ReadAllText(file).Contains("Skeleton only", StringComparison.Ordinal)) continue;
    undiscovered++;
    failures.Add($"{expected}: live mirror file exists but reflection did not discover the type — " +
                 $"missing or wrong `namespace` declaration in {segs[0]}/{segs[1]}/{segs[2]}?");
}

Console.WriteLine(
    $"CardEffect.Binding.Auto: {cardTypes.Count} card(s) discovered ({undiscovered} live mirror(s) undiscovered)" +
    (filter is null ? "" : $" (filter '{filter}')") +
    $", {live} live, {stopPending} stop-pending, {activationPending} activation-pending, {failures.Count} failing.");

if (failures.Count > 0)
{
    Console.Error.WriteLine();
    foreach (string f in failures) Console.Error.WriteLine($"FAIL {f}");
    Console.Error.WriteLine($"\n{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine($"\n{live} test(s) passed.");

static IEnumerable<Type> SafeTypes(Assembly asm)
{
    try { return asm.GetTypes(); }
    catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t is not null)!; }
}

static string FindRepositoryRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "HeadlessDCGO.Engine")))
    {
        dir = dir.Parent;
    }
    return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
}

static bool MirrorHasStopMarker(string repoRoot, Type type)
{
    // namespace ...Assets.Scripts.CardEffect.<SET>.<COLOR> + type name -> mirror source path
    string[] parts = type.Namespace!.Split('.');
    string set = parts[^2], color = parts[^1];
    string path = Path.Combine(
        repoRoot, "src", "HeadlessDCGO.Engine", "Assets", "Scripts", "CardEffect", set, color, type.Name + ".cs");
    return File.Exists(path) && File.ReadAllText(path).Contains("// STOP", StringComparison.Ordinal);
}

static string Root(Exception ex)
{
    while (ex.InnerException is not null) ex = ex.InnerException;
    return $"{ex.GetType().Name}: {ex.Message}";
}
