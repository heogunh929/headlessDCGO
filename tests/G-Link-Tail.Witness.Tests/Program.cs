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
// G-Link-Tail 정본 witness 스위트 — G-Link 마감 트랜치 (docs/audit/g_link_design_2026-07-18.md §⑤ tail).
// 하네스는 EXEMPLAR-GLINK 정본 복사(DcgoMatch.CreatePumpDriven + PolicyChoiceProvider 좌석).
//   T1 BT25_004  WhenWouldLink 창(REQUIRED): 링크-시도 컨텍스트 → 창 CanUse 게이트(Social 발화·비-Social 미발화)
//                → 효과(그랜트) → 코스트 감소가 매칭 소스엔 적용(2→1)·비-매칭 컨트롤엔 미적용(2).
//   T2 BT25_052  [When Linking] linked-effect: 적 1체 suspend (Tap).
//   T3 BT25_056  [When Linking] linked-effect: 적 suspended Digimon 1체 덱 최하단 (PutLibraryBottom).
//   T4 BT25_070  [Your Turn] WhenLinked: 적 ≤4 코스트 Digimon 1체 delete (Destroy → Trash).
//   T5 BT25_072  [When Linking] linked-effect: 적 2체 can't-unsuspend (GainCanNotUnsuspend).
//   T6 ST22_12   [When Linking] linked-effect: 적 ≤5000 DP Digimon 1체 덱 최하단 (PutLibraryBottom).
//   T7 P_234     [On Play] 등재 + [Your Turn] OnLinkCardDiscarded 등재 + 구동 시 self-suspend(link 진입 코스트).
//   T8 BT25_052  attach: 실 ported link 카드(link cost 2 / Appmon 조건)를 합성 Appmon 호스트에 ILinkCard로 부착.
//   T9 P2-② witness: GiveEffectToPlayer(AddEffectToPlayer) 로 부여한 player-scope IChangeLinkCostEffect 가
//                GetChangedLinkCost/FoldLinkCost 에 fold 되는지(P6A latent 종료 실증).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("T1 BT25_004 WhenWouldLink 창: Social 소스 CanUse ON·비-Social OFF; 그랜트 후 fold Social 2→1, 비-Social 2 유지", T1_BT25004_WhenWouldLink),
    ("T2 BT25_052 [When Linking]: 적 Digimon 1체 suspend (Tap)", T2_BT25052_SuspendEnemy),
    ("T3 BT25_056 [When Linking]: 적 suspended Digimon 1체 덱 최하단", T3_BT25056_BottomDeckSuspended),
    ("T4 BT25_070 [Your Turn] WhenLinked: 적 ≤4 코스트 Digimon delete", T4_BT25070_DeleteLowCost),
    ("T5 BT25_072 [When Linking]: 적 Digimon can't-unsuspend", T5_BT25072_CanNotUnsuspend),
    ("T6 ST22_12 [When Linking]: 적 ≤5000 DP Digimon 덱 최하단", T6_ST2212_BottomDeckLowDp),
    ("T7 P_234 [On Play]+[Your Turn] 등재 + OnLinkCardDiscarded 구동 시 self-suspend", T7_P234_LinkDiscardSuspend),
    ("T8 BT25_052 attach: 실 link 카드를 합성 Appmon 호스트에 ILinkCard 부착", T8_BT25052_AttachToAppmonHost),
    ("T9 P2-② GiveEffectToPlayer link-cost fold: AddEffectToPlayer 그랜트가 FoldLinkCost 2→0", T9_PlayerScopeLinkCostFold),
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

// ═══════════════════════════════════ T1 BT25_004 WhenWouldLink ═══════════════════════════════════

async Task T1_BT25004_WhenWouldLink()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 8001);
    await ReachMainWaitAsync(match);
    // BT25_004 (Tapmon DigiEgg) grants its WhenWouldLink reduction as an INHERITED effect — it is available only
    // as a digivolution SOURCE under a top Digimon, not as the top card itself. Stage a top Digimon with Tapmon
    // buried under it (the host whose links it cheapens).
    HeadlessEntityId topDigimon = StageSynthetic(match, P1, "TAP-HOST", dp: 4000, level: 4, "1:battle:taphost");
    HeadlessEntityId tapmon = Stage(match, P1, "BT25_004", ChoiceZone.None, "1:root:Tapmon", register: true);
    SetSources(match, topDigimon, tapmon);
    HeadlessEntityId social = StageSynthetic(match, P1, "SRC-SOCIAL", dp: 3000, level: 4, "1:hand:social",
        traits: new[] { "Social" }, zone: ChoiceZone.Hand);
    HeadlessEntityId beast = StageSynthetic(match, P1, "SRC-BEAST", dp: 3000, level: 4, "1:hand:beast",
        traits: new[] { "Beast" }, zone: ChoiceZone.Hand);

    Cec.ICardEffect? reactor = EffectNamed(match, tapmon, Cec.EffectTiming.WhenWouldLink, "May reduce Link cost by 1");
    AssertTrue(reactor is not null, "BT25_004 registers the WhenWouldLink ActivateClass under EffectTiming.WhenWouldLink");

    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        var hostPerm = new Cec.Permanent(match.Context, topDigimon, P1);
        var socialCard = new Cec.CardSource(match.Context, social, P1);
        var beastCard = new Cec.CardSource(match.Context, beast, P1);

        // link-attempt context: a WouldLinkHashtable for {source, target = the host that buries Tapmon, Root.Hand}.
        Hashtable socialWl = Cec.CardEffectCommons.WouldLinkHashtable(socialCard, hostPerm, Script.SelectCardEffect.Root.Hand, null!);
        Hashtable beastWl = Cec.CardEffectCommons.WouldLinkHashtable(beastCard, hostPerm, Script.SelectCardEffect.Root.Hand, null!);

        AssertTrue(reactor!.CanUse(socialWl), "WhenWouldLink CanUse TRUE for a [Social] source linking to Tapmon's host on the owner's turn (inherited effect available as a buried source)");
        AssertTrue(!reactor!.CanUse(beastWl), "NEGATIVE: CanUse FALSE for a non-[Social/Tool/Game] source (CardCondition gate)");

        // window effect: activating the reactor stacks a GrantedReduceLinkCostClass(-1) on the owner.
        await ((Cec.ActivateICardEffect)reactor!).Activate(socialWl);
    }

    // cost fold: the -1 grant applies to a matching [Social] source, NOT to the non-matching control.
    int socialCost = FoldLinkCost(match, social, baseCost: 2, topDigimon, Script.SelectCardEffect.Root.Hand);
    int beastCost = FoldLinkCost(match, beast, baseCost: 2, topDigimon, Script.SelectCardEffect.Root.Hand);
    AssertEqual(1, socialCost, "the WhenWouldLink reduction applies to the [Social] source: base 2 → 1");
    AssertEqual(2, beastCost, "NEGATIVE: the control (non-Social) source links at the unreduced base cost 2 (grant CardCondition gate)");
}

// ═══════════════════════════════════ T2 BT25_052 suspend ═══════════════════════════════════

async Task T2_BT25052_SuspendEnemy()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 8002);
    await ReachMainWaitAsync(match);
    Stage(match, P1, "BT25_052", ChoiceZone.BattleArea, "1:battle:Logimon", register: true);
    HeadlessEntityId enemy = StageSynthetic(match, P2, "ENEMY-1", dp: 4000, level: 4, "2:battle:enemy");

    HeadlessEntityId host = new("1:battle:Logimon");
    Cec.ICardEffect? whenLinking = EffectNamed(match, host, Cec.EffectTiming.WhenLinked, "Suspend 1 enemy Digimon or Tamers");
    AssertTrue(whenLinking is not null, "BT25_052 registers the [When Linking] suspend ActivateClass under WhenLinked");

    AssertTrue(!IsSuspendedMeta(match, enemy), "precondition: the enemy Digimon is unsuspended");
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == enemy),
        req => ChoiceResult.Select(enemy));
    await DriveActivateAsync(match, whenLinking!);
    AssertTrue(IsSuspendedMeta(match, enemy), "[When Linking] suspended (tapped) the opponent's Digimon");
}

// ═══════════════════════════════════ T3 BT25_056 bottom-deck ═══════════════════════════════════

async Task T3_BT25056_BottomDeckSuspended()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 8003);
    await ReachMainWaitAsync(match);
    Stage(match, P1, "BT25_056", ChoiceZone.BattleArea, "1:battle:Bootmon", register: true);
    HeadlessEntityId enemy = StageSynthetic(match, P2, "ENEMY-3", dp: 5000, level: 4, "2:battle:enemy", suspended: true);

    HeadlessEntityId host = new("1:battle:Bootmon");
    Cec.ICardEffect? whenLinking = EffectNamed(match, host, Cec.EffectTiming.WhenLinked, "Bottom deck 1 of enemy suspended Digimon");
    AssertTrue(whenLinking is not null, "BT25_056 registers the [When Linking] bottom-deck ActivateClass under WhenLinked");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == enemy),
        req => ChoiceResult.Select(enemy));
    await DriveActivateAsync(match, whenLinking!);
    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(enemy), "the enemy suspended Digimon left the battle area");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Library).Contains(enemy), "the enemy suspended Digimon was returned to the deck (Library)");
}

// ═══════════════════════════════════ T4 BT25_070 delete ═══════════════════════════════════

async Task T4_BT25070_DeleteLowCost()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 8004);
    await ReachMainWaitAsync(match);
    Stage(match, P1, "BT25_070", ChoiceZone.BattleArea, "1:battle:Logamon", register: true);
    HeadlessEntityId enemy = StageSynthetic(match, P2, "ENEMY-4", dp: 3000, level: 4, "2:battle:enemy", playCost: 3);

    HeadlessEntityId host = new("1:battle:Logamon");
    Cec.ICardEffect? whenLinked = EffectNamed(match, host, Cec.EffectTiming.WhenLinked, "Delete 1 enemy 4 cost or less Digimon");
    AssertTrue(whenLinked is not null, "BT25_070 registers the [Your Turn] delete ActivateClass under WhenLinked");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == enemy),
        req => ChoiceResult.Select(enemy));
    await DriveActivateAsync(match, whenLinked!);
    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(enemy), "the enemy 3-cost Digimon left the battle area");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Trash).Contains(enemy), "the enemy 3-cost Digimon was deleted to the trash");
}

// ═══════════════════════════════════ T5 BT25_072 can't-unsuspend ═══════════════════════════════════

async Task T5_BT25072_CanNotUnsuspend()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 8005);
    await ReachMainWaitAsync(match);
    Stage(match, P1, "BT25_072", ChoiceZone.BattleArea, "1:battle:Shutmon", register: true);
    HeadlessEntityId enemy = StageSynthetic(match, P2, "ENEMY-5", dp: 4000, level: 4, "2:battle:enemy");

    HeadlessEntityId host = new("1:battle:Shutmon");
    Cec.ICardEffect? whenLinking = EffectNamed(match, host, Cec.EffectTiming.WhenLinked, "2 enemy Digimon or Tamers can't unsuspend until their turn ends");
    AssertTrue(whenLinking is not null, "BT25_072 registers the [When Linking] can't-unsuspend ActivateClass under WhenLinked");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == enemy),
        req => ChoiceResult.Select(enemy));
    await DriveActivateAsync(match, whenLinking!);
    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    AssertTrue(!new Cec.Permanent(match.Context, enemy, P2).CanUnsuspend,
        "[When Linking] the opponent's Digimon can no longer unsuspend (ICanNotUnsuspendEffect active)");
}

// ═══════════════════════════════════ T6 ST22_12 bottom-deck ═══════════════════════════════════

async Task T6_ST2212_BottomDeckLowDp()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 8006);
    await ReachMainWaitAsync(match);
    Stage(match, P1, "ST22_12", ChoiceZone.BattleArea, "1:battle:DoGatchamon", register: true);
    HeadlessEntityId enemy = StageSynthetic(match, P2, "ENEMY-6", dp: 4000, level: 4, "2:battle:enemy");

    HeadlessEntityId host = new("1:battle:DoGatchamon");
    Cec.ICardEffect? whenLinking = EffectNamed(match, host, Cec.EffectTiming.WhenLinked, "Return a digimon to bottom of deck");
    AssertTrue(whenLinking is not null, "ST22_12 registers the [When Linking] bottom-deck ActivateClass under WhenLinked");

    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == enemy),
        req => ChoiceResult.Select(enemy));
    await DriveActivateAsync(match, whenLinking!);
    AssertTrue(!ZoneCards(match, P2, ChoiceZone.BattleArea).Contains(enemy), "the enemy ≤5000 DP Digimon left the battle area");
    AssertTrue(ZoneCards(match, P2, ChoiceZone.Library).Contains(enemy), "the enemy ≤5000 DP Digimon was returned to the deck (Library)");
}

// ═══════════════════════════════════ T7 P_234 link-discard suspend ═══════════════════════════════════

async Task T7_P234_LinkDiscardSuspend()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 8007);
    await ReachMainWaitAsync(match);
    HeadlessEntityId yujin = Stage(match, P1, "P_234", ChoiceZone.BattleArea, "1:battle:Yujin", register: true);

    Cec.ICardEffect? onPlay = EffectNamed(match, yujin, Cec.EffectTiming.OnEnterFieldAnyone, "Reveal 4, add 1, bot deck rest");
    AssertTrue(onPlay is not null, "P_234 registers the [On Play] reveal ActivateClass under OnEnterFieldAnyone");

    Cec.ICardEffect? onDiscard = EffectNamed(match, yujin, Cec.EffectTiming.OnLinkCardDiscarded, "By suspending, you may link from hand for -2 cost");
    AssertTrue(onDiscard is not null, "P_234 registers the [Your Turn] link-card-discarded ActivateClass under OnLinkCardDiscarded");

    AssertTrue(!IsSuspendedMeta(match, yujin), "precondition: Yujin Ozora is unsuspended");
    await DriveActivateAsync(match, onDiscard!);
    AssertTrue(IsSuspendedMeta(match, yujin), "driving the OnLinkCardDiscarded effect pays the self-suspend cost (Yujin is suspended)");
}

// ═══════════════════════════════════ T8 BT25_052 attach ═══════════════════════════════════

async Task T8_BT25052_AttachToAppmonHost()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 8008);
    await ReachMainWaitAsync(match);
    // BT25_052 has AddSelfLinkConditionStaticEffect(Appmon, cost 2) — a real link card usable as a link SOURCE.
    HeadlessEntityId logimon = Stage(match, P1, "BT25_052", ChoiceZone.Hand, "1:hand:Logimon", register: true);
    HeadlessEntityId host = StageSynthetic(match, P1, "APPMON-HOST", dp: 5000, level: 4, "1:battle:host", traits: new[] { "Appmon" });

    int memBefore = MemoryFor(match, P1);
    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        var logimonCard = new Cec.CardSource(match.Context, logimon, P1);
        var hostPerm = new Cec.Permanent(match.Context, host, P1);
        AssertTrue(logimonCard.CanLinkToTargetPermanent(hostPerm, false),
            "BT25_052's own AddSelfLinkConditionStaticEffect makes it linkable to an [Appmon] host");

        var cause = new Cfx.ActivateClass();
        cause.SetUpICardEffect("T8-cause", _ => true, logimonCard);
        await new Script.ILinkCard(true, logimonCard, hostPerm, cause).LinkCard();
    }

    AssertTrue(LinkedCardsOf(match, host).Contains(logimon), "BT25_052 was attached to the [Appmon] host's LinkedCards");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(logimon), "the link card left the hand");
    AssertEqual(2, memBefore - MemoryFor(match, P1), "the base link cost 2 was paid (no reduction)");
}

// ═══════════════════════════════════ T9 player-scope link cost fold ═══════════════════════════════════

async Task T9_PlayerScopeLinkCostFold()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 8009);
    await ReachMainWaitAsync(match);
    HeadlessEntityId probe = StageSynthetic(match, P1, "PROBE", dp: 3000, level: 4, "1:hand:probe",
        traits: new[] { "Social" }, zone: ChoiceZone.Hand);
    HeadlessEntityId host = StageSynthetic(match, P1, "HOST", dp: 5000, level: 4, "1:battle:host", traits: new[] { "Appmon" });

    // baseline (no producer).
    AssertEqual(2, FoldLinkCost(match, probe, 2, host, Script.SelectCardEffect.Root.Hand),
        "baseline: FoldLinkCost with no IChangeLinkCostEffect producer leaves base 2");

    using (AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context))
    {
        var probeCard = new Cec.CardSource(match.Context, probe, P1);
        // P2-② : grant a player-scope IChangeLinkCostEffect via the GiveEffectToPlayer WRITE path
        // (AddEffectToPlayer → a Player duration bucket), NOT via UntilCalculateFixedCostEffect directly.
        Cec.ICardEffect grant = Cec.CardEffectFactory.GrantedReduceLinkCostClass(
            card: probeCard, reducedCost: 2,
            cardSourceCondition: _ => true, permanentCondition: _ => true, rootCondition: _ => true);
        Cec.CardEffectCommons.AddEffectToPlayer(
            EffectDuration.UntilOwnerTurnEnd, probeCard, grant, Cec.EffectTiming.None);
    }

    AssertEqual(0, FoldLinkCost(match, probe, 2, host, Script.SelectCardEffect.Root.Hand),
        "P2-② LIVE: a GiveEffectToPlayer (AddEffectToPlayer) player-scope ChangeLinkCost(-2) grant folds via " +
        "Player.EffectList (region 2) — base 2 → 0 (clamp). The P6A 'player-EffectList latent' caveat is retired.");
}

// ═══════════════════════════════════ queries ═══════════════════════════════════

int FoldLinkCost(DcgoMatch match, HeadlessEntityId cardId, int baseCost, HeadlessEntityId targetPermanentId, Script.SelectCardEffect.Root root)
    => Cec.NewModelContinuousScan.FoldLinkCost(match.Context, cardId, baseCost, targetPermanentId, root);

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

async Task DriveActivateAsync(DcgoMatch match, Cec.ICardEffect effect)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await ((Cec.ActivateICardEffect)effect).Activate(new Hashtable());
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

static bool IsSuspendedMeta(DcgoMatch match, HeadlessEntityId cardId) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null
    && record.Metadata.TryGetValue("isSuspended", out object? raw) && raw is true;

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
    string[]? traits = null, int? playCost = null, bool suspended = false)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}:{instanceId}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
    }

    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name ?? number, meta, CardType: cardType, PlayCost: playCost));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level, ["isSuspended"] = suspended }));
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

// ═══════════════════════════════════ harness (EXEMPLAR-GLINK 정본 복사) ═══════════════════════════════════

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    PlayerDeckSetup[] decks = new[]
    {
        new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
        new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
    };
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

/// <summary>에이전트 좌석: 술어-매칭 스크립트 답변 + ScriptedChoiceProvider 폴백 (EXEMPLAR-GLINK 정본).</summary>
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

/// <summary>EngineContext.CreateDefault의 1:1 재현 — provider 좌석만 교체 (EXEMPLAR-GLINK 정본).</summary>
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
