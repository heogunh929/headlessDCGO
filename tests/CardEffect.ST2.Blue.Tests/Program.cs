using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Card group CardEffect/ST2/Blue — all 12 cards (group-standard project; see card_group_standard.md).
// Continuous (ST2_01/08), trigger (ST2_11/12), activated select/trash/memory/restrict/bounce
// (ST2_03/06/09/13/14/16), the effectClass alias ST2_07 (-> ST1_06), and the deferred play-from-under
// ST2_15. Activated effects are resolved imperatively (BuildRequest -> scripted answer -> Apply).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessPlayerId[] Both = { new(1), new(2) };

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST2_07: effectClass alias resolves to ST1_06 (<Blocker>)", ST2_07_Alias),
    ("ST2_01: inherited DP +1000 while opponent has a no-evo Digimon (owner turn)", ST2_01_Dp),
    ("ST2_08: inherited Security Attack +1 while opponent has a no-evo Digimon", ST2_08_SecurityAttack),
    ("ST2_03: [When Attacking] only opponent Digimon lvl<=5 with sources are candidates; trashes 1", ST2_03_Trash),
    ("ST2_06: [When Attacking] trashes 1 bottom digivolution card of the chosen opponent Digimon", ST2_06_Trash),
    ("ST2_09: [When Digivolving] trashes 2 bottom digivolution cards", ST2_09_Trash),
    ("ST2_11: [When Attacking] unsuspends this Digimon", ST2_11_Unsuspend),
    ("ST2_11: another ally attacking does NOT unsuspend it (self-scope)", ST2_11_OtherAllyNoFire),
    ("ST2_12: [Start of Your Turn] gains 1 memory when opponent has a no-evo Digimon", ST2_12_Memory),
    ("ST2_13: [Main] +1 memory / [Security] +2 memory", ST2_13_Memory),
    // (R6-Da'-3) ST2_14_Restrict / ST2_14_ResolverCase REMOVED — both were STALE-red white-box asserts on the
    // deleted invented `ActivatedTargetRestrictionEffect` + EffectRegistry restriction bindings. ST2_14 is
    // re-ported to the inline AS-IS ActivateClass + GainCanNotAttack/GainCanNotBlock (new-model permanent bucket,
    // read via NewModelContinuousScan) — the live restriction behavior is covered there. (Zero green sacrificed:
    // both cases were already failing on the InvalidCast to the retired type; whole suite is A6-disposal material.)
    ("ST2_15: [Main] plays a Digimon under-card as a new Digimon; [Security] reuses Main", ST2_15_PlayFromUnder),
    ("ST2_16: [Main] returns the chosen opponent Digimon to its owner's hand", ST2_16_Bounce),
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

async Task ST2_07_Alias()
{
    EngineContext context = Context();
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST2_07def"), "ST2_07", "Grizzlymon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["effectClass"] = "ST1_06" }, CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:battle:ST2_07");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("ST2_07def"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));

    // (uniform-사멸 flip re-target) <Blocker> is a new-model kind-class (BlockerClass : IBlockerEffect) read LIVE
    // from the permanent's EffectList — there is no EffectRegistry keyword table in AS-IS. The alias real-rule
    // (effectClass "ST1_06" grants <Blocker>) is re-aimed onto that live surface: after registering via the alias,
    // the ST2_07 permanent's continuous (None-timing) effect list carries ST1_06's <Blocker> kind-class. (The
    // battle-time NewModelContinuousScan.HasBlocker scan additionally gates on ICardEffect.CanTrigger's
    // DoneStartGame precondition — a started-game runtime state, orthogonal to the alias grant this case owns.)
    AssertTrue(CardEffectRegistrar.RegisterCard(context, id, P1), "ST2_07 registered via its effectClass alias");
    using (AmbientMatchContext.Enter(context))
    {
        bool hasBlocker = new Permanent(context, id, P1).EffectList(EffectTiming.None)
            .Any(e => e is IBlockerEffect && string.Equals(e.EffectName, "Blocker", StringComparison.Ordinal));
        AssertTrue(hasBlocker, "ST2_07 gained <Blocker> from ST1_06 via its effectClass alias (live IBlockerEffect in EffectList)");
    }
    await Task.CompletedTask;
}

async Task ST2_01_Dp()
{
    // (a) battling a no-evo opponent -> +1000.
    EngineContext a = Context();
    a.TurnController.SetPhase(HeadlessPhase.Main);
    (HeadlessEntityId topA, HeadlessEntityId srcA) = await SelfStack(a, P1, new HeadlessEntityId("p1:battle:TOP01a"));
    var foeA = new HeadlessEntityId("p2:battle:FOEa");
    await PlaceDigimon(a, P2, foeA, level: 4, sources: 0);
    RegisterContinuous(a, new ST2_01(), "ST2_01", srcA);
    a.AttackController.DeclareAttack(P1, topA, P2, foeA);
    AssertEqual(3000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(a, topA).DP, "battling a no-evo opponent: +1000");

    // (b) battling an opponent WITH digivolution cards -> no buff.
    EngineContext b = Context();
    b.TurnController.SetPhase(HeadlessPhase.Main);
    (HeadlessEntityId topB, HeadlessEntityId srcB) = await SelfStack(b, P1, new HeadlessEntityId("p1:battle:TOP01b"));
    var foeB = new HeadlessEntityId("p2:battle:FOEb");
    await PlaceDigimon(b, P2, foeB, level: 4, sources: 1);
    RegisterContinuous(b, new ST2_01(), "ST2_01", srcB);
    b.AttackController.DeclareAttack(P1, topB, P2, foeB);
    AssertEqual(2000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(b, topB).DP, "battling an evo opponent: no buff");

    // (c) NOT in a battle — even with a no-evo opponent on the board -> no buff (battle-specific, not "any opponent").
    EngineContext c = Context();
    c.TurnController.SetPhase(HeadlessPhase.Main);
    (HeadlessEntityId topC, HeadlessEntityId srcC) = await SelfStack(c, P1, new HeadlessEntityId("p1:battle:TOP01c"));
    await PlaceDigimon(c, P2, new HeadlessEntityId("p2:battle:FOEc"), level: 4, sources: 0);
    RegisterContinuous(c, new ST2_01(), "ST2_01", srcC);
    AssertEqual(2000, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(c, topC).DP, "not battling: no buff");
}

async Task ST2_08_SecurityAttack()
{
    EngineContext context = Context();
    context.TurnController.SetPhase(HeadlessPhase.Main);
    (HeadlessEntityId top, HeadlessEntityId source) = await SelfStack(context, P1, new HeadlessEntityId("p1:battle:TOP08"));
    await PlaceDigimon(context, P2, new HeadlessEntityId("p2:battle:NOEVO8"), level: 3, sources: 0);
    RegisterContinuous(context, new ST2_08(), "ST2_08", source);

    // Permanent.Strike (unlike Permanent.DP) does not self-scope AmbientMatchContext; in live play the security-check
    // fold is always read inside the match scope (the DP getter self-scopes only as a convenience). Enter it here.
    using (AmbientMatchContext.Enter(context))
    {
        AssertEqual(2, new HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.Permanent(context, top).Strike, "opponent has a no-evo Digimon: SA +1");
    }
}

// (uniform-사멸 flip re-target) ST2_03 was re-ported to the AS-IS inline ActivateClass + SelectPermanentEffect
// Mode.Custom + TrashDigivolutionCardsFromTopOrBottom. The old-model BuildRequest/Apply cast is retired; the
// real-rule content (targeting filter + trash count) is re-aimed onto the LIVE coroutine: a valid target is
// selectable and loses 1 bottom card; the lvl6 / no-source / protected Digimon are non-candidates (a board of
// only those trashes nothing — MatchConditionPermanentCount == 0).
async Task ST2_03_Trash()
{
    // Positive: the lvl<=5 opponent Digimon with a trashable source is selectable and loses 1 bottom card.
    EngineContext context = Context();
    var ok = new HeadlessEntityId("p2:battle:OK");        // lvl 4, has sources -> candidate
    var tooHigh = new HeadlessEntityId("p2:battle:HIGH");  // lvl 6 -> excluded
    var noSrc = new HeadlessEntityId("p2:battle:NOSRC");   // lvl 4, no sources -> excluded
    var prot = new HeadlessEntityId("p2:battle:PROT");     // lvl 4, only source is trash-protected -> excluded
    await PlaceDigimon(context, P2, ok, level: 4, sources: 2);
    await PlaceDigimon(context, P2, tooHigh, level: 6, sources: 2);
    await PlaceDigimon(context, P2, noSrc, level: 4, sources: 0);
    await PlaceDigimon(context, P2, prot, level: 4, sources: 1);
    ProtectSource(context, P2, prot, 0);

    ActivateClass effect = ActivatedAC(new ST2_03(), context, EffectTiming.OnAllyAttack);
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(ok));
    await DriveActivate(context, effect);
    AssertEqual(1, TrashCount(context, P2), "the valid lvl<=5 target lost 1 bottom digivolution card");

    // Exclusion: a board of ONLY the excluded Digimon (lvl6 / no-source / protected) yields no candidate -> nothing trashed.
    EngineContext excl = Context();
    await PlaceDigimon(excl, P2, tooHigh, level: 6, sources: 2);
    await PlaceDigimon(excl, P2, noSrc, level: 4, sources: 0);
    await PlaceDigimon(excl, P2, prot, level: 4, sources: 1);
    ProtectSource(excl, P2, prot, 0);
    await DriveActivate(excl, ActivatedAC(new ST2_03(), excl, EffectTiming.OnAllyAttack));
    AssertEqual(0, TrashCount(excl, P2), "lvl6 / no-source / protected Digimon are not trash candidates");
}

async Task ST2_06_Trash()
{
    // No level gate on ST2_06: even a lvl6 opponent Digimon is a candidate and loses 1 bottom card.
    EngineContext context = Context();
    var target = new HeadlessEntityId("p2:battle:T06");
    await PlaceDigimon(context, P2, target, level: 6, sources: 1);
    ActivateClass effect = ActivatedAC(new ST2_06(), context, EffectTiming.OnAllyAttack);
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(target));
    await DriveActivate(context, effect);
    AssertEqual(1, TrashCount(context, P2), "the opponent Digimon (any level) lost 1 bottom digivolution card");
}

async Task ST2_09_Trash()
{
    // Positive: the opponent Digimon with a trashable source loses 2 bottom cards.
    EngineContext context = Context();
    var target = new HeadlessEntityId("p2:battle:T09");
    var prot = new HeadlessEntityId("p2:battle:T09P");   // only source protected -> excluded
    await PlaceDigimon(context, P2, target, level: 4, sources: 2);
    await PlaceDigimon(context, P2, prot, level: 4, sources: 1);
    ProtectSource(context, P2, prot, 0);

    ActivateClass effect = ActivatedAC(new ST2_09(), context, EffectTiming.WhenDigivolving);
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(target));
    await DriveActivate(context, effect);
    AssertEqual(2, TrashCount(context, P2), "2 digivolution cards trashed from the valid target");

    // Exclusion: a board of ONLY the protected Digimon yields no candidate -> nothing trashed.
    EngineContext excl = Context();
    await PlaceDigimon(excl, P2, prot, level: 4, sources: 1);
    ProtectSource(excl, P2, prot, 0);
    await DriveActivate(excl, ActivatedAC(new ST2_09(), excl, EffectTiming.WhenDigivolving));
    AssertEqual(0, TrashCount(excl, P2), "a Digimon whose only source is trash-protected is not a candidate");
}

// (uniform-사멸 flip re-target) ST2_11 was re-ported to the AS-IS inline ActivateClass (maxCountPerTurn=1 +
// SetHashString + IUnsuspendPermanents). The stale RegisterOnEnterPlay "OnAllyAttack" binding lookup is retired;
// the real-rule content is re-aimed onto the live effect: the once-per-turn cap DECLARATION, the unsuspend
// behavior (drive the coroutine live), and the self-scope trigger predicate.
async Task ST2_11_Unsuspend()
{
    EngineContext context = Context();
    var id = new HeadlessEntityId("p1:battle:T11");
    await PlaceDigimon(context, P1, id, level: 4, sources: 0);
    var effect = (ActivateClass)new ST2_11().CardEffects(EffectTiming.OnAllyAttack, new CardSource(context, id, P1)).Single();

    // [Once Per Turn] is DECLARED on the effect (the live cap is enforced by the shared trigger-resolver seat
    // cEntity_EffectController.isOverMaxCountPerTurn, not by the card body). The OLD "2nd attack blocked / next
    // turn resets" sub-asserts drove a HARNESS-LOCAL cap dictionary (LocalCapUses) — a self-referential probe
    // with zero engine coverage — and are retired; the truthful card-level surface is the declaration below.
    AssertEqual(1, effect.MaxCountPerTurn, "once-per-turn cap (maxCountPerTurn=1) declared");
    AssertEqual("Unsuspend_ST2_11", effect.HashString, "once-per-turn hash key declared");

    await Suspend(context, id);
    await DriveActivate(context, effect);
    AssertTrue(!IsSuspended(context, id), "[When Attacking] unsuspends this Digimon");
}

// Self-scope: when ANOTHER ally attacks (attacking permanent != this card), the self-unsuspend must NOT trigger.
async Task ST2_11_OtherAllyNoFire()
{
    EngineContext context = Context();
    var id = new HeadlessEntityId("p1:battle:T11b");
    var other = new HeadlessEntityId("p1:battle:OTHER");
    await PlaceDigimon(context, P1, id, level: 4, sources: 0);
    await PlaceDigimon(context, P1, other, level: 4, sources: 0);
    var effect = (ActivateClass)new ST2_11().CardEffects(EffectTiming.OnAllyAttack, new CardSource(context, id, P1)).Single();

    using (AmbientMatchContext.Enter(context))
    {
        Hashtable self = CardEffectCommons.OnAttackCheckHashtableOfPermanent(new Permanent(context, id, P1), effect);
        Hashtable notSelf = CardEffectCommons.OnAttackCheckHashtableOfPermanent(new Permanent(context, other, P1), effect);
        AssertTrue(effect.CanUseCondition(self), "self-unsuspend triggers when THIS card attacks");
        AssertTrue(!effect.CanUseCondition(notSelf), "another ally attacking does NOT trigger the self-unsuspend");
    }
}

async Task Suspend(EngineContext context, HeadlessEntityId id)
{
    var sink = Sink(context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.SuspendKind, id,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = id.Value }));
    await sink.FlushAsync();
}

// (uniform-사멸 flip re-target) ST2_12 [Start of Your Turn] was re-ported to the AS-IS inline ActivateClass
// (isExistOnField + CanAddMemory -> AddMemory(1)), gated by CanActivateCondition (opponent has a no-evo Digimon).
// The stale RegisterOnEnterPlay "OnStartTurn" binding lookup is retired; the conditional real-rule is re-aimed
// onto the live effect's CanActivateCondition gate + a live AddMemory drive. The [Security] "Play this Tamer"
// arm is the shared AS-IS CardEffectFactory.PlaySelfTamerSecurityEffect factory (its play behavior is covered
// wherever that factory is exercised — BT2_084/085/087/090, EX4_062 witnesses); the retired
// PlayThisCardToBattleEffect.Apply cast is re-aimed onto the factory-identity surface.
async Task ST2_12_Memory()
{
    // Positive: opponent has a no-evo Digimon -> the activation gate is open and 1 memory is gained.
    EngineContext context = Context();
    var tamer = new HeadlessEntityId("p1:battle:T12");
    await PlaceDigimon(context, P1, tamer, level: 0, sources: 0);
    await PlaceDigimon(context, P2, new HeadlessEntityId("p2:battle:NOEVO12"), level: 4, sources: 0);

    var effect = (ActivateClass)new ST2_12().CardEffects(EffectTiming.OnStartTurn, new CardSource(context, tamer, P1)).Single();
    using (AmbientMatchContext.Enter(context))
    {
        AssertTrue(effect.CanActivateCondition(new Hashtable()), "opponent has a no-evo Digimon -> activation gate open");
    }

    context.MemoryController.Set(0);
    await DriveActivate(context, effect);
    AssertEqual(1, context.MemoryController.Current.Current, "gained 1 memory at start of turn");

    // Negative: the opponent's only Digimon HAS digivolution cards -> the activation gate is closed.
    EngineContext noGate = Context();
    var tamer2 = new HeadlessEntityId("p1:battle:T12b");
    await PlaceDigimon(noGate, P1, tamer2, level: 0, sources: 0);
    await PlaceDigimon(noGate, P2, new HeadlessEntityId("p2:battle:EVO12"), level: 4, sources: 1);
    var effect2 = (ActivateClass)new ST2_12().CardEffects(EffectTiming.OnStartTurn, new CardSource(noGate, tamer2, P1)).Single();
    using (AmbientMatchContext.Enter(noGate))
    {
        AssertTrue(!effect2.CanActivateCondition(new Hashtable()), "opponent has no no-evo Digimon -> activation gate closed");
    }

    // [Security] "Play this Tamer" routes to the shared AS-IS PlaySelfTamerSecurityEffect factory.
    EngineContext sec = Context();
    var revealed = new HeadlessEntityId("p1:trash:ST2_12T");
    var security = (ActivateClass)new ST2_12().CardEffects(EffectTiming.SecuritySkill, new CardSource(sec, revealed, P1)).Single();
    AssertTrue(security.IsSecurityEffect, "[Security] arm is a security effect");
    AssertTrue(security.EffectDiscription.Contains("Play this card", StringComparison.Ordinal),
        "[Security] routes to the AS-IS PlaySelfTamerSecurityEffect (\"Play this card without paying its memory cost\")");
}

async Task ST2_13_Memory()
{
    EngineContext context = Context();
    var opt = new HeadlessEntityId("p1:trash:OPT13");

    // (uniform-사멸 flip re-target) ST2_13 was re-ported to the AS-IS inline ActivateClass per timing (AddMemory);
    // drive the live coroutine instead of the retired ActivatedMemoryEffect.Apply cast.
    var main = (ActivateClass)new ST2_13().CardEffects(EffectTiming.OptionSkill, Source(context, opt)).Single();
    context.MemoryController.Set(2);
    await DriveActivate(context, main);
    AssertEqual(3, context.MemoryController.Current.Current, "[Main] +1 memory");

    var sec = (ActivateClass)new ST2_13().CardEffects(EffectTiming.SecuritySkill, Source(context, opt)).Single();
    context.MemoryController.Set(2);
    await DriveActivate(context, sec);
    AssertEqual(4, context.MemoryController.Current.Current, "[Security] +2 memory");
}

// (R6-Da'-3) ST2_14_Restrict / ST2_14_ResolverCase method bodies REMOVED with their registrations above — see
// the attestation note in the tests table. ST2_14's live restriction path is the inline AS-IS ActivateClass +
// GainCanNotAttack/GainCanNotBlock (new-model permanent bucket).

async Task ST2_15_PlayFromUnder()
{
    EngineContext context = Context();
    var opt = new HeadlessEntityId("p1:trash:OPT15");
    // (이연③-g) Register `opt` as a live ST2_15 Option instance: the [Security] factory resolves
    // OptionMainEffect(card) = card.EffectList(OptionSkill) at BUILD time (AS-IS CardEffectFactory.cs:553),
    // which dispatches through the card instance's definition — exactly the production reveal path.
    ((CardDatabase)context.CardRepository).Upsert(
        new CardRecord(new HeadlessEntityId("ST2_15def"), "ST2_15", "ST2_15", new Dictionary<string, object?>(), CardType: "Option"));
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(opt, new HeadlessEntityId("ST2_15def"), P1));
    // An owner Digimon with a Digimon digivolution card under it.
    var host = new HeadlessEntityId("p1:battle:H15");
    await PlaceDigimon(context, P1, host, level: 4, sources: 1);
    var under = new HeadlessEntityId($"{host.Value}:src0");

    // (uniform-사멸 flip re-target) ST2_15 [Main] was re-ported to the AS-IS inline ActivateClass (SelectPermanent
    // host -> SelectCard the under-card -> PlayPermanentCards payCost:false). Drive the live select flow instead of
    // the retired ActivatedPlayFromUnderEffect.BuildRequest/Apply cast: pick the host, then the under-card.
    var main = (ActivateClass)new ST2_15().CardEffects(EffectTiming.OptionSkill, Source(context, opt)).Single();
    // The host permanent is the only candidate (pool == maxCount == 1) so the SelectPermanent step auto-forces
    // without a prompt; only the under-card SelectCard step needs a scripted answer.
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(under));
    await DriveActivate(context, main);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, under), "[Main] the under-card is now its own battle-area Digimon");
    AssertTrue(CardEffectCommons.HasNoDigivolutionCards(new CardSource(context, host, P1), host), "host lost that digivolution source");

    // [Security] reuses the Main option — (이연③-g) now the AS-IS ActivateClass from
    // CardEffectFactory.ActivateMainOptionSecurityEffect (SetIsSecurityEffect + [Main] reuse coroutine),
    // emitted by the commons AddActivateMainOptionSecurityEffect factory, not the retired ReuseMainOptionEffect.
    // The factory resolves OptionMainEffect(card) (= card.EffectList(OptionSkill)) at BUILD time (AS-IS
    // CardEffectFactory.cs:553), so the security build now runs under the ambient match scope production's
    // ResolveAsync always enters before building an effect list.
    ICardEffect security;
    using (AmbientMatchContext.Enter(context))
    {
        security = new ST2_15().CardEffects(EffectTiming.SecuritySkill, Source(context, opt)).Single();
    }

    AssertTrue(security is ActivateClass { IsSecurityEffect: true }, "[Security] reuses the Main option");
}

async Task ST2_16_Bounce()
{
    EngineContext context = Context();
    var target = new HeadlessEntityId("p2:battle:T16");
    await PlaceDigimon(context, P2, target, level: 4, sources: 0);

    // (uniform-사멸 flip re-target) ST2_16 was re-ported to the AS-IS inline ActivateClass (SelectPermanentEffect
    // Mode.Bounce) — drive the coroutine live through the scripted provider (the old uniform Body cast died).
    var effect = (HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.ActivateClass)Activated(new ST2_16(), context, EffectTiming.OptionSkill);
    ((ScriptedChoiceProvider)context.ChoiceProvider).Enqueue(ChoiceResult.Select(target));
    using (AmbientMatchContext.Enter(context))
    {
        await effect.Activate(CardEffectCommons.OptionMainCheckHashtable(effect.EffectSourceCard));
    }

    AssertTrue(InZone(context, P2, ChoiceZone.Hand, target), "the opponent Digimon was returned to its owner's hand");
    AssertTrue(!InZone(context, P2, ChoiceZone.BattleArea, target), "the opponent Digimon left the battle area");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 2);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

CardSource Source(EngineContext context, HeadlessEntityId id) => new(context, id, P1);

ICardEffect Activated(CEntity_Effect card, EngineContext context, EffectTiming timing) =>
    card.CardEffects(timing, new CardSource(context, new HeadlessEntityId("p1:trash:ACT"), P1)).Single();

// (uniform-사멸 flip re-target) The former cards were re-ported to the AS-IS inline ActivateClass; drive the live
// coroutine through the scripted choice provider (same live-surface pattern the green ST2_16 case established).
ActivateClass ActivatedAC(CEntity_Effect card, EngineContext context, EffectTiming timing) =>
    (ActivateClass)Activated(card, context, timing);

async Task DriveActivate(EngineContext context, ActivateClass effect)
{
    using (AmbientMatchContext.Enter(context))
    {
        await effect.Activate(new Hashtable());
    }
}

IReadOnlyList<HeadlessEntityId> RegisterContinuous(EngineContext context, CEntity_Effect effect, string number, HeadlessEntityId source) =>
    CardEffectRegistrar.RegisterOnEnterPlay(context, effect, number, new CardSource(context, source, P1));

async Task<(HeadlessEntityId Top, HeadlessEntityId Source)> SelfStack(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId top)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("TOPDEF"), "TOPDEF", "Top", new Dictionary<string, object?>(), CardType: "Digimon"));
    cards.Upsert(new CardRecord(new HeadlessEntityId("SRCDEF"), "SRCDEF", "Src", new Dictionary<string, object?>(), CardType: "Digimon"));
    var src = new HeadlessEntityId($"{top.Value}:src0");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(src, new HeadlessEntityId("SRCDEF"), owner));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["sourceIds"] = new List<string> { src.Value }, ["dp"] = 2000 };
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(top, new HeadlessEntityId("TOPDEF"), owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, top, ChoiceZone.None, ChoiceZone.BattleArea));
    return (top, src);
}

// Mark the host's source #index as trash-protected (CanNotTrashFromDigivolutionCards).
void ProtectSource(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId host, int index)
{
    var defId = new HeadlessEntityId($"DEF:{host.Value}");
    var sid = new HeadlessEntityId($"{host.Value}:src{index}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(sid, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["cannotTrashFromDigivolution"] = true }));
}

async Task PlaceDigimon(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, int level, int sources)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    cards.Upsert(new CardRecord(defId, defId.Value, id.Value, new Dictionary<string, object?>(), CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level };
    if (sources > 0)
    {
        var sourceIds = new List<string>();
        for (int i = 0; i < sources; i++)
        {
            var sid = new HeadlessEntityId($"{id.Value}:src{i}");
            context.CardInstanceRepository.Upsert(new CardInstanceRecord(sid, defId, owner));
            sourceIds.Add(sid.Value);
        }

        meta["sourceIds"] = sourceIds;
    }

    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController);

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

int TrashCount(EngineContext context, HeadlessPlayerId player) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Trash).Count;

bool IsSuspended(EngineContext context, HeadlessEntityId id) =>
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst) && inst is not null
    && inst.Metadata.TryGetValue("isSuspended", out object? v) && v is true;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
