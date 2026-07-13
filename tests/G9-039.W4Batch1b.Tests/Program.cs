using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// PRIM-W4 batch 1b (G9-039): modifiers/keywords + memory.
//  ChangeBaseDPGlobal (BaseDp modifier, behavior-live), InvertSAttack / ChangeLinkMax (carried modifier),
//  Collision / Vortex / TreatAsDigimon (keyword grant), Gain1TamerOwnerConditional / EoTLose3 (memory).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ChangeBaseDPGlobal +1000 -> baseDpDelta applies to owner's Digimon (player-scope)", ChangeBaseDp),
    ("InvertSAttack -> invertSecurityAttackDelta carried", () =>
        Carried(c => CardEffectFactory.InvertSAttackStaticEffect(null, 1, false, c, null), ModifierHelpers.InvertSecurityAttackDeltaKey)),
    ("ChangeLinkMaxStatic +1 -> linkedMaxDelta carried (player-scope)", () =>
        CarriedScoped(c => CardEffectFactory.ChangeLinkMaxStaticEffect(null, 1, false, c, null), ModifierHelpers.LinkedMaxDeltaKey)),
    ("Collision -> owner's ally has Collision", () => Keyword(c => CardEffectFactory.CollisionStaticEffect(null, false, c, null), ContinuousKeywordGate.Collision)),
    // (K1) un-flattened: the marker is its OWN keyword (player-target eligibility), NOT a Vortex grant.
    ("VortexCanAttackPlayers -> owner's ally has the marker (not Vortex)", () => Keyword(c => CardEffectFactory.VortexCanAttackPlayersStaticEffect(null, false, c, null, "vortex-marker"), ContinuousKeywordGate.VortexCanAttackPlayers)),
    ("TreatAsDigimon -> HasKeyword(TreatAsDigimon)", TreatAsDigimon),
    ("(K4) IsDigimon chokepoint: a TreatAsDigimon Tamer counts as a Digimon (predicate honored)", TreatAsDigimonChokepoint),
    ("(K4) a TreatAsDigimon Tamer with hasBlocker is an eligible blocker (consumer wired)", TreatAsDigimonBlocks),
    ("Gain1TamerOwnerConditional: condition true -> +1 / false -> 0", Gain1Conditional),
    ("EoTLose3Memory: 5 -> 2 at end of your turn", EoTLose3),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failures.Add(test.Name); Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}"); }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task ChangeBaseDp()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var ally = await Place(context, P1, "ALLY");
    // MIGRATION-NOTE (P7 test-fix): ChangeBaseDPClass is a new-model kind-class with no ToBinding/EffectRegistry
    // bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The gate this test checks (PlayerScopeCarries
    // -> the substrate EffectRegistry player-scope query) does not scan the AS-IS live effect list, so there is
    // no buildable way to make this grant observable yet. Assertion below is UNCHANGED and EXPECTED TO FAIL
    // until stage B lands — tracked, not silently weakened.
    CardEffectFactory.ChangeBaseDPGlobalEffect(null, 1000, false, new CardSource(context, src, P1), null);
    AssertTrue(PlayerScopeCarries(context, ally, ModifierHelpers.BaseDpDeltaKey), "baseDpDelta applies to owner's Digimon (player-scope)");
}

async Task TreatAsDigimon()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.TreatAsDigimon), "absent before");
    // MIGRATION-NOTE (P7 test-fix): TreatAsDigimonClass is a new-model kind-class with no ToBinding/EffectRegistry
    // bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The gate this test checks
    // (ContinuousKeywordGate.HasKeyword) reads only the substrate EffectRegistry, not the AS-IS live scan, so
    // there is no buildable way to make this grant observable yet. Assertion below is UNCHANGED and EXPECTED TO
    // FAIL until stage B lands — tracked, not silently weakened.
    CardEffectFactory.TreatAsDigimonStaticEffect(null, false, new CardSource(context, id, P1), null);
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.TreatAsDigimon), "TreatAsDigimon live");
}

// (K4) AS-IS Permanent.IsDigimon single chokepoint: native CardType OR an active ITreatAsDigimonEffect
// whose permanentCondition accepts the permanent being judged.
async Task TreatAsDigimonChokepoint()
{
    EngineContext context = Context();
    var tamer = await Place(context, P1, "TAMER", cardType: "Tamer");
    var option = await Place(context, P1, "OPTION", cardType: "Option");
    var digimon = await Place(context, P1, "DIGIMON");

    AssertTrue(!ContinuousKeywordGate.IsDigimon(context, tamer), "the Tamer is not a Digimon before the grant");
    // MIGRATION-NOTE (P7 test-fix): TreatAsDigimonClass is a new-model kind-class with no ToBinding/EffectRegistry
    // bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). The gate this test checks
    // (ContinuousKeywordGate.IsDigimon) reads only the substrate EffectRegistry, not the AS-IS live scan, so
    // there is no buildable way to make this grant observable yet. Assertions below are UNCHANGED and EXPECTED
    // TO FAIL until stage B lands — tracked, not silently weakened.
    CardEffectFactory.TreatAsDigimonStaticEffect(p => p.IsTamer, false, new CardSource(context, tamer, P1), null);

    AssertTrue(ContinuousKeywordGate.IsDigimon(context, tamer), "the Tamer is treated as a Digimon");
    AssertTrue(!ContinuousKeywordGate.IsDigimon(context, option), "the predicate is honored (Option not matched)");
    AssertTrue(ContinuousKeywordGate.IsDigimon(context, digimon), "a native Digimon stays a Digimon");
}

async Task TreatAsDigimonBlocks()
{
    EngineContext context = Context();
    var attacker = await Place(context, P1, "ATK");
    var tamer = await Place(context, P2, "TAMER", cardType: "Tamer");
    SetFlag(context, tamer, BlockTiming.HasBlockerKey, true);

    context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    var withoutKeyword = new BlockTiming().GetBlockerCandidates(context);
    AssertTrue(!withoutKeyword.Any(c => c.BlockerId == tamer), "without the keyword a Tamer cannot block (control)");

    // MIGRATION-NOTE (P7 test-fix): TreatAsDigimonClass is a new-model kind-class with no ToBinding/EffectRegistry
    // bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). BlockTiming's blocker-candidate scan reads
    // only the substrate EffectRegistry, not the AS-IS live scan, so there is no buildable way to make this
    // grant observable yet. Assertion below is UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked,
    // not silently weakened.
    CardEffectFactory.TreatAsDigimonStaticEffect(p => p.IsTamer, false, new CardSource(context, tamer, P2), null);
    var withKeyword = new BlockTiming().GetBlockerCandidates(context);
    AssertTrue(withKeyword.Any(c => c.BlockerId == tamer), "the TreatAsDigimon Tamer is an eligible blocker");
}

void SetFlag(EngineContext context, HeadlessEntityId id, string key, bool value)
{
    context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? inst);
    context.CardInstanceRepository.Upsert(inst! with
    {
        Metadata = new Dictionary<string, object?>(inst!.Metadata, StringComparer.Ordinal) { [key] = value }
    });
}

async Task Gain1Conditional()
{
    foreach ((bool cond, int expected) in new[] { (true, 1), (false, 0) })
    {
        EngineContext context = Context();
        context.MemoryController.Set(0);
        var tamer = await Place(context, P1, "TAMER");
        await Resolve(context, CardEffectFactory.Gain1MemoryTamerOwnerDigimonConditionalEffect("desc", null, () => cond, new CardSource(context, tamer, P1)));
        AssertEqual(expected, context.MemoryController.Current.Current, $"cond={cond} -> {expected}");
    }
}

async Task EoTLose3()
{
    EngineContext context = Context();
    context.MemoryController.Set(5);
    var tamer = await Place(context, P1, "TAMER");
    await Resolve(context, CardEffectFactory.EoTLose3Memory(new CardSource(context, tamer, P1)));
    AssertEqual(2, context.MemoryController.Current.Current, "5 - 3 = 2");
}

async Task Carried(Func<CardSource, ICardEffect> build, string key)
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    // MIGRATION-NOTE (P7 test-fix): InvertSAttackClass (the only Carried() consumer) is a new-model kind-class
    // with no ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md). HasFlag reads
    // only the substrate EffectRegistry, not the AS-IS live scan, so there is no buildable way to make this grant
    // observable yet. The factory call is kept (exercises construction); registration is skipped since there is
    // no bridge. Assertion below is UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked, not silently
    // weakened.
    build(new CardSource(context, id, P1));
    AssertTrue(HasFlag(context, id, key), $"'{key}' carried");
}

async Task CarriedScoped(Func<CardSource, ICardEffect> build, string key)
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var ally = await Place(context, P1, "ALLY");
    // MIGRATION-NOTE (P7 test-fix): ChangeLinkMaxClass (the only CarriedScoped() consumer) is a new-model
    // kind-class with no ToBinding/EffectRegistry bridge (stage-B RED, docs/audit/rebuild_p6_stageA_notes.md).
    // PlayerScopeCarries reads only the substrate EffectRegistry, not the AS-IS live scan, so there is no
    // buildable way to make this grant observable yet. The factory call is kept (exercises construction);
    // registration is skipped since there is no bridge. Assertion below is UNCHANGED and EXPECTED TO FAIL until
    // stage B lands — tracked, not silently weakened.
    build(new CardSource(context, src, P1));
    AssertTrue(PlayerScopeCarries(context, ally, key), $"'{key}' carried onto owner's ally (player-scope)");
}

bool PlayerScopeCarries(EngineContext context, HeadlessEntityId cardId, string key)
{
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? inst);
    CardRecord card = context.CardRepository.GetCard(inst!.DefinitionId);
    return HeadlessDCGO.Engine.Headless.Effects.PlayerScopeContinuousHelpers
        .CollectApplicable(context.EffectRegistry, ContinuousRestrictionGate.Scope, inst.OwnerId, card)
        .Any(e => e.Context.Values.ContainsKey(key));
}

async Task Keyword(Func<CardSource, ICardEffect> build, string keyword)
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var ally = await Place(context, P1, "ALLY");
    // MIGRATION-NOTE (P7 test-fix): the Keyword() consumers here (CollisionClass, VortexCanAttackPlayersClass)
    // are new-model kind-classes with no ToBinding/EffectRegistry bridge (stage-B RED,
    // docs/audit/rebuild_p6_stageA_notes.md). ContinuousKeywordGate.HasKeyword reads only the substrate
    // EffectRegistry, not the AS-IS live scan, so there is no buildable way to make this grant observable yet.
    // The factory call is kept (exercises construction); registration is skipped since there is no bridge.
    // Assertion below is UNCHANGED and EXPECTED TO FAIL until stage B lands — tracked, not silently weakened.
    build(new CardSource(context, src, P1));
    AssertTrue(ContinuousKeywordGate.HasKeyword(context, ally, keyword), $"owner's ally has {keyword}");
}

// --- Helpers -------------------------------------------------------------

bool HasFlag(EngineContext context, HeadlessEntityId id, string key) =>
    context.EffectRegistry.GetContinuousEffects(new EffectQueryContext(ContinuousRestrictionGate.Scope, targetEntityId: id))
        .Any(e => e.Context.Values.ContainsKey(key))
    || context.EffectRegistry.GetContinuousEffects(new EffectQueryContext(ContinuousModifierGate.Scope, targetEntityId: id))
        .Any(e => e.Context.Values.ContainsKey(key));

async Task Resolve(EngineContext context, ICardEffect effect)
{
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue);
    if (!LegacyBindingBridge.TryToBinding(effect, "mem", out var binding) || binding is null)
        throw new InvalidOperationException($"{effect.GetType().Name} has no ToBinding bridge.");
    await ((IHeadlessCardEffect)effect).ResolveAsync(new CardEffectResolveContext(binding.Request), sink);
    await sink.FlushAsync();
}

EngineContext Context()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 939);
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    return context;
}

async Task<HeadlessEntityId> Place(EngineContext context, HeadlessPlayerId owner, string tag, string cardType = "Digimon")
{
    var cards = (CardDatabase)context.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, defId.Value, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 }, CardType: cardType));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false }));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertEqual<T>(T e, T a, string label) { if (!EqualityComparer<T>.Default.Equals(e, a)) throw new InvalidOperationException($"{label}: expected '{e}', got '{a}'."); }
