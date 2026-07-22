using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
// (R4 S3b) the mirror Script/MainPhaseAction classes share the AS-IS names — pin the Runtime one.
using AttackPermanentAction = HeadlessDCGO.Engine.Headless.Runtime.AttackPermanentAction;

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

// (④ harness rewire) The invented EffectRegistry JointRestrictionEffect bindings are deleted; the attack/
// digivolve gates now read the AS-IS-literal NewModelContinuousScan (ICanNotAttackTargetDefendingPermanentEffect
// / ICanNotDigivolveEffect over field permanents). Grant each restriction the way a real card does: attach a
// live kind-class (CardEffectFactory.CanNotAttackStaticEffect / CanNotDigivolveStaticEffect) to a field
// permanent's live effect list, with the attacker/defender (resp. under-card) scoping carried by the
// kind-class's own attackerCondition/defenderCondition (resp. permanentCondition) predicates.
void RegisterCannotAttack(EngineContext context, HeadlessEntityId attackerId, HeadlessPlayerId owner, HeadlessEntityId scopedDefender)
{
    var holder = new CardSource(context, attackerId, owner);
    using (AmbientMatchContext.Enter(context))
    {
        holder.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(
            CardEffectFactory.CanNotAttackStaticEffect(
                attackerCondition: a => a is not null && a.InstanceId == attackerId,
                // null defender = direct attack (blanket "cannot attack anything") — NOT restricted here.
                defenderCondition: d => d is not null && d.InstanceId == scopedDefender,
                isInheritedEffect: false, card: holder, condition: () => true, effectName: "cannot-attack-scoped"));
    }
}

void RegisterCannotDigivolve(EngineContext context, HeadlessEntityId targetCardId, HeadlessPlayerId owner)
{
    var holder = new CardSource(context, targetCardId, owner);
    using (AmbientMatchContext.Enter(context))
    {
        holder.cEntity_EffectController.cEntity_Effect = new TestCardEntityEffect(
            CardEffectFactory.CanNotDigivolveStaticEffect(
                permanentCondition: p => p is not null && p.InstanceId == targetCardId,
                cardCondition: null,
                isInheritedEffect: false, card: holder, condition: () => true, effectName: "cannot-digivolve-onto"));
    }
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

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") }, firstPlayerId: P1,
        shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 33, setup: setup));

    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, P1));

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


// --- Phase driving (pump auto-flow, F62/c2/EXEMPLAR-T1 precedent, 4b B2-c3) ---
// Drive the pump's natural Active->Draw->Breeding->Main auto-flow to the player's main wait; the OLD
// AdvancePhase step currency is retired. Breeding/Mulligan decisions are declined; assertion strength unchanged.
static bool AtMainWaitOf(DcgoMatch match, HeadlessPlayerId player) =>
    match.Context.TurnController.Current.Phase == HeadlessPhase.Main
    && match.Context.TurnController.Current.TurnPlayerId == player
    && !match.HasPendingChoice() && !match.IsTerminal();

static async Task DriveUntilAsync(DcgoMatch match, Func<DcgoMatch, bool> condition)
{
    for (int i = 0; i < 96 && !condition(match); i++)
    {
        if (match.HasPendingChoice())
        {
            bool decline = match.Context.ChoiceController.PendingRequest!.Type is ChoiceType.BreedingDecision or ChoiceType.Mulligan;
            await ResolvePendingDriveAsync(match, skip: decline);
        }
        else await StepOnceDriveAsync(match);
    }
    if (!condition(match))
    {
        HeadlessTurnState t = match.Context.TurnController.Current;
        throw new InvalidOperationException(
            $"pump drive did not reach the expected state - phase:{t.Phase} turn:{t.TurnNumber} player:{t.TurnPlayerId} " +
            $"choice:{match.Context.ChoiceController.PendingRequest?.Type.ToString() ?? "<none>"} pending:{match.HasPendingChoice()} terminal:{match.IsTerminal()}");
    }
}

static async Task ResolvePendingDriveAsync(DcgoMatch match, bool skip)
{
    HeadlessPlayerId chooser = match.Context.ChoiceController.PendingRequest!.PlayerId;
    LegalAction? action;
    using (AmbientMatchContext.Enter(match.Context))
    {
        action = match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice
                && a.Id.Value.EndsWith(":skip", StringComparison.Ordinal) == skip)
            ?? match.GetLegalActions(chooser).FirstOrDefault(a => a.ActionType == HeadlessActionTypes.ResolveChoice);
    }
    if (action is null) throw new InvalidOperationException("no ResolveChoice lane for the pending request");
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.ApplyActionAsync(action);
    await match.StepAsync();
    await match.StepAsync();
}

static async Task StepOnceDriveAsync(DcgoMatch match)
{
    using AmbientMatchContext.Scope _ = AmbientMatchContext.Enter(match.Context);
    await match.StepAsync();
}

// (④) attaches a built kind-class to a card's live effect list (the seam a ported card uses); no timing key
// so it surfaces at EffectTiming.None (the continuous-scan read point).
sealed class TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}

