using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST7.Red;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Card group CardEffect/ST7/Red — ported card tests (group-standard project; see card_group_standard.md).
//   ST7_10: [All Turns] <Security Attack +1> + [When Attacking] <Piercing>.
//
// (RD-A6-01 re-pin) The original CardEffect.ST7.Red.Tests suite was retired in campaign (4) — 2 of its 3
// subtests asserted the deleted invented EffectRegistry surface (context.EffectRegistry.GetKeywordEffects /
// CardEffectRegistrar.RegisterOnEnterPlay's old return-value contract). The 3rd (SecurityAttackPlusOne) was a
// real behavioral pin, but it too relied on the OLD registry indirectly: `Board()` never actually moved the
// card onto the battle area (ZoneMover), yet the assertion still expected `Permanent.Strike` to change (1->2)
// and then (2->3) on a SECOND read of the SAME unchanged state -- Permanent.Strike is a pure computed
// property with no side effects (AS-IS Permanent.cs:1938-1951 / this repo's Permanent.cs:2712), so a second
// read of unchanged state cannot legitimately differ. That "2 then 3" shape was an artifact of the retired
// EffectRegistry's own bookkeeping, not real AS-IS behavior -- it is NOT reproduced here.
//
// ST7_10 (src/.../CardEffect/ST7/Red/ST7_10.cs) is fully ported (not a skeleton): CardEffects(EffectTiming.None,...)
// returns CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, ...) (a live ChangeSAttackClass :
// IChangeSAttackEffect kind-class) and CardEffects(EffectTiming.OnDetermineDoSecurityCheck,...) returns
// CardEffectFactory.PierceSelfEffect(...) (a live ActivateClass). Neither registers into any registry --
// AS-IS truth is a LIVE re-scan of the card's own EffectList at read time (Permanent.Strike_AllowMinus /
// Permanent.HasPierce, unioned into the mirror via NewModelContinuousScan / ContinuousKeywordGate.HasKeyword
// -- see that file's header, which names ST7_10 SA+1 as the exact scenario the live-scan union exists for).
// This suite pins ST7_10 through those LIVE surfaces only: place the card on the battle area, ATTACH its
// CEntity_Effect to the card's controller the same way CardEffectRegistrar.RegisterCard does for a real play
// (RegisterOnEnterPlay -- which registers nothing into any registry post-(4), it only performs that
// attachment), then read Permanent.Strike / ContinuousKeywordGate.HasKeyword.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST7_10: Security Attack +1 folds into the live Strike scan once granted (base 1 -> 2, stable on re-read)", SecurityAttackPlusOne),
    ("ST7_10: Piercing is live via ContinuousKeywordGate.HasKeyword once granted (absent before, present after)", PiercingLive),
    ("ST7_10: both grants are scoped to the card itself, not a bystander", ScopedToOwnCard),
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

// --- Tests ---------------------------------------------------------------

async Task SecurityAttackPlusOne()
{
    EngineContext context = Context();
    HeadlessEntityId card = await PlaceDigimon(context, P1, "T10", dp: 4000);

    using (AmbientMatchContext.Enter(context))
    {
        AssertEqual(1, new Permanent(context, card, P1).Strike, "base Strike is 1 (AS-IS constant seed) before ST7_10 is granted");
    }

    Register(context, card);

    using (AmbientMatchContext.Enter(context))
    {
        AssertEqual(2, new Permanent(context, card, P1).Strike, "base 1 + ST7_10's Security Attack +1 = 2");
        // Permanent.Strike is a pure computed re-scan (no mutation) -- a second read over the SAME unchanged
        // state must return the SAME value, not accumulate further.
        AssertEqual(2, new Permanent(context, card, P1).Strike, "a second read of unchanged state is stable at 2 (not cumulative)");
    }
}

async Task PiercingLive()
{
    EngineContext context = Context();
    HeadlessEntityId card = await PlaceDigimon(context, P1, "T10b", dp: 4000);

    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, card, "Piercing"), "Piercing not present before ST7_10 is granted");

    Register(context, card);

    AssertTrue(ContinuousKeywordGate.HasKeyword(context, card, "Piercing"), "Piercing is live after ST7_10 is granted");
}

async Task ScopedToOwnCard()
{
    EngineContext context = Context();
    HeadlessEntityId granted = await PlaceDigimon(context, P1, "T10c", dp: 4000);
    HeadlessEntityId bystander = await PlaceDigimon(context, P1, "BYST", dp: 4000);

    Register(context, granted);

    AssertTrue(ContinuousKeywordGate.HasKeyword(context, granted, "Piercing"), "the granted card HAS Piercing");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, bystander, "Piercing"), "a bystander does NOT get Piercing");

    using (AmbientMatchContext.Enter(context))
    {
        AssertEqual(2, new Permanent(context, granted, P1).Strike, "the granted card's Strike is 2");
        AssertEqual(1, new Permanent(context, bystander, P1).Strike, "a bystander's Strike stays at base 1");
    }
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 710);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    // Game past setup (phase != None) so the live continuous scans' CanTrigger DoneStartGame gate is open.
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

async Task<HeadlessEntityId> PlaceDigimon(EngineContext context, HeadlessPlayerId owner, string tag, int dp)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var def = new HeadlessEntityId($"DEF:{tag}");
    cards.Upsert(new CardRecord(def, def.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, def, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// (RD-A6-01) ATTACH ST7_10's CEntity_Effect to its own card's controller -- the same seam
// CardEffectRegistrar.RegisterCard uses for a real play (RegisterOnEnterPlay's load-bearing side effect
// post-(4); it registers NOTHING into any registry, see CardEffectRegistrar.cs).
void Register(EngineContext context, HeadlessEntityId card) =>
    CardEffectRegistrar.RegisterOnEnterPlay(context, new ST7_10(), "ST7_10", new CardSource(context, card, P1));

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
