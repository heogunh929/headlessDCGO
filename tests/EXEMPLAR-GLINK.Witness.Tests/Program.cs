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
using Cfx = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using Script = HeadlessDCGO.Engine.Assets.Scripts.Script;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// EXEMPLAR-GLINK 정본 witness 스위트 — G-Link 배치 2 (설계서 docs/audit/g_link_design_2026-07-18.md §⑤).
// 하네스는 EXEMPLAR-T3A 정본 축약 복사(DcgoMatch.CreatePumpDriven + PolicyChoiceProvider 좌석).
//   W1  EX10_029 팩토리 flip (RD-P6C2-7): 손패 선언 → SelectPermanent → ILinkCard(payCost) → 코스트 2 지불
//       → attach — K:Link 팩토리 70장 게이트의 대표 실단언.
//   W2  WhenWouldLink 창: 창 멤버십(리액터 발화 1회)·타이밍(지불 **전** — 발화 시점 메모리 불변)·root=Hand.
//   W3  링크 코스트 fold: 기저 2 → GrantedReduceLinkCostClass(-2) 생산자 존재 시 0(클램프) → 제거 시 2.
//   W4  WhenLinked 단일 발화 (설계 리스크 1 부정 단언): 전체 ILinkCard 흐름에서 리액터 발화 == 1 (이중발화 없음).
//   W5  필드→링크 전환 (설계 리스크 2 부정 단언): IPlacePermanentToLinkCards — 링크된 카드는 트래시에 없음
//       (이중삭제 없음), 배틀에어리어 이탈, 진화원만 트래시(DiscardEvoRoots), LinkedCards 단일 등재.
// 픽스처 관례: 실카드 EX10_029(트랜치 포팅분, AddSelfLinkConditionStaticEffect cost 2 / Appmon 호스트) +
// 합성 Appmon 호스트(StageSynthetic "traits" 키 — BT22_035/C2-Witness 관례).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("W1 EX10_029 팩토리 flip: [Link] 선언 등재 + Activate → 호스트 선택 → 코스트 2 지불 → attach (RD-P6C2-7 실단언)", W1_FactoryLinkFlip),
    ("W2 WhenWouldLink 창: 리액터 1회 발화 + 지불-전 타이밍(발화 시점 메모리 불변) + root=Hand", W2_WhenWouldLinkWindow),
    ("W3 링크 코스트 fold: GetChangedLinkCost 기저 2 → ChangeLinkCost 생산자(-2) 존재 시 0 → 제거 시 2", W3_LinkCostFold),
    ("W4 WhenLinked 단일 발화(리스크 1 부정 단언): 전체 ILinkCard 흐름 리액터 발화 == 1", W4_WhenLinkedSingleFire),
    ("W5 필드→링크 전환(리스크 2 부정 단언): 이중삭제 없음 — 링크카드 비-트래시·배틀 이탈·진화원만 트래시", W5_FieldToLinkNoDoubleDelete),
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

// ═══════════════════════════════════ W1: factory flip ═══════════════════════════════════

async Task W1_FactoryLinkFlip()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 7101, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId warpmon = Stage(match, P1, "EX10_029", ChoiceZone.Hand, "1:hand:Warpmon", register: true);
    HeadlessEntityId host = StageSynthetic(match, P1, "GLINK-HOST", dp: 5000, level: 4, "1:battle:host", traits: new[] { "Appmon" });

    // 선언 등재: LinkEffect 팩토리가 OnDeclaration에 "Link (Cost: 2)"를 등재(진입 게이트: 손패 + 매칭 호스트).
    Cec.ICardEffect? link = EffectNamed(match, warpmon, Cec.EffectTiming.OnDeclaration, "Link (Cost: 2)");
    AssertTrue(link is not null, "LinkEffect ActivateClass registered under OnDeclaration (hand + matching Appmon host)");
    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        AssertTrue(link!.CanUse(new Hashtable()), "CanUseCondition TRUE (on hand + matching host exists)");
    }

    int memBefore = MemoryFor(match, P1);

    policy.On(
        req => req.Type == ChoiceType.Permanent && req.Message.Contains("link", StringComparison.OrdinalIgnoreCase),
        req => ChoiceResult.Select(host));

    using (AmbientMatchContext.Scope _a = AmbientMatchContext.Enter(match.Context))
    {
        await ((Cec.ActivateICardEffect)link!).Activate(new Hashtable());
    }

    // FLIP RD-P6C2-7: 선언 → 선택 → 지불 → attach 실착지.
    AssertTrue(LinkedCardsOf(match, host).Contains(warpmon), "the link card was attached to the host's LinkedCards");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(warpmon), "the link card left the hand");
    AssertEqual(2, memBefore - MemoryFor(match, P1), "link cost 2 was paid via AddMemory (payCost: true, no reduction)");
}

// ═══════════════════════════════════ W2: WhenWouldLink window ═══════════════════════════════════

async Task W2_WhenWouldLinkWindow()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 7102, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId warpmon = Stage(match, P1, "EX10_029", ChoiceZone.Hand, "1:hand:Warpmon", register: true);
    HeadlessEntityId host = StageSynthetic(match, P1, "GLINK-HOST", dp: 5000, level: 4, "1:battle:host", traits: new[] { "Appmon" });

    int memBefore = MemoryFor(match, P1);

    // WhenWouldLink 리액터: 호스트 permanent의 Until-리스트에 타이밍-게이트 등록(EX10_029 SuccessProcess 관례).
    int fired = 0;
    int memoryAtFire = int.MinValue;
    object? rootAtFire = null;
    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        var hostPerm = new Cec.Permanent(match.Context, host, P1);
        var reactor = new Cfx.ActivateClass();
        reactor.SetUpICardEffect("GLINK-WWL-reactor",
            h => Cec.CardEffectCommons.CanTriggerWhenWouldLink(h, null!, null!), hostPerm.TopCard);
        reactor.SetUpActivateClass(null!, ReactorCoroutine, -1, false, "GLINK WhenWouldLink reactor");
        hostPerm.UntilOpponentTurnEndEffects.Add(t => t == Cec.EffectTiming.WhenWouldLink ? reactor : null!);

        Task ReactorCoroutine(Hashtable h)
        {
            fired++;
            memoryAtFire = MemoryFor(match, P1);
            rootAtFire = h["Root"];
            return Task.CompletedTask;
        }
    }

    policy.On(
        req => req.Type == ChoiceType.Permanent && req.Message.Contains("link", StringComparison.OrdinalIgnoreCase),
        req => ChoiceResult.Select(host));

    Cec.ICardEffect? link = EffectNamed(match, warpmon, Cec.EffectTiming.OnDeclaration, "Link (Cost: 2)");
    AssertTrue(link is not null, "LinkEffect registered");
    using (AmbientMatchContext.Scope _a = AmbientMatchContext.Enter(match.Context))
    {
        await ((Cec.ActivateICardEffect)link!).Activate(new Hashtable());
    }

    AssertEqual(1, fired, "the WhenWouldLink window fired the reactor exactly ONCE (window membership)");
    AssertEqual(memBefore, memoryAtFire, "the window fired BEFORE the cost payment (AS-IS pre-payment order)");
    AssertTrue(rootAtFire is Script.SelectCardEffect.Root.Hand, $"WouldLinkHashtable Root == Hand (got {rootAtFire})");
    AssertEqual(2, memBefore - MemoryFor(match, P1), "after the window, the cost 2 payment landed");
    AssertTrue(LinkedCardsOf(match, host).Contains(warpmon), "the link resolved after the window");
}

// ═══════════════════════════════════ W3: cost fold ═══════════════════════════════════

async Task W3_LinkCostFold()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 7103, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    HeadlessEntityId warpmon = Stage(match, P1, "EX10_029", ChoiceZone.Hand, "1:hand:Warpmon", register: true);
    HeadlessEntityId host = StageSynthetic(match, P1, "GLINK-HOST", dp: 5000, level: 4, "1:battle:host", traits: new[] { "Appmon" });

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    var warpmonCard = new Cec.CardSource(match.Context, warpmon, P1);
    var hostPerm = new Cec.Permanent(match.Context, host, P1);

    // 기저: AddSelfLinkConditionStaticEffect(linkCost: 2) → 생산자 부재 시 fold는 기저 그대로.
    AssertEqual(2, warpmonCard.GetChangedLinkCost(hostPerm, Script.SelectCardEffect.Root.Hand),
        "base link cost 2 with NO IChangeLinkCostEffect producer");

    // 생산자 존재: 플레이어-소유 UntilCalculateFixedCostEffect grant(BT25_089 [Main] 기제 — players 원천 fold).
    Cec.ICardEffect GetCardEffect(Cec.EffectTiming t) =>
        (t == Cec.EffectTiming.None
            ? Cec.CardEffectFactory.GrantedReduceLinkCostClass(
                card: warpmonCard, reducedCost: 2,
                cardSourceCondition: _ => true, permanentCondition: _ => true, rootCondition: _ => true)
            : null)!;

    var owner = new Cec.Player(match.Context, P1);
    owner.UntilCalculateFixedCostEffect.Add(GetCardEffect);
    AssertEqual(0, warpmonCard.GetChangedLinkCost(hostPerm, Script.SelectCardEffect.Root.Hand),
        "with a -2 ChangeLinkCost producer the folded cost is 0 (2 - 2, Math.Max(0, ...) clamp)");

    owner.UntilCalculateFixedCostEffect.Remove(GetCardEffect);
    AssertEqual(2, warpmonCard.GetChangedLinkCost(hostPerm, Script.SelectCardEffect.Root.Hand),
        "removing the producer restores the base cost 2");
}

// ═══════════════════════════════════ W4: WhenLinked single fire (risk 1) ═══════════════════════════════════

async Task W4_WhenLinkedSingleFire()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewExemplarMatchAsync(seed: 7104, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // WhenLinked 몸통이 없는 합성 Appmon 링크카드(리액터 발화만 계수 — EX10_029 [When Linking] 프롬프트 배제).
    HeadlessEntityId linkCard = StageSynthetic(match, P1, "GLINK-CARD", dp: 3000, level: 4, "1:hand:linkcard",
        traits: new[] { "Appmon" }, zone: ChoiceZone.Hand);
    HeadlessEntityId host = StageSynthetic(match, P1, "GLINK-HOST", dp: 5000, level: 4, "1:battle:host", traits: new[] { "Appmon" });

    int fired = 0;
    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        var hostPerm = new Cec.Permanent(match.Context, host, P1);
        var reactor = new Cfx.ActivateClass();
        reactor.SetUpICardEffect("GLINK-WL-counter", _ => true, hostPerm.TopCard);
        reactor.SetUpActivateClass(null!, _ => { fired++; return Task.CompletedTask; }, -1, false, "GLINK WhenLinked counter");
        hostPerm.UntilOpponentTurnEndEffects.Add(t => t == Cec.EffectTiming.WhenLinked ? reactor : null!);

        // 전체 ILinkCard 흐름(직접-호출 8장의 AS-IS 경로): WhenWouldLink 창 → 지불(조건 미선언 → cost 0) → AddLinkCard.
        var linkCardSource = new Cec.CardSource(match.Context, linkCard, P1);
        var cause = new Cfx.ActivateClass();
        cause.SetUpICardEffect("GLINK-cause", _ => true, linkCardSource);
        var iLinkCard = new Script.ILinkCard(true, linkCardSource, hostPerm, cause);
        await iLinkCard.LinkCard();
        AssertTrue(iLinkCard.WasLinked, "ILinkCard.WasLinked TRUE after placement");

        // WhenLinked는 game-event 큐 경유(LinkHelpers 단일 emit) — 안정점까지 드레인 후 계수.
        await new GameFlowProcessor().RunToStableAsync(match.Context);
    }

    AssertTrue(LinkedCardsOf(match, host).Contains(linkCard), "the card is attached to the host");
    AssertEqual(1, fired, "RISK-1 NEGATIVE ASSERTION: WhenLinked fired exactly ONCE (no double emit from the ILinkCard flow)");
}

// ═══════════════════════════════════ W5: field→link, no double delete (risk 2) ═══════════════════════════════════

async Task W5_FieldToLinkNoDoubleDelete()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewExemplarMatchAsync(seed: 7105, MonoDecks("BT1_028", "BT1_028"));
    await ReachMainWaitAsync(match);
    // EX10_029를 배틀에어리어 디지몬으로(팩토리 게이트: IsExistOnBattleAreaDigimon && !IsLinked) + 진화원 1장.
    HeadlessEntityId warpmon = Stage(match, P1, "EX10_029", ChoiceZone.BattleArea, "1:battle:Warpmon", register: true);
    HeadlessEntityId evoRoot = StageSynthetic(match, P1, "GLINK-ROOT", dp: 2000, level: 3, "1:root:under",
        zone: ChoiceZone.None);
    SetSources(match, warpmon, evoRoot);
    HeadlessEntityId host = StageSynthetic(match, P1, "GLINK-HOST", dp: 5000, level: 4, "1:battle:host", traits: new[] { "Appmon" });

    int battleBefore = ZoneCards(match, P1, ChoiceZone.BattleArea).Count;
    int memBefore = MemoryFor(match, P1);

    policy.On(
        req => req.Type == ChoiceType.Permanent && req.Message.Contains("link", StringComparison.OrdinalIgnoreCase),
        req => ChoiceResult.Select(host));

    Cec.ICardEffect? link = EffectNamed(match, warpmon, Cec.EffectTiming.OnDeclaration, "Link (Cost: 2)");
    AssertTrue(link is not null, "LinkEffect registered for the battle-area declarer");
    using (AmbientMatchContext.Scope _a = AmbientMatchContext.Enter(match.Context))
    {
        await ((Cec.ActivateICardEffect)link!).Activate(new Hashtable());
    }

    // root == None → IPlacePermanentToLinkCards: 필드 permanent가 링크 카드로 전환.
    AssertTrue(LinkedCardsOf(match, host).Contains(warpmon), "the field permanent's top card became the host's link card");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(warpmon), "the link card left the battle area (RemoveField)");
    // RISK-2 NEGATIVE ASSERTIONS: 이중삭제 없음.
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Trash).Contains(warpmon),
        "RISK-2 NEGATIVE ASSERTION: the linked card is NOT in the trash (field->link is a conversion, not a deletion)");
    AssertEqual(1, LinkedCardsOf(match, host).Count(id => id == warpmon),
        "RISK-2 NEGATIVE ASSERTION: the link card is attached exactly once");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.Trash).Contains(evoRoot),
        "the digivolution source was trashed by DiscardEvoRoots (exactly the AS-IS under-stack cleanup)");
    AssertEqual(1, ZoneCards(match, P1, ChoiceZone.Trash).Count(id => id == evoRoot),
        "the digivolution source is in the trash exactly once (no double DiscardEvoRoots)");
    AssertEqual(battleBefore - 1, ZoneCards(match, P1, ChoiceZone.BattleArea).Count,
        "the battle area lost exactly ONE permanent (the converted declarer; the host survived)");
    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(host), "the host permanent survived the conversion");
    AssertEqual(2, memBefore - MemoryFor(match, P1), "the battle-area declaration still paid the link cost 2 (root None)");
}

// ═══════════════════════════════════ fixtures ═══════════════════════════════════

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

HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
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

// ═══════════════════════════════════ queries ═══════════════════════════════════

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

static HeadlessPlayerId OwnerOf(DcgoMatch match, HeadlessEntityId id) =>
    match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
        ? rec.OwnerId
        : new HeadlessPlayerId(1);

static IReadOnlyList<HeadlessEntityId> LinkedCardsOf(DcgoMatch match, HeadlessEntityId hostId) =>
    match.Context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? rec) && rec is not null
        ? LinkHelpers.ReadLinkedCardIds(rec.Metadata)
        : Array.Empty<HeadlessEntityId>();

static int MemoryFor(DcgoMatch match, HeadlessPlayerId player)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.Player(match.Context, player).MemoryForPlayer;
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

// ═══════════════════════════════════ harness (EXEMPLAR-T3A 정본 복사) ═══════════════════════════════════

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
            $"pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
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

    using AmbientMatchContext.Scope _apply = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

/// <summary>에이전트 좌석: 술어-매칭 스크립트 답변 + ScriptedChoiceProvider 폴백 (EXEMPLAR-T3A 정본).</summary>
sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

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

/// <summary>EngineContext.CreateDefault의 1:1 재현 — provider 좌석만 교체 (EXEMPLAR-T3A 정본).</summary>
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
