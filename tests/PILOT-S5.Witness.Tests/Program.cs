using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using Cfx = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using Script = HeadlessDCGO.Engine.Assets.Scripts.Script;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S5 witness 스위트 — Sonnet 트랜치 S5 10장, 카드당 1개 이상(PILOT-S1~S4.Witness.Tests 템플릿 복제).
// 표준 템플릿: DcgoMatch.CreatePumpDriven + 에이전트 액션 구동(ApplyActionAsync) — 실 레인이 있으면 그걸 구동;
// [On Play]/[When Digivolving] 계열은 BT10_111/P_198 PILOT-S4 선례와 동형으로 ActivateClass.CanUse/CanActivate/
// Activate 공개 API를 직접 호출(펌프 과-캐스케이드로 UntilEachTurnEndEffects류 부여가 같은 스텝 안에서 쓸려
//나가는 현상 회피).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT9_013 W1: ESS CanAttackTargetDefendingPermanentClass — [OmniShoutmon] 진화원 보유 시 상대 언서스펜드 디지몬 공격 허용, 서스펜드면 불허", BT9013_CanAttackUnsuspendedWithSource),
    ("BT16_052 W1: [When Digivolving] 직접 발화 → [KoHagurumon] 토큰이 자기 배틀에어리어에 착지", BT16052_WhenDigivolvingPlaysToken),
    ("ST20_15 W1: [Security] 직접 발화 → 손패 테이머 1장이 코스트 없이 배틀에어리어에 착지", ST2015_SecurityPlaysTamerFree),
    ("BT12_044 W1: [When Digivolving] 직접 발화 → 상대 약체 디지몬이 Security Attack -2 부여받음(HasSecurityAttackChanges)", BT12044_WhenDigivolvingGrantsSAttackDebuff),
    ("BT10_084 W1: [On Play] 직접 발화 → 트래시 [BagraArmy] Lv4 이하 디지몬 최대 2장 무상 플레이 + Blocker 부여", BT10084_OnPlayPlaysTrashDigimonAndGrantsBlocker),
    ("BT23_081 W1: [On Play] 직접 발화 → 손패 [Hudie] 플레이코스트5 이하 디지몬 1장 무상 플레이", BT23081_OnPlayPlaysHudieDigimonFree),
    ("AD1_006 W1: [On Play] 직접 발화(공유 OP/WD/WA 몸통) → 상대 약체 디지몬 덱밑 이동 + 자기 언서스펜드", AD1006_OnPlayBottomDecksAndUnsuspends),
    ("ST22_08 W1: [Security] 직접 발화 → 상대 최소DP 디지몬 삭제 + 이 카드가 손패로", ST2208_SecurityDeletesMinDpAndReturnsToHand),
    ("BT20_079 W1: [On Play] 직접 발화 → 상대 최소레벨 디지몬 삭제", BT20079_OnPlayDeletesMinLevel),
    ("BT23_057 W1: 트래시 3장([Huckmon]류) 존재 시 [None] Play Cost -5 ChangeCostClass의 CanUse가 true(EffectList(BeforePayCost) sink 조회 경유)", BT23057_ReducedCostArmActivatesWithThreeTrashMatches),
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
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(20)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ BT9_013 ═══════════════════════════════════

async Task BT9013_CanAttackUnsuspendedWithSource()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt9013 = Stage(match, P1, "BT9_013", ChoiceZone.BattleArea, "1:battle:BT9013", register: true);
    HeadlessEntityId source = StageSynthetic(match, P1, "EXS5-OMNI", dp: 0, level: 0, "1:src:omni", name: "OmniShoutmon", zone: ChoiceZone.None, cardType: "Option");
    SetSources(match, bt9013, source);
    HeadlessEntityId oppDigimon = StageSynthetic(match, P2, "EXS5-OPP1", dp: 3000, level: 3, "2:battle:opp1");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt9013, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Red.BT9_013();
    List<Cec.ICardEffect> noneEffects = effectInstance.CardEffects(Cec.EffectTiming.None, card);
    var ess = (Cfx.CanAttackTargetDefendingPermanentClass)noneEffects.First(e => e.EffectName == "Can attack to unsuspended Digimon");

    AssertTrue(ess.CanUse(new System.Collections.Hashtable()),
        "CanUse is true — [OmniShoutmon]-named digivolution source present and it is the owner's turn");

    var attacker = new Cec.Permanent(match.Context, bt9013, P1);
    var defender = new Cec.Permanent(match.Context, oppDigimon, P2);

    AssertTrue(ess.CanAttackTargetDefendingPermanent(attacker, defender, ess),
        "the granted ability lets the attacker target the opponent's UNSUSPENDED Digimon");

    SetSuspended(match, oppDigimon, true);
    var defenderSuspended = new Cec.Permanent(match.Context, oppDigimon, P2);

    AssertTrue(!ess.CanAttackTargetDefendingPermanent(attacker, defenderSuspended, ess),
        "once the opponent's Digimon is suspended, the same predicate no longer permits targeting it " +
        "(a normal attack would already reach a suspended defender — this ability is specifically about unsuspended ones)");
}

// ═══════════════════════════════════ BT16_052 ═══════════════════════════════════

async Task BT16052_WhenDigivolvingPlaysToken()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9301, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt16052 = Stage(match, P1, "BT16_052", ChoiceZone.BattleArea, "1:battle:BT16052", register: true);
    int battleBefore = Count(match, P1, ChoiceZone.BattleArea);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt16052, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT16.Black.BT16_052();
    List<Cec.ICardEffect> wdEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var wd = (Cfx.ActivateClass)wdEffects.First(e => e.EffectName == "Play 1 [KoHagurumon] token");

    AssertTrue(wd.CanActivate(new System.Collections.Hashtable()), "CanActivate is true — the permanent exists on the battle area (no frame-capacity gate in the mirror)");

    await wd.Activate(new System.Collections.Hashtable());

    AssertTrue(Count(match, P1, ChoiceZone.BattleArea) >= battleBefore + 1,
        $"[When Digivolving]: a [KoHagurumon] token additionally landed on the owner's battle area " +
        $"(before:{battleBefore} after:{Count(match, P1, ChoiceZone.BattleArea)})");
}

// ═══════════════════════════════════ ST20_15 ═══════════════════════════════════

async Task ST2015_SecurityPlaysTamerFree()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId st2015 = Stage(match, P1, "ST20_15", ChoiceZone.Security, "1:sec:ST2015", register: true);
    HeadlessEntityId tamer = StageSynthetic(match, P1, "EXS5-TAMER", dp: 0, level: 0, "1:hand:tamer", zone: ChoiceZone.Hand, cardType: "Tamer");
    int handBefore = Count(match, P1, ChoiceZone.Hand);
    int battleBefore = Count(match, P1, ChoiceZone.BattleArea);

    policy.On(req => req.Candidates.Any(c => c.Id == tamer), req => ChoiceResult.Select(tamer));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, st2015, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST20.White.ST20_15();
    List<Cec.ICardEffect> secEffects = effectInstance.CardEffects(Cec.EffectTiming.SecuritySkill, card);
    var security = (Cfx.ActivateClass)secEffects.First(e => e.EffectName == "Play 1 tamer from hand");

    AssertTrue(security.CanActivate(new System.Collections.Hashtable()), "CanActivate is true (no CanActivateCondition gate on this arm)");

    await security.Activate(new System.Collections.Hashtable());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(tamer),
        $"[Security]: the tamer left the hand and landed on the battle area for free [debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertEqual(handBefore - 1, Count(match, P1, ChoiceZone.Hand), "exactly 1 card left the hand");
    AssertEqual(battleBefore + 1, Count(match, P1, ChoiceZone.BattleArea), "exactly 1 permanent landed on the battle area");
}

// ═══════════════════════════════════ BT12_044 ═══════════════════════════════════

async Task BT12044_WhenDigivolvingGrantsSAttackDebuff()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt12044 = Stage(match, P1, "BT12_044", ChoiceZone.BattleArea, "1:battle:BT12044", register: true);
    StageStat(match, bt12044, dp: 8000);
    HeadlessEntityId weakOpp = StageSynthetic(match, P2, "EXS5-WEAK", dp: 3000, level: 3, "2:battle:weak");

    policy.On(req => req.Candidates.Any(c => c.Id == weakOpp), req => ChoiceResult.Select(weakOpp));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt12044, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT12.Yellow.BT12_044();
    List<Cec.ICardEffect> wdEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var wd = (Cfx.ActivateClass)wdEffects.First(e => e.EffectName == "Security Attack -2");

    var weakOppPermanentBefore = new Cec.Permanent(match.Context, weakOpp, P2);
    AssertTrue(!weakOppPermanentBefore.HasSecurityAttackChanges, "the opponent's weak Digimon starts with no Security Attack changes");

    AssertTrue(wd.CanActivate(new System.Collections.Hashtable()),
        "CanActivate is true — a weaker opponent Digimon (3000 DP <= 8000 DP) is present");

    await wd.Activate(new System.Collections.Hashtable());

    var weakOppPermanentAfter = new Cec.Permanent(match.Context, weakOpp, P2);
    AssertTrue(weakOppPermanentAfter.HasSecurityAttackChanges,
        $"[When Digivolving]: the selected weaker opponent Digimon gained a Security Attack change (-2) " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT10_084 ═══════════════════════════════════

async Task BT10084_OnPlayPlaysTrashDigimonAndGrantsBlocker()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt10084 = Stage(match, P1, "BT10_084", ChoiceZone.BattleArea, "1:battle:BT10084", register: true);
    HeadlessEntityId trash1 = StageSynthetic(match, P1, "EXS5-BAGRA1", dp: 2000, level: 3, "1:trash:bagra1", zone: ChoiceZone.Trash, traits: new[] { "BagraArmy" });
    HeadlessEntityId trash2 = StageSynthetic(match, P1, "EXS5-BAGRA2", dp: 2000, level: 4, "1:trash:bagra2", zone: ChoiceZone.Trash, traits: new[] { "BagraArmy" });
    int trashBefore = Count(match, P1, ChoiceZone.Trash);
    int battleBefore = Count(match, P1, ChoiceZone.BattleArea);

    policy.On(req => req.Type == ChoiceType.Card, req => ChoiceResult.Select(req.Candidates.Where(c => c.IsSelectable).Take(req.MaxCount).Select(c => c.Id)));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt10084, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT10.Purple.BT10_084();
    List<Cec.ICardEffect> opEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var onPlay = (Cfx.ActivateClass)opEffects.First(e => e.EffectName == "Play Digimon from trash");

    AssertTrue(onPlay.CanActivate(new System.Collections.Hashtable()), "CanActivate is true — the permanent exists on the battle area and 2 trash cards match");

    await onPlay.Activate(new System.Collections.Hashtable());

    AssertEqual(trashBefore - 2, Count(match, P1, ChoiceZone.Trash),
        $"[On Play]: both matching trash Digimon left the trash [debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertEqual(battleBefore + 2, Count(match, P1, ChoiceZone.BattleArea), "both landed on the battle area");

    var playedPermanent1 = new Cec.Permanent(match.Context, trash1, P1);
    var playedPermanent2 = new Cec.Permanent(match.Context, trash2, P1);
    AssertTrue(playedPermanent1.EffectList(Cec.EffectTiming.None).Any(e => e is Cfx.BlockerClass),
        "the first played permanent gained a Blocker grant (GainBlocker, EffectTiming.None bucket)");
    AssertTrue(playedPermanent2.EffectList(Cec.EffectTiming.None).Any(e => e is Cfx.BlockerClass),
        "the second played permanent gained a Blocker grant (GainBlocker, EffectTiming.None bucket)");
}

// ═══════════════════════════════════ BT23_081 ═══════════════════════════════════

async Task BT23081_OnPlayPlaysHudieDigimonFree()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9701, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt23081 = Stage(match, P1, "BT23_081", ChoiceZone.BattleArea, "1:battle:BT23081", register: true);
    HeadlessEntityId hudie = StageSynthetic(match, P1, "EXS5-HUDIE", dp: 3000, level: 3, "1:hand:hudie", zone: ChoiceZone.Hand, traits: new[] { "Hudie" }, playCost: 4);
    int handBefore = Count(match, P1, ChoiceZone.Hand);
    int battleBefore = Count(match, P1, ChoiceZone.BattleArea);

    policy.On(req => req.Candidates.Any(c => c.Id == hudie), req => ChoiceResult.Select(hudie));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt23081, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT23.Yellow.BT23_081();
    List<Cec.ICardEffect> opEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var onPlay = (Cfx.ActivateClass)opEffects.First(e => e.EffectName == "play 1 play cost 5- [Hudie] trait digimon from hand");

    AssertTrue(onPlay.CanActivate(new System.Collections.Hashtable()), "CanActivate is true — the permanent exists on the battle area");

    await onPlay.Activate(new System.Collections.Hashtable());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(hudie),
        $"[On Play]: the [Hudie] Digimon left the hand and landed on the battle area for free " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertEqual(handBefore - 1, Count(match, P1, ChoiceZone.Hand), "exactly 1 card left the hand");
    AssertEqual(battleBefore + 1, Count(match, P1, ChoiceZone.BattleArea), "exactly 1 permanent landed on the battle area");
}

// ═══════════════════════════════════ AD1_006 ═══════════════════════════════════

async Task AD1006_OnPlayBottomDecksAndUnsuspends()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9801, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId ad1006 = Stage(match, P1, "AD1_006", ChoiceZone.BattleArea, "1:battle:AD1006", register: true);
    StageStat(match, ad1006, dp: 8000);
    SetSuspended(match, ad1006, true);
    HeadlessEntityId weakOpp = StageSynthetic(match, P2, "EXS5-AD1WEAK", dp: 5000, level: 4, "2:battle:ad1weak");
    int opponentLibraryBefore = Count(match, P2, ChoiceZone.Library);

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == weakOpp),
        req => ChoiceResult.Select(weakOpp));
    policy.On(req => req.Type == ChoiceType.ModeChoice,
        req => ChoiceResult.Select(req.Candidates.First(c => c.Label.Contains("Yes")).Id));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, ad1006, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.AD1.Red.AD1_006();
    List<Cec.ICardEffect> opEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var onPlay = (Cfx.ActivateClass)opEffects.First(e => e.EffectName == "May bottom deck an opponent's Digimon with same or less DP as this. May unsuspend");

    AssertTrue(onPlay.CanActivate(new System.Collections.Hashtable()),
        "CanActivate is true — a weaker/equal-DP opponent Digimon is present (also true via the self-suspended branch)");

    await onPlay.Activate(new System.Collections.Hashtable());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(weakOpp),
        $"[On Play]: the opponent's weaker Digimon left the battle area (bottom-decked) [debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertEqual(opponentLibraryBefore + 1, Count(match, P2, ChoiceZone.Library),
        "the bounced card was placed on the bottom of the OPPONENT's OWN deck (SelectPermanentEffect.Mode.PutLibraryBottom " +
        "moves the target permanent to its own controller's library, not the effect owner's)");
    AssertTrue(!IsSuspended(match, ad1006), "this Digimon chose to unsuspend (Yes branch)");
}

// ═══════════════════════════════════ ST22_08 ═══════════════════════════════════

async Task ST2208_SecurityDeletesMinDpAndReturnsToHand()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9901, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId st2208 = Stage(match, P1, "ST22_08", ChoiceZone.Security, "1:sec:ST2208", register: true);
    HeadlessEntityId weakOpp = StageSynthetic(match, P2, "EXS5-ST22WEAK", dp: 2000, level: 3, "2:battle:st22weak");
    HeadlessEntityId strongOpp = StageSynthetic(match, P2, "EXS5-ST22STRONG", dp: 6000, level: 5, "2:battle:st22strong");
    int handBefore = Count(match, P1, ChoiceZone.Hand);

    policy.On(req => req.Type == ChoiceType.Permanent, req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, st2208, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST22.Red.ST22_08();
    List<Cec.ICardEffect> secEffects = effectInstance.CardEffects(Cec.EffectTiming.SecuritySkill, card);
    var security = (Cfx.ActivateClass)secEffects.First(e => e.EffectName == "Delete opponent's lowest DP Digimon and add this card to hand");

    AssertTrue(security.CanActivate(new System.Collections.Hashtable()), "CanActivate is true (no CanActivateCondition gate on this arm)");

    await security.Activate(new System.Collections.Hashtable());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(weakOpp),
        $"[Security]: the opponent's LOWEST-DP Digimon was deleted [debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(strongOpp),
        "the opponent's higher-DP Digimon was NOT deleted (IsMinDP scoping verified)");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Hand).Contains(st2208), "this card was added to its owner's hand");
    AssertEqual(handBefore + 1, Count(match, P1, ChoiceZone.Hand), "exactly 1 card was added to the hand");
}

// ═══════════════════════════════════ BT20_079 ═══════════════════════════════════

async Task BT20079_OnPlayDeletesMinLevel()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 91001, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt20079 = Stage(match, P1, "BT20_079", ChoiceZone.BattleArea, "1:battle:BT20079", register: true);
    HeadlessEntityId lowLevelOpp = StageSynthetic(match, P2, "EXS5-LOWLVL", dp: 3000, level: 3, "2:battle:lowlvl");
    HeadlessEntityId highLevelOpp = StageSynthetic(match, P2, "EXS5-HIGHLVL", dp: 8000, level: 6, "2:battle:highlvl");

    policy.On(req => req.Type == ChoiceType.Permanent, req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt20079, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT20.Purple.BT20_079();
    List<Cec.ICardEffect> opEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var onPlay = (Cfx.ActivateClass)opEffects.First(e => e.EffectName == "Delete 1 of your opponent's Digimon");

    AssertTrue(onPlay.CanActivate(new System.Collections.Hashtable()), "CanActivate is true — a lowest-level opponent Digimon is present");

    await onPlay.Activate(new System.Collections.Hashtable());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(lowLevelOpp),
        $"[On Play]: the opponent's LOWEST-level Digimon was deleted [debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(highLevelOpp),
        "the opponent's higher-level Digimon was NOT deleted (IsMinLevel scoping verified)");
}

// ═══════════════════════════════════ BT23_057 ═══════════════════════════════════

async Task BT23057_ReducedCostArmActivatesWithThreeTrashMatches()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 91101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt23057 = Stage(match, P1, "BT23_057", ChoiceZone.Hand, "1:hand:BT23057", register: true);
    StageSynthetic(match, P1, "EXS5-HUCK1", dp: 0, level: 0, "1:trash:huck1", name: "Huckmon", zone: ChoiceZone.Trash);
    StageSynthetic(match, P1, "EXS5-HUCK2", dp: 0, level: 0, "1:trash:huck2", name: "Huckmon", zone: ChoiceZone.Trash);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt23057, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT23.Black.BT23_057();
    List<Cec.ICardEffect> noneEffects = effectInstance.CardEffects(Cec.EffectTiming.None, card);
    var reduceArm = (Cfx.ChangeCostClass)noneEffects.First(e => e.EffectName == "Play Cost -5");

    AssertTrue(!reduceArm.CanUse(new System.Collections.Hashtable()),
        "CanUse is false with only 2 matching trash cards (the gate requires >= 3 AND the BeforePayCost sibling arm registered)");

    StageSynthetic(match, P1, "EXS5-HUCK3", dp: 0, level: 0, "1:trash:huck3", name: "Huckmon", zone: ChoiceZone.Trash);

    List<Cec.ICardEffect> noneEffectsAfter = effectInstance.CardEffects(Cec.EffectTiming.None, card);
    var reduceArmAfter = (Cfx.ChangeCostClass)noneEffectsAfter.First(e => e.EffectName == "Play Cost -5");

    AssertTrue(reduceArmAfter.CanUse(new System.Collections.Hashtable()),
        "CanUse becomes true once a 3rd matching trash card is present — card.EffectList(BeforePayCost) finds the " +
        "registered sibling 'Return 3 cards...' arm (this card was staged with register:true) AND the trash count " +
        "gate (MatchConditionOwnersCardCountInTrash >= 3) passes");
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

// 합성 픽스처 카드(R4S3b StageBattleDigimon 관례 확장): def 업서트 + 인스턴스 + 존 이동.
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

// 실카드(Stage로 스테이징된, 합성 def가 아닌) 인스턴스의 dp를 조정 — BT12_044/AD1_006처럼 카드 자신의 DP를
// 비교 기준으로 쓰는 위트니스에서 필요.
static void StageStat(DcgoMatch match, HeadlessEntityId id, int? dp = null, int? level = null)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing instance {id.Value}");
    }

    var meta = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal);
    if (dp is int dpValue) { meta["dp"] = dpValue; }
    if (level is int levelValue) { meta["level"] = levelValue; }
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

static void SetSuspended(DcgoMatch match, HeadlessEntityId id, bool suspended)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing instance {id.Value}");
    }

    var meta = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { ["isSuspended"] = suspended };
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

static bool IsSuspended(DcgoMatch match, HeadlessEntityId id)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"missing instance {id.Value}");
    }

    return record.Metadata.TryGetValue("isSuspended", out object? v) && v is bool b && b;
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
        var effectRegistry = new InMemoryEffectRegistry();
        var gameEventQueue = new GameEventQueue();
        EngineContext? selfRef = null;
        var effectScheduler = new EffectScheduler(
            new EffectResolutionQueue(),
            CardEffectSchedulerResolver.Create(
                effectRegistry,
                sinkFactory: _ => new MatchStateMutationSink(
                    cardInstanceRepository, logSink, zoneMover, memoryController, effectRegistry, gameEventQueue,
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
            effectRegistry: effectRegistry,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
