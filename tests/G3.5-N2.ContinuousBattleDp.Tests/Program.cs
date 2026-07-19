using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// N-2 / D-A1: a FIELD battle recomputes each permanent's DP through the continuous registry (ContinuousDpGate),
// so a continuous DP modifier from ANOTHER card changes who wins. (RD-7 Part A / TODO-71 correction) A SECURITY
// battle is different: AS-IS a security Digimon uses its CardDP, which folds ONLY IChangeCardDPEffect
// (securityCardDpDelta), NOT the permanent-DP pipeline — so a GENERIC continuous DP effect does NOT touch a
// security Digimon's battle DP (the attacker's own DP, as a permanent, still uses ContinuousDpGate).

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);
HeadlessEntityId AttackerId = new("p1:main:001:P1-M01");
HeadlessEntityId TargetId = new("p2:main:001:P2-M01");
HeadlessEntityId SecurityDigimonId = new("p2:main:006:P2-M06");

var tests = new (string Name, Func<Task> Body)[]
{
    ("Field battle: a continuous +DP buff flips the loser into the winner", FieldBuffFlipsOutcome),
    ("Field battle control: without the buff the lower-DP attacker is deleted", FieldControlDeleted),
    ("Field battle: a continuous -DP debuff on the attacker makes it lose", FieldDebuffLoses),
    ("Security battle: a continuous +DP buff lets the attacker survive a stronger security Digimon", SecurityBuffSurvives),
    ("Security battle: a GENERIC permanent-DP debuff does NOT touch the security Digimon's CardDP (TODO-71)", SecurityGenericDpDoesNotTouchCardDp),
    ("D-A3: DP-reduction immunity prevents a continuous -DP from reducing the target", ImmunityPreventsReduction),
    ("D-A3: immunity still lets a positive buff apply", ImmunityKeepsBuff),
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

async Task FieldBuffFlipsOutcome()
{
    // Attacker 3000 would lose to target 5000; a continuous +3000 on the attacker makes it 6000 -> wins.
    DcgoMatch match = await FieldSetup(attackerDp: 3000, targetDp: 5000);
    RegisterDpModifier(match.Context, AttackerId, owner: P1, dpDelta: 3000);
    await DeclareTargetAttack(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "buffed attacker survived");
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "weaker target was deleted");
}

async Task FieldControlDeleted()
{
    DcgoMatch match = await FieldSetup(attackerDp: 3000, targetDp: 5000);
    await DeclareTargetAttack(match);

    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId, "unbuffed lower-DP attacker was deleted");
}

async Task FieldDebuffLoses()
{
    // Attacker 5000 would beat target 3000; a continuous -3000 on the attacker makes it 2000 -> loses.
    DcgoMatch match = await FieldSetup(attackerDp: 5000, targetDp: 3000);
    RegisterDpModifier(match.Context, AttackerId, owner: P1, dpDelta: -3000);
    await DeclareTargetAttack(match);

    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId, "debuffed attacker was deleted");
    AssertInZone(match, P2, ChoiceZone.BattleArea, TargetId, "target survived");
}

async Task SecurityBuffSurvives()
{
    // Attacker 2000 vs a 7000 security Digimon -> would die; +6000 makes it 8000 -> survives.
    DcgoMatch match = await SecuritySetup(attackerDp: 2000, topSecurityDp: 7000);
    RegisterDpModifier(match.Context, AttackerId, owner: P1, dpDelta: 6000);
    await DeclareDirectAttack(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "buffed attacker survived the security Digimon");
}

async Task SecurityGenericDpDoesNotTouchCardDp()
{
    // (RD-7 Part A / TODO-71) A security Digimon battles with its CardDP, which folds ONLY IChangeCardDPEffect
    // (headless securityCardDpDelta), NOT the permanent-DP pipeline. So a GENERIC continuous -3000 on the
    // security Digimon does NOT reduce its battle DP: it stays 7000 and the 5000 attacker still dies. (A
    // securityCardDpDelta effect WOULD change it — covered by the security-battle DP-grant cards.)
    DcgoMatch match = await SecuritySetup(attackerDp: 5000, topSecurityDp: 7000);
    RegisterDpModifier(match.Context, SecurityDigimonId, owner: P2, dpDelta: -3000);
    await DeclareDirectAttack(match);

    AssertInZone(match, P1, ChoiceZone.Trash, AttackerId,
        "a generic permanent-DP debuff does NOT weaken a security Digimon's CardDP; the attacker still dies");
}

async Task ImmunityPreventsReduction()
{
    // Attacker 5000 vs target 3000 -> attacker wins. A continuous -3000 would drop it to 2000 (loss),
    // but a DP-reduction-immunity replacement prevents the reduction, so the attacker still wins.
    DcgoMatch match = await FieldSetup(attackerDp: 5000, targetDp: 3000);
    RegisterDpModifier(match.Context, AttackerId, owner: P1, dpDelta: -3000);
    RegisterDpReductionImmunity(match.Context, AttackerId, owner: P1);
    await DeclareTargetAttack(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "immune attacker was not reduced");
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "target deleted by the un-reduced attacker");
}

async Task ImmunityKeepsBuff()
{
    // Attacker 3000 vs target 5000 -> would lose. A +3000 buff makes it 6000; immunity must not block
    // the positive buff, so the attacker still wins.
    DcgoMatch match = await FieldSetup(attackerDp: 3000, targetDp: 5000);
    RegisterDpModifier(match.Context, AttackerId, owner: P1, dpDelta: 3000);
    RegisterDpReductionImmunity(match.Context, AttackerId, owner: P1);
    await DeclareTargetAttack(match);

    AssertInZone(match, P1, ChoiceZone.BattleArea, AttackerId, "buffed immune attacker survived");
    AssertInZone(match, P2, ChoiceZone.Trash, TargetId, "weaker target deleted");
}

// --- Continuous DP modifier registration ---------------------------------

// (수리-2 repair) The old raw `values["dpDelta"]` continuous binding routed through
// ContinuousEffectEvaluator.ResolveDp, which is DEAD — 0 live callers (CardEffectCommons.cs:1732-1735) — so the
// delta never materialised into an IChangeDPEffect and Permanent.DP (the battle-read seat, BattleResolver.cs:759)
// never folded it. The attack-time DP seat is AS-IS-correct; the test's registration was obsolete. Reconstructed
// with the LIVE canon proven green by FAILa-02: a PlayerScopeModifierEffect(DpDeltaKey) binding, which
// ModifierHelpers folds into Permanent.DP.
void RegisterDpModifier(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId owner, int dpDelta)
{
    var cards = (CardDatabase)context.CardRepository;
    var srcDef = new HeadlessEntityId("SRC-DPMOD");
    cards.Upsert(new CardRecord(srcDef, "SRC-DPMOD", "dp source",
        new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon"));
    var srcId = new HeadlessEntityId($"src:{cardId.Value}:{dpDelta}");
    context.CardInstanceRepository.Upsert(new CardInstanceRecord(srcId, srcDef, owner));

    var source = new CardSource(context, srcId, owner);
    var target = new Permanent(context, cardId, owner);
    // AS-IS ChangeDigimonDP: store a live ±DP ChangeDPClass (IChangeDPEffect) into the target's None duration
    // bucket, which Permanent.DP folds LIVE (Permanent.cs:348) — the same mechanism the field battle reads at
    // BattleResolver.cs:759. Duration spans the whole turn so it survives the battle resolution.
    CardEffectCommons.ChangeDigimonDP(target, dpDelta, EffectDuration.UntilEachTurnEnd, source);
}

// (수리-2 repair) DP-reduction immunity via the live ImmuneFromDPMinusClass attached on the protected card's
// cEntity_EffectController (the AS-IS-faithful seam; the class has no EffectRegistry bridge) — the exact pattern
// FAILa-02 proves green. NewModelContinuousScan.HasImmuneFromDpMinus evaluates it per reducing modifier.
void RegisterDpReductionImmunity(EngineContext context, HeadlessEntityId cardId, HeadlessPlayerId owner)
{
    var protectedCard = new CardSource(context, cardId, owner);
    ICardEffect built = CardEffectFactory.ImmuneFromDPMinusStaticEffect(
        permanentCondition: null,
        cardEffectCondition: null,
        isInheritedEffect: false,
        card: protectedCard, condition: null, effectName: "ImmuneFromDPMinus");
    protectedCard.cEntity_EffectController.cEntity_Effect = new N2TestCardEntityEffect(built);
}

// --- Harness (field battle, from C2 / R2-1) ------------------------------

async Task<DcgoMatch> FieldSetup(int attackerDp, int targetDp)
{
    DcgoMatch match = await BaseMatch();
    EngineContext context = match.Context;
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, ["dp"] = attackerDp });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true, ["dp"] = targetDp });
    return match;
}

async Task DeclareTargetAttack(DcgoMatch match)
{
    LegalAction attack = match.GetLegalActions(P1)
        .Single(a => a.ActionType == HeadlessActionTypes.DeclareAttack &&
            ReadId(a.Parameters, HeadlessActionParameterKeys.AttackTargetId) == TargetId.Value);
    await match.ApplyActionAsync(attack);
    await match.StepAsync();
}

// --- Harness (security battle, from W5 / R2-1) ---------------------------

async Task<DcgoMatch> SecuritySetup(int attackerDp, int topSecurityDp)
{
    DcgoMatch match = await BaseMatch(initialSecurity: 0);
    EngineContext context = match.Context;
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P1, AttackerId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, TargetId, ChoiceZone.Hand, ChoiceZone.BattleArea));
    await context.ZoneMover.MoveAsync(new ZoneMoveRequest(P2, SecurityDigimonId, ChoiceZone.None, ChoiceZone.Security));

    SetMetadata(match, AttackerId, new Dictionary<string, object?> { ["isSuspended"] = false, ["dp"] = attackerDp, [SecurityResolver.StrikeKey] = 1 });
    SetMetadata(match, TargetId, new Dictionary<string, object?> { ["isSuspended"] = true });
    SetMetadata(match, SecurityDigimonId, new Dictionary<string, object?> { ["dp"] = topSecurityDp });
    return match;
}

async Task DeclareDirectAttack(DcgoMatch match)
{
    match.Context.AttackController.DeclareAttack(P1, AttackerId, P2, targetId: null, isDirectAttack: true);
    await new AttackPipeline().AdvanceAsync(match.Context);
    await match.StepAsync();
}

// --- Shared --------------------------------------------------------------

async Task<DcgoMatch> BaseMatch(int initialSecurity = 5)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 74);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1,
        initialSecuritySize: initialSecurity,
        shuffleDecks: false,
        shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 74, setup: setup));

    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction advance = match.GetLegalActions(P1).Single(a => a.ActionType == HeadlessActionTypes.AdvancePhase);
        await match.ApplyActionAsync(advance);
        await match.StepAsync();
    }

    return match;
}

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static string? ReadId(IReadOnlyDictionary<string, object?> p, string key)
{
    if (!p.TryGetValue(key, out object? raw) || raw is null) return null;
    return raw is HeadlessEntityId id ? id.Value : raw.ToString();
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

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, zone).Contains(cardId);

void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label)
{
    if (!InZone(match, player, zone, cardId)) throw new InvalidOperationException($"{label}: {cardId.Value} not in {player.Value}'s {zone}.");
}

// (수리-2 repair) Wraps a single ICardEffect as a dispatch-less CEntity_Effect so it can be pinned onto a card's
// cEntity_EffectController (the ImmuneFromDPMinus attachment seam; mirrors FAILa-02's TestCardEntityEffect).
sealed class N2TestCardEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public N2TestCardEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
