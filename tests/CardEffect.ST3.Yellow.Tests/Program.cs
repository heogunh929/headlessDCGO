using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
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

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessPlayerId[] Both = { new(1), new(2) };

var tests = new (string Name, Func<Task> Body)[]
{
    ("ST3_07: effectClass alias resolves to ST1_06 (<Blocker>)", ST3_07_Alias),
    ("ST3_01: [On opp 0-DP delete] this Digimon gets +1000 DP for the turn", ST3_01_SelfBuff),
    ("ST3_04: [On opp 0-DP delete] gain 1 memory", ST3_04_Memory),
    ("ST3_05: [When Attacking] gains 1 memory only with >= 4 security", ST3_05_SecurityGate),
    ("ST3_08: [When Attacking] chosen opponent Digimon gets -1000 DP", ST3_08_Debuff),
    ("ST3_09: [When Digivolving] recovers 1 with <= 3 security; not above", ST3_09_Recovery),
    ("ST3_11: [When Attacking] chosen opponent Digimon gets -4000 DP", ST3_11_Debuff),
    ("ST3_12: your Security Digimon get +2000 DP on the opponent's turn", ST3_12_SecurityDp),
    ("ST3_13: [Main] +3000 to your Digimon / [Security] +5000 all and back to hand", ST3_13_Buff),
    ("ST3_14: [Main] -2000 to opponent / [Security] returns to hand", ST3_14_Debuff),
    ("ST3_15: [Main] Security Attack -3 to opponent / [Security] -1 to all opponents", ST3_15_SecurityAttack),
    ("ST3_16: [Main] -10000 to opponent / [Security] reuses Main", ST3_16_Debuff),
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

    AssertTrue(CardEffectRegistrar.RegisterCard(context, id, P1), "ST3_07 registered via its effectClass alias");
    AssertTrue(context.EffectRegistry.GetKeywordEffects("Blocker").Count >= 1, "ST3_07 gained <Blocker> from ST1_06");
    await Task.CompletedTask;
}

async Task ST3_01_SelfBuff()
{
    EngineContext context = Context(P1);
    var self = new HeadlessEntityId("p1:battle:T01");
    await PlaceDigimon(context, P1, self, level: 4, sources: 0, dp: 4000);
    IReadOnlyList<EffectBinding> bindings = Register(context, new ST3_01(), "ST3_01", self);
    await ResolveTrigger(context, bindings, "OnDestroyedAnyone");
    AssertEqual(5000, ContinuousDpGate.ResolveDp(context, self, baseDp: 4000), "self +1000 DP for the turn");
}

async Task ST3_04_Memory()
{
    EngineContext context = Context(P1);
    var self = new HeadlessEntityId("p1:battle:T04");
    await PlaceDigimon(context, P1, self, level: 4, sources: 0, dp: 4000);
    IReadOnlyList<EffectBinding> bindings = Register(context, new ST3_04(), "ST3_04", self);
    context.MemoryController.Set(0);
    await ResolveTrigger(context, bindings, "OnDestroyedAnyone");
    AssertEqual(1, context.MemoryController.Current.Current, "gained 1 memory");
}

async Task ST3_05_SecurityGate()
{
    // >= 4 security -> +1 memory.
    EngineContext four = Context(P1);
    var s4 = new HeadlessEntityId("p1:battle:T05a");
    await PlaceDigimon(four, P1, s4, level: 4, sources: 0, dp: 4000);
    await FillSecurity(four, P1, 4);
    IReadOnlyList<EffectBinding> bindings = Register(four, new ST3_05(), "ST3_05", s4);
    four.MemoryController.Set(0);
    await ResolveTrigger(four, bindings, "OnAllyAttack");
    AssertEqual(1, four.MemoryController.Current.Current, "4 security: +1 memory");

    // 3 security -> no memory (CanResolve fails).
    EngineContext three = Context(P1);
    var s3 = new HeadlessEntityId("p1:battle:T05b");
    await PlaceDigimon(three, P1, s3, level: 4, sources: 0, dp: 4000);
    await FillSecurity(three, P1, 3);
    IReadOnlyList<EffectBinding> bindings3 = Register(three, new ST3_05(), "ST3_05", s3);
    three.MemoryController.Set(0);
    await ResolveTrigger(three, bindings3, "OnAllyAttack");
    AssertEqual(0, three.MemoryController.Current.Current, "3 security: no memory");
}

async Task ST3_08_Debuff() => await DebuffCase(new ST3_08(), EffectTiming.OnAllyAttack, -1000);

async Task ST3_11_Debuff() => await DebuffCase(new ST3_11(), EffectTiming.OnAllyAttack, -4000);

async Task ST3_09_Recovery()
{
    // <= 3 security and a non-empty deck -> Recovery +1 (deck top -> security).
    EngineContext low = Context(P1);
    var d = new HeadlessEntityId("p1:battle:T09");
    await PlaceDigimon(low, P1, d, level: 4, sources: 0, dp: 4000);
    await FillSecurity(low, P1, 2);
    await FillLibrary(low, P1, 3);
    IReadOnlyList<EffectBinding> bindings = Register(low, new ST3_09(), "ST3_09", d);
    await ResolveTrigger(low, bindings, "OnEnterFieldAnyone");
    AssertEqual(3, SecurityCount(low, P1), "2 + recovered 1 = 3 security");

    // 4 security -> condition fails, no recovery.
    EngineContext high = Context(P1);
    var d2 = new HeadlessEntityId("p1:battle:T09b");
    await PlaceDigimon(high, P1, d2, level: 4, sources: 0, dp: 4000);
    await FillSecurity(high, P1, 4);
    await FillLibrary(high, P1, 3);
    IReadOnlyList<EffectBinding> bindings2 = Register(high, new ST3_09(), "ST3_09", d2);
    await ResolveTrigger(high, bindings2, "OnEnterFieldAnyone");
    AssertEqual(4, SecurityCount(high, P1), "4 security: no recovery");
}

async Task ST3_12_SecurityDp()
{
    // It is the opponent's (P2's) turn -> the owner's Security Digimon get +2000 DP.
    EngineContext context = Context(P2);
    var tamer = new HeadlessEntityId("p1:battle:T12");
    await PlaceDigimon(context, P1, tamer, level: 0, sources: 0, dp: 0);
    var secDigi = new HeadlessEntityId("p1:security:SD");
    await PlaceInZone(context, P1, secDigi, ChoiceZone.Security, dp: 3000);
    Register(context, new ST3_12(), "ST3_12", tamer);
    AssertEqual(5000, ContinuousDpGate.ResolveDp(context, secDigi, baseDp: 3000), "opponent turn: security Digimon +2000");

    // On the owner's turn the condition is false -> no buff.
    EngineContext own = Context(P1);
    var tamer2 = new HeadlessEntityId("p1:battle:T12b");
    await PlaceDigimon(own, P1, tamer2, level: 0, sources: 0, dp: 0);
    var secDigi2 = new HeadlessEntityId("p1:security:SD2");
    await PlaceInZone(own, P1, secDigi2, ChoiceZone.Security, dp: 3000);
    Register(own, new ST3_12(), "ST3_12", tamer2);
    AssertEqual(3000, ContinuousDpGate.ResolveDp(own, secDigi2, baseDp: 3000), "owner turn: no buff");
}

async Task ST3_13_Buff()
{
    // [Main] +3000 to a chosen owner Digimon.
    EngineContext context = Context(P1);
    var mine = new HeadlessEntityId("p1:battle:MINE");
    await PlaceDigimon(context, P1, mine, level: 4, sources: 0, dp: 4000);
    var main = (ActivatedTargetBuffEffect)Activated(new ST3_13(), context, EffectTiming.OptionSkill);
    main.ApplyBuff(new[] { mine });
    AssertEqual(7000, ContinuousDpGate.ResolveDp(context, mine, baseDp: 4000), "[Main] +3000 to my Digimon");

    // [Security] +5000 player-scope to all your Digimon, plus this card returns to hand.
    EngineContext sec = Context(P1);
    var d1 = new HeadlessEntityId("p1:battle:D1");
    await PlaceDigimon(sec, P1, d1, level: 4, sources: 0, dp: 4000);
    var opt = new HeadlessEntityId("p1:security:OPT13");
    await PlaceInZone(sec, P1, opt, ChoiceZone.Security, dp: 0);
    IReadOnlyList<ICardEffect> effects = new ST3_13().CardEffects(EffectTiming.SecuritySkill, new CardSource(sec, opt, P1));

    // Resolution order: apply the +5000 player-scope buffs first (observe them this turn), THEN bounce the
    // option card to hand. (The buffs are registered while the card resolves; once the card leaves play the
    // engine unregisters its bindings — so the buff is asserted before the self-bounce, matching the turn's
    // observable state.)
    foreach (ActivatedPlayerScopeBuffEffect ps in effects.OfType<ActivatedPlayerScopeBuffEffect>())
    {
        ps.ApplyBuff();
    }

    AssertEqual(9000, ContinuousDpGate.ResolveDp(sec, d1, baseDp: 4000), "[Security] +5000 to all my Digimon");

    AddThisCardToHandEffect hand = effects.OfType<AddThisCardToHandEffect>().Single();
    var handSink = Sink(sec);
    hand.Apply(handSink);
    await handSink.FlushAsync();
    AssertTrue(InZone(sec, P1, ChoiceZone.Hand, opt), "[Security] this card returned to its owner's hand");
}

async Task ST3_14_Debuff()
{
    EngineContext context = Context(P1);
    await DebuffOn(context, new ST3_14(), EffectTiming.OptionSkill, -2000);

    // [Security] returns this card to hand.
    EngineContext sec = Context(P1);
    var opt = new HeadlessEntityId("p1:security:OPT14");
    await PlaceInZone(sec, P1, opt, ChoiceZone.Security, dp: 0);
    var hand = (AddThisCardToHandEffect)new ST3_14().CardEffects(EffectTiming.SecuritySkill, new CardSource(sec, opt, P1)).Single();
    var s = Sink(sec);
    hand.Apply(s);
    await s.FlushAsync();
    AssertTrue(InZone(sec, P1, ChoiceZone.Hand, opt), "[Security] returned to hand");
}

async Task ST3_15_SecurityAttack()
{
    // [Main] Security Attack -3 to a chosen opponent Digimon.
    EngineContext context = Context(P1);
    var foe = new HeadlessEntityId("p2:battle:FOE15");
    await PlaceDigimon(context, P2, foe, level: 4, sources: 0, dp: 4000);
    var main = (ActivatedTargetBuffEffect)Activated(new ST3_15(), context, EffectTiming.OptionSkill);
    main.ApplyBuff(new[] { foe });
    AssertEqual(1, ContinuousModifierGate.ResolveSecurityAttack(context, foe, baseSecurityAttack: 4), "[Main] SA 4 - 3 = 1");

    // [Security] -1 Security Attack to ALL opponent Digimon (opponent-scoped player buff).
    EngineContext sec = Context(P1);
    var foe2 = new HeadlessEntityId("p2:battle:FOE15b");
    await PlaceDigimon(sec, P2, foe2, level: 4, sources: 0, dp: 4000);
    var opt = new HeadlessEntityId("p1:security:OPT15");
    var ps = (ActivatedPlayerScopeBuffEffect)new ST3_15().CardEffects(EffectTiming.SecuritySkill, new CardSource(sec, opt, P1)).Single();
    ps.ApplyBuff();
    AssertEqual(3, ContinuousModifierGate.ResolveSecurityAttack(sec, foe2, baseSecurityAttack: 4), "[Security] all opponents SA 4 - 1 = 3");
}

async Task ST3_16_Debuff()
{
    EngineContext context = Context(P1);
    await DebuffOn(context, new ST3_16(), EffectTiming.OptionSkill, -10000);

    // [Security] reuses the Main option.
    EngineContext sec = Context(P1);
    var opt = new HeadlessEntityId("p1:security:OPT16");
    ICardEffect security = new ST3_16().CardEffects(EffectTiming.SecuritySkill, new CardSource(sec, opt, P1)).Single();
    AssertTrue(security is ReuseMainOptionEffect, "[Security] reuses the Main option");
}

// --- Shared debuff helpers -----------------------------------------------

async Task DebuffCase(CEntity_Effect card, EffectTiming timing, int delta)
{
    EngineContext context = Context(P1);
    await DebuffOn(context, card, timing, delta);
}

async Task DebuffOn(EngineContext context, CEntity_Effect card, EffectTiming timing, int delta)
{
    var foe = new HeadlessEntityId($"p2:battle:FOE{delta}");
    await PlaceDigimon(context, P2, foe, level: 4, sources: 0, dp: 8000);
    var effect = (ActivatedTargetBuffEffect)Activated(card, context, timing);
    ChoiceRequest request = effect.BuildRequest(Both);
    AssertEqual(1, request.Candidates.Count, "the opponent Digimon is the candidate");
    effect.ApplyBuff(new[] { foe });
    AssertEqual(8000 + delta, ContinuousDpGate.ResolveDp(context, foe, baseDp: 8000), $"DP {delta}");
}

// --- Helpers -------------------------------------------------------------

EngineContext Context(HeadlessPlayerId turnPlayer)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 3);
    context.TurnController.Initialize(new[] { P1, P2 }, turnPlayer);
    return context;
}

ICardEffect Activated(CEntity_Effect card, EngineContext context, EffectTiming timing) =>
    card.CardEffects(timing, new CardSource(context, new HeadlessEntityId("p1:trash:ACT"), P1)).Single();

IReadOnlyList<EffectBinding> Register(EngineContext context, CEntity_Effect effect, string number, HeadlessEntityId source) =>
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
    cards.Upsert(new CardRecord(defId, defId.Value, id.Value, new Dictionary<string, object?>(), CardType: "Digimon"));
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

async Task ResolveTrigger(EngineContext context, IReadOnlyList<EffectBinding> bindings, string timing)
{
    EffectBinding binding = bindings.Single(b => string.Equals(b.Request.Timing, timing, StringComparison.Ordinal));
    AssertTrue(binding.Effect is not null, $"trigger binding for {timing} carries an effect body");
    var sink = Sink(context);
    await binding.Effect!.ResolveAsync(new CardEffectResolveContext(binding.Request), sink);
    await sink.FlushAsync();
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController, context.EffectRegistry);

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(id);

int SecurityCount(EngineContext context, HeadlessPlayerId player) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, ChoiceZone.Security).Count;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
