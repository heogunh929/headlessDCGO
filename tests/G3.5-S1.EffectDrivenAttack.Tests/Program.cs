using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Diagnostics;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// S1 Effect-driven attack: the engine hub for an EFFECT initiating an attack (AS-IS SelectAttackEffect),
// unlocking C-20 Vortex / C-16 Overclock (attack part). No new pipeline — declaring the attack lets the
// existing AttackPipeline drive it. The target is an agent choice (rules-faithful). Options control the
// suspend cost (WithoutTap) and legal targets (player / Digimon / unsuspended).

HeadlessPlayerId P1 = new(1);   // attacker side (turn player)
HeadlessPlayerId P2 = new(2);   // defender side

var tests = new (string Name, Func<Task> Body)[]
{
    ("Initiate declares the effect-driven attack on the chosen target", InitiateDeclaresAttack),
    ("WithoutTap leaves the attacker unsuspended; default suspends it", WithoutTapControlsSuspend),
    ("GetTargets honours AllowDigimonTarget / AllowPlayerTarget", TargetsHonourAllowFlags),
    ("GetTargets excludes unsuspended Digimon unless TargetUnsuspended", TargetsHonourUnsuspended),
    ("Initiate refuses to nest inside a pending attack", InitiateRefusesNesting),
    ("Agent choice: selecting a target declares the attack", ChoiceSelectsTarget),
    ("Agent choice: declining initiates no attack", ChoiceDeclineNoAttack),
    ("(DEF-S6) mandatory attack (CanSelectNotAttack=false) is non-skippable and rejects a skip", MandatoryAttackNotSkippable),
    ("Vortex options expose Digimon and player targets (unsuspended allowed)", VortexOptionsTargets),
    ("(RD-9 / P1-2) an effect-driven attack emits the OnAttack choke point (window opens via the inline insert) and NOT OnDeclaration", EffectDrivenFiresWhenAttacking),
    ("(RD-9 / P1-2) the shared chokepoint emits OnAttack and NO LONGER emits OnAllyAttack (inline insert is the sole opener)", EffectDrivenSkipsOnDeclaration),
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

async Task InitiateDeclaresAttack()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId target = await Establish(s, P2, dp: 3000, suspended: true);

    var options = new EffectAttackOptions();
    AttackTargetCandidate pick = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, options)
        .Single(t => t.TargetId == target);
    AssertTrue(EffectDrivenAttack.Initiate(s.Match.Context, attacker, pick, options), "initiate succeeds");

    HeadlessAttackState attack = s.Match.Context.AttackController.Current;
    AssertTrue(attack.IsPending, "an attack is now pending");
    AssertEqual(attacker, attack.AttackerId, "attacker matches");
    AssertEqual(target, attack.TargetId, "target matches");
    AssertFalse(attack.IsDirectAttack, "a Digimon attack is not direct");
}

async Task WithoutTapControlsSuspend()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    _ = await Establish(s, P2, dp: 3000, suspended: true);

    AttackTargetCandidate player = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions())
        .Single(t => t.IsDirectAttack);
    EffectDrivenAttack.Initiate(s.Match.Context, attacker, player, new EffectAttackOptions(WithoutTap: true));
    AssertFalse(ReadFlag(s.Match, attacker, EffectDrivenAttack.IsSuspendedKey), "WithoutTap keeps the attacker unsuspended");

    Setup s2 = await NewMatch();
    HeadlessEntityId attacker2 = await Establish(s2, P1, dp: 4000, suspended: false);
    _ = await Establish(s2, P2, dp: 3000, suspended: true);
    AttackTargetCandidate player2 = EffectDrivenAttack.GetTargets(s2.Match.Context, attacker2, new EffectAttackOptions())
        .Single(t => t.IsDirectAttack);
    EffectDrivenAttack.Initiate(s2.Match.Context, attacker2, player2, new EffectAttackOptions(WithoutTap: false));
    AssertTrue(ReadFlag(s2.Match, attacker2, EffectDrivenAttack.IsSuspendedKey), "default suspends the attacker (cost)");
}

async Task TargetsHonourAllowFlags()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    _ = await Establish(s, P2, dp: 3000, suspended: true);

    var playerOnly = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions(AllowDigimonTarget: false));
    AssertTrue(playerOnly.All(t => t.IsDirectAttack), "AllowDigimonTarget=false leaves only the player target");
    AssertTrue(playerOnly.Any(t => t.IsDirectAttack), "the player target is present");

    var digimonOnly = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions(AllowPlayerTarget: false));
    AssertTrue(digimonOnly.All(t => !t.IsDirectAttack), "AllowPlayerTarget=false leaves only Digimon targets");
    AssertTrue(digimonOnly.Count > 0, "a Digimon target is present");
}

async Task TargetsHonourUnsuspended()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId unsus = await Establish(s, P2, dp: 3000, suspended: false);

    var normal = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions(TargetUnsuspended: false, AllowPlayerTarget: false));
    AssertFalse(normal.Any(t => t.TargetId == unsus), "an unsuspended Digimon is not a normal target");

    var lifted = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions(TargetUnsuspended: true, AllowPlayerTarget: false));
    AssertTrue(lifted.Any(t => t.TargetId == unsus), "TargetUnsuspended lets the unsuspended Digimon be targeted");
}

async Task InitiateRefusesNesting()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId target = await Establish(s, P2, dp: 3000, suspended: true);

    var options = new EffectAttackOptions();
    AttackTargetCandidate pick = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, options).Single(t => t.TargetId == target);
    AssertTrue(EffectDrivenAttack.Initiate(s.Match.Context, attacker, pick, options), "first initiate succeeds");
    AssertFalse(EffectDrivenAttack.Initiate(s.Match.Context, attacker, pick, options), "nested initiate is refused");
}

async Task ChoiceSelectsTarget()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId target = await Establish(s, P2, dp: 3000, suspended: true);

    AssertTrue(EffectDrivenAttack.RequestChoice(s.Match.Context, attacker, new EffectAttackOptions()), "choice opened");
    AssertEqual(ChoiceType.EffectAttack, s.Match.Context.ChoiceController.PendingRequest!.Type, "choice type");
    AssertTrue(EffectDrivenAttack.ResolveChoice(s.Match.Context, ChoiceResult.Select(target)), "resolve succeeds");

    HeadlessAttackState attack = s.Match.Context.AttackController.Current;
    AssertTrue(attack.IsPending, "the attack was declared");
    AssertEqual(target, attack.TargetId, "declared on the chosen target");
}

async Task ChoiceDeclineNoAttack()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    _ = await Establish(s, P2, dp: 3000, suspended: true);

    AssertTrue(EffectDrivenAttack.RequestChoice(s.Match.Context, attacker, new EffectAttackOptions()), "choice opened");
    AssertTrue(EffectDrivenAttack.ResolveChoice(s.Match.Context, ChoiceResult.Skip()), "resolve (skip) succeeds");

    AssertFalse(s.Match.Context.AttackController.Current.IsPending, "declining initiates no attack");
}

async Task MandatoryAttackNotSkippable()
{
    // (DEF-S6) AS-IS SelectAttackEffect.SetCanNotSelectNotAttack (SelectAttackEffect.cs:40-43) suppresses the
    // "Not Attack" opt-out — the effect-driven attack becomes MANDATORY. Threaded from SelectPermanentEffect
    // Attack mode (!_canNoSelect, :1023) and Overclock (:92). The mirror: canSkip=false + minCount=1.
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId target = await Establish(s, P2, dp: 3000, suspended: true);

    AssertTrue(EffectDrivenAttack.RequestChoice(s.Match.Context, attacker, new EffectAttackOptions(CanSelectNotAttack: false)),
        "mandatory choice opened");
    ChoiceRequest request = s.Match.Context.ChoiceController.PendingRequest!;
    AssertFalse(request.CanSkip, "mandatory attack request is not skippable");
    AssertEqual(1, request.MinCount, "mandatory attack requires a pick (minCount 1)");

    // A skip on a mandatory request is rejected (the choice stays pending, no attack declared).
    AssertFalse(EffectDrivenAttack.ResolveChoice(s.Match.Context, ChoiceResult.Skip()), "resolve (skip) is refused");
    AssertFalse(s.Match.Context.AttackController.Current.IsPending, "no attack declared from a refused skip");

    // The mandatory attacker CAN still declare on a legal target.
    AssertTrue(EffectDrivenAttack.ResolveChoice(s.Match.Context, ChoiceResult.Select(target)), "resolve (select) succeeds");
    AssertTrue(s.Match.Context.AttackController.Current.IsPending, "mandatory attack declared on the chosen target");
}

async Task VortexOptionsTargets()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    HeadlessEntityId unsus = await Establish(s, P2, dp: 3000, suspended: false);

    // Vortex: attack Digimon + players, unsuspended allowed.
    var vortex = new EffectAttackOptions(AllowDigimonTarget: true, AllowPlayerTarget: true, TargetUnsuspended: true);
    var targets = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, vortex);
    AssertTrue(targets.Any(t => t.IsDirectAttack), "player target available");
    AssertTrue(targets.Any(t => t.TargetId == unsus), "unsuspended Digimon target available");
}

// (RD-9 / P1-2 C2r) Before the chokepoint, EffectDrivenAttack.Initiate declared the attack but opened NO
// [When Attacking] window, so a Vortex/Execute attacker's OnAllyAttack effect never fired. Both attack paths now go
// through AttackDeclarationCommons.Declare -> AttackProcess.Attack(), which opens the OnAllyAttack window via an
// INLINE StackSkillInfos insert (the sole opener). The OnAllyAttack EMIT was REMOVED at the P1-2 flip (keeping both
// would double-fire — supply converts the emit to the same window). So at the EVENT layer we now assert the OnAttack
// choke-point emit is present (subject = attacker) and OnDeclaration is absent; the window actually OPENING for an
// effect-driven attack is witnessed END-TO-END in F1-Tier2-OnEndAttack (TfxEffectDrivenAllyAttackFires).
async Task EffectDrivenFiresWhenAttacking()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    _ = await Establish(s, P2, dp: 3000, suspended: true);
    s.Match.Context.GameEventQueue.DrainPending();   // discard setup events

    AttackTargetCandidate pick = EffectDrivenAttack.GetTargets(s.Match.Context, attacker, new EffectAttackOptions())
        .Single(t => t.IsDirectAttack);
    AssertTrue(EffectDrivenAttack.Initiate(s.Match.Context, attacker, pick, new EffectAttackOptions()), "initiate succeeds");

    var events = s.Match.Context.GameEventQueue.DrainPending();
    AssertTrue(events.Any(e => e.Cause == TriggerTimings.OnAttack && e.Subject == attacker),
        "effect-driven Initiate emits the OnAttack choke point (subject = attacker); the OnAllyAttack window opens via the inline insert");
    // The OnAllyAttack EMIT was removed at the P1-2 flip — the inline StackSkillInfos insert is the sole window opener.
    AssertFalse(events.Any(e => e.Cause == TriggerTimings.OnAllyAttack),
        "effect-driven Initiate does NOT re-emit OnAllyAttack (the inline insert is the sole opener — no double-fire)");
    // OnDeclaration is a MAIN-skill-declaration timing, not an attack window — the effect-driven path must not
    // emit it (only the player-action path does, as a stopgap for the not-yet-ported main-skill action).
    AssertFalse(events.Any(e => e.Cause == TriggerTimings.OnDeclaration),
        "effect-driven Initiate does NOT emit the main-skill OnDeclaration window");
}

// (RD-9 / P1-2 C2r) The shared chokepoint emits the OnAttack choke point but NO LONGER emits OnAllyAttack: the
// OnAllyAttack window is opened by an inline StackSkillInfos insert in AttackProcess.Attack(), so re-emitting it
// would double-fire (supply converts the emit to the same window). Assert OnAttack present, OnAllyAttack absent.
async Task EffectDrivenSkipsOnDeclaration()
{
    Setup s = await NewMatch();
    HeadlessEntityId attacker = await Establish(s, P1, dp: 4000, suspended: false);
    _ = await Establish(s, P2, dp: 3000, suspended: true);
    s.Match.Context.GameEventQueue.DrainPending();

    HeadlessAttackState attack = AttackDeclarationCommons.Declare(
        s.Match.Context, P1, attacker, P2, targetId: null, isDirectAttack: true);
    AssertTrue(attack.IsPending, "Declare puts an attack in flight");

    var events = s.Match.Context.GameEventQueue.DrainPending();
    AssertTrue(events.Any(e => e.Cause == TriggerTimings.OnAttack && e.Subject == attacker), "Declare emits OnAttack");
    AssertFalse(events.Any(e => e.Cause == TriggerTimings.OnAllyAttack),
        "Declare NO LONGER emits OnAllyAttack (the inline StackSkillInfos insert is the sole window opener)");
}

// --- Harness (mirrors G3.5-C3 / C18) -------------------------------------

async Task<Setup> NewMatch()
{
    EngineContext context = EngineContext.CreateDefault(randomSeed: 73);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Digimon($"P1-M{index:D2}"));
        cards.Upsert(Digimon($"P2-M{index:D2}"));
    }

    DcgoMatch match = DcgoMatch.CreatePumpDriven(context, new EngineTrace());
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { Deck(P1, "P1"), Deck(P2, "P2") },
        firstPlayerId: P1, shuffleDecks: false, shuffleDigitamaDecks: false);
    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 73, setup: setup));
    await AdvanceToMainAsync(match, P1);
    return new Setup(match, new Dictionary<int, int>());
}

async Task<HeadlessEntityId> Establish(Setup s, HeadlessPlayerId player, int dp, bool suspended)
{
    int next = s.Used.TryGetValue(player.Value, out int n) ? n + 1 : 1;
    s.Used[player.Value] = next;

    HeadlessEntityId card = HandCard(s.Match, player, next);
    await s.Match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(player, card, ChoiceZone.Hand, ChoiceZone.BattleArea));
    SetMetadata(s.Match, card, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        [BattleResolver.DpKey] = dp,
        [EffectDrivenAttack.IsSuspendedKey] = suspended
    });
    return card;
}

HeadlessEntityId HandCard(DcgoMatch match, HeadlessPlayerId player, int index)
{
    HeadlessEntityId[] hand = ((IZoneStateReader)match.Context.ZoneMover)
        .GetCards(player, ChoiceZone.Hand).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    if (hand.Length < index) throw new InvalidOperationException($"Player '{player}' hand has {hand.Length}; needed {index}.");
    return hand[index - 1];
}

async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId player)
{
    await StepOnceDriveAsync(match);
    await DriveUntilAsync(match, m => AtMainWaitOf(m, player));

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values) metadata[pair.Key] = pair.Value;
    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static CardRecord Digimon(string id) =>
    new(new HeadlessEntityId(id), id, $"{id} Card", new Dictionary<string, object?>(StringComparer.Ordinal), CardType: "Digimon");

static PlayerDeckSetup Deck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

static void AssertTrue(bool value, string label) { if (!value) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool value, string label) { if (value) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected '{expected}', actual '{actual}'.");
}

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

sealed record Setup(DcgoMatch Match, Dictionary<int, int> Used);

