using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

// F-8.4: turn / ownership predicates ("during your turn", "your opponent's Digimon").

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Action Body)[]
{
    ("IsOwnerTurn true on the owner's turn", () => AssertTrue(TurnOwnershipHelpers.IsOwnerTurn(P1, P1), "owner turn")),
    ("IsOwnerTurn false on the opponent's turn", () => AssertFalse(TurnOwnershipHelpers.IsOwnerTurn(P2, P1), "not owner turn")),
    ("IsOwnerTurn false when no turn player", () => AssertFalse(TurnOwnershipHelpers.IsOwnerTurn(null, P1), "no turn player")),
    ("IsOpponentTurn true on the opponent's turn", () => AssertTrue(TurnOwnershipHelpers.IsOpponentTurn(P2, P1), "opponent turn")),
    ("IsOpponentTurn false on the owner's turn", () => AssertFalse(TurnOwnershipHelpers.IsOpponentTurn(P1, P1), "not opponent turn")),
    ("IsOpponentTurn false when no turn player", () => AssertFalse(TurnOwnershipHelpers.IsOpponentTurn(null, P1), "no turn player")),
    ("IsOwner true for same player", () => AssertTrue(TurnOwnershipHelpers.IsOwner(P1, P1), "same owner")),
    ("IsOwner false for different player", () => AssertFalse(TurnOwnershipHelpers.IsOwner(P2, P1), "different owner")),
    ("IsOpponent true for different player", () => AssertTrue(TurnOwnershipHelpers.IsOpponent(P2, P1), "opponent")),
    ("IsOpponent false for same player", () => AssertFalse(TurnOwnershipHelpers.IsOpponent(P1, P1), "not opponent")),
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
