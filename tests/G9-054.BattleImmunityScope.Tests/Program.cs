using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// M-2 #3 verification (G9-054): CanNotBeDestroyedByBattleStaticEffect is NOT condition-less — its
// permanentCondition (EX8_068: "your DS Digimon") DOES gate battle-deletion immunity. Only the separate 4-arg
// canNotBeDestroyedByBattleCondition (permanent == attacker || defender) is omitted, and that is trivially true
// for any battle-deletion candidate (only the battle's participants are ever "deleted by battle").

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("Matching (predicate) Digimon IS battle-immune", () => Immune(match: true)),
    ("Non-matching Digimon is NOT battle-immune (permanentCondition enforced, not condition-less)", () => Immune(match: false)),
    ("Self form (permanentCondition=null) grants self battle immunity", SelfForm),
    ("condition gate (e.g. memory>=1) is honoured LIVE — immunity off when false, on when true", ConditionGate),
    ("(AD1-G) GainCanNotBeDeletedByBattle: timed grant protects the TARGET, expires at opponent turn end", GainTimedGrant),
    ("(AD1-G) the grant is LIVE-gated on the target staying in play (leave -> off)", GainLiveGate),
    ("(AD1-G / RD-J-01) grant LANDS on a live-immune target — inert while immune, ACTIVE once immunity lifts", GainLandsInertUntilImmunityLifts),
    ("(AD1-G) the stored 4-arg battle predicate gates the immunity against the current attack", GainBattlePredicate),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}: {ex.Message}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

async Task Immune(bool match)
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var subject = await Place(ctx, P1, "SUBJ", level: match ? 5 : 4);
    // "Your Level-5 Digimon cannot be deleted by battle" (permanentCondition = Level==5); the 4-arg battle
    // condition is trivially true for a participant (AS-IS EX8_068: permanent == AttackingPermanent ||
    // permanent == DefendingPermanent) — spelled `_ => true` here, NOT null: AS-IS
    // CanNotBeDestroyedByBattleClass.CanNotBeDestroyedByBattle gates its WHOLE body on
    // `_canNotBeDestroyedByBattleCondition != null` (no accept-all fallback, same trap as
    // BlockerClass.IsBlocker/AllianceStaticEffect elsewhere in this suite) — a null condition would never
    // grant immunity regardless of permanentCondition.
    // (P7 RD-P6B-10 resolved SEAM) CanNotBeDestroyedByBattleClass is a new-model kind-class with no
    // ToBinding/EffectRegistry bridge — the AS-IS-faithful path is the LIVE cEntity_EffectController scan
    // NewModelContinuousScan.HasCanNotBeDestroyedByBattle/BattleDeletionGate.PreventsBattleDeletion now
    // performs. Attach the built effect via the same seam every ported card definition class uses.
    var srcCard = new CardSource(ctx, src, P1);
    ICardEffect built = CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
        canNotBeDestroyedByBattleCondition: (p, atk, def, defCard) => true, permanentCondition: p => p.Level == 5, isInheritedEffect: false,
        card: srcCard, condition: null, effectName: "cbdb-test");
    srcCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);

    bool immune = BattleDeletionGate.PreventsBattleDeletion(ctx, subject);
    AssertTrue(immune == match, $"battle-immune == {match} (permanentCondition gates it — NOT condition-less)");
}

async Task SelfForm()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF", level: 4);
    // (P7 RD-P6B-10 resolved SEAM) see the SEAM/condition note in Immune() above for the full explanation.
    var selfCard = new CardSource(ctx, self, P1);
    ICardEffect built = CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
        canNotBeDestroyedByBattleCondition: (p, atk, def, defCard) => true, permanentCondition: null, isInheritedEffect: false,
        card: selfCard, condition: null, effectName: "cbdb-test");
    selfCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, self), "self is battle-immune");
}

async Task ConditionGate()
{
    EngineContext ctx = Ctx();
    var self = await Place(ctx, P1, "SELF", level: 4);
    // Mirror EX8_068's CanUseCondition gate: immunity active only while the owner's memory >= 1. Evaluated LIVE.
    // (P7 RD-P6B-10 resolved SEAM) see the SEAM/condition note in Immune() above for the full explanation.
    var selfCard = new CardSource(ctx, self, P1);
    ICardEffect built = CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
        canNotBeDestroyedByBattleCondition: (p, atk, def, defCard) => true, permanentCondition: null, isInheritedEffect: false,
        card: selfCard, condition: () => ctx.MemoryController.Current.Current >= 1, effectName: "cbdb-test");
    selfCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(built);

    ctx.MemoryController.Set(0);
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, self), "memory 0 -> NOT immune (condition false)");
    ctx.MemoryController.Set(1);
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, self), "memory 1 -> immune (condition true)");
    ctx.MemoryController.Set(0);
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, self), "memory back to 0 -> immune turns OFF (re-evaluated live)");
}

// (AD1-G) AS-IS GainCanNotBeDeletedByBattle (GiveEffect/CanNotBeDeletedByBattle.cs:11-54): a timed,
// target-locked battle immunity built on the same factory family, added to a duration bucket.

async Task GainTimedGrant()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var target = await Place(ctx, P1, "TGT", level: 5);
    var bystander = await Place(ctx, P1, "OTHER", level: 5);

    // (R3-W3c-2) AS-IS callers ALWAYS pass a non-null 4-arg battle condition (AD1_011, etc.); the bucket path
    // (CanNotBeDestroyedByBattleClass) gates its whole body on `condition != null`, so a null condition grants NO
    // immunity — spelled `(_,_,_,_) => true` here (trivially true for a battle participant), NOT null.
    AssertTrue(CardEffectCommons.GainCanNotBeDeletedByBattle(
        new Permanent(ctx, target, P1), (p, atk, def, defCard) => true, EffectDuration.UntilOpponentTurnEnd,
        Grant(ctx, src, P1), "test-grant"), "grant registered");

    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, target), "the TARGET is battle-immune");
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, bystander), "the grant is target-locked (a bystander is not)");

    // (R3-W3c-2) The opponent's turn ends -> the AS-IS UntilOpponentTurnEnd BUCKET is reset by the AS-IS reset site
    // (HeadlessEndTurnCleanupFlow), NOT the invented registry-expiry sweep. The grant lives in the target's
    // UntilOpponentTurnEndEffects (target is its owner's permanent), cleared when the non-turn player (P1) is scoped
    // at P2's turn end.
    new HeadlessEndTurnCleanupFlow().Cleanup(ctx, new HeadlessTurnState(
        TurnNumber: 1, TurnPlayerId: P2, NonTurnPlayerId: P1,
        Phase: HeadlessPhase.End, StepCursor: TurnStepCursor.PhaseStart, IsFirstTurn: false, PlayerOrder: new[] { P1, P2 }));
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, target), "immunity expired at the opponent's turn end");
}

async Task GainLiveGate()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var target = await Place(ctx, P1, "TGT", level: 5);
    CardEffectCommons.GainCanNotBeDeletedByBattle(
        new Permanent(ctx, target, P1), (p, atk, def, defCard) => true, EffectDuration.UntilOpponentTurnEnd,
        Grant(ctx, src, P1), "test-live");

    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, target), "immune while in play");
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, target, ChoiceZone.BattleArea, ChoiceZone.Trash));
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, target), "leaving the battle area turns the grant off (live CanUse mirror)");
}

async Task GainLandsInertUntilImmunityLifts()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P2, "ENEMYSRC", level: 4);   // OPPONENT grants -> immunity-gated
    var target = await Place(ctx, P1, "TGT", level: 5);
    // A TOGGLEABLE immunity: the flipped CanNotAffectedStaticEffect (live CardSource.CanNotBeAffected scan) whose
    // CanUse re-reads `immunityActive` each call, so the same grant flips with the immunity state.
    bool immunityActive = true;
    var targetCard = new CardSource(ctx, target, P1);
    ICardEffect cnaEffect = CardEffectFactory.CanNotAffectedStaticEffect(
        null, null, false, targetCard, () => immunityActive);
    targetCard.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(cnaEffect);

    using var _ = AmbientMatchContext.Enter(ctx);
    // (RD-J-01) AS-IS grants UNCONDITIONALLY — the grant LANDS on a live-immune target (no grant-time refusal;
    // the earlier "immune target refuses the grant" pin asserted an INVENTED behavior).
    // Non-null 4-arg battle predicate (AS-IS callers always pass one; a null predicate grants no immunity —
    // GainTimedGrant note): trivially true for a battle participant, so the ONLY live gate is the immunity toggle.
    AssertTrue(CardEffectCommons.GainCanNotBeDeletedByBattle(
        new Permanent(ctx, target, P1), (p, atk, def, defCard) => true, EffectDuration.UntilOpponentTurnEnd,
        Grant(ctx, src, P2), "test-inert"), "RD-J-01: the grant LANDS on a live-immune target (AS-IS unconditional grant)");

    // While immune: the granted battle-immunity is INERT — its live CanUse is gated by CanNotBeAffected, so the
    // gate does not fire.
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, target), "inert while immune: the granted battle-immunity does not fire (live CanUse gated)");

    // Immunity expires -> the SAME grant becomes ACTIVE. The invented grant-time refusal would have stored nothing,
    // so this re-application could never happen.
    immunityActive = false;
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, target), "ACTIVE after immunity lifts: the same grant now prevents battle deletion (AS-IS re-application)");
}

async Task GainBattlePredicate()
{
    EngineContext ctx = Ctx();
    var src = await Place(ctx, P1, "SRC", level: 4);
    var target = await Place(ctx, P1, "TGT", level: 5);
    var attacker = await Place(ctx, P2, "ATK", level: 5);
    // AS-IS AD1_011 shape: protected only when THIS permanent is a battle participant vs a named attacker —
    // here: only when the attacker's top card is "ATK".
    CardEffectCommons.GainCanNotBeDeletedByBattle(
        new Permanent(ctx, target, P1),
        (self, atk, def, defCard) => atk is not null && atk.TopCard.EqualsCardName("ATK"),
        EffectDuration.UntilOpponentTurnEnd, Grant(ctx, src, P1), "test-pred");

    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(ctx, target), "no attack in flight -> predicate false -> not immune");
    ctx.AttackController.DeclareAttack(P2, attacker, P1, target, isDirectAttack: false);
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(ctx, target), "attacked by the named attacker -> predicate true -> immune");
}

// --- Helpers ---

EngineContext Ctx()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 954);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // (P7 test-fix) ICardEffect.CanTrigger gates on TurnStateMachine.DoneStartGame (phase past None/Setup) —
    // without this every candidate effect's CanUse(null) trivially returns false and the new-model scan never
    // fires (same fix already documented in rebuild_p6_stageB_notes.md §5). The legacy GainCanNotBeDeletedByBattle
    // tests below are unaffected (that path reads a registered replacement's context values directly, no CanUse
    // gate), so this was never needed until the new-model scan tests above.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string tag, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{owner.Value}:{tag}");
    cards.Upsert(new CardRecord(defId, tag, tag,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["level"] = level }, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 5000, ["isSuspended"] = false }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// (R3-W3c-1) AS-IS GainCanNotBeDeletedByBattle takes the causing `ICardEffect activateClass` (its EffectSourceCard
// is the grant source). Build a minimal ActivateClass sourced by `src` (owner determines opponent-relativity).
ActivateClass Grant(EngineContext ctx, HeadlessEntityId src, HeadlessPlayerId owner)
{
    var a = new ActivateClass();
    a.SetUpICardEffect("grant", _ => true, new CardSource(ctx, src, owner));
    return a;
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }

sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
