using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

// F-7: inherited-effect activation rule. An inherited effect (the bottom text of a digivolution source)
// is active only when its source is an UNDER-card (not the top), is not flipped, and the permanent is a
// Digimon. A main effect is active only when its source IS the top card.

HeadlessEntityId Egg = new("egg");
HeadlessEntityId Mid = new("mid");
HeadlessEntityId Top = new("top");

DigivolutionStack Stack = new(new[]
{
    new StackedCard(Egg, "EGG-01", StackRole.DigiEgg, Level: 2, BaseDp: 0),
    new StackedCard(Mid, "MID-01", StackRole.Digivolution, Level: 3, BaseDp: 3000),
    new StackedCard(Top, "TOP-01", StackRole.Top, Level: 4, BaseDp: 5000),
});

var tests = new (string Name, Action Body)[]
{
    ("Inherited effect on an under-card is active (Digimon, not flipped)", () =>
        AssertTrue(InheritedEffectHelpers.IsInheritedEffectActive(Stack, Egg, sourceFlipped: false, permanentIsDigimon: true), "egg inherited active")),
    ("Inherited effect on the TOP card is not an inherited effect", () =>
        AssertFalse(InheritedEffectHelpers.IsInheritedEffectActive(Stack, Top, sourceFlipped: false, permanentIsDigimon: true), "top is not inherited")),
    ("Flipped source disables the inherited effect", () =>
        AssertFalse(InheritedEffectHelpers.IsInheritedEffectActive(Stack, Mid, sourceFlipped: true, permanentIsDigimon: true), "flipped source inactive")),
    ("Non-Digimon permanent disables inherited effects", () =>
        AssertFalse(InheritedEffectHelpers.IsInheritedEffectActive(Stack, Egg, sourceFlipped: false, permanentIsDigimon: false), "non-Digimon inactive")),
    ("A card not on the stack has no inherited effect", () =>
        AssertFalse(InheritedEffectHelpers.IsInheritedEffectActive(Stack, new HeadlessEntityId("other"), sourceFlipped: false, permanentIsDigimon: true), "off-stack inactive")),
    ("Main effect is active only on the top card", () =>
    {
        AssertTrue(InheritedEffectHelpers.IsMainEffectActive(Stack, Top), "top main active");
        AssertFalse(InheritedEffectHelpers.IsMainEffectActive(Stack, Egg), "under-card main inactive");
    }),
    ("ActiveInheritedSources lists non-flipped under-cards for a Digimon", () =>
    {
        IReadOnlyList<HeadlessEntityId> active = InheritedEffectHelpers.ActiveInheritedSources(Stack, _ => false, permanentIsDigimon: true);
        AssertEqual(2, active.Count, "two under-cards active");
        AssertTrue(active.Contains(Egg) && active.Contains(Mid), "egg and mid active");
        AssertFalse(active.Contains(Top), "top excluded");
    }),
    ("ActiveInheritedSources excludes a flipped under-card", () =>
    {
        IReadOnlyList<HeadlessEntityId> active = InheritedEffectHelpers.ActiveInheritedSources(Stack, id => id == Mid, permanentIsDigimon: true);
        AssertEqual(1, active.Count, "only egg active");
        AssertTrue(active.Contains(Egg), "egg active");
        AssertFalse(active.Contains(Mid), "flipped mid excluded");
    }),
    ("ActiveInheritedSources is empty for a non-Digimon permanent", () =>
        AssertEqual(0, InheritedEffectHelpers.ActiveInheritedSources(Stack, _ => false, permanentIsDigimon: false).Count, "non-Digimon none")),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
