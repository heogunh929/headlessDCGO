using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

// G9-001 (CARDS-ALIAS-Dispatch): cards.json carries an `effectClass` per card that is authoritative.
// Most cards' effectClass equals their card number, but ALIAS cards reuse another class — e.g. ST2_07
// (Grizzlymon) and ST3_07 (Unimon) both reuse ST1_06 (<Blocker> + [When Attacking] lose 2), and every
// alternate-art reprint `*_P2` reuses its base. The dispatch resolved effects by card NUMBER only, so those
// aliases registered nothing. CardEffectDispatch.TryCreateForCard now honors the effectClass alias.

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Action Body)[]
{
    ("Alias card resolves to its effectClass (ST2_07 -> ST1_06)", AliasResolvesToEffectClass),
    ("No effectClass metadata falls back to card number (no regression)", NoMetadataFallsBackToCardNumber),
    ("Un-ported alias is a no-op", UnportedAliasIsNoOp),
    ("RegisterCard registers the alias's effects (ST2_07 gets <Blocker>)", RegisterCardUsesAlias),
    ("RegisterCard on a non-alias card is unchanged (ST1_06 gets <Blocker>)", RegisterCardNonAliasUnchanged),
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

void AliasResolvesToEffectClass()
{
    var def = new CardRecord(new HeadlessEntityId("ST2_07"), "ST2_07", "Grizzlymon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["effectClass"] = "ST1_06" }, CardType: "Digimon");

    AssertTrue(CardEffectDispatch.TryCreateForCard(def, out var effect), "alias resolved");
    AssertEqual("ST1_06", effect!.GetType().Name, "resolved to the effectClass type, not the card number");
}

void NoMetadataFallsBackToCardNumber()
{
    // No effectClass in metadata -> resolve by card number exactly as before (preserves all existing tests).
    var def = new CardRecord(new HeadlessEntityId("ST1_06"), "ST1_06", "Grizzlymon",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

    AssertTrue(CardEffectDispatch.TryCreateForCard(def, out var effect), "card-number fallback resolved");
    AssertEqual("ST1_06", effect!.GetType().Name, "resolved by card number");
}

void UnportedAliasIsNoOp()
{
    // effectClass present but points at an un-ported class -> no-op (false), like an un-ported card.
    var def = new CardRecord(new HeadlessEntityId("ST2_07"), "ST2_07", "Grizzlymon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["effectClass"] = "NOPE_9999" }, CardType: "Digimon");

    AssertTrue(!CardEffectDispatch.TryCreateForCard(def, out var effect), "un-ported alias does not resolve");
    AssertTrue(effect is null, "no effect produced for an un-ported alias");
}

void RegisterCardUsesAlias()
{
    EngineContext context = Context();
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST2_07def"), "ST2_07", "Grizzlymon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["effectClass"] = "ST1_06" }, CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:battle:ST2_07");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("ST2_07def"), P1));

    AssertTrue(CardEffectRegistrar.RegisterCard(context, id, P1), "alias card registered");
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Blocker").Count >= 1, "ST2_07 gained <Blocker> via ST1_06");
}

void RegisterCardNonAliasUnchanged()
{
    EngineContext context = Context();
    CardDatabase cards = (CardDatabase)context.CardRepository;
    // effectClass equals the card number, as for normal loaded cards — must behave identically.
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST1_06def"), "ST1_06", "Grizzlymon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["effectClass"] = "ST1_06" }, CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:battle:ST1_06");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("ST1_06def"), P1));

    AssertTrue(CardEffectRegistrar.RegisterCard(context, id, P1), "non-alias card registered");
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Blocker").Count >= 1, "ST1_06 gained <Blocker>");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context() => EngineContext.CreateDefault(randomSeed: 901);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
