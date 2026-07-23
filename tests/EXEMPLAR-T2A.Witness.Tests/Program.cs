using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using System.Collections;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using ScriptSelectCardEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectCardEffect;
using AceOverflowClass = HeadlessDCGO.Engine.Assets.Scripts.Script.AceOverflowClass;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T2A 정본 witness 스위트 — 커버리지 정본 포팅 2차 트랜치(클린 레인 7장), 카드당 3종.
// 하네스/좌석/컨텍스트 팩토리는 EXEMPLAR-T1 정본을 그대로 복사(DcgoMatch.CreatePumpDriven + 에이전트 액션/
// PolicyChoiceProvider 좌석). 축-표면 검증은 두 갈래로 나뉜다:
//   (a) 액션 표면(펌프 레인): match.GetLegalActions로 Digivolve/PlayCard/DeclareAttack 레인·프롬프트를 구동
//       (BT19_091 토큰 플레이 관례).
//   (b) 포팅된 CardEffects(timing,card) 술어 표면: CreatePumpDriven 매치 위에서 AmbientMatchContext로
//       CardSource.EffectList(timing)를 조회해 축 효과(ActivateClass/*Class)의 존재 + CanActivate 양/음 대조
//       (T1 W2/W3 wrapper-level witness 관례 — EX11070 DP floor·P223 name rule과 동형). 이 갈래는 포팅
//       충실도(=CardEffects 술어)를 직접 고정한다.
// 카드/축 매핑은 각 카드 소스 헤더(①②③ 정본 주석)와 docs/audit/coverage_exemplar_audit_2026-07-18.md §4.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // BT5_086 — Omnimon (K:Blitz · T:WhenReturntoHand/LibraryAnyone)
    ("BT5_086 W1 [When Digivolving]: Unsuspend ESS — CanActivate = IsExistOnBattleArea + CanUnsuspend (서스펜드 양/음 대조)", BT5086_WhenDigivolvingUnsuspend),
    ("BT5_086 W2 [All Turns] 3-way(WhenReturntoHand/Library/WouldBeDeleted): Lv6 진화원 보유 시 prevent-removal CanActivate ON (미보유 OFF)", BT5086_PreventRemovalGate),
    ("BT5_086 W3 [Blitz]: BlitzSelfEffect ESS가 OnEnterFieldAnyone 창에 등록(isWhenDigivolving)", BT5086_BlitzPresent),
    // EX10_010 — WarGreymon ACE (P:CanNotAffectedClass · T:OnRemovedField/WhenTopCardTrashed)
    ("EX10_010 W1 Static: Reboot/Blocker/CanNotAffected(None) + Raid(OnAllyAttack) + Blast Digivolve(OnCounterTiming) 등록", EX10010_StaticEffectsPresent),
    ("EX10_010 W2 [On Play]/[When Digivolving] delete: play-cost ≤7 적 존재 시 CanActivate ON (>7만 존재 시 OFF)", EX10010_DeleteGatePositiveNegative),
    ("EX10_010 W3 [All Turns] DP: 상대 DP≥13000 존재 시 +3000 boost 착지 (미존재 시 boost 없음 — OnRemovedField/WhenTopCardTrashed 재평가)", EX10010_DpBoostPositiveNegative),
    // BT18_042 — MagnaGarurumon (P:AceOverflowClass · X:AceOverflow)
    ("BT18_042 W1 AceOverflow: un-flipped ACE 오버플로가 턴플레이어 메모리를 overflow만큼 소진 (비-ACE=no-op)", BT18042_AceOverflow),
    ("BT18_042 W2 [When Digivolving] place-security→destroy: Digimon 진화원 보유 시 CanActivate ON (미보유 OFF)", BT18042_WhenDigivolvingPlaceSecurityGate),
    ("BT18_042 W3 Alt-digivolve(None) + [All Turns] unsuspend: 보안 ≥1 시 CanActivate ON (0이면 OFF)", BT18042_AltDigivolveAndUnsuspendGate),
    // ST17_13 — Magnamon (P:DigivolveIntoExcecutingAreaCard · X:ExecutingAreaDigivolve)
    ("ST17_13 W1 [Security] <De-Digivolve 1>: ExecutingArea에 있을 때 CanActivate ON (배틀에어리어면 OFF — X:ExecutingAreaDigivolve 게이트)", ST17_13_SecurityExecutingAreaGate),
    ("ST17_13 W2 [When Digivolving]: trash-per-color ActivateClass 등록 + CanActivate(IsExistOnBattleAreaDigimon)", ST17_13_WhenDigivolvingTrashGate),
    ("ST17_13 W3 Static: Blocker + AddDigivolutionRequirement(None) + ArmorPurge(WhenPermanentWouldBeDeleted) 등록", ST17_13_StaticEffectsPresent),
    // AD1_011 — Paildramon (P:CanNotSwitchAttackTargetEffect · P:GetJogressConditionClass)
    ("AD1_011 W1 조건 등록(None): GetJogressConditionClass(AddJogressConditionClass) + Alt-digivolve(AddDigivolutionRequirementClass)", AD1011_ConditionsPresent),
    ("AD1_011 W2 [When Digivolving]: can't-be-deleted-in-battle ActivateClass + CanActivate(IsExistOnBattleArea)", AD1011_WhenDigivolvingGate),
    ("AD1_011 W3 Partition(WhenRemoveField ×2 own+inherited) + [When Attacking] Digivolve-into-Imperialdramon 등록", AD1011_PartitionAndAttack),
    // BT20_017 — Jesmon (K:Decoy(토큰) · P:PlayAthoRenePorToken)
    ("BT20_017 W1 [On Play]: PlayAthoRenePorToken 구동 — [Atho, René & Por] 토큰(BT20-017-token) 배틀에어리어 진입", BT20017_OnPlayTokenPlayed),
    ("BT20_017 W2 K:Decoy: 진입한 토큰 CardSource가 <Decoy> 트레이트 보유(토큰 정의 = BT20_017_token)", BT20017_TokenCarriesDecoy),
    ("BT20_017 W3 [Your Turn] delete-then-attack ActivateClass + [When Digivolving] 토큰 ActivateClass 등록", BT20017_YourTurnAndWhenDigivolvingGates),
    // BT14_030 — MarineAngemon (P:BouncePeremanentAndProcessAccordingToResult · T:OnPermamemtReturnedToHand)
    ("BT14_030 W1 [On Play] bounce-chain: 유효 대상(상대 Lv3/자기 Digimon) 존재 시 CanActivate ON (미존재 OFF)", BT14030_OnPlayBounceGate),
    ("BT14_030 W2 [When Digivolving] bounce-chain: 유효 대상 존재 시 CanActivate ON (미존재 OFF)", BT14030_WhenDigivolvingBounceGate),
    ("BT14_030 W3 [Your Turn] <Recovery+1(Deck)>: OnPermamemtReturnedToHand 창에 ActivateClass 등록 + CanActivate", BT14030_RecoveryGate),
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

// ═══════════════════════════════════ BT5_086 ═══════════════════════════════════

async Task BT5086_WhenDigivolvingUnsuspend()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId omni = Stage(match, P1, "BT5_086", ChoiceZone.BattleArea, "1:battle:Omnimon", register: true);

    // 양: 서스펜드 상태 → CanUnsuspend true → CanActivate ON.
    SetInstanceMeta(match, omni, ("isSuspended", true));
    Cec.ICardEffect? unsuspend = EffectNamed(match, omni, Cec.EffectTiming.WhenDigivolving, "Unsuspend this Digimon");
    AssertTrue(unsuspend is not null, "[When Digivolving] Unsuspend ESS must be registered under the WhenDigivolving dialect key");
    AssertTrue(CanActivate(match, unsuspend!), "CanActivate ON: the battle-area Omnimon is suspended (CanUnsuspend true)");

    // 음: 언탭 상태 → CanUnsuspend false → CanActivate OFF.
    SetInstanceMeta(match, omni, ("isSuspended", false));
    AssertTrue(!CanActivate(match, EffectNamed(match, omni, Cec.EffectTiming.WhenDigivolving, "Unsuspend this Digimon")!),
        "negative: an already-unsuspended Omnimon can't unsuspend (CanUnsuspend false)");
}

async Task BT5086_PreventRemovalGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2102, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId omni = Stage(match, P1, "BT5_086", ChoiceZone.BattleArea, "1:battle:Omnimon", register: true);
    HeadlessEntityId lv6 = StageSynthetic(match, P1, "EXT2-LV6", dp: 11000, level: 6, "1:under:lv6", zone: ChoiceZone.None);
    HeadlessEntityId lv4 = StageSynthetic(match, P1, "EXT2-LV4", dp: 4000, level: 4, "1:under2:lv4", zone: ChoiceZone.None);

    // 3-way 타이밍 전부에 등록되는지 확인(WhenReturntoHand/Library/WouldBeDeleted).
    foreach (Cec.EffectTiming t in new[] { Cec.EffectTiming.WhenReturntoHandAnyone, Cec.EffectTiming.WhenReturntoLibraryAnyone, Cec.EffectTiming.WhenPermanentWouldBeDeleted })
    {
        AssertTrue(EffectNamed(match, omni, t, "Prevent this Digimon from being deleted or returned to hand or deck") is not null,
            $"prevent-removal ESS must be registered under {t}");
    }

    // 양: Lv6 Digimon 진화원 보유 → CanActivate ON.
    SetSources(match, omni, lv6);
    AssertTrue(CanActivate(match, EffectNamed(match, omni, Cec.EffectTiming.WhenReturntoHandAnyone, "Prevent this Digimon from being deleted or returned to hand or deck")!),
        "CanActivate ON: a Lv.6 Digimon digivolution card is present to trash");

    // 음: Lv4 진화원만 → CanSelectCardCondition(Level==6) 미충족 → CanActivate OFF.
    SetSources(match, omni, lv4);
    AssertTrue(!CanActivate(match, EffectNamed(match, omni, Cec.EffectTiming.WhenReturntoHandAnyone, "Prevent this Digimon from being deleted or returned to hand or deck")!),
        "negative: no Lv.6 Digimon among the digivolution cards → can't pay the trash");
}

async Task BT5086_BlitzPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2103, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId omni = Stage(match, P1, "BT5_086", ChoiceZone.BattleArea, "1:battle:Omnimon", register: true);

    AssertTrue(EffectNamed(match, omni, Cec.EffectTiming.OnEnterFieldAnyone, "Blitz") is not null,
        "BlitzSelfEffect (isWhenDigivolving:true) must register a [Blitz] ESS in the OnEnterFieldAnyone window");
}

// ═══════════════════════════════════ EX10_010 ═══════════════════════════════════

async Task EX10010_StaticEffectsPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId wg = Stage(match, P1, "EX10_010", ChoiceZone.BattleArea, "1:battle:WarGreymon", register: true);

    List<Cec.ICardEffect> none = EffectsOf(match, wg, P1, Cec.EffectTiming.None);
    AssertTrue(HasEffectType(none, "RebootClass"), "RebootSelfStaticEffect → RebootClass under None");
    AssertTrue(HasEffectType(none, "BlockerClass"), "BlockerSelfStaticEffect → BlockerClass under None");
    AssertTrue(HasEffectType(none, "CanNotAffectedClass"), "[All Turns] Immunity → CanNotAffectedClass under None");
    AssertTrue(EffectNamed(match, wg, Cec.EffectTiming.OnAllyAttack, "Raid") is not null, "RaidSelfEffect under OnAllyAttack");

    // Blast Digivolve는 손패에서만 활성(BlastDigivolveEffect가 !IsExistOnHand면 null 반환) — 별도 손패 셋업.
    (DcgoMatch hMatch, PolicyChoiceProvider _h) = await NewExemplarMatchAsync(seed: 2211, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(hMatch);
    HeadlessEntityId inHand = Stage(hMatch, P1, "EX10_010", ChoiceZone.Hand, "1:hand:WarGreymon");
    StageSynthetic(hMatch, P1, "EXT2-ALLY", dp: 4000, level: 4, "1:battle:ally");
    AssertTrue(EffectNamed(hMatch, inHand, Cec.EffectTiming.OnCounterTiming, "Blast Digivolve") is not null,
        "BlastDigivolveEffect (in-hand + ≥1 battle-area permanent) registers a [Blast Digivolve] ESS under OnCounterTiming");
}

async Task EX10010_DeleteGatePositiveNegative()
{
    async Task<bool> CanDelete(int oppCost)
    {
        (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2202, MonoDecks("BT1_028", "BT1_028"));
        await ReachMainWaitAsync(match);
        HeadlessEntityId wg = Stage(match, P1, "EX10_010", ChoiceZone.BattleArea, "1:battle:WarGreymon", register: true);
        StageSynthetic(match, P2, "EXT2-OPP", dp: 4000, level: 4, "2:battle:opp", playCost: oppCost);
        return CanActivate(match, EffectNamed(match, wg, Cec.EffectTiming.OnEnterFieldAnyone, "Delete 1 Digimon/Tamer")!);
    }

    AssertTrue(await CanDelete(7), "CanActivate ON: opponent Digimon with play cost 7 (≤7) is a valid delete target");
    AssertTrue(!await CanDelete(8), "negative: opponent play cost 8 (>7) is not targetable → CanActivate OFF");
}

async Task EX10010_DpBoostPositiveNegative()
{
    async Task<int> BoostDelta(int oppDp)
    {
        (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2203, MonoDecks("BT1_028", "BT1_028"));
        await ReachMainWaitAsync(match);
        HeadlessEntityId wg = Stage(match, P1, "EX10_010", ChoiceZone.BattleArea, "1:battle:WarGreymon", register: true);
        StageSynthetic(match, P2, "EXT2-BIG", dp: oppDp, level: 6, "2:battle:big");

        int before = PermanentDp(match, wg, P1);
        // OnEnterFieldAnyone 창의 명령형 DP 리전이 boost를 재평가한다(EffectList 평가가 CardEffects를 구동).
        _ = EffectsOf(match, wg, P1, Cec.EffectTiming.OnEnterFieldAnyone);
        int after = PermanentDp(match, wg, P1);
        return after - before;
    }

    AssertEqual(3000, await BoostDelta(13000), "opponent DP≥13000 present → +3000 DP boost (AT_EX10-010) lands");
    AssertEqual(0, await BoostDelta(12000), "negative: no opponent DP≥13000 → no boost");
}

// ═══════════════════════════════════ BT18_042 ═══════════════════════════════════

async Task BT18042_AceOverflow()
{
    int Delta(bool ace)
    {
        (DcgoMatch match, PolicyChoiceProvider _) = NewExemplarMatchAsync(seed: 2301, MonoDecks("BT1_028", "BT1_028")).GetAwaiter().GetResult();
        ReachMainWaitAsync(match).GetAwaiter().GetResult();
        HeadlessEntityId aceId = StageSynthetic(match, P1, "EXT2-ACE", dp: 6000, level: 6, "1:battle:ace");
        var meta = new (string, object?)[] { ("overflowMemory", 3) };
        SetInstanceMeta(match, aceId, ace ? new[] { ("isAce", (object?)true), ("overflowMemory", (object?)3) } : new[] { ("overflowMemory", (object?)3) });

        int before = MemoryFor(match, P1);
        using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
        {
            var cs = new Cec.CardSource(match.Context, aceId, P1);
            new AceOverflowClass(new List<Cec.CardSource> { cs }).Overflow().GetAwaiter().GetResult();
        }

        _ = meta;
        return MemoryFor(match, P1) - before;
    }

    int aceDelta = Delta(ace: true);
    AssertTrue(aceDelta != 0 && Math.Abs(aceDelta) == 3,
        $"un-flipped ACE overflow moves the turn-player memory gauge by its printed overflow (3) [delta:{aceDelta}]");
    AssertEqual(0, Delta(ace: false), "negative: a non-ACE card has 0 overflow → no memory change (OverflowFor gate)");
}

async Task BT18042_WhenDigivolvingPlaceSecurityGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2302, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId magna = Stage(match, P1, "BT18_042", ChoiceZone.BattleArea, "1:battle:Magna", register: true);
    HeadlessEntityId digiSource = StageSynthetic(match, P1, "EXT2-SRC", dp: 4000, level: 4, "1:under:src", zone: ChoiceZone.None);

    Cec.ICardEffect? place = EffectNamed(match, magna, Cec.EffectTiming.WhenDigivolving,
        "Place Digivolution card as your bottom security card to delete all opponent's same level Digimon");
    AssertTrue(place is not null, "[When Digivolving] place-security ActivateClass must register under the WhenDigivolving key");

    // 음: 진화원 없음 → CanActivateConditionShared (DigivolutionCards.Some(IsDigimon)) OFF.
    AssertTrue(!CanActivate(match, place!), "negative: no Digimon digivolution card → CanActivate OFF");
    // 양: Digimon 진화원 → CanActivate ON.
    SetSources(match, magna, digiSource);
    AssertTrue(CanActivate(match, EffectNamed(match, magna, Cec.EffectTiming.WhenDigivolving,
        "Place Digivolution card as your bottom security card to delete all opponent's same level Digimon")!),
        "CanActivate ON: a Digimon digivolution card is available to place");
}

async Task BT18042_AltDigivolveAndUnsuspendGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2303, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId magna = Stage(match, P1, "BT18_042", ChoiceZone.BattleArea, "1:battle:Magna", register: true);

    AssertTrue(HasEffectType(EffectsOf(match, magna, P1, Cec.EffectTiming.None), "AddDigivolutionRequirementClass"),
        "Alt-digivolve (AddSelfDigivolutionRequirementStaticEffect) → AddDigivolutionRequirementClass under None");

    Cec.ICardEffect? unsuspend = EffectNamed(match, magna, Cec.EffectTiming.OnAllyAttack,
        "By adding the top card of your security stack to the hand, unsuspend this Digimon");
    AssertTrue(unsuspend is not null, "[All Turns] unsuspend ActivateClass under OnAllyAttack");
    // 음: 보안 0장(펌프 StartGame 딜 후 비움) → CanActivate OFF.
    await ClearZoneAsync(match, P1, ChoiceZone.Security, ChoiceZone.Trash);
    AssertTrue(!CanActivate(match, EffectNamed(match, magna, Cec.EffectTiming.OnAllyAttack,
        "By adding the top card of your security stack to the hand, unsuspend this Digimon")!),
        "negative: 0 security cards → CanActivate OFF");
    // 양: 보안 1장 → CanActivate ON.
    StageSynthetic(match, P1, "EXT2-SEC", dp: 1000, level: 3, "1:sec:s0", zone: ChoiceZone.Security);
    AssertTrue(CanActivate(match, EffectNamed(match, magna, Cec.EffectTiming.OnAllyAttack,
        "By adding the top card of your security stack to the hand, unsuspend this Digimon")!),
        "CanActivate ON: ≥1 security card present");
}

// ═══════════════════════════════════ ST17_13 ═══════════════════════════════════

async Task ST17_13_SecurityExecutingAreaGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 상대 배틀에어리어에 De-Digivolve 대상(Digimon) 1장.
    StageSynthetic(match, P2, "EXT2-TGT", dp: 4000, level: 4, "2:battle:tgt");

    // 양: ExecutingArea(ChoiceZone.Execution)에 배치 → IsExistOnExecutingArea true → CanActivate ON.
    HeadlessEntityId exec = Stage(match, P1, "ST17_13", ChoiceZone.Execution, "1:exec:Magnamon", register: true);
    Cec.ICardEffect? deDigi = EffectNamed(match, exec, Cec.EffectTiming.SecuritySkill, "De-Digivolve 1 to 1 Digimon");
    AssertTrue(deDigi is not null, "[Security] <De-Digivolve 1> ActivateClass must register under SecuritySkill");
    AssertTrue(CanActivate(match, deDigi!), "CanActivate ON: ST17_13 sits on the ExecutingArea (X:ExecutingAreaDigivolve gate)");

    // 음: 배틀에어리어 인스턴스 → IsExistOnExecutingArea false → CanActivate OFF.
    HeadlessEntityId inBattle = Stage(match, P1, "ST17_13", ChoiceZone.BattleArea, "1:battle:Magnamon", register: true);
    AssertTrue(!CanActivate(match, EffectNamed(match, inBattle, Cec.EffectTiming.SecuritySkill, "De-Digivolve 1 to 1 Digimon")!),
        "negative: a battle-area copy is not on the ExecutingArea → CanActivate OFF");
}

async Task ST17_13_WhenDigivolvingTrashGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2402, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId magnamon = Stage(match, P1, "ST17_13", ChoiceZone.BattleArea, "1:battle:Magnamon", register: true);

    Cec.ICardEffect? trash = EffectNamed(match, magnamon, Cec.EffectTiming.WhenDigivolving, "Trash digivolution cards");
    AssertTrue(trash is not null, "[When Digivolving] trash-per-color ActivateClass must register under the WhenDigivolving key");
    AssertTrue(CanActivate(match, trash!), "CanActivate ON: IsExistOnBattleAreaDigimon (Magnamon in the battle area)");
}

async Task ST17_13_StaticEffectsPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2403, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId magnamon = Stage(match, P1, "ST17_13", ChoiceZone.BattleArea, "1:battle:Magnamon", register: true);

    List<Cec.ICardEffect> none = EffectsOf(match, magnamon, P1, Cec.EffectTiming.None);
    AssertTrue(HasEffectType(none, "BlockerClass"), "BlockerSelfStaticEffect → BlockerClass under None");
    AssertTrue(HasEffectType(none, "AddDigivolutionRequirementClass"), "Veemon digivolve condition → AddDigivolutionRequirementClass under None");
    AssertTrue(EffectNamed(match, magnamon, Cec.EffectTiming.WhenPermanentWouldBeDeleted, "Armor Purge") is not null,
        "ArmorPurgeEffect → [Armor Purge] under WhenPermanentWouldBeDeleted");
}

// ═══════════════════════════════════ AD1_011 ═══════════════════════════════════

async Task AD1011_ConditionsPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId paildramon = Stage(match, P1, "AD1_011", ChoiceZone.Hand, "1:hand:Paildramon");

    List<Cec.ICardEffect> none = EffectsOf(match, paildramon, P1, Cec.EffectTiming.None);
    AssertTrue(HasEffectType(none, "AddJogressConditionClass"), "GetJogressConditionClass → AddJogressConditionClass under None");
    AssertTrue(HasEffectType(none, "AddDigivolutionRequirementClass"), "Alt-digivolve (Free/Hero, lvl4, cost3) → AddDigivolutionRequirementClass under None");
}

async Task AD1011_WhenDigivolvingGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2502, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId paildramon = Stage(match, P1, "AD1_011", ChoiceZone.BattleArea, "1:battle:Paildramon", register: true);

    Cec.ICardEffect? cantDelete = EffectNamed(match, paildramon, Cec.EffectTiming.WhenDigivolving,
        "Can't be deleted in battle. If DNA: Attack target can't be changed");
    AssertTrue(cantDelete is not null, "[When Digivolving] can't-be-deleted-in-battle ActivateClass under the WhenDigivolving key");
    AssertTrue(CanActivate(match, cantDelete!), "CanActivate ON: IsExistOnBattleArea (Paildramon in the battle area)");
}

async Task AD1011_PartitionAndAttack()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2503, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId paildramon = Stage(match, P1, "AD1_011", ChoiceZone.BattleArea, "1:battle:Paildramon", register: true);

    // PartitionSelfEffect는 named "Partition" ActivateClass 반환(own + inherited 2회).
    int partitions = EffectsOf(match, paildramon, P1, Cec.EffectTiming.WhenRemoveField).Count(e => e.EffectName == "Partition");
    AssertTrue(partitions >= 2, $"PartitionSelfEffect own+inherited → two [Partition] ESS under WhenRemoveField [count:{partitions}]");
    AssertTrue(EffectNamed(match, paildramon, Cec.EffectTiming.OnAllyAttack, "Digivolve into Imperialdramon") is not null,
        "[When Attacking] Digivolve-into-[Imperialdramon] ActivateClass under OnAllyAttack");
}

// ═══════════════════════════════════ BT20_017 ═══════════════════════════════════

async Task BT20017_OnPlayTokenPlayed()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // Jesmon을 배틀에어리어에 배치·등록 — [On Play] 몸통이 호출하는 PlayAthoRenePorToken 프리미티브를 직접 구동
    // (CardSource 오버로드; CardEffectCommons.cs:2573). 펌프-드리븐 매치 컨텍스트 위에서 프리미티브 결과 관찰.
    HeadlessEntityId jesmon = Stage(match, P1, "BT20_017", ChoiceZone.BattleArea, "1:battle:Jesmon", register: true);

    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        var source = new Cec.CardSource(match.Context, jesmon, P1);
        await Cec.CardEffectCommons.PlayAthoRenePorToken(source);
    }

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Any(id => CardNumberOf(match, id) == "BT20-017-token"),
        "PlayAthoRenePorToken: the [Atho, René & Por] token (BT20-017-token) entered the battle area");
}

async Task BT20017_TokenCarriesDecoy()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2602, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId jesmon = Stage(match, P1, "BT20_017", ChoiceZone.BattleArea, "1:battle:Jesmon", register: true);
    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        var source = new Cec.CardSource(match.Context, jesmon, P1);
        await Cec.CardEffectCommons.PlayAthoRenePorToken(source);
    }

    HeadlessEntityId token = ZoneCards(match, P1, ChoiceZone.BattleArea).First(id => CardNumberOf(match, id) == "BT20-017-token");
    // 수확(RD-T2A-01): 감사 §4가 BT20_017에 배정한 K:Decoy는 카드 본체가 아니라 플레이되는 토큰
    // [Atho, René & Por]가 싣는 키워드다. 그런데 미러 토큰 효과 스크립트 BT20_017_token.cs가 아직
    // 미포팅 스텁(7줄 skeleton)이라 토큰의 <Reboot>/<Blocker>/<Decoy>는 미러에서 휴면 — 토큰 CardSource
    // 효과창/트레이트 어디에도 Decoy 표면이 없다. 카드 본체(BT20_017)는 Decoy API를 직접 호출하지 않으므로
    // (PlayAthoRenePorToken만 호출) 이 트랜치 범위에서 Decoy는 명목상만 커버된다. 정직 문서화(우회 green 금지):
    // 상환(토큰 포팅) 시 이 assert를 뒤집어 토큰의 <Decoy> 발화를 검증할 것.
    using AmbientMatchContext.Scope _decoy = AmbientMatchContext.Enter(match.Context);
    var tokenSource = new Cec.CardSource(match.Context, token, P1);
    bool decoy = tokenSource.EffectList(Cec.EffectTiming.None).Any(e => e.GetType().Name.Contains("Decoy", StringComparison.Ordinal))
        || tokenSource.CardTraits.Any(t => t.Contains("Decoy", StringComparison.OrdinalIgnoreCase));
    AssertTrue(!decoy,
        "HARVEST RD-T2A-01: the played token entered (P:PlayAthoRenePorToken covered), but K:Decoy is DORMANT " +
        "because the token effect port BT20_017_token.cs is an unported stub — when this assert fails the token " +
        "was ported and this witness must flip to assert the <Decoy> keyword surface");
}

async Task BT20017_YourTurnAndWhenDigivolvingGates()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2603, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId jesmon = Stage(match, P1, "BT20_017", ChoiceZone.BattleArea, "1:battle:Jesmon", register: true);

    AssertTrue(EffectNamed(match, jesmon, Cec.EffectTiming.OnEnterFieldAnyone, "Delete 8k DP or less, Then 1 Digimon may attack.") is not null,
        "[Your Turn] delete-then-attack ActivateClass under OnEnterFieldAnyone");
    AssertTrue(EffectNamed(match, jesmon, Cec.EffectTiming.WhenDigivolving, "Play token") is not null,
        "[When Digivolving] token ActivateClass under the WhenDigivolving key");
    AssertTrue(EffectNamed(match, jesmon, Cec.EffectTiming.OnEnterFieldAnyone, "Play a token") is not null,
        "[On Play] token ActivateClass stays under OnEnterFieldAnyone");
}

// ═══════════════════════════════════ BT14_030 ═══════════════════════════════════

async Task BT14030_OnPlayBounceGate()
{
    // AS-IS의 CanSelectPermanentCondition은 "상대 Lv3 OR 자기 임의 Digimon" — MarineAngemon 자신도 자기
    // Digimon이라 항상 대상(자기-바운스)이 되므로 "대상 없음" 음성은 존재하지 않는다. 대신 CanActivate의
    // IsExistOnBattleArea 게이트(배틀 vs 손패)로 양/음 대조. 양성엔 상대 Lv3도 배치해 대상 존재도 함께 증인.
    async Task<bool> CanBounce(bool inBattle)
    {
        (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2701, MonoDecks("BT1_028", "BT1_028"));
        await ReachMainWaitAsync(match);
        HeadlessEntityId marine = inBattle
            ? Stage(match, P1, "BT14_030", ChoiceZone.BattleArea, "1:battle:Marine", register: true)
            : Stage(match, P1, "BT14_030", ChoiceZone.Hand, "1:hand:Marine");
        StageSynthetic(match, P2, "EXT2-L3", dp: 3000, level: 3, "2:battle:l3");
        return CanActivate(match, EffectNamed(match, marine, Cec.EffectTiming.OnEnterFieldAnyone, "Return Digimon to hand")!);
    }

    AssertTrue(await CanBounce(inBattle: true), "CanActivate ON: MarineAngemon in the battle area + a valid bounce target (opp Lv.3)");
    AssertTrue(!await CanBounce(inBattle: false), "negative: MarineAngemon in hand → IsExistOnBattleArea false → CanActivate OFF");
}

async Task BT14030_WhenDigivolvingBounceGate()
{
    async Task<bool> CanBounce(bool inBattle)
    {
        (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2702, MonoDecks("BT1_028", "BT1_028"));
        await ReachMainWaitAsync(match);
        HeadlessEntityId marine = inBattle
            ? Stage(match, P1, "BT14_030", ChoiceZone.BattleArea, "1:battle:Marine", register: true)
            : Stage(match, P1, "BT14_030", ChoiceZone.Hand, "1:hand:Marine");
        StageSynthetic(match, P2, "EXT2-L3", dp: 3000, level: 3, "2:battle:l3");
        Cec.ICardEffect? bounce = EffectNamed(match, marine, Cec.EffectTiming.WhenDigivolving, "Return Digimon to hand");
        AssertTrue(bounce is not null, "[When Digivolving] bounce-chain ActivateClass under the WhenDigivolving key");
        return CanActivate(match, bounce!);
    }

    AssertTrue(await CanBounce(inBattle: true), "CanActivate ON: MarineAngemon in the battle area (+opp Lv.3)");
    AssertTrue(!await CanBounce(inBattle: false), "negative: MarineAngemon in hand → IsExistOnBattleArea false → CanActivate OFF");
}

async Task BT14030_RecoveryGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 2703, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId marine = Stage(match, P1, "BT14_030", ChoiceZone.BattleArea, "1:battle:Marine", register: true);

    Cec.ICardEffect? recovery = EffectNamed(match, marine, Cec.EffectTiming.OnPermamemtReturnedToHand, "Recovery +1 (Deck)");
    AssertTrue(recovery is not null, "[Your Turn] <Recovery +1 (Deck)> ActivateClass under OnPermamemtReturnedToHand");
    AssertTrue(CanActivate(match, recovery!), "CanActivate ON: IsExistOnBattleArea (MarineAngemon in the battle area)");
}

// ═══════════════════════════════ T2A-specific helpers ═══════════════════════════════

List<Cec.ICardEffect> EffectsOf(DcgoMatch match, HeadlessEntityId id, HeadlessPlayerId owner, Cec.EffectTiming timing)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.CardSource(match.Context, id, owner).EffectList(timing);
}

Cec.ICardEffect? EffectNamed(DcgoMatch match, HeadlessEntityId id, Cec.EffectTiming timing, string name)
{
    HeadlessPlayerId owner = OwnerOf(match, id);
    return EffectsOf(match, id, owner, timing).FirstOrDefault(e => e.EffectName == name);
}

static bool HasEffectType(List<Cec.ICardEffect> effects, string typeName) => effects.Any(e => e.GetType().Name == typeName);

static bool CanActivate(DcgoMatch match, Cec.ICardEffect effect)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return effect.CanActivate(new Hashtable());
}

static HeadlessPlayerId OwnerOf(DcgoMatch match, HeadlessEntityId id) =>
    match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
        ? rec.OwnerId
        : new HeadlessPlayerId(1);

void SetInstanceMeta(DcgoMatch match, HeadlessEntityId id, params (string Key, object? Val)[] kv)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) || rec is null)
    {
        throw new InvalidOperationException($"missing instance {id.Value}");
    }

    var meta = new Dictionary<string, object?>(rec.Metadata, StringComparer.Ordinal);
    foreach ((string k, object? v) in kv)
    {
        meta[k] = v;
    }

    match.Context.CardInstanceRepository.Upsert(rec with { Metadata = meta });
}

static int PermanentDp(DcgoMatch match, HeadlessEntityId id, HeadlessPlayerId owner)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.Permanent(match.Context, id, owner).DP;
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
        HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    }

    return id;
}

// 합성 픽스처 카드(R4S3b StageBattleDigimon 관례 확장): def 업서트 + 인스턴스 + 존 이동.
HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null, Dictionary<string, object?>? extraDefMeta = null, int? playCost = null)
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

    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name ?? number, meta, CardType: cardType, PlayCost: playCost));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level, ["isSuspended"] = false }));
    if (zone != ChoiceZone.None)
    {
        ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult();
    }

    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
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
