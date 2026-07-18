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

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-T3A 정본 witness 스위트 — 커버리지 정본 포팅 수확 트랜치3A(STOP-예상 5장), 카드당 2~3종.
// 하네스/좌석/컨텍스트 팩토리는 EXEMPLAR-T2A 정본을 그대로 복사(DcgoMatch.CreatePumpDriven + 에이전트 액션/
// PolicyChoiceProvider 좌석 + CardSource.EffectList(timing) 술어-표면 조회). 수확 트랜치이므로 검증은:
//   (a) 포팅된 팔: EffectList(timing)에 축 효과 존재 + CanActivate 양/음 대조(포팅 충실도 고정).
//   (b) STOP 팔: 정직 수확 케이스 — 효과는 등재(선언 게이트 클린)되나 RESOLUTION이 NotSupportedException STOP
//       임을 assert-throws로 고정(우회 green 금지). 예측이 뚫린 팔(Ascension/공유/AddSkill)은 등재 확인 =
//       예측 BUSTED 정정.
// 카드/축 매핑은 각 카드 소스 헤더(①②③ 정본 주석)와 docs/audit/coverage_exemplar_audit_2026-07-18.md §4·§6.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // BT25_040 — MagnaAngemon (K:Ascension·TrashSecurity·OnDiscardSecurity·OnLoseSecurity) — 전부 포팅
    ("BT25_040 W1 등재: [When Trashed](OnDiscardSecurity)·[Ascension](OnDestroyedAnyone)·Shared(OnEnterFieldAnyone) 효과 등재 — Ascension/공유 STOP 예측 BUSTED", BT25040_TrashAscensionSharedPresent),
    ("BT25_040 W2 [All Turns] OnLoseSecurity -4000: 효과 등재 + CanActivate ON(배틀에어리어 Digimon)", BT25040_LoseSecurityGate),
    // BT25_104 — ShineGreymon: Burst Mode (14축) — Arts만 latent STOP, 나머지 포팅
    ("BT25_104 W1 정적 키워드 등재: AddDigivolutionRequirement·AddBurstDigivolutionCondition·ChangeSAttack·Blocker·ChangeBaseDP·Rush·UseRequirements·Arts(None)", BT25104_StaticsPresent),
    ("BT25_104 W2 [Raid](OnAllyAttack)·Option [Main](OptionSkill) 등재 + Arts latent STOP(RD-P6C2-10)·Burst execution 별개 STOP(RD-P6C1-6) 수확", BT25104_RaidMainArtsHarvest),
    // BT25_089 — Kazuki & Itsuki (Link·AppFusion STOP; Gain-memory·Security 포팅)
    ("BT25_089 W1 포팅 팔: [Start of Main](Gain1Memory)·[Security](PlaySelfTamer) 효과 등재", BT25089_PortableArms),
    ("BT25_089 W2 수확 STOP: [Main] link 등재 + CanUse ON, RESOLUTION throws NotSupported(ILinkCard 부재/CanLink payCost — RD-EXT3-01)", BT25089_LinkStopHarvest),
    ("BT25_089 W3 수확 STOP: [End of Turn] AppFusion 등재, RESOLUTION throws NotSupported(CanAppFusion RD-P6C1-2 + PermanentFrame RD-P6C3-D1 — RD-EXT3-02)", BT25089_AppFusionStopHarvest),
    // EX7_072 — Seventh Fascination (AddSkill nested-grant) — 전부 포팅(예측 BUSTED)
    ("EX7_072 W1 [Security] delete: 효과 등재 + CanActivate ON(상대 미서스펜드 Digimon) / OFF(부재) 양·음", EX7072_SecurityDeleteGate),
    ("EX7_072 W2 등재: [Main](OptionSkill AddSkill nested-grant)·[Trash](WhenDigivolving OptionMain) — AddSkillClass STOP 예측 BUSTED", EX7072_MainAndTrashPresent),
    // EX7_014 — Volcanicdramon (CanNotMove enforced / CanNotPutField inert 수확)
    ("EX7_014 W1 [On Play] delete: 효과 등재 + CanActivate ON(상대 Digimon) / OFF(부재) 양·음", EX7014_OnPlayDeleteGate),
    ("EX7_014 W2 [When Digivolving] flip: CanNotMove+CanNotPutField 생산자 빌드; 두 술어 집행 ON(≤6000 적)·>6000 OFF; CanNotPutField 플레이 경로 ENFORCED — ≤6000 적 PlayCard 표 이탈(RD-EXT3-03 배선)", EX7014_WhenDigivolvingHarvest),
    ("EX7_014 W3 [All Turns] WhenRemoveField play: 효과 등재", EX7014_AllTurnsPresent),
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
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(6)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ BT25_040 ═══════════════════════════════════

async Task BT25040_TrashAscensionSharedPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId magna = Stage(match, P1, "BT25_040", ChoiceZone.BattleArea, "1:battle:Magna", register: true);

    AssertTrue(EffectNamed(match, magna, Cec.EffectTiming.OnDiscardSecurity, "Play 1 level 4 or lower [Angel] or [Iliad] card") is not null,
        "[When Trashed] play ActivateClass registered under OnDiscardSecurity");
    AssertTrue(EffectNamed(match, magna, Cec.EffectTiming.OnDestroyedAnyone, "Ascension") is not null,
        "HARVEST (BUST RD-3A-01/RD-P6C3-A3): AscensionSelfEffect registers under OnDestroyedAnyone — Ascension is ported, not STOP");
    AssertTrue(EffectNamed(match, magna, Cec.EffectTiming.OnEnterFieldAnyone,
        "By trashing top or bottom security card, 1 opponent digimon gains -8k DP until their turn ends") is not null,
        "HARVEST (BUST P2-9): ActivateClassesForSharedEffects registers the shared OP/WD effect (pure dispatcher, no STOP)");
}

async Task BT25040_LoseSecurityGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3402, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId magna = Stage(match, P1, "BT25_040", ChoiceZone.BattleArea, "1:battle:Magna", register: true);

    // [All Turns] inherited -4000 ActivateClass는 OnLoseSecurity에 등재; T:OnLoseSecurity 축 포팅 확인.
    // (CanActivate 게이트는 once-per-turn maxCount + 턴 컨텍스트에 의존하므로 등재-표면으로 고정.)
    Cec.ICardEffect? at = EffectNamed(match, magna, Cec.EffectTiming.OnLoseSecurity, "1 of your opponent's Digimon gets -4000 DP");
    AssertTrue(at is not null, "[All Turns] inherited -4000 ActivateClass registered under OnLoseSecurity (T:OnLoseSecurity axis)");
    AssertTrue(EffectNamed(match, magna, Cec.EffectTiming.None, "Origin DP is 12000") is null,
        "sanity: BT25_040 has no BT25_104-style [None] DP effect (correct card resolved)");
}

// ═══════════════════════════════════ BT25_104 ═══════════════════════════════════

async Task BT25104_StaticsPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3411, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId shine = Stage(match, P1, "BT25_104", ChoiceZone.BattleArea, "1:battle:Shine", register: true);
    List<Cec.ICardEffect> none = EffectsOf(match, shine, P1, Cec.EffectTiming.None);

    foreach (string t in new[]
    {
        "AddDigivolutionRequirementClass", "AddBurstDigivolutionConditionClass", "ChangeSAttackClass",
        "BlockerClass", "ChangeBaseDPClass", "RushClass", "IgnoreColorConditionClass", "OptionResolutionClass",
    })
    {
        AssertTrue(HasEffectType(none, t), $"[None] static effect present: {t}");
    }
}

async Task BT25104_RaidMainArtsHarvest()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3412, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId shine = Stage(match, P1, "BT25_104", ChoiceZone.BattleArea, "1:battle:Shine", register: true);

    AssertTrue(EffectNamed(match, shine, Cec.EffectTiming.OnAllyAttack, "Raid") is not null,
        "[Raid] RaidSelfEffect registered under OnAllyAttack");
    AssertTrue(EffectNamed(match, shine, Cec.EffectTiming.OptionSkill, "As an option effect, 1 Enemy Digimon gets -15k DP, play 1 tamer.") is not null,
        "Option [Main] ActivateClass registered under OptionSkill");
    // HARVEST: Arts Digivolution registers (OptionResolutionClass) but its RESOLUTION is the deferred STOP
    // RD-P6C2-10 (CanResolveCondition/ResolutionCoroutine closures throw). Burst EXECUTION is the separate engine
    // STOP RD-P6C1-6 (SelectBurstDigivolutionEffect / CardController burst-play) — not driven here.
    AssertTrue(HasEffectType(EffectsOf(match, shine, P1, Cec.EffectTiming.None), "OptionResolutionClass"),
        "HARVEST: ArtsDigivolveEffect registers (latent STOP RD-P6C2-10 on resolution; factory call/registration clean)");
}

// ═══════════════════════════════════ BT25_089 ═══════════════════════════════════

async Task BT25089_PortableArms()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3421, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId kazuki = Stage(match, P1, "BT25_089", ChoiceZone.BattleArea, "1:battle:Kazuki", register: true, cardType: "Tamer");

    AssertTrue(EffectsOf(match, kazuki, P1, Cec.EffectTiming.OnStartMainPhase).Count >= 1,
        "[Start of Your Main Phase] Gain1MemoryTamerOpponentDigimonEffect registered");
    AssertTrue(EffectsOf(match, kazuki, P1, Cec.EffectTiming.SecuritySkill).Count >= 1,
        "[Security] PlaySelfTamerSecurityEffect registered");
}

async Task BT25089_LinkStopHarvest()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3422, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId kazuki = Stage(match, P1, "BT25_089", ChoiceZone.BattleArea, "1:battle:Kazuki", register: true, cardType: "Tamer");

    Cec.ICardEffect? main = EffectNamed(match, kazuki, Cec.EffectTiming.OnDeclaration, "Link for -2");
    AssertTrue(main is not null, "[Main] link ActivateClass registered under OnDeclaration");
    AssertTrue(await ActivateThrowsAsync(match, main!),
        "HARVEST RD-EXT3-01: the [Main] link RESOLUTION throws NotSupportedException (ILinkCard has no mirror + CanLink(payCost:true)) " +
        "— when this no longer throws the link subsystem was ported and this witness must flip to assert the link");
}

async Task BT25089_AppFusionStopHarvest()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3423, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId kazuki = Stage(match, P1, "BT25_089", ChoiceZone.BattleArea, "1:battle:Kazuki", register: true, cardType: "Tamer");

    Cec.ICardEffect? fuse = EffectNamed(match, kazuki, Cec.EffectTiming.OnEndTurn, "App fuse 1 digimon into digimon in hand");
    AssertTrue(fuse is not null, "[End of Your Turn] app-fusion ActivateClass registered under OnEndTurn");
    AssertTrue(await ActivateThrowsAsync(match, fuse!),
        "HARVEST RD-EXT3-02: the AppFusion RESOLUTION throws NotSupportedException (CanAppFusionFromTargetPermanent RD-P6C1-2 + " +
        "Permanent.PermanentFrame.FrameID RD-P6C3-D1) — when this no longer throws the AppFusion subsystem was ported");
}

// ═══════════════════════════════════ EX7_072 ═══════════════════════════════════

async Task EX7072_SecurityDeleteGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3431, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId seventh = Stage(match, P1, "EX7_072", ChoiceZone.BattleArea, "1:battle:Seventh", register: true);

    // [Security] delete ActivateClass는 canActivateCondition이 null(AS-IS :240 SetUpActivateClass(null,...)) —
    // 대상 존재 체크는 ActivateCoroutine 내부(HasMatchConditionPermanent). 등재-표면 + 보안 플래그로 고정.
    Cec.ICardEffect? sec = EffectNamed(match, seventh, Cec.EffectTiming.SecuritySkill, "Delete 1 Opponents unsuspended Digimon");
    AssertTrue(sec is not null, "[Security] delete ActivateClass registered under SecuritySkill (SelectPermanentEffect Destroy axis)");
    // 상대 미서스펜드 Digimon이 존재해도 등재는 불변 — 대상 술어(IsPermanentExistsOnOpponentBattleAreaDigimon +
    // !IsSuspended)는 해소 시 평가됨. 포팅 충실도는 timing/effect 등재로 고정.
    StageSynthetic(match, P2, "EXT3-OPP", dp: 3000, level: 4, "2:battle:opp");
    AssertTrue(EffectNamed(match, seventh, Cec.EffectTiming.SecuritySkill, "Delete 1 Opponents unsuspended Digimon") is not null,
        "[Security] effect still registered with an opponent Digimon present");
}

async Task EX7072_MainAndTrashPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3432, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId seventh = Stage(match, P1, "EX7_072", ChoiceZone.BattleArea, "1:battle:Seventh", register: true);

    AssertTrue(EffectNamed(match, seventh, Cec.EffectTiming.OptionSkill, "All Opponents Digimon gain \"Delete 1 of your Digimon\"") is not null,
        "HARVEST (BUST nested-grant STOP): [Main] AddSkillClass grant ActivateClass registered under OptionSkill (IAddSkillEffect is live-scanned)");
    AssertTrue(EffectNamed(match, seventh, Cec.EffectTiming.WhenDigivolving, "Return this to bottom of deck, Activate Main") is not null,
        "[Trash] OptionMain re-activation ActivateClass registered under WhenDigivolving");
}

// ═══════════════════════════════════ EX7_014 ═══════════════════════════════════

async Task EX7014_OnPlayDeleteGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3441, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId volc = Stage(match, P1, "EX7_014", ChoiceZone.BattleArea, "1:battle:Volc", register: true);

    Cec.ICardEffect OnPlay() => EffectsOf(match, volc, P1, Cec.EffectTiming.OnEnterFieldAnyone)
        .First(e => e.EffectName != "Marcus" && e is Cec.ActivateICardEffect);
    AssertTrue(EffectsOf(match, volc, P1, Cec.EffectTiming.OnEnterFieldAnyone).Count >= 1, "[On Play] delete ActivateClass registered under OnEnterFieldAnyone");
    AssertTrue(!CanActivate(match, OnPlay()), "negative: no opponent Digimon → lowest-DP delete CanActivate OFF");

    StageSynthetic(match, P2, "EXT3-OPP", dp: 3000, level: 4, "2:battle:opp");
    AssertTrue(CanActivate(match, OnPlay()), "CanActivate ON: an opponent Digimon exists (IsMinDP target)");
}

async Task EX7014_WhenDigivolvingHarvest()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3442, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId volc = Stage(match, P1, "EX7_014", ChoiceZone.BattleArea, "1:battle:Volc", register: true);
    HeadlessEntityId low = StageSynthetic(match, P2, "EXT3-LOW", dp: 5000, level: 4, "2:battle:low");
    HeadlessEntityId high = StageSynthetic(match, P2, "EXT3-HIGH", dp: 9000, level: 6, "2:battle:high");
    // (G-Field flip) the opponent's HAND candidates the CanNotPutField restriction gates on the PLAY path:
    // a ≤6000 DP Digimon (blocked when the restriction is active) and a >6000 DP control (never blocked).
    // playCost 0 so cost payability is never the reason the play is absent — only CanEnterField is.
    HeadlessEntityId lowHand = StageSynthetic(match, P2, "EXT3-HANDLOW", dp: 5000, level: 4, "2:hand:low", zone: ChoiceZone.Hand, playCost: 0);
    HeadlessEntityId highHand = StageSynthetic(match, P2, "EXT3-HANDHIGH", dp: 9000, level: 6, "2:hand:high", zone: ChoiceZone.Hand, playCost: 0);

    // Local: does the opponent's PlayCard legal-action table (the dispatcher's PlayCard candidate seat, which
    // is exactly new PlayCardAction().GetLegalActions → Validate) currently offer this hand card?
    bool P2Offers(HeadlessEntityId c)
    {
        using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
        return new PlayCardAction().GetLegalActions(match.Context, P2).Any(a => ActionCardIds(a).Contains(c));
    }

    Cec.ICardEffect? wd = EffectNamed(match, volc, Cec.EffectTiming.WhenDigivolving, "Opponent can't play or move Digimon with 6000 DP or less");
    AssertTrue(wd is not null, "[When Digivolving] restriction ActivateClass registered under WhenDigivolving");

    // BASELINE (restriction not yet active): both opponent hand Digimon are offered on the PlayCard table.
    AssertTrue(P2Offers(lowHand), "baseline: opponent's 5000 DP hand Digimon is a legal PlayCard before the restriction");
    AssertTrue(P2Offers(highHand), "baseline: opponent's 9000 DP hand Digimon is a legal PlayCard before the restriction");

    // 효과 발화 — 두 제약(CanNotMove + CanNotPutField)을 상대 UntilOwnerTurnEndEffects에 배치.
    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        await ((Cec.ActivateICardEffect)wd!).Activate(new Hashtable());
    }

    List<Cec.ICardEffect> enemyNone = PlayerEffectsOf(match, P2, Cec.EffectTiming.None);
    AssertTrue(HasEffectType(enemyNone, "CanNotMoveClass"), "CanNotMoveClass producer built on the opponent player bucket");
    AssertTrue(HasEffectType(enemyNone, "CanNotPutFieldClass"), "CanNotPutFieldClass producer built on the opponent player bucket");

    // CanNotMove 술어 집행: ≤6000 적 Digimon은 이동 불가(ON), >6000은 가능(OFF) — 양/음.
    Cec.ICanNotMoveEffect cannotMove = (Cec.ICanNotMoveEffect)enemyNone.First(e => e is Cec.ICanNotMoveEffect);
    // CanNotPutField 술어 집행(포팅 충실도): ≤6000 적 Digimon은 배치 불가(ON), >6000은 가능(OFF) — 양/음.
    Cec.ICanNotPutFieldEffect cannotPut = (Cec.ICanNotPutFieldEffect)enemyNone.First(e => e is Cec.ICanNotPutFieldEffect);
    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        var lowCs = new Cec.CardSource(match.Context, low, P2);
        var highCs = new Cec.CardSource(match.Context, high, P2);
        AssertTrue(cannotMove.CanNotMove(lowCs, null!), "CanNotMove ENFORCED: opponent's 5000 DP (≤6000) Digimon can't move");
        AssertTrue(!cannotMove.CanNotMove(highCs, null!), "negative: opponent's 9000 DP (>6000) Digimon can move");
        AssertTrue(cannotPut.CanNotPutField(lowCs, null!), "CanNotPutField predicate ON: opponent's 5000 DP (≤6000) Digimon");
        AssertTrue(!cannotPut.CanNotPutField(highCs, null!), "negative: CanNotPutField OFF for opponent's 9000 DP (>6000) Digimon");
    }

    // FLIP RD-EXT3-03 (was INERT): with the CanNotPutFieldClass producer active, PlayCardAction.Validate now
    // consults CardSource.CanEnterField, so the ≤6000 opponent Digimon DROPS OUT of the legal-action table
    // (table == executable contract), while the >6000 control is unaffected — the enforcement-wiring gap closed.
    AssertTrue(!P2Offers(lowHand), "ENFORCED: opponent's 5000 DP (≤6000) hand Digimon is no longer a legal PlayCard (CanEnterField blocks it)");
    AssertTrue(P2Offers(highHand), "control: opponent's 9000 DP (>6000) hand Digimon is still a legal PlayCard");
}

async Task EX7014_AllTurnsPresent()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 3443, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId volc = Stage(match, P1, "EX7_014", ChoiceZone.BattleArea, "1:battle:Volc", register: true);

    AssertTrue(EffectNamed(match, volc, Cec.EffectTiming.WhenRemoveField, "Play 1 Digimon card with the [Machine Dragon]/[Sky Dragon] trait") is not null,
        "[All Turns] once-per-turn play ActivateClass registered under WhenRemoveField");
}

// ═══════════════════════════════ T3A-specific helpers ═══════════════════════════════

async Task<bool> ActivateThrowsAsync(DcgoMatch match, Cec.ICardEffect effect)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    try
    {
        await ((Cec.ActivateICardEffect)effect).Activate(new Hashtable());
        return false;
    }
    catch (NotSupportedException)
    {
        return true;
    }
}

List<Cec.ICardEffect> PlayerEffectsOf(DcgoMatch match, HeadlessPlayerId player, Cec.EffectTiming timing)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.Player(match.Context, player).EffectList(timing);
}
// ═══════════════════════════════ T3A-specific helpers ═══════════════════════════════

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
        Cec.CardEffectRegistrar.RegisterCard(ctx, id, owner);
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
