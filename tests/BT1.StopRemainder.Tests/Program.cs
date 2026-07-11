// BT1 STOP-remainder pass — assertion tests for the 5 newly-ported cards (078 / 084-br2 / 056 / 087 / 109)
// and the engine primitives they added. Each test drives the card's real effect class (via
// ActivatedEffectResolver, which dispatches by CardNumber) with a ScriptedChoiceProvider and asserts the
// observable outcome.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT1_078: [When Attacking] reveal 3, digivolve a level-6 green onto self for free, remaining to deck bottom", Bt1_078_RevealDigivolve),
    ("BT1_078: skipping the pick sends all revealed to the deck bottom, no digivolve", Bt1_078_SkipNoDigivolve),
    ("BT1_084 br2: [When Attacking] return a level-6 source to hand and unsuspend self", Bt1_084_SourceToHandUnsuspend),
    ("BT1_056: [On Play] play a Tinkermon from TRASH cost-free (multi-zone pool)", Bt1_056_PlayFromTrash),
    ("BT1_056: [On Play] play a Tinkermon from HAND cost-free (multi-zone pool)", Bt1_056_PlayFromHand),
    ("BT1_087: [On Play] add a YELLOW security card to hand -> Recovery+1 -> shuffle", Bt1_087_YellowRecovers),
    ("BT1_087: [On Play] a NON-yellow security card to hand -> NO recovery", Bt1_087_NonYellowNoRecover),
    ("BT1_109: [Main] green level-5->6 digivolution cost is reduced by 4", Bt1_109_ReducesMatching),
    ("BT1_109: a NON-matching digivolution (level-4 target) is NOT reduced", Bt1_109_IgnoresNonMatching),
    ("BT1_109: with no BT1_109 active, cost is unchanged (baseline)", Bt1_109_BaselineNoReduction),
    ("BT1_088: [Main] suspend self -> reveal top; a Digimon goes to hand", Bt1_088_RevealDigimonToHand),
    ("BT1_088: [Main] a non-Digimon reveal goes to the deck bottom; gate needs a level-5+ green", Bt1_088_NonDigimonToBottomAndGate),
    ("BT1_089: [Main] suspend self -> hatch when the breeding area is empty (hatch precedes move)", Bt1_089_HatchBranch),
    ("BT1_089: [Main] with a level-3+ breeding Digimon (no hatch possible), move it to the battle area", Bt1_089_MoveBranch),
    ("BT1_021: [When Attacking] gains 3 NOW and registers the EoT -3 PER ACTIVATION (no static every-turn loss)", Bt1_021_AttackRegisteredReversal),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ---- BT1_078 ----------------------------------------------------------------

async Task Bt1_078_RevealDigivolve()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(5);
    // BT1_078: a level-5 green Digimon on the battle area.
    var self = Battle(ctx, "BT1_078", "BT1_078", level: 5, colors: new[] { "Green" });
    // Library top 3: one selectable (level-6 green, has an evolution cost so TryGetEvolutionCost passes), two not.
    var good = Library(ctx, "g6", "G6", level: 6, colors: new[] { "Green" }, evolutionCost: 3);
    var bad1 = Library(ctx, "b4a", "B4", level: 4, colors: new[] { "Green" });
    var bad2 = Library(ctx, "b4b", "B4", level: 4, colors: new[] { "Green" });

    Script(ctx, ChoiceResult.Select(good));
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnAllyAttack);

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, good), "the selected level-6 is the new battle-area top (digivolved)");
    AssertFalse(InZone(ctx, ChoiceZone.BattleArea, self), "BT1_078 is no longer a standalone top (became a source)");
    AssertTrue(SourcesOf(ctx, good).Contains(self.Value), "BT1_078 is now a digivolution source under the level-6");
    AssertTrue(InZone(ctx, ChoiceZone.Library, bad1) && InZone(ctx, ChoiceZone.Library, bad2), "the 2 non-selected revealed cards went to the deck");
    AssertEqual(5, ctx.MemoryController.Current.Current, "no memory paid (free digivolve)");
}

async Task Bt1_078_SkipNoDigivolve()
{
    EngineContext ctx = NewContext();
    var self = Battle(ctx, "BT1_078", "BT1_078", level: 5, colors: new[] { "Green" });
    var good = Library(ctx, "g6", "G6", level: 6, colors: new[] { "Green" }, evolutionCost: 3);
    var b1 = Library(ctx, "b4a", "B4", level: 4, colors: new[] { "Green" });

    Script(ctx, ChoiceResult.Skip());
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnAllyAttack);

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, self), "BT1_078 stays the top (no digivolve)");
    AssertTrue(InZone(ctx, ChoiceZone.Library, good) && InZone(ctx, ChoiceZone.Library, b1), "all revealed cards went to the deck bottom");
}

// ---- BT1_084 br2 ------------------------------------------------------------

async Task Bt1_084_SourceToHandUnsuspend()
{
    EngineContext ctx = NewContext();
    // BT1_084 suspended on the battle area, with a level-6 Digimon digivolution source under it.
    var src = Instance(ctx, "src6", "SRC6", level: 6);
    var self = Battle(ctx, "BT1_084", "BT1_084", level: 6, colors: new[] { "White" }, sources: new[] { src.Value }, suspended: true);

    Script(ctx, ChoiceResult.Select(src));
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnAllyAttack);

    AssertTrue(InZone(ctx, ChoiceZone.Hand, src), "the level-6 digivolution source returned to hand");
    AssertFalse(IsSuspended(ctx, self), "BT1_084 was unsuspended");
    AssertFalse(SourcesOf(ctx, self).Contains(src.Value), "the returned source is no longer under BT1_084");
}

// ---- BT1_056 ----------------------------------------------------------------

async Task Bt1_056_PlayFromTrash()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0); // cost-free even at 0 memory.
    var self = Battle(ctx, "BT1_056", "BT1_056", level: 3, colors: new[] { "Yellow" });
    var handTink = Hand(ctx, "hT", "Tinkermon", level: 3);
    var trashTink = Trash(ctx, "tT", "Tinkermon", level: 3);

    Script(ctx, ChoiceResult.Select(trashTink));
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnEnterFieldAnyone);

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, trashTink), "the trash Tinkermon was played to the battle area");
    AssertFalse(InZone(ctx, ChoiceZone.Trash, trashTink), "it left the trash");
    AssertTrue(InZone(ctx, ChoiceZone.Hand, handTink), "the hand Tinkermon was not played");
}

async Task Bt1_056_PlayFromHand()
{
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    var self = Battle(ctx, "BT1_056", "BT1_056", level: 3, colors: new[] { "Yellow" });
    var handTink = Hand(ctx, "hT", "Tinkermon", level: 3);
    var trashTink = Trash(ctx, "tT", "Tinkermon", level: 3);

    Script(ctx, ChoiceResult.Select(handTink));
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnEnterFieldAnyone);

    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, handTink), "the hand Tinkermon was played to the battle area");
    AssertFalse(InZone(ctx, ChoiceZone.Hand, handTink), "it left the hand");
    AssertTrue(InZone(ctx, ChoiceZone.Trash, trashTink), "the trash Tinkermon was not played");
}

// ---- BT1_087 ----------------------------------------------------------------

async Task Bt1_087_YellowRecovers()
{
    EngineContext ctx = NewContext();
    var self = Battle(ctx, "BT1_087", "BT1_087", level: 3, colors: new[] { "Yellow" });
    var secYellow = Security(ctx, "secY", "SecY", colors: new[] { "Yellow" });
    var secBlue = Security(ctx, "secB", "SecB", colors: new[] { "Blue" });
    var libTop = Library(ctx, "libR", "LibR");

    int secBefore = ZoneCount(ctx, ChoiceZone.Security);
    Script(ctx, ChoiceResult.Select(secYellow));
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnEnterFieldAnyone);

    AssertTrue(InZone(ctx, ChoiceZone.Hand, secYellow), "the yellow security card is now in hand");
    AssertTrue(InZone(ctx, ChoiceZone.Security, libTop), "Recovery moved the top deck card into security");
    AssertEqual(secBefore, ZoneCount(ctx, ChoiceZone.Security), "security count: -1 (to hand) +1 (recovery) = unchanged");
    AssertTrue(InZone(ctx, ChoiceZone.Security, secBlue), "the other security card is still in security");
}

async Task Bt1_087_NonYellowNoRecover()
{
    EngineContext ctx = NewContext();
    var self = Battle(ctx, "BT1_087", "BT1_087", level: 3, colors: new[] { "Yellow" });
    var secYellow = Security(ctx, "secY", "SecY", colors: new[] { "Yellow" });
    var secBlue = Security(ctx, "secB", "SecB", colors: new[] { "Blue" });
    var libTop = Library(ctx, "libR", "LibR");

    Script(ctx, ChoiceResult.Select(secBlue));
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnEnterFieldAnyone);

    AssertTrue(InZone(ctx, ChoiceZone.Hand, secBlue), "the blue security card is now in hand");
    AssertFalse(InZone(ctx, ChoiceZone.Security, libTop), "no Recovery: the top deck card stays in the library");
    AssertEqual(1, ZoneCount(ctx, ChoiceZone.Security), "security count dropped to 1 (blue moved out, no recovery)");
}

// ---- BT1_109 ----------------------------------------------------------------

async Task Bt1_109_ReducesMatching()
{
    EngineContext ctx = NewContext();
    var opt = Instance(ctx, "BT1_109", "BT1_109", cardType: "Option");
    await ActivatedEffectResolver.ResolveAsync(ctx, opt, P1, EffectTiming.OptionSkill);

    var target = Battle(ctx, "t5", "T5", level: 5, colors: new[] { "Green" });   // digivolving-FROM (level 5 green)
    var toCard = Hand(ctx, "to6", "TO6", level: 6, colors: new[] { "Green" });    // digivolving-TO (level 6 green)

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(ctx, toCard, baseDigivolutionCost: 5, digivolveTargetPermanentId: target);
    AssertEqual(1, cost, "a green level-5->6 digivolve is reduced by 4 (5 -> 1)");
}

async Task Bt1_109_IgnoresNonMatching()
{
    EngineContext ctx = NewContext();
    var opt = Instance(ctx, "BT1_109", "BT1_109", cardType: "Option");
    await ActivatedEffectResolver.ResolveAsync(ctx, opt, P1, EffectTiming.OptionSkill);

    var target4 = Battle(ctx, "t4", "T4", level: 4, colors: new[] { "Green" });   // level 4, not 5 -> no match
    var toCard = Hand(ctx, "to6", "TO6", level: 6, colors: new[] { "Green" });

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(ctx, toCard, baseDigivolutionCost: 5, digivolveTargetPermanentId: target4);
    AssertEqual(5, cost, "a non-matching (level-4 target) digivolve is NOT reduced");
}

async Task Bt1_109_BaselineNoReduction()
{
    EngineContext ctx = NewContext();
    var target = Battle(ctx, "t5", "T5", level: 5, colors: new[] { "Green" });
    var toCard = Hand(ctx, "to6", "TO6", level: 6, colors: new[] { "Green" });

    int cost = ContinuousModifierGate.ResolveDigivolutionCost(ctx, toCard, baseDigivolutionCost: 5, digivolveTargetPermanentId: target);
    AssertEqual(5, cost, "with no BT1_109 registered, the cost is unchanged");
    await Task.CompletedTask;
}

// ---- Harness ----------------------------------------------------------------

// ---- BT1_088 / BT1_089 [Main] (formerly STOP; unblocked by the B-2 declaration action) ----------------

async Task Bt1_088_RevealDigimonToHand()
{
    EngineContext ctx = NewContext();
    var self = Battle(ctx, "BT1_088", "BT1_088");
    Battle(ctx, "g5", "G5Green", level: 5, colors: new[] { "Green" }); // the level-5+ green gate witness
    var top = Library(ctx, "topD", "TopDigimon", level: 3);

    Script(ctx, ChoiceResult.Select(top));
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnDeclaration, declarative: true);

    AssertTrue(IsSuspended(ctx, self), "BT1_088 suspended itself (the cost)");
    AssertTrue(InZone(ctx, ChoiceZone.Hand, top), "the revealed Digimon card went to the hand (AS-IS AddHand route)");
}

async Task Bt1_088_NonDigimonToBottomAndGate()
{
    EngineContext ctx = NewContext();
    var self = Battle(ctx, "BT1_088", "BT1_088");
    var top = Place(ctx, "topO", "topO", "TopOption", ChoiceZone.Library, "Option", null, null, null, null, false);

    // Gate: WITHOUT a level-5+ green Digimon the [Main] cannot be used at all (AS-IS CanUseCondition).
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnDeclaration, declarative: true);
    AssertFalse(IsSuspended(ctx, self), "without the level-5+ green gate the skill did not fire (no self-suspend)");
    AssertTrue(InZone(ctx, ChoiceZone.Library, top), "the library is untouched");

    // With the gate satisfied, a NON-Digimon reveal routes to the deck bottom.
    Battle(ctx, "g5", "G5Green", level: 5, colors: new[] { "Green" });
    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnDeclaration, declarative: true);
    AssertTrue(IsSuspended(ctx, self), "BT1_088 suspended itself (the cost)");
    AssertTrue(InZone(ctx, ChoiceZone.Library, top), "the revealed non-Digimon card returned to the deck (bottom)");
    AssertFalse(InZone(ctx, ChoiceZone.Hand, top), "the non-Digimon card did NOT go to hand");
}

async Task Bt1_089_HatchBranch()
{
    EngineContext ctx = NewContext();
    var self = Battle(ctx, "BT1_089", "BT1_089");
    Battle(ctx, "g5", "G5Green", level: 5, colors: new[] { "Green" });
    var egg = Place(ctx, "egg", "egg", "Egg", ChoiceZone.DigitamaLibrary, "Digi-Egg", 2, null, null, null, false);

    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnDeclaration, declarative: true);

    AssertTrue(IsSuspended(ctx, self), "BT1_089 suspended itself (the cost)");
    AssertTrue(InZone(ctx, ChoiceZone.BreedingArea, egg), "the digi-egg hatched into the empty breeding area (AS-IS CanHatch branch wins)");
}

async Task Bt1_089_MoveBranch()
{
    EngineContext ctx = NewContext();
    var self = Battle(ctx, "BT1_089", "BT1_089");
    Battle(ctx, "g5", "G5Green", level: 5, colors: new[] { "Green" });
    // Breeding occupied by a level-3 Digimon (CanHatch false: breeding not empty), no digitama needed.
    var grown = Place(ctx, "grown", "grown", "Grown", ChoiceZone.BreedingArea, "Digimon", 3, null, null, null, false);

    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnDeclaration, declarative: true);

    AssertTrue(IsSuspended(ctx, self), "BT1_089 suspended itself (the cost)");
    AssertTrue(InZone(ctx, ChoiceZone.BattleArea, grown), "the level-3 breeding Digimon moved to the battle area (AS-IS else-branch)");
    AssertEqual(0, ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, ChoiceZone.BreedingArea).Count, "the breeding area is now empty");
}

async Task Bt1_021_AttackRegisteredReversal()
{
    // AS-IS BT1_021: the "-3 at end of turn" exists ONLY per [When Attacking] activation
    // (card.Owner.UntilEachTurnEndEffects.Add inside the ActivateCoroutine, :36-46) — never as a static
    // OnEndTurn effect of the card. Two attacks stack two reversals; no attack registers none.
    EngineContext ctx = NewContext();
    ctx.MemoryController.Set(0);
    var self = Battle(ctx, "BT1_021", "BT1_021", level: 4, colors: new[] { "Red" });
    ctx.RegisterEnteredCardEffects(self, P1);

    AssertEqual(0, ctx.EffectRegistry.GetEffectsForTiming("OnEndTurn").Count,
        "no attack yet: NO end-of-turn loss is registered (the old static declaration was the divergence)");

    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnAllyAttack);
    AssertEqual(3, ctx.MemoryController.Current.Current, "first activation gains +3 immediately");
    AssertEqual(1, ctx.EffectRegistry.GetEffectsForTiming("OnEndTurn").Count,
        "first activation registered exactly ONE one-shot EoT -3 (AS-IS UntilEachTurnEndEffects entry)");

    await ActivatedEffectResolver.ResolveAsync(ctx, self, P1, EffectTiming.OnAllyAttack);
    AssertEqual(6, ctx.MemoryController.Current.Current, "second activation gains +3 again (uncapped)");
    AssertEqual(2, ctx.EffectRegistry.GetEffectsForTiming("OnEndTurn").Count,
        "second activation stacks a SECOND reversal (AS-IS: attack twice → lose 6 at end of turn)");
}

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

void Script(EngineContext ctx, params ChoiceResult[] choices)
{
    var provider = (ScriptedChoiceProvider)ctx.ChoiceProvider;
    provider.Clear();
    foreach (ChoiceResult c in choices) { provider.Enqueue(c); }
}

CardRecord Def(EngineContext ctx, string number, string name, string cardType, int? level, string[]? colors, int? evolutionCost)
{
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (level is int lv) { meta["level"] = lv; }
    if (colors is not null) { meta["colors"] = colors; }
    var def = new CardRecord(new HeadlessEntityId(number), number, name, meta, CardType: cardType, EvolutionCost: evolutionCost);
    ((CardDatabase)ctx.CardRepository).Upsert(def);
    return def;
}

HeadlessEntityId Place(EngineContext ctx, string idSuffix, string number, string name, ChoiceZone zone, string cardType, int? level, string[]? colors, int? evolutionCost, string[]? sources, bool suspended)
{
    Def(ctx, number, name, cardType, level, colors, evolutionCost);
    var id = new HeadlessEntityId($"p1:{idSuffix}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (sources is not null) { meta["sourceIds"] = sources; }
    if (suspended) { meta["isSuspended"] = true; }
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(number), P1, Metadata: meta));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }
    return id;
}

HeadlessEntityId Battle(EngineContext ctx, string number, string name, int? level = null, string[]? colors = null, string[]? sources = null, bool suspended = false) =>
    Place(ctx, number, number, name, ChoiceZone.BattleArea, "Digimon", level, colors, null, sources, suspended);

HeadlessEntityId Hand(EngineContext ctx, string idSuffix, string name, int? level = null, string[]? colors = null) =>
    Place(ctx, idSuffix, idSuffix, name, ChoiceZone.Hand, "Digimon", level, colors, null, null, false);

HeadlessEntityId Trash(EngineContext ctx, string idSuffix, string name, int? level = null, string[]? colors = null) =>
    Place(ctx, idSuffix, idSuffix, name, ChoiceZone.Trash, "Digimon", level, colors, null, null, false);

HeadlessEntityId Library(EngineContext ctx, string idSuffix, string name, int? level = null, string[]? colors = null, int? evolutionCost = null) =>
    Place(ctx, idSuffix, idSuffix, name, ChoiceZone.Library, "Digimon", level, colors, evolutionCost, null, false);

HeadlessEntityId Security(EngineContext ctx, string idSuffix, string name, string[]? colors = null) =>
    Place(ctx, idSuffix, idSuffix, name, ChoiceZone.Security, "Digimon", null, colors, null, null, false);

HeadlessEntityId Instance(EngineContext ctx, string number, string name, int? level = null, string cardType = "Digimon") =>
    Place(ctx, number, number, name, ChoiceZone.None, cardType, level, null, null, null, false);

bool InZone(EngineContext ctx, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Contains(id);

int ZoneCount(EngineContext ctx, ChoiceZone zone) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(P1, zone).Count;

IReadOnlyList<string> SourcesOf(EngineContext ctx, HeadlessEntityId host)
{
    if (ctx.CardInstanceRepository.TryGetInstance(host, out CardInstanceRecord? rec) && rec is not null
        && rec.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> s)
    {
        return s.ToArray();
    }
    return Array.Empty<string>();
}

bool IsSuspended(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
    && rec.Metadata.TryGetValue("isSuspended", out object? raw) && raw is bool b && b;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }
}
