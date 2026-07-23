using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// RD-BATCH7B witness — 융합 소재-선택 클러스터 옵션 A 착지 (BT18_065 Snatchmon).
// 아키텍처 가설 검증: 펌프 매치에서 DigiXros 카드를 손패에서 빈-프레임으로 플레이 → 미러 인터랙티브
// SelectDigiXrosClass.Select 세션이 표면 → 트래시 소재(AddMaxTrashCountDigiXros 경유) 포함 소재가 스택.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

const string VEMMON = "BT18_060";   // real Vemmon (level 3 Digimon) — name "Vemmon" => CardNames_DigiXros ∋ "Vemmon"

var tests = new (string Name, Func<Task> Body)[]
{
    ("W1 enumeration(cost-projection): 펌프 Main에서 BT18_065가 PlayCard 레인으로 제안됨(HasDigiXros availability=0 투영)", W1_EnumerationOffers),
    ("W2 registration: 5 arm이 각 timing에 실착지 (WhenDigivolving/OnEndTurn/None×2/OnDigivolutionCardReturnToDeckBottom)", W2_ArmsRegistered),
    ("W3 full pump DigiXros play: BT18_065 빈-프레임 플레이 → SelectDigiXros ModeChoice 표면 → 소재(트래시 포함) 스택 → BT18_065 필드 진입", W3_PumpDigiXrosPlay),
    ("W4 negative: Vemmon 소재 0장 → DigiXros 모드 소재 미충족(빈 보드 Select가 0장 선택, 소비 미성립)", W4_NoMaterialNegative),
    ("W5 arm3 trash-cap gate: 필드에 non-Vemmon Digimon 존재 시 AddMaxTrashCountDigiXros CanUse=false (트래시 소재 불허)", W5_TrashCapGate),
};

int failed = 0;
foreach ((string name, Func<Task> body) in tests)
{
    try { await body(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is string st) { Console.WriteLine(string.Join('\n', st.Split('\n').Take(6))); }
    }
}
Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ W1 enumeration ═══════════════════════════════════

async Task W1_EnumerationOffers()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 7101);
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);
    HeadlessEntityId bt = Stage(match, P1, "BT18_065", ChoiceZone.Hand, "1:hand:bt");

    LegalAction? play = FindLane(match, P1, HeadlessActionTypes.PlayCard, bt);
    AssertTrue(play is not null,
        "펌프 Main이 BT18_065(HasDigiXros)를 PlayCard 레인으로 제안 — availability cost-projection(1180-1201 DigiXros 감산=0)로 payable");
    int projected = play!.Parameters.TryGetValue(HeadlessActionParameterKeys.MemoryCost, out object? mc) && mc is int i ? i : -1;
    AssertTrue(projected == 0,
        $"cost-projection = 0 (HasDigiXros && CanReduceCost => checkAvailability 0, AS-IS-등가) [got {projected}]");
}

// ═══════════════════════════════════ W2 registration ═══════════════════════════════════

async Task W2_ArmsRegistered()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 7201);
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT18_065", ChoiceZone.Hand, "1:hand:bt");

    List<string> none = EffectTypes(match, bt, P1, Cec.EffectTiming.None);
    AssertTrue(none.Contains("AddMaxTrashCountDigiXrosClass"), $"None: AddMaxTrashCountDigiXros holder [got {string.Join(",", none)}]");
    AssertTrue(none.Contains("AddDigiXrosConditionClass"), "None: DigiXros condition holder (Vemmon×4, min 1)");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.WhenDigivolving).Contains("ActivateClass"),
        "WhenDigivolving: [When Digivolving] trash-Vemmon-to-sources (mirror dedicated key)");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnEndTurn).Contains("ActivateClass"),
        "OnEndTurn: [End of Your Turn] re-digivolve if >=4 sources");
    AssertTrue(EffectTypes(match, bt, P1, Cec.EffectTiming.OnDigivolutionCardReturnToDeckBottom).Contains("ActivateClass"),
        "OnDigivolutionCardReturnToDeckBottom: ESS unsuspend + <Blocker>");
}

// ═══════════════════════════════════ W3 full pump play ═══════════════════════════════════

async Task W3_PumpDigiXrosPlay()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 7301);
    await ReachMainWaitAsync(match);
    match.Context.MemoryController.Set(10);

    HeadlessEntityId bt = Stage(match, P1, "BT18_065", ChoiceZone.Hand, "1:hand:bt", register: true);
    // materials: 2 Vemmon in HAND + 1 Vemmon in TRASH (the trash slot exercises AddMaxTrashCountDigiXros=4).
    HeadlessEntityId hMat1 = Stage(match, P1, VEMMON, ChoiceZone.Hand, "1:hand:v1", register: true);
    HeadlessEntityId hMat2 = Stage(match, P1, VEMMON, ChoiceZone.Hand, "1:hand:v2", register: true);
    HeadlessEntityId tMat = Stage(match, P1, VEMMON, ChoiceZone.Trash, "1:trash:v3", register: true);

    // policy: for the DigiXros area ModeChoice pick HAND (digiXros#0) then TRASH (digiXros#2); for material
    // Card selection pick a Vemmon; when no more desired, End Selection (digiXros#4).
    int picks = 0;
    policy.On(req => req.Type == ChoiceType.ModeChoice && req.Candidates.Any(c => c.Id.Value.StartsWith("digiXros#")),
        req =>
        {
            // pick hand while a hand Vemmon remains and we want <=2, else trash for one, else end.
            string? Want(string tag) => req.SelectableCandidates.FirstOrDefault(c => c.Id.Value == tag)?.Id.Value;
            if (picks < 2 && Want("digiXros#0") is string h) { return ChoiceResult.Select(new HeadlessEntityId(h)); }
            if (picks == 2 && Want("digiXros#2") is string t) { return ChoiceResult.Select(new HeadlessEntityId(t)); }
            string end = req.SelectableCandidates.First(c => c.Id.Value == "digiXros#4").Id.Value;
            return ChoiceResult.Select(new HeadlessEntityId(end));
        }, oneShot: false);
    policy.On(req => (req.Type == ChoiceType.Card || req.Type == ChoiceType.HandCard)
            && req.SelectableCandidates.Any(c => IsVemmon(match, c.Id)),
        req => { picks++; return ChoiceResult.Select(req.SelectableCandidates.First(c => IsVemmon(match, c.Id)).Id); }, oneShot: false);

    LegalAction play = RequireLane(match, P1, HeadlessActionTypes.PlayCard, bt, "BT18_065 empty-frame DigiXros play");
    await ApplyAsync(match, play);
    await DriveUntilAsync(match, m => ZoneCards(m, P1, ChoiceZone.BattleArea).Contains(bt) || m.IsTerminal());

    AssertTrue(ZoneCards(match, P1, ChoiceZone.BattleArea).Contains(bt),
        $"BT18_065 이 필드로 진입 (빈-프레임 DigiXros 플레이 완주) [prompts:{string.Join(" | ", policy.Seen)}]");

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var perm = new Cec.Permanent(match.Context, bt, P1);
    var underIds = perm.DigivolutionCards.Select(c => c.InstanceId).ToList();
    AssertTrue(underIds.Count >= 1,
        $"소재가 BT18_065 아래로 스택됨 (DigiXros 소재 tuck) [under={underIds.Count}: {string.Join(",", underIds.Select(x => x.Value))}]");
    // the trash material actually fused (AddMaxTrashCountDigiXros arm exercised) OR at least a hand material fused.
    bool trashFused = underIds.Contains(tMat);
    bool handFused = underIds.Contains(hMat1) || underIds.Contains(hMat2);
    Console.WriteLine($"     [W3 stack] under={underIds.Count} hand-fused={handFused} trash-fused={trashFused} memory={match.Context.MemoryController.Current.Current} prompts={policy.Seen.Count}");
    AssertTrue(handFused || trashFused, "선택한 Vemmon 소재(손패/트래시)가 진화원으로 편입");
    AssertTrue(trashFused, "트래시 Vemmon 소재가 AddMaxTrashCountDigiXros 경유로 진화원 편입(트래시-cap 팔 실발화)");
    AssertTrue(!ZoneCards(match, P1, ChoiceZone.Hand).Contains(bt), "BT18_065 은 손패를 떠남");

    // ═══ (P2-9 상환) DigiXros 지불-시점 검증: the translated-recipe MemoryCost:0 is INERT for a live DigiXros play ═══
    // AS-IS DigiXros charge (CardSource.cs:664-701 GetPayingCostWithBaseCost / :695): baseCost − selectedMaterial
    // Count × digiXrosCondition.reduceCostPerCard. The mirror ports this 1:1 (CardSource.cs:1382). The pump lane
    // DELIBERATELY OMITS SpecialPlayAction (HeadlessLegalActionDispatcher :77-88, Option A / batch 7b) and routes
    // BT18_065 through PlayCardAction → PlayCardClass.PlayCard → SelectDigiXros → GetPayingCostWithBaseCost, so
    // SpecialPlayAction.EnsureSpecialPlayRecipe's hardcoded `MemoryCost: 0` (SpecialPlayAction.cs:185) is NEVER the
    // amount charged for a live DigiXros — it is inert as a PAY amount (its only live role is the AS-IS-faithful
    // availability gate CanPay(0), mirroring AS-IS's `if (checkAvailability) return 0`). PROOF: BT18_065 playCost 6,
    // reduceCostPerCard 1, 3 materials fused → the live charge is 6 − 3×1 = 3 (memory 10→7). Were the recipe's 0 the
    // governing charge, the delta would be 0 (memory would stay 10). It is 3 — the recipe field is not consumed here.
    const int Bt18_065PlayCost = 6;        // cards.json BT18_065.playCost
    const int ReduceCostPerCard = 1;       // BT18_065.cs DigiXrosCondition(elements, null, 1)
    int materialsFused = underIds.Count;   // = SelectDigiXros.selectedDigicrossCards.Count (the tucked materials)
    int expectedCharge = Bt18_065PlayCost - materialsFused * ReduceCostPerCard;   // AS-IS baseCost − count × reduce
    AssertTrue(match.Context.MemoryController.Current.Current == 10 - expectedCharge,
        $"DigiXros live-pay = AS-IS baseCost({Bt18_065PlayCost}) − {materialsFused}×reduceCostPerCard({ReduceCostPerCard}) = {expectedCharge} (memory 10→{10 - expectedCharge}); the translated recipe's MemoryCost:0 is INERT for the live play [got memory {match.Context.MemoryController.Current.Current}]");
}

// ═══════════════════════════════════ W4 negative ═══════════════════════════════════

async Task W4_NoMaterialNegative()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 7401);
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT18_065", ChoiceZone.Hand, "1:hand:bt", register: true);
    Cec.CardSource cs = new(match.Context, bt, P1);

    // bare board: no Vemmon anywhere. SelectDigiXros runs to completion, selecting nothing (소비 미성립).
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    var select = new HeadlessDCGO.Engine.Assets.Scripts.Script.SelectDigiXrosClass();
    await select.Select(cs);
    AssertTrue(select.selectedDigicrossCards.Count == 0,
        "Vemmon 소재 부재 → SelectDigiXros가 STOP 없이 완주하되 0장 선택 (DigiXros 미성립, 일반 플레이만 가능)");
}

// ═══════════════════════════════════ W5 arm3 gate ═══════════════════════════════════

async Task W5_TrashCapGate()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 7501);
    await ReachMainWaitAsync(match);
    HeadlessEntityId bt = Stage(match, P1, "BT18_065", ChoiceZone.Hand, "1:hand:bt", register: true);

    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    Cec.CardSource cs = new(match.Context, bt, P1);
    var maxTrash = cs.EffectList(Cec.EffectTiming.None)
        .OfType<HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.AddMaxTrashCountDigiXrosClass>().FirstOrDefault();
    AssertTrue(maxTrash is not null, "AddMaxTrashCountDigiXros holder present");
    AssertTrue(maxTrash!.CanUse(new System.Collections.Hashtable()),
        "빈 보드(non-Vemmon Digimon 0체) → CanUse true (트래시 소재 허용, self=4)");

    // now place a non-Vemmon Digimon on the field: the gate flips false.
    StageSynthetic(match, P1, "NONVEM", dp: 3000, level: 3, "1:battle:nonvem", name: "NonVemmon");
    Cec.CardSource cs2 = new(match.Context, bt, P1);
    var maxTrash2 = cs2.EffectList(Cec.EffectTiming.None)
        .OfType<HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects.AddMaxTrashCountDigiXrosClass>().First();
    AssertTrue(!maxTrash2.CanUse(new System.Collections.Hashtable()),
        "필드에 non-Vemmon Digimon 존재 → MatchConditionPermanentCount != 0 → CanUse false (트래시 소재 불허)");
}

// ═══════════════════════════════════ helpers (EXEMPLAR-T3B 판례) ═══════════════════════════════════

bool IsVemmon(DcgoMatch match, HeadlessEntityId id)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.CardSource(match.Context, id, P1).CardNames.Contains("Vemmon");
}

PlayerDeckSetup[] MonoDecks(string p1Number, string p2Number) => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId(p1Number), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId(p2Number), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        MonoDecks("BT1_028", "BT1_028"), firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
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
    for (int i = 0; i < 160 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.BreedingDecision
                || match.Context.ChoiceController.PendingRequest!.Type == ChoiceType.Mulligan;
            await ResolvePendingAsync(match, skip: decline);
        }
        else { await StepOnceAsync(match); }
    }

    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"drive did not reach expected — phase:{t.Phase}/{t.StepCursor} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} terminal:{match.IsTerminal()}");
    }
}

async Task ResolvePendingAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context))
    {
        var lanes = match.GetLegalActions(chooser);
        action = lanes.FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? lanes.FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) { throw new InvalidOperationException("no ResolveChoice lane for the pending request"); }
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

static LegalAction? FindLane(DcgoMatch match, HeadlessPlayerId player, string actionType, HeadlessEntityId cardId) =>
    Legal(match, player).FirstOrDefault(a => a.ActionType == actionType && ActionCardIds(a).Contains(cardId));

static LegalAction RequireLane(DcgoMatch match, HeadlessPlayerId player, string actionType, HeadlessEntityId cardId, string why) =>
    FindLane(match, player, actionType, cardId)
        ?? throw new InvalidOperationException(
            $"expected {actionType} lane for {cardId.Value} — {why}. listed: " +
            string.Join(", ", Legal(match, player).Select(a => $"{a.ActionType}({string.Join('/', ActionCardIds(a).Select(i => i.Value))})")));

static IEnumerable<HeadlessEntityId> ActionCardIds(LegalAction action)
{
    foreach (string key in new[] { HeadlessActionParameterKeys.CardId, HeadlessActionParameterKeys.AttackerId, HeadlessActionParameterKeys.TargetCardId })
    {
        if (action.Parameters.TryGetValue(key, out object? raw) && raw is HeadlessEntityId id) { yield return id; }
    }
}

List<string> EffectTypes(DcgoMatch match, HeadlessEntityId instanceId, HeadlessPlayerId owner, Cec.EffectTiming timing)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.CardSource(match.Context, instanceId, owner).EffectList(timing).Select(e => e.GetType().Name).ToList();
}

static IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone) =>
    match.Context.ZoneMover is IZoneStateReader zones ? zones.GetCards(player, zone) : Array.Empty<HeadlessEntityId>();

static void AssertTrue(bool condition, string message)
{
    if (!condition) { throw new InvalidOperationException($"Assertion failed: {message}"); }
}

HeadlessEntityId Stage(DcgoMatch match, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone, string instanceId, bool register = false)
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
    if (zone != ChoiceZone.None) { ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult(); }
    if (register) { HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner); }
    return id;
}

HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea, string[]? traits = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 }) { meta["traits"] = traits; }
    ((CardDatabase)ctx.CardRepository).Upsert(new CardRecord(defId, number, name ?? number, meta, CardType: cardType));
    var id = new HeadlessEntityId(instanceId);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level, ["isSuspended"] = false }));
    if (zone != ChoiceZone.None) { ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone)).GetAwaiter().GetResult(); }
    HeadlessDCGO.Engine.Headless.Runtime.CardEffectRegistrar.RegisterCard(ctx, id, owner);
    return id;
}

// ═══════════════════════════════ providers/context (EXEMPLAR-T3B 판례) ═══════════════════════════════

sealed class PolicyChoiceProvider : IChoiceProvider
{
    private readonly List<(Func<ChoiceRequest, bool> Applies, Func<ChoiceRequest, ChoiceResult> Answer, bool OneShot)> _handlers = new();
    private readonly ScriptedChoiceProvider _fallback = new();

    public void On(Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot = true)
        => _handlers.Add((applies, answer, oneShot));

    public List<string> Seen { get; } = new();

    public Task<ChoiceResult> ChooseAsync(ChoiceRequest request, CancellationToken cancellationToken = default)
    {
        Seen.Add($"{request.Type}:'{request.Message}'x{request.Candidates.Count}[{string.Join(",", request.Candidates.Take(6).Select(c => c.Id.Value))}]");
        for (int i = 0; i < _handlers.Count; i++)
        {
            (Func<ChoiceRequest, bool> applies, Func<ChoiceRequest, ChoiceResult> answer, bool oneShot) = _handlers[i];
            if (applies(request))
            {
                ChoiceResult result = answer(request);
                result.ThrowIfInvalid(request);
                if (oneShot) { _handlers.RemoveAt(i); }
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
            provider, randomSource, new CardDatabase(), cardInstanceRepository, zoneMover,
            new InMemoryRuleQueryService(), new InMemoryHeadlessTurnController(), choiceController,
            new InMemoryHeadlessAttackController(), memoryController, logSink,
            new HeadlessDCGO.Engine.Headless.Coroutines.EngineTaskRunner(), effectScheduler,
            gameEventQueue: gameEventQueue);
        selfRef = context;
        return context;
    }
}
