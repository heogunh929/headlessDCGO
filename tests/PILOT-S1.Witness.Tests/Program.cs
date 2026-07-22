using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using ScriptSelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 witness 스위트 — Sonnet 파일럿 S1 10장, 카드당 1개 이상(EXEMPLAR-T1.Witness.Tests 템플릿 복제).
// 표준 템플릿: DcgoMatch.CreatePumpDriven + 에이전트 액션 구동(ApplyActionAsync); 효과-내부 Select*/Optional
// 프롬프트는 PolicyChoiceProvider 좌석으로 응답.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("EX8_030 W1: Lv.2 [NSo] 위 코스트 0 대체진화 → 착지 + 메모리 무변", EX8030_AltDigivolveLevel2NSoCostZero),
    ("BT9_009 W1: [Guilmon] 위 코스트 0 대체진화 → [When Digivolving] 3000DP 이하 상대 디지몬 삭제", BT9009_AltDigivolveThenDeleteLowDp),
    ("BT9_103 W1: [Main] 발화 → CannotAddSecurityClass가 UntilOpponentTurnEndEffects에 등재", BT9103_MainGrantsCannotAddSecurity),
    ("BT2_082 W1: [When Attacking] 발화 → [Diaboromon] 토큰 무료 플레이", BT2082_WhenAttackingPlaysDiaboromonToken),
    ("BT9_111 W1: [Alphamon]+[Ouryumon]원 위 코스트 3 대체진화 → [When Digivolving] 상대 최고코스트 디지몬 삭제", BT9111_AltDigivolveThenDeleteHighestCost),
    ("EX8_037 W1: Lv.6 [Sakuyamon] 비X항체 위 코스트 1 대체진화(+진화원 Sakuyamon) → [Uka-no-Mitama] 토큰 플레이", EX8037_AltDigivolveThenPlaysUkaNoMitama),
    ("EX8_028 W1: 등록-검증 — Iceclad/Barrier/대체진화 3효과가 실제 구성 타입으로 등재", EX8028_RegistersIcecladBarrierAltDigivolve),
    ("EX8_068 W1: [Security] 발화 → [DS] Lv.5 이하 디지몬 무료 플레이", EX8068_SecurityPlaysDsDigimonFree),
    ("EX8_068 W2: [Main] 발화 → 바닥 시큐리티 1장 손패로 + EX8_068 자신이 바닥 시큐리티(앞면) 배치 (RD-P6C3-B1 UN-STOP)", EX8068_MainReplacesBottomSecurityWithSelf),
    ("BT18_034 W1: [Start of Your Main Phase] 손패 1장 트래시→\"Discard\" 선택 → 상대 시큐리티 1장 트래시", BT18034_StartOfMainDiscardThenDestroySecurity),
    ("BT18_098 W1: [Main] 발화 → 자기 시큐리티 1장 트래시 + 상대 디지몬 DP-6000 + 시큐리티≤2면 바닥 배치", BT18098_MainTrashSecurityChangeDpAndReplace),
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
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(8)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ EX8_030 ═══════════════════════════════════

async Task EX8030_AltDigivolveLevel2NSoCostZero()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId host = StageSynthetic(match, P1, "EXT1-NSO", dp: 3000, level: 2, "1:battle:nso", traits: new[] { "NSo" });
    HeadlessEntityId ex = Stage(match, P1, "EX8_030", ChoiceZone.Hand, "1:hand:EX8030");
    int memBefore = MemoryFor(match, P1);

    LegalAction digivolve = RequireLane(match, P1, HeadlessActionTypes.Digivolve, ex,
        $"AddSelfDigivolutionRequirementStaticEffect must open a cost-0 digivolve lane onto a level-2 [NSo] host [debug lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}]");
    _ = digivolve;
    LegalAction onto = FindDigivolveLane(match, P1, ex, host)
        ?? throw new InvalidOperationException("expected a digivolve lane EX8_030 -> the level-2 [NSo] host");
    await ApplyAsync(match, onto);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(ex) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(ex),
        "EX8_030 landed on the battle area via the alternate (cost-0) digivolution requirement");
    AssertTrue(SourcesOf(match, ex).Contains(host), "the level-2 [NSo] host threaded as a digivolution source");
    AssertEqual(memBefore, MemoryFor(match, P1), "digivolution cost 0 — no memory paid");
}

// ═══════════════════════════════════ BT9_009 ═══════════════════════════════════

async Task BT9009_AltDigivolveThenDeleteLowDp()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId host = StageSynthetic(match, P1, "EXT1-GUIL", dp: 1000, level: 2, "1:battle:guilmon", name: "Guilmon");
    HeadlessEntityId bt9009 = Stage(match, P1, "BT9_009", ChoiceZone.Hand, "1:hand:BT9009");
    HeadlessEntityId lowDp = StageSynthetic(match, P2, "EXT1-LOW", dp: 2000, level: 3, "2:battle:low");
    HeadlessEntityId highDp = StageSynthetic(match, P2, "EXT1-HIGH", dp: 9000, level: 6, "2:battle:high");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == lowDp),
        req => ChoiceResult.Select(lowDp));

    LegalAction onto = FindDigivolveLane(match, P1, bt9009, host)
        ?? throw new InvalidOperationException("expected a cost-0 digivolve lane BT9_009 -> the [Guilmon] host");
    await ApplyAsync(match, onto);
    await DriveUntilAsync(match, m => ZoneCards(m, P2, ChoiceZone.Trash).Contains(lowDp) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(bt9009), "BT9_009 landed on the battle area");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(lowDp),
        "[When Digivolving]: the 3000-DP-or-less opponent Digimon was deleted");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(highDp),
        "negative: the 9000 DP opponent Digimon (above the 3000 threshold) was NOT targeted/deleted");
}

// ═══════════════════════════════════ BT9_103 ═══════════════════════════════════

async Task BT9103_MainGrantsCannotAddSecurity()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2301, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // BT9_103 is Black/Purple — its color requirement (OptionColorRequirement.Matches) needs a Purple-colored
    // field permanent; stage one so the [Main] lane actually opens.
    StageSynthetic(match, P1, "EXT1-PURP", dp: 3000, level: 3, "1:battle:purple", colors: new[] { "Purple" });
    HeadlessEntityId bt9103 = Stage(match, P1, "BT9_103", ChoiceZone.Hand, "1:hand:BT9103");
    int grantsBefore = new Cec.Player(match.Context, P1).UntilOpponentTurnEndEffects.Count;

    LegalAction option = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, bt9103,
        "BT9_103's own [Main] OptionSkill must be offered (CanTriggerOptionMainEffect)");
    await ApplyAsync(match, option);
    await DriveUntilAsync(match, m => !m.HasPendingChoice() || m.IsTerminal());

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    int grantsAfter = new Cec.Player(match.Context, P1).UntilOpponentTurnEndEffects.Count;
    AssertTrue(grantsAfter > grantsBefore,
        $"CannotAddSecurityClass: the [Main] body registered a new UntilOpponentTurnEndEffects grant (before:{grantsBefore} after:{grantsAfter})");
}

// ═══════════════════════════════════ BT2_082 ═══════════════════════════════════

async Task BT2082_WhenAttackingPlaysDiaboromonToken()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId host = Stage(match, P1, "BT2_082", ChoiceZone.BattleArea, "1:battle:BT2082", register: true);

    // [When Attacking] isOptional=true — the token-play prompt is an OptionalEffect seat.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    LegalAction attack = RequireLane(match, P1, HeadlessActionTypes.DeclareAttack, host, "BT2_082 must be able to declare a (direct) attack");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Any(id => CardNumberOf(m, id) == "BT2-082-token")
        || (!m.HasPendingChoice() && !m.Context.AttackController.Current.IsPending) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Any(id => CardNumberOf(match, id) == "BT2-082-token"),
        "PlayDiaboromonToken: the [Diaboromon] token entered the battle area during the attack");
}

// ═══════════════════════════════════ BT9_111 ═══════════════════════════════════

async Task BT9111_AltDigivolveThenDeleteHighestCost()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ouryumonSource = StageSynthetic(match, P1, "EXT1-OURY", dp: 3000, level: 3, "1:under:oury",
        zone: ChoiceZone.None, name: "Ouryumon");
    HeadlessEntityId host = StageSynthetic(match, P1, "EXT1-ALPHA", dp: 8000, level: 6, "1:battle:alpha", name: "Alphamon");
    SetSources(match, host, ouryumonSource);
    HeadlessEntityId bt9111 = Stage(match, P1, "BT9_111", ChoiceZone.Hand, "1:hand:BT9111");
    HeadlessEntityId lowCost = StageSynthetic(match, P2, "EXT1-LC", dp: 2000, level: 3, "2:battle:lowcost", playCost: 3);
    HeadlessEntityId highCost = StageSynthetic(match, P2, "EXT1-HC", dp: 6000, level: 5, "2:battle:highcost", playCost: 8);
    int memBefore = MemoryFor(match, P1);

    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    LegalAction onto = FindDigivolveLane(match, P1, bt9111, host)
        ?? throw new InvalidOperationException(
            $"expected a cost-3 digivolve lane BT9_111 -> Alphamon(+Ouryumon source) — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, onto);
    await DriveUntilAsync(match, m => ZoneCards(m, P2, ChoiceZone.Trash).Contains(highCost) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(bt9111), "BT9_111 landed on the battle area");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(highCost),
        "[When Digivolving]: the highest-play-cost opponent Digimon (cost 8) was deleted");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(lowCost),
        "negative: the lower-play-cost opponent Digimon (cost 3, not the max) survived");
    AssertEqual(memBefore - 3, MemoryFor(match, P1), "the alternative digivolution cost 3 was paid");
}

// ═══════════════════════════════════ EX8_037 ═══════════════════════════════════

async Task EX8037_AltDigivolveThenPlaysUkaNoMitama()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId sakuyaSource = StageSynthetic(match, P1, "EXT1-SAKS", dp: 3000, level: 3, "1:under:sakusrc",
        zone: ChoiceZone.None, name: "Sakuyamon");
    HeadlessEntityId host = StageSynthetic(match, P1, "EXT1-SAKU", dp: 9000, level: 6, "1:battle:sakuya", name: "Sakuyamon");
    SetSources(match, host, sakuyaSource);
    HeadlessEntityId ex = Stage(match, P1, "EX8_037", ChoiceZone.Hand, "1:hand:EX8037");

    LegalAction onto = FindDigivolveLane(match, P1, ex, host)
        ?? throw new InvalidOperationException(
            $"expected a cost-1 digivolve lane EX8_037 -> Lv.6 [Sakuyamon] (non-X-Antibody) — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, onto);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Any(id => CardNumberOf(m, id) == "EX8-037-token")
        || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(ex), "EX8_037 landed on the battle area");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Any(id => CardNumberOf(match, id) == "EX8-037-token"),
        "[When Digivolving]: the [Uka-no-Mitama] token entered the battle area (a [Sakuyamon] digivolution source was present)");
}

// ═══════════════════════════════════ EX8_028 ═══════════════════════════════════

async Task EX8028_RegistersIcecladBarrierAltDigivolve()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2701, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX8_028", ChoiceZone.BattleArea, "1:battle:EX8028", register: true);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, ex, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Blue.EX8_028();

    List<Cec.ICardEffect> noneEffects = effectInstance.CardEffects(Cec.EffectTiming.None, card);
    AssertTrue(noneEffects.Any(e => e.GetType().Name == "AddDigivolutionRequirementClass"),
        $"AddSelfDigivolutionRequirementStaticEffect must register under timing=None — got [{string.Join(",", noneEffects.Select(e => e.GetType().Name))}]");
    AssertTrue(noneEffects.Any(e => e.GetType().Name == "IcecladClass"),
        $"IcecladSelfStaticEffect must register under timing=None — got [{string.Join(",", noneEffects.Select(e => e.GetType().Name))}]");

    List<Cec.ICardEffect> barrierEffects = effectInstance.CardEffects(Cec.EffectTiming.WhenPermanentWouldBeDeleted, card);
    AssertTrue(barrierEffects.Any(e => e.EffectName == "Barrier"),
        $"BarrierSelfEffect must register under timing=WhenPermanentWouldBeDeleted (BarrierEffect returns an " +
        $"ActivateClass named \"Barrier\") — got [{string.Join(",", barrierEffects.Select(e => $"{e.GetType().Name}:{e.EffectName}"))}]");

    List<Cec.ICardEffect> digivolvingEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    AssertEqual(2, digivolvingEffects.Count,
        $"[When Digivolving] free-play + [When Digivolving]-OPT unsuspend both register under OnEnterFieldAnyone — got [{string.Join(",", digivolvingEffects.Select(e => e.EffectName))}]");
}

// ═══════════════════════════════════ EX8_068 ═══════════════════════════════════

async Task EX8068_SecurityPlaysDsDigimonFree()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2801, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    HeadlessEntityId sec = Stage(match, P1, "EX8_068", ChoiceZone.Security, "1:sec:EX8068");
    HeadlessEntityId dsDigimon = StageSynthetic(match, P1, "EXT1-DS", dp: 3000, level: 4, "1:hand:dsdigi",
        zone: ChoiceZone.Hand, traits: new[] { "DS" });
    HeadlessEntityId attacker = StageSynthetic(match, P2, "EXT1-ATK", dp: 3000, level: 4, "2:battle:atk");

    policy.On(req => req.Candidates.Any(c => c.Id == dsDigimon), req => ChoiceResult.Select(dsDigimon));

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    LegalAction attack = RequireLane(match, P2, HeadlessActionTypes.DeclareAttack, attacker, "P2 direct attack");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(dsDigimon)
        || AtMainWaitOf(m, P2) || m.IsTerminal());

    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Security).Contains(sec), "the flipped EX8_068 left the security stack");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(dsDigimon),
        "[Security]: the level-5-or-lower [DS] Digimon was played from hand without paying the cost — " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// EX8_068 W2 — the [Main] OptionSkill (ReplaceBottomSecurityWithFaceUpOptionMainEffect, AS-IS :83) whose body
// was the RD-P6C3-B1 STOP seat. Drives the real ActivateOption lane end-to-end: the bottom security card is
// added to hand, then EX8_068 itself is placed face-up as the new bottom security card (AS-IS
// CardEffectFactory.cs:645 ReplaceBottomSecurityWithFaceUpOptionEffect).
async Task EX8068_MainReplacesBottomSecurityWithSelf()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2802, MonoDecks("BT1_028", "BT1_028"));
    EngineContext ctx = match.Context;
    await ReachMainWaitAsync(match);
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);

    // EX8_068 is a Blue Option (playCost 2) — its color requirement (OptionColorRequirement.Matches) opens the
    // [Main] lane when P1 controls a Blue field permanent (BT9_103 recipe). Give ample memory to afford the play.
    StageSynthetic(match, P1, "EXT1-BLUE", dp: 3000, level: 3, "1:battle:blue", colors: new[] { "Blue" });
    // Two distinct security cards so "bottom" (last) vs "top" (first) is observable.
    StageSynthetic(match, P1, "EXT1-SECTOP", dp: 1000, level: 3, "1:sec:top", zone: ChoiceZone.Security);
    StageSynthetic(match, P1, "EXT1-SECBOT", dp: 2000, level: 4, "1:sec:bot", zone: ChoiceZone.Security);
    HeadlessEntityId ex8068 = Stage(match, P1, "EX8_068", ChoiceZone.Hand, "1:hand:EX8068main");
    ctx.MemoryController.Set(10);

    HeadlessEntityId bottomId, topId;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx))
    {
        var secBefore = new Cec.Player(ctx, P1).SecurityCards;
        AssertEqual(2, secBefore.Count, "staging produced exactly 2 security cards");
        bottomId = secBefore.Last().InstanceId;
        topId = secBefore.First().InstanceId;
    }

    LegalAction option = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, ex8068,
        "EX8_068's own [Main] OptionSkill must be offered (CanTriggerOptionMainEffect + Blue color enabler)");
    await ApplyAsync(match, option);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.Security).Contains(ex8068) || m.IsTerminal());

    // (1) the old bottom security card was added to the hand; (2) it left the security stack.
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Hand).Contains(bottomId),
        "the bottom security card was added to P1's hand");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Security).Contains(bottomId),
        "the old bottom security card left the security stack");
    // (3) the top security card is untouched (only the BOTTOM was pulled).
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Security).Contains(topId),
        "the top security card was NOT touched (Bottom variant pulls the bottom only)");
    // (4) EX8_068 itself is now sitting in security, and NOT in hand/trash.
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Security).Contains(ex8068),
        "EX8_068 placed itself as a security card (option did not go to trash)");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(ex8068),
        "EX8_068 is no longer in hand");
    // (5) net security count unchanged (removed 1 bottom, added EX8_068 as new bottom).
    AssertEqual(2, Count(match, P1, ChoiceZone.Security),
        "security count unchanged: -1 (bottom to hand) +1 (EX8_068 placed) = net 2");

    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(ctx))
    {
        // (6) EX8_068 is the BOTTOM (last) security card, placed FACE UP (faceUp:true).
        var secAfter = new Cec.Player(ctx, P1).SecurityCards;
        AssertEqual(ex8068.Value, secAfter.Last().InstanceId.Value,
            "EX8_068 is the new BOTTOM (last) security card");
        AssertEqual(topId.Value, secAfter.First().InstanceId.Value,
            "the original top security card is still on top");
        AssertTrue(SecurityFaceState.IsFaceUpInSecurity(ctx, ex8068),
            "EX8_068 was placed FACE UP as the bottom security card");
    }
}

// ═══════════════════════════════════ BT18_034 ═══════════════════════════════════

async Task BT18034_StartOfMainDiscardThenDestroySecurity()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 2901, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    Stage(match, P1, "BT18_034", ChoiceZone.BattleArea, "1:battle:BT18034", register: true);
    Stage(match, P1, "BT1_028", ChoiceZone.Hand, "1:hand:filler");
    int p2SecBefore = Count(match, P2, ChoiceZone.Security);
    AssertTrue(p2SecBefore >= 1, "P2 must be dealt at least 1 security card (pump StartGame deal) for the Discard branch to be observable");

    // Discard prompt (SelectHandEffect Mode.Discard, canNoSelect:true) — select the filler card (not skip).
    policy.On(req => req.Type == ChoiceType.Card || req.Type == ChoiceType.HandCard,
        req => req.Candidates.Any(c => c.IsSelectable) ? ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id) : PolicyChoiceProvider.Fallback(req));
    // ModeChoice ("Discard" vs "Not Discard") — pick "Discard" (the IDestroySecurity branch).
    policy.On(req => req.Type == ChoiceType.ModeChoice, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.Label.Contains("Discard", StringComparison.Ordinal) && !c.Label.Contains("Not", StringComparison.Ordinal)).Id));

    // [Start of Your Main Phase] is a mandatory (isOptional:false) trigger — pass to the NEXT of P1's own turns
    // so the phase-start pump re-fires it fresh (avoids racing the CURRENT bootstrap Main window).
    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    await PassTurnAsync(match, P2);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1) || Count(m, P2, ChoiceZone.Security) < p2SecBefore || m.IsTerminal());

    AssertTrue(Count(match, P2, ChoiceZone.Security) < p2SecBefore,
        $"[Start of Your Main Phase]: discarding a hand card opened the \"Discard\" branch — the opponent's top " +
        $"security card was destroyed (before:{p2SecBefore} after:{Count(match, P2, ChoiceZone.Security)}) " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT18_098 ═══════════════════════════════════

async Task BT18098_MainTrashSecurityChangeDpAndReplace()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 3001, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    // Exactly 2 security cards left after ClearZone (0) + 2 staged: post-trash count is 1 (<=2) -> the
    // bottom-security-placement branch of [Main] fires.
    StageSynthetic(match, P1, "EXT1-SEC0", dp: 1000, level: 3, "1:sec:0", zone: ChoiceZone.Security);
    // BT18_098 is Yellow/Red — its OWN IgnoreColorConditionClass (ported half) waives the color requirement
    // while P1 controls a Yellow Digimon with the [Data]/[Witchelny] trait; stage one instead of also
    // supplying Red.
    StageSynthetic(match, P1, "EXT1-YDATA", dp: 3000, level: 3, "1:battle:ydata", colors: new[] { "Yellow" }, traits: new[] { "Data" });
    HeadlessEntityId bt18098 = Stage(match, P1, "BT18_098", ChoiceZone.Hand, "1:hand:BT18098");
    HeadlessEntityId target = StageSynthetic(match, P2, "EXT1-TGT", dp: 7000, level: 5, "2:battle:tgt");

    // BT18_098's [Main] ActivateClass is registered isOptional:true ("you may") — an OptionalEffect prompt
    // ("Will you use ...?") gates the whole body before IDestroySecurity/SelectPermanent run.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == target),
        req => ChoiceResult.Select(target));

    LegalAction option = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, bt18098,
        $"BT18_098's own [Main] OptionSkill must be offered (>=1 security + CanTriggerOptionMainEffect) — " +
        $"lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    int secBefore = Count(match, P1, ChoiceZone.Security);
    await ApplyAsync(match, option);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.Security).Contains(bt18098) || m.IsTerminal());

    AssertEqual(secBefore - 1 + 1, Count(match, P1, ChoiceZone.Security),
        "IDestroySecurity trashed the top security card (-1), then (security<=2) BT18_098 itself was placed as the new bottom security card (+1) — net unchanged count");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Security).Contains(bt18098),
        "BT18_098 itself is now sitting in P1's security stack (the <=2-security replacement branch fired)");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var targetPermanent = new Cec.Permanent(match.Context, target, P2);
    AssertEqual(1000, targetPermanent.DP, "ChangeDigimonDP: the targeted opponent Digimon's DP dropped by 6000 (7000 -> 1000) until the end of their turn");
}

// ═══════════════════════════════════ harness ═══════════════════════════════════

PlayerDeckSetup[] MonoDecks(string p1Number, string p2Number) => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId(p1Number), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId(p2Number), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewPilotMatchAsync(int seed, PlayerDeckSetup[] decks)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        decks, firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
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
            $"drive did not reach the expected state — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} " +
            $"player:{t.TurnPlayerId} choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} " +
            $"pending:{match.HasPendingChoice()} controllerState:{match.Context.ChoiceController.Current.IsPending}/{match.Context.ChoiceController.Current.IsResolved} " +
            $"terminal:{match.IsTerminal()} memory:{match.Context.MemoryController.Current.Current} " +
            $"lanes:[{string.Join(", ", Legal(match, t.TurnPlayerId ?? default).Select(a => a.ActionType))}]");
    }
}

async Task PassTurnAsync(DcgoMatch match, HeadlessPlayerId player)
{
    LegalAction pass = Legal(match, player).First(a => a.ActionType == HeadlessActionTypes.Pass);
    await ApplyAsync(match, pass);
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

    await ApplyAsync(match, action);
}

async Task ApplyAsync(DcgoMatch match, LegalAction action)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

static IReadOnlyList<LegalAction> Legal(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return match.GetLegalActions(player);
}

static LegalAction? FindLane(DcgoMatch match, HeadlessPlayerId player, string actionType, HeadlessEntityId cardId)
{
    return Legal(match, player).FirstOrDefault(a => a.ActionType == actionType && ActionCardIds(a).Contains(cardId));
}

static LegalAction RequireLane(DcgoMatch match, HeadlessPlayerId player, string actionType, HeadlessEntityId cardId, string why)
{
    return FindLane(match, player, actionType, cardId)
        ?? throw new InvalidOperationException(
            $"expected a {actionType} lane for {cardId.Value} — {why}. listed: " +
            string.Join(", ", Legal(match, player).Select(a => $"{a.ActionType}({string.Join('/', ActionCardIds(a).Select(i => i.Value))})")));
}

static LegalAction? FindDigivolveLane(DcgoMatch match, HeadlessPlayerId player, HeadlessEntityId cardId, HeadlessEntityId targetId)
{
    return Legal(match, player).FirstOrDefault(a =>
        a.ActionType == HeadlessActionTypes.Digivolve
        && a.Parameters.TryGetValue(HeadlessActionParameterKeys.CardId, out object? c) && c is HeadlessEntityId cid && cid == cardId
        && a.Parameters.TryGetValue(HeadlessActionParameterKeys.TargetCardId, out object? t) && t is HeadlessEntityId tid && tid == targetId);
}

static IEnumerable<HeadlessEntityId> ActionCardIds(LegalAction action)
{
    foreach (string key in new[]
    {
        HeadlessActionParameterKeys.CardId,
        HeadlessActionParameterKeys.AttackerId,
        HeadlessActionParameterKeys.TargetCardId,
    })
    {
        if (action.Parameters.TryGetValue(key, out object? raw) && raw is HeadlessEntityId id)
        {
            yield return id;
        }
    }
}

// 실카드 스테이징: cards.json 로더가 이미 def를 넣었으므로(def id = 카드번호) 인스턴스만 만들어 이동.
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
        Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    }

    return id;
}

// 합성 픽스처 카드(R4S3b StageBattleDigimon 관례 확장): def 업서트 + 인스턴스 + 존 이동. playCost 추가
// (AS-IS CardSource.HasPlayCost/GetCostItself 시나리오 — EXEMPLAR-T1 원본은 미설정이었음).
HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null, int? playCost = null, string[]? colors = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
    }

    if (colors is { Length: > 0 })
    {
        meta["colors"] = colors;
    }

    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name ?? number, meta, CardType: cardType, PlayCost: playCost));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level, ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

static async Task ClearZoneAsync(DcgoMatch match, HeadlessPlayerId owner, ChoiceZone from, ChoiceZone to)
{
    foreach (HeadlessEntityId id in ZoneCards(match, owner, from).ToArray())
    {
        await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, from, to));
    }
}

static void SetSources(DcgoMatch match, HeadlessEntityId hostId, params HeadlessEntityId[] sourceIds)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing instance {hostId.Value}");
    }

    var meta = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
    {
        [DigivolutionStackReader.SourceIdsKey] = sourceIds.Select(id => id.Value).ToArray(),
    };
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

static IReadOnlyList<HeadlessEntityId> SourcesOf(DcgoMatch match, HeadlessEntityId hostId)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    if (!match.Context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? record) || record is null)
    {
        return Array.Empty<HeadlessEntityId>();
    }

    var host = new Cec.Permanent(match.Context, hostId, record.OwnerId);
    return host.DigivolutionCards.Select(cs => cs.InstanceId).ToArray();
}

static string CardNumberOf(DcgoMatch match, HeadlessEntityId cardId)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null
        || !match.Context.CardRepository.TryGetCard(record.DefinitionId, out CardRecord? def) || def is null)
    {
        return string.Empty;
    }

    return def.CardNumber;
}

static int MemoryFor(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.Player(match.Context, player).MemoryForPlayer;
}

static int Count(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones ? zones.GetCards(player, zone).Count : -1;
}

static IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone)
{
    return match.Context.ZoneMover is IZoneStateReader zones
        ? zones.GetCards(player, zone)
        : Array.Empty<HeadlessEntityId>();
}

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

// ═══════════════════════════════ providers/context ═══════════════════════════════

/// <summary>에이전트 좌석: 술어-매칭 스크립트 답변 + ScriptedChoiceProvider 동일 폴백(검증 포함).</summary>
sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

    public static ChoiceResult Fallback(ChoiceRequest request)
        => new ScriptedChoiceProvider().ChooseAsync(request).GetAwaiter().GetResult();

    /// <summary>진단용: 이 좌석이 응답한 프롬프트 요약(타입/메시지/후보수).</summary>
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

/// <summary>EngineContext.CreateDefault의 1:1 재현 — provider 좌석만 교체(그 외 배선 동일).</summary>
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
