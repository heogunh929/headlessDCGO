using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using SelectDigiXrosClass = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectDigiXrosClass;
using SelectAssemblyClass = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectAssemblyClass;
using AddAssemblyConditionClass = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.AddAssemblyConditionClass;
using SelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;
using IPlacePermanentToDigivolutionCards = HeadlessDCGO.Engine.Assets.Scripts.Script.IPlacePermanentToDigivolutionCards;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3B 정본 witness 스위트 — 수확 트랜치 3B(STOP-예상 5장), 카드당 실행동/등록/정직 수확 혼합.
// 템플릿=EXEMPLAR-T1(DcgoMatch.CreatePumpDriven + 에이전트 액션/PolicyChoiceProvider 좌석). 이 트랜치의 성격은
// "수확 레인": ①포팅 가능한 팔은 1:1 포팅(등록 실착지를 EffectList 표면으로 실증) ②갭에 부딪히는 팔은 정확한
// STOP + 요구사항 명세 수확(NotSupportedException 마커; witness는 assert-반전 내장 — 상환 시 뒤집을 것).
// 카드/축 매핑은 각 카드 소스 헤더(①②③ 정본 주석)와 docs/audit/coverage_exemplar_audit_2026-07-18.md §4.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // LM_047 — Chartreuse Memory Boost! (클린 풀-포팅; 고급 SelectCardConditionClass 술어 실평가)
    ("LM_047 W1 [Main] fidelity: ignore-color ON(필드 녹 Digimon)→ActivateOption→덱탑3 공개, 고급 SelectCardConditionClass 술어가 황/녹 Digimon만 손패로(비매치는 덱밑) — 술어 실평가", LM047_MainRevealSelectDigimonFidelity),
    ("LM_047 W2 경계: 필드 녹 Digimon/Tamer 부재 + 황색 공급 부재 → ignore-color OFF·색 요구 미충족 → ActivateOption 레인 부재", LM047_IgnoreColorGateNegative),
    ("LM_047 W3 등록: <Delay>(OnDeclaration)=Gain2MemoryOptionDelayEffect·[Security](SecuritySkill)=PlaceSelfDelayOptionSecurityEffect 실착지", LM047_DelayAndSecurityRegistered),
    // EX10_045 — Tuwarmon (다중 팔 포팅; DigiXros 소비만 잠복 STOP)
    ("EX10_045 W1 등록: None=AltDigi+DigiXros홀더+Rush, OnCounterTiming=Collision, OnDestroyedAnyone=<Save>, OnDigivolutionCardDiscarded=ESS, OP/WD/WA 공유 3키 — 전 팔 실착지", EX10045_ArmsRegisteredAcrossTimings),
    ("EX10_045 W2 착지(RD-EXT3-01 상환): DigiXros 소비 파이프 하우징 — SelectDigiXrosClass.Select가 STOP 없이 완주(빈 보드=소재 0)", EX10045_DigiXrosConsumptionHoused),
    // BT24_062 — MasterBlimpmon (다중 팔 포팅; Assembly 소비만 잠복 STOP)
    ("BT24_062 W1 등록: None=AltDigi+Blocker+Assembly홀더+CanNotSwitch, WhenPermanentWouldBeDeleted=ArmorPurge, OnEndAttack/OnEndTurn 공유 — 전 팔 실착지", BT24062_ArmsRegisteredAcrossTimings),
    ("BT24_062 W2 착지(RD-EXT3-02 상환): Assembly 등록 홀더가 buildable AssemblyCondition(reduceCost 2)를 산출 + 인터랙티브 Assembly 소비 실행(트래시 [TS] Tamer 선정→BT24_062 진화원으로 스택)", BT24062_AssemblyConsumes),
    // BT21_030 — Shoutmon X7: Superior Mode (다중 팔 포팅; DigiXros/BeforePayCost 잠복)
    ("BT21_030 W1 등록: None=AltDigi+DigiXros홀더, BeforePayCost=DigiXros특수(잠복), OnEnterFieldAnyone/WhenDigivolving/OnAllyAttack 실착지", BT21030_ArmsRegisteredAcrossTimings),
    ("BT21_030 W2 착지(RD-EXT3-01 상환): DigiXros 인터랙티브 소비 실행 — 손패 [Xros Heart] 소재 선정(area choice→Hand)→BT21_030 진화원으로 스택 형성", BT21030_DigiXrosConsumes),
    // BT3_056 — Ceresmon (수확 STOP 상환 RD-EXT3-03 — 실단언 flip)
    ("BT3_056 W1 발화: <Digisorption -3> BeforePayCost 진입→서스펜드 수락 시 진화 코스트 -3 실적용(base 5→2), 세 팔(BeforePayCost/WhenDigisorption/None) 실착지", BT3056_DigisorptionAcceptReducesBy3),
    ("BT3_056 W2 발화: 서스펜드 거절(no-select) 시 ChangeCost 미등록 → 정가 유지(base 5→5)", BT3056_DigisorptionDeclineFullPrice),
    ("BT3_056 W3 경계: 서스펜드 가용성 없음(유일 아군 디지몬 서스펜드 상태) → 옵션 CanActivate false·정가 유지 — CanTapWhenAbsorbEvolution_CheckAvailability 가용성 판정", BT3056_DigisorptionNoAvailabilityNoOption),
    // RD-EXT3-05 — 필드-소재 재부모화 (IPlacePermanentToDigivolutionCards)
    ("RD-EXT3-05 착지: 필드 디지몬을 DigiXros 소재로 → 톱 카드가 호스트 아래로 재부모화·원 permanent 소멸·언더스택 트래시(DiscardEvoRoots)·북키핑 Reset(RD-R3-02 DIE)", EXT305_FieldMaterialReparents),
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

// ═══════════════════════════════════ RD-EXT3-05 ═══════════════════════════════════

async Task EXT305_FieldMaterialReparents()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // the DigiXros host permanent (the play card, already on field).
    HeadlessEntityId host = StageSynthetic(match, P1, "XROS-HOST", dp: 5000, level: 5, "1:battle:host", name: "XrosHost");
    // a field-material Digimon permanent WITH a digivolution (under) card — proves DiscardEvoRoots trashes the
    // under-stack while only the material's TOP card re-parents (AS-IS semantics).
    HeadlessEntityId matTop = StageSynthetic(match, P1, "XROS-MATTOP", dp: 3000, level: 4, "1:battle:mattop", name: "XrosMatTop");
    HeadlessEntityId matUnder = StageSynthetic(match, P1, "XROS-MATUNDER", dp: 1000, level: 3, "1:battle:matunder", name: "XrosMatUnder", zone: ChoiceZone.None);
    // attach matUnder under matTop (the field material's stack).
    if (match.Context.CardInstanceRepository.TryGetInstance(matTop, out CardInstanceRecord? rec) && rec is not null)
    {
        var meta = new Dictionary<string, object?>(rec.Metadata, StringComparer.Ordinal) { [DigivolutionStackReader.SourceIdsKey] = new[] { matUnder.Value } };
        match.Context.CardInstanceRepository.Upsert(rec with { Metadata = meta });
    }

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var hostPerm = new Cec.Permanent(match.Context, host, P1);
    var matPerm = new Cec.Permanent(match.Context, matTop, P1);
    AssertTrue(matPerm.DigivolutionCards.Any(c => c.InstanceId == matUnder), "fixture: the field material has an under-card (matUnder)");
    // stamp bookkeeping on the material permanent to prove it is Reset when the source dies.
    matPerm.IsBurstDigivolved = true;

    // RD-EXT3-05: re-parent the field material's top card under the host via IPlacePermanentToDigivolutionCards.
    var place = new IPlacePermanentToDigivolutionCards(
        new List<Cec.Permanent[]> { new[] { matPerm, hostPerm } }, false, null, isDigixros: true);
    place.SetNotShowCards();
    await place.PlacePermanentToDigivolutionCards();

    AssertTrue(place.Placed, "RD-EXT3-05: the re-parent executed (Placed == true)");
    var hostAfter = new Cec.Permanent(match.Context, host, P1);
    AssertTrue(hostAfter.DigivolutionCards.Any(c => c.InstanceId == matTop),
        "the field material's TOP card is now a digivolution (under) card of the host permanent");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(matTop),
        "the field material's original permanent is GONE from the battle area (소재 소멸)");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Trash).Contains(matUnder),
        "the material's under-stack was trashed (DiscardEvoRoots) — only its top card re-parented");
    AssertTrue(!new Cec.Permanent(match.Context, matTop, P1).IsBurstDigivolved,
        "북키핑 정리: the dead source permanent's bookkeeping (IsBurstDigivolved) was Reset at the field-leave chokepoint (RD-R3-02 DIE)");
}

// ═══════════════════════════════════ LM_047 ═══════════════════════════════════

async Task LM047_MainRevealSelectDigimonFidelity()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 3101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // ignore-color ON 전제: 필드에 owner 녹색 Digimon 배치(LM_047 CanUse = 녹 Digimon/Tamer on field).
    StageSynthetic(match, P1, "EXT3-GREENFLD", dp: 3000, level: 3, "1:battle:greenfld",
        extraDefMeta: new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Green" } });
    // 라이브러리 통제: 비우고 top = 녹 Digimon(매치) + 적 Digimon(비매치). 술어 실평가의 증인.
    await ClearZoneAsync(match, P1, ChoiceZone.Library, ChoiceZone.Trash);
    HeadlessEntityId matchDigi = StageSynthetic(match, P1, "EXT3-GDIGI", dp: 1000, level: 3, "1:lib:gdigi",
        zone: ChoiceZone.Library, extraDefMeta: new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Green" } });
    HeadlessEntityId nonMatchDigi = StageSynthetic(match, P1, "EXT3-RDIGI", dp: 1000, level: 3, "1:lib:rdigi",
        zone: ChoiceZone.Library, extraDefMeta: new Dictionary<string, object?>(StringComparer.Ordinal) { ["colors"] = new[] { "Red" } });
    HeadlessEntityId lm = Stage(match, P1, "LM_047", ChoiceZone.Hand, "1:hand:LM047");

    // 효과-내부 SelectCardEffect(AddHand 패스): 매치 녹 Digimon 선택.
    policy.On(req => req.Candidates.Any(c => c.Id == matchDigi), req => ChoiceResult.Select(matchDigi), oneShot: false);

    LegalAction option = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, lm,
        "IgnoreColorConditionClass ON (a Green Digimon on the field) waives LM_047's Yellow color requirement");
    await ApplyAsync(match, option);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.Hand).Contains(matchDigi)
        || ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(lm) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.Hand).Contains(matchDigi),
        "SelectCardConditionClass predicate EVALUATED (fidelity): the Green Digimon matched (Yellow||Green) and was added to hand " +
        $"[debug hand:{string.Join(",", ZoneCards(match, P1, ChoiceZone.Hand).Select(i => i.Value))} prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(nonMatchDigi),
        "the Red Digimon did NOT match the predicate (not Yellow/Green) — not added to hand (mushing the predicate would FAIL here)");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(lm),
        "PlaceDelayOptionCards: LM_047 sits in the battle area as a delay option");
}

async Task LM047_IgnoreColorGateNegative()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3102, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 필드에 녹 Digimon/Tamer 없음 + 필드 황색 공급 없음 → ignore-color OFF·색 요구(Yellow) 미충족 → 레인 부재.
    HeadlessEntityId lm = Stage(match, P1, "LM_047", ChoiceZone.Hand, "1:hand:LM047");
    AssertTrue(FindLane(match, P1, HeadlessActionTypes.ActivateOption, lm) is null,
        "without a Green Digimon/Tamer on the field the ignore-color gate is OFF and the Yellow color requirement blocks the option");
}

async Task LM047_DelayAndSecurityRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3103, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId lm = Stage(match, P1, "LM_047", ChoiceZone.Hand, "1:hand:LM047");

    List<string> onDecl = EffectTypes(match, lm, P1, Cec.EffectTiming.OnDeclaration);
    List<string> sec = EffectTypes(match, lm, P1, Cec.EffectTiming.SecuritySkill);
    AssertTrue(onDecl.Count == 1,
        $"<Delay> arm registers exactly one effect on OnDeclaration (Gain2MemoryOptionDelayEffect) [got {string.Join(",", onDecl)}]");
    AssertTrue(sec.Count == 1,
        $"[Security] arm registers exactly one effect on SecuritySkill (PlaceSelfDelayOptionSecurityEffect) [got {string.Join(",", sec)}]");
}

// ═══════════════════════════════════ EX10_045 ═══════════════════════════════════

async Task EX10045_ArmsRegisteredAcrossTimings()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX10_045", ChoiceZone.Hand, "1:hand:ex10");

    List<string> none = EffectTypes(match, ex, P1, Cec.EffectTiming.None);
    AssertTrue(none.Contains("AddDigivolutionRequirementClass"), $"None: AddSelfDigivolutionRequirement ([Damemon]) registered [got {string.Join(",", none)}]");
    AssertTrue(none.Contains("AddDigiXrosConditionClass"), "None: DigiXros condition holder registered (ports as data holder)");
    AssertTrue(none.Contains("RushClass"), "None: <Rush> registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OnCounterTiming).Contains("CollisionClass"), "OnCounterTiming: <Collision> registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OnDestroyedAnyone).Contains("ActivateClass"), "OnDestroyedAnyone: <Save> (SaveEffect ActivateClass) registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OnDigivolutionCardDiscarded).Contains("ActivateClass"), "OnDigivolutionCardDiscarded: ESS <Draw 1> registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OnEnterFieldAnyone).Contains("ActivateClass"), "OnEnterFieldAnyone: [On Play] shared registered");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.WhenDigivolving).Contains("ActivateClass"), "WhenDigivolving: [When Digivolving] shared registered (mirror dedicated key)");
    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.OnAllyAttack).Contains("ActivateClass"), "OnAllyAttack: [When Attacking] shared registered");
}

async Task EX10045_DigiXrosConsumptionHoused()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3202, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex = Stage(match, P1, "EX10_045", ChoiceZone.Hand, "1:hand:ex10");
    Cec.CardSource cs = MakeSource(match, ex, P1);

    AssertTrue(EffectTypes(match, ex, P1, Cec.EffectTiming.None).Contains("AddDigiXrosConditionClass"),
        "port half: the DigiXros condition holder is registered on None");

    // 상환(RD-EXT3-01): 이 홀더를 소비하는 인터랙티브 DigiXros 재료-선택 SelectDigiXrosClass.Select가 하우징됨
    // (Permanent.CanSubstituteForDigiXrosCondition 포팅 + SelectHandEffect 미러 실착지). 빈 보드(소재 부재)에서
    // Select는 STOP 없이 완주하고 소재를 0장 고른다 — 소비 파이프의 착지(음영 케이스) 증인.
    using AmbientMatchContext.Scope _scope = AmbientMatchContext.Enter(match.Context);
    var selectDigiXros = new SelectDigiXrosClass();
    await selectDigiXros.Select(cs);
    AssertTrue(selectDigiXros.selectedDigicrossCards.Count == 0,
        "LANDED RD-EXT3-01: SelectDigiXrosClass.Select runs to completion (no STOP) — with no eligible material on a bare board it selects nothing");
}

// ═══════════════════════════════════ BT24_062 ═══════════════════════════════════

async Task BT24062_ArmsRegisteredAcrossTimings()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3301, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT24_062", ChoiceZone.Hand, "1:hand:bt24");

    List<string> none = EffectTypes(match, bt, P1, Cec.EffectTiming.None);
    AssertTrue(none.Contains("AddDigivolutionRequirementClass"), $"None: AddSelfDigivolutionRequirement (Lv.4 [TS]/Machine/Cyborg) registered [got {string.Join(",", none)}]");
    AssertTrue(none.Contains("BlockerClass"), "None: <Blocker> registered");
    AssertTrue(none.Contains("AddAssemblyConditionClass"), "None: Assembly condition holder registered (ports as data holder)");
    AssertTrue(none.Contains("CanNotSwitchAttackTargetClass"), "None: ESS can't-switch-attack-target registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.WhenPermanentWouldBeDeleted).Contains("ActivateClass"), "WhenPermanentWouldBeDeleted: <Armor Purge> (ArmorPurgeEffect) registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnEndAttack).Contains("ActivateClass"), "OnEndAttack: shared play-from-source registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnEndTurn).Contains("ActivateClass"), "OnEndTurn: shared play-from-source registered");
}

async Task BT24062_AssemblyConsumes()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 3302, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // BT24_062 already ON the battle area (a permanent) so the Assembly material can stack under it.
    HeadlessEntityId bt = Stage(match, P1, "BT24_062", ChoiceZone.BattleArea, "1:battle:bt24");
    // an Assembly material in the OWNER'S TRASH: a Tamer with the [TS] trait (the element matches
    // [Blimpmon] OR a Tamer Card w/ [TS] trait).
    HeadlessEntityId mat = StageSynthetic(match, P1, "TSMAT", dp: 0, level: 1, "1:trash:tsmat",
        cardType: "Tamer", zone: ChoiceZone.Trash, traits: new[] { "TS" });

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = new(match.Context, bt, P1);

    // port half: the registration holder still builds the full AssemblyCondition (reduceCost 2, 1 element).
    var holder = cs.EffectList(Cec.EffectTiming.None).OfType<AddAssemblyConditionClass>().FirstOrDefault();
    AssertTrue(holder is not null, "port half: AddAssemblyConditionClass holder present on None");
    Cec.AssemblyCondition? cond = holder!.GetAssemblyCondition(cs);
    AssertTrue(cond is not null && cond.reduceCost == 2 && cond.elements.Count == 1,
        $"the Assembly condition is fully built (reduceCost 2, 1 element) [cond:{(cond is null ? "null" : $"reduce{cond.reduceCost}/elems{cond.elements.Count}")}]");

    // 상환(RD-EXT3-02): 이 조건을 소비하는 인터랙티브 Assembly 플레이가 하우징됨 — SelectAssemblyClass 인스턴스
    // 표면(Select→SelectTrashCard→AddDigivolutiuonCards). 트래시 SelectCardEffect가 [TS] Tamer를 고른다.
    policy.On(req => req.Type == ChoiceType.Card && req.Candidates.Any(c => c.Id == mat),
        req => ChoiceResult.Select(mat), oneShot: false);

    var selectAssembly = new SelectAssemblyClass();
    await selectAssembly.Select(cs);
    AssertTrue(selectAssembly.selectedAssemblyCards.Any(c => c.InstanceId == mat),
        "Assembly material selection lands: the [TS] Tamer in trash is chosen as the Assembly material (predicate evaluated 1:1)");

    await selectAssembly.AddDigivolutiuonCards(cs);
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Trash).Contains(mat),
        "the material left the trash (consumed as an Assembly material)");
    AssertTrue(new Cec.Permanent(match.Context, bt, P1).DigivolutionCards.Any(c => c.InstanceId == mat),
        "LANDED RD-EXT3-02: the material is now a digivolution card UNDER BT24_062 (Assembly stack formed)");
}

// ═══════════════════════════════════ BT21_030 ═══════════════════════════════════

async Task BT21030_ArmsRegisteredAcrossTimings()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT21_030", ChoiceZone.Hand, "1:hand:bt21");

    List<string> none = EffectTypes(match, bt, P1, Cec.EffectTiming.None);
    AssertTrue(none.Contains("AddDigivolutionRequirementClass"), $"None: AddSelfDigivolutionRequirement (Lv.6 [Hero]) registered [got {string.Join(",", none)}]");
    AssertTrue(none.Contains("AddDigiXrosConditionClass"), "None: DigiXros condition holder registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.BeforePayCost).Contains("ActivateClass"), "BeforePayCost: DigiXros-special cost machinery registered (latent — fires only inside a DigiXros play)");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnEnterFieldAnyone).Contains("ActivateClass"), "OnEnterFieldAnyone: [On Play] trash-stack registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.WhenDigivolving).Contains("ActivateClass"), "WhenDigivolving: trash-stack registered (mirror dedicated key)");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnAllyAttack).Contains("ActivateClass"), "OnAllyAttack: [When Attacking] deck-bottom-bounce registered");
}

async Task BT21030_DigiXrosConsumes()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 3402, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // BT21_030 already ON the battle area (a permanent) so the selected DigiXros material can stack under it.
    HeadlessEntityId bt = Stage(match, P1, "BT21_030", ChoiceZone.BattleArea, "1:battle:bt21");
    // a hand material matching the DigiXros element (a Digimon with the [Xros Heart] trait).
    HeadlessEntityId mat = StageSynthetic(match, P1, "XROSMAT", dp: 3000, level: 4, "1:hand:xrosmat",
        cardType: "Digimon", zone: ChoiceZone.Hand, traits: new[] { "Xros Heart" });

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = new(match.Context, bt, P1);

    // 상환(RD-EXT3-01): DigiXros 인터랙티브 소비가 하우징됨. area choice(ModeChoice)→Hand, 그다음
    // SelectHandEffect가 손패 [Xros Heart] 소재를 고른다.
    policy.On(req => req.Type == ChoiceType.ModeChoice,
        req => ChoiceResult.Select(req.Candidates.First(c => c.Id.Value == "digiXros#0").Id), oneShot: false);
    policy.On(req => req.Type == ChoiceType.Card && req.Candidates.Any(c => c.Id == mat),
        req => ChoiceResult.Select(mat), oneShot: false);

    var selectDigiXros = new SelectDigiXrosClass();
    await selectDigiXros.Select(cs);
    AssertTrue(selectDigiXros.selectedDigicrossCards.Any(c => c.InstanceId == mat),
        $"DigiXros material selection lands: the [Xros Heart] hand card is chosen (area-choice + hand-select ran) " +
        $"[selected:{string.Join(",", selectDigiXros.selectedDigicrossCards.Select(c => c.InstanceId.Value))} prompts:{string.Join(" | ", policy.Seen)}]");

    await selectDigiXros.AddDigivolutiuonCards(cs);
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(mat),
        "the material left the hand (stacked under the DigiXros permanent)");
    AssertTrue(new Cec.Permanent(match.Context, bt, P1).DigivolutionCards.Any(c => c.InstanceId == mat),
        "LANDED RD-EXT3-01: the material is now a digivolution card UNDER BT21_030 (DigiXros stack formed)");
}

// ═══════════════════════════════════ BT3_056 ═══════════════════════════════════

// (RD-EXT3-03 상환) BT3_056 셋업: BT3_056을 손패에(진화 대상 카드), 아군 배틀에어리어에 진화-원/서스펜드 후보
// 디지몬 1체. suspendedFrom=true면 그 디지몬을 서스펜드 상태로(가용성 없음 경계).
async Task<(DcgoMatch Match, PolicyChoiceProvider Policy, HeadlessEntityId Bt, HeadlessEntityId DigiFrom)>
    SetupDigisorptionAsync(int seed, bool suspendedFrom)
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT3_056", ChoiceZone.Hand, "1:hand:bt3");
    HeadlessEntityId digiFrom = StageSynthetic(match, P1, "EXT3-DIGIFROM", dp: 3000, level: 4, "1:battle:digifrom");

    if (suspendedFrom)
    {
        using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
        new Cec.Permanent(match.Context, digiFrom, P1).IsSuspended = true;
    }

    return (match, policy, bt, digiFrom);
}

// <Digisorption -3> BeforePayCost 진입 팔을 직접 발화(펌프-외 직접 호출 — EX10_045/BT24_062 판례와 동일한
// witness 소비 패턴): EffectList(BeforePayCost)에서 ActivateClass를 뽑아 Activate() → 서스펜드 SelectPermanent
// (Mode.Tap)를 policy가 수락/거절 → 그 뒤 실제 진화-코스트 파이프(GetPayingCostWithBaseCost)로 감액 결과를 대조.
async Task<int> FireDigisorptionAndCostAsync(
    DcgoMatch match, PolicyChoiceProvider policy, HeadlessEntityId bt, HeadlessEntityId digiFrom, int baseCost, bool accept)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var cs = new Cec.CardSource(match.Context, bt, P1);

    Cec.ICardEffect activate = cs.EffectList(Cec.EffectTiming.BeforePayCost).First();
    policy.On(
        req => req.Type == ChoiceType.Permanent,
        req => accept ? ChoiceResult.Select(digiFrom) : ChoiceResult.Skip(),
        oneShot: false);

    await ((Cec.ActivateICardEffect)activate).Activate(new System.Collections.Hashtable());

    // 진화-원 permanent를 targetPermanents로 넘겨 실제 코스트 파이프를 돌린다(ChangeCostClass는 진화-원이
    // 아군 배틀에어리어 permanent일 때만 -3; ChangeCostClass.GetCost의 CanReduceCost 게이트 포함).
    var targets = new List<Cec.Permanent> { new(match.Context, digiFrom, P1) };
    return cs.GetPayingCostWithBaseCost(baseCost, SelectCardEffect.Root.None, targets);
}

async Task BT3056_DigisorptionAcceptReducesBy3()
{
    (DcgoMatch match, PolicyChoiceProvider policy, HeadlessEntityId bt, HeadlessEntityId digiFrom) =
        await SetupDigisorptionAsync(seed: 3501, suspendedFrom: false);

    // 세 팔이 STOP 없이 실착지(등록 표면) — BeforePayCost=ActivateClass, WhenDigisorption=ActivateClass,
    // None=CanSuspendByDigisorptionClass.
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.BeforePayCost).Contains("ActivateClass"),
        $"BeforePayCost: <Digisorption -3> ActivateClass registered (STOP resolved) [got {string.Join(",", EffectTypes(match, bt, P1, Cec.EffectTiming.BeforePayCost))}]");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.WhenDigisorption).Contains("ActivateClass"),
        "WhenDigisorption: opponent-substitute ActivateClass registered");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.None).Contains("CanSuspendByDigisorptionClass"),
        "None: ESS CanSuspendByDigisorptionClass registered");

    int cost = await FireDigisorptionAndCostAsync(match, policy, bt, digiFrom, baseCost: 5, accept: true);
    AssertEqual(2, cost,
        "accepting the suspend registers the ChangeCostClass and the digivolution cost is reduced by 3 (5→2) — the option-suspend→conditional ChangeCost pipe fires 1:1");
}

async Task BT3056_DigisorptionDeclineFullPrice()
{
    (DcgoMatch match, PolicyChoiceProvider policy, HeadlessEntityId bt, HeadlessEntityId digiFrom) =
        await SetupDigisorptionAsync(seed: 3502, suspendedFrom: false);

    int cost = await FireDigisorptionAndCostAsync(match, policy, bt, digiFrom, baseCost: 5, accept: false);
    AssertEqual(5, cost,
        "declining the suspend (no-select) registers NO ChangeCostClass — the digivolution cost stays at full price (5)");
}

async Task BT3056_DigisorptionNoAvailabilityNoOption()
{
    (DcgoMatch match, PolicyChoiceProvider policy, HeadlessEntityId bt, HeadlessEntityId digiFrom) =
        await SetupDigisorptionAsync(seed: 3503, suspendedFrom: true);

    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        var cs = new Cec.CardSource(match.Context, bt, P1);
        Cec.ICardEffect activate = cs.EffectList(Cec.EffectTiming.BeforePayCost).First();
        // 유일 아군 디지몬이 서스펜드 상태 → CanTapWhenAbsorbEvolution_CheckAvailability false →
        // CanActivate false: 옵션 미제시.
        AssertTrue(!activate.CanActivate(new System.Collections.Hashtable()),
            "with the only own Digimon already suspended, CanTapWhenAbsorbEvolution_CheckAvailability is false so the <Digisorption -3> option is NOT presented (CanActivate false)");
    }

    // 옵션이 발화하지 않으면 감액도 없다 — 정가 유지(펌프-외 직접 발화 시 후보 0 → ChangeCost 미등록).
    int cost = await FireDigisorptionAndCostAsync(match, policy, bt, digiFrom, baseCost: 5, accept: true);
    AssertEqual(5, cost,
        "no suspendable Digimon ⇒ SelectPermanent has no candidate ⇒ no ChangeCostClass ⇒ full price (5)");
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
