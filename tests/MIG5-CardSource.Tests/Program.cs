using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Services;
using CardSource = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource;
using Permanent = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent;
using Player = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Player;

// (MIG5 goal-5 surface) The AS-IS CardSource instance-method surface added to the mirror CardSource class,
// delegating to verified headless gates: CanNotBeAffected (ContinuousImmunityGate), CanNotTrashFromDigivolution
// Cards (willBeRemoveSources / TrashProtectedKey marks + TrashProtectionScan). Smoke coverage for the guard +
// mark paths (an additive surface with no current caller — these are its only coverage).

HeadlessPlayerId P1 = new(1);

var tests = new (string Name, Func<Task> Body)[]
{
    ("CanNotBeAffected(null cause) is false (AS-IS null cardEffect -> false)", CanNotBeAffectedNull),
    ("CanNotBeAffected with no immunity registered is false", CanNotBeAffectedNoImmunity),
    ("CanNotTrashFromDigivolutionCards is true when willBeRemoveSources mark is set", TrashProtectedByMark),
    ("CanNotTrashFromDigivolutionCards is true when the trash-protected stamp is set", TrashProtectedByStamp),
    ("CanNotTrashFromDigivolutionCards is false with no mark/stamp and null cause", TrashNotProtected),
    ("HasSameCardName is true for two cards sharing a name", HasSameName),
    ("HasDigimonColor is true for a Digimon with that color, false for the wrong color", DigimonColor),
    ("Player.GetBattleAreaPermanents/Digimons returns the seated player's battle-area cards", PlayerZoneReads),
    ("Player.Enemy returns the other seat; SetLose marks the player lost", PlayerEnemyAndLose),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");
await Task.CompletedTask;

// --- Tests ---------------------------------------------------------------

Task CanNotBeAffectedNull()
{
    EngineContext context = Board();
    HeadlessEntityId card = Place(context, "C", new Dictionary<string, object?>());
    var cs = new CardSource(context, card, P1, P1);
    AssertFalse(cs.CanNotBeAffected(null), "null cause is never blocked");
    return Task.CompletedTask;
}

Task CanNotBeAffectedNoImmunity()
{
    EngineContext context = Board();
    HeadlessEntityId card = Place(context, "C", new Dictionary<string, object?>());
    HeadlessEntityId src = Place(context, "SRC", new Dictionary<string, object?>());
    var cs = new CardSource(context, card, P1, P1);
    AssertFalse(cs.CanNotBeAffected(src), "no immunity registered -> not blocked");
    return Task.CompletedTask;
}

Task TrashProtectedByMark()
{
    EngineContext context = Board();
    HeadlessEntityId card = Place(context, "C", new Dictionary<string, object?>(StringComparer.Ordinal) { ["willBeRemoveSources"] = true });
    var cs = new CardSource(context, card, P1, P1);
    AssertTrue(cs.CanNotTrashFromDigivolutionCards(null), "willBeRemoveSources mark -> protected");
    return Task.CompletedTask;
}

Task TrashProtectedByStamp()
{
    EngineContext context = Board();
    HeadlessEntityId card = Place(context, "C", new Dictionary<string, object?>(StringComparer.Ordinal) { ["cannotTrashFromDigivolution"] = true });
    var cs = new CardSource(context, card, P1, P1);
    AssertTrue(cs.CanNotTrashFromDigivolutionCards(null), "trash-protected stamp -> protected");
    return Task.CompletedTask;
}

Task TrashNotProtected()
{
    EngineContext context = Board();
    HeadlessEntityId card = Place(context, "C", new Dictionary<string, object?>());
    var cs = new CardSource(context, card, P1, P1);
    AssertFalse(cs.CanNotTrashFromDigivolutionCards(null), "no mark/stamp + null cause -> not protected");
    return Task.CompletedTask;
}

Task HasSameName()
{
    EngineContext context = Board();
    HeadlessEntityId a = PlaceNamed(context, "A", "Agumon");
    HeadlessEntityId b = PlaceNamed(context, "B", "Agumon");
    HeadlessEntityId c = PlaceNamed(context, "C", "Gabumon");
    AssertTrue(new CardSource(context, a, P1, P1).HasSameCardName(new CardSource(context, b, P1, P1)), "shared name");
    AssertFalse(new CardSource(context, a, P1, P1).HasSameCardName(new CardSource(context, c, P1, P1)), "different name");
    return Task.CompletedTask;
}

Task DigimonColor()
{
    EngineContext context = Board();
    HeadlessEntityId d = PlaceColored(context, "D", "Red");
    var cs = new CardSource(context, d, P1, P1);
    AssertTrue(cs.HasDigimonColor("Red"), "Digimon has Red");
    AssertFalse(cs.HasDigimonColor("Blue"), "Digimon lacks Blue");
    return Task.CompletedTask;
}

Task PlayerZoneReads()
{
    EngineContext context = Board();
    Place(context, "M1", new Dictionary<string, object?>());
    Place(context, "M2", new Dictionary<string, object?>());
    var player = new Player(context, P1);
    AssertEqual(2, player.GetBattleAreaPermanents().Count, "two battle-area permanents");
    AssertEqual(2, player.GetBattleAreaDigimons().Count, "both are Digimon");
    return Task.CompletedTask;
}

Task PlayerEnemyAndLose()
{
    EngineContext context = Board();
    var p1 = new Player(context, P1);
    AssertEqual(new HeadlessPlayerId(2), p1.Enemy!.PlayerId, "enemy is P2");
    p1.SetLose();
    AssertTrue(context.PlayerStatusController.IsLose(P1), "P1 marked lost");
    return Task.CompletedTask;
}

// --- Helpers -------------------------------------------------------------

HeadlessEntityId PlaceNamed(EngineContext context, string tag, string name)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, name, new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1));
    return id;
}

HeadlessEntityId PlaceColored(EngineContext context, string tag, string color)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { color } }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}


EngineContext Board()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 51);
    context.TurnController.Initialize(new[] { P1, new HeadlessPlayerId(2) }, P1);
    return context;
}

HeadlessEntityId Place(EngineContext context, string tag, Dictionary<string, object?> meta)
{
    ((CardDatabase)context.CardRepository).Upsert(new CardRecord(new HeadlessEntityId($"DEF:{tag}"), tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var id = new HeadlessEntityId($"card:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{tag}"), P1, Metadata: meta));
    context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
