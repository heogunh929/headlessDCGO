using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
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

// --- latent-2: a field pure-OPTION does NOT supply colors (AS-IS TopCard.IsPermanent, CardSource.cs:311). ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 311);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;
    var optDef = new HeadlessEntityId("O");
    cards.Upsert(new CardRecord(optDef, "O", "O", new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" } }, CardType: "Option"));
    var option = new HeadlessEntityId("p1:O");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(option, optDef, P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, option, ChoiceZone.None, ChoiceZone.Hand));
    // A field pure-Option (delay option permanent) that is Red.
    var fieldOptDef = new HeadlessEntityId("FO");
    cards.Upsert(new CardRecord(fieldOptDef, "FO", "FO", new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" } }, CardType: "Option"));
    var fieldOpt = new HeadlessEntityId("p1:FO");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(fieldOpt, fieldOptDef, P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, fieldOpt, ChoiceZone.None, ChoiceZone.BattleArea));

    Check(!OptionColorRequirement.Matches(ctx, P1, option),
        "a field pure-Option does NOT supply its color (only Digimon/Tamer/DigiEgg are permanents)");
}

// --- latent-1: a DUAL card (Digimon+Option) played as option uses its OptionColorRequirements, not its
//      printed Digimon colors. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 307);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;
    // Dual card: printed Digimon color Green, but its option-play requirement is Red.
    var dualDef = new HeadlessEntityId("DUAL");
    cards.Upsert(new CardRecord(dualDef, "DUAL", "Dual",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["colors"] = new[] { "Green" },
            [CardSource.OptionColorRequirementsKey] = new[] { "Red" },
            [CardRecord.AdditionalCardTypesKey] = new[] { "Option" },
        },
        CardType: "Digimon"));
    var dual = new HeadlessEntityId("p1:DUAL");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(dual, dualDef, P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, dual, ChoiceZone.None, ChoiceZone.Hand));

    // Field has a RED Digimon (matches the dual's OPTION requirement) but not Green.
    var redDef = new HeadlessEntityId("DRed");
    cards.Upsert(new CardRecord(redDef, "DRed", "DRed", new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" } }, CardType: "Digimon"));
    var red = new HeadlessEntityId("p1:red");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(red, redDef, P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, red, ChoiceZone.None, ChoiceZone.BattleArea));

    Check(OptionColorRequirement.Matches(ctx, P1, dual),
        "a dual card's option color requirement is its OptionColorRequirements (Red), satisfied by a Red field");

    // Now a GREEN field only: the dual's Digimon color is Green, but its OPTION requirement is Red -> NOT met.
    EngineContext ctx2 = EngineContext.CreateDefault(randomSeed: 308);
    ctx2.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards2 = (CardDatabase)ctx2.CardRepository;
    cards2.Upsert(new CardRecord(dualDef, "DUAL", "Dual",
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["colors"] = new[] { "Green" },
            [CardSource.OptionColorRequirementsKey] = new[] { "Red" },
            [CardRecord.AdditionalCardTypesKey] = new[] { "Option" },
        }, CardType: "Digimon"));
    var dual2 = new HeadlessEntityId("p1:DUAL");
    ctx2.CardInstanceRepository.Upsert(new CardInstanceRecord(dual2, dualDef, P1));
    await ctx2.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, dual2, ChoiceZone.None, ChoiceZone.Hand));
    var greenDef = new HeadlessEntityId("DGreen");
    cards2.Upsert(new CardRecord(greenDef, "DGreen", "DGreen", new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Green" } }, CardType: "Digimon"));
    var green = new HeadlessEntityId("p1:green");
    ctx2.CardInstanceRepository.Upsert(new CardInstanceRecord(green, greenDef, P1));
    await ctx2.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, green, ChoiceZone.None, ChoiceZone.BattleArea));

    Check(!OptionColorRequirement.Matches(ctx2, P1, dual2),
        "the dual card's Digimon color (Green) does NOT satisfy its option requirement (Red)");
}

// --- latent-3: an option carrying its OWN self ignore-color effect is playable with no matching field. ---
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 263);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    var cards = (CardDatabase)ctx.CardRepository;
    // Red option whose card number dispatches the self ignore-color fixture; NO field permanent.
    var optDef = new HeadlessEntityId("IGN");
    cards.Upsert(new CardRecord(optDef, "TfxOptionIgnoreColor", "Ign",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" } }, CardType: "Option"));
    var option = new HeadlessEntityId("p1:IGN");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(option, optDef, P1));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, option, ChoiceZone.None, ChoiceZone.Hand));

    Check(OptionColorRequirement.Matches(ctx, P1, option),
        "an option with its OWN ignore-color effect is playable despite no matching field color");
}

if (failures > 0) { Console.Error.WriteLine($"\n{failures} test(s) failed."); Environment.Exit(1); }
Console.WriteLine("\nall RD-2 option-color checks passed.");
