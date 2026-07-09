using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (RD-2) AS-IS CardSource.MatchColorRequirement (CardSource.cs:255-321): an option is playable only if every
// one of its colors is present on some owner field/breeding permanent's colors, unless an ignore-color effect
// applies. CanNotPlayThisOption gates on !MatchColorRequirement.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

// Build a context; place an option (given colors) in P1's hand and the given colored Digimon on each zone.
async Task<(EngineContext ctx, HeadlessEntityId option)> Setup(
    string[] optionColors, (ChoiceZone zone, string color)[] field)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 255);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;

    var optDef = new HeadlessEntityId("OPT");
    cards.Upsert(new CardRecord(optDef, "OPT", "Opt",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = optionColors },
        CardType: "Option"));
    var option = new HeadlessEntityId("p1:OPT");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(option, optDef, P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, option, ChoiceZone.None, ChoiceZone.Hand));

    int i = 0;
    foreach (var (zone, color) in field)
    {
        var def = new HeadlessEntityId($"D{color}");
        cards.Upsert(new CardRecord(def, $"D{color}", $"D{color}",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { color } }, CardType: "Digimon"));
        var id = new HeadlessEntityId($"p1:field:{i++}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, P1));
        await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone));
    }

    return (ctx, option);
}

bool MatchesFor(string[] optionColors, (ChoiceZone, string)[] field)
{
    var (ctx, option) = Setup(optionColors, field).GetAwaiter().GetResult();
    return OptionColorRequirement.Matches(ctx, P1, option);
}

// --- 1. Colorless option: no requirement, always playable. ---
Check(MatchesFor(Array.Empty<string>(), Array.Empty<(ChoiceZone, string)>()),
    "a colorless option has no color requirement");

// --- 2. Red option + a Red Digimon on the battle area: playable. ---
Check(MatchesFor(new[] { "Red" }, new[] { (ChoiceZone.BattleArea, "Red") }),
    "a Red option is playable with a Red Digimon in the battle area");

// --- 3. Red option + no field permanent: NOT playable. ---
Check(!MatchesFor(new[] { "Red" }, Array.Empty<(ChoiceZone, string)>()),
    "a Red option is NOT playable with no field permanent");

// --- 4. Blue option + only a Red field: NOT playable (color absent). ---
Check(!MatchesFor(new[] { "Blue" }, new[] { (ChoiceZone.BattleArea, "Red") }),
    "a Blue option is NOT playable when only a Red Digimon is in play");

// --- 5. Two-color option (Red+Blue) + only Red field: NOT playable (not ALL colors). ---
Check(!MatchesFor(new[] { "Red", "Blue" }, new[] { (ChoiceZone.BattleArea, "Red") }),
    "a Red+Blue option needs BOTH colors present (only Red -> not playable)");

// --- 5b. Two-color option (Red+Blue) + Red and Blue field: playable. ---
Check(MatchesFor(new[] { "Red", "Blue" }, new[] { (ChoiceZone.BattleArea, "Red"), (ChoiceZone.BattleArea, "Blue") }),
    "a Red+Blue option is playable when both a Red and a Blue Digimon are in play");

// --- 6. Red option + a Red Digimon in the BREEDING area: playable (breeding counts). ---
Check(MatchesFor(new[] { "Red" }, new[] { (ChoiceZone.BreedingArea, "Red") }),
    "a Red option is playable with a Red Digimon in the breeding area (AS-IS GetFieldPermanents spans breeding)");

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-2 option-color checks passed.");
