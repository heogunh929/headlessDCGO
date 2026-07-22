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

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// W3-SpecialWitness — end-to-end behavior witnesses for the 4 ported witness cards (BT25_075, EX9_008,
// P_098, BT21_077) + the RD-3A-01-resolved StartOfMainAttack firing window.
// Harness = EXEMPLAR-T1/T2A 정본(DcgoMatch.CreatePumpDriven + PolicyChoiceProvider 동기 좌석). 효과 몸통은
// EffectList(timing)에서 ActivateClass를 뽑아 Activate()로 실구동(EXEMPLAR-T3B FireDigisorption 관례),
// 선택 프롬프트는 policy.On(...)으로 결정론적 응답. 상태 변화(LinkedMax/isSuspended/DigivolutionCards/
// BattleDeletionGate/AttackController)를 직접 관찰한다.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT25_075 W1 inert-grant: [All Turns] ChangeLinkMaxStaticEffect(+1) LinkedMax read-side 실착지 — [TS] owner Digimon LinkedMax=2, 비-TS=1", BT25075_LinkMaxGrantConsumed),
    ("EX9_008 W1 Training: OnDeclaration TrainingEffect 실구동 — 자기 서스펜드 + 라이브러리 톱 카드 진화원 바닥 추가", EX9008_TrainingSuspendAndBottomAdd),
    ("P_098 W1 grant: [On Play] can't-be-deleted-in-battle 부여된 파랑 Digimon이 배틀 삭제에서 생존(BattleDeletionGate ON), 비대상 OFF", P098_GrantedSurvivesBattleDeletion),
    ("BT21_077 W1 full-loop: [On Play] grant → 대상 owner 메인 시작 창(OnStartMainPhase)에 강제공격 ActivateClass 표면·게이팅", BT21077_StartOfMainAttackWindowSurfaces),
    ("BT21_077 W2 full-loop: 강제공격 offer 실발화(decline 불가) → 공격 선언(AttackController attackerId=대상)", BT21077_MandatoryAttackFires),
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
            Console.WriteLine(string.Join('\n', st.Split('\n').Take(10)));
        }
    }
}

Console.WriteLine($"SUMMARY: PASS={tests.Length - failed} FAIL={failed} TOTAL={tests.Length}");
if (failed > 0) { Environment.Exit(1); }

// ═══════════════════════════════════ BT25_075 ═══════════════════════════════════

async Task BT25075_LinkMaxGrantConsumed()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 9101);
    await ReachMainWaitAsync(match);

    Stage(match, P1, "BT25_075", ChoiceZone.BattleArea, "1:battle:Vulcanusmon", register: true);
    HeadlessEntityId tsId = StageSynthetic(match, P1, "TS-DIGI", dp: 6000, level: 5, "1:battle:ts", traits: new[] { "TS" });
    HeadlessEntityId plainId = StageSynthetic(match, P1, "PLAIN-DIGI", dp: 6000, level: 5, "1:battle:plain");

    // The [All Turns] ChangeLinkMaxStaticEffect(+1) is registered under None on BT25_075 (on the battle area).
    // Read side = Permanent.LinkedMax (LinkHelpers.ResolveLinkedMax folds active ChangeLinkMaxClass). Base = 1.
    AssertEqual(2, LinkedMax(match, tsId, P1), "a [TS] owner Digimon's LinkedMax is base(1)+1 — the grant is CONSUMED (not inert)");
    AssertEqual(1, LinkedMax(match, plainId, P1), "negative: a non-[TS] Digimon keeps base LinkedMax(1) — the +1 is correctly scoped to [TS]");
}

// ═══════════════════════════════════ EX9_008 ═══════════════════════════════════

async Task EX9008_TrainingSuspendAndBottomAdd()
{
    (DcgoMatch match, PolicyChoiceProvider _) = await NewMatchAsync(seed: 9201);
    await ReachMainWaitAsync(match);

    HeadlessEntityId biyomon = Stage(match, P1, "EX9_008", ChoiceZone.BattleArea, "1:battle:Biyomon", register: true);

    int digiBefore = DigivolutionCount(match, biyomon, P1);
    int libBefore = ZoneCards(match, P1, ChoiceZone.Library).Count;
    AssertTrue(!IsSuspendedMeta(match, biyomon), "precondition: Biyomon is unsuspended");
    AssertTrue(libBefore > 0, "precondition: the library has cards to place under");

    Cec.ICardEffect training = EffectNamed(match, biyomon, Cec.EffectTiming.OnDeclaration, "Training")
        ?? throw new InvalidOperationException("Training effect not registered under OnDeclaration");
    AssertTrue(CanActivate(match, training), "CanActivate ON: CanActivateSuspendCostEffect (Biyomon can suspend)");

    await DriveActivateAsync(match, training);

    AssertTrue(IsSuspendedMeta(match, biyomon), "[Training] suspended (tapped) Biyomon");
    AssertEqual(digiBefore + 1, DigivolutionCount(match, biyomon, P1), "[Training] placed 1 card face-down at the bottom of Biyomon's digivolution cards");
    AssertEqual(libBefore - 1, ZoneCards(match, P1, ChoiceZone.Library).Count, "[Training] moved the top library card out of the deck (into Biyomon's stack)");
}

// ═══════════════════════════════════ P_098 ═══════════════════════════════════

async Task P098_GrantedSurvivesBattleDeletion()
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed: 9301);
    await ReachMainWaitAsync(match);

    HeadlessEntityId p098 = Stage(match, P1, "P_098", ChoiceZone.BattleArea, "1:battle:P098", register: true);
    HeadlessEntityId blue = StageSynthetic(match, P1, "BLUE-DIGI", dp: 5000, level: 4, "1:battle:blue", colors: new[] { "Blue" });
    HeadlessEntityId foe = StageSynthetic(match, P2, "FOE-ATK", dp: 5000, level: 4, "2:battle:foe");

    // Drive the [On Play] can't-be-deleted grant; the SelectPermanent picks the blue Digimon.
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == blue),
        req => ChoiceResult.Select(blue), oneShot: false);

    Cec.ICardEffect onPlay = EffectNamedByDesc(match, p098, Cec.EffectTiming.OnEnterFieldAnyone,
        "Your 1 Digimon cannot be deleted in battle", "[On Play]")
        ?? throw new InvalidOperationException("[On Play] can't-be-deleted grant not registered");
    await DriveActivateAsync(match, onPlay);

    // Positive: with the blue Digimon as the battle defender, the gate reports it protected (survives deletion).
    match.Context.AttackController.DeclareAttack(P2, foe, P1, targetId: blue);
    AssertTrue(BattleDeletionGate.PreventsBattleDeletion(match.Context, blue),
        "the granted blue Digimon is protected from battle deletion when it is the battle defender (GainCanNotBeDeletedByBattle consumed by BattleDeletionGate)");

    // Negative: the ungranted attacker is not protected.
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(match.Context, foe),
        "negative: the ungranted opponent attacker is NOT protected");

    // Negative 2: the blue Digimon in an unrelated attack (neither attacker nor defender) — the 4-arg predicate fails.
    HeadlessEntityId foe2 = StageSynthetic(match, P2, "FOE2", dp: 5000, level: 4, "2:battle:foe2");
    match.Context.AttackController.DeclareAttack(P2, foe, P1, targetId: foe2);
    AssertTrue(!BattleDeletionGate.PreventsBattleDeletion(match.Context, blue),
        "negative: the grant is battle-scoped — the blue Digimon is unprotected when it is neither the attacker nor the defender");
}

// ═══════════════════════════════════ BT21_077 ═══════════════════════════════════

// Grants StartOfMainAttack + Collision to an opponent Digimon via the [On Play] body, returns the granted target.
async Task<(DcgoMatch Match, PolicyChoiceProvider Policy, HeadlessEntityId Target)> GrantViaBT21077Async(int seed)
{
    (DcgoMatch match, PolicyChoiceProvider policy) = await NewMatchAsync(seed);
    await ReachMainWaitAsync(match);

    HeadlessEntityId bt = Stage(match, P1, "BT21_077", ChoiceZone.BattleArea, "1:battle:Regulusmon", register: true);
    // Hand card with [Gammamon] in its text (CanTrashCard = HasText("Gammamon"); HasText reads def meta "effect").
    HeadlessEntityId gamma = StageSynthetic(match, P1, "GAMMA-TXT", dp: 2000, level: 3, "1:hand:gamma",
        zone: ChoiceZone.Hand, extraDefMeta: new Dictionary<string, object?> { ["effect"] = "Gammamon inheritance" });
    HeadlessEntityId target = StageSynthetic(match, P2, "FOE-TGT", dp: 5000, level: 4, "2:battle:foetgt");

    // [On Play]: discard 1 Gammamon-text card, then pick an opponent Digimon to receive the grant.
    policy.On(req => (req.Type == ChoiceType.HandCard || req.Type == ChoiceType.Card) && req.Candidates.Any(c => c.Id == gamma),
        req => ChoiceResult.Select(gamma), oneShot: false);
    policy.On(req => req.Type == ChoiceType.Permanent && req.Candidates.Any(c => c.Id == target),
        req => ChoiceResult.Select(target), oneShot: false);

    Cec.ICardEffect onPlay = EffectNamed(match, bt, Cec.EffectTiming.OnEnterFieldAnyone, "Force attack and give collision")
        ?? throw new InvalidOperationException("[On Play] Force-attack grant not registered");
    await DriveActivateAsync(match, onPlay);
    return (match, policy, target);
}

async Task BT21077_StartOfMainAttackWindowSurfaces()
{
    (DcgoMatch match, PolicyChoiceProvider _, HeadlessEntityId target) = await GrantViaBT21077Async(seed: 9401);

    // The grant added a GetCardEffect delegate to the target's UntilOwnerTurnEndEffects that yields the mandatory
    // attack ActivateClass ONLY at OnStartMainPhase — this is the firing window the RD-3A-01 STOP said was missing.
    Cec.ICardEffect? atMain = PermanentEffectNamed(match, target, Cec.EffectTiming.OnStartMainPhase, "Attack with this Digimon");
    AssertTrue(atMain is not null, "the granted mandatory-attack ActivateClass SURFACES at the OnStartMainPhase window (EffectList_Added walks UntilOwnerTurnEndEffects)");

    Cec.ICardEffect? atNone = PermanentEffectNamed(match, target, Cec.EffectTiming.None, "Attack with this Digimon");
    AssertTrue(atNone is null, "the grant is timing-gated: it does NOT surface at None (GetCardEffect returns null off-window)");

    // CanUse is true on the target-owner's (P2) main phase — the mandatory attack is live at their main entry.
    match.Context.TurnController.Initialize(new[] { P1, P2 }, P2);
    match.Context.TurnController.SetPhase(HeadlessPhase.Main);
    AssertTrue(CanUse(match, PermanentEffectNamed(match, target, Cec.EffectTiming.OnStartMainPhase, "Attack with this Digimon")!),
        "CanUse ON at the target-owner's (P2) main phase — the offer is live at their next main-phase entry");

    // On the NON-owner's (P1) turn the CanUse gate is false (start-of-YOUR-main-phase only).
    match.Context.TurnController.Initialize(new[] { P1, P2 }, P1);
    match.Context.TurnController.SetPhase(HeadlessPhase.Main);
    AssertTrue(!CanUse(match, PermanentEffectNamed(match, target, Cec.EffectTiming.OnStartMainPhase, "Attack with this Digimon")!),
        "negative: CanUse OFF on the opponent's (P1) turn — [Start of YOUR Main Phase] gate");
}

async Task BT21077_MandatoryAttackFires()
{
    (DcgoMatch match, PolicyChoiceProvider policy, HeadlessEntityId target) = await GrantViaBT21077Async(seed: 9402);

    // It is the target-owner's (P2) main phase.
    match.Context.TurnController.Initialize(new[] { P1, P2 }, P2);
    match.Context.TurnController.SetPhase(HeadlessPhase.Main);

    // Drive the granted mandatory-attack ActivateClass. SelectAttackEffect raises a ChoiceType.Permanent request
    // with CanSkip == false (SetCanNotSelectNotAttack — decline impossible). Answer it by attacking the player
    // (the synthetic "{attacker}:attack-player" candidate).
    bool mandatoryOfferFired = false;
    HeadlessEntityId attackPlayerId = new($"{target.Value}:attack-player");
    policy.On(
        req =>
        {
            if (req.Type == ChoiceType.Permanent && !req.CanSkip && req.Candidates.Any(c => c.Id == attackPlayerId))
            {
                mandatoryOfferFired = true;
                return true;
            }
            return false;
        },
        req => ChoiceResult.Select(attackPlayerId),
        oneShot: false);

    Cec.ICardEffect atMain = PermanentEffectNamed(match, target, Cec.EffectTiming.OnStartMainPhase, "Attack with this Digimon")
        ?? throw new InvalidOperationException("granted attack effect missing at OnStartMainPhase");

    bool declared = false;
    try
    {
        await DriveActivateAsync(match, atMain);
    }
    finally
    {
        declared = match.Context.AttackController.Current.AttackerId == target;
    }

    AssertTrue(mandatoryOfferFired, "the mandatory attack offer FIRED (a no-skip ChoiceType.Permanent attack prompt was raised — decline impossible per SetCanNotSelectNotAttack)");
    AssertTrue(declared || IsSuspendedMeta(match, target),
        "the offer resolved into a real attack by the granted Digimon (AttackController attackerId == target, or the attacker suspended)");
}

// ═══════════════════════════════ card-specific helpers ═══════════════════════════════

static int LinkedMax(DcgoMatch match, HeadlessEntityId id, HeadlessPlayerId owner)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.Permanent(match.Context, id, owner).LinkedMax;
}

static int DigivolutionCount(DcgoMatch match, HeadlessEntityId id, HeadlessPlayerId owner)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return new Cec.Permanent(match.Context, id, owner).DigivolutionCards.Count;
}

async Task DriveActivateAsync(DcgoMatch match, Cec.ICardEffect effect)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await ((Cec.ActivateICardEffect)effect).Activate(new Hashtable());
}

// Granted DURATION effects live in the PERMANENT's Until*Effects buckets (Permanent.EffectList_Added), NOT the
// card's own EffectList — so the OnStartMainPhase grant is read via Permanent.EffectList(timing).
Cec.ICardEffect? PermanentEffectNamed(DcgoMatch match, HeadlessEntityId id, Cec.EffectTiming timing, string name)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    HeadlessPlayerId owner = OwnerOf(match, id);
    return new Cec.Permanent(match.Context, id, owner).EffectList(timing).FirstOrDefault(e => e.EffectName == name);
}

// ═══════════════════════════════ generic effect helpers (EXEMPLAR-T2A) ═══════════════════════════════

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

Cec.ICardEffect? EffectNamedByDesc(DcgoMatch match, HeadlessEntityId id, Cec.EffectTiming timing, string name, string descContains)
{
    HeadlessPlayerId owner = OwnerOf(match, id);
    return EffectsOf(match, id, owner, timing)
        .FirstOrDefault(e => e.EffectName == name && (e.EffectDiscription ?? string.Empty).Contains(descContains, StringComparison.Ordinal));
}

static bool CanActivate(DcgoMatch match, Cec.ICardEffect effect)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return effect.CanActivate(new Hashtable());
}

static bool CanUse(DcgoMatch match, Cec.ICardEffect effect)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    return effect.CanUse(new Hashtable());
}

static HeadlessPlayerId OwnerOf(DcgoMatch match, HeadlessEntityId id) =>
    match.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
        ? rec.OwnerId
        : new HeadlessPlayerId(1);

static bool IsSuspendedMeta(DcgoMatch match, HeadlessEntityId cardId) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) && record is not null
    && record.Metadata.TryGetValue("isSuspended", out object? raw) && raw is true;

static IReadOnlyList<HeadlessEntityId> ZoneCards(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone) =>
    match.Context.ZoneMover is IZoneStateReader zones ? zones.GetCards(player, zone) : Array.Empty<HeadlessEntityId>();

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

// ═══════════════════════════════════ harness ═══════════════════════════════════

PlayerDeckSetup[] MonoDecks() => new[]
{
    new PlayerDeckSetup(P1, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
    new PlayerDeckSetup(P2, Enumerable.Repeat(new HeadlessEntityId("BT1_028"), 50).ToArray()),
};

async Task<(DcgoMatch Match, PolicyChoiceProvider Policy)> NewMatchAsync(int seed)
{
    var policy = new PolicyChoiceProvider();
    EngineContext context = ContextFactory.CreateWithProvider(policy, seed);
    CardBaseEntityLoader.LoadInto((CardDatabase)context.CardRepository);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        MonoDecks(), firstPlayerId: P1, initialHandSize: 0, initialSecuritySize: 0, enableMulligan: false);
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
        throw new InvalidOperationException($"drive did not reach main-wait — phase:{t.Phase}/{t.StepCursor} player:{t.TurnPlayerId}");
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

    using AmbientMatchContext.Scope _s = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

// 실카드 스테이징(EXEMPLAR-T2A Stage 관례).
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

// 합성 픽스처(EXEMPLAR-T2A StageSynthetic 관례 + colors/effect def-meta 확장).
HeadlessEntityId StageSynthetic(DcgoMatch match, HeadlessPlayerId owner, string number, int dp, int level, string instanceId,
    string? name = null, string cardType = "Digimon", ChoiceZone zone = ChoiceZone.BattleArea,
    string[]? traits = null, string[]? colors = null, Dictionary<string, object?>? extraDefMeta = null, int? playCost = null)
{
    EngineContext ctx = match.Context;
    var defId = new HeadlessEntityId($"DEF:{number}:{owner.Value}:{instanceId}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = level };
    if (traits is { Length: > 0 })
    {
        meta["traits"] = traits;
    }

    if (colors is { Length: > 0 })
    {
        meta["colors"] = colors;
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

// ═══════════════════════════════ providers/context (EXEMPLAR-T1 정본) ═══════════════════════════════

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
