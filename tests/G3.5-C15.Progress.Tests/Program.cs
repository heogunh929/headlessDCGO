// C-15 Progress (S2): while a Progress Digimon attacks, it is not affected by the opponent's effects
// (UntilEndAttack). AS-IS ProgressStaticEffect (CanNotAffectedClass, SkillCondition = IsOpponentEffect),
// a passive static effect. Engine: ContinuousImmunityGate (opponent-only immunity, source-relativity) +
// the mutation sink immunity check; ProgressImmunity auto-registers the immunity at attack declaration;
// grant GrantProgress -> hasProgress.
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1);   // protected side
HeadlessPlayerId P2 = new(2);   // opponent (effect source) side

var tests = new (string Name, Func<Task> Body)[]
{
    ("Immunity blocks an opponent-sourced effect; own effects pass", GateSourceRelativity),
    ("The sink skips an opponent delete on an immune card", SinkBlocksOpponentDelete),
    ("The sink still applies an OWN delete on an immune card", SinkAllowsOwnDelete),
    ("A non-immune card is deleted by the opponent normally", NonImmuneDeleted),
    ("ProgressImmunity grants to the UntilEndAttack bucket on attack and expires at attack end", ProgressRegistersAndExpires),
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

async Task GateSourceRelativity()
{
    EngineContext context = await Field(
        ("P1-Protected", P1),
        ("P2-Enemy", P2),
        ("P1-Ally", P1));
    var protectedId = new HeadlessEntityId("P1-Protected");
    RegisterImmunity(context, protectedId, P1);

    AssertTrue(ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, protectedId, new HeadlessEntityId("P2-Enemy"), context),
        "opponent-sourced effect is blocked");
    AssertFalse(ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, protectedId, new HeadlessEntityId("P1-Ally"), context),
        "own/ally effect is not blocked (source-relativity)");
}

async Task SinkBlocksOpponentDelete()
{
    // (B군 P0-1) The sink's S2 general-immunity gate (MatchStateMutationSink.cs:527) was rehomed from the now-dead
    // ContinuousImmunityGate.BlocksOpponentEffect registry scan to the LIVE CardSource.CanNotBeAffected getter. So
    // the immunity must be a live CanNotAffectedClass on the protected card's effect list (the seam a ported card
    // uses — G9-057 idiom), NOT a registry binding. The skillCondition is opponent-only (IsOpponentEffect).
    EngineContext context = await Field(("P1-Protected", P1), ("P2-Enemy", P2));
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);   // CanUse -> DoneStartGame gate
    var protectedId = new HeadlessEntityId("P1-Protected");
    GrantLiveImmunity(context, protectedId, P1);
    using var _ = AmbientMatchContext.Enter(context);

    await DeleteBy(context, protectedId, deleter: new HeadlessEntityId("P2-Enemy"));

    AssertFalse(InZone(context, P1, ChoiceZone.Trash, protectedId), "an opponent delete is prevented by live immunity");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, protectedId), "the protected card survives");
}

async Task SinkAllowsOwnDelete()
{
    EngineContext context = await Field(("P1-Protected", P1), ("P1-Ally", P1));
    var protectedId = new HeadlessEntityId("P1-Protected");
    RegisterImmunity(context, protectedId, P1);

    await DeleteBy(context, protectedId, deleter: new HeadlessEntityId("P1-Ally"));

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, protectedId), "an own-sourced delete still applies (not blocked)");
}

async Task NonImmuneDeleted()
{
    EngineContext context = await Field(("P1-Plain", P1), ("P2-Enemy", P2));
    var plainId = new HeadlessEntityId("P1-Plain");

    await DeleteBy(context, plainId, deleter: new HeadlessEntityId("P2-Enemy"));

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, plainId), "a non-immune card is deleted normally");
}

async Task ProgressRegistersAndExpires()
{
    EngineContext context = await Field(("P1-Attacker", P1), ("P2-Enemy", P2));
    var attackerId = new HeadlessEntityId("P1-Attacker");

    // (R3-W3c-2) PRODUCER FLIP: TryRegister now appends the AS-IS CanNotAffectedClass to the attacker's
    // UntilEndAttack bucket (AS-IS ProgressProcess), read back LIVE by CardSource.CanNotBeAffected — NOT the
    // invented registry binding. CanUse->IsDisabled reads GManager.instance, so drive it inside the ambient
    // match scope with a started game (same harness as W3B-Progress / G9-054).
    context.TurnController.Initialize(new[] { P1, P2 }, P1);
    context.TurnController.SetPhase(HeadlessPhase.Main);
    using var _scope = AmbientMatchContext.Enter(context);
    SetFlag(context, attackerId, ProgressImmunity.HasProgressKey);

    // P1 attacker declares an attack; Progress passively grants the opponent-effect immunity to the bucket.
    context.AttackController.DeclareAttack(P1, attackerId, P2, targetId: null, isDirectAttack: true);
    ProgressImmunity.TryRegister(context);

    ICardEffect opponentProbe = OpponentProbe(context, new HeadlessEntityId("P2-Enemy"), P2);
    AssertTrue(new CardSource(context, attackerId, P1).CanNotBeAffected(opponentProbe),
        "Progress immunity is active during the attack (bucket granted + live scan read)");

    // (R3-W3c-2) expiry is the AS-IS UntilEndAttack bucket reset (AttackProcess.Cleanup :489-495), NOT the
    // registry-expiry sweep — reset the attacker's bucket + the per-attack applied marker, exactly as Cleanup does.
    new Permanent(context, attackerId, P1).UntilEndAttackEffects = new();
    ProgressImmunity.ClearApplied(context, attackerId);

    AssertFalse(new CardSource(context, attackerId, P1).CanNotBeAffected(opponentProbe),
        "Progress immunity expires at attack end (bucket reset)");
}

// A probe ICardEffect whose EffectSourceCard is the given field card — the AS-IS "causing effect" the
// CanNotBeAffected scan hands to the granted immunity's SkillCondition (IsOpponentEffect checks its source owner).
ICardEffect OpponentProbe(EngineContext ctx, HeadlessEntityId sourceId, HeadlessPlayerId owner)
{
    var source = new CardSource(ctx, sourceId, owner);
    return CardEffectFactory.ProgressStaticEffect(isInheritedEffect: false, card: source, condition: () => true);
}

// --- Harness -------------------------------------------------------------

async Task<EngineContext> Field(params (string Id, HeadlessPlayerId Owner)[] cards)
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 7);
    foreach ((string id, HeadlessPlayerId owner) in cards)
    {
        var entityId = new HeadlessEntityId(id);
        context.CardInstanceRepository.Upsert(new CardInstanceRecord(entityId, new HeadlessEntityId("def"), owner,
            Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
        await context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, entityId, ChoiceZone.None, ChoiceZone.BattleArea));
    }

    return context;
}

void RegisterImmunity(EngineContext context, HeadlessEntityId targetId, HeadlessPlayerId owner)
{
    // (structure-1:1) opponent-only immunity as the canonical joint predicate ContinuousImmunityGate reads on the
    // context path (mirrors ProgressImmunity + AS-IS CanNotBeAffected — protected card, opponent-sourced cause).
    var effectContext = new EffectContext(
        owner, owner, targetId,
        triggerEntityId: null,
        targetEntityIds: new[] { targetId },
        values: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [ContinuousImmunityGate.JointPredicateKey] =
                (Func<HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource, HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardSource, bool>)(
                    (prot, cause) => prot.InstanceId == targetId && cause.Owner != prot.Owner),
        });
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId($"{targetId.Value}:immunity"), owner, "Continuous", effectContext),
        keywords: new[] { "Progress" },
        EffectQueryRole.Continuous,
        new[] { ContinuousImmunityGate.Scope }));
}

async Task DeleteBy(EngineContext context, HeadlessEntityId targetId, HeadlessEntityId deleter)
{
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context.GameEventQueue, context: context);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, deleter,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = targetId.Value }));
    await sink.FlushAsync();
}

void SetFlag(EngineContext context, HeadlessEntityId cardId, string key)
{
    if (!context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null) return;
    context.CardInstanceRepository.Upsert(record with
    {
        Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { [key] = true }
    });
}

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

// (B군 P0-1) Attach a LIVE opponent-only CanNotAffectedClass to the target card's effect list (permanentCondition
// null => protects the holder). This is the AS-IS seam the live CardSource.CanNotBeAffected scan reads — the sink's
// rehomed S2 gate consults it instead of the retired registry.
void GrantLiveImmunity(EngineContext context, HeadlessEntityId targetId, HeadlessPlayerId owner)
{
    var card = new CardSource(context, targetId, owner);
    ICardEffect immunity = CardEffectFactory.CanNotAffectedStaticEffect(
        permanentCondition: null,
        skillCondition: e => CardEffectCommons.IsOpponentEffect(e?.EffectSourceCard, card),
        isInheritedEffect: false, card: card, condition: null);
    card.cEntity_EffectController.cEntity_Effect = new LiveImmunityEntityEffect(immunity);
}

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }

// Returns the supplied CanNotAffectedClass from the card's live effect list (the seam a ported card definition uses).
sealed class LiveImmunityEntityEffect : CEntity_Effect
{
    private readonly ICardEffect _effect;
    public LiveImmunityEntityEffect(ICardEffect effect) { _effect = effect; }
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource cardSource) => new() { _effect };
}
