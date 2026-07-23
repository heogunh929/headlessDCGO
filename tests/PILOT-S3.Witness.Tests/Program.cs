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
// PILOT-S3 witness 스위트 — Sonnet 트랜치 S3 10장, 카드당 1개 이상(PILOT-S1/S2.Witness.Tests 템플릿 복제).
// 표준 템플릿: DcgoMatch.CreatePumpDriven + 에이전트 액션 구동(ApplyActionAsync); 효과-내부 Select*/Optional
// 프롬프트는 PolicyChoiceProvider 좌석으로 응답.
// W1 (BT25_004)은 G-Link 원장 P2-④(창-변조 witness)를 해소하는 핵심 카드 — 실제 ILinkCard 흐름을 직접
// 구동해 "링크 시도 → WhenWouldLink 창 발화 → 개입(비용 -1) → 재산정 반영(실 결제액)"을 전부 관측한다.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT25_004 W1 (G-Link P2-④): 링크 시도 → WhenWouldLink 창 발화(OptionalEffect) → GrantedReduceLinkCostClass(-1) → 재산정(기저1→0, 결제0)", BT25004_WhenWouldLinkReducesCost),
    ("BT25_061 W1: [Start of Your Main Phase] 발화 → Appmon 트레잇 손패 트래시 → Draw 1 + 메모리 +1", BT25061_StartOfMainDiscardDrawsAndGainsMemory),
    ("BT25_102 W1: [Blocker]/[Link+1] 술어 — 흑/적+[TS] 배틀에어리어 디지몬에 TRUE, 비-[TS]에 FALSE(부정 단언)", BT25102_BlockerAndLinkMaxPredicates),
    ("BT25_102 W2 (P1-1 face-gate): Ignore Color Requirement 게이트 — 시큐리티 전부 face-down이면 TRUE, 1장 face-up이면 FALSE(SecurityFaceState)", BT25102_IgnoreColorGateReadsSecurityFaceState),
    ("BT25_102 W3 (REPAIR stale-STOP): [Main] ReplaceBottomSecurityWithFaceUpOption 실행(RD-P6C3-B1 UN-STOP) — 바텀 시큐리티→손패, self가 face-up 바텀 시큐리티로 착지", BT25102_MainReplacesBottomSecurity),
    ("BT25_101 W1: [Link Condition] AddLinkConditionClass.GetLinkCondition — cost=3 + [Vulcanusmon]명 digimonCondition 술어 평가", BT25101_LinkConditionPredicateAndCost),
    ("EX7_058 W1: [On Play] 발화 → 상대 디지몬 선택 → UntilOwnerTurnEndEffects에 [End of Attack] 삭제 그랜트 등재", EX7058_OnPlayGrantsEndOfAttackDelete),
    ("EX7_010 W1: [When Digivolving] 발화 → 진화원의 Option 카드 1장 트래시", EX7010_WhenDigivolvingTrashesOption),
    ("BT17_068 W1: [On Deletion] 발화 → [Dark Masters] 레벨6 카드 손패에서 무료 플레이", BT17068_OnDeletionPlaysDarkMastersFree),
    ("BT17_095 W1: [Main] 발화 → [Agumon] 손패 무료 플레이", BT17095_MainPlaysAgumonFree),
    ("EX2_072 W1: [Main] 발화(오너 배틀 디지몬 부재 → 진화 불가 분기) → 리빌 5장 중 디지몬 1장 손패 추가", EX2072_MainRevealsAndAddsDigimonToHand),
    ("BT21_058 W1: [On Play] 발화 → 리빌 3장 중 [Vemmon] 텍스트 카드 1장 손패 추가", BT21058_OnPlayRevealsAddsVemmonCard),
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

// ═══════════════════════════════════ BT25_004 (G-Link P2-④) ═══════════════════════════════════

async Task BT25004_WhenWouldLinkReducesCost()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 8101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    // BT25_004's own [Your Turn] WhenWouldLink grant is registered with SetIsInheritedEffect(true) — AS-IS
    // ICardEffect.CanActivate excludes an inherited effect whenever EffectSourceCard IS its own permanent's
    // TopCard (ICardEffect.cs:462-465), so BT25_004 must sit BURIED as a digivolution SOURCE under another
    // permanent (its natural in-game shape: an earlier Tapmon-line stage under a further digivolution),
    // exactly the same structural requirement documented on the BT25_039 witness in PILOT-S2.
    // Registration order matters: CardEffectRegistrar.RegisterOnEnterPlay probes each card's OnDeclaration arm,
    // and BT25_061's own [Link] keyword (CardEffectFactory.LinkEffect) scans ALL of the owner's EXISTING battle
    // permanents at registration time via card.LinkConditionOf() — a pre-existing substrate gap
    // (CheckEffectDisabledClass.PotentiallyDisablingEffects, CheckEffectDisabledClass.cs:116, dereferences
    // GManager.instance.turnStateMachine.gameContext.Players with no null-guard, unlike the guarded
    // "before start of game" region a few lines up in ICardEffect.CanActivate) that NPEs whenever a
    // LinkEffect-bearing card registers while ANY other permanent already sits on its owner's battle area.
    // EXEMPLAR-GLINK's own witnesses avoid this identical landmine by always staging the [Link] card (EX10_029)
    // before any other battle permanent exists — followed here: BT25_061 registers FIRST (empty battle area).
    HeadlessEntityId linkCard = Stage(match, P1, "BT25_061", ChoiceZone.Hand, "1:hand:BT25061", register: true);
    HeadlessEntityId topHost = StageSynthetic(match, P1, "EXT3-TOP", dp: 5000, level: 5, "1:battle:top");
    HeadlessEntityId bt25004Src = Stage(match, P1, "BT25_004", ChoiceZone.None, "1:src:BT25004", register: true);
    SetSources(match, topHost, bt25004Src);
    int memBefore = MemoryFor(match, P1);

    // BT25_004's grant is isOptional=true ("may reduce Link cost by 1") — an OptionalEffect seat.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var linkCardSource = new Cec.CardSource(match.Context, linkCard, P1);
    var topPermanent = new Cec.Permanent(match.Context, topHost, P1);

    // Base cost sanity BEFORE any window fires (BT25_061's own declared link cost).
    int baseCost = linkCardSource.GetChangedLinkCost(topPermanent, Script.SelectCardEffect.Root.Hand);
    AssertEqual(1, baseCost, "BT25_061's own AddSelfLinkConditionStaticEffect declares base link cost 1");

    var cause = new Cfx.ActivateClass();
    cause.SetUpICardEffect("PILOT-S3-link-cause", _ => true, linkCardSource);

    var iLinkCard = new Script.ILinkCard(true, linkCardSource, topPermanent, cause);
    await iLinkCard.LinkCard();

    AssertTrue(iLinkCard.WasLinked, "BT25_061 linked to the host permanent that carries BT25_004 as a digivolution source");
    AssertTrue(LinkedCardsOf(match, topHost).Contains(linkCard), "BT25_061 registered under the host's LinkedCards");
    AssertEqual(memBefore, MemoryFor(match, P1),
        $"[Your Turn]: the WhenWouldLink window fired BEFORE payment, BT25_004's GrantedReduceLinkCostClass(-1) " +
        $"folded the base cost 1 down to 0 (Math.Max(0,..) clamp) -> ILinkCard paid 0 memory " +
        $"(before:{memBefore} after:{MemoryFor(match, P1)}) [debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT25_061 ═══════════════════════════════════

async Task BT25061_StartOfMainDiscardDrawsAndGainsMemory()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 8201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    Stage(match, P1, "BT25_061", ChoiceZone.BattleArea, "1:battle:BT25061", register: true);
    // A synthetic filler (no CardEffects class of its own) — a REAL card carrying its own [Link] keyword
    // (e.g. EX10_029) would re-trigger the CheckEffectDisabledClass registration-time NPE documented on the
    // BT25_004 witness above, since BT25_061 already sits on the battle area by the time this registers.
    HeadlessEntityId appmonFiller = StageSynthetic(match, P1, "EXT3-APPFILL", dp: 1000, level: 3, "1:hand:appmonfiller",
        zone: ChoiceZone.Hand, traits: new[] { "Appmon" });
    int libraryBefore = Count(match, P1, ChoiceZone.Library);
    int memBefore = MemoryFor(match, P1);

    // [Start of Your Main Phase] isOptional=true -> OptionalEffect seat, THEN a hand-discard select seat.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    policy.On(req => req.Candidates.Any(c => c.Id == appmonFiller), req => ChoiceResult.Select(appmonFiller));

    await PassTurnAsync(match, P1);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P2));
    await PassTurnAsync(match, P2);
    await DriveUntilAsync(match, m => (Count(m, P1, ChoiceZone.Library) < libraryBefore) || m.IsTerminal());

    AssertTrue(Count(match, P1, ChoiceZone.Library) < libraryBefore,
        $"[Start of Your Main Phase]: trashing the [Appmon] hand card triggered <Draw 1> (library before:{libraryBefore} after:{Count(match, P1, ChoiceZone.Library)}) " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(appmonFiller), "the [Appmon] card left the hand (trashed)");
    AssertTrue(MemoryFor(match, P1) > memBefore,
        $"AddMemory(1, activateClass) produced a real memory gain (before:{memBefore} after:{MemoryFor(match, P1)})");
}

// ═══════════════════════════════════ BT25_102 ═══════════════════════════════════

async Task BT25102_BlockerAndLinkMaxPredicates()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewPilotMatchAsync(seed: 8301, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt25102 = Stage(match, P1, "BT25_102", ChoiceZone.Security, "1:sec:BT25102", register: true);
    HeadlessEntityId matching = StageSynthetic(match, P1, "EXT3-TS", dp: 5000, level: 4, "1:battle:ts", traits: new[] { "TS" }, colors: new[] { "Black" });
    HeadlessEntityId nonMatching = StageSynthetic(match, P1, "EXT3-NOTS", dp: 5000, level: 4, "1:battle:nots", traits: new[] { "Hero" }, colors: new[] { "Black" });

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt25102, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Black.BT25_102();
    List<Cec.ICardEffect> noneEffects = effectInstance.CardEffects(Cec.EffectTiming.None, card);

    var blocker = (Cfx.BlockerClass)noneEffects.First(e => e.GetType().Name == "BlockerClass");
    var linkMax = (Cfx.ChangeLinkMaxClass)noneEffects.First(e => e.GetType().Name == "ChangeLinkMaxClass");

    var matchingPerm = new Cec.Permanent(match.Context, matching, P1);
    var nonMatchingPerm = new Cec.Permanent(match.Context, nonMatching, P1);

    AssertTrue(blocker.IsBlocker(matchingPerm), "Blocker predicate TRUE for an owner battle Digimon w/ Black/Red color + [TS] trait");
    AssertTrue(!blocker.IsBlocker(nonMatchingPerm), "negative: Blocker predicate FALSE for a non-[TS] Digimon");
    AssertTrue(linkMax.PermanentCondition(matchingPerm), "ChangeLinkMax predicate TRUE for the same matching permanent");
    AssertEqual(6, linkMax.GetLinkMax(5, matchingPerm, invertValue: 0), "GetLinkMax(+1) folds base 5 -> 6 for a matching permanent");
    AssertTrue(!linkMax.PermanentCondition(nonMatchingPerm), "negative: ChangeLinkMax predicate FALSE for a non-[TS] permanent");
}

// BT25_102 W2 (P1-1 REPAIR) — the "Ignore Color Requirement" gate (AS-IS `SecurityCards.Count(cs =>
// !cs.IsFlipped) == 0`) reads face state via SecurityFaceState (never the raw field-ACE IsFlipped flag — the
// mirror never stamps that for a security card; Permanent.cs FoldLinkedMax precedent, commit 40d1eaee).
// Faithful black-box surface: CardSource.IgnoreColorConditionActive() (AS-IS CardSource.MatchColorRequirement's
// ignore-colour scan) — the production consumer (CardSource.cs:2546-2548).
async Task BT25102_IgnoreColorGateReadsSecurityFaceState()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewPilotMatchAsync(seed: 8302, MonoDecks("BT1_028", "BT1_028"));
    EngineContext ctx = match.Context;
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt25102 = Stage(match, P1, "BT25_102", ChoiceZone.Security, "1:sec:BT25102gate", register: true);
    HeadlessEntityId other = StageSynthetic(match, P1, "EXT3-SECGATE", dp: 1000, level: 3, "1:sec:gateother", zone: ChoiceZone.Security);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(ctx);
    var card = new Cec.CardSource(ctx, bt25102, P1);
    AssertTrue(card.IgnoreColorConditionActive(),
        "all-face-down security (AS-IS default): the Ignore Color Requirement gate is TRUE");

    SecurityFaceState.Stamp(ctx.CardInstanceRepository, other, faceUp: true);
    AssertTrue(!card.IgnoreColorConditionActive(),
        "one face-up security card: the Ignore Color Requirement gate is FALSE");
}

// BT25_102 W3 (REPAIR batch A — stale-STOP strike) — the [Main] body's
// ReplaceBottomSecurityWithFaceUpOptionEffect is PORTED (RD-P6C3-B1 UN-STOP, CardEffectFactory.cs:259-269);
// the earlier "throws NotSupportedException at activation" note was stale. This drives the [Main] OptionSkill
// ActivateClass and proves the previously-"throwing" Replace path RUNS: the bottom security card moves to hand
// and BT25_102 lands as the face-up bottom security card (BT25_094 LT-B W2 sibling).
async Task BT25102_MainReplacesBottomSecurity()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 8303, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT25_102", ChoiceZone.Hand, "1:hand:BT25102main");
    // Bottom security card (the Replace target).
    HeadlessEntityId sec = StageSynthetic(match, P1, "EXT3-SECBOT", dp: 0, level: 1, "1:sec:secbot",
        cardType: "Digimon", zone: ChoiceZone.Security);
    // The follow-up "play 1 [TS] Digimon for -3" hand prompt is optional — skip it (isolate the Replace).
    policy.On(req => true, req => ChoiceResult.Skip(), oneShot: false);

    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        var cs = new Cec.CardSource(match.Context, bt, P1);
        var main = (Cfx.ActivateClass)cs.EffectList(Cec.EffectTiming.OptionSkill).First();
        await main.Activate(new System.Collections.Hashtable());
    }
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.Hand).Contains(sec)
        || ZoneCards(m, P1, ChoiceZone.Security).Contains(bt) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.Hand).Contains(sec),
        $"ReplaceBottomSecurityWithFaceUpOption(RD-P6C3-B1 UN-STOP): the old bottom security card is now in hand " +
        $"[hand:{string.Join(",", ZoneCards(match, P1, ChoiceZone.Hand).Select(i => i.Value))}]");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Security).Contains(bt),
        "ReplaceBottomSecurityWithFaceUpOption: BT25_102 is now placed as the face-up bottom security card");
}

// ═══════════════════════════════════ BT25_101 ═══════════════════════════════════

async Task BT25101_LinkConditionPredicateAndCost()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewPilotMatchAsync(seed: 8401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt25101 = Stage(match, P1, "BT25_101", ChoiceZone.Hand, "1:hand:BT25101");
    HeadlessEntityId vulcanusmon = StageSynthetic(match, P1, "EXT3-VULC", dp: 8000, level: 6, "1:battle:vulc", name: "Vulcanusmon");
    HeadlessEntityId other = StageSynthetic(match, P1, "EXT3-OTHER", dp: 8000, level: 6, "1:battle:other", name: "NotVulcanusmon");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt25101, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Black.BT25_101();
    List<Cec.ICardEffect> noneEffects = effectInstance.CardEffects(Cec.EffectTiming.None, card);

    var addLinkCondition = (Cfx.AddLinkConditionClass)noneEffects.First(e => e.GetType().Name == "AddLinkConditionClass");

    Cec.LinkCondition? linkCondition = addLinkCondition.GetLinkCondition(card);
    AssertTrue(linkCondition is not null, "BT25_101's AddLinkConditionClass returns a real LinkCondition for its own card instance");
    AssertEqual(3, linkCondition!.cost, "the declared link cost is 3");

    var vulcPerm = new Cec.Permanent(match.Context, vulcanusmon, P1);
    var otherPerm = new Cec.Permanent(match.Context, other, P1);
    AssertTrue(linkCondition.digimonCondition(vulcPerm), "digimonCondition TRUE for a [Vulcanusmon]-named permanent");
    AssertTrue(!linkCondition.digimonCondition(otherPerm), "negative: digimonCondition FALSE for a non-[Vulcanusmon] permanent");
}

// ═══════════════════════════════════ EX7_058 ═══════════════════════════════════

async Task EX7058_OnPlayGrantsEndOfAttackDelete()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 8501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);
    HeadlessEntityId ex7058 = Stage(match, P1, "EX7_058", ChoiceZone.Hand, "1:hand:EX7058");
    HeadlessEntityId oppTarget = StageSynthetic(match, P2, "EXT3-OPPD", dp: 3000, level: 4, "2:battle:oppd");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == oppTarget),
        req => ChoiceResult.Select(oppTarget));

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, ex7058,
        $"expected a PlayCard lane for EX7_058 — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => !m.HasPendingChoice() || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(ex7058), "EX7_058 landed on the battle area");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var oppPerm = new Cec.Permanent(match.Context, oppTarget, P2);
    bool grantFound = oppPerm.UntilOwnerTurnEndEffects.Any(getEffect => getEffect(Cec.EffectTiming.OnEndAttack) != null);
    AssertTrue(grantFound,
        $"[On Play]: the selected opponent Digimon gained an [End of Attack] delete-this-Digimon grant " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ EX7_010 ═══════════════════════════════════

async Task EX7010_WhenDigivolvingTrashesOption()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 8601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);
    HeadlessEntityId host = StageSynthetic(match, P1, "EXT3-HOST3", dp: 3000, level: 3, "1:battle:host3", colors: new[] { "Red" });
    HeadlessEntityId optionSrc = StageSynthetic(match, P1, "EXT3-OPT", dp: 0, level: 0, "1:under:opt", zone: ChoiceZone.None, cardType: "Option");
    SetSources(match, host, optionSrc);
    HeadlessEntityId ex7010 = Stage(match, P1, "EX7_010", ChoiceZone.Hand, "1:hand:EX7010");

    // [When Digivolving] isOptional=true (maxCountPerTurn 1) -> OptionalEffect seat, THEN the trash-option select.
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    policy.On(req => req.Candidates.Any(c => c.Id == optionSrc), req => ChoiceResult.Select(optionSrc));

    LegalAction onto = FindDigivolveLane(match, P1, ex7010, host)
        ?? throw new InvalidOperationException(
            $"expected a cost-2 Red lvl-3 digivolve lane EX7_010 -> host — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, onto);
    await DriveUntilAsync(match, m => !m.HasPendingChoice() || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(ex7010), "EX7_010 landed on the battle area");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Trash).Contains(optionSrc),
        $"[When Digivolving]: the Option digivolution-source card was trashed via SelectTrashDigivolutionCards " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT17_068 ═══════════════════════════════════

async Task BT17068_OnDeletionPlaysDarkMastersFree()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 8701, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);
    HeadlessEntityId bt17068 = Stage(match, P1, "BT17_068", ChoiceZone.BattleArea, "1:battle:BT17068", register: true);
    HeadlessEntityId darkMastersCard = StageSynthetic(match, P1, "EXT3-DM", dp: 5000, level: 6, "1:hand:dm",
        zone: ChoiceZone.Hand, traits: new[] { "Dark Masters" });

    // [On Deletion] isOptional=true -> OptionalEffect seat; only 1 hand candidate exists so the hand-vs-trash
    // bool pre-select never opens a real prompt (SetBool(canSelectHand) is a plain setter, not a ChoiceRequest).
    policy.On(req => req.Type == ChoiceType.OptionalEffect, req =>
        ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);
    policy.On(req => req.Candidates.Any(c => c.Id == darkMastersCard), req => ChoiceResult.Select(darkMastersCard));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    HeadlessEntityId fillerId = Stage(match, P1, "BT1_028", ChoiceZone.Hand, "1:hand:filler-del068");
    var causerCard = new Cec.CardSource(match.Context, fillerId, P1);
    var causer = new Cfx.ActivateClass();
    causer.SetUpICardEffect("test-harness delete", _ => true, causerCard);

    var targetPermanent = new Cec.Permanent(match.Context, bt17068, P1);
    await Cec.CardEffectCommons.DeletePeremanentAndProcessAccordingToResult(
        new List<Cec.Permanent> { targetPermanent }, causer, null, null);

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(darkMastersCard),
        $"[On Deletion]: the [Dark Masters] level-6 hand card was played for free after BT17_068 was deleted by an effect " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(darkMastersCard), "the played card left the hand");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Trash).Contains(bt17068), "BT17_068 itself ended up in the trash (deleted)");
}

// ═══════════════════════════════════ BT17_095 ═══════════════════════════════════

async Task BT17095_MainPlaysAgumonFree()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 8801, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);
    // BT17_095 is dual-color Red+Blue — OptionColorRequirement.Matches requires EVERY one of the option's
    // colors to be covered by SOME owner field permanent (optionColors.All(...), not "any") — so both a
    // Red and a Blue permanent must be present (BT9_103 PILOT-S1 precedent covers only the single-color case).
    StageSynthetic(match, P1, "EXT3-BLUE095", dp: 3000, level: 3, "1:battle:blue095", colors: new[] { "Blue" });
    StageSynthetic(match, P1, "EXT3-RED095", dp: 3000, level: 3, "1:battle:red095", colors: new[] { "Red" });
    HeadlessEntityId bt17095 = Stage(match, P1, "BT17_095", ChoiceZone.Hand, "1:hand:BT17095");
    HeadlessEntityId agumon = StageSynthetic(match, P1, "EXT3-AGU", dp: 1000, level: 3, "1:hand:agu",
        zone: ChoiceZone.Hand, name: "Agumon");

    policy.On(req => req.Candidates.Any(c => c.Id == agumon), req => ChoiceResult.Select(agumon));

    LegalAction option = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, bt17095,
        $"BT17_095's own [Main] OptionSkill must be offered (CanTriggerOptionMainEffect) — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, option);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(agumon) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(agumon),
        $"[Main]: the [Agumon] hand card was played without paying its cost " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ EX2_072 ═══════════════════════════════════

async Task EX2072_MainRevealsAndAddsDigimonToHand()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 8901, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    StageSynthetic(match, P1, "EXT3-TAMER", dp: 0, level: 0, "1:battle:tamer", cardType: "Tamer");
    HeadlessEntityId ex2072 = Stage(match, P1, "EX2_072", ChoiceZone.Hand, "1:hand:EX2072");
    int libraryBefore = Count(match, P1, ChoiceZone.Library);

    // No owner battle-area Digimon exists -> CanSelectCardCondition (digivolve target) is always false, so the
    // reveal falls straight to the add-hand branch (canNoSelect:false — a Digimon among the 5 reveals is forced).
    // The remaining-cards routing (ReturnRevealedCardsToLibraryBottom, >=2 cards) opens an ORDERING prompt
    // requiring ALL selectable candidates picked (MinCount==MaxCount==count) — a naive "pick 1" catch-all
    // trips its range validator, so this answers every prompt with however many it actually demands.
    policy.On(req => true, req =>
    {
        List<HeadlessEntityId> selectable = req.Candidates.Where(c => c.IsSelectable).Select(c => c.Id).ToList();
        return req.MaxCount > 1
            ? ChoiceResult.Select(selectable.Take(Math.Max(req.MinCount, 1)))
            : ChoiceResult.Select(selectable.First());
    }, oneShot: false);

    LegalAction option = RequireLane(match, P1, HeadlessActionTypes.ActivateOption, ex2072,
        $"EX2_072's own [Main] OptionSkill must be offered — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, option);
    await DriveUntilAsync(match, m => !m.HasPendingChoice() || m.IsTerminal());

    AssertEqual(libraryBefore - 1, Count(match, P1, ChoiceZone.Library),
        $"[Main]: 5 cards were revealed, 1 Digimon left the library permanently (added to hand), 4 returned to the bottom " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT21_058 ═══════════════════════════════════

async Task BT21058_OnPlayRevealsAddsVemmonCard()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 9001, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt21058 = Stage(match, P1, "BT21_058", ChoiceZone.Hand, "1:hand:BT21058");
    // MatchSetupConfig shuffles each deck at setup, so a hand-built ordered deck array can't guarantee a real
    // [Vemmon]-text card lands in the top-3 reveal window; instead re-point the CURRENT top library card's
    // definition to [BT11_061] "Vemmon" post-shuffle (same instance id, deterministic top-of-library swap).
    RepointLibraryTop(match, P1, "BT11_061", count: 1);
    int libraryBefore = Count(match, P1, ChoiceZone.Library);

    policy.On(req => req.Candidates.Any(c => CardNumberOf(match, c.Id) == "BT11_061"),
        req => ChoiceResult.Select(req.Candidates.First(c => CardNumberOf(match, c.Id) == "BT11_061").Id));
    // "place [Vemmon] under 1 of your Digimon" sub-step (canNoSelect:true, no owner Digimon here) — decline.
    policy.On(req => true, req => req.CanSkip ? ChoiceResult.Skip() : ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id), oneShot: false);

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, bt21058,
        $"expected a PlayCard lane for BT21_058 — lanes:{string.Join(",", Legal(match, P1).Select(a => a.ActionType))}");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.Hand).Any(id => CardNumberOf(m, id) == "BT11_061") || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(bt21058), "BT21_058 landed on the battle area");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Hand).Any(id => CardNumberOf(match, id) == "BT11_061"),
        $"[On Play]: the [Vemmon]-text card among the top 3 reveals was added to the hand " +
        $"[debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertEqual(libraryBefore - 3, Count(match, P1, ChoiceZone.Library),
        "the top 3 library cards were revealed and left the library (1 to hand, 2 to trash)");
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

// Re-points the top `count` library card instances (post-shuffle) to a different definition, in place —
// deterministic top-of-library control without fighting MatchSetupConfig's deck shuffle.
static void RepointLibraryTop(DcgoMatch match, HeadlessPlayerId player, string cardNumber, int count)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId(cardNumber);
    if (!ctx.CardRepository.TryGetCard(defId, out CardRecord? existing) || existing is null)
    {
        throw new InvalidOperationException($"definition {cardNumber} not found in the loaded card database");
    }

    var zones = (IZoneStateReader)ctx.ZoneMover;
    foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.Library).Take(count))
    {
        if (ctx.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null)
        {
            ctx.CardInstanceRepository.Upsert(rec with { DefinitionId = defId });
        }
    }
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

static IReadOnlyList<HeadlessEntityId> LinkedCardsOf(DcgoMatch match, HeadlessEntityId hostId) =>
    match.Context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? rec) && rec is not null
        ? LinkHelpers.ReadLinkedCardIds(rec.Metadata)
        : Array.Empty<HeadlessEntityId>();

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
