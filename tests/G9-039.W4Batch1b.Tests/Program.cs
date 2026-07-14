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
    ("InvertSAttack -> invertSecurityAttackDelta carried", InvertSAttack),
    ("ChangeLinkMaxStatic +1 -> effective link max raised on owner's ally (player-scope)", LinkMaxScoped),
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
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    int before = ContinuousDpGate.ResolveDp(context, ally, 4000);
    // SEAM (post-stage-B): ChangeBaseDPClass is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.FoldBaseDp (AS-IS Permanent.BaseDP) — attach it to the source card's controller
    // (a player-scope grant, folded over EVERY field permanent of Players_ForTurnPlayer, not just its own).
    // The original assertion checked the substrate registry directly (PlayerScopeCarries), which can never see
    // a new-model grant (no ToBinding bridge) regardless of stage — the AS-IS-faithful observation point is
    // the resolved DP itself (ContinuousDpGate.ResolveDp, which folds BaseDp before current-DP).
    // TEST-BUG FIX (same recurring class as AllianceStaticEffect/BlockerClass — precedent
    // docs/audit/rebuild_p6_stageB_notes.md §7): ChangeBaseDPGlobalEffect's internal PermanentCondition is
    // `permanentCondition != null && permanentCondition(permanent) && ...` — a NULL permanentCondition has NO
    // accept-all fallback, it makes EVERY permanent fail the check (global no-op). Pass `_ => true` for "any
    // of the owner's Digimon", matching the test's stated intent.
    var srcSource = new CardSource(context, src, P1);
    ICardEffect effect = CardEffectFactory.ChangeBaseDPGlobalEffect(_ => true, 1000, false, srcSource, null);
    srcSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
    int after = ContinuousDpGate.ResolveDp(context, ally, 4000);
    // AS-IS ChangeBaseDPGlobalEffect ("Origin DP is {value}", CardEffectFactory/ChangeOriginDP.cs) is a SET, not
    // an add (isUpDownFunc: () => false -> ChangeDP body does `DP = _changeValue()`, not `DP += _changeValue()`)
    // — the factory's own doc-comment ("Origin DP is X") and effect name confirm this; the ORIGINAL assertion
    // (`before + 1000`) assumed an additive delta that this factory does not have.
    AssertEqual(1000, after, "baseDpGlobal sets owner's Digimon origin DP to the fixed value (player-scope)");
}

async Task TreatAsDigimon()
{
    EngineContext context = Context();
    var id = await Place(context, P1, "SELF");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    AssertTrue(!ContinuousKeywordGate.HasKeyword(context, id, ContinuousKeywordGate.TreatAsDigimon), "absent before");
    // SEAM (post-stage-B): TreatAsDigimonClass is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.HasTreatAsDigimon (AS-IS Permanent.IsDigimon's ITreatAsDigimonEffect region) —
    // attach it to the card's own controller.
    var source = new CardSource(context, id, P1);
    ICardEffect effect = CardEffectFactory.TreatAsDigimonStaticEffect(null, false, source, null);
    source.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
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
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);

    AssertTrue(!ContinuousKeywordGate.IsDigimon(context, tamer), "the Tamer is not a Digimon before the grant");
    // SEAM (post-stage-B): TreatAsDigimonClass is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.HasTreatAsDigimon; attach it to the granting card's controller.
    var tamerSource = new CardSource(context, tamer, P1);
    ICardEffect effect = CardEffectFactory.TreatAsDigimonStaticEffect(p => p.IsTamer, false, tamerSource, null);
    tamerSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

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
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);

    context.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);
    var withoutKeyword = new BlockTiming().GetBlockerCandidates(context);
    AssertTrue(!withoutKeyword.Any(c => c.BlockerId == tamer), "without the keyword a Tamer cannot block (control)");

    // SEAM (post-stage-B): TreatAsDigimonClass is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.HasTreatAsDigimon (BlockTiming already consults ContinuousKeywordGate.IsDigimon,
    // BlockTiming.cs:233); attach it to the tamer's controller.
    var tamerSource = new CardSource(context, tamer, P2);
    ICardEffect effect = CardEffectFactory.TreatAsDigimonStaticEffect(p => p.IsTamer, false, tamerSource, null);
    tamerSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
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

// SEAM (post-stage-B): InvertSAttackClass is a new-model kind-class with no ToBinding/EffectRegistry bridge —
// the substrate-registry HasFlag check can never observe it (no card exists purely for construction fidelity).
// The AS-IS-faithful observation point is FoldSAttack's live IInvertSAttackEffect fold (AS-IS
// Permanent.InvertSecutiryValue), which FLIPS the direction of a paired IChangeSAttackEffect delta on the SAME
// scope (design item RD-P6B-9 / FAILb-01 precedent) — pair it with a real SA change to observe the flip.
async Task InvertSAttack()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var ally = await Place(context, P1, "ALLY");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);

    var srcSource = new CardSource(context, src, P1);
    var effects = new List<ICardEffect> { CardEffectFactory.ChangeSAttackStaticEffect(null, 2, false, srcSource, null) };
    srcSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effects);
    int withoutInvert = ContinuousModifierGate.ResolveSecurityAttack(context, ally, 3);

    effects.Add(CardEffectFactory.InvertSAttackStaticEffect(null, 1, false, srcSource, null));
    int withInvert = ContinuousModifierGate.ResolveSecurityAttack(context, ally, 3);

    AssertEqual(5, withoutInvert, "no invert: +2 raises 3 -> 5");
    AssertEqual(1, withInvert, "invertSecurityAttackDelta carried: the +2 increase is flipped to a decrease (3 -> 1)");
}

// SEAM (RD-P6B-16): ChangeLinkMaxClass is a new-model kind-class (IChangeLinkMaxEffect.GetLinkMax, no
// ToBinding). LinkHelpers.ResolveLinkedMax now UNIONs the AS-IS Permanent.LinkedMax interface scan
// (NewModelContinuousScan.FoldLinkedMax) — a player-scope grant (null permanentCondition = any battle-area
// permanent) folds over EVERY field permanent, so it raises the ally's effective link max too. Attach the
// grant to SRC's controller and observe through the real consumer (the earlier raw-registry PlayerScopeCarries
// check was the wrong layer — a new-model grant registers no binding).
async Task LinkMaxScoped()
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var ally = await Place(context, P1, "ALLY");
    using var _scope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    var srcSource = new CardSource(context, src, P1);
    ICardEffect effect = CardEffectFactory.ChangeLinkMaxStaticEffect(null, 1, false, srcSource, null);
    srcSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
    AssertEqual(LinkHelpers.DefaultLinkedMax + 1, LinkHelpers.ResolveLinkedMax(context, ally), "owner's ally effective link max +1 (player-scope)");
}

async Task Keyword(Func<CardSource, ICardEffect> build, string keyword)
{
    EngineContext context = Context();
    var src = await Place(context, P1, "SRC");
    var ally = await Place(context, P1, "ALLY");
    using var _ambientScope = AmbientMatchContext.Enter(context);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    // SEAM (post-stage-B): the Keyword() consumers here (CollisionClass, VortexCanAttackPlayersClass) are
    // new-model kind-classes observed via the unioned NewModelContinuousScan.HasCollision /
    // HasVortexCanAttackPlayers (both already unioned into ContinuousKeywordGate.HasKeyword) — attach the
    // built effect to the source card's controller (a player-scope grant, folded over the owner's whole field).
    var srcSource = new CardSource(context, src, P1);
    ICardEffect effect = build(srcSource);
    srcSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);
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

// Minimal AS-IS-shaped CEntity_Effect: the same seam every ported card definition class (e.g. `class BT1_001 :
// CEntity_Effect`) uses to surface its printed effect list to CardSource.EffectList/EffectList_ExceptAddedEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly List<ICardEffect> _effects;

    public TestCardEntityEffect(ICardEffect effect) { _effects = new List<ICardEffect> { effect }; }
    public TestCardEntityEffect(List<ICardEffect> effects) { _effects = effects; }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => _effects;
}
