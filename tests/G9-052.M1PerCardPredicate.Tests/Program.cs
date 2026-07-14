using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// M-1 (G9-052): ChangeSecurityDigimonCardDPStaticEffect honours cardCondition 1:1 — the predicate decides the
// affected set INCLUDING which player. "Your opponent's Security Digimon get -2000 DP" must hit the ENEMY's
// Security only, not the owner's (the previous port hardcoded owner-scope = wrong player).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Opponent-targeting cardCondition hits enemy Security only", EnemySecurity),
    ("Self-targeting cardCondition hits own Security only", OwnSecurity),
    ("Security-zone scope: enemy battle-area Digimon unaffected", ZoneScope),
    ("UseRequirements: ignore-color active only with a matching permanent", UseReq),
    ("AddSelfDigivolution cardCondition: added requirement reaches matching cards only", AddSelfCardCond),
    ("M-5 ChangeBaseDPGlobal applies to BOTH players' matching Digimon (not owner-only)", BaseDpGlobal),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task EnemySecurity()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var ownSec = await Place(ctx, P1, "OWNSEC", ChoiceZone.Security);
    var enemySec = await Place(ctx, P2, "ENEMYSEC", ChoiceZone.Security);
    // "Your opponent's Security Digimon get -2000 DP" — predicate selects the enemy (source owner is P1).
    // STOP (post-stage-B re-diagnosis, per task brief — heavy-substrate, outside touch scope):
    // ChangeSecurityDigimonCardDPStaticEffect returns ChangeCardDPClass (IChangeCardDPEffect.GetDP(int,
    // CardSource) — note CardSource, NOT Permanent). Its AS-IS consumer is a SEPARATE property,
    // CardSource.CardDP (DCGO CardSource.cs:2383), NOT Permanent.DP — ContinuousDpGate.ResolveDp (this test's
    // observation point) mirrors Permanent.DP, a different getter entirely. The mirror's real CardDP-shaped
    // consumer is SecurityResolver.cs's security-battle DP fold (Headless/Runtime/SecurityResolver.cs:694,
    // explicitly documented there as "a SEPARATE fold... NOT the permanent-DP pipeline", reading only the
    // legacy SecurityCardDpDeltaKey registry key) — neither SecurityResolver.cs nor ContinuousDpGate.cs's
    // Permanent.DP path is the right union target for this new-model grant; the correct target
    // (SecurityResolver.cs) is not a Headless/Runtime/*Gate.cs file, outside this pass's touch scope. Not
    // forced; left failing (querying ResolveDp here was already the wrong observation point regardless).
    CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect(
        cs => cs.Owner == P2, -2000, false, new CardSource(ctx, src, P1), null, $"sd:{src.Value}");

    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemySec, 5000) == 3000, "enemy Security -2000");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, ownSec, 5000) == 5000, "own Security unchanged (right player)");
}

async Task OwnSecurity()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var ownSec = await Place(ctx, P1, "OWNSEC", ChoiceZone.Security);
    var enemySec = await Place(ctx, P2, "ENEMYSEC", ChoiceZone.Security);
    // STOP: same root cause as EnemySecurity() above (ChangeCardDPClass's real AS-IS consumer is
    // CardSource.CardDP / SecurityResolver.cs, not Permanent.DP / ContinuousDpGate — and SecurityResolver.cs
    // is not a *Gate.cs touch-scope file).
    CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect(
        cs => cs.Owner == P1, 2000, false, new CardSource(ctx, src, P1), null, $"sd:{src.Value}");

    AssertTrue(ContinuousDpGate.ResolveDp(ctx, ownSec, 5000) == 7000, "own Security +2000");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemySec, 5000) == 5000, "enemy Security unchanged");
}

async Task ZoneScope()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
    var enemyBattle = await Place(ctx, P2, "ENEMYBATTLE", ChoiceZone.BattleArea);
    // NOTE: same STOP as EnemySecurity()/OwnSecurity() above. This assertion PASSES, but coincidentally — the
    // grant is unobserved either way, so "unaffected" holds regardless of whether the zone-scope is honoured.
    CardEffectFactory.ChangeSecurityDigimonCardDPStaticEffect(
        cs => cs.Owner == P2, -2000, false, new CardSource(ctx, src, P1), null, $"sd:{src.Value}");

    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemyBattle, 5000) == 5000, "enemy BATTLE-area Digimon unaffected (Security-zone scope)");
}

async Task UseReq()
{
    // ignore-color is gated on the owner controlling a Level-5 Digimon (cardCondition via TopCard).
    foreach (bool hasMatch in new[] { false, true })
    {
        EngineContext ctx = Ctx();
        var src = await Place(ctx, P1, "SRC", ChoiceZone.BattleArea);
        if (hasMatch)
        {
            await PlaceLevel(ctx, P1, "MATCH", ChoiceZone.BattleArea, level: 5);
        }

        // STOP (post-stage-B re-diagnosis, per task brief — heavy-substrate, outside touch scope; same root
        // cause as G9-037.W3LinkColor's UseRequirements subtest): IgnoreColorConditionClass is a real
        // new-model kind-class (IIgnoreColorConditionEffect.IgnoreColorCondition, no ToBinding). Its consumer
        // is DigivolveAction's ignore-color-requirement check (Headless/Runtime/DigivolveAction.cs,
        // CanIgnoreColorRequirement / IgnoreColorRequirementKey) — not a Headless/Runtime/*Gate.cs file, so no
        // union is reachable from this pass's touch scope. The raw-registry query below was already the wrong
        // layer regardless. Not forced; left failing.
        CardEffectFactory.UseRequirements(new CardSource(ctx, src, P1), cs => cs.Level == 5);

        bool active = ContinuousScopeEvaluation
            .ApplicableEffects(ctx, ContinuousRestrictionGate.Scope, src)
            .Any(e => e.Context.Values.TryGetValue(DigivolveAction.IgnoreColorRequirementKey, out object? v) && v is true);
        AssertTrue(active == hasMatch, $"ignore-color active == {hasMatch} (cardCondition gate honored)");
    }
}

async Task BaseDpGlobal()
{
    EngineContext ctx = Ctx();
    var src = await PlaceLevel(ctx, P1, "SRC", ChoiceZone.BattleArea, level: 4);
    var ownLv5 = await PlaceLevel(ctx, P1, "OWN5", ChoiceZone.BattleArea, level: 5);
    var enemyLv5 = await PlaceLevel(ctx, P2, "ENE5", ChoiceZone.BattleArea, level: 5);
    var enemyLv4 = await PlaceLevel(ctx, P2, "ENE4", ChoiceZone.BattleArea, level: 4);
    using var _ambientScope = AmbientMatchContext.Enter(ctx);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    // "All Level-5 Digimon get +1000 base DP" — global (both players), predicate = Level==5.
    // SEAM (post-stage-B): ChangeBaseDPClass is a new-model kind-class observed via the unioned
    // NewModelContinuousScan.FoldBaseDp (already unioned into ContinuousDpGate.ResolveDp) — attach it to the
    // source card's controller. FoldBaseDp/ScanPlayers iterates Players_ForTurnPlayer (ALL players, turn-first
    // order), so a global (non-owner-scoped) predicate genuinely reaches both players' matching Digimon.
    var srcSource = new CardSource(ctx, src, P1);
    ICardEffect effect = CardEffectFactory.ChangeBaseDPGlobalEffect(p => p.Level == 5, 1000, false, srcSource, null);
    srcSource.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(effect);

    // TEST-BUG FIX (same recurring class as G9-039's ChangeBaseDp — precedent
    // docs/audit/rebuild_p6_stageB_notes.md): ChangeBaseDPGlobalEffect ("Origin DP is {value}",
    // CardEffectFactory/ChangeOriginDP.cs) is a SET, not an add (isUpDownFunc: () => false ->
    // ChangeDP body does `DP = _changeValue()`, not `DP += _changeValue()`) — the AS-IS effect name and the
    // DCGO source (ChangeOriginDP.cs:66-74) confirm this byte-for-byte. The ORIGINAL assertions (`5000 + 1000`)
    // assumed an additive delta this factory does not have.
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, ownLv5, 5000) == 1000, "own Lv5 origin DP set to the fixed value");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemyLv5, 5000) == 1000, "ENEMY Lv5 too (global, not owner-only)");
    AssertTrue(ContinuousDpGate.ResolveDp(ctx, enemyLv4, 5000) == 5000, "enemy Lv4 unchanged (predicate)");
}

async Task AddSelfCardCond()
{
    EngineContext ctx = Ctx();
    var src = await PlaceNamed(ctx, P1, "SRC", "Sourcemon", ChoiceZone.BattleArea);
    var match = await PlaceNamed(ctx, P1, "MATCH", "UlforceVeedramon", ChoiceZone.Hand);
    var nonMatch = await PlaceNamed(ctx, P1, "OTHER", "Agumon", ChoiceZone.Hand);
    // "Your UlforceVeedramon cards can digivolve from <permanentCondition>" — cardCondition targets other cards.
    // STOP: same root cause as G9-024.AddDigivolutionRequirement/G9-044.AddSelfDigivolveReq —
    // AddDigivolutionRequirementClass is a real new-model kind-class (IAddDigivolutionRequirementEffect.
    // GetEvoCost) whose AS-IS consumer (CardSource.EvoCosts/CostList) has no mirror equivalent wired into
    // DigivolveAction at all; DigivolveAction's OWN added-requirement consumer reads a different legacy key
    // (AddedEvolutionPredicateKey, from AddedDigivolutionRequirementPredicateEffect) that this factory never
    // produces. DigivolveAction.cs is not a Headless/Runtime/*Gate.cs file — outside this pass's touch scope.
    // Not forced; left failing.
    CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(
        permanentCondition: _ => true, digivolutionCost: 0, ignoreDigivolutionRequirement: false,
        card: new CardSource(ctx, src, P1), condition: null, cardCondition: cs => cs.EqualsCardName("UlforceVeedramon"));

    AssertTrue(HasAddedReq(ctx, match), "added requirement reaches the matching (UlforceVeedramon) card");
    AssertTrue(!HasAddedReq(ctx, nonMatch), "added requirement does NOT reach a non-matching card (cardCondition honored)");
}

bool HasAddedReq(EngineContext ctx, HeadlessEntityId cardId) =>
    ContinuousScopeEvaluation.ApplicableEffects(ctx, ContinuousRestrictionGate.Scope, cardId)
        .Any(e => e.Context.Values.ContainsKey(DigivolveAction.AddedEvolutionPredicateKey));

async Task<HeadlessEntityId> PlaceNamed(EngineContext ctx, HeadlessPlayerId owner, string tag, string name, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, name, name,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 6 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

async Task<HeadlessEntityId> PlaceLevel(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 952);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, ChoiceZone zone)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = 4 }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:{zone}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    return id;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

// Minimal AS-IS-shaped CEntity_Effect: the same seam every ported card definition class (e.g. `class BT1_001 :
// CEntity_Effect`) uses to surface its printed effect list to CardSource.EffectList/EffectList_ExceptAddedEffects.
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;

    public TestCardEntityEffect(ICardEffect effect)
    {
        _effect = effect;
    }

    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
