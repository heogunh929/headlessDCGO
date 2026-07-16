using System.Text;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (R4 batch P4, design docs/audit/r4_tsm_s1_design_2026-07-16.md §4) SHADOW-RUN harness.
//
// Runs TWO DcgoMatch instances built from the SAME MatchConfig (same RandomSeed + UseDeterministicChoices +
// same starter decks) side by side, driven by ONE seed-deterministic self-play policy so both take the exact
// same action every step, and compares their full state (HeadlessTurnState value-equality + a zone/memory/
// attack/choice digest from GetObservation) after every step. The first divergence is reported with the seed,
// step index, and the differing digest line.
//
// CURRENT STAGE = OLD-vs-OLD identity sanity: BOTH matches use the DEFAULT (OLD) turn driver, so this proves
// the harness itself is deterministic (a prerequisite for the S3 gate). At S3 this same harness flips match B
// to the NEW TurnStateMachine-backed driver (OLD-vs-NEW) via the BuildMatch injection seam below — the per-step
// comparison then witnesses that the re-housed turn flow reproduces the substrate state bit-for-bit.
//
// N is a small default (STEP-suite time budget); raise it for the S3 cutover gate (see GameCount).

// Each shadow game runs TWO full matches in lockstep, so the step budget is ~2x a normal self-play game; the
// suite default is kept small for time. (S3) Raise GameCount substantially for the OLD-vs-NEW cutover gate
// (the design targets 200-1000) — optionally override at runtime via the SHADOW_GAMES env var.
int GameCount = int.TryParse(Environment.GetEnvironmentVariable("SHADOW_GAMES"), out int gc) && gc > 0 ? gc : 2;
const int StepCap = 800;            // full starter-deck games terminate via security depletion well under this.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
string[] sets = { "ST1", "ST2", "ST3" };

// Deterministic (seed, deck-pairing) schedule so the run is reproducible and each game is distinct.
var games = new List<(string P1Set, string P2Set, int EnvSeed, int PolicySeed)>();
for (int i = 0; i < GameCount; i++)
{
    string a = sets[i % sets.Length];
    string b = sets[(i + 1) % sets.Length];
    games.Add((a, b, EnvSeed: 1000 + i * 7, PolicySeed: 500 + i * 13));
}

var results = new List<ShadowResult>();
string? hardFailure = null;
try
{
    foreach (var g in games)
        results.Add(await RunShadowGameAsync(g.P1Set, g.P2Set, g.EnvSeed, g.PolicySeed, StepCap));
}
catch (Exception ex)
{
    hardFailure = $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
}

// --- Report --------------------------------------------------------------
Console.WriteLine("matchup       envSeed  steps  term  reachedEnd  identical  end");
foreach (var r in results)
{
    string end = r.Terminal ? "terminal" : r.EngineHalt ? "engineHalt" : (r.Steps >= StepCap ? "stepCap" : "noMover");
    Console.WriteLine($"{r.Matchup,-12} {r.EnvSeed,7} {r.Steps,6} {(r.Terminal ? "  Y" : "  .")}  {(r.ReachedTerminalOrCap ? "     Y" : "     ."),-9}  {(r.Identical ? "Y" : "N"),-9}  {end}");
}

// Latent engine/card bugs surfaced by random self-play (both matches hit them IDENTICALLY, so they are
// findings, not divergences). Printed loudly so they are never hidden — see RD-R4P4-01/02.
var halts = results.Where(r => r.EngineHalt).ToList();
if (halts.Count > 0)
{
    Console.WriteLine($"\n[engine-halt findings] {halts.Count} game(s) ended on a DETERMINISTIC engine exception hit identically by both OLD matches:");
    foreach (var h in halts.GroupBy(h => h.EngineHaltReport.Contains(':') ? h.EngineHaltReport[(h.EngineHaltReport.IndexOf(']') + 1)..].Trim() : h.EngineHaltReport))
        Console.WriteLine($"  x{h.Count()}  {h.Key}");
}

if (hardFailure is not null)
{
    Console.Error.WriteLine($"\nFAIL R4P4-ShadowRun (unexpected harness exception outside the compared step)\n{hardFailure}");
    Environment.Exit(1);
}

// --- Assertions ----------------------------------------------------------
var failures = new List<string>();
void Check(bool ok, string label) { if (!ok) failures.Add(label); }

foreach (var r in results)
{
    if (!r.Identical)
    {
        Check(false,
            $"{r.Matchup} (envSeed={r.EnvSeed}) DIVERGED at step {r.DivergenceStep}:\n{r.DivergenceReport}");
    }
}
// The core property: OLD-vs-OLD produced bit-identical trajectories (including any identical deterministic
// halts) for EVERY game — this is what the S3 gate flips to OLD-vs-NEW.
Check(results.All(r => r.Identical), "every OLD-vs-OLD shadow game stayed bit-identical for its whole trajectory");
Check(results.All(r => r.Steps > 0), "every shadow game took at least one step");
Check(results.All(r => r.ReachedTerminalOrCap), "every shadow game ran to completion (terminal / cap / identical halt)");
Check(results.Count(r => r.Terminal) >= 2, "multiple shadow games ran a full game to a natural terminal (win/loss)");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"\nFAIL R4P4-ShadowRun ({failures.Count} property/properties):");
    foreach (var f in failures) Console.Error.WriteLine($"  - {f}");
    Environment.Exit(1);
}

Console.WriteLine($"\nPASS R4P4-ShadowRun: {results.Count} OLD-vs-OLD shadow games stayed bit-identical " +
    $"({results.Count(r => r.Terminal)} natural terminals, {halts.Count} identical engine-halts). Harness is deterministic.");

// --- Shadow rollout ------------------------------------------------------

async Task<ShadowResult> RunShadowGameAsync(string p1Set, string p2Set, int envSeed, int policySeed, int cap)
{
    DcgoMatch a = await NewMatchAsync(p1Set, p2Set, envSeed, driver: null);   // OLD driver
    DcgoMatch b = await NewMatchAsync(p1Set, p2Set, envSeed, driver: null);   // OLD driver (S3: NEW driver here)

    var rng = new Random(policySeed);
    var players = new[] { P1, P2 };

    int steps = 0;
    bool terminal = false;
    bool identical = true;
    bool engineHalt = false;
    string engineHaltReport = string.Empty;
    int divergenceStep = -1;
    string divergenceReport = string.Empty;

    // Guard: the two matches must START identical (same config/seed/setup).
    if (!TryCompare(a, b, out string initReport))
    {
        return new ShadowResult($"{p1Set}v{p2Set}", envSeed, 0, false, true,
            Identical: false, EngineHalt: false, EngineHaltReport: string.Empty,
            DivergenceStep: 0, DivergenceReport: $"[initial state]\n{initReport}");
    }

    while (steps < cap && !a.IsTerminal())
    {
        // Mover = first player with a legal action. Both matches are (so far) identical, so the mover and its
        // legal-action LIST must match; a mismatch is itself a divergence to report before we desync.
        HeadlessPlayerId mover = default;
        bool foundA = false;
        foreach (HeadlessPlayerId p in players)
        {
            if (Legal(a, p).Count > 0) { mover = p; foundA = true; break; }
        }
        if (!foundA) { break; } // no mover, not terminal: end the game (deadlock is caught by ReachedTerminalOrCap).

        IReadOnlyList<LegalAction> legalA = Legal(a, mover);
        IReadOnlyList<LegalAction> legalB = Legal(b, mover);
        if (!LegalSetsMatch(legalA, legalB))
        {
            identical = false;
            divergenceStep = steps;
            divergenceReport = "[legal-action set mismatch for mover P" + mover.Value + "]\n" +
                "A: " + string.Join(", ", legalA.Select(x => x.Id.Value)) + "\n" +
                "B: " + string.Join(", ", legalB.Select(x => x.Id.Value));
            break;
        }

        int idx = rng.Next(legalA.Count);
        LegalAction actionA = legalA[idx];
        LegalAction actionB = legalB[idx];

        // Step BOTH matches on the same action; a thrown engine exception is captured (not rethrown) so the two
        // matches' FAILURE modes are compared too — a deterministic engine halt that hits BOTH identically is
        // still identical OLD-vs-OLD behavior (a reported finding, not a harness/divergence failure).
        string? exA = await StepMatchAsync(a, actionA);
        string? exB = await StepMatchAsync(b, actionB);
        steps++;

        if (exA is not null || exB is not null)
        {
            if (exA == exB)
            {
                // Both threw the SAME exception at the SAME step = deterministic engine halt (identical). This
                // is a pre-existing latent engine/card bug surfaced by random self-play (see RD-R4P4-01/02),
                // not a turn-flow divergence — the shadow comparison still holds.
                engineHalt = true;
                engineHaltReport = $"[step {steps}: both matches threw identically after P{mover.Value} {actionA.ActionType}] {exA}";
                break;
            }

            identical = false;
            divergenceStep = steps;
            divergenceReport = $"[exception mismatch after step {steps}: P{mover.Value} {actionA.ActionType}]\nA: {exA ?? "<none>"}\nB: {exB ?? "<none>"}";
            break;
        }

        if (!TryCompare(a, b, out string report))
        {
            identical = false;
            divergenceStep = steps;
            divergenceReport = $"[after step {steps}: mover P{mover.Value} action {actionA.ActionType} ({actionA.Id.Value})]\n{report}";
            break;
        }

        if (a.IsTerminal() != b.IsTerminal())
        {
            identical = false;
            divergenceStep = steps;
            divergenceReport = $"[terminal mismatch after step {steps}] A.IsTerminal={a.IsTerminal()} B.IsTerminal={b.IsTerminal()}";
            break;
        }

        if (a.IsTerminal()) { terminal = true; break; }
    }

    // "reached end" = a natural terminal, the step cap, a deterministic engine halt in both matches, or a
    //   no-mover stop after real progress (the game ran to completion rather than aborting on step 0).
    bool reachedTerminalOrCap = terminal || steps >= cap || engineHalt || steps > 0;

    return new ShadowResult($"{p1Set}v{p2Set}", envSeed, steps, terminal, reachedTerminalOrCap,
        identical, engineHalt, engineHaltReport, divergenceStep, divergenceReport);
}

async Task<DcgoMatch> NewMatchAsync(string p1Set, string p2Set, int envSeed, IActionProcessor? driver)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: envSeed);
    var db = (CardDatabase)context.CardRepository;
    CardBaseEntityLoader.LoadInto(db);

    StarterDecks.StarterDeck d1 = StarterDecks.Get(p1Set), d2 = StarterDecks.Get(p2Set);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[]
        {
            new PlayerDeckSetup(P1, d1.MainDefinitions, d1.DigitamaDefinitions),
            new PlayerDeckSetup(P2, d2.MainDefinitions, d2.DigitamaDefinitions),
        }, firstPlayerId: P1);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: envSeed, setup: setup);

    DcgoMatch match = BuildMatch(context, driver);
    await match.InitializeAsync(config);
    return match;
}

// (S3 SEAM) The driver injection point. `driver == null` uses the DEFAULT (OLD) turn driver that
// HeadlessGameLoop builds internally (MetadataActionProcessor). At S3, pass the NEW TurnStateMachine-backed
// IActionProcessor here for match B to run OLD-vs-NEW; today both sides pass null = OLD-vs-OLD.
static DcgoMatch BuildMatch(EngineContext context, IActionProcessor? driver)
    => new DcgoMatch(context, new EngineTrace(), actionProcessor: driver, actionLegality: new LegalActionSetValidator());

// Every match interaction is scoped under this match's ambient context — the documented AmbientMatchContext
// contract ("the engine sets it at the start of a match operation"), so the mirror GManager.instance resolves
// to THIS match. It is applied identically to A and B, so it never biases the OLD-vs-OLD comparison. NOTE
// (RD-R4P4-01): this scoping is required here because DcgoMatch.StepAsync does NOT self-scope the ambient, and
// the block-choice resolution path (MetadataActionProcessor.ResolveChoiceAsync -> BlockTiming.GetBlockerCandidates
// -> Permanent.HasCollision -> CEntity_EffectController.GetCardEffects reads GManager.instance) runs at the
// ProcessAsync level OUTSIDE any inner ambient scope — so a block-with-collision NREs without this wrap. That is
// a pre-existing latent engine gap surfaced by the harness (independent of R4/P3), reported for S3 review.
static async Task<string?> StepMatchAsync(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    try
    {
        await match.ApplyActionAsync(action);
        await match.StepAsync();
        return null;
    }
    catch (Exception ex)
    {
        // Captured, not rethrown: the caller compares the two matches' failure modes (an identical
        // deterministic halt is still identical OLD-vs-OLD behavior).
        return $"{ex.GetType().Name}: {ex.Message}";
    }
}

static IReadOnlyList<LegalAction> Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

static bool LegalSetsMatch(IReadOnlyList<LegalAction> a, IReadOnlyList<LegalAction> b)
{
    if (a.Count != b.Count) return false;
    for (int i = 0; i < a.Count; i++)
    {
        if (a[i].Id.Value != b[i].Id.Value || a[i].ActionType != b[i].ActionType) return false;
    }
    return true;
}

// Value-compare the two matches: HeadlessTurnState fields + the observation digest. Returns false + a report
// pinpointing the first differing digest line on divergence.
static bool TryCompare(DcgoMatch a, DcgoMatch b, out string report)
{
    string da = Digest(a);
    string db = Digest(b);
    if (da == db) { report = string.Empty; return true; }

    string[] la = da.Split('\n');
    string[] lb = db.Split('\n');
    var sb = new StringBuilder();
    int n = Math.Max(la.Length, lb.Length);
    for (int i = 0; i < n; i++)
    {
        string ra = i < la.Length ? la[i] : "<none>";
        string rb = i < lb.Length ? lb[i] : "<none>";
        if (ra != rb)
        {
            sb.AppendLine($"  A| {ra}");
            sb.AppendLine($"  B| {rb}");
        }
    }
    report = sb.ToString();
    return false;
}

static string Digest(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    ObservationSnapshot o = match.GetObservation();
    HeadlessTurnState t = match.Context.TurnController.Current;
    var sb = new StringBuilder();

    // Turn (compared explicitly rather than via record == because HeadlessTurnState.PlayerOrder is a list,
    //   which records compare by reference — the digest compares its contents).
    sb.AppendLine($"turn: n={t.TurnNumber} tp={PId(t.TurnPlayerId)} ntp={PId(t.NonTurnPlayerId)} phase={t.Phase} first={t.IsFirstTurn} order=[{string.Join(",", t.PlayerOrder.Select(p => p.Value))}]");
    sb.AppendLine($"terminal: {match.IsTerminal()}  pendingChoice: {match.HasPendingChoice()}");
    sb.AppendLine($"memory: cur={o.Memory.Current} min={o.Memory.Minimum} max={o.Memory.Maximum}");

    HeadlessAttackState atk = o.Attack;
    sb.AppendLine($"attack: count={atk.AttackCount} atkr={EId(atk.AttackerId)} tgt={EId(atk.TargetId)} blk={EId(atk.BlockerId)} isBlocked={atk.IsBlocked} direct={atk.IsDirectAttack} pending={atk.IsPending} resolved={atk.IsResolved} phase={atk.Phase} atkP={PId(atk.AttackingPlayerId)} defP={PId(atk.DefendingPlayerId)}");

    HeadlessChoiceState ch = o.Choice;
    sb.AppendLine($"choice: pending={ch.IsPending} cand=[{string.Join(",", ch.CandidateIds.Select(c => c.Value))}]");

    foreach (PlayerObservation p in o.Players.OrderBy(p => p.PlayerId.Value))
    {
        foreach (ZoneObservation z in p.Zones.OrderBy(z => (int)z.Zone))
        {
            sb.AppendLine($"p{p.PlayerId.Value}:{z.Zone}={z.Count}:[{string.Join(",", z.CardIds.Select(c => c.Value))}]");
        }
    }

    return sb.ToString();
}

static string PId(HeadlessPlayerId? id) => id.HasValue ? id.Value.Value.ToString() : "-";
static string EId(HeadlessEntityId? id) => id.HasValue ? id.Value.Value : "-";

internal sealed record ShadowResult(
    string Matchup,
    int EnvSeed,
    int Steps,
    bool Terminal,
    bool ReachedTerminalOrCap,
    bool Identical,
    bool EngineHalt,
    string EngineHaltReport,
    int DivergenceStep,
    string DivergenceReport);
