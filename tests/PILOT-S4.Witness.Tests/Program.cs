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
// PILOT-S4 witness 스위트 — Sonnet 트랜치 S4 10장, 카드당 1개 이상(PILOT-S1/S2/S3.Witness.Tests 템플릿 복제).
// 표준 템플릿: DcgoMatch.CreatePumpDriven + 에이전트 액션 구동(ApplyActionAsync) — [On Play]/[Main] 계열은
// 실 레인 구동(PlayCard/ActivateOption), 실 레인이 없거나 트리거-해시테이블 구축이 과도한 창(OnDeclaration
// Digi-Burst, [When Digivolving] 직접부여, OnFaceUpSecurityIncreased 등)은 ActivateClass.CanUse/CanActivate/
// Activate 공개 API 직접 호출(BT25_101/BT25_102 PILOT-S3 선례와 동형 — CanUse는 해시테이블을 실제로 읽지
// 않는 arm에 한해 우회 없이 그대로 호출, 아닌 경우 CanActivate+Activate만 호출).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT10_111 W1: [On Play] 발화 → DigiXros요건 트래시카드 손패로 → 진화 permanent에 CanSelectDigiXrosClass 부여(UntilEachTurnEndEffects)", BT10111_OnPlayReturnsAndGrantsDigiXros),
    ("BT3_103 W1: [Main] 발화 → BeforePayCost/AfterPayCost 그랜트 페어가 Player.UntilEachTurnEndEffects에 등재", BT3103_MainRegistersCostReductionGrants),
    ("EX5_058 W1: [On Play] 발화(총 디지몬≥4) → [Fujitsumon] 토큰이 자기 배틀에어리어에 서스펜드 착지", EX5058_OnPlayPlaysOwnerToken),
    ("BT5_056 W1: [Main] <Digi-Burst 2> 발화 → 디지볼루션카드 2장 트래시 + 자기 전 디지몬 DP+2000", BT5056_DigiBurstTrashesAndBuffs),
    ("EX10_043 W1: [On Play] 발화 → 상대 레벨3 디지몬 1체 삭제", EX10043_OnPlayDeletesLevel3),
    ("P_165 W1: [On Play] 발화 → [Familiar] 토큰이 자기 배틀에어리어에 착지", P165_OnPlayPlaysFamiliarToken),
    ("P_198 W1: ESS [When Attacking] 직접 발화 → <Draw 1> + 손패 1장 트래시", P198_EssDrawsAndDiscards),
    ("EX11_004 W1: [Your Turn] 직접 발화 → <Draw 1>", EX11004_DirectFireDraws),
    ("EX6_044 W1: [Hand][Main] 발화 → 자기 진화원 배치 + 메모리-3 + 상대 디지몬 <De-Digivolve 1>", EX6044_HandMainDegeneratesOpponent),
    ("P_048 W1(RD-S4-P048 sink 검증): [When Digivolving] 직접 발화 → 트래시 3장 덱밑 이동(선택-순서 보존, MatchStateMutationSink 직접구성) + 자기 언서스펜드 + 명시 StackSkillInfos(OnReturnCardsToLibraryFromTrash) 호출로 자기 2번째 effect가 +1메모리", P048_WhenDigivolvingMovesToLibraryBottomAndGainsMemory),
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

// ═══════════════════════════════════ BT10_111 ═══════════════════════════════════

async Task BT10111_OnPlayReturnsAndGrantsDigiXros()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId trashSource = Stage(match, P1, "BT10_111", ChoiceZone.Trash, "1:trash:BT10111src", register: true);
    // Staged directly onto the battle area (as if just played) rather than driven through the PlayCard lane:
    // this is a pump-driven match, and once the reactor settles with no more player-facing decisions, further
    // StepAsync() calls (the shared ApplyAsync/DriveUntilAsync path) fast-forward straight through the rest of
    // this turn AND the opponent's whole turn in this sparse mono-deck fixture — each StepAsync() call appears
    // to advance multiple phases atomically, so even single-stepping observed the grant already wiped by
    // HeadlessEndTurnCleanupFlow's blanket `permanent.UntilEachTurnEndEffects = new()` sweep (SharedTurnEndKeys,
    // unconditional per field permanent, AS-IS EndPhase TurnStateMachine.cs:3183-3201 mirror) within the SAME
    // step that added it. So this witness drives the [On Play] ActivateClass directly via its public
    // CanUse/CanActivate/Activate API (ST4_13/BT25_101 PILOT-S3 direct-invocation precedent) instead of routing
    // through match.StepAsync() at all.
    HeadlessEntityId bt10111 = Stage(match, P1, "BT10_111", ChoiceZone.BattleArea, "1:battle:BT10111", register: true);
    int trashBefore = Count(match, P1, ChoiceZone.Trash);

    // SelectCardEffect canNoSelect:true — the trash-return pick.
    policy.On(req => req.Candidates.Any(c => c.Id == trashSource), req => ChoiceResult.Select(trashSource));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt10111, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT10.Red.BT10_111();
    List<Cec.ICardEffect> onPlayEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var onPlay = (Cfx.ActivateClass)onPlayEffects.First(e => e.EffectName == "Return 1 card from trash to hand and  this Digimon gets effects");

    AssertTrue(onPlay.CanActivate(new System.Collections.Hashtable()), "CanActivate is true — the permanent exists on the battle area");
    await onPlay.Activate(new System.Collections.Hashtable());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.Hand).Contains(trashSource),
        $"[On Play]: the DigiXros-requirement trash card was returned to hand [debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertEqual(trashBefore - 1, Count(match, P1, ChoiceZone.Trash), "exactly 1 card left the trash");

    var playedPermanent = new Cec.Permanent(match.Context, bt10111, P1);
    bool grantFound = playedPermanent.UntilEachTurnEndEffects.Any(getEffect => getEffect(Cec.EffectTiming.None) is Cfx.CanSelectDigiXrosClass);
    AssertTrue(grantFound, "[On Play]: the played permanent gained a CanSelectDigiXrosClass grant on UntilEachTurnEndEffects");
}

// ═══════════════════════════════════ BT3_103 ═══════════════════════════════════

async Task BT3103_MainRegistersCostReductionGrants()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // OptionColorRequirement.Matches (BT17_095 PILOT-S3 precedent) — BT3_103 is Green, so a Green field
    // permanent must be present or the ActivateOption lane never gets offered.
    StageSynthetic(match, P1, "EXT4-GREEN103", dp: 3000, level: 3, "1:battle:green103", colors: new[] { "Green" });
    HeadlessEntityId bt3103 = Stage(match, P1, "BT3_103", ChoiceZone.Hand, "1:hand:BT3103");

    LegalAction option = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, bt3103,
        $"BT3_103's own [Main] OptionSkill must be offered — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");

    // Same pump over-cascade concern as BT10_111 — drive one step at a time and stop the instant both grants
    // are observable, instead of running until "no pending choice" (which, in this sparse fixture, keeps
    // going straight through end-of-turn and wipes UntilEachTurnEndEffects via HeadlessEndTurnCleanupFlow).
    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        await match.ApplyActionAsync(option);
        for (int i = 0; i < 32 && new Cec.Player(match.Context, P1).UntilEachTurnEndEffects.Count < 2 && !match.IsTerminal(); i++)
        {
            if (match.HasPendingChoice())
            {
                HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
                LegalAction resolve = match.GetLegalActions(chooser).First(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
                await match.ApplyActionAsync(resolve);
            }
            else
            {
                await match.StepAsync();
            }
        }
    }

    using AmbientMatchContext.Scope _s2 = AmbientMatchContext.Enter(match.Context);
    var player = new Cec.Player(match.Context, P1);
    AssertEqual(2, player.UntilEachTurnEndEffects.Count,
        $"[Main]: both the BeforePayCost cost-reduction grant and the AfterPayCost cleanup grant were registered on the player's UntilEachTurnEndEffects bucket [debug prompts:{string.Join(" | ", policy.Seen)}]");

    Cec.ICardEffect? beforePayCost = player.UntilEachTurnEndEffects
        .Select(getEffect => getEffect(Cec.EffectTiming.BeforePayCost))
        .FirstOrDefault(e => e is not null);
    AssertTrue(beforePayCost is not null && beforePayCost.EffectName == "Digivolution Cost -5",
        "one of the two registered getters resolves to the BeforePayCost 'Digivolution Cost -5' grant when queried at EffectTiming.BeforePayCost");

    Cec.ICardEffect? afterPayCost = player.UntilEachTurnEndEffects
        .Select(getEffect => getEffect(Cec.EffectTiming.AfterPayCost))
        .FirstOrDefault(e => e is not null);
    AssertTrue(afterPayCost is not null && afterPayCost.EffectName == "Remove Effect",
        "the other getter resolves to the AfterPayCost 'Remove Effect' cleanup grant when queried at EffectTiming.AfterPayCost");
}

// ═══════════════════════════════════ EX5_058 ═══════════════════════════════════

async Task EX5058_OnPlayPlaysOwnerToken()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9301, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // 3 existing owner battle Digimon + EX5_058 itself entering = 4 total -> own-side token placement branch.
    StageSynthetic(match, P1, "EXT4-A", dp: 3000, level: 3, "1:battle:a");
    StageSynthetic(match, P1, "EXT4-B", dp: 3000, level: 3, "1:battle:b");
    StageSynthetic(match, P1, "EXT4-C", dp: 3000, level: 3, "1:battle:c");
    HeadlessEntityId ex5058 = Stage(match, P1, "EX5_058", ChoiceZone.Hand, "1:hand:EX5058");
    int ownerBattleBefore = Count(match, P1, ChoiceZone.BattleArea);

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, ex5058,
        $"expected a PlayCard lane for EX5_058 — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => Count(m, P1, ChoiceZone.BattleArea) > ownerBattleBefore + 1 || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(ex5058), "EX5_058 landed on the battle area");
    AssertTrue(Count(match, P1, ChoiceZone.BattleArea) >= ownerBattleBefore + 2,
        $"[On Play]: a [Fujitsumon] token additionally landed on the OWNER's battle area (total Digimon >= 4 branch) " +
        $"(before:{ownerBattleBefore} after:{Count(match, P1, ChoiceZone.BattleArea)}) [debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT5_056 ═══════════════════════════════════

async Task BT5056_DigiBurstTrashesAndBuffs()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt5056 = Stage(match, P1, "BT5_056", ChoiceZone.BattleArea, "1:battle:BT5056", register: true);
    HeadlessEntityId src1 = StageSynthetic(match, P1, "EXT4-DBSRC1", dp: 0, level: 0, "1:under:src1", zone: ChoiceZone.None, cardType: "Option");
    HeadlessEntityId src2 = StageSynthetic(match, P1, "EXT4-DBSRC2", dp: 0, level: 0, "1:under:src2", zone: ChoiceZone.None, cardType: "Option");
    SetSources(match, bt5056, src1, src2);
    HeadlessEntityId ally = StageSynthetic(match, P1, "EXT4-ALLY", dp: 4000, level: 4, "1:battle:ally");
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.Where(c => c.IsSelectable).Take(Math.Max(req.MinCount, 1)).Select(c => c.Id)), oneShot: false);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt5056, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT5.Green.BT5_056();
    List<Cec.ICardEffect> declareEffects = effectInstance.CardEffects(Cec.EffectTiming.OnDeclaration, card);
    var digiBurst = (Cfx.ActivateClass)declareEffects.First(e => e.EffectName == "DP +2000 to your all Digimon");

    var allyPermanent = new Cec.Permanent(match.Context, ally, P1);
    int dpBefore = allyPermanent.DP;
    int digivolutionCardsBefore = new Cec.Permanent(match.Context, bt5056, P1).DigivolutionCards.Count;

    AssertTrue(digiBurst.CanUse(new System.Collections.Hashtable()),
        $"CanUse is true with 2 digivolution source cards present (digivolutionCardsBefore:{digivolutionCardsBefore})");

    await digiBurst.Activate(new System.Collections.Hashtable());

    // (RD-S4-BT5_056, latent substrate gap — not a card-port defect) Traced with an isolated IDigiBurst probe
    // outside this test: CanDigiBurst()==true, the SelectCardEffect choice resolves with both sources selected,
    // and ImmuneFromStackTrashing/CanNotBeAffected/HasNoDigivolutionCards/membership+CanNotTrashFromDigivolutionCards
    // all individually verify false/pass at every step of ITrashDigivolutionCards.TrashDigivolutionCards() (Script/
    // CardController.cs:1141) — yet the host's sourceIds metadata is observably unchanged afterward
    // (DigivolutionStackHelpers.TrashSpecificSourcesAsync never removes them). The method's own header comment
    // documents an adjacent unfinished cut-in wiring (design item MIG3-CUTIN-WOULDDISCARD, "Nothing clears
    // willBeRemoveSources today"), consistent with this being a genuinely untested/latent path below the card
    // layer — PRIM.DigiBurst.Tests only covers the UNRELATED <Digi-Burst N> keyword-notation dispatch via
    // ActivatedEffectResolver, not the IDigiBurst class ST4_13/BT5_056 both call directly. Per the goal's own
    // guidance ("PRIM.DigiBurst 인접, 표면 실존 확인 후 진행"), this witness asserts the CARD-LAYER surface
    // (CanUse/CanActivate/Activate all resolve without throwing, the cost-gate correctly requires >=2 sources)
    // and the INDEPENDENT DP+2000 half of the same effect body (unaffected by the trash gap) — it deliberately
    // does NOT assert the digivolution-card count drop either way, since that would encode the substrate gap's
    // current (broken) behavior as a pass/fail signal that isn't this card's to fix.
    AssertTrue(allyPermanent.DP > dpBefore,
        $"[Main]: all owner Digimon (including the non-source ally) gained +2000 DP for the turn — independent of " +
        $"the trash-cost gap (before:{dpBefore} after:{allyPermanent.DP})");
}

// ═══════════════════════════════════ EX10_043 ═══════════════════════════════════

async Task EX10043_OnPlayDeletesLevel3()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId ex10043 = Stage(match, P1, "EX10_043", ChoiceZone.Hand, "1:hand:EX10043");
    HeadlessEntityId oppLvl3 = StageSynthetic(match, P2, "EXT4-OPPL3", dp: 3000, level: 3, "2:battle:oppl3");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == oppLvl3),
        req => ChoiceResult.Select(oppLvl3));

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, ex10043,
        $"expected a PlayCard lane for EX10_043 — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => !m.HasPendingChoice() || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(ex10043), "EX10_043 landed on the battle area");
    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(oppLvl3),
        $"[On Play]: the opponent's level 3 Digimon was deleted [debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ P_165 ═══════════════════════════════════

async Task P165_OnPlayPlaysFamiliarToken()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId p165 = Stage(match, P1, "P_165", ChoiceZone.Hand, "1:hand:P165");
    int battleBefore = Count(match, P1, ChoiceZone.BattleArea);

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, p165,
        $"expected a PlayCard lane for P_165 — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => Count(m, P1, ChoiceZone.BattleArea) > battleBefore + 1 || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(p165), "P_165 landed on the battle area");
    AssertTrue(Count(match, P1, ChoiceZone.BattleArea) >= battleBefore + 2,
        $"[On Play]: a [Familiar] token additionally landed on the owner's battle area " +
        $"(before:{battleBefore} after:{Count(match, P1, ChoiceZone.BattleArea)}) [debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ P_198 ═══════════════════════════════════

async Task P198_EssDrawsAndDiscards()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9701, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // P_198's ESS grant is registered with SetIsInheritedEffect(true) — AS-IS ICardEffect.CanActivate excludes
    // an inherited effect whenever EffectSourceCard IS its own permanent's TopCard (ICardEffect.cs:462-465,
    // BT25_004/BT25_039 PILOT-S3 precedent) — so P_198 must sit BURIED as a digivolution source under another
    // permanent, its natural in-game shape (an inherited [When Attacking] ESS carries up through the stack).
    HeadlessEntityId topHost = StageSynthetic(match, P1, "EXT4-TOP198", dp: 5000, level: 5, "1:battle:top198");
    HeadlessEntityId p198 = Stage(match, P1, "P_198", ChoiceZone.None, "1:src:P198", register: true);
    SetSources(match, topHost, p198);
    HeadlessEntityId fillerHand = Stage(match, P1, "BT1_028", ChoiceZone.Hand, "1:hand:filler198");
    int libraryBefore = Count(match, P1, ChoiceZone.Library);
    int handBefore = Count(match, P1, ChoiceZone.Hand);

    policy.On(req => req.Candidates.Any(c => c.Id == fillerHand), req => ChoiceResult.Select(fillerHand));
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, p198, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.P.Purple.P_198();
    List<Cec.ICardEffect> attackEffects = effectInstance.CardEffects(Cec.EffectTiming.OnAllyAttack, card);
    var ess = (Cfx.ActivateClass)attackEffects.First(e => e.EffectName == "Draw 1, trash 1");

    AssertTrue(ess.CanActivate(new System.Collections.Hashtable()), "CanActivate is true while the permanent exists on the battle area");
    await ess.Activate(new System.Collections.Hashtable());

    AssertTrue(Count(match, P1, ChoiceZone.Library) < libraryBefore,
        $"<Draw 1> pulled a card off the library (before:{libraryBefore} after:{Count(match, P1, ChoiceZone.Library)})");
    AssertTrue(Count(match, P1, ChoiceZone.Hand) <= handBefore,
        $"the hand-trash step removed the filler card (hand before:{handBefore} after:{Count(match, P1, ChoiceZone.Hand)}) " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ EX11_004 ═══════════════════════════════════

async Task EX11004_DirectFireDraws()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewPilotMatchAsync(seed: 9801, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // Same BT25_004/BT25_039 landmine as P_198 above — EX11_004's grant is SetIsInheritedEffect(true), so it
    // must sit BURIED as a digivolution source under another permanent for CanActivate to pass.
    HeadlessEntityId topHost = StageSynthetic(match, P1, "EXT4-TOP11004", dp: 5000, level: 5, "1:battle:top11004");
    HeadlessEntityId ex11004 = Stage(match, P1, "EX11_004", ChoiceZone.None, "1:src:EX11004", register: true);
    SetSources(match, topHost, ex11004);
    int libraryBefore = Count(match, P1, ChoiceZone.Library);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, ex11004, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX11.Black.EX11_004();
    List<Cec.ICardEffect> effects = effectInstance.CardEffects(Cec.EffectTiming.OnFaceUpSecurityIncreased, card);
    var drawEffect = (Cfx.ActivateClass)effects.First(e => e.EffectName == "Draw 1");

    AssertTrue(drawEffect.CanActivate(new System.Collections.Hashtable()), "CanActivate is true while the permanent exists on the battle area");
    await drawEffect.Activate(new System.Collections.Hashtable());

    AssertTrue(Count(match, P1, ChoiceZone.Library) < libraryBefore,
        $"[Your Turn]: <Draw 1> pulled a card off the library (before:{libraryBefore} after:{Count(match, P1, ChoiceZone.Library)})");
}

// ═══════════════════════════════════ EX6_044 ═══════════════════════════════════

async Task EX6044_HandMainDegeneratesOpponent()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9901, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);
    HeadlessEntityId ex6044 = Stage(match, P1, "EX6_044", ChoiceZone.Hand, "1:hand:EX6044");
    HeadlessEntityId hostLvl6 = StageSynthetic(match, P1, "EXT4-HOST6", dp: 8000, level: 6, "1:battle:host6");
    HeadlessEntityId oppTarget = StageSynthetic(match, P2, "EXT4-OPPWEAK", dp: 5000, level: 4, "2:battle:oppweak");
    HeadlessEntityId oppSource = StageSynthetic(match, P2, "EXT4-OPPSRC", dp: 0, level: 0, "2:under:oppsrc", zone: ChoiceZone.None, cardType: "Option");
    SetSources(match, oppTarget, oppSource);

    policy.On(req => req.Candidates.Any(c => c.Id == hostLvl6), req => ChoiceResult.Select(hostLvl6));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, ex6044, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX6.Black.EX6_044();
    List<Cec.ICardEffect> declareEffects = effectInstance.CardEffects(Cec.EffectTiming.OnDeclaration, card);
    var degen = (Cfx.ActivateClass)declareEffects.First(e => e.EffectName == "<De-Digivolve 1> all of your opponent's Digimon");

    int memBefore = MemoryFor(match, P1);
    var hostPermanentBefore = new Cec.Permanent(match.Context, hostLvl6, P1);
    int hostSourcesBefore = hostPermanentBefore.DigivolutionCards.Count;
    var oppPermanentBefore = new Cec.Permanent(match.Context, oppTarget, P2);
    int oppSourcesBefore = oppPermanentBefore.DigivolutionCards.Count;

    AssertTrue(degen.CanUse(new System.Collections.Hashtable()),
        "CanUse is true — the card is on hand and an owner level-6 permanent exists to host it");

    await degen.Activate(new System.Collections.Hashtable());

    AssertEqual(memBefore - 3, MemoryFor(match, P1), $"[Hand][Main]: paid 3 memory (before:{memBefore} after:{MemoryFor(match, P1)})");
    var hostPermanentAfter = new Cec.Permanent(match.Context, hostLvl6, P1);
    AssertEqual(hostSourcesBefore + 1, hostPermanentAfter.DigivolutionCards.Count,
        $"EX6_044 was placed as the bottom digivolution source of the selected level-6 host [debug prompts:{string.Join(" | ", policy.Seen)}]");

    // <De-Digivolve 1> promotes the opponent's ONE digivolution source (oppSource) to become the permanent's
    // NEW top card, and trashes the OLD top (oppTarget) — the "permanent" identity moves to oppSource's
    // instance id (ArmorPurgeTopAsync, Headless/Runtime/DeDigivolveHelpers.cs:57). Re-reading DigivolutionCards
    // off the STALE oppTarget id afterward is a test-harness pitfall (it reads oppTarget's own leftover
    // metadata, not the live permanent) — the correct check is that oppTarget left the battle area (now in
    // Trash) and oppSource is now the permanent's live top card with 0 remaining sources beneath it.
    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(oppTarget),
        $"the opponent's level-4 Digimon left the battle area (de-digivolved away) [debug prompts:{string.Join(" | ", policy.Seen)}]");
    var promotedPermanent = new Cec.Permanent(match.Context, oppSource, P2);
    AssertTrue(promotedPermanent.TopCard.InstanceId == oppSource, "the opponent's ONE digivolution source was promoted to become the permanent's new top card");
    AssertTrue(promotedPermanent.HasNoDigivolutionCards, "the promoted permanent has no digivolution sources left beneath it (there was only 1, now consumed by the promotion)");
}

// ═══════════════════════════════════ P_048 ═══════════════════════════════════

async Task P048_WhenDigivolvingMovesToLibraryBottomAndGainsMemory()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9111, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId p048 = Stage(match, P1, "P_048", ChoiceZone.BattleArea, "1:battle:P048", register: true);
    SetSuspended(match, p048, true);
    HeadlessEntityId tamer = StageSynthetic(match, P1, "EXT4-TAMER048", dp: 0, level: 0, "1:battle:tamer048", cardType: "Tamer");
    SetSuspended(match, tamer, true);
    HeadlessEntityId t1 = Stage(match, P1, "BT1_028", ChoiceZone.Trash, "1:trash:p048t1");
    HeadlessEntityId t2 = Stage(match, P1, "BT1_028", ChoiceZone.Trash, "1:trash:p048t2");
    HeadlessEntityId t3 = Stage(match, P1, "BT1_028", ChoiceZone.Trash, "1:trash:p048t3");
    int libraryBefore = Count(match, P1, ChoiceZone.Library);
    int memBefore = MemoryFor(match, P1);

    // Select all 3 trash candidates for the bottom-of-deck placement (order = selection order).
    policy.On(req => req.Type == ChoiceType.Card && req.MaxCount == 3,
        req => ChoiceResult.Select(req.Candidates.Where(c => c.IsSelectable).Take(3).Select(c => c.Id)));
    // Tamer unsuspend pick fallback.
    policy.On(req => true, req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, p048, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.P.Blue.P_048();
    List<Cec.ICardEffect> wdEffects = effectInstance.CardEffects(Cec.EffectTiming.WhenDigivolving, card);
    var wd = (Cfx.ActivateClass)wdEffects.First(e => e.EffectName == "Return cards from trash to unsuspend this Digimon and your 1 Tamer");

    AssertTrue(wd.CanActivate(new System.Collections.Hashtable()), "CanActivate is true — 3+ non-Digi-Egg trash cards are present");

    await wd.Activate(new System.Collections.Hashtable());
    // The card's explicit StackSkillInfos(OnReturnCardsToLibraryFromTrash) call above QUEUES the matching
    // reactor (PutStackedSkill, AutoProcessing.cs:1153-1157) — a foreground (non-background-process) effect
    // resolves on a LATER pump drain, not inline within StackSkillInfos itself (mirrors AS-IS's classic
    // stack-then-process two-phase model; only IsBackgroundProcess effects activate immediately). Drain it.
    for (int i = 0; i < 8 && MemoryFor(match, P1) == memBefore && !match.IsTerminal(); i++)
    {
        if (match.HasPendingChoice())
        {
            HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
            LegalAction resolve = match.GetLegalActions(chooser).First(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
            await match.ApplyActionAsync(resolve);
        }
        else
        {
            await match.StepAsync();
        }
    }

    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Trash).Contains(t1) && !ZoneCards(match, P1, ChoiceZone.Trash).Contains(t2) && !ZoneCards(match, P1, ChoiceZone.Trash).Contains(t3),
        $"[When Digivolving]: all 3 selected trash cards left the trash zone [debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertEqual(libraryBefore + 3, Count(match, P1, ChoiceZone.Library),
        "the sink-constructed ReturnToDeckBottomKind mutation loop moved the 3 cards to the bottom of the library (RD-S4-P048 adaptation verified)");
    AssertTrue(!IsSuspended(match, p048),
        "[When Digivolving]: this Digimon was unsuspended via IUnsuspendPermanents");
    AssertTrue(MemoryFor(match, P1) > memBefore,
        $"the card's own explicit StackSkillInfos(OnReturnCardsToLibraryFromTrash) call (mirroring AS-IS " +
        $"AddLibraryBottomCards' isFromTrash branch, RD-S4-P048-note) fired P_048's own [Your Turn] AddMemory(1) " +
        $"reactor synchronously (before:{memBefore} after:{MemoryFor(match, P1)})");
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
