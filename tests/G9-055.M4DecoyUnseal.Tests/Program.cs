// M-4 (G9-055): Decoy un-seal. (C-Del 3c-2b conversion) The old test proved the invented redirect gate
// (DeletionReplacementGate.FindDecoyRedirect) recognised a granted Decoy keyword. That gate is retired: Decoy
// now fires ONLY through its AS-IS home — the printed DecoySelfEffect ActivateClass resolved by the PRE cut-in
// window, whose body CardEffectCommons.DecoyProcess deletes the decoy self and, on success, redirect-saves a
// matching owner-battle-area DIGIMON ally (clears that ally's willBeRemoveField). These tests drive that printed
// Process directly (the pattern C-Del-3C2A DormantSurvival uses) so the SAME behaviour the gate unit-tests
// asserted — a Decoy holder redirect-saves a matching ally; the permanentCondition narrows the protected set;
// a Tamer is Digimon-only-excluded — is proven via the AS-IS window path. No retired gate symbol is referenced.

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Decoy self-effect fires and redirect-saves a matching ally (keyword recognised)", KeywordRecognised),
    ("permanentCondition MATCHES the protected target -> the ally is redirect-saved", PredicateMatchRedirects),
    ("permanentCondition does NOT match -> no redirect (predicate honored, not flattened)", PredicateMismatchNoRedirect),
    ("a Tamer ally is NOT redirect-protected (AS-IS Digimon-only)", TamerAllyNotProtected),
    ("no matching ally -> the decoy self still dies, nothing is saved (control, no NRE)", NoAllyControl),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// AS-IS DecoyEffect:60 CanSelectPermanentCondition, verbatim.
Func<Permanent, bool> DecoyCandidateRule(EngineContext ctx, HeadlessEntityId decoy, Func<Permanent, bool>? condition) =>
    p => p.InstanceId != decoy && CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(p, V(ctx, decoy)) &&
         (condition is null || condition(p));

async Task KeywordRecognised()
{
    EngineContext ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var decoy = await Place(ctx, P1, "DECOY");
    var ally = await Place(ctx, P1, "ALLY");
    await Place(ctx, P2, "ENEMY");
    Perm(ctx, ally).willBeRemoveField = true;

    ICardEffect act = CardEffectFactory.DecoySelfEffect(false, V(ctx, decoy), null, null, "Decoy", "Decoy");
    await CardEffectCommons.DecoyProcess(act, Perm(ctx, decoy), DecoyCandidateRule(ctx, decoy, null));

    AssertTrue(Deleted(ctx, P1, decoy), "the Decoy self was deleted");
    AssertTrue(!Perm(ctx, ally).willBeRemoveField, "the matching ally was redirect-saved (willBeRemoveField cleared)");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, ally), "the redirected ally is still on the battle area");
}

async Task PredicateMatchRedirects()
{
    EngineContext ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var decoy = await Place(ctx, P1, "DECOY", level: 3);
    var ally = await Place(ctx, P1, "ALLY", level: 4);
    await Place(ctx, P2, "ENEMY");
    Perm(ctx, ally).willBeRemoveField = true;

    ICardEffect act = CardEffectFactory.DecoySelfEffect(false, V(ctx, decoy), null, p => p.Level == 4, "Decoy", "Decoy");
    await CardEffectCommons.DecoyProcess(act, Perm(ctx, decoy), DecoyCandidateRule(ctx, decoy, p => p.Level == 4));

    AssertTrue(Deleted(ctx, P1, decoy), "the Decoy self was deleted");
    AssertTrue(!Perm(ctx, ally).willBeRemoveField, "the condition-matching ally was redirect-saved");
}

async Task PredicateMismatchNoRedirect()
{
    EngineContext ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var decoy = await Place(ctx, P1, "DECOY", level: 3);
    var ally = await Place(ctx, P1, "ALLY", level: 3);
    await Place(ctx, P2, "ENEMY");
    Perm(ctx, ally).willBeRemoveField = true;

    ICardEffect act = CardEffectFactory.DecoySelfEffect(false, V(ctx, decoy), null, p => p.Level == 4, "Decoy", "Decoy");
    await CardEffectCommons.DecoyProcess(act, Perm(ctx, decoy), DecoyCandidateRule(ctx, decoy, p => p.Level == 4));

    AssertTrue(Deleted(ctx, P1, decoy), "the Decoy self was still deleted");
    AssertTrue(Perm(ctx, ally).willBeRemoveField, "the non-matching ally was NOT redirect-saved (predicate honored)");
}

async Task TamerAllyNotProtected()
{
    EngineContext ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var decoy = await Place(ctx, P1, "DECOY");
    var tamerAlly = await Place(ctx, P1, "TAMER-ALLY", cardType: "Tamer");
    await Place(ctx, P2, "ENEMY");
    Perm(ctx, tamerAlly).willBeRemoveField = true;

    ICardEffect act = CardEffectFactory.DecoySelfEffect(false, V(ctx, decoy), null, null, "Decoy", "Decoy");
    await CardEffectCommons.DecoyProcess(act, Perm(ctx, decoy), DecoyCandidateRule(ctx, decoy, null));

    AssertTrue(Deleted(ctx, P1, decoy), "the Decoy self was still deleted");
    AssertTrue(Perm(ctx, tamerAlly).willBeRemoveField, "a Tamer ally is not a Decoy-protected permanent (Digimon-only)");
}

async Task NoAllyControl()
{
    EngineContext ctx = Ctx();
    using var scope = AmbientMatchContext.Enter(ctx);
    var decoy = await Place(ctx, P1, "DECOY");
    await Place(ctx, P2, "ENEMY");

    ICardEffect act = CardEffectFactory.DecoySelfEffect(false, V(ctx, decoy), null, null, "Decoy", "Decoy");
    // No matching ally -> after the self dies, the redirect selection finds no target (no NRE).
    await CardEffectCommons.DecoyProcess(act, Perm(ctx, decoy), DecoyCandidateRule(ctx, decoy, null));

    AssertTrue(Deleted(ctx, P1, decoy), "the Decoy self was deleted even with no redirect target");
}

// --- Harness ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 955);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));
CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));
HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level = 4, string cardType = "Digimon")
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = level }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false, ["level"] = level }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

bool Deleted(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) => !InZone(ctx, owner, ChoiceZone.BattleArea, id);
bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
