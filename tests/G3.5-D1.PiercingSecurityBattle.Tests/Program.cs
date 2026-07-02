using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// G3.5-D1: a Piercing attacker's follow-up security check now runs the SAME loop as a direct attack
// (SecurityResolver.RunSecurityCheckLoopAsync) — including the W5 security-Digimon battle and the W4
// OnSecurityCheck window. Before the fix the piercing path only moved cards to trash, so a revealed
// security Digimon never battled the attacker and security effects never fired.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Piercing into a stronger security Digimon deletes the attacker", PiercingIntoStrongerSecurityDeletesAttacker),
    ("Piercing into a weaker security Digimon leaves the attacker alive", PiercingIntoWeakerSecuritySurvives),
    ("Piercing fires the revealed security card's OnSecurityCheck effect", PiercingFiresSecurityEffect),
    ("(A1) Piercing into EMPTY security does nothing — no invented game loss", PiercingIntoEmptySecurityNoLoss),
    ("(B2) battle triggers drain BEFORE the piercing check: a knock-out trigger that kills the attacker cancels it", TriggerKillsAttackerBeforePiercing),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex.GetType().Name}: {ex.Message}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task PiercingIntoStrongerSecurityDeletesAttacker()
{
    // Attacker 5000 beats target 3000 (piercing triggers), then the revealed security Digimon (7000)
    // out-DPs the attacker -> attacker is deleted by the security battle.
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 7000, piercing: true);
    await DeclareTargetAttackAsync(match);

    AssertFalse(InZone(match, P1, ChoiceZone.BattleArea, AttackerId), "attacker left the battle area");
    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId, "attacker deleted by the security Digimon");
}

async Task PiercingIntoWeakerSecuritySurvives()
{
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 2000, piercing: true);
    HeadlessEntityId topSecurity = TopSecurity(match, P2);
    await DeclareTargetAttackAsync(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "attacker survives the weaker security Digimon");
    AssertInZone(match, P2, ChoiceZone.Trash, topSecurity, "security card was still checked into trash");
}

async Task PiercingFiresSecurityEffect()
{
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 2000, piercing: true);
    HeadlessEntityId topSecurity = TopSecurity(match, P2);

    var securityEffect = new RecordingFakeEffect("sec-fx", topSecurity.Value, TriggerTimings.OnSecurityCheck);
    match.Context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId("sec-fx"), P2, TriggerTimings.OnSecurityCheck,
            new EffectContext(P2, P2, topSecurity, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>())),
        effect: securityEffect));

    await DeclareTargetAttackAsync(match);

    AssertEqual(1, securityEffect.ResolveCalls, "the revealed security card's OnSecurityCheck effect fired via piercing");
}

// (A1) AS-IS CanActivatePierce: Pierce fires only with >= 1 security; with 0 security nothing happens.
// The empty-security game loss belongs only to the DIRECT-attack path.
async Task PiercingIntoEmptySecurityNoLoss()
{
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 1000, piercing: true);
    var zones = (IZoneStateReader)match.Context.ZoneMover;
    foreach (HeadlessEntityId securityCard in zones.GetCards(P2, ChoiceZone.Security).ToArray())
    {
        await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, securityCard, ChoiceZone.Security, ChoiceZone.Trash));
    }

    await DeclareTargetAttackAsync(match);

    AssertFalse(match.Context.PlayerStatusController.IsLose(P2), "the defending player did NOT lose (Pierce no-op at 0 security)");
    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "attacker survives (battle won, no security battle)");
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "the defending Digimon was deleted by the field battle");
}

// (B2) AS-IS AttackProcess: battle → TriggeredSkillProcess (drain) → `if (AttackingPermanent.TopCard ==
// null) End` → security check. A battle-generated trigger that removes the attacker must cancel Piercing.
async Task TriggerKillsAttackerBeforePiercing()
{
    DcgoMatch match = await Setup(attackerDp: 5000, targetDp: 3000, topSecurityDp: 1000, piercing: true);
    int before = ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Security).Count;

    var killer = new AttackerKillingEffect("ko-fx", TargetId.Value, TriggerTimings.OnKnockOut, AttackerId);
    match.Context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId("ko-fx"), P2, TriggerTimings.OnKnockOut,
            new EffectContext(P2, P2, TargetId, triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>())),
        effect: killer));

    await DeclareTargetAttackAsync(match);

    AssertEqual(1, killer.ResolveCalls, "the knock-out trigger resolved");
    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId, "the attacker was deleted by the drained trigger");
    AssertEqual(before, ((IZoneStateReader)match.Context.ZoneMover).GetCards(P2, ChoiceZone.Security).Count,
        "NO security was checked — Piercing was cancelled by the pre-check survival test");
}

// --- Harness (from C2) ---------------------------------------------------

async Task<DcgoMatch> Setup(int attackerDp, int targetDp, int topSecurityDp, bool piercing)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match);

    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));

    var attackerMeta = new Dictionary<string, object?> { ["isSuspended"] = false, ["dp"] = attackerDp };
    if (piercing) attackerMeta[BattleResolver.HasPiercingKey] = true;
    SetMetadata(match, AttackerId, attackerMeta);
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true, ["dp"] = targetDp });

    // The piercing check reveals the top of P2's security first — give that card the test DP.
    SetMetadata(match, TopSecurity(match, P2), new Dictionary<string, object?> { ["dp"] = topSecurityDp });
    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

async Task AdvanceToMainAsync(DcgoMatch match)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

async Task DeclareTargetAttackAsync(DcgoMatch match)
{
    LegalAction attack = match.GetLegalActions(P1)
        .Single(a => a.ActionType == HeadlessActionTypes.DeclareAttack &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.AttackTargetId) == TargetId.Value);
    await match.ApplyActionAsync(attack);
    await match.StepAsync();
}

static string? ReadId(IReadOnlyDictionary<string, object?> parameters, string key)
{
    if (!parameters.TryGetValue(key, out object? raw) || raw is null) return null;
    return raw is HeadlessEntityId entityId ? entityId.Value : raw.ToString();
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

static HeadlessEntityId TopSecurity(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Security)[0];

static bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

static void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label)
{
    if (!InZone(match, player, zone, cardId)) throw new InvalidOperationException($"{label}: not in {zone}.");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}

// (B2) a knock-out trigger that deletes the attacker via the mutation sink.
internal sealed class AttackerKillingEffect : IHeadlessCardEffect
{
    private readonly HeadlessEntityId _attackerId;

    public AttackerKillingEffect(string effectId, string sourceId, string timing, HeadlessEntityId attackerId)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId), new HeadlessEntityId(sourceId), name: effectId, timing: timing);
        _attackerId = attackerId;
    }

    public CardEffectDefinition Definition { get; }

    public int ResolveCalls { get; private set; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        mutations.Apply(new EffectMutation(
            MatchStateMutationSink.DeleteKind,
            Definition.SourceEntityId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = _attackerId.Value,
            }));
        return ValueTask.FromResult(EffectResult.Success("attacker deleted"));
    }
}

internal sealed class RecordingFakeEffect : IHeadlessCardEffect
{
    public RecordingFakeEffect(string effectId, string sourceId, string timing)
    {
        Definition = new CardEffectDefinition(
            new HeadlessEntityId(effectId), new HeadlessEntityId(sourceId), name: effectId, timing: timing);
    }

    public CardEffectDefinition Definition { get; }

    public int ResolveCalls { get; private set; }

    public CardEffectCanResolveResult CanResolve(CardEffectResolveContext context) => CardEffectCanResolveResult.Success();

    public ValueTask<EffectResult> ResolveAsync(
        CardEffectResolveContext context,
        IEffectMutationSink mutations,
        CancellationToken cancellationToken = default)
    {
        ResolveCalls++;
        return ValueTask.FromResult(EffectResult.Success("fake resolved"));
    }
}
