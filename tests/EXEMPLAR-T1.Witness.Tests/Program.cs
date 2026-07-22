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
// EXEMPLAR-T1 정본 witness 스위트 — 커버리지 정본 포팅 1차 트랜치(클린 레인 6장), 카드당 3종.
// 표준 템플릿(후속 트랜치 복사 대상): 모든 witness는 DcgoMatch.CreatePumpDriven + 에이전트 액션 구동
// (R4RL-03/R4S3b 스타일 — 리걸 테이블에서 액션을 골라 ApplyActionAsync; OLD-cadence 직접 컨트롤러 호출·
// 스텝 액션 금지). 효과-내부 Select*/Optional 프롬프트는 ChoiceProvider 좌석(스크립트 답변 = 에이전트
// 좌석의 답)으로 응답 — R4RL-01의 ScriptedChoiceProvider 관례를 술어-매칭으로 일반화(PolicyChoiceProvider,
// CreateDefault의 provider 좌석만 교체하는 검증-동일 배선).
// 카드/축 매핑은 각 카드 소스 헤더(①②③ 정본 주석)와 docs/audit/coverage_exemplar_audit_2026-07-18.md §4.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // LM_054 — Treadmill Training (7축: K:Training*표기·Delete/Digivolve/IgnoreColor/PlaceDelay/DelayOption/IgnoreReq)
    ("LM_054 W1 [Main]: 덱탑 2장 공개→황색 1장 손패→나머지 덱밑→자신을 delay로 배틀에어리어 배치 (ActivateOption 펌프 레인)", LM054_MainRevealAddHandAndDelayPlaced),
    ("LM_054 W2 경계: 배틀에어리어에 동명 [Treadmill Training] 존재 시 ignore-color 게이트 OFF → 색 요구 미충족 → ActivateOption 레인 부재", LM054_IgnoreColorGateNegative),
    ("LM_054 W3 <Delay>: ActivateMain 발화 — 자기 삭제 착지 + 삭제-계속(successProcess) 진화 완주(evo 배틀에어리어 진입, 코스트-2 클램프) — RD-EXT1-02 상환", LM054_DelayDeletesSelfAndDigivolvesReduced),
    // BT19_091 — Trinity Burst! (5축: Alliance·토큰3종·SelectAttack)
    ("BT19_091 W1 [Main]: 토큰 플레이(동명 Taomon 스킵)→Lv.5에 <Alliance> 2회→강제 공격(SelectAttackEffect) 수행", BT19091_MainTokensAllianceForcedAttack),
    ("BT19_091 W2 경계: 동명 디지몬 2종(WarGrowlmon·Taomon) 존재 시 Rapidmon 토큰만 플레이", BT19091_DuplicateTokenNamesSkipped),
    ("BT19_091 W3 [Security]: 시큐리티에서 발화 — 손패의 Lv.5 [Rapidmon] 무료 플레이", BT19091_SecurityPlaysLevel5Free),
    // EX11_070 — Unchained (5축: MindLink·ChangeDP·ImmuneStackTrashing·MindLinkClass·PlayMindLinkTamer)
    ("EX11_070 W1 [Start of Your Turn]: 메모리 ≤2 → 3으로 세트 (없으면 1 유지 — 양/음 대조)", EX11070_StartOfTurnSetsMemoryTo3),
    ("EX11_070 W2 ESS: [Maquinamon] 텍스트 보유 숙주 DP 하한 1000 (텍스트 없으면 미적용 — 양/음 대조)", EX11070_DpFloorInheritedEssPositiveNegative),
    ("EX11_070 W3 [End of Your Turn]: <Mind Link> — 테이머가 [Maquinamon] 텍스트 디지몬의 진화원 밑으로 tuck", EX11070_EndOfTurnMindLinkTucksTamer),
    // BT17_026 — Beowolfmon (4축: CanNotSuspend·ChangeCardColor·ChangePermanentLevel·DontHaveDP)
    ("BT17_026 W1 [Hand][Main] 수확 flip: 손패 OnDeclaration 펌프 레인 등재 → 실효과 구동(트래시 Lobomon+KendoGarurumon→Koji 밑, Koji→BT17_026 진화) (RD-EXT1-01 해소)", BT17026_HandMainPumpLaneFlip),
    ("BT17_026 W2 [When Digivolving]: Digivolve 펌프 레인 → Hybrid 진화원 손패 회수 + 상대 퍼머넌트 서스펜드 불가", BT17026_WhenDigivolvingReturnsHybridAndCanNotSuspend),
    ("BT17_026 W3 [When Attacking] ESS: Hybrid 숙주 공격 시 상대 Lv.4 이하 바운스 (트레이트 없으면 미발화 — 양/음 대조)", BT17026_WhenAttackingInheritedBouncePositiveNegative),
    // EX10_029 — Warpmon (4축: Link*STOP·ImmuneFromDeDigivolve·PlaySelfDigimonAfterBattleSecurity·TrashLinkCards)
    ("EX10_029 W1 <Blocker>: 상대 다이렉트 어택에 블로커 창 개방 → 블록 → 시큐리티 무손실·공격자 전투 삭제", EX10029_BlockerBlocksDirectAttack),
    ("EX10_029 W2 대체 진화 조건: [Stnd.] Appmon 위 코스트 2 Digivolve 레인 (비-Stnd. 대상 레인 부재 — 양/음 대조)", EX10029_AltDigivolveConditionOntoStndAppmon),
    ("EX10_029 W3 [Security] 수확: 자기-플레이 발화는 기존 STOP RD-P6C3-B2 — 체크·전투는 완료, 플레이는 불발(정직 문서화)", EX10029_SecurityPlaysSelfAfterBattle),
    // P_223 — Kuzuhamon (4축: ChangeCardNames·PlayOptionCards·PlayPipeFox·OnUseOption)
    ("P_223 W1: 보안≤3 코스트 -4(7 지불) → [On Play] 트래시의 [Plug-In] 옵션 무료 사용·[Main] 해소 (+토큰 꼬리=RD-EXT1-03 상환·OnUseOption 반응자 [Pipe Fox] 발화)", P223_ReducedPlayThenOptionFromTrashAndPipeFox),
    ("P_223 W2 경계: 보안 4장이면 감소 없음 → 코스트 11 > 지불가능 10 → PlayCard 레인 부재", P223_NoReductionAtFourSecurity),
    ("P_223 W3 이름 룰: [Sakuyamon]으로도 취급 (타 카드는 불취급 — 양/음 대조)", P223_NameRuleSakuyamon),
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

// ═══════════════════════════════════ LM_054 ═══════════════════════════════════

async Task LM054_MainRevealAddHandAndDelayPlaced()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1101, MonoDecks("BT10_030", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId lm = Stage(match, P1, "LM_054", ChoiceZone.Hand, "1:hand:LM054");

    int handBefore = Count(match, P1, ChoiceZone.Hand);
    int libBefore = Count(match, P1, ChoiceZone.Library);

    // 공개-선택(황색 1장)은 효과-내부 Select — 에이전트 좌석(policy)이 1장 선택으로 응답.
    policy.On(req => req.Type == ChoiceType.Card && req.Candidates.Count > 0, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));

    LegalAction option = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, lm,
        "LM_054's own IgnoreColorConditionClass must waive the (Yellow+Purple) color requirement");
    await ApplyAsync(match, option);
    await DriveUntilAsync(match, m => Count(m, P1, ChoiceZone.BattleArea) >= 1 || m.IsTerminal());

    AssertEqual(handBefore, Count(match, P1, ChoiceZone.Hand),
        "hand: LM_054 left (-1), one revealed Yellow card added (+1) — net unchanged");
    AssertEqual(libBefore - 1, Count(match, P1, ChoiceZone.Library),
        "library: 2 revealed, 1 added to hand, 1 returned to the bottom — net -1");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(lm),
        "PlaceDelayOptionCards: LM_054 sits in the battle area as a delay option");
}

async Task LM054_IgnoreColorGateNegative()
{
    (DcgoMatch match, PolicyChoiceProvider unusedPolicy) = await NewExemplarMatchAsync(seed: 1102, MonoDecks("BT10_030", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 동명 [Treadmill Training]을 미리 배틀에어리어에 배치(등록) → 자체 ignore-color 게이트 OFF.
    HeadlessEntityId placed = Stage(match, P1, "LM_054", ChoiceZone.BattleArea, "1:battle:LM054", register: true);
    HeadlessEntityId inHand = Stage(match, P1, "LM_054", ChoiceZone.Hand, "1:hand:LM054");

    // 필드에 Purple 색 공급자 없음(색 요구 [Yellow,Purple] 미충족) + ignore-color OFF → 레인 부재.
    AssertTrue(FindLane(match, P1, HeadlessActionTypes.ActivateOption, inHand) is null,
        "with a same-named delay already on the field the ignore-color gate is OFF and the dual color requirement blocks the option");
    _ = placed;
}

async Task LM054_DelayDeletesSelfAndDigivolvesReduced()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1103, MonoDecks("BT10_030", "BT1_028"));
    await ReachMainWaitAsync(match);
    // delay 상태의 LM_054(배틀에어리어) + 황색 Lv.3 숙주 + 손패의 황색 Lv.4(진화코스트 1 → -2 ⇒ 0).
    HeadlessEntityId delay = Stage(match, P1, "LM_054", ChoiceZone.BattleArea, "1:battle:LM054delay", register: true);
    // (RD-EXT1-02) A <Delay> option reaches the battle area ONLY via PlaceDelayOptionCards, which marks the
    // resulting permanent IsPlayedOptionPermanent=true (AS-IS CardEffectCommons.cs:131 / Permanent.cs:3946). A
    // direct-staged option lacking that flag is trashed by the no-DP rule sweep (AutoProcessing.TrashNoDP-
    // PermanentProcess, AS-IS :182 — options are swept UNLESS IsPlayedOptionPermanent) before its declaration
    // resolves. Stamp the flag so the fixture is a faithful delay-option permanent (the earlier harvest pin
    // mis-attributed the missing digivolve to a pump flow-drain asymmetry; the real gap was this fixture flag —
    // once set, SetActSkill finds the <Delay> OnDeclaration effect and the whole body runs: self-delete →
    // successProcess → SelectPermanent → DigivolveIntoHandOrTrashCard, all on the AS-IS pump ActivateMain path).
    MarkPlayedOptionPermanent(match, delay);
    HeadlessEntityId host = Stage(match, P1, "BT10_030", ChoiceZone.BattleArea, "1:battle:Tinkermon", register: true);
    HeadlessEntityId evo = Stage(match, P1, "BT10_033", ChoiceZone.Hand, "1:hand:Shortmon");

    int memBefore = MemoryFor(match, P1);

    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    policy.On(req => req.Type == ChoiceType.Permanent, req => ChoiceResult.Select(host));
    policy.On(req => req.Type == ChoiceType.Card || req.Type == ChoiceType.HandCard, req =>
        req.Candidates.Any(c => c.Id == evo) ? ChoiceResult.Select(evo) : PolicyChoiceProvider.Fallback(req), oneShot: false);

    LegalAction main = RequireLane(match, P1, HeadlessActionTypes.ActivateMain, delay,
        "the delay option's OnDeclaration skill must be listed for the battle-area LM_054 (CanDeclareOptionDelayEffect)");
    await ApplyAsync(match, main);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.Trash).Contains(delay) || m.IsTerminal());
    for (int i = 0; i < 16 && !ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(evo); i++)
    {
        await StepOnceAsync(match);
    }

    AssertTrue(ZoneCards(match, P1, ChoiceZone.Trash).Contains(delay),
        "DeletePeremanentAndProcessAccordingToResult: the delay LM_054 deleted itself into the trash");
    // (RD-EXT1-02 상환) 자기 삭제는 DeletePeremanentAndProcessAccordingToResult의 successProcess 계속을 즉시
    // settle(자기-삭제는 대체창 없이 인라인 착지 → destroyed=1)하고, successProcess가 SelectPermanent(host)→
    // DigivolveIntoHandOrTrashCard(evo, 코스트-2)를 AS-IS 펌프 ActivateMain(SetActSkill→ActivateEffectProcess)
    // 경로에서 완주한다. 진화 절반이 실착지 — evo가 배틀에어리어에 진입한다.
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(evo),
        "RD-EXT1-02: the delete-continuation digivolve landed — the Yellow Lv.4 [Shortmon] digivolved onto the " +
        $"host on the pump ActivateMain path [debug canEvolve:{DebugCanEvolve(match, evo, host)} prompts:{string.Join(" | ", policy.Seen)}]");
    AssertEqual(memBefore, MemoryFor(match, P1),
        "digivolution cost 1 reduced by 2 clamps to 0 — no memory paid");
}

// ═══════════════════════════════════ BT19_091 ═══════════════════════════════════

async Task BT19091_MainTokensAllianceForcedAttack()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // Lv.5 [Taomon](실카드 BT10_039)이 ignore-color 조건(자기 Lv.5 + 3종 중 1명명)을 만족.
    HeadlessEntityId taomon = Stage(match, P1, "BT10_039", ChoiceZone.BattleArea, "1:battle:Taomon", register: true);
    // Alliance 파트너(공격 중 AllianceTarget 창 개방의 증인).
    HeadlessEntityId partner = Stage(match, P1, "BT10_030", ChoiceZone.BattleArea, "1:battle:Partner", register: true);
    HeadlessEntityId option = Stage(match, P1, "BT19_091", ChoiceZone.Hand, "1:hand:TrinityBurst");
    int p2SecBefore = Count(match, P2, ChoiceZone.Security);

    // <Alliance> 부여의 행동 증거: 공격 중 AllianceProcess의 서스펜드-아군 선택("Select 1 Digimon to
    // suspend", C-Atk-Alliance 관례)이 열리는지 관찰(코스트 8 지불로 턴이 넘어가며 UntilEachTurnEnd
    // 그랜트가 만료되므로 사후 HasAlliance 판독은 불가 — 창 관찰이 정본).
    bool allianceWindowSeen = false;
    policy.On(req => req.Message.Contains("Select 1 Digimon to suspend", StringComparison.Ordinal), req =>
    {
        allianceWindowSeen = true;
        return req.CanSkip ? ChoiceResult.Skip() : PolicyChoiceProvider.Fallback(req);
    }, oneShot: false);

    LegalAction lane = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, option,
        "conditional IgnoreColorConditionClass (own Lv.5 + [Taomon] name) must waive the triple color requirement");
    await ApplyAsync(match, lane);
    await DriveUntilAsync(match, m => !m.HasPendingChoice() && m.Context.AttackController.Current.IsPending == false || m.IsTerminal());

    IReadOnlyList<HeadlessEntityId> field = ZoneCards(match, P1, ChoiceZone.BattleArea);
    AssertTrue(field.Any(id => CardNumberOf(match, id) == "BT19-091-token-red"),
        "the [WarGrowlmon] token entered the battle area");
    AssertTrue(field.Any(id => CardNumberOf(match, id) == "BT19-091-token-green"),
        "the [Rapidmon] token entered the battle area");
    AssertTrue(!field.Any(id => CardNumberOf(match, id) == "BT19-091-token-yellow"),
        "the [Taomon] token was SKIPPED (a same-named Digimon is on the field)");
    AssertTrue(allianceWindowSeen,
        "GainAlliance: the forced attack opened the <Alliance> suspend-ally window (the grant was live during the attack)");
    AssertTrue(IsSuspendedMeta(match, taomon) || Count(match, P2, ChoiceZone.Security) < p2SecBefore,
        "SelectAttackEffect (mandatory): the Lv.5 attacked — suspended attacker or a consumed security card");
    _ = partner;
}

async Task BT19091_DuplicateTokenNamesSkipped()
{
    (DcgoMatch match, PolicyChoiceProvider unusedPolicy) = await NewExemplarMatchAsync(seed: 1202, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    Stage(match, P1, "BT10_039", ChoiceZone.BattleArea, "1:battle:Taomon", register: true);
    Stage(match, P1, "AD1_003", ChoiceZone.BattleArea, "1:battle:WarGrowlmon", register: true);
    HeadlessEntityId option = Stage(match, P1, "BT19_091", ChoiceZone.Hand, "1:hand:TrinityBurst");
    int fieldBefore = Count(match, P1, ChoiceZone.BattleArea);

    LegalAction lane = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, option, "option lane");
    await ApplyAsync(match, lane);
    await DriveUntilAsync(match, m => !m.HasPendingChoice() || m.IsTerminal());

    IReadOnlyList<HeadlessEntityId> field = ZoneCards(match, P1, ChoiceZone.BattleArea);
    AssertTrue(field.Any(id => CardNumberOf(match, id) == "BT19-091-token-green"),
        "only the [Rapidmon] token was played");
    AssertTrue(!field.Any(id => CardNumberOf(match, id) == "BT19-091-token-red")
        && !field.Any(id => CardNumberOf(match, id) == "BT19-091-token-yellow"),
        "the [WarGrowlmon]/[Taomon] tokens were both skipped (same-named Digimon present)");
    AssertEqual(fieldBefore + 1, Count(match, P1, ChoiceZone.BattleArea), "net +1 permanent (1 token)");
}

async Task BT19091_SecurityPlaysLevel5Free()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1203, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 펌프 StartGame이 시큐리티 5장을 딜함 — 체크 순서를 결정론화하기 위해 P1 딜 시큐리티를 비우고
    // BT19_091 1장만 남긴다. 손패 = Lv.5 [Rapidmon](BT19_050). P2 공격자 스테이징 후 P2 턴으로.
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    HeadlessEntityId sec = Stage(match, P1, "BT19_091", ChoiceZone.Security, "1:sec:TrinityBurst");
    HeadlessEntityId rapidmon = Stage(match, P1, "BT19_050", ChoiceZone.Hand, "1:hand:Rapidmon");
    HeadlessEntityId attacker = StageSynthetic(match, P2, "EXT1-ATK", dp: 3000, level: 4, "2:battle:atk");

    // SelectHandEffect의 요청 타입은 Card(브릿지 관례) — 후보 포함 여부로 매칭.
    policy.On(req => req.Candidates.Any(c => c.Id == rapidmon), req => ChoiceResult.Select(rapidmon));

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));

    LegalAction attack = RequireLane(match, P2, HeadlessActionTypes.DeclareAttack, attacker,
        "the staged P2 digimon must be able to declare a (direct) attack");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(rapidmon)
        || AtMainWaitOf(m, P2) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(rapidmon),
        "[Security] played the Lv.5 [Rapidmon] from hand — " +
        $"[debug sec-in-security:{ZoneCards(match, P1, ChoiceZone.Security).Contains(sec)} " +
        $"sec-in-trash:{ZoneCards(match, P1, ChoiceZone.Trash).Contains(sec)} " +
        $"rapidmon-in-hand:{ZoneCards(match, P1, ChoiceZone.Hand).Contains(rapidmon)} " +
        $"p1sec:{Count(match, P1, ChoiceZone.Security)} attacker-susp:{IsSuspendedMeta(match, attacker)} " +
        $"prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Security).Contains(sec),
        "the flipped BT19_091 left the security stack");
    // P2 턴 중(P2 +3 = P1 -3) 무료 플레이 — 메모리 무변(공격 선언 시점 대비).
    AssertEqual(-3, MemoryFor(match, P1), "played WITHOUT paying the cost — the gauge stayed at the attack-turn value (P2 +3)");
}

// ═══════════════════════════════════ EX11_070 ═══════════════════════════════════

async Task EX11070_StartOfTurnSetsMemoryTo3()
{
    // 패스는 상대에게 3을 주므로(AS-IS 패스 규칙) 구분 불가 — P2가 BT1_028(코스트 2)를 2장 플레이해
    // 메모리를 3→1→-1로 소진시키면 P1은 자기 턴을 메모리 1로 시작한다(기본 룰은 1 유지).
    // 대조군: 테이머 없음 → 1. 실험군: EX11_070 → [Start of Your Turn] 2 이하 → 3.
    async Task<int> TurnThreeMemory(bool withTamer)
    {
        (DcgoMatch match, PolicyChoiceProvider unusedPolicy) = await NewExemplarMatchAsync(seed: 1301, MonoDecks("BT1_028", "BT1_028"));
        await ReachMainWaitAsync(match);
        if (withTamer)
        {
            Stage(match, P1, "EX11_070", ChoiceZone.BattleArea, "1:battle:Unchained", register: true, cardType: "Tamer");
        }

        await PassTurnAsync(match, P1);
        await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
        for (int i = 0; i < 2 && AtMainWaitOf(match, P2); i++)
        {
            LegalAction play = Legal(match, P2).First(a => a.ActionType == HeadlessActionTypes.PlayCard);
            await ApplyAsync(match, play);
            await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || AtMainWaitOf(m, P1) || m.IsTerminal());
        }

        await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));
        return MemoryFor(match, P1);
    }

    AssertEqual(1, await TurnThreeMemory(withTamer: false),
        "control: P2 spending 2+2 from 3 hands P1 the turn at 1 memory (base rule keeps 1)");
    AssertEqual(3, await TurnThreeMemory(withTamer: true),
        "SetMemoryTo3TamerEffect: [Start of Your Turn] with 2 or less memory sets it to 3");
}

async Task EX11070_DpFloorInheritedEssPositiveNegative()
{
    (DcgoMatch match, PolicyChoiceProvider unusedPolicy) = await NewExemplarMatchAsync(seed: 1302, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 숙주(+) = [Maquinamon] 텍스트 보유(effect 메타), DP 500; 숙주(-) = 텍스트 없음.
    HeadlessEntityId hostPos = StageSynthetic(match, P1, "EXT1-MAQ", dp: 500, level: 4, "1:battle:maq",
        extraDefMeta: new Dictionary<string, object?>(StringComparer.Ordinal) { ["effect"] = "This is a Maquinamon support body." });
    HeadlessEntityId hostNeg = StageSynthetic(match, P1, "EXT1-NOMAQ", dp: 500, level: 4, "1:battle:nomaq");
    HeadlessEntityId tamerUnder = Stage(match, P1, "EX11_070", ChoiceZone.None, "1:under:Unchained", cardType: "Tamer");
    HeadlessEntityId tamerUnder2 = Stage(match, P1, "EX11_070", ChoiceZone.None, "1:under2:Unchained", cardType: "Tamer");
    SetSources(match, hostPos, tamerUnder);
    SetSources(match, hostNeg, tamerUnder2);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var pos = new Cec.Permanent(match.Context, hostPos, P1);
    var neg = new Cec.Permanent(match.Context, hostNeg, P1);
    AssertEqual(1000, pos.DP, "ESS ChangeDPClass: a [Maquinamon]-text host under-carrying EX11_070 can't drop below 1000 DP");
    AssertEqual(500, neg.DP, "negative: without the [Maquinamon] text the DP floor does not apply");
}

async Task EX11070_EndOfTurnMindLinkTucksTamer()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1303, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId tamer = Stage(match, P1, "EX11_070", ChoiceZone.BattleArea, "1:battle:Unchained", register: true, cardType: "Tamer");
    HeadlessEntityId digimon = StageSynthetic(match, P1, "EXT1-MAQ2", dp: 5000, level: 5, "1:battle:maqhost",
        extraDefMeta: new Dictionary<string, object?>(StringComparer.Ordinal) { ["effect"] = "Maquinamon rider." });

    // OnEndTurn 몸통: DNA 후보 없음(내부 스킵) → MindLink 선택 — 에이전트 좌석이 디지몬을 선택.
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == digimon),
        req => ChoiceResult.Select(digimon));
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());

    AssertTrue(!ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(tamer),
        "MindLinkClass.MindLink: the tamer permanent left the battle area");
    AssertTrue(SourcesOf(match, digimon).Contains(tamer),
        "the tamer tucked to the BOTTOM of the selected Digimon's digivolution cards");
}

// ═══════════════════════════════════ BT17_026 ═══════════════════════════════════

async Task BT17026_HandMainPumpLaneFlip()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 전제 전부 충족: 손패 BT17_026, 배틀에어리어 [Koji Minamoto](비-토큰 테이머), 트래시 Lobomon+KendoGarurumon.
    HeadlessEntityId inHand = Stage(match, P1, "BT17_026", ChoiceZone.Hand, "1:hand:Beowolf");
    HeadlessEntityId koji = StageSynthetic(match, P1, "EXT1-KOJI", dp: 0, level: 0, "1:battle:koji", name: "Koji Minamoto", cardType: "Tamer");
    HeadlessEntityId lobo = StageSynthetic(match, P1, "EXT1-LOBO", dp: 4000, level: 4, "1:trash:lobo", name: "Lobomon", zone: ChoiceZone.Trash);
    HeadlessEntityId kendo = StageSynthetic(match, P1, "EXT1-KENDO", dp: 4000, level: 4, "1:trash:kendo", name: "KendoGarurumon", zone: ChoiceZone.Trash);

    // RD-EXT1-01 해소: the hand [Main] pump lane now scans HAND (AS-IS TurnStateMachine.CanSelect:925), so
    // BT17_026's OnDeclaration [Hand][Main] skill is OFFERED. This is the harvest FLIP — the lane EXISTS and drives
    // the REAL effect (no bypass): the CanDeclareAt gate (CanUse(null)) only surfaces it because all AS-IS
    // preconditions hold (Lobomon+KendoGarurumon in trash + a Koji Minamoto on the field).
    LegalAction main = RequireLane(match, P1, HeadlessActionTypes.ActivateMain, inHand,
        "the HAND card BT17_026's OnDeclaration [Hand][Main] skill is now offered by the pump lane (RD-EXT1-01 flip)");

    // Drive the genuine effect: SelectPermanent(Koji Minamoto) → SelectCard(Lobomon+KendoGarurumon from trash) →
    // place them under Koji → digivolve Koji into BT17_026 (as a level-4 blue Digimon for cost 3).
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == koji),
        req => ChoiceResult.Select(koji), oneShot: false);
    // The trash multi-select (maxCount 2) opens as sequential single-select requests; pick one matching card each
    // time (the picked one drops out of the candidate set on the re-request), so Lobomon then KendoGarurumon are chosen.
    policy.On(req => (req.Type == ChoiceType.Card || req.Type == ChoiceType.HandCard)
            && req.Candidates.Any(c => c.IsSelectable && (c.Id == lobo || c.Id == kendo)),
        req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable && (c.Id == lobo || c.Id == kendo)).Id),
        oneShot: false);
    policy.On(req => req.Type == ChoiceType.OptionalEffect,
        req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    await ApplyAsync(match, main);
    // Drive the resolution to settle (BT17_026 leaves the hand — either onto the field on a successful digivolve,
    // or to the trash via the AS-IS DigivolvedFailed → IDiscardHand path).
    await DriveUntilAsync(match, m => !ZoneCards(m, P1, ChoiceZone.Hand).Contains(inHand) || m.IsTerminal());

    // RD-EXT1-01 FLIP (non-bypass): the pump lane drove BT17_026's GENUINE [Hand][Main] effect — SelectPermanent
    // (Koji) → SelectCard (Lobomon+KendoGarurumon from trash) → place-under all executed, so the two trash cards
    // are now digivolution sources under Koji (they LEFT the trash). This committed state change is produced ONLY by
    // the real effect; it proves the hand [Main] skill both surfaces AND resolves.
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Trash).Contains(lobo)
            && !ZoneCards(match, P1, ChoiceZone.Trash).Contains(kendo),
        "RD-EXT1-01 FLIP: the hand [Main] lane drove the real effect — Lobomon+KendoGarurumon were placed under Koji " +
        "(left the trash), the genuine committed first stage of BT17_026's [Hand][Main] skill");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(inHand),
        "BT17_026 left the hand (the [Hand][Main] declaration resolved and consumed it)");
    // RD-EXT1-04 FLIP (now landed): the FINAL cross-type digivolve — Koji (a Tamer, level 0) → BT17_026 treated as
    // a level-4 blue Digimon for a fixed cost 3, via ChangeCardColor/ChangePermanentLevel/TreatAsDigimon +
    // DigivolveIntoHandOrTrashCard — lands in the AS-IS DigivolvedFailed branch: BT17_026 is discarded to the TRASH
    // via `new IDiscardHand(card).Discard(...)` (AS-IS BT17_026.cs:395-401 DigivolvedFailed → IDiscardHand, mirror
    // BT17_026.cs:456-462). The residual pin is now a definite landing, not a disjunction.
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Trash).Contains(inHand),
        "RD-EXT1-04 FLIP: the cross-type treat-as-Digimon digivolve (Koji Tamer → BT17_026) lands in the AS-IS " +
        "DigivolvedFailed → IDiscardHand branch — BT17_026 is in the TRASH");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(inHand),
        "BT17_026 did not settle as a battle-area permanent (the treat-as-Digimon digivolve resolved via DigivolvedFailed, not a successful digivolve)");
}

async Task BT17026_WhenDigivolvingReturnsHybridAndCanNotSuspend()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1402, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 숙주: Blue Lv.4(BT10_019 Greymon) + Hybrid 트레이트 진화원 1장; 손패: BT17_026(진화비 4).
    HeadlessEntityId host = Stage(match, P1, "BT10_019", ChoiceZone.BattleArea, "1:battle:Greymon", register: true);
    HeadlessEntityId hybridSource = StageSynthetic(match, P1, "EXT1-HYB", dp: 3000, level: 3, "1:under:hyb",
        zone: ChoiceZone.None, traits: new[] { "Hybrid" });
    SetSources(match, host, hybridSource);
    HeadlessEntityId beowolf = Stage(match, P1, "BT17_026", ChoiceZone.Hand, "1:hand:Beowolf");
    HeadlessEntityId p2digi = StageSynthetic(match, P2, "EXT1-P2D", dp: 4000, level: 4, "2:battle:p2d");

    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));
    policy.On(req => req.Type == ChoiceType.Card && req.Candidates.Any(c => c.Id == hybridSource),
        req => ChoiceResult.Select(hybridSource));
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == p2digi),
        req => ChoiceResult.Select(p2digi));

    LegalAction digivolve = RequireLane(match, P1, HeadlessActionTypes.Digivolve, beowolf,
        "BT17_026 must be able to digivolve onto the Blue Lv.4");
    await ApplyAsync(match, digivolve);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.Hand).Contains(hybridSource) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.Hand).Contains(hybridSource),
        "[When Digivolving] returned the [Hybrid] digivolution card to the hand");

    // 상대 퍼머넌트 서스펜드 불가 — CanNotSuspendClass 부여 검증: P2 턴에 공격 선언 레인 부재.
    // (진화비 4 지불로 메모리가 0을 건너 턴이 자동 종료 — R4S3b PlayCardDispatch와 동일 흐름이므로
    // Pass 없이 P2 메인 대기까지 드라이브만 한다.)
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());
    AssertTrue(FindLane(match, P2, HeadlessActionTypes.DeclareAttack, p2digi) is null,
        "CanNotSuspendClass: the debuffed P2 permanent cannot suspend, so it cannot declare an attack on its turn");
}

async Task BT17026_WhenAttackingInheritedBouncePositiveNegative()
{
    async Task<bool> Bounced(bool hybridHost)
    {
        (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1403, MonoDecks("BT1_028", "BT1_028"));
        await ReachMainWaitAsync(match);
        HeadlessEntityId host = StageSynthetic(match, P1, "EXT1-HOST", dp: 7000, level: 5, "1:battle:host",
            traits: hybridHost ? new[] { "Hybrid" } : Array.Empty<string>());
        HeadlessEntityId source = Stage(match, P1, "BT17_026", ChoiceZone.None, "1:under:Beowolf");
        SetSources(match, host, source);
        Cec.CardEffectRegistrar.RegisterCard(match.Context, host, P1);
        HeadlessEntityId p2digi = StageSynthetic(match, P2, "EXT1-P2D", dp: 4000, level: 4, "2:battle:p2d");

        policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == p2digi),
            req => ChoiceResult.Select(p2digi));

        LegalAction attack = RequireLane(match, P1, HeadlessActionTypes.DeclareAttack, host, "host attack lane");
        await ApplyAsync(match, attack);
        await DriveUntilAsync(match, m => !m.HasPendingChoice() && !m.Context.AttackController.Current.IsPending || m.IsTerminal());
        return ZoneCards(match, P2, ChoiceZone.Hand).Contains(p2digi);
    }

    AssertTrue(await Bounced(hybridHost: true),
        "[When Attacking] inherited: a [Hybrid] host attacking bounces the opponent's Lv.4-or-lower Digimon to hand");
    AssertTrue(!await Bounced(hybridHost: false),
        "negative: without the [Hybrid]/[Ten Warriors] trait the inherited effect does not fire");
}

// ═══════════════════════════════════ EX10_029 ═══════════════════════════════════

async Task EX10029_BlockerBlocksDirectAttack()
{
    (DcgoMatch match, PolicyChoiceProvider unusedPolicy) = await NewExemplarMatchAsync(seed: 1501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId warpmon = Stage(match, P1, "EX10_029", ChoiceZone.BattleArea, "1:battle:Warpmon", register: true);
    HeadlessEntityId attacker = StageSynthetic(match, P2, "EXT1-ATK", dp: 3000, level: 4, "2:battle:atk");
    int p1SecBefore = Count(match, P1, ChoiceZone.Security);

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    LegalAction attack = RequireLane(match, P2, HeadlessActionTypes.DeclareAttack, attacker, "P2 direct attack");
    await ApplyAsync(match, attack);

    // 블로커 창: P1의 EX10_029가 후보로 개방 — 블록 선택.
    await DriveUntilAsync(match, m => m.HasPendingChoice() && m.Context.ChoiceController.PendingRequest!.Type == ChoiceType.Blocker || m.IsTerminal());
    ChoiceRequest blocker = match.Context.ChoiceController.PendingRequest!;
    AssertTrue(blocker.Candidates.Any(c => c.Id == warpmon),
        "BlockerSelfStaticEffect: EX10_029 is listed in the block window");
    await ResolveChoosingAsync(match, warpmon);
    await DriveUntilAsync(match, m => !m.Context.AttackController.Current.IsPending || m.IsTerminal());

    AssertEqual(p1SecBefore, Count(match, P1, ChoiceZone.Security), "the block prevented the security check");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(attacker),
        "battle: the 3000 DP attacker lost to the 4000 DP blocker and was deleted");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(warpmon), "the blocker survived");
}

async Task EX10029_AltDigivolveConditionOntoStndAppmon()
{
    (DcgoMatch match, PolicyChoiceProvider unusedPolicy) = await NewExemplarMatchAsync(seed: 1502, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId appmon = StageSynthetic(match, P1, "EXT1-STND", dp: 3000, level: 3, "1:battle:stnd",
        traits: new[] { "Stnd." });
    HeadlessEntityId nonApp = StageSynthetic(match, P1, "EXT1-PLAIN", dp: 3000, level: 3, "1:battle:plain");
    HeadlessEntityId warpmon = Stage(match, P1, "EX10_029", ChoiceZone.Hand, "1:hand:Warpmon");
    int memBefore = MemoryFor(match, P1);

    LegalAction? ontoPlain = FindDigivolveLane(match, P1, warpmon, nonApp);
    AssertTrue(ontoPlain is null,
        "negative: no digivolve lane onto a non-[Stnd.] non-Purple-Lv.3 permanent");

    LegalAction ontoAppmon = FindDigivolveLane(match, P1, warpmon, appmon)
        ?? throw new InvalidOperationException("AddSelfDigivolutionRequirementStaticEffect must open the [Stnd.] Appmon digivolve lane");
    await ApplyAsync(match, ontoAppmon);
    // 진화 후 상주 id는 warpmon으로 재키(S3b-2① AddCardSource) — appmon은 진화원으로 스레드.
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(warpmon) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(warpmon)
        && SourcesOf(match, warpmon).Contains(appmon),
        "EX10_029 digivolved onto the [Stnd.] Appmon (re-keyed resident, Appmon threaded as source)");
    AssertEqual(memBefore - 2, MemoryFor(match, P1), "the alternative digivolution cost 2 was paid");
}

async Task EX10029_SecurityPlaysSelfAfterBattle()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1503, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    HeadlessEntityId sec = Stage(match, P1, "EX10_029", ChoiceZone.Security, "1:sec:Warpmon");
    HeadlessEntityId attacker = StageSynthetic(match, P2, "EXT1-ATK", dp: 3000, level: 4, "2:battle:atk");

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    LegalAction attack = RequireLane(match, P2, HeadlessActionTypes.DeclareAttack, attacker, "P2 direct attack");
    await ApplyAsync(match, attack);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2) || m.IsTerminal());

    // 수확(RD-P6C3-B2): 미러 팩토리 PlaySelfDigimonAfterBattleSecurityEffect의 ActivateCoroutine은
    // NotSupportedException STOP(CardEffectFactory.cs:1010-1017 — UntilEndBattle 그랜트 버킷/
    // DestroyPermanentsClass 미이관). CanActivate(IsExistOnExecutingArea)가 미러 시큐리티 흐름
    // (체크 카드→트래시)에서 false라 STOP까지 도달하지 못하고 자기-플레이가 조용히 불발된다.
    // 감사 §4의 "클린" 분류를 뒤집는 실측 — 이 witness는 그 상태를 정직하게 고정한다(우회 green 금지):
    // 시큐리티 체크는 소비되고, 시큐리티 디지몬 전투(4000 vs 3000)는 공격자를 삭제하며, 자기-플레이는 불발.
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Trash).Contains(sec),
        "the checked EX10_029 was consumed into the trash (security check ran)");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(attacker),
        "the security-Digimon battle (4000 vs 3000) deleted the attacker");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(sec),
        "HARVEST RD-P6C3-B2: the self-play half does NOT fire on the mirror (existing factory STOP) — " +
        "when this assert fails the STOP was repaid and this witness must flip to assert the play");
}

// ═══════════════════════════════════ P_223 ═══════════════════════════════════

async Task P223_ReducedPlayThenOptionFromTrashAndPipeFox()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 1601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 펌프 StartGame이 시큐리티 5장을 딜함 — 보안≤3 전제를 위해 P1 시큐리티를 비운다(0 ≤ 3).
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    // 코스트 11-4=7. 트래시: [Plug-In] 트레이트를 단 실옵션 ST1_15. P2: 저DP 디지몬(ST1_15 대상).
    HeadlessEntityId kuzuha = Stage(match, P1, "P_223", ChoiceZone.Hand, "1:hand:Kuzuhamon");
    HeadlessEntityId st115 = StageSynthetic(match, P1, "ST1_15", dp: 0, level: 0, "1:trash:st115",
        zone: ChoiceZone.Trash, cardType: "Option", traits: new[] { "Plug-In" }, name: "Hammer Spark");
    HeadlessEntityId p2low = StageSynthetic(match, P2, "EXT1-LOW", dp: 1000, level: 3, "2:battle:low");

    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    policy.On(req => req.Candidates.Any(c => c.Id == st115), req => ChoiceResult.Select(st115));

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, kuzuha,
        "with 3-or-fewer security the hidden ChangeCostClass reduces the play cost to 7 (< max payable 10) " +
        $"[debug payingCost(avail:true):{DebugPayingCost(match, kuzuha, true)} payingCost(avail:false):{DebugPayingCost(match, kuzuha, false)} " +
        $"changeCost:{DebugChangeCost(match, kuzuha)}]");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(kuzuha) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(kuzuha), "P_223 entered the battle area");
    AssertEqual(-7, MemoryFor(match, P1), "cost 11 reduced by 4 — exactly 7 memory paid (gauge -7 for P1)");
    await DriveUntilAsync(match, m => !m.HasPendingChoice() || m.IsTerminal());
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(p2low) || !ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(p2low),
        "PlayOptionCards: the [Plug-In] option from the trash resolved its [Main] (the low-DP Digimon was deleted)");
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Any(id => CardNumberOf(m, id) == "BT19-040-token")
        || AtMainWaitOf(m, P2) || m.IsTerminal());
    // (RD-EXT1-03 상환) 효과-구동 PlayOptionCards는 이제 AS-IS UseOption 좌석(StackSkillInfos(OnUseOption)
    // + ActivateBackgroundEffects, PlayCardsBridge.cs)에서 옵션-사용 창을 연다 — 수동 펌프 플레이 경로
    // (PlayCardClass → UseOptionClass, CardController.cs:4277-4279)와 동일 좌석. 배틀에어리어 리액터
    // (P_223의 OnUseOption ActivateClass)가 수집·드레인되어 [Pipe Fox] 토큰 optional 프롬프트가 열리고
    // 플레이된다(정책 OptionalEffect 좌석이 수락). 이전 bare TriggerEventEmitter.Emit은 펌프 드레인에
    // 스택되지 않아 미발화였음.
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Any(id => CardNumberOf(match, id) == "BT19-040-token"),
        "RD-EXT1-03: the effect-driven option play opened the OnUseOption window at the AS-IS StackSkillInfos seat — " +
        "P_223's battle-area [All Turns] OnUseOption reactor fired and played the [Pipe Fox] Token to the battle area");
}

async Task P223_NoReductionAtFourSecurity()
{
    (DcgoMatch match, PolicyChoiceProvider unusedPolicy) = await NewExemplarMatchAsync(seed: 1602, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 딜된 5장을 비우고 정확히 4장으로 고정(경계: 4 > 3 → 감소 없음).
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Library);
    for (int i = 0; i < 4; i++)
    {
        StageSynthetic(match, P1, "EXT1-SEC" + i, dp: 1000, level: 3, $"1:sec:{i}", zone: ChoiceZone.Security);
    }

    HeadlessEntityId kuzuha = Stage(match, P1, "P_223", ChoiceZone.Hand, "1:hand:Kuzuhamon");
    AssertTrue(FindLane(match, P1, HeadlessActionTypes.PlayCard, kuzuha) is null,
        "with 4 security the -4 reduction is off: cost 11 exceeds the max payable 10 — no PlayCard lane");
}

async Task P223_NameRuleSakuyamon()
{
    (DcgoMatch match, PolicyChoiceProvider unusedPolicy) = await NewExemplarMatchAsync(seed: 1603, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId kuzuha = Stage(match, P1, "P_223", ChoiceZone.BattleArea, "1:battle:Kuzuhamon", register: true);
    HeadlessEntityId other = StageSynthetic(match, P1, "EXT1-OTHER", dp: 3000, level: 3, "1:battle:other");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var kuzuhaSource = new Cec.CardSource(match.Context, kuzuha, P1);
    var otherSource = new Cec.CardSource(match.Context, other, P1);
    AssertTrue(kuzuhaSource.EqualsCardName("Sakuyamon"),
        "ChangeCardNamesClass: P_223 is ALSO treated as [Sakuyamon]");
    AssertTrue(kuzuhaSource.EqualsCardName("Kuzuhamon"), "the printed name stays");
    AssertTrue(!otherSource.EqualsCardName("Sakuyamon"),
        "negative: the name grant is scoped to P_223 itself (cardSource == card)");
}

// ═══════════════════════════════════ harness ═══════════════════════════════════

PlayerDeckSetup[] MonoDecks(string p1Number, string p2Number) => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId(p1Number), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId(p2Number), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewExemplarMatchAsync(int seed, PlayerDeckSetup[] decks)
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

async Task ResolveChoosingAsync(DcgoMatch match, HeadlessEntityId targetId)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser)
            .First(a => a.ActionType == HeadlessActionTypes.ResolveChoice && ReadSelectedIds(a).Contains(targetId));
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

static IReadOnlyList<HeadlessEntityId> ReadSelectedIds(LegalAction action) =>
    action.Parameters.TryGetValue(HeadlessActionParameterKeys.ChoiceSelectedIds, out object? raw) && raw is IEnumerable<HeadlessEntityId> ids
        ? ids.ToArray()
        : Array.Empty<HeadlessEntityId>();

// 실카드 스테이징: cards.json 로더가 이미 def를 넣었으므로(def id = 카드번호) 인스턴스만 만들어 이동.
// register: true → CardEffectRegistrar.RegisterCard (배틀에어리어 효과원). C-Del/R4RL-03 Place 관례.
HeadlessEntityId Stage(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone, string instanceId,
    bool register = false, string? cardType = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId(cardNumber);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? existing) || existing is null)
    {
        throw new InvalidOperationException($"definition {cardNumber} not found in the loaded card database");
    }

    _ = cardType;
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

// (RD-EXT1-02) Stamp a battle-area option instance as a played-option permanent (AS-IS
// Permanent.IsPlayedOptionPermanent, set by PlaceDelayOptionCards). Exempts the option from the no-DP rule
// sweep and makes it a declarable field permanent — the state a directly-staged delay option must replicate.
void MarkPlayedOptionPermanent(DcgoMatch match, HeadlessEntityId id)
{
    if (match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null)
    {
        var meta = new Dictionary<string, object?>(rec.Metadata, StringComparer.Ordinal)
        {
            [GameFlowProcessor.IsPlayedOptionPermanentKey] = true,
        };
        match.Context.CardInstanceRepository.Upsert(rec with { Metadata = meta });
    }
}

// 합성 픽스처 카드(R4S3b StageBattleDigimon 관례 확장): def 업서트 + 인스턴스 + 존 이동.
HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null, Dictionary<string, object?>? extraDefMeta = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
    }

    if (extraDefMeta is not null)
    {
        foreach ((string k, object? v) in extraDefMeta)
        {
            meta[k] = v;
        }
    }

    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name ?? number, meta, CardType: cardType));
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

static bool DebugCanEvolve(DcgoMatch match, HeadlessEntityId cardId, HeadlessEntityId hostId)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var cs = new Cec.CardSource(match.Context, cardId, new HeadlessPlayerId(1));
    var host = new Cec.Permanent(match.Context, hostId, new HeadlessPlayerId(1));
    return cs.CanEvolve(host, true);
}

static int DebugPayingCost(DcgoMatch match, HeadlessEntityId cardId, bool avail)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.CardSource(match.Context, cardId, new HeadlessPlayerId(1))
        .PayingCost(ScriptSelectCardEffect.Root.Hand, null!, checkAvailability: avail);
}

static string DebugChangeCost(DcgoMatch match, HeadlessEntityId cardId)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var cs = new Cec.CardSource(match.Context, cardId, new HeadlessPlayerId(1));
    var parts = new List<string>();
    foreach (Cec.ICardEffect e in cs.EffectList(Cec.EffectTiming.None))
    {
        if (e is Cec.IChangeCostEffect cc)
        {
            string got;
            try { got = cc.GetCost(11, cs, ScriptSelectCardEffect.Root.Hand, null!).ToString(); }
            catch (Exception ex) { got = "EX:" + ex.GetType().Name; }
            string canUse;
            try { canUse = e.CanUse(null!) + "/trig:" + e.CanTrigger(null!) + "/act:" + e.CanActivate(null!); }
            catch (Exception ex) { canUse = "EX:" + ex.GetType().Name + ":" + ex.Message; }
            parts.Add($"{e.EffectName}(canUse:{canUse} cardCond:{cc.CardCondition(cs)} chkAvail:{cc.IsCheckAvailability()} paying:{cc.IsChangePayingCost()} getCost11:{got})");
        }
    }
    parts.Add("beforePay:[" + string.Join(",", cs.EffectList(Cec.EffectTiming.BeforePayCost).Select(x => x.GetType().Name + ":" + x.EffectName)) + "]");
    return string.Join(" ; ", parts);
}

static string DebugNoneEffects(DcgoMatch match, HeadlessEntityId cardId)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var cs = new Cec.CardSource(match.Context, cardId, new HeadlessPlayerId(1));
    return string.Join(",", cs.EffectList(Cec.EffectTiming.None).Select(e => e.GetType().Name + ":" + e.EffectName));
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

static HeadlessEntityId? TopCardOf(DcgoMatch match, HeadlessEntityId permanentId)
{
    // digivolve 후 존-상주 id는 진화 결과 톱 카드로 재키될 수 있음 — 상주 id의 톱 카드를 읽는다.
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    if (!match.Context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? record) || record is null)
    {
        return null;
    }

    var p = new Cec.Permanent(match.Context, permanentId, record.OwnerId);
    return p.TopCard?.InstanceId;
}

static bool HasAlliance(DcgoMatch match, HeadlessEntityId permanentId)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    if (!match.Context.CardInstanceRepository.TryGetInstance(permanentId, out CardInstanceRecord? record) || record is null)
    {
        return false;
    }

    return new Cec.Permanent(match.Context, permanentId, record.OwnerId).HasAlliance;
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

static bool IsSuspendedMeta(DcgoMatch match, HeadlessEntityId cardId)
{
    return match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null
        && record.Metadata.TryGetValue("isSuspended", out object? raw) && raw is true;
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

/// <summary>에이전트 좌석: 술어-매칭 스크립트 답변 + ScriptedChoiceProvider 동일 폴백(검증 포함).
/// R4RL-01의 Enqueue 관례를 술어형으로 일반화 — 효과-내부 Select*/Optional 프롬프트를 결정론적으로 응답.</summary>
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

/// <summary>EngineContext.CreateDefault의 1:1 재현 — provider 좌석만 교체(그 외 배선 동일).
/// CreateDefault(EngineContext.cs:373-436)와 시그니처/구성 요소를 라인 대조로 맞춤.</summary>
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
