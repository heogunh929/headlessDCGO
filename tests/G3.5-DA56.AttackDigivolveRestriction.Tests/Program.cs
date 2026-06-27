using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// D-A6: attack legality is now target-aware — a continuous "cannot attack <defender>" restriction
// removes only that defender from the attack candidates (other defenders / direct attack remain).
// D-A5: digivolve legality consults a continuous "cannot digivolve" restriction on the under-card.
// Both are consumption-side wiring: no-op until such restrictions are registered (Phase 4 card pool);
// here the tests register them synthetically to exercise the gate.

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId DefenderA = new("p2:main:001:P2-M01");
HeadlessEntityId DefenderB = new("p2:main:002:P2-M02");

var tests = new (string Name, Func<Task> Body)[]
{
    ("D-A6: a defender-scoped cannot-attack removes only that target", TargetScopedAttackRestriction),
    ("D-A6 control: without the restriction both defenders are attackable", AttackControl),
    ("D-A5: a cannot-digivolve restriction removes the digivolve onto that target", DigivolveRestriction),
    ("D-A5 control: without the restriction the digivolve is offered", DigivolveControl),
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

// --- D-A6: attack-target restriction -------------------------------------

async Task TargetScopedAttackRestriction()
{
    DcgoMatch match = await AttackSetup();
    // "Attacker cannot attack DefenderA" — scoped to DefenderA only.
    RegisterCannotAttack(match.Context, AttackerId, owner: P1, scopedDefender: DefenderA);

    var declarations = new AttackPermanentAction().GetAttackDeclarations(match.Context, P1)
        .Single(d => d.AttackerId == AttackerId);
    HeadlessEntityId?[] targets = declarations.TargetCandidates.Select(c => c.TargetId).ToArray();

    AssertFalse(targets.Contains(DefenderA), "restricted defender A is excluded");
    AssertTrue(targets.Contains(DefenderB), "unrestricted defender B remains attackable");
    AssertTrue(targets.Contains((HeadlessEntityId?)null), "direct attack remains available");
}

async Task AttackControl()
{
    DcgoMatch match = await AttackSetup();
    var declarations = new AttackPermanentAction().GetAttackDeclarations(match.Context, P1)
        .Single(d => d.AttackerId == AttackerId);
    HeadlessEntityId?[] targets = declarations.TargetCandidates.Select(c => c.TargetId).ToArray();

    AssertTrue(targets.Contains(DefenderA), "defender A attackable without restriction");
    AssertTrue(targets.Contains(DefenderB), "defender B attackable without restriction");
}

// --- D-A5: cannot-digivolve restriction ----------------------------------

async Task DigivolveRestriction()
{
    DcgoMatch match = await DigivolveSetup();
    HeadlessEntityId underCard = HandToBattle(match);
    HeadlessEntityId evolving = FirstHand(match, P1);
    RegisterCannotDigivolve(match.Context, underCard, owner: P1);

    bool offered = new DigivolveAction().GetLegalActions(match.Context, P1)
        .Any(a => ReadId(a.Parameters, HeadlessActionParameterKeys.TargetCardId) == underCard.Value);
    AssertFalse(offered, "digivolve onto a restricted under-card is not offered");
}

async Task DigivolveControl()
{
    DcgoMatch match = await DigivolveSetup();
    HeadlessEntityId underCard = HandToBattle(match);

    bool offered = new DigivolveAction().GetLegalActions(match.Context, P1)
        .Any(a => ReadId(a.Parameters, HeadlessActionParameterKeys.TargetCardId) == underCard.Value);
    AssertTrue(offered, "digivolve onto an unrestricted under-card is offered");
}

// --- Restriction registration --------------------------------------------

void RegisterCannotAttack(EngineContext context, HeadlessEntityId attackerId, HeadlessPlayerId owner, HeadlessEntityId scopedDefender)
{
    // The restriction is ABOUT the attacker (the continuous query targets attackerId). The DEFENDER scope
    // is encoded as the restriction's SourceEntityId — CannotAttack(attackerId, …, defenderId) matches
    // defenderId against restriction.SourceEntityId (restriction.TargetEntityId would scope the attacker).
    // The simple `cannotAttack` flag yields a GLOBAL restriction, so use the explicit object form.
    Register(context, attackerId, owner, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [RestrictionHelpers.RestrictionsKey] = new[]
        {
            new CannotRestriction($"no-attack:{scopedDefender.Value}", CannotRestrictionKind.Attack, targetEntityId: null, sourceEntityId: scopedDefender)
        }
    });
}

void RegisterCannotDigivolve(EngineContext context, HeadlessEntityId targetCardId, HeadlessPlayerId owner)
{
    Register(context, targetCardId, owner, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [RestrictionHelpers.CannotDigivolveKey] = true
    });
}

void Register(EngineContext context, HeadlessEntityId aboutCardId, HeadlessPlayerId owner, Dictionary<string, object?> values)
{
    var effectId = new HeadlessEntityId($"restrict:{aboutCardId.Value}:{string.Join(",", values.Keys)}");
    var effectContext = new EffectContext(
        owner, owner, new HeadlessEntityId($"src:{aboutCardId.Value}"),
        triggerEntityId: null, targetEntityIds: new[] { aboutCardId }, values: values);
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(effectId, owner, "Continuous", effectContext),
        keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope }));
}

// --- Harness -------------------------------------------------------------

async Task<DcgoMatch> AttackSetup()
{
    DcgoMatch match = await BaseMatch();
    EngineContext ctx = match.Context;
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, DefenderA, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, DefenderB, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMeta(match, AttackerId, new() { ["isSuspended"] = false, ["dp"] = 3000 });
    SetMeta(match, DefenderA, new() { ["isSuspended"] = true, ["dp"] = 3000 });
    SetMeta(match, DefenderB, new() { ["isSuspended"] = true, ["dp"] = 3000 });
    return match;
}

async Task<DcgoMatch> DigivolveSetup()
{
    // BaseMatch already advances P1 to Main; the hand has playable cards with fixedDigivolutionCost 0.
    return await BaseMatch();
}

HeadlessEntityId HandToBattle(DcgoMatch match)
{
    HeadlessEntityId underCard = FirstHand(match, P1);
    match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, underCard, ChoiceZone.Hand, ChoiceZone.BattleArea)).GetAwaiter().GetResult();
    return underCard;
}

HeadlessEntityId FirstHand(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Hand)
        .OrderBy(id => id.Value, StringComparer.Ordinal).First();

async Task<DcgoMatch> BaseMatch()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 33);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 33, setup: setup));

    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card",
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["fixedDigivolutionCost"] = 0 },
        CardType: "Digimon", PlayCost: 0);

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

void SetMeta(DcgoMatch match, HeadlessEntityId cardId, Dictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing instance '{cardId}'.");
    Dictionary<string, object?> meta = new(record.Metadata, StringComparer.Ordinal);
    foreach (var kv in values) meta[kv.Key] = kv.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = meta });
}

static string? ReadId(IReadOnlyDictionary<string, object?> p, string key)
{
    if (!p.TryGetValue(key, out object? raw) || raw is null) return null;
    return raw is HeadlessEntityId id ? id.Value : raw.ToString();
}

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
