// C-Del 3c-2a DORMANT survival witnesses — the printed-factory Process bodies of the cross-card PRE
// replacement keywords Decoy & Scapegoat, driven DIRECTLY (window-routed via the AS-IS
// DeletePeremanentAndProcessAccordingToResult, which the mirror routes through the sink FlushAsync +
// DeletionOutcomeWatcher — recursive PRE/POST window path, promote-to-defer for interactive sub-deletes).
// These prove the AS-IS `willBeRemoveField = false` SURVIVAL-OWNERSHIP restoration (3c-2a): survival is owned
// by the window body, not the (still-live, not retired here) DeletionReplacementGate. The gate is NOT touched;
// these are the DORMANT completion witnesses that make the 3c-2b flip safe.
//
// Decoy   : delete THIS Digimon (the decoy self); on success the owner redirects a matching ally — clears that
//           ALLY's pending deletion (willBeRemoveField = false).
// Scapegoat: owner selects 1 matching Digimon to delete instead; when that substitute ACTUALLY dies, clears
//           THIS Digimon's pending deletion (willBeRemoveField = false). Failure (substitute survives) leaves it.
//
// Also a factory-liveness smoke for the three free-play PRE keywords (Decode/Partition/MaterialSave) already
// ported 1:1 (no body delta in 3c-2a): the printed ActivateClass builds and CanActivate reflects source state.

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("SCAPEGOAT survival: the substitute is deleted and THIS Digimon's willBeRemoveField is cleared (survives)", ScapegoatSurvives),
    ("SCAPEGOAT no-substitute control: no matching ally -> early return -> willBeRemoveField stays set (dies)", ScapegoatNoSubstitute),
    ("SCAPEGOAT failure gating: substitute EVADES (survives) -> delete fails -> THIS Digimon NOT cleared (dies)", ScapegoatSubstituteEvades),
    ("DECOY redirect: the decoy self is deleted and the selected ally's willBeRemoveField is cleared (ally survives)", DecoyRedirects),
    ("DECOY no-ally control: decoy self deleted, no ally to redirect -> nothing cleared", DecoyNoAlly),
    ("FACTORY liveness: Decode/Partition/MaterialSave printed ActivateClass build 1:1 (non-null; free-play path intact)", FactoryLiveness),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ============================================================ SCAPEGOAT

async Task ScapegoatSurvives()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var self = await Place(ctx, P1, "SELF");
    var sub = await Place(ctx, P1, "SUB");
    await Place(ctx, P2, "FOE");
    Perm(ctx, self).willBeRemoveField = true;   // THIS Digimon is in the would-be-deleted batch

    ICardEffect act = CardEffectFactory.ScapegoatSelfEffect(false, V(ctx, self), null, "Scapegoat", "Scapegoat");
    await CardEffectCommons.ScapegoatProcess(act, Perm(ctx, self), p => p.InstanceId == sub);

    AssertTrue(Deleted(ctx, P1, sub), "the selected substitute was actually deleted");
    AssertFalse(Perm(ctx, self).willBeRemoveField, "THIS Digimon's willBeRemoveField was cleared on substitute-death (survives)");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, self), "THIS Digimon is still on the battle area");
}

async Task ScapegoatNoSubstitute()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var self = await Place(ctx, P1, "SELF");
    await Place(ctx, P2, "FOE");
    Perm(ctx, self).willBeRemoveField = true;

    ICardEffect act = CardEffectFactory.ScapegoatSelfEffect(false, V(ctx, self), null, "Scapegoat", "Scapegoat");
    await CardEffectCommons.ScapegoatProcess(act, Perm(ctx, self), p => p.InstanceId == new HeadlessEntityId("nope"));

    AssertTrue(Perm(ctx, self).willBeRemoveField, "no matching substitute -> early return -> willBeRemoveField stays set (dies)");
}

async Task ScapegoatSubstituteEvades()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var self = await Place(ctx, P1, "SELF");
    // The substitute carries a WINDOW-COLLECTIBLE mandatory would-be-deleted survive replacement (the printed
    // TfxWouldBeDeleted ActivateClass at WhenPermanentWouldBeDeleted, registered). ScapegoatProcess routes the
    // substitute's deletion through DeletePeremanentAndProcessAccordingToResult → the effect-delete sink, which
    // opens the AS-IS PRE cut-in window; the mandatory replacement clears willBeRemoveField inline, so the
    // substitute is SPARED and never lands in DestroyedPermanents. (3c-2b: the retired synthetic HasEvadeKey flag
    // no longer fires — survival is owned by the window, so the substitute must carry the window form to evade.)
    var sub = await Place(ctx, P1, "TfxWouldBeDeleted");
    await Place(ctx, P2, "FOE");
    CardEffectRegistrar.RegisterCard(ctx, sub, P1);
    Perm(ctx, self).willBeRemoveField = true;

    ICardEffect act = CardEffectFactory.ScapegoatSelfEffect(false, V(ctx, self), null, "Scapegoat", "Scapegoat");
    await CardEffectCommons.ScapegoatProcess(act, Perm(ctx, self), p => p.InstanceId == sub);

    AssertFalse(Deleted(ctx, P1, sub), "the substitute SURVIVED via the PRE would-be-deleted window — it was not deleted");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, sub), "the substitute is still on the battle area (spared by the window)");
    AssertTrue(Perm(ctx, self).willBeRemoveField, "substitute survived -> delete FAILED -> THIS Digimon NOT cleared (AS-IS DestroyedPermanents gating) -> holder dies");
}

// ============================================================ DECOY

async Task DecoyRedirects()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var decoy = await Place(ctx, P1, "DECOY");
    var ally = await Place(ctx, P1, "ALLY");
    await Place(ctx, P2, "FOE");
    Perm(ctx, ally).willBeRemoveField = true;   // the ally is the one Decoy will redirect-save

    ICardEffect act = CardEffectFactory.DecoySelfEffect(false, V(ctx, decoy), null, null, "Decoy", "Decoy");
    await CardEffectCommons.DecoyProcess(act, Perm(ctx, decoy), p => p.InstanceId == ally);

    AssertTrue(Deleted(ctx, P1, decoy), "the Decoy self was actually deleted");
    AssertFalse(Perm(ctx, ally).willBeRemoveField, "the redirected ally's willBeRemoveField was cleared (survives)");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, ally), "the redirected ally is still on the battle area");
}

async Task DecoyNoAlly()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var decoy = await Place(ctx, P1, "DECOY");
    await Place(ctx, P2, "FOE");

    ICardEffect act = CardEffectFactory.DecoySelfEffect(false, V(ctx, decoy), null, null, "Decoy", "Decoy");
    // no ally exists that matches -> after the self dies, SuccessProcess finds no redirect target (no NRE).
    await CardEffectCommons.DecoyProcess(act, Perm(ctx, decoy), p => p.InstanceId == new HeadlessEntityId("nope"));

    AssertTrue(Deleted(ctx, P1, decoy), "the Decoy self was deleted even with no redirect target");
}

// ============================================================ FACTORY LIVENESS (Decode/Partition/MaterialSave)

async Task FactoryLiveness()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var host = await Place(ctx, P1, "HOST");

    var decode = CardEffectFactory.DecodeEffect(Perm(ctx, host), false, new[] { "Red", "Red" }, () => true, _ => true, null, V(ctx, host));
    AssertTrue(decode is not null, "DecodeEffect printed ActivateClass builds");

    var material = CardEffectFactory.MaterialSaveEffect(V(ctx, host), 1);
    AssertTrue(material is not null, "MaterialSaveEffect printed ActivateClass builds (RD-P6C2-4 IsContainDigiXrosCondition ported)");

    var conds = new List<HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition>
    {
        new(3, "Red"),
        new(3, "Blue"),
    };
    var partition = CardEffectFactory.PartitionEffect(Perm(ctx, host), false, () => true, conds, V(ctx, host));
    AssertTrue(partition is not null, "PartitionEffect printed ActivateClass builds (PartitionClass 1:1)");

    await Task.CompletedTask;
}

// ============================================================ HARNESS

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 32);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

Permanent Perm(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id));

CardSource V(EngineContext ctx, HeadlessEntityId id) => new(ctx, id, OwnerOf(ctx, id), OwnerOf(ctx, id));

HeadlessPlayerId OwnerOf(EngineContext ctx, HeadlessEntityId id) =>
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r) && r is not null ? r.OwnerId : default;

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false, ["level"] = 4 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

void SetFlag(EngineContext ctx, HeadlessEntityId id, string key, bool value)
{
    ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? r);
    var meta = new Dictionary<string, object?>(r!.Metadata, StringComparer.Ordinal) { [key] = value };
    ctx.CardInstanceRepository.Upsert(r with { Metadata = meta });
}

// AS-IS success = the target actually LEFT the battle area (DestroyedPermanents membership).
bool Deleted(EngineContext ctx, HeadlessPlayerId owner, HeadlessEntityId id) =>
    !InZone(ctx, owner, ChoiceZone.BattleArea, id);

bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
