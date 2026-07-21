using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// GR-007: the Piercing keyword GRANT mutation must set the SAME presence flag its consumer reads.
// Before this, the keyword mutations wrote dead flags (scheduleRebootUnsuspend / pendingSecurityCheck) that
// no consumer read, while the consumer (BattleResolver) reads hasPiercing which no mutation set — so a
// Piercing GRANTED via this mutation was inert. The sink now maps: SetSecurityCheck -> hasPiercing.
// (Self-static Piercing already worked via the GR-005 ContinuousKeywordGate; this fixes the grant path.)
//
// (R6-Da'-4 / RD-P6B-6, ledger #5) The Reboot carrier (ScheduleRebootUnsuspend -> hasReboot) was REMOVED: the
// OLD phase-flow reader of the hasReboot metadata flag retired in 4b and the live Reboot read is rehoused to the
// AS-IS Permanent.HasReboot keyword scan (covered by the R4P2a ActiveRebootUnsuspend witness), leaving the
// metadata carrier with 0 gameplay consumers. RebootGrantNoLongerWritesDeadFlag pins that removal.

var tests = new (string Name, Action Body)[]
{
    ("Granting Reboot (ScheduleRebootUnsuspend) no longer writes the retired hasReboot metadata flag", RebootGrantNoLongerWritesDeadFlag),
    ("Granting Piercing (SetSecurityCheck) sets the hasPiercing flag the consumer reads", PiercingGrantSetsConsumerFlag),
    ("No dead set-only flags are written (scheduleRebootUnsuspend / pendingSecurityCheck)", NoDeadFlags),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}\n{ex}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

void RebootGrantNoLongerWritesDeadFlag()
{
    // (R6-Da'-4 / RD-P6B-6, ledger #5) The ScheduleRebootUnsuspend -> hasReboot metadata carrier was removed:
    // the OLD phase-flow reader retired and the live Reboot read is the AS-IS Permanent.HasReboot keyword scan
    // (R4P2a ActiveRebootUnsuspend witness). The mutation now writes no metadata flag (0 gameplay consumers).
    var (repo, sink) = Setup("r1");
    sink.Apply(new EffectMutation("ScheduleRebootUnsuspend", new HeadlessEntityId("src"),
        new Dictionary<string, object?> { ["targetEntityId"] = "r1" }));
    AssertTrue(!Flag(repo, "r1", "hasReboot"), "the retired hasReboot metadata carrier no longer writes a flag");
}

void PiercingGrantSetsConsumerFlag()
{
    var (repo, sink) = Setup("p1");
    sink.Apply(new EffectMutation("SetSecurityCheck", new HeadlessEntityId("src"),
        new Dictionary<string, object?> { ["targetEntityId"] = "p1" }));
    AssertTrue(Flag(repo, "p1", "hasPiercing"), "hasPiercing (the flag BattleResolver reads) is set");
}

void NoDeadFlags()
{
    var (repo, sink) = Setup("x1");
    sink.Apply(new EffectMutation("ScheduleRebootUnsuspend", new HeadlessEntityId("src"),
        new Dictionary<string, object?> { ["targetEntityId"] = "x1" }));
    sink.Apply(new EffectMutation("SetSecurityCheck", new HeadlessEntityId("src"),
        new Dictionary<string, object?> { ["targetEntityId"] = "x1" }));
    AssertTrue(!Flag(repo, "x1", "scheduleRebootUnsuspend"), "no dead scheduleRebootUnsuspend flag");
    AssertTrue(!Flag(repo, "x1", "pendingSecurityCheck"), "no dead pendingSecurityCheck flag");
}

// --- Helpers -------------------------------------------------------------

static (InMemoryCardInstanceRepository Repo, MatchStateMutationSink Sink) Setup(string instanceId)
{
    var repo = new InMemoryCardInstanceRepository();
    repo.Upsert(new CardInstanceRecord(new HeadlessEntityId(instanceId), new HeadlessEntityId($"def:{instanceId}"), new HeadlessPlayerId(1)));
    return (repo, new MatchStateMutationSink(repo));
}

static bool Flag(InMemoryCardInstanceRepository repo, string instanceId, string key) =>
    repo.TryGetInstance(new HeadlessEntityId(instanceId), out CardInstanceRecord? record) && record is not null
        && record.Metadata.TryGetValue(key, out object? value) && value is true;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
