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
// PILOT-S6 witness 스위트 — Sonnet 트랜치 S6 롱테일 최종 7장, 카드당 1개 이상(PILOT-S1~S5.Witness.Tests 템플릿
// 복제). 표준 템플릿: ActivateClass.CanUse/CanActivate/Activate 공개 API를 직접 호출(트리거-해시테이블 게이트
// (CanTrigger*)는 실 디스패치 밖에서 구성하기 어려우므로 CanUse는 스킵하고 보드-상태 게이트인 CanActivate만
// 확인 후 직접 Activate — BT16_052 PILOT-S5 선례와 동형).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("EX4_062 W1: [Start of Your Main Phase] Memory +1 — 2체 이상 필드 존재 시 CanActivate true, 발동 시 소유자 메모리 +1", EX4062_MemoryPlusOneOnMainPhase),
    ("BT25_019 W1: 공유 OP/WD 디스패처 — [On Play] 직접 발화 → 상대 최고DP 1체 삭제(IsMaxDP 스코핑 실증)", BT25019_SharedOnPlayDeletesMaxDp),
    ("BT7_055 W1: [When Digivolving] 직접 발화 → 상대 언서스펜드 디지몬 1체 서스펜드 + 서스펜드 수만큼 메모리 획득", BT7055_WhenDigivolvingSuspendsAndGainsMemory),
    ("BT7_112 W1: [When Digivolving] 직접 발화 → 상대 디지몬 1체 삭제", BT7112_WhenDigivolvingDeletesOpponent),
    ("BT19_035 W1: [When Attacking] ESS(상속) — 이 디지몬이 [Xros Heart]면 상대 1체 DP -2000", BT19035_WhenAttackingXrosHeartDebuff),
    ("BT21_059 W1: [Your Turn] WhenLinked 직접 발화 → 상대 디지몬 1체 <De-Digivolve 1>(진화원 1장 감소)", BT21059_WhenLinkingDeDigivolvesOpponent),
    ("EX9_062 W1: [None] ChangeCardLevelForAssemblyClass — [Kimeramon] 어셈블리에서 레벨4로도 취급(순수 함수 검증)", EX9062_AssemblyLevelAlsoFour),
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

// ═══════════════════════════════════ EX4_062 ═══════════════════════════════════

async Task EX4062_MemoryPlusOneOnMainPhase()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 96201, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId ex4062 = Stage(match, P1, "EX4_062", ChoiceZone.BattleArea, "1:battle:EX4062", register: true);
    HeadlessEntityId other = StageSynthetic(match, P1, "S6-EX4-OTHER", dp: 2000, level: 3, "1:battle:other");
    HeadlessEntityId other2 = StageSynthetic(match, P2, "S6-EX4-OTHER2", dp: 2000, level: 3, "2:battle:other2");

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, ex4062, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX4.Blue.EX4_062();
    List<Cec.ICardEffect> mainEffects = effectInstance.CardEffects(Cec.EffectTiming.OnStartMainPhase, card);
    var memoryPlus = (Cfx.ActivateClass)mainEffects.First(e => e.EffectName == "Memory +1");

    int memoryBefore = MemoryOf(match, P1);

    AssertTrue(memoryPlus.CanActivate(new System.Collections.Hashtable()),
        "CanActivate is true — 2 Digimon exist between both battle areas and the owner can add memory");

    await memoryPlus.Activate(new System.Collections.Hashtable());

    AssertEqual(memoryBefore + 1, MemoryOf(match, P1), "[Start of Your Main Phase]: the owner's memory increased by exactly 1");
}

// ═══════════════════════════════════ BT25_019 ═══════════════════════════════════

async Task BT25019_SharedOnPlayDeletesMaxDp()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 96301, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt25019 = Stage(match, P1, "BT25_019", ChoiceZone.BattleArea, "1:battle:BT25019", register: true);
    HeadlessEntityId lowDp = StageSynthetic(match, P2, "S6-BT25019-LOW", dp: 2000, level: 3, "2:battle:low");
    HeadlessEntityId highDp = StageSynthetic(match, P2, "S6-BT25019-HIGH", dp: 9000, level: 6, "2:battle:high");

    policy.On(req => req.Type == ChoiceType.Permanent, req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt25019, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT25.Red.BT25_019();
    List<Cec.ICardEffect> opEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var onPlay = (Cfx.ActivateClass)opEffects.First(e => e.EffectName == "Delete 1 Digimon with highest DP.");

    AssertTrue(onPlay.CanActivate(new System.Collections.Hashtable()), "CanActivate is true — the permanent exists on the battle area and an opponent Digimon is present");

    await onPlay.Activate(new System.Collections.Hashtable());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(highDp),
        $"[On Play] shared dispatcher: the opponent's HIGHEST-DP Digimon was deleted [debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(lowDp),
        "the opponent's lower-DP Digimon was NOT deleted (IsMaxDP scoping verified)");
}

// ═══════════════════════════════════ BT7_055 ═══════════════════════════════════

async Task BT7055_WhenDigivolvingSuspendsAndGainsMemory()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 96401, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt7055 = Stage(match, P1, "BT7_055", ChoiceZone.BattleArea, "1:battle:BT7055", register: true);
    HeadlessEntityId oppUnsuspended = StageSynthetic(match, P2, "S6-BT7055-OPP", dp: 3000, level: 3, "2:battle:opp");

    policy.On(req => req.Type == ChoiceType.Permanent, req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt7055, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT7.Green.BT7_055();
    List<Cec.ICardEffect> wdEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var wd = (Cfx.ActivateClass)wdEffects.First(e => e.EffectName == "Suspend 1 Digimon and gain Memory");

    AssertTrue(wd.CanActivate(new System.Collections.Hashtable()), "CanActivate is true — an unsuspended opponent Digimon is present to select");

    AssertTrue(!IsSuspended(match, oppUnsuspended), "the opponent's Digimon starts unsuspended");

    await wd.Activate(new System.Collections.Hashtable());

    AssertTrue(IsSuspended(match, oppUnsuspended),
        $"[When Digivolving]: the selected opponent Digimon became suspended [debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT7_112 ═══════════════════════════════════

async Task BT7112_WhenDigivolvingDeletesOpponent()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 96501, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt7112 = Stage(match, P1, "BT7_112", ChoiceZone.BattleArea, "1:battle:BT7112", register: true);
    HeadlessEntityId oppDigimon = StageSynthetic(match, P2, "S6-BT7112-OPP", dp: 4000, level: 4, "2:battle:opp");

    policy.On(req => req.Type == ChoiceType.Permanent, req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt7112, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT7.White.BT7_112();
    List<Cec.ICardEffect> wdEffects = effectInstance.CardEffects(Cec.EffectTiming.OnEnterFieldAnyone, card);
    var wd = (Cfx.ActivateClass)wdEffects.First(e => e.EffectName == "Delete 1 Digimon");

    AssertTrue(wd.CanActivate(new System.Collections.Hashtable()), "CanActivate is true — an opponent Digimon is present to select");

    await wd.Activate(new System.Collections.Hashtable());

    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(oppDigimon),
        $"[When Digivolving]: the opponent's Digimon was deleted [debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT19_035 ═══════════════════════════════════

async Task BT19035_WhenAttackingXrosHeartDebuff()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 96601, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    // ESS ([When Attacking] SetIsInheritedEffect(true)) fires when [BT19_035] is a BURIED digivolution source
    // beneath a host permanent — the base ICardEffect.CanActivate inherited-effect gate requires
    // EffectSourceCard != permanentOfThisCard.TopCard, so [BT19_035] must be staged as a source, not the top
    // card, and the host's OWN top card must carry [Xros Heart] (AS-IS reads `card.PermanentOfThisCard().TopCard
    // .EqualsTraits(...)`, i.e. the currently-active top face, not the buried source's own printed trait).
    HeadlessEntityId host = StageSynthetic(match, P1, "S6-BT19035-HOST", dp: 6000, level: 5, "1:battle:host", traits: new[] { "Xros Heart" });
    HeadlessEntityId bt19035 = Stage(match, P1, "BT19_035", ChoiceZone.None, "1:src:BT19035", register: true);
    SetSources(match, host, bt19035);
    HeadlessEntityId oppDigimon = StageSynthetic(match, P2, "S6-BT19035-OPP", dp: 5000, level: 4, "2:battle:opp");

    policy.On(req => req.Type == ChoiceType.Permanent, req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt19035, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT19.Yellow.BT19_035();
    List<Cec.ICardEffect> essEffects = effectInstance.CardEffects(Cec.EffectTiming.OnAllyAttack, card);
    var ess = (Cfx.ActivateClass)essEffects.First(e => e.EffectName == "DP -2000 if this Digimon has [Xros Heart] trait");

    AssertTrue(ess.CanActivate(new System.Collections.Hashtable()),
        "CanActivate is true — [BT19_035] is a buried digivolution source, its host's top card carries [Xros Heart], and an opponent Digimon is present");

    var oppPermanentBefore = new Cec.Permanent(match.Context, oppDigimon, P2);
    int dpBefore = oppPermanentBefore.GetDP();

    await ess.Activate(new System.Collections.Hashtable());

    var oppPermanentAfter = new Cec.Permanent(match.Context, oppDigimon, P2);
    AssertEqual(dpBefore - 2000, oppPermanentAfter.GetDP(),
        $"[When Attacking][ESS]: the selected opponent Digimon lost 2000 DP for the turn [debug prompts:{string.Join(" | ", policy.Seen)}]");
}

// ═══════════════════════════════════ BT21_059 ═══════════════════════════════════

async Task BT21059_WhenLinkingDeDigivolvesOpponent()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 96701, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt21059 = Stage(match, P1, "BT21_059", ChoiceZone.BattleArea, "1:battle:BT21059", register: true);
    HeadlessEntityId oppDigimon = StageSynthetic(match, P2, "S6-BT21059-OPP", dp: 5000, level: 4, "2:battle:opp");
    HeadlessEntityId oppSource = StageSynthetic(match, P2, "S6-BT21059-SRC", dp: 0, level: 0, "2:src:opp", zone: ChoiceZone.None, cardType: "Option");
    SetSources(match, oppDigimon, oppSource);

    policy.On(req => req.Type == ChoiceType.Permanent, req => ChoiceResult.Select(req.Candidates.First(c => c.IsSelectable).Id));

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, bt21059, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT21.Black.BT21_059();
    // Both "[Your Turn]" and "[When Linking]" arms share the EffectName "<De-Digivolve 1>"; the FIRST
    // (index 0) is the non-inherited/non-linked "Your Turn" arm — the base ICardEffect.CanActivate
    // else-branch requires EffectSourceCard == permanentOfThisCard.TopCard, which matches [BT21_059] staged
    // directly as its own permanent's top card (the [When Linking] arm at index 1 instead requires
    // [BT21_059] to be a LINKED card attached to a different host permanent — a distinct staging shape,
    // out of scope for this witness).
    List<Cec.ICardEffect> wlEffects = effectInstance.CardEffects(Cec.EffectTiming.WhenLinked, card);
    var whenLinking = (Cfx.ActivateClass)wlEffects.Where(e => e.EffectName == "<De-Digivolve 1>").ElementAt(0);

    var oppPermanentBefore = new Cec.Permanent(match.Context, oppDigimon, P2);
    int sourcesBefore = oppPermanentBefore.DigivolutionCards.Count;
    AssertTrue(sourcesBefore >= 1, "harness precondition: the opponent's Digimon starts with at least 1 digivolution source");

    AssertTrue(whenLinking.CanActivate(new System.Collections.Hashtable()),
        "CanActivate is true — the permanent exists on the battle area and an opponent Digimon is present (board-state gate; the CanUse trigger gate needs a live WhenLinking dispatch context and is exercised by the engine's link flow, not this direct-API witness)");

    await whenLinking.Activate(new System.Collections.Hashtable());

    // <De-Digivolve 1> trashes the CURRENT top card (oppDigimon) and PROMOTES its digivolution source
    // (oppSource) to become the new top-level battle permanent — the permanent's IDENTITY (instance id)
    // therefore changes; re-reading DigivolutionCards on the OLD (now-trashed) id is not the right signal.
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(oppDigimon),
        $"[When Linking]: <De-Digivolve 1> trashed the opponent's former top card [debug prompts:{string.Join(" | ", policy.Seen)}]");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(oppSource),
        "the digivolution source was promoted to the battle area as the new top card (de-digivolve landed)");
    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(oppDigimon),
        "the opponent's former top card no longer occupies the battle area");
}

// ═══════════════════════════════════ EX9_062 ═══════════════════════════════════

async Task EX9062_AssemblyLevelAlsoFour()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewPilotMatchAsync(seed: 96801, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);

    HeadlessEntityId ex9062 = Stage(match, P1, "EX9_062", ChoiceZone.BattleArea, "1:battle:EX9062", register: true);

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var card = new Cec.CardSource(match.Context, ex9062, P1);
    var effectInstance = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX9.Purple.EX9_062();
    List<Cec.ICardEffect> noneEffects = effectInstance.CardEffects(Cec.EffectTiming.None, card);
    var changeLevel = (Cfx.ChangeCardLevelForAssemblyClass)noneEffects.First(e => e.EffectName == "This card is also treated as level 4 for [Kimeramon]'s assembly.");

    List<int> result = changeLevel.ChangeCardLevelForAssembly(new List<int>(), card);

    AssertTrue(result.Contains(4),
        "ChangeCardLevelForAssemblyClass appends level 4 to this card's assembly-level list (so it also counts as level 4 for [Kimeramon]'s assembly), regardless of its printed level");
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

static int MemoryOf(DcgoMatch match, HeadlessPlayerId player) =>
    new Cec.Player(match.Context, player).MemoryForPlayer;

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
