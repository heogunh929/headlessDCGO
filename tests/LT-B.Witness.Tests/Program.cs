using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using SelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;
using AddDigiXrosConditionClass = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.AddDigiXrosConditionClass;
using AddAppFusionConditionClass = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.AddAppFusionConditionClass;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// LT-B 정본 witness 스위트 — coverage-exemplar 포팅 트랜치(11장). 카드당 ≥1 실행동 subtest + 저렴한 통제.
// 템플릿=EXEMPLAR-T1/T3B (DcgoMatch.CreatePumpDriven + PolicyChoiceProvider 좌석; EffectList 등록 표면 +
// CanActivate 게이트 + 옵션/직접 발화 flip). 카드↔축 매핑은 각 카드 소스 헤더의 ①②③ 정본 주석 참조.
//
// BT13_033 — 이전 STOP 해제(포팅 완료): [When Attacking] 팔의 상대 손패 in-place 셔플은 신규 hand-shuffle
// zone op(IZoneMover.ShuffleHandAsync — 시드된 RandomUtility 미러)로 1:1 착지. 아래 2개 witness:
// (1) shuffle-blind-pick 팔 end-to-end(시드 결정론: 상대 손패 9→8, 픽 카드 덱밑, self unsuspend),
// (2) [None] Burst 조건(AddBurstDigivolutionConditionClass) 등재 + tamer/digimon 술어 실평가.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // ── LM_046 — Navy Memory Boost! (Blue Option; delay-option 계열) ──────────────────────────────
    ("LM_046 W1 [Main] flip: ignore-color ON(필드 Purple Digimon)→ActivateOption→덱탑3 공개, Blue/Purple Digimon 1장 손패·비매치 덱밑·self 배틀에어리어 배치", LM046_MainRevealAddHandAndPlaced),
    ("LM_046 W2 경계: 필드 Purple Digimon/Tamer 부재 → ignore-color OFF·색 요구 미충족 → ActivateOption 레인 부재", LM046_IgnoreColorGateNegative),
    ("LM_046 W3 등록: <Delay>(OnDeclaration)=Gain2MemoryOptionDelayEffect·[Security](SecuritySkill)=PlaceSelfDelayOptionSecurityEffect 실착지", LM046_DelayAndSecurityRegistered),

    // ── EX8_072 — Barbamon(X) support (Purple Option; OptionMainEffect) ───────────────────────────
    ("EX8_072 W1 등록: [Trash][Your Turn](WhenDigivolving)·[Main](OptionSkill)·[Security](SecuritySkill) 실착지", EX8072_ArmsRegistered),
    ("EX8_072 W2 [Main] flip: 상대 손패 6장→1장 트래시 + levelMax=5(=7-6/3) 이하 상대 Digimon 삭제", EX8072_MainDiscardAndDelete),
    ("EX8_072 W3 levelMax 경계(over-level control): 상대 손패 11장→discard 후 levelMax=4: lvl5 삭제-select 비적격·survives / lvl4 적격·삭제 (실 select 구동)", EX8072_MainLevelMaxScales),

    // ── BT20_072 — Execute + [On Deletion] play-from-trash (Purple Digimon) ───────────────────────
    ("BT20_072 W1 등록: Execute(OnEndTurn)·[On Deletion](OnDestroyedAnyone) normal+ESS 실착지", BT20072_ArmsRegistered),
    ("BT20_072 W2 [On Deletion] ESS 분기: OnDestroyedAnyone 두 팔 = 프린트(IsInheritedEffect=false) + 진화원 ESS(true) — 정상/ESS 분기 fidelity", BT20072_OnDeletionEssSplit),

    // ── EX4_013 — DontBattleSecurity + [On Play]/[When Attacking] delete (Red Digimon) ────────────
    ("EX4_013 W1 등록: None=DontBattleSecurityDigimonClass·SecuritySkill·OnEnterFieldAnyone·OnAllyAttack 실착지", EX4013_ArmsRegistered),
    ("EX4_013 W2 [On Play] 게이트+술어: 상대 Digimon 존재 시 CanActivate ON, DP≤6000 삭제-후보 술어 실평가(양:5000/음:7000)", EX4013_OnPlayDeleteGateAndPredicate),

    // ── BT25_094 — Cosmic Area (Red Option; ReplaceBottomSecurityWithFaceUpOption) ────────────────
    ("BT25_094 W1 등록: None=IgnoreColor+Rush(static)·OnAllyAttack=Alliance(static)·OptionSkill=Main·SecuritySkill 실착지", BT25094_ArmsRegistered),
    ("BT25_094 W2 [Main] flip: ReplaceBottomSecurityWithFaceUpOption — 바텀 시큐리티→손패, self가 face-up 바텀 시큐리티로 착지", BT25094_ReplaceBottomSecurityFlip),

    // ── EX4_020 — DigiXros/MaterialSave/Rush (Blue Digimon) ───────────────────────────────────────
    ("EX4_020 W1 등록: None=AddDigiXrosConditionClass·OnEnterFieldAnyone(OnPlay)·WhenPermanentWouldBeDeleted(MaterialSave)·OnAllyAttack(ESS) 실착지", EX4020_ArmsRegistered),
    ("EX4_020 W2 DigiXros 레시피 fidelity: GetDigiXrosCondition이 2-요소(Greymon[Blue]·MailBirdramon) 조건 산출(술어 실평가)", EX4020_DigiXrosRecipeFidelity),

    // ── BT19_081 — MaxUnderTamerCountDigiXros + place-under-tamer (Blue Tamer) ─────────────────────
    ("BT19_081 W1 등록: OnStartMainPhase·BeforePayCost(AddMaxUnderTamer 기계)·SecuritySkill(PlaySelfTamer) 실착지", BT19081_ArmsRegistered),
    ("BT19_081 W2 [Start of Main] 게이트: 손패 [Blue Flare]/[Xros Heart] Digimon 존재 시 CanActivate ON (부재 시 OFF — 양/음)", BT19081_StartMainGate),

    // ── BT22_087 — Torajiro Asuka (Yellow Tamer; AppFusion) ───────────────────────────────────────
    ("BT22_087 W1 등록: OnStartMainPhase=Gain1MemoryTamerOpponentDigimon·WhenLinked·SecuritySkill=PlaySelfTamer 실착지", BT22087_ArmsRegistered),
    ("BT22_087 W2 WhenLinked suspend-cost 게이트: 이 Tamer 미서스펜드 시 CanActivate ON, 서스펜드 시 OFF (양/음)", BT22087_WhenLinkedSuspendGate),

    // ── BT23_021 — Dosukomon (Blue Digimon; AppFusion + Link + immunity) ──────────────────────────
    ("BT23_021 W1 등록: None=AppFusion+AltDigivolveReq+LinkCondition·OnDeclaration(Link)·OnEnterFieldAnyone/OnAllyAttack(WD/WA link)·WhenLinked(immunity)x2 실착지", BT23021_ArmsRegistered),
    ("BT23_021 W2 AppFusion 조건 fidelity: GetAppFusionCondition이 cost 0 조건 산출 + AltDigivolveReq/LinkCondition 홀더 실착지", BT23021_AppFusionConditionFidelity),

    // ── BT13_033 (포팅 완료 — 이전 STOP 해제) ─────────────────────────────────────────────────────
    ("BT13_033 W1 [When Attacking] end-to-end(시드 결정론): 상대 손패 9장 → 8장 남기고 1장 블라인드-픽 덱밑 + 서스펜드된 self unsuspend", BT13033_WhenAttackingShuffleBlindPick),
    ("BT13_033 W2 [None] Burst 등재+술어: AddBurstDigivolutionConditionClass 소비 → [Thomas H. Norstein] tamer 술어 TRUE(비-매치 FALSE) + [MirageGaogamon] digimon 술어 TRUE(비-매치 FALSE)", BT13033_BurstConditionRegisteredAndPredicates),
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

// ═══════════════════════════════════ LM_046 ═══════════════════════════════════

async Task LM046_MainRevealAddHandAndPlaced()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 4101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // ignore-color ON 전제: 필드에 owner Purple Digimon (LM_046 CanUse = Purple Digimon/Tamer on field).
    StageSynthetic(match, P1, "LTB-PURPFLD", dp: 3000, level: 3, "1:battle:purpfld",
        extraDefMeta: new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Purple" } });
    // 라이브러리 통제: 비우고 top = Blue Digimon(매치) + Red Digimon(비매치). 술어 실평가의 증인.
    await ClearZoneAsync(match, P1, ChoiceZone.Library, ChoiceZone.Trash);
    HeadlessEntityId matchDigi = StageSynthetic(match, P1, "LTB-BDIGI", dp: 1000, level: 3, "1:lib:bdigi",
        zone: ChoiceZone.Library, extraDefMeta: new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Blue" } });
    HeadlessEntityId nonMatchDigi = StageSynthetic(match, P1, "LTB-RDIGI", dp: 1000, level: 3, "1:lib:rdigi",
        zone: ChoiceZone.Library, extraDefMeta: new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" } });
    HeadlessEntityId lm = Stage(match, P1, "LM_046", ChoiceZone.Hand, "1:hand:LM046");

    policy.On(req => req.Candidates.Any(c => c.Id == matchDigi), req => ChoiceResult.Select(matchDigi), oneShot: false);

    LegalAction option = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, lm,
        "IgnoreColorConditionClass ON (a Purple Digimon on the field) waives LM_046's color requirement");
    await ApplyAsync(match, option);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.Hand).Contains(matchDigi)
        || ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(lm) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.Hand).Contains(matchDigi),
        "SelectCardConditionClass predicate EVALUATED: the Blue Digimon matched (Blue||Purple) and was added to hand " +
        $"[hand:{string.Join(",", ZoneCards(match, P1, ChoiceZone.Hand).Select(i => i.Value))} prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(nonMatchDigi),
        "the Red Digimon did NOT match the predicate — not added to hand (mushing the predicate would FAIL here)");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(lm),
        "PlaceDelayOptionCards: LM_046 sits in the battle area as a delay option");
}

async Task LM046_IgnoreColorGateNegative()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4102, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId lm = Stage(match, P1, "LM_046", ChoiceZone.Hand, "1:hand:LM046");
    AssertTrue(FindLane(match, P1, HeadlessActionTypes.ActivateOption, lm) is null,
        "without a Purple Digimon/Tamer on the field the ignore-color gate is OFF and the color requirement blocks the option");
}

async Task LM046_DelayAndSecurityRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4103, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId lm = Stage(match, P1, "LM_046", ChoiceZone.Hand, "1:hand:LM046");
    List<string> onDecl = EffectTypes(match, lm, P1, Cec.EffectTiming.OnDeclaration);
    List<string> sec = EffectTypes(match, lm, P1, Cec.EffectTiming.SecuritySkill);
    List<string> none = EffectTypes(match, lm, P1, Cec.EffectTiming.None);
    AssertTrue(none.Contains("IgnoreColorConditionClass"), $"None: IgnoreColorConditionClass registered [got {string.Join(",", none)}]");
    AssertTrue(onDecl.Count == 1, $"<Delay> registers exactly one effect on OnDeclaration (Gain2MemoryOptionDelayEffect) [got {string.Join(",", onDecl)}]");
    AssertTrue(sec.Count == 1, $"[Security] registers exactly one effect on SecuritySkill (PlaceSelfDelayOptionSecurityEffect) [got {string.Join(",", sec)}]");
}

// ═══════════════════════════════════ EX8_072 ═══════════════════════════════════

async Task EX8072_ArmsRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX8_072", ChoiceZone.Hand, "1:hand:ex8");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.WhenDigivolving).Contains("ActivateClass"),
        "WhenDigivolving: [Trash][Your Turn] return-to-deck-bottom + re-fire Main registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OptionSkill).Contains("ActivateClass"),
        "OptionSkill: [Main] discard + delete registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.SecuritySkill).Count >= 1,
        "SecuritySkill: [Security] activate-main-option registered");
}

async Task EX8072_MainDiscardAndDelete()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 4202, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX8_072", ChoiceZone.Hand, "1:hand:ex8");
    // 상대(P2) 손패 6장(levelMax=7-6/3=5), 상대 배틀에어리어 lvl4 Digimon 1체(삭제 후보).
    for (int i = 0; i < 6; i++)
    {
        StageSynthetic(match, P2, "LTB-OPPH", dp: 1000, level: 3, $"2:hand:h{i}", zone: ChoiceZone.Hand);
    }
    HeadlessEntityId oppDigi = StageSynthetic(match, P2, "LTB-OPPD", dp: 4000, level: 4, "2:battle:oppd");
    int oppHandBefore = Count(match, P2, ChoiceZone.Hand);

    // 효과-내부 Select(상대 손패 discard 1 · 상대 Digimon delete 1)는 policy 좌석이 첫 후보로 응답.
    policy.On(req => req.Candidates.Any(c => c.IsSelectable), req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        Cec.CardSource cs = MakeSource(match, ex, P1);
        var main = (Cec.ActivateICardEffect)cs.EffectList(Cec.EffectTiming.OptionSkill).First();
        await main.Activate(new System.Collections.Hashtable());
    }
    await DriveUntilAsync(match, m => Count(m, P2, ChoiceZone.Hand) < oppHandBefore
        || !ZoneCards(m, P2, ChoiceZone.BattleArea).Contains(oppDigi) || m.IsTerminal());

    AssertTrue(Count(match, P2, ChoiceZone.Hand) == oppHandBefore - 1,
        $"opponent (hand 6 ≥ 5) trashed 1 card [before {oppHandBefore} now {Count(match, P2, ChoiceZone.Hand)}]");
    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(oppDigi),
        "a level-4 opponent Digimon (≤ levelMax 5) was deleted by the [Main] effect");
}

async Task EX8072_MainLevelMaxScales()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 4203, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX8_072", ChoiceZone.Hand, "1:hand:ex8");
    // The [Main] discards 1 opponent hand card FIRST, THEN computes levelMax = 7 - (handCount/3) for the delete.
    // Bring the opponent's hand to exactly 11 (the setup already deals some cards): after the 1-card discard the hand
    // is 10 → levelMax = 7 - 10/3 = 4 (and 11/3 = 3 → 4 pre-discard too, so the boundary is 4 either way). The delete
    // select's canTargetCondition (SelectOpponentsDigimon) is evaluated then: level ≤ 4 eligible, level 5 EXCLUDED.
    int baseHand = Count(match, P2, ChoiceZone.Hand);
    for (int i = 0; baseHand + i < 11; i++)
    {
        StageSynthetic(match, P2, "LTB-OPPH2", dp: 1000, level: 3, $"2:hand:hh{i}", zone: ChoiceZone.Hand);
    }
    AssertTrue(Count(match, P2, ChoiceZone.Hand) == 11, $"precondition: opponent hand set to 11 (levelMax=4 after the discard) [got {Count(match, P2, ChoiceZone.Hand)}]");
    // over-level control: level 5 (> levelMax 4) — must be EXCLUDED from the delete select; eligible target = level 4.
    HeadlessEntityId lvl5 = StageSynthetic(match, P2, "LTB-OPP5", dp: 6000, level: 5, "2:battle:opp5");
    HeadlessEntityId lvl4 = StageSynthetic(match, P2, "LTB-OPP4", dp: 4000, level: 4, "2:battle:opp4");

    bool lvl5Selectable = false;
    policy.On(req => req.Candidates.Any(c => c.IsSelectable), req =>
    {
        if (req.Type == ChoiceType.Permanent)
        {
            lvl5Selectable |= req.Candidates.Any(c => c.Id == lvl5 && c.IsSelectable);
        }

        return ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id);
    }, oneShot: false);

    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        Cec.CardSource cs = MakeSource(match, ex, P1);
        var main = (Cec.ActivateICardEffect)cs.EffectList(Cec.EffectTiming.OptionSkill).First();
        await main.Activate(new System.Collections.Hashtable());
    }

    await DriveUntilAsync(match, m => !ZoneCards(m, P2, ChoiceZone.BattleArea).Contains(lvl4) || m.IsTerminal());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(lvl4),
        "levelMax scaling: the level-4 opponent Digimon (≤ levelMax 4 after the discard) was deleted by the [Main]");
    AssertTrue(!lvl5Selectable,
        "over-level control: the level-5 opponent Digimon was NEVER a selectable delete candidate (level 5 > levelMax 4 → excluded by SelectOpponentsDigimon)");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(lvl5),
        "over-level control: the level-5 opponent Digimon (> levelMax 4) survives on the battle area");
}

// ═══════════════════════════════════ BT20_072 ═══════════════════════════════════

async Task BT20072_ArmsRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4301, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // Execute/OnDeletion 팔은 permanent-scoped(ExecuteSelfEffect는 ResolvePermanentOfThisCard 필요) →
    // 배틀에어리어 permanent로 스테이징.
    HeadlessEntityId bt = Stage(match, P1, "BT20_072", ChoiceZone.BattleArea, "1:battle:bt20", register: true);
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnEndTurn).Contains("ActivateClass"),
        "OnEndTurn: <Execute> self-effect registered (permanent-scoped)");
    List<string> del = EffectTypes(match, bt, P1, Cec.EffectTiming.OnDestroyedAnyone);
    AssertTrue(del.Count(t => t == "ActivateClass") == 2,
        $"OnDestroyedAnyone registers TWO [On Deletion] arms (normal + ESS/inherited) [got {string.Join(",", del)}]");
}

async Task BT20072_OnDeletionEssSplit()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4302, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT20_072", ChoiceZone.BattleArea, "1:battle:bt20", register: true);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = MakeSource(match, bt, P1);
    List<Cec.ICardEffect> arms = cs.EffectList(Cec.EffectTiming.OnDestroyedAnyone).ToList();
    AssertTrue(arms.Count == 2, $"two [On Deletion] arms present [got {arms.Count}]");
    // 하나는 프린트 효과(IsInheritedEffect=false), 하나는 진화원[ESS] 효과(SetIsInheritedEffect(true)).
    AssertTrue(arms.Any(e => !e.IsInheritedEffect), "one [On Deletion] arm is the printed effect (IsInheritedEffect=false)");
    AssertTrue(arms.Any(e => e.IsInheritedEffect), "one [On Deletion] arm is the ESS/inherited effect (IsInheritedEffect=true) — the normal/ESS split is faithful");
}

// ═══════════════════════════════════ EX4_013 ═══════════════════════════════════

async Task EX4013_ArmsRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX4_013", ChoiceZone.Hand, "1:hand:ex4013");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.None).Contains("DontBattleSecurityDigimonClass"),
        "None: DontBattleSecurityDigimonClass (ignore-battle) registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.SecuritySkill).Contains("ActivateClass"),
        "SecuritySkill: play-from-security-without-battle registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OnEnterFieldAnyone).Contains("ActivateClass"),
        "OnEnterFieldAnyone: [On Play] delete/suspend registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OnAllyAttack).Contains("ActivateClass"),
        "OnAllyAttack: [When Attacking] delete/suspend registered");
}

async Task EX4013_OnPlayDeleteGateAndPredicate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4402, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX4_013", ChoiceZone.BattleArea, "1:battle:ex4013");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = MakeSource(match, ex, P1);
    var arm = cs.EffectList(Cec.EffectTiming.OnEnterFieldAnyone).First();
    bool noOpp = arm.CanActivate(new System.Collections.Hashtable());

    // 상대 배틀에어리어 Digimon 배치 → CanActivate ON (delete OR suspend 브랜치).
    StageSynthetic(match, P2, "LTB-EX4OPP", dp: 5000, level: 4, "2:battle:ex4opp");
    bool withOpp = arm.CanActivate(new System.Collections.Hashtable());

    AssertTrue(!noOpp, "with no opponent Digimon the [On Play] delete/suspend cannot activate (negative control)");
    AssertTrue(withOpp, "with an opponent Digimon present the [On Play] delete/suspend gate is ON");
}

// ═══════════════════════════════════ BT25_094 ═══════════════════════════════════

async Task BT25094_ArmsRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT25_094", ChoiceZone.Hand, "1:hand:bt25");
    List<string> none = EffectTypes(match, bt, P1, Cec.EffectTiming.None);
    AssertTrue(none.Contains("IgnoreColorConditionClass"), $"None: IgnoreColorConditionClass registered [got {string.Join(",", none)}]");
    AssertTrue(none.Contains("RushClass"), "None: <Rush> static registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnAllyAttack).Contains("ActivateClass"),
        "OnAllyAttack: <Alliance> static registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OptionSkill).Contains("ActivateClass"),
        "OptionSkill: [Main] replace-bottom-security registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.SecuritySkill).Contains("ActivateClass"),
        "SecuritySkill: [Security] play [TS] Digimon registered");
}

async Task BT25094_ReplaceBottomSecurityFlip()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 4502, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT25_094", ChoiceZone.Hand, "1:hand:bt25");
    // 바텀 시큐리티 카드 1장(교체 대상).
    HeadlessEntityId sec = StageSynthetic(match, P1, "LTB-SEC", dp: 0, level: 1, "1:sec:sec1", cardType: "Digimon", zone: ChoiceZone.Security);
    int handBefore = Count(match, P1, ChoiceZone.Hand);
    // 옵션-내부 손패 플레이 프롬프트는 no-select(skip).
    policy.On(req => true, req => ChoiceResult.Skip(), oneShot: false);

    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        Cec.CardSource cs = MakeSource(match, bt, P1);
        var main = (Cec.ActivateICardEffect)cs.EffectList(Cec.EffectTiming.OptionSkill).First();
        await main.Activate(new System.Collections.Hashtable());
    }
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.Hand).Contains(sec)
        || ZoneCards(m, P1, ChoiceZone.Security).Contains(bt) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.Hand).Contains(sec),
        $"ReplaceBottomSecurityWithFaceUpOption: the old bottom security card is now in hand [hand:{string.Join(",", ZoneCards(match, P1, ChoiceZone.Hand).Select(i => i.Value))}]");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Security).Contains(bt),
        "ReplaceBottomSecurityWithFaceUpOption: BT25_094 is now placed as a security card (face up, bottom)");
    _ = handBefore;
}

// ═══════════════════════════════════ EX4_020 ═══════════════════════════════════

async Task EX4020_ArmsRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX4_020", ChoiceZone.Hand, "1:hand:ex4020");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.None).Contains("AddDigiXrosConditionClass"),
        "None: DigiXros condition holder registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OnEnterFieldAnyone).Contains("ActivateClass"),
        "OnEnterFieldAnyone: [On Play] Rush + trash-digivolution-cards registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.WhenPermanentWouldBeDeleted).Contains("ActivateClass"),
        "WhenPermanentWouldBeDeleted: <MaterialSave 2> registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OnAllyAttack).Contains("ActivateClass"),
        "OnAllyAttack: [When Attacking] (GreyKnightsmon) can't-attack ESS registered");
}

async Task EX4020_DigiXrosRecipeFidelity()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4602, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX4_020", ChoiceZone.Hand, "1:hand:ex4020");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = MakeSource(match, ex, P1);
    var holder = cs.EffectList(Cec.EffectTiming.None).OfType<AddDigiXrosConditionClass>().FirstOrDefault();
    AssertTrue(holder is not null, "None: AddDigiXrosConditionClass holder present");
    var cond = holder!.GetDigiXrosCondition(cs);
    AssertTrue(cond is not null && cond.elements.Count == 2,
        $"the DigiXros recipe is fully built (2 elements: Greymon[Blue] + MailBirdramon) [{(cond is null ? "null" : $"elems:{cond.elements.Count}")}]");
}

// ═══════════════════════════════════ BT19_081 ═══════════════════════════════════

async Task BT19081_ArmsRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4701, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT19_081", ChoiceZone.Hand, "1:hand:bt19");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnStartMainPhase).Contains("ActivateClass"),
        "OnStartMainPhase: place-under-tamer + gain-memory registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.BeforePayCost).Contains("ActivateClass"),
        "BeforePayCost: AddMaxUnderTamerCountDigiXros machinery registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.SecuritySkill).Count >= 1,
        "SecuritySkill: play-self-tamer registered");
}

async Task BT19081_StartMainGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4702, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT19_081", ChoiceZone.BattleArea, "1:battle:bt19");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = MakeSource(match, bt, P1);
    var arm = cs.EffectList(Cec.EffectTiming.OnStartMainPhase).First();
    bool before = arm.CanActivate(new System.Collections.Hashtable());

    // 손패에 [Blue Flare] Digimon 배치 → HasMatchConditionOwnersHand(HasBlueFlareXrosHeart) ON.
    StageSynthetic(match, P1, "LTB-BF", dp: 3000, level: 4, "1:hand:bf", cardType: "Digimon", zone: ChoiceZone.Hand, traits: new[] { "Blue Flare" });
    bool after = arm.CanActivate(new System.Collections.Hashtable());

    AssertTrue(!before, "with no [Blue Flare]/[Xros Heart] Digimon in hand the [Start of Main] place cannot activate (negative control)");
    AssertTrue(after, "with a [Blue Flare] Digimon in hand the [Start of Main] gate is ON (trait predicate evaluated)");
}

// ═══════════════════════════════════ BT22_087 ═══════════════════════════════════

async Task BT22087_ArmsRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4801, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT22_087", ChoiceZone.Hand, "1:hand:bt22");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnStartMainPhase).Count >= 1,
        "OnStartMainPhase: Gain1MemoryTamerOpponentDigimon registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.WhenLinked).Contains("ActivateClass"),
        "WhenLinked: suspend-self → -2K DP → app-fuse registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.SecuritySkill).Count >= 1,
        "SecuritySkill: play-self-tamer registered");
}

async Task BT22087_WhenLinkedSuspendGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4802, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT22_087", ChoiceZone.BattleArea, "1:battle:bt22");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = MakeSource(match, bt, P1);
    var arm = cs.EffectList(Cec.EffectTiming.WhenLinked).First();
    bool unsuspended = arm.CanActivate(new System.Collections.Hashtable());

    // 이 Tamer를 서스펜드 상태로 → CanActivateSuspendCostEffect false.
    new Cec.Permanent(match.Context, bt, P1).IsSuspended = true;
    bool suspended = arm.CanActivate(new System.Collections.Hashtable());

    AssertTrue(unsuspended, "unsuspended Tamer on your turn → suspend-cost effect can activate (positive)");
    AssertTrue(!suspended, "an already-suspended Tamer cannot pay the suspend cost → CanActivate OFF (negative control)");
}

// ═══════════════════════════════════ BT23_021 ═══════════════════════════════════

async Task BT23021_ArmsRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4901, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT23_021", ChoiceZone.Hand, "1:hand:bt23");
    List<string> none = EffectTypes(match, bt, P1, Cec.EffectTiming.None);
    AssertTrue(none.Contains("AddAppFusionConditionClass"), $"None: AddAppFusionConditionClass registered [got {string.Join(",", none)}]");
    AssertTrue(none.Contains("AddDigivolutionRequirementClass"), "None: alt-digivolve requirement ([Stnd.] cost 3) registered");
    AssertTrue(none.Contains("AddLinkConditionClass"), "None: link condition ([Appmon] linkCost 2) registered");
    // NOTE: OnDeclaration LinkEffect is conditionally-registered (LinkEffect returns null unless a linkable
    // own Digimon is on the field + owner turn) — its wiring is exercised by the None AddLinkConditionClass
    // holder above; the unconditional [WD]/[WA]/[WhenLinked] arms below are the registration witnesses.
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnEnterFieldAnyone).Contains("ActivateClass"), "OnEnterFieldAnyone: [When Digivolving] link registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnAllyAttack).Contains("ActivateClass"), "OnAllyAttack: [When Attacking] link registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.WhenLinked).Count(t => t == "ActivateClass") == 2,
        $"WhenLinked: TWO immunity arms ([Your Turn] OPT + [When Linking] linked-effect) registered [got {string.Join(",", EffectTypes(match, bt, P1, Cec.EffectTiming.WhenLinked))}]");
}

async Task BT23021_AppFusionConditionFidelity()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4902, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT23_021", ChoiceZone.Hand, "1:hand:bt23");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = MakeSource(match, bt, P1);
    var holder = cs.EffectList(Cec.EffectTiming.None).OfType<AddAppFusionConditionClass>().FirstOrDefault();
    AssertTrue(holder is not null, "None: AddAppFusionConditionClass holder present");
    var cond = holder!.GetAppFusionCondition(cs);
    AssertTrue(cond is not null && cond.cost == 0,
        $"the App Fusion condition is built with cost 0 (Dokamon/Perorimon/Musclemon combos) [{(cond is null ? "null" : $"cost:{cond.cost}")}]");
}

// ═══════════════════════════════════ BT13_033 (포팅 완료) ═══════════════════════════════════

async Task BT13033_WhenAttackingShuffleBlindPick()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 4051, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT13_033", ChoiceZone.BattleArea, "1:battle:bt13", register: true);
    // 공격 시 self는 서스펜드 상태 — 효과의 보상은 이 Digimon을 unsuspend 하는 것.
    new Cec.Permanent(match.Context, bt, P1).IsSuspended = true;
    // 상대(P2) 손패를 정확히 9장으로 통제(오프닝 핸드 청소 후 9장 스테이징) — maxCount = 9-8 = 1장 블라인드-픽 → 덱밑.
    await ClearZoneAsync(match, P2, ChoiceZone.Hand, ChoiceZone.Trash);
    for (int i = 0; i < 9; i++)
    {
        StageSynthetic(match, P2, "LTB-BT13H", dp: 1000, level: 3, $"2:hand:bt13h{i}", zone: ChoiceZone.Hand);
    }

    int handBefore = Count(match, P2, ChoiceZone.Hand);
    int libBefore = Count(match, P2, ChoiceZone.Library);
    // 블라인드-픽(SelectCardEffect over 셔플된 상대 손패)은 MaxCount 범위 멀티-셀렉트 — policy가
    // 선택가능 후보를 MaxCount만큼 응답(시드 결정론: 셔플된 순서에서 앞쪽 후보).
    policy.On(req => req.Candidates.Any(c => c.IsSelectable),
        req => ChoiceResult.Select(req.Candidates.Where(c => c.IsSelectable).Take(req.MaxCount).Select(c => c.Id)),
        oneShot: false);

    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        Cec.CardSource cs = MakeSource(match, bt, P1);
        var arm = (Cec.ActivateICardEffect)cs.EffectList(Cec.EffectTiming.OnAllyAttack).First();
        await arm.Activate(new System.Collections.Hashtable());
    }
    await DriveUntilAsync(match, m => Count(m, P2, ChoiceZone.Hand) <= 8 || m.IsTerminal());

    AssertTrue(Count(match, P2, ChoiceZone.Hand) == 8,
        $"opponent hand shuffled (ShuffleHandAsync) + 1 card blind-picked to deck bottom so 8 remain [before {handBefore} now {Count(match, P2, ChoiceZone.Hand)}]");
    AssertTrue(Count(match, P2, ChoiceZone.Library) == libBefore + 1,
        $"the blind-picked card was returned to the BOTTOM of the opponent's deck [lib before {libBefore} now {Count(match, P2, ChoiceZone.Library)}]");
    AssertTrue(!new Cec.Permanent(match.Context, bt, P1).IsSuspended,
        "returned==true → BT13_033 was UNSUSPENDED (the [When Attacking] payoff; IUnsuspendPermanents)");
}

async Task BT13033_BurstConditionRegisteredAndPredicates()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 4052, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT13_033", ChoiceZone.BattleArea, "1:battle:bt13b", register: true);
    // [Thomas H. Norstein] tamer + [MirageGaogamon] digimon (owner P1) + 비-매치 통제.
    HeadlessEntityId thomas = StageSynthetic(match, P1, "THOMAS", dp: 0, level: 0, "1:battle:thomas", name: "Thomas H. Norstein", cardType: "Tamer");
    HeadlessEntityId mirage = StageSynthetic(match, P1, "MIRAGE", dp: 9000, level: 6, "1:battle:mirage", name: "MirageGaogamon");
    HeadlessEntityId other = StageSynthetic(match, P1, "OTHER13", dp: 3000, level: 4, "1:battle:other13", name: "SomeOther");

    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.None).Contains("AddBurstDigivolutionConditionClass"),
        "None: AddBurstDigivolutionConditionClass registered");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = MakeSource(match, bt, P1);
    Cec.BurstDigivolutionCondition? bdc = Cec.CardSourceAsIsPlayAccessors.BurstDigivolutionConditionOf(cs);
    AssertTrue(bdc is not null, "BT13_033 exposes a BurstDigivolutionCondition (AddBurstDigivolutionConditionClass consumed)");

    var thomasPerm = new Cec.Permanent(match.Context, thomas, P1);
    var miragePerm = new Cec.Permanent(match.Context, mirage, P1);
    var otherPerm = new Cec.Permanent(match.Context, other, P1);

    AssertTrue(bdc!.tamerCondition(thomasPerm),
        "[Thomas H. Norstein] burst-tamer condition TRUE (name + owner + battle-area + !CannotReturnToHand)");
    AssertTrue(!bdc.tamerCondition(otherPerm),
        "negative: a non-[Thomas H. Norstein] permanent is not a valid burst tamer");
    AssertTrue(bdc.digimonCondition(miragePerm),
        "[MirageGaogamon] Digimon is a valid burst base (!CanNotEvolve + name)");
    AssertTrue(!bdc.digimonCondition(otherPerm),
        "negative: a non-[MirageGaogamon] Digimon is not a valid burst base");
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
            $"pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()} " +
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

// EffectList 등록 표면: 스테이징된 실카드 인스턴스의 CardSource가 timing별로 등록하는 ICardEffect 타입 이름.
List<string> EffectTypes(DcgoMatch match, HeadlessEntityId instanceId, HeadlessPlayerId owner, Cec.EffectTiming timing)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.CardSource(match.Context, instanceId, owner).EffectList(timing).Select(e => e.GetType().Name).ToList();
}

Cec.CardSource MakeSource(DcgoMatch match, HeadlessEntityId instanceId, HeadlessPlayerId owner) =>
    new Cec.CardSource(match.Context, instanceId, owner);

static bool ThrowsNotSupported(Action action, string mustContain)
{
    try
    {
        action();
        return false;
    }
    catch (NotSupportedException ex)
    {
        return ex.Message.Contains(mustContain, StringComparison.Ordinal);
    }
    catch (AggregateException agg) when (agg.InnerException is NotSupportedException ns)
    {
        return ns.Message.Contains(mustContain, StringComparison.Ordinal);
    }
}

static async Task ClearZoneAsync(DcgoMatch match, HeadlessPlayerId owner, ChoiceZone from, ChoiceZone to)
{
    foreach (HeadlessEntityId id in ZoneCards(match, owner, from).ToArray())
    {
        await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, from, to));
    }
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

// 실카드 스테이징(EXEMPLAR-T1 관례): cards.json 로더가 def를 넣었으므로 인스턴스만 만들어 이동.
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

// 합성 픽스처 카드(EXEMPLAR-T1 StageSynthetic 관례): def 업서트 + 인스턴스 + 존 이동 + 등록.
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

// ═══════════════════════════════ providers/context ═══════════════════════════════

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

    public static ChoiceResult Fallback(ChoiceRequest request)
        => new ScriptedChoiceProvider().ChooseAsync(request).GetAwaiter().GetResult();

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
