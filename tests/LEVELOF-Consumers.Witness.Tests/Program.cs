// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// LEVELOF-Consumers witness — the CardEffectCommons.LevelOf effect-FOLD reaches the gate of each live consumer.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// N-3: LevelOf (CardEffectCommons.cs:4341) delegates to the folding Permanent.Level (Permanent.cs:565), so an
// active IChangePermanentLevelEffect grant is visible to every gate routed through it
// (permanent_fidelity_audit_2026-07-20 §결론 3b). The fold itself is pinned only at PILOT/EXEMPLAR level; the
// three MIRROR consumers that read LevelOf on a per-card gate had no dedicated pin proving the fold reaches
// THEIR gate. This suite drives each with a live ChangePermanentLevelClass grant (attached to the target
// permanent's PermanentEffects, the AS-IS BT7_087/BT17_026 grant shape — surfaced to the fold via
// Permanent.EffectList(None)) versus an ungranted control, so the grant is the sole discriminator that flips
// the gate outcome:
//   * BT1_068 (BT1/Green): [All Turns] Security Attack +1 while the own permanent is Lv6+ (LevelOf>=6). A
//     printed-level-5 base -> gate CLOSED; a +level grant folding to 6 -> gate OPEN (ChangeSAttackClass.CanUse).
//   * ST4_01  (ST4/Green): the same shape for DP +1000 (ChangeDPClass.CanUse).
//   * BT2_095 (BT2/Blue): [Main] bounce candidacy = opponent battle Digimon AND LevelOf==3. A printed-level-2
//     opponent -> NOT a candidate (stays); a +level grant folding to 3 -> candidate, selected + bounced to hand
//     (the fold reaches the SelectPermanentEffect Mode.Bounce candidate gate end-to-end).
// All three cards are real 1:1 ports (verified not stubs), so no pre-port was needed.

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;
using HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT1_068: LevelOf fold reaches the [All Turns] Lv6+ Security Attack +1 gate (granted Lv6 opens; ungranted Lv5 closed)", BT1_068_LevelFoldReachesSAttackGate),
    ("ST4_01: LevelOf fold reaches the [All Turns] Lv6+ DP +1000 gate (granted Lv6 opens; ungranted Lv5 closed)", ST4_01_LevelFoldReachesDpGate),
    ("BT2_095: LevelOf fold reaches the [Main] level-3 bounce candidate gate (granted Lv3 bounced; ungranted Lv2 stays)", BT2_095_LevelFoldReachesBounceSelection),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st) { Console.WriteLine(string.Join('\n', st.Split('\n').Take(10))); }
    }
}
Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ BT1_068 ═══════════════════════════════════

async Task BT1_068_LevelFoldReachesSAttackGate()
{
    // ungranted control: printed level 5 -> LevelOf 5 (<6) -> the inherited Security Attack +1 static effect gate is closed.
    {
        EngineContext ctx = NewCtx(6801);
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
        HeadlessEntityId id = StageDigimon(ctx, P1, "BT1068-CTRL", level: 5);
        var card = new CardSource(ctx, id, P1);
        var sa = (ChangeSAttackClass)new BT1_068().CardEffects(EffectTiming.None, card).Single();
        AssertFalse(sa.CanUse(new Hashtable()),
            "ungranted: printed level 5 (<6) -> BT1_068 [All Turns] Security Attack +1 gate is CLOSED");
    }

    // granted: a ChangePermanentLevelClass folding the own permanent to 6 -> LevelOf==6 -> the gate opens.
    {
        EngineContext ctx = NewCtx(6802);
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
        HeadlessEntityId id = StageDigimon(ctx, P1, "BT1068-GRANT", level: 5);
        GrantLevel(ctx, id, P1, newLevel: 6);
        var card = new CardSource(ctx, id, P1);
        var sa = (ChangeSAttackClass)new BT1_068().CardEffects(EffectTiming.None, card).Single();
        AssertTrue(sa.CanUse(new Hashtable()),
            "granted: ChangePermanentLevelClass folds LevelOf to 6 -> BT1_068's Lv6+ Security Attack +1 gate reaches TRUE (fold plumbing verified)");
    }
}

// ═══════════════════════════════════ ST4_01 ═══════════════════════════════════

async Task ST4_01_LevelFoldReachesDpGate()
{
    // ungranted control: printed level 5 -> LevelOf 5 (<6) -> the inherited DP +1000 static effect gate is closed.
    {
        EngineContext ctx = NewCtx(401);
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
        HeadlessEntityId id = StageDigimon(ctx, P1, "ST401-CTRL", level: 5);
        var card = new CardSource(ctx, id, P1);
        var dp = (ChangeDPClass)new ST4_01().CardEffects(EffectTiming.None, card).Single();
        AssertFalse(dp.CanUse(new Hashtable()),
            "ungranted: printed level 5 (<6) -> ST4_01 [All Turns] DP +1000 gate is CLOSED");
    }

    // granted: fold to 6 -> gate opens.
    {
        EngineContext ctx = NewCtx(402);
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
        HeadlessEntityId id = StageDigimon(ctx, P1, "ST401-GRANT", level: 5);
        GrantLevel(ctx, id, P1, newLevel: 6);
        var card = new CardSource(ctx, id, P1);
        var dp = (ChangeDPClass)new ST4_01().CardEffects(EffectTiming.None, card).Single();
        AssertTrue(dp.CanUse(new Hashtable()),
            "granted: ChangePermanentLevelClass folds LevelOf to 6 -> ST4_01's Lv6+ DP +1000 gate reaches TRUE (fold plumbing verified)");
    }
}

// ═══════════════════════════════════ BT2_095 ═══════════════════════════════════

async Task BT2_095_LevelFoldReachesBounceSelection()
{
    // ungranted control: opponent Digimon printed level 2 (not 3) -> not a bounce candidate -> ActivateCoroutine
    // opens no selection window, so it stays on the battle area.
    {
        EngineContext ctx = NewCtx(2095);
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
        HeadlessEntityId opp = StageDigimon(ctx, P2, "BT2095-CTRL-OPP", level: 2);
        var card = new CardSource(ctx, new HeadlessEntityId("p1:opt:BT2095c"), P1);
        var eff = (ActivateClass)new BT2_095().CardEffects(EffectTiming.OptionSkill, card).Single();
        await eff.Activate(CardEffectCommons.OptionMainCheckHashtable(card));
        AssertTrue(InZone(ctx, P2, ChoiceZone.BattleArea, opp),
            "ungranted: the level-2 opponent Digimon is NOT a level-3 bounce candidate (LevelOf==3 gate closed) -> stays on the battle area");
        AssertFalse(InZone(ctx, P2, ChoiceZone.Hand, opp),
            "ungranted: it was not returned to hand");
    }

    // granted: fold the opponent Digimon to level 3 -> now a candidate -> selected + bounced to its owner's hand.
    {
        EngineContext ctx = NewCtx(2096);
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx);
        HeadlessEntityId opp = StageDigimon(ctx, P2, "BT2095-GR-OPP", level: 2);
        GrantLevel(ctx, opp, P2, newLevel: 3);
        ((ScriptedChoiceProvider)ctx.ChoiceProvider).Enqueue(ChoiceResult.Select(opp));
        var card = new CardSource(ctx, new HeadlessEntityId("p1:opt:BT2095g"), P1);
        var eff = (ActivateClass)new BT2_095().CardEffects(EffectTiming.OptionSkill, card).Single();
        await eff.Activate(CardEffectCommons.OptionMainCheckHashtable(card));
        AssertTrue(InZone(ctx, P2, ChoiceZone.Hand, opp),
            "granted: ChangePermanentLevelClass folds LevelOf to 3 -> the opponent Digimon becomes a bounce candidate and is returned to its owner's hand (fold reaches the selection gate end-to-end)");
        AssertFalse(InZone(ctx, P2, ChoiceZone.BattleArea, opp),
            "granted: it left the battle area");
    }
}

// ═══════════════════════════════════ harness ═══════════════════════════════════

EngineContext NewCtx(int seed)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: seed);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);   // phase past None -> DoneStartGame true (CanTrigger/CanUse gate open)
    return ctx;
}

HeadlessEntityId StageDigimon(EngineContext ctx, HeadlessPlayerId owner, string idSuffix, int level)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId($"DEF:{idSuffix}");
    cards.Upsert(new CardRecord(defId, idSuffix, idSuffix,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level, ["dp"] = 5000 }, CardType: "Digimon"));
    var id = new HeadlessEntityId(idSuffix);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level, ["dp"] = 5000, ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return id;
}

// Attach a live ChangePermanentLevelClass grant that folds the target permanent to `newLevel` (the AS-IS
// BT7_087/BT17_026 grant shape: a ChangePermanentLevelClass whose GetLevel matches the target, held on the
// permanent's PermanentEffects list — surfaced to Permanent.Level's fold via Permanent.EffectList(None)).
void GrantLevel(EngineContext ctx, HeadlessEntityId permId, HeadlessPlayerId owner, int newLevel)
{
    var source = new CardSource(ctx, permId, owner);
    var grant = new ChangePermanentLevelClass();
    grant.SetUpICardEffect($"witness: treat as level {newLevel}", _ => true, source);
    grant.SetUpChangePermanentLevelClass(GetLevel: (permanent, level) =>
        permanent.InstanceId == permId ? newLevel : level);
    new Permanent(ctx, permId, owner).PermanentEffects.Add(_timing => grant);
}

bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(id);

static void AssertTrue(bool v, string m) { if (!v) throw new InvalidOperationException($"Assertion failed: {m}"); }
static void AssertFalse(bool v, string m) { if (v) throw new InvalidOperationException($"Assertion failed: {m}"); }
