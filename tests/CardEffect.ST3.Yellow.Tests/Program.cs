using System.Collections;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// Card group CardEffect/ST3/Yellow — all 12 cards (group-standard project; see card_group_standard.md).
// Destroy-trigger (ST3_01/04), security-count condition (ST3_05/09), DP/SA debuff selects (ST3_08/11/14/15/16),
// recovery (ST3_09), continuous security-zone DP (ST3_12), player-scope buffs + self-bounce (ST3_13),
// the effectClass alias ST3_07 (-> ST1_06). Activated effects resolved imperatively.
//
// (deferred-queue re-aim, ST2.Blue toolbox) These were OLD-model white-box reads: the alias was probed via the
// retired EffectRegistry.GetKeywordEffects keyword table, and the triggers were driven via EffectBinding
// .ResolveAsync + a harness-local once-per-turn ledger (LocalCapUses) — but RegisterOnEnterPlay yields NO binding
// for a new-model ActivateClass (hence "Sequence contains no matching element"). Re-aimed onto the live AS-IS
// surface exactly as ST2_07/11/12: <Blocker> is a live IBlockerEffect in the permanent's None-timing EffectList;
// each trigger is an ActivateClass whose gate (CanUseCondition / CanActivateCondition) is evaluated against the
// live per-event Hashtable (OnDeletionHashtable / OnAttackCheckHashtableOfPermanent /
// WhenDigivolutionCheckHashtableOfPermanent) and whose body is driven live through Activate; the continuous
// security-zone DP is read from the live Permanent.DP scan (SetPhase opens the DoneStartGame gate).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST3_07: effectClass alias resolves to ST1_06 (<Blocker>)", ST3_07_Alias),
    ("ST3_01: [On opp 0-DP delete] this Digimon gets +1000 DP for the turn", ST3_01_SelfBuff),
    ("ST3_04: [On opp 0-DP delete] gain 1 memory", ST3_04_Memory),
    ("ST3_05: [When Attacking] gains 1 memory only with >= 4 security", ST3_05_SecurityGate),
    ("ST3_09: [When Digivolving] recovers 1 with <= 3 security; not above", ST3_09_Recovery),
    ("ST3_12: your Security Digimon get +2000 DP on the opponent's turn", ST3_12_SecurityDp),
    // (R6-Da'-3) ST3_08/11/13/14/15/16 buff/debuff cases RETIRED — white-box coverage of the invented buff
    // bodies (ActivatedTargetBuffEffect / ActivatedPlayerScopeBuffEffect, DELETED; 0 card call-sites, D2=A).
    // Their cards were already re-ported inline to the AS-IS ActivateClass + ChangeDigimonDP/
    // ChangeDigimonSAttackPlayerEffect duration-bucket idiom; the DP/SA +/- grant-and-expiry behavior is covered
    // behaviorally by G3.5-CVA1 (EffectDuration), G3.5-F15 (AttackEndDurationExpiry), G3.5-F5
    // (PlayerScopeContinuous) and W3c3-DpDeltaGrant. The [Security] return-to-hand (ST3_14) / reuse-main (ST3_16)
    // tails rode on the retired [Main] cases; AddThisCardToHandEffect / ReuseMainOptionEffect are exercised by
    // G9-046 (SelectAndPlay) and the ST3 card re-ports respectively.
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

async Task ST3_07_Alias()
{
    EngineContext context = Context(P1);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    cards.Upsert(new CardRecord(new HeadlessEntityId("ST3_07def"), "ST3_07", "Unimon",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["effectClass"] = "ST1_06" }, CardType: "Digimon"));
    var id = new HeadlessEntityId("p1:battle:ST3_07");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId("ST3_07def"), P1));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, id, ChoiceZone.None, ChoiceZone.BattleArea));

    // (re-aim) <Blocker> is a new-model kind-class (BlockerClass : IBlockerEffect) read LIVE from the permanent's
    // EffectList — there is no EffectRegistry keyword table in AS-IS. The alias real-rule (effectClass "ST1_06"
    // grants <Blocker>) is re-aimed onto that live surface, the same as ST2_07.
    AssertTrue(CardEffectRegistrar.RegisterCard(context, id, P1), "ST3_07 registered via its effectClass alias");
    using (AmbientMatchContext.Enter(context))
    {
        bool hasBlocker = new Permanent(context, id, P1).EffectList(EffectTiming.None)
            .Any(e => e is IBlockerEffect && string.Equals(e.EffectName, "Blocker", StringComparison.Ordinal));
        AssertTrue(hasBlocker, "ST3_07 gained <Blocker> from ST1_06 via its effectClass alias (live IBlockerEffect in EffectList)");
    }
}

async Task ST3_01_SelfBuff()
{
    ActivateClass Effect(EngineContext c, HeadlessEntityId self) =>
        (ActivateClass)new ST3_01().CardEffects(EffectTiming.OnDestroyedAnyone, new CardSource(c, self, P1)).Single();

    Hashtable DeletionHt(EngineContext c, HeadlessEntityId deleted, HeadlessPlayerId owner, bool dpZero) =>
        CardEffectCommons.OnDeletionHashtable(
            new List<Permanent> { new Permanent(c, deleted, owner) }, byEffectCause: false, byBattleCause: false, isDPZero: dpZero);

    // (a) opponent 0-DP delete -> this Digimon gets +1000 DP for the turn.
    EngineContext a = Context(P1);
    var sa = new HeadlessEntityId("p1:battle:T01a");
    await PlaceDigimon(a, P1, sa, level: 4, sources: 0, dp: 4000);
    var foeA = new HeadlessEntityId("p2:battle:FOEa");
    await PlaceDigimon(a, P2, foeA, level: 4, sources: 0, dp: 5000);
    ActivateClass ea = Effect(a, sa);

    // (re-aim) [Once Per Turn] is DECLARED on the effect (maxCountPerTurn=1 + hash key). The live cap is enforced
    // by the shared trigger-resolver seat cEntity_EffectController.isOverMaxCountPerTurn, not by direct Activate;
    // the OLD (b)/(d) "2nd delete same turn blocked / next turn resets" sub-asserts drove a HARNESS-LOCAL
    // LocalCapUses dict (a self-referential probe with zero engine coverage) and are retired — the truthful
    // card-level surface is the declaration below (same as ST2_11).
    AssertEqual(1, ea.MaxCountPerTurn, "once-per-turn cap (maxCountPerTurn=1) declared");
    AssertEqual("DP+1000_ST3_01", ea.HashString, "once-per-turn hash key declared");

    using (AmbientMatchContext.Enter(a))
    {
        Hashtable ht = DeletionHt(a, foeA, P2, dpZero: true);
        AssertTrue(ea.CanUseCondition(ht), "opponent 0-DP delete fires");
        await ea.Activate(ht);
    }
    AssertEqual(5000, new Permanent(a, sa).DP, "self +1000 DP for the turn");

    // (b) NOT a 0-DP delete -> no fire, no buff.
    EngineContext b = Context(P1);
    var sb = new HeadlessEntityId("p1:battle:T01b");
    await PlaceDigimon(b, P1, sb, level: 4, sources: 0, dp: 4000);
    var foeB = new HeadlessEntityId("p2:battle:FOEb");
    await PlaceDigimon(b, P2, foeB, level: 4, sources: 0, dp: 5000);
    using (AmbientMatchContext.Enter(b))
    {
        AssertTrue(!Effect(b, sb).CanUseCondition(DeletionHt(b, foeB, P2, dpZero: false)), "non-0-DP delete does NOT fire");
    }
    AssertEqual(4000, new Permanent(b, sb).DP, "no buff");

    // (c) OWN Digimon deleted by 0 DP -> no fire (the deleted permanent is not an opponent's).
    EngineContext c = Context(P1);
    var sc = new HeadlessEntityId("p1:battle:T01c");
    await PlaceDigimon(c, P1, sc, level: 4, sources: 0, dp: 4000);
    var mine = new HeadlessEntityId("p1:battle:MINEc");
    await PlaceDigimon(c, P1, mine, level: 4, sources: 0, dp: 5000);
    using (AmbientMatchContext.Enter(c))
    {
        AssertTrue(!Effect(c, sc).CanUseCondition(DeletionHt(c, mine, P1, dpZero: true)), "own Digimon delete does NOT fire");
    }
}

async Task ST3_04_Memory()
{
    ActivateClass Effect(EngineContext c, HeadlessEntityId self) =>
        (ActivateClass)new ST3_04().CardEffects(EffectTiming.OnDestroyedAnyone, new CardSource(c, self, P1)).Single();

    Hashtable DeletionHt(EngineContext c, HeadlessEntityId deleted, bool dpZero) =>
        CardEffectCommons.OnDeletionHashtable(
            new List<Permanent> { new Permanent(c, deleted, P2) }, byEffectCause: false, byBattleCause: false, isDPZero: dpZero);

    // opponent 0-DP delete -> +1 memory.
    EngineContext a = Context(P1);
    var self = new HeadlessEntityId("p1:battle:T04");
    await PlaceDigimon(a, P1, self, level: 4, sources: 0, dp: 4000);
    var foe = new HeadlessEntityId("p2:battle:FOE04");
    await PlaceDigimon(a, P2, foe, level: 4, sources: 0, dp: 5000);
    ActivateClass ea = Effect(a, self);
    // (re-aim) once-per-turn DECLARED (see the ST3_01 attestation); the harness-local 2nd-delete ledger is retired.
    AssertEqual(1, ea.MaxCountPerTurn, "once-per-turn cap (maxCountPerTurn=1) declared");
    AssertEqual("Memory+1_ST3_04", ea.HashString, "once-per-turn hash key declared");
    a.MemoryController.Set(0);
    using (AmbientMatchContext.Enter(a))
    {
        Hashtable ht = DeletionHt(a, foe, dpZero: true);
        AssertTrue(ea.CanUseCondition(ht), "opponent 0-DP delete fires");
        await ea.Activate(ht);
    }
    AssertEqual(1, a.MemoryController.Current.Current, "gained 1 memory");

    // non-0-DP delete -> no memory.
    EngineContext b = Context(P1);
    var self2 = new HeadlessEntityId("p1:battle:T04b");
    await PlaceDigimon(b, P1, self2, level: 4, sources: 0, dp: 4000);
    var foe2 = new HeadlessEntityId("p2:battle:FOE04b");
    await PlaceDigimon(b, P2, foe2, level: 4, sources: 0, dp: 5000);
    b.MemoryController.Set(0);
    using (AmbientMatchContext.Enter(b))
    {
        AssertTrue(!Effect(b, self2).CanUseCondition(DeletionHt(b, foe2, dpZero: false)), "non-0-DP no fire");
    }
    AssertEqual(0, b.MemoryController.Current.Current, "no memory");
}

async Task ST3_05_SecurityGate()
{
    ActivateClass Effect(EngineContext c, HeadlessEntityId self) =>
        (ActivateClass)new ST3_05().CardEffects(EffectTiming.OnAllyAttack, new CardSource(c, self, P1)).Single();

    // >= 4 security -> +1 memory.
    EngineContext four = Context(P1);
    var s4 = new HeadlessEntityId("p1:battle:T05a");
    await PlaceDigimon(four, P1, s4, level: 4, sources: 0, dp: 4000);
    await FillSecurity(four, P1, 4);
    four.MemoryController.Set(0);
    ActivateClass e4 = Effect(four, s4);
    using (AmbientMatchContext.Enter(four))
    {
        Hashtable ht = CardEffectCommons.OnAttackCheckHashtableOfPermanent(new Permanent(four, s4, P1), e4);
        AssertTrue(e4.CanUseCondition(ht), "[When Attacking] triggers when THIS card attacks");
        AssertTrue(e4.CanActivateCondition(ht), "4 security -> activation gate open");
        await e4.Activate(ht);
    }
    AssertEqual(1, four.MemoryController.Current.Current, "4 security: +1 memory");

    // 3 security -> activation gate closed, no memory.
    EngineContext three = Context(P1);
    var s3 = new HeadlessEntityId("p1:battle:T05b");
    await PlaceDigimon(three, P1, s3, level: 4, sources: 0, dp: 4000);
    await FillSecurity(three, P1, 3);
    three.MemoryController.Set(0);
    ActivateClass e3 = Effect(three, s3);
    using (AmbientMatchContext.Enter(three))
    {
        Hashtable ht = CardEffectCommons.OnAttackCheckHashtableOfPermanent(new Permanent(three, s3, P1), e3);
        AssertTrue(e3.CanUseCondition(ht), "trigger scope open (this card attacks)");
        AssertTrue(!e3.CanActivateCondition(ht), "3 security -> activation gate closed");
    }
    AssertEqual(0, three.MemoryController.Current.Current, "3 security: no memory");

    // Self-scope: when ANOTHER ally attacks (AttackingPermanent != this card), the +1 must NOT fire even at 4 security.
    EngineContext other = Context(P1);
    var sOwn = new HeadlessEntityId("p1:battle:T05c");
    var otherAttacker = new HeadlessEntityId("p1:battle:OTHER05");
    await PlaceDigimon(other, P1, sOwn, level: 4, sources: 0, dp: 4000);
    await PlaceDigimon(other, P1, otherAttacker, level: 4, sources: 0, dp: 4000);
    await FillSecurity(other, P1, 4);
    other.MemoryController.Set(0);
    ActivateClass eo = Effect(other, sOwn);
    using (AmbientMatchContext.Enter(other))
    {
        Hashtable notSelf = CardEffectCommons.OnAttackCheckHashtableOfPermanent(new Permanent(other, otherAttacker, P1), eo);
        AssertTrue(!eo.CanUseCondition(notSelf), "another ally attacking does NOT trigger the +1");
    }
}

async Task ST3_09_Recovery()
{
    ActivateClass Effect(EngineContext c, HeadlessEntityId self) =>
        (ActivateClass)new ST3_09().CardEffects(EffectTiming.WhenDigivolving, new CardSource(c, self, P1)).Single();

    // <= 3 security and a non-empty deck -> Recovery +1 (deck top -> security).
    EngineContext low = Context(P1);
    var d = new HeadlessEntityId("p1:battle:T09");
    await PlaceDigimon(low, P1, d, level: 4, sources: 0, dp: 4000);
    await FillSecurity(low, P1, 2);
    await FillLibrary(low, P1, 3);
    ActivateClass el = Effect(low, d);
    using (AmbientMatchContext.Enter(low))
    {
        Hashtable ht = CardEffectCommons.WhenDigivolutionCheckHashtableOfPermanent(new Permanent(low, d, P1));
        AssertTrue(el.CanUseCondition(ht), "[When Digivolving] triggers on THIS card's digivolve");
        AssertTrue(el.CanActivateCondition(ht), "<= 3 security -> recovery gate open");
        await el.Activate(ht);
    }
    AssertEqual(3, SecurityCount(low, P1), "2 + recovered 1 = 3 security");

    // 4 security -> condition fails, no recovery.
    EngineContext high = Context(P1);
    var d2 = new HeadlessEntityId("p1:battle:T09b");
    await PlaceDigimon(high, P1, d2, level: 4, sources: 0, dp: 4000);
    await FillSecurity(high, P1, 4);
    await FillLibrary(high, P1, 3);
    ActivateClass eh = Effect(high, d2);
    using (AmbientMatchContext.Enter(high))
    {
        Hashtable ht = CardEffectCommons.WhenDigivolutionCheckHashtableOfPermanent(new Permanent(high, d2, P1));
        AssertTrue(eh.CanUseCondition(ht), "trigger scope open (this card digivolves)");
        AssertTrue(!eh.CanActivateCondition(ht), "4 security -> recovery gate closed");
    }
    AssertEqual(4, SecurityCount(high, P1), "4 security: no recovery");

    // Digivolve-only gate: a plain field entry (isEvolution false) must NOT trigger, even at <= 3 security.
    EngineContext plain = Context(P1);
    var d3 = new HeadlessEntityId("p1:battle:T09c");
    await PlaceDigimon(plain, P1, d3, level: 4, sources: 0, dp: 4000);
    await FillSecurity(plain, P1, 2);
    await FillLibrary(plain, P1, 3);
    ActivateClass ep = Effect(plain, d3);
    using (AmbientMatchContext.Enter(plain))
    {
        Hashtable plainEntry = CardEffectCommons.OnPlayCheckHashtableOfPermanent(new Permanent(plain, d3, P1));
        AssertTrue(!ep.CanUseCondition(plainEntry), "non-digivolve field entry does NOT trigger recovery");
    }
    AssertEqual(2, SecurityCount(plain, P1), "non-digivolve field entry: no recovery");
}

async Task ST3_12_SecurityDp()
{
    // (re-aim) The buff is a security-Digimon CARD-DP effect (ChangeCardDPClass : IChangeCardDPEffect), folded by
    // CardSource.CardDP — NOT the battle-area Permanent.DP the old test read (which never sees it). Its
    // CardCondition additionally gates on the AS-IS `attackProcess.SecurityDigimon == this` — the effect only
    // reaches the security Digimon currently BEING battled in a security check. Re-aimed onto that live surface:
    // mark the security Digimon as the active one, then read CardSource.CardDP.
    // It is the opponent's (P2's) turn -> the owner's Security Digimon get +2000 DP.
    EngineContext context = Context(P2);
    var tamer = new HeadlessEntityId("p1:battle:T12");
    await PlaceDigimon(context, P1, tamer, level: 0, sources: 0, dp: 0);
    var secDigi = new HeadlessEntityId("p1:security:SD");
    await PlaceInZone(context, P1, secDigi, ChoiceZone.Security, dp: 3000);
    Register(context, new ST3_12(), "ST3_12", tamer);
    using (AmbientMatchContext.Enter(context))
    {
        HeadlessDCGO.Engine.Assets.Scripts.Script.AttackProcess.For(context).SecurityDigimon = secDigi;
        AssertEqual(5000, new CardSource(context, secDigi, P1).CardDP, "opponent turn: security Digimon +2000");
    }

    // On the owner's turn the condition (IsOpponentTurn) is false -> no buff.
    EngineContext own = Context(P1);
    var tamer2 = new HeadlessEntityId("p1:battle:T12b");
    await PlaceDigimon(own, P1, tamer2, level: 0, sources: 0, dp: 0);
    var secDigi2 = new HeadlessEntityId("p1:security:SD2");
    await PlaceInZone(own, P1, secDigi2, ChoiceZone.Security, dp: 3000);
    Register(own, new ST3_12(), "ST3_12", tamer2);
    using (AmbientMatchContext.Enter(own))
    {
        HeadlessDCGO.Engine.Assets.Scripts.Script.AttackProcess.For(own).SecurityDigimon = secDigi2;
        AssertEqual(3000, new CardSource(own, secDigi2, P1).CardDP, "owner turn: no buff");
    }

    // (re-aim) [Security] "Play this Tamer" routes to the shared AS-IS PlaySelfTamerSecurityEffect factory (an
    // ActivateClass, IsSecurityEffect + "Play this card without paying its memory cost") — the retired invented
    // PlayThisCardToBattleEffect cast died. Same factory-identity surface as ST2_12 / ST1_12; the play behaviour
    // is covered wherever the factory is exercised (BT2_084/085/087/090, EX4_062 witnesses).
    EngineContext sec = Context(P1);
    CardDatabase scards = (CardDatabase)sec.CardRepository;
    scards.Upsert(new CardRecord(new HeadlessEntityId("ST3_12def"), "ST3_12", "Tamer", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Tamer"));
    var revealed = new HeadlessEntityId("p1:trash:ST3_12T");
    sec.CardInstanceRepository.Upsert(new CardInstanceRecord(revealed, new HeadlessEntityId("ST3_12def"), P1));
    var security = (ActivateClass)new ST3_12().CardEffects(EffectTiming.SecuritySkill, new CardSource(sec, revealed, P1)).Single();
    AssertTrue(security.IsSecurityEffect, "[Security] arm is a security effect");
    AssertTrue(security.EffectDiscription.Contains("Play this card", StringComparison.Ordinal),
        "[Security] routes to the AS-IS PlaySelfTamerSecurityEffect (\"Play this card without paying its memory cost\")");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context(HeadlessPlayerId turnPlayer)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 3);
    context.TurnController.Initialize(new[] { P1, P2 }, turnPlayer);
    // (re-aim) game past setup (phase != None) so the live continuous / activation scans' CanTrigger
    // DoneStartGame gate is open.
    context.TurnController.SetPhase(HeadlessPhase.Main);
    return context;
}

IReadOnlyList<HeadlessEntityId> Register(EngineContext context, CEntity_Effect effect, string number, HeadlessEntityId source) =>
    CardEffectRegistrar.RegisterOnEnterPlay(context, effect, number, new CardSource(context, source, P1));

async Task PlaceDigimon(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, int level, int sources, int dp)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    cards.Upsert(new CardRecord(defId, defId.Value, id.Value, new Dictionary<string, object?>(), CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
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

async Task PlaceInZone(EngineContext context, HeadlessPlayerId owner, HeadlessEntityId id, ChoiceZone zone, int dp)
{
    CardDatabase cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{id.Value}");
    // The printed dp goes on the DEFINITION metadata too — CardSource.HasDP (the CardDP fold's gate for a
    // security-zone Digimon) reads the definition, not the instance.
    cards.Upsert(new CardRecord(defId, defId.Value, id.Value, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp }, CardType: "Digimon"));
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp };
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
}

async Task FillSecurity(EngineContext context, HeadlessPlayerId owner, int count)
{
    for (int i = 0; i < count; i++)
    {
        await PlaceInZone(context, owner, new HeadlessEntityId($"{owner.Value}:sec:{i}"), ChoiceZone.Security, dp: 0);
    }
}

async Task FillLibrary(EngineContext context, HeadlessPlayerId owner, int count)
{
    for (int i = 0; i < count; i++)
    {
        await PlaceInZone(context, owner, new HeadlessEntityId($"{owner.Value}:lib:{i}"), ChoiceZone.Library, dp: 0);
    }
}

int SecurityCount(EngineContext context, HeadlessPlayerId player) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Security).Count;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
