// C-15 Progress (S2): while a Progress Digimon attacks, it is not affected by the opponent's effects
// (UntilEndAttack). AS-IS ProgressStaticEffect (CanNotAffectedClass, SkillCondition = IsOpponentEffect),
// a passive static effect. Engine: ContinuousImmunityGate (opponent-only immunity, source-relativity) +
// the mutation sink immunity check; ProgressImmunity auto-registers the immunity at attack declaration;
// grant GrantProgress -> hasProgress.
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
    ("ProgressImmunity registers on attack and expires at attack end", ProgressRegistersAndExpires),
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

    AssertTrue(ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, protectedId, new HeadlessEntityId("P2-Enemy")),
        "opponent-sourced effect is blocked");
    AssertFalse(ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, protectedId, new HeadlessEntityId("P1-Ally")),
        "own/ally effect is not blocked (source-relativity)");
}

async Task SinkBlocksOpponentDelete()
{
    EngineContext context = await Field(("P1-Protected", P1), ("P2-Enemy", P2));
    var protectedId = new HeadlessEntityId("P1-Protected");
    RegisterImmunity(context, protectedId, P1);

    await DeleteBy(context, protectedId, deleter: new HeadlessEntityId("P2-Enemy"));

    AssertFalse(InZone(context, P1, ChoiceZone.Trash, protectedId), "an opponent delete is prevented by immunity");
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
    SetFlag(context, attackerId, ProgressImmunity.HasProgressKey);

    // P1 attacker declares an attack; Progress passively registers the opponent-effect immunity.
    context.AttackController.DeclareAttack(P1, attackerId, P2, targetId: null, isDirectAttack: true);
    ProgressImmunity.TryRegister(context);

    AssertTrue(ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, attackerId, new HeadlessEntityId("P2-Enemy")),
        "Progress immunity is active during the attack");

    EffectDurationExpiry.ExpireAttackEnd(context.EffectRegistry);

    AssertFalse(ContinuousImmunityGate.BlocksOpponentEffect(context.EffectRegistry, context.CardInstanceRepository, attackerId, new HeadlessEntityId("P2-Enemy")),
        "Progress immunity expires at attack end");
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
    var effectContext = new EffectContext(
        owner, owner, targetId,
        triggerEntityId: null,
        targetEntityIds: new[] { targetId },
        values: new Dictionary<string, object?>(StringComparer.Ordinal) { [ContinuousImmunityGate.ImmunityFromOpponentOnlyKey] = true });
    context.EffectRegistry.Register(new EffectBinding(
        new EffectRequest(new HeadlessEntityId($"{targetId.Value}:immunity"), owner, "Continuous", effectContext),
        keywords: new[] { "Progress" },
        EffectQueryRole.Continuous,
        new[] { ContinuousImmunityGate.Scope }));
}

async Task DeleteBy(EngineContext context, HeadlessEntityId targetId, HeadlessEntityId deleter)
{
    var sink = new MatchStateMutationSink(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry);
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

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
