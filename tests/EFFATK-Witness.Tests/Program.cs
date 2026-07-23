using System.Collections;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using CecFx = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EFFATK-Witness — 효과-공격 pausable화 두 좌석의 end-to-end witness.
//   * RD-W3-7  : ST13_06 <Blitz> + pre-OnAttack 훅(DNA 삭제+시큐리티 트래시). 효과-공격이 SelectAttackEffect로
//                선언되고, AttackProcess.Attack의 beforeOnAttack 지점에서 훅이 발화(삭제-선택 + IDestroySecurity)
//                한 뒤 공격이 선언 상태로 이어진다.
//   * RD-EXT2B-01 : EX11_074 [All Turns] 강제 배틀(new IBattle(this, target, null, true).Battle()). 보류 공격
//                없이 DP 비교 → 패자 삭제 / 승자 생존, 공격/시큐리티 기구 미개입.
// 하네스: DcgoMatch.CreatePumpDriven + PolicyChoiceProvider(동기 응답) — 효과는 ActivateClass.Activate 직접
//         구동(AmbientMatchContext 진입; PILOT-S4 직접-구동 선례). 동기 provider이므로 Select*/공격-타깃/삭제-
//         선택이 park 없이 인라인 해소된다(RD-W3-7 수정의 정확성 검증; park/resume 트랜스포트는 S3 펌프 기증물).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("RD-EXT2B-01 EX11_074 forced battle: winner survives, weaker foe deleted (no attack/security)", EX11074_ForcedBattle_WeakerFoeDeleted),
    ("RD-EXT2B-01 EX11_074 forced battle contrast: stronger foe wins, EX11_074 deleted (DP compare both ways)", EX11074_ForcedBattle_StrongerFoeWins),
    ("RD-EXT2B-01 EX11_074 negative: no opponent Digimon -> no battle, nothing deleted", EX11074_ForcedBattle_NoTargetNoBattle),
    ("RD-W3-7 ST13_06 Blitz: effect-attack declared + pre-OnAttack hook deletes foe(s) + trashes security", ST1306_BlitzBeforeOnAttack_HookFires),
    ("RD-W3-7 ST13_06 negative: non-Jogress hashtable -> Blitz attack declared but hook deletes nothing (gate honoured)", ST1306_BlitzBeforeOnAttack_NonJogressNoDestroy),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try
    {
        await body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st)
        {
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(12)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════ RD-EXT2B-01 (EX11_074 forced battle) ═══════════════════════════════

async Task EX11074_ForcedBattle_WeakerFoeDeleted()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(3801);
    await ReachMainWaitAsync(match);

    HeadlessEntityId vortex = Stage(match, P1, "EX11_074", ChoiceZone.BattleArea, "1:battle:Vortex", register: true);
    HeadlessEntityId foe = StageSynthetic(match, P2, "EFFATK-WEAK", dp: 2000, level: 4, "2:battle:weakfoe");
    int vortexDp = DpOf(match, vortex);
    AssertTrue(vortexDp > 2000, $"fixture sanity: Vortexdramon DP {vortexDp} exceeds the foe's 2000 (so Vortex wins)");

    // The [All Turns] battle-target select: pick the foe.
    policy.On(req => req.Candidates.Any(c => c.Id == foe), req => ChoiceResult.Select(foe), oneShot: false);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await DriveOnTappedBattle(match, vortex);

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(foe),
        $"battle loser (weaker foe) left the battle area (deleted per DP) [prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(foe), "the deleted foe is in the trash");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(vortex), "battle winner (Vortexdramon) survives on the battle area");
    AssertTrue(!match.Context.AttackController.Current.IsPending,
        "no attack was declared — an effect-forced battle runs NO attack-declaration/security machinery");
}

async Task EX11074_ForcedBattle_StrongerFoeWins()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(3802);
    await ReachMainWaitAsync(match);

    HeadlessEntityId vortex = Stage(match, P1, "EX11_074", ChoiceZone.BattleArea, "1:battle:Vortex", register: true);
    HeadlessEntityId foe = StageSynthetic(match, P2, "EFFATK-STRONG", dp: 20000, level: 7, "2:battle:strongfoe");
    int vortexDp = DpOf(match, vortex);
    AssertTrue(vortexDp < 20000, $"fixture sanity: Vortexdramon DP {vortexDp} is below the foe's 20000 (so Vortex loses)");

    policy.On(req => req.Candidates.Any(c => c.Id == foe), req => ChoiceResult.Select(foe), oneShot: false);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await DriveOnTappedBattle(match, vortex);

    AssertTrue(!ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(vortex),
        $"battle loser (Vortexdramon, lower DP) left the battle area (deleted per DP) [prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(foe), "battle winner (stronger foe) survives on the battle area");
}

async Task EX11074_ForcedBattle_NoTargetNoBattle()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(3803);
    await ReachMainWaitAsync(match);

    HeadlessEntityId vortex = Stage(match, P1, "EX11_074", ChoiceZone.BattleArea, "1:battle:Vortex", register: true);
    // No opponent Digimon on the battle area — the battle-target select has no candidates (canNoSelect) -> null -> no battle.

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await DriveOnTappedBattle(match, vortex);

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(vortex), "no target -> no battle -> Vortexdramon untouched");
    AssertEqual(0, ZoneCards(match, P2, ChoiceZone.Trash).Count, "no target -> nothing was deleted");
}

// ═══════════════════════════════ RD-W3-7 (ST13_06 Blitz pre-OnAttack hook) ═══════════════════════════════

async Task ST1306_BlitzBeforeOnAttack_HookFires()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(3701);
    await ReachMainWaitAsync(match);

    // ST13_06 with 4 digivolution sources -> count() = 4/4 = 1 (delete 1 foe + trash 1 security per DNA "arm").
    HeadlessEntityId st13 = StageWithDigivolutionSources(match, P1, "ST13_06", "1:battle:ST1306", sourceCount: 4, register: true);
    // Two cost<=20 opponent Digimon (DNA delete targets) + 3 opponent security.
    HeadlessEntityId foe1 = StageFoeWithCost(match, P2, "EFFATK-FOE1", dp: 3000, playCost: 10, "2:battle:foe1");
    HeadlessEntityId foe2 = StageFoeWithCost(match, P2, "EFFATK-FOE2", dp: 4000, playCost: 20, "2:battle:foe2");
    StageSecurity(match, P2, 3);
    int securityBefore = ZoneCards(match, P2, ChoiceZone.Security).Count;

    // opponent-side memory so <Blitz> can activate (CanActivateBlitz: MemoryController.Current <= -1).
    match.Context.MemoryController.Initialize(-1, minimum: -30, maximum: 40);

    // Effect-attack target select (SelectAttackEffect): attack the player directly (foes are unsuspended, so
    // only the player target is legal). The destroy-select (SelectPermanentEffect.Mode.Destroy) picks a foe.
    policy.On(req => req.Candidates.Any(c => c.Id.Value.EndsWith(":attack-player", StringComparison.Ordinal)),
        req => ChoiceResult.Select(req.Candidates.First(c => c.Id.Value.EndsWith(":attack-player", StringComparison.Ordinal)).Id),
        oneShot: false);
    policy.On(req => req.Candidates.Any(c => c.Id == foe1 || c.Id == foe2),
        req => ChoiceResult.Select(req.Candidates.First(c => c.Id == foe1 || c.Id == foe2).Id), oneShot: false);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var jogress = new Hashtable { ["isJogress"] = true };
    await DriveWhenDigivolving(match, st13, jogress);

    AssertTrue(match.Context.AttackController.Current.IsPending,
        $"the <Blitz> effect-attack was DECLARED (attack pending) — the SelectAttackEffect path ran to declaration [prompts:{string.Join(" | ", policy.Seen)}]");
    int foesLeft = ZoneCards(match, P2, ChoiceZone.BattleArea).Count(id => id == foe1 || id == foe2);
    AssertTrue(foesLeft < 2,
        "the pre-OnAttack hook fired: at least one cost<=20 opponent Digimon was deleted (DNA delete)");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Security).Count < securityBefore,
        "the pre-OnAttack hook fired IDestroySecurity: the opponent's security stack shrank");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(st13),
        "ST13_06 survives on the battle area after declaring the attack");
    // The attacker was suspended by the declared attack (AttackProcess.Attack suspends BEFORE the beforeOnAttack
    // hook) — the durable `suspendedByAttack` marker proves the attack sequence ran the suspend→hook ordering.
    // (ST13_06's own [All Turns] OnLoseSecurity effect then UN-suspends it — the hook's IDestroySecurity removed a
    // security card — so `isSuspended` reads false again: the correct emergent full-loop behaviour, not a miss.)
    bool suspendedByAttack = match.Context.CardInstanceRepository.TryGetInstance(st13, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue("suspendedByAttack", out object? sa) && sa is true;
    AssertTrue(suspendedByAttack,
        "the attack sequence suspended the attacker (ran past the suspend point, then the pre-OnAttack hook)");
}

async Task ST1306_BlitzBeforeOnAttack_NonJogressNoDestroy()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(3702);
    await ReachMainWaitAsync(match);

    HeadlessEntityId st13 = StageWithDigivolutionSources(match, P1, "ST13_06", "1:battle:ST1306", sourceCount: 4, register: true);
    HeadlessEntityId foe1 = StageFoeWithCost(match, P2, "EFFATK-FOE1", dp: 3000, playCost: 10, "2:battle:foe1");
    StageSecurity(match, P2, 3);
    int securityBefore = ZoneCards(match, P2, ChoiceZone.Security).Count;

    match.Context.MemoryController.Initialize(-1, minimum: -30, maximum: 40);

    policy.On(req => req.Candidates.Any(c => c.Id.Value.EndsWith(":attack-player", StringComparison.Ordinal)),
        req => ChoiceResult.Select(req.Candidates.First(c => c.Id.Value.EndsWith(":attack-player", StringComparison.Ordinal)).Id),
        oneShot: false);
    policy.On(req => req.Candidates.Any(c => c.Id == foe1),
        req => ChoiceResult.Select(foe1), oneShot: false);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    // NON-Jogress hashtable: the DNA delete/security block is gated OFF (IsJogress false) even though the hook runs.
    var notJogress = new Hashtable { ["isJogress"] = false };
    await DriveWhenDigivolving(match, st13, notJogress);

    AssertTrue(match.Context.AttackController.Current.IsPending,
        "the <Blitz> effect-attack still declared (the keyword attack is independent of the DNA arm)");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(foe1),
        "non-Jogress: the DNA delete did NOT fire (IsJogress gate honoured inside the hook) — foe survives");
    AssertEqual(securityBefore, ZoneCards(match, P2, ChoiceZone.Security).Count,
        "non-Jogress: IDestroySecurity did NOT fire — the security stack is unchanged");
}

// ═══════════════════════════════ effect drivers ═══════════════════════════════

// Direct-drives EX11_074's [All Turns] OnTappedAnyone ActivateClass (unsuspend arm skipped: staged unsuspended).
async Task DriveOnTappedBattle(DcgoMatch match, HeadlessEntityId vortexId)
{
    var card = new Cec.CardSource(match.Context, vortexId, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX11.Green.EX11_074();
    var activate = (CecFx.ActivateClass)effect.CardEffects(Cec.EffectTiming.OnTappedAnyone, card).First();
    await activate.Activate(new Hashtable());
}

// Direct-drives ST13_06's [When Digivolving] ActivateClass with a Jogress-marked hashtable.
async Task DriveWhenDigivolving(DcgoMatch match, HeadlessEntityId st13Id, Hashtable hashtable)
{
    var card = new Cec.CardSource(match.Context, st13Id, P1);
    var effect = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST13.Red.ST13_06();
    var activate = (CecFx.ActivateClass)effect.CardEffects(Cec.EffectTiming.WhenDigivolving, card)
        .First(e => e is CecFx.ActivateClass);
    await activate.Activate(hashtable);
}

// ═══════════════════════════════ harness (EXEMPLAR-T2B 템플릿) ═══════════════════════════════

PlayerDeckSetup[] MonoDecks(string p1Number, string p2Number) => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId(p1Number), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId(p2Number), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        MonoDecks("BT1_028", "BT1_028"), firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
    MatchConfig config = MatchConfig.Create(new[] { P1, P2 }, randomSeed: seed, setup: setup);

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    await match.InitializeAsync(config);
    return (match, policy);
}

async Task ReachMainWaitAsync(DcgoMatch match)
{
    await StepOnceAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
}

static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice()
    && !match.IsTerminal();

async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.BreedingDecision
                || match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else
        {
            await StepOnceAsync(match);
        }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"drive did not reach the expected state — phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} terminal:{match.IsTerminal()}");
    }
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser)
            .FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }

    if (action is null)
    {
        throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    }

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

// 실카드 스테이징: def id = 카드번호; 인스턴스만 만들어 이동. register → 배틀에어리어 효과원.
HeadlessEntityId Stage(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone, string instanceId,
    bool register = false)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId(cardNumber);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? existing) || existing is null)
    {
        throw new InvalidOperationException($"definition {cardNumber} not found in the loaded card database");
    }

    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    if (register)
    {
        HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    }

    return id;
}

// 합성 디지몬(dp/level만) — 배틀에어리어 배치.
HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, number, meta, CardType: "Digimon"));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level, ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

// 합성 디지몬 + PlayCost(HasPlayCost/GetCostItself<=20 게이트용).
HeadlessEntityId StageFoeWithCost(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int playCost, string instanceId)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 };
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, number, meta, CardType: "Digimon", PlayCost: playCost));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4, ["isSuspended"] = false }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

// 실카드 + N개의 진화원(sourceIds) — DigivolutionCards.Count = N.
HeadlessEntityId StageWithDigivolutionSources(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, string instanceId,
    int sourceCount, bool register = false)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId(cardNumber);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? existing) || existing is null)
    {
        throw new InvalidOperationException($"definition {cardNumber} not found in the loaded card database");
    }

    var sourceIds = new List<string>();
    for (int i = 0; i < sourceCount; i++)
    {
        var srcDef = new HeadlessEntityId($"DEF:SRC:{owner.Value}:{i}");
        ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(srcDef, $"SRC{i}", $"SRC{i}",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 1000, ["level"] = 3 }, CardType: "Digimon"));
        var srcId = new HeadlessEntityId($"{instanceId}:src:{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, srcDef, owner,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
        sourceIds.Add(srcId.Value);
    }

    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["isSuspended"] = false,
            [DigivolutionStackReader.SourceIdsKey] = sourceIds.ToArray(),
        }));
    ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea)).GetAwaiter().GetResult();

    if (register)
    {
        HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    }

    return id;
}

void StageSecurity(DcgoMatch match, HeadlessPlayerId owner, int count)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:SEC:{owner.Value}");
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, "SECCARD", "SECCARD",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 1000, ["level"] = 3 }, CardType: "Digimon"));
    for (int i = 0; i < count; i++)
    {
        var id = new HeadlessEntityId($"{owner.Value}:sec:{i}");
        ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Security)).GetAwaiter().GetResult();
    }
}

static int DpOf(DcgoMatch match, HeadlessEntityId id)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) || rec is null)
    {
        return -1;
    }

    return new Cec.Permanent(match.Context, id, rec.OwnerId).DP;
}

static bool IsSuspendedMeta(DcgoMatch match, HeadlessEntityId cardId) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null
    && record.Metadata.TryGetValue("isSuspended", out object? raw) && raw is true;

static IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone) =>
    match.Context.ZoneMover is IZoneStateReader zones ? zones.GetCards(player, zone) : Array.Empty<HeadlessEntityId>();

static void AssertTrue(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException($"Assertion failed: {message}"); }
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Assertion failed: {message} (expected {expected}, got {actual})");
    }
}

// ═══════════════════════════════ providers/context (EXEMPLAR-T2B 사본) ═══════════════════════════════

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

    public List<string> Seen { get; } = new();

    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        Seen.Add($"{request.Type}:'{request.Message}'x{request.Candidates.Count}");
        for (int i = 0; i < _handlers.Count; i++)
        {
            (Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot) = _handlers[i];
            if (applies(request))
            {
                ChoiceResult result = answer(request);
                result.ThrowIfInvalid(request);
                if (oneShot)
                {
                    _handlers.RemoveAt(i);
                }

                return Task.FromResult(result);
            }
        }

        return _fallback.ChooseAsync(request, cancellationToken);
    }
}

static class ContextFactory
{
    public static EngineContext CreateWithProvider(IChoiceProvider provider, int randomSeed)
    {
        var randomSource = new GameRandomSource(randomSeed);
        var cardInstanceRepository = new InMemoryCardInstanceRepository();
        var logSink = new NullLogSink();
        var zoneMover = new InMemoryZoneMover(randomSource);
        var memoryController = new InMemoryHeadlessMemoryController();
        var gameEventQueue = new GameEventQueue();
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, gameEventQueue,
                    currentTurnPlayer: () => selfRef?.TurnController.Current.TurnPlayerId,
                    context: selfRef),
                strictUnbound: false));

        var choiceController = new InMemoryHeadlessChoiceController();
        var context = new EngineContext(
            provider,
            randomSource,
            new CardDatabase(),
            cardInstanceRepository,
            zoneMover,
            new InMemoryRuleQueryService(),
            new InMemoryHeadlessTurnController(),
            choiceController,
            new InMemoryHeadlessAttackController(),
            memoryController,
            logSink,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(),
            effectScheduler,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
