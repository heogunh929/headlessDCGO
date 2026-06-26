using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// G3.5-W2: expanded effect-mutation vocabulary. Effects emit EffectMutations; the sink applies them
// to card-instance metadata that battle/security/observation read. Headline: typed DP modifiers.

HeadlessPlayerId P1 = new(1);
HeadlessEntityId Card = new("p1:main:001:X");

var tests = new (string Name, Action Body)[]
{
    ("AddDpModifier appends a relative DP modifier that battle/observation read", DpRelativeApplied),
    ("AddDpModifier with absolute=true appends an absolute set", DpAbsoluteApplied),
    ("Multiple DP modifiers accumulate via DpCalculator", DpAccumulate),
    ("Suspend / Unsuspend toggle the isSuspended flag", SuspendToggles),
    ("SetFlag / ClearFlag write an arbitrary named flag", NamedFlags),
    ("Previously-dropped keyword kinds are now applied, not unsupported", KeywordKindsApplied),
    ("Unknown kind is recorded as unsupported", UnknownIsUnsupported),
    ("A mutation targeting a missing card is skipped", MissingTargetSkipped),
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

void DpRelativeApplied()
{
    var (repo, sink) = NewSink();
    sink.Apply(Mutation(MatchStateMutationSink.AddDpModifierKind, new() { [MatchStateMutationSink.DpValueKey] = 3000 }));

    AssertEqual(1, sink.AppliedCount, "applied");
    DpModifier[] mods = ReadDpModifiers(repo);
    AssertEqual(1, mods.Length, "one modifier");
    AssertTrue(mods[0].IsRelative, "relative");
    AssertEqual(3000, mods[0].Value, "value");
    AssertEqual(8000, DpCalculator.ComputeDp(5000, mods), "battle/observation DP reflects +3000");
}

void DpAbsoluteApplied()
{
    var (repo, sink) = NewSink();
    sink.Apply(Mutation(MatchStateMutationSink.AddDpModifierKind, new()
    {
        [MatchStateMutationSink.DpValueKey] = 9000,
        [MatchStateMutationSink.DpAbsoluteKey] = true,
    }));

    DpModifier[] mods = ReadDpModifiers(repo);
    AssertTrue(mods[0].IsAbsolute, "absolute");
    AssertEqual(9000, DpCalculator.ComputeDp(5000, mods), "absolute set overrides base");
}

void DpAccumulate()
{
    var (repo, sink) = NewSink();
    sink.Apply(Mutation(MatchStateMutationSink.AddDpModifierKind, new() { [MatchStateMutationSink.DpValueKey] = 2000 }));
    sink.Apply(Mutation(MatchStateMutationSink.AddDpModifierKind, new() { [MatchStateMutationSink.DpValueKey] = -1000 }));

    DpModifier[] mods = ReadDpModifiers(repo);
    AssertEqual(2, mods.Length, "two modifiers accumulate");
    AssertEqual(6000, DpCalculator.ComputeDp(5000, mods), "5000 +2000 -1000 = 6000");
}

void SuspendToggles()
{
    var (repo, sink) = NewSink();
    sink.Apply(Mutation(MatchStateMutationSink.SuspendKind, new()));
    AssertEqual(true, Flag(repo, MatchStateMutationSink.SuspendedFlagKey), "suspended after Suspend");

    sink.Apply(Mutation(MatchStateMutationSink.UnsuspendKind, new()));
    AssertEqual(false, Flag(repo, MatchStateMutationSink.SuspendedFlagKey), "not suspended after Unsuspend");
}

void NamedFlags()
{
    var (repo, sink) = NewSink();
    sink.Apply(Mutation(MatchStateMutationSink.SetFlagKind, new() { [MatchStateMutationSink.FlagKeyKey] = "cannotAttack" }));
    AssertEqual(true, Flag(repo, "cannotAttack"), "named flag set");

    sink.Apply(Mutation(MatchStateMutationSink.ClearFlagKind, new() { [MatchStateMutationSink.FlagKeyKey] = "cannotAttack" }));
    AssertEqual(false, Flag(repo, "cannotAttack"), "named flag cleared");
}

void KeywordKindsApplied()
{
    var (repo, sink) = NewSink();
    foreach (string kind in new[] { "RequestBlitzAttack", "DeleteRetaliationTarget", "ApplyArmorPurge" })
    {
        sink.Apply(Mutation(kind, new()));
    }

    AssertEqual(0, sink.UnsupportedCount, "no longer unsupported");
    AssertEqual(3, sink.AppliedCount, "all three keyword kinds applied");
    AssertEqual(true, Flag(repo, "hasBlitz"), "blitz flag");
}

void UnknownIsUnsupported()
{
    var (_, sink) = NewSink();
    sink.Apply(Mutation("TotallyUnknownKind", new()));
    AssertEqual(1, sink.UnsupportedCount, "unknown kind recorded as unsupported");
    AssertEqual(0, sink.AppliedCount, "nothing applied");
}

void MissingTargetSkipped()
{
    var repo = new InMemoryCardInstanceRepository();
    var sink = new MatchStateMutationSink(repo); // empty repo
    sink.Apply(Mutation(MatchStateMutationSink.SuspendKind, new()));
    AssertEqual(1, sink.SkippedCount, "mutation on a missing card is skipped");
}

// --- Helpers -------------------------------------------------------------

(InMemoryCardInstanceRepository Repo, MatchStateMutationSink Sink) NewSink()
{
    var repo = new InMemoryCardInstanceRepository();
    repo.Upsert(new CardInstanceRecord(Card, new HeadlessEntityId("def"), P1));
    return (repo, new MatchStateMutationSink(repo));
}

EffectMutation Mutation(string kind, Dictionary<string, object?> values) => new(kind, Card, values);

DpModifier[] ReadDpModifiers(InMemoryCardInstanceRepository repo)
{
    repo.TryGetInstance(Card, out CardInstanceRecord? record);
    return record!.Metadata.TryGetValue(MatchStateMutationSink.DpModifiersKey, out object? raw) && raw is IEnumerable<DpModifier> mods
        ? mods.ToArray()
        : Array.Empty<DpModifier>();
}

bool Flag(InMemoryCardInstanceRepository repo, string key)
{
    repo.TryGetInstance(Card, out CardInstanceRecord? record);
    return record!.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;
}

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
