// C-Del 3c-1c SECURITY-battle PRE cut-in transport witness (RD-3C1B-01). SecurityResolver now opens the AS-IS
// "would be deleted" PRE cut-in window (WhenPermanentWouldBeDeleted → WhenRemoveField) over the LOSING ATTACKER of
// a security-Digimon battle — the security-path sibling of BattleResolver's 3c-1b field-battle window. AS-IS a
// security battle exits through the SAME IBattle.Battle → DestroyPermanentsClass(LoserPermanents, hashtable).Destroy()
// as a field battle (CardController.cs:4179 → :4705 → :3690-3705), with the "battle" cause the LoserPermanents
// hashtable carries at :4700, so the byBattle cut-in (Barrier, Factory/Barrier.cs:53-55 IsByBattle) fires here too.
// The mirror security path never routes through the effect-delete sink, so this transport is required.
//
// A replacement body that clears willBeRemoveField SPARES the attacker (survival) → the check CONTINUES (AS-IS
// StopSecurityCheck breaks only when the attacker is gone). Non-interactive drains inline; an INTERACTIVE replacement
// promotes-to-defer, parks the remaining checks at DeletionReplacement, and FinalizeDeferredSecurityCheckAsync
// re-enters after the agent resolves the parked ForCutIn choice — resolving the survivor/casualty split on
// willBeRemoveField and resuming the remaining checks.
//
// GATE-INVISIBLE fixtures (TfxWouldBeDeleted / …ByBattle / …Interactive): non-keyword-named new-model ActivateClasses
// with no EffectRegistry binding, so HasPreOption(byBattle) is FALSE → they are NOT gate-deferred → the new window is
// the sole firing path. A REAL gate keyword (hasEvade) still parks via the gate — the new window EXCLUDES it (double-
// fire 0).

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var P1 = new HeadlessPlayerId(1);   // attacker's owner
var P2 = new HeadlessPlayerId(2);   // defender (security holder)

var tests = new (string Name, Func<Task> Body)[]
{
    ("NON-INTERACTIVE SURVIVE: an attacker that loses a security battle with a mandatory would-be-deleted replacement is spared (PRE window opened, willBeRemoveField cleared) and the check continues", NonInteractiveSurvives),
    ("CONTROL: a plain attacker that loses a security battle (no PRE effect) is trashed and stops the check (false-green guard)", ControlTrashed),
    ("BY-BATTLE CAUSE: a replacement that REQUIRES IsByBattle survives a SECURITY-BATTLE loss (the byBattle cause is threaded onto the security cut-in hashtable)", ByBattleCauseSurvives),
    ("INTERACTIVE SURVIVE: an optional replacement pauses the drain → the check defers; resolving YES then FinalizeDeferredSecurityCheckAsync spares the attacker and RESUMES the remaining check", InteractiveSurvivesParkResume),
    ("INTERACTIVE DECLINE: resolving NO leaves willBeRemoveField set → FinalizeDeferredSecurityCheckAsync trashes the attacker and stops the check", InteractiveDeclinesParkResume),
    ("RETIRED KEYWORD FIRES VIA WINDOW: a would-be-deleted survive replacement (the window form of the retired Evade keyword) fires through the SecurityResolver PRE cut-in window ALONE — NOT gate-deferred (DeferredForDeletionReplacement false) — sparing the attacker and continuing the check", RetiredKeywordFiresViaWindow),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ============================================================ TESTS

async Task NonInteractiveSurvives()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await PlaceBattle(ctx, P1, "TfxWouldBeDeleted", dp: 3000);   // loses the security battle, then survives via PRE
    var security = await PlaceSecurity(ctx, P2, "SECDGM", dp: 9000, isDigimon: true);
    Register(ctx, attacker, P1);
    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    SecurityCheckLoopResult loop = await new SecurityResolver()
        .RunSecurityCheckLoopAsync(ctx, Zones(ctx), P1, attacker, P2, strike: 1);

    AssertFalse(loop.AttackerDeleted, "the attacker was spared by the PRE would-be-deleted window (check continues)");
    AssertFalse(loop.DeferredForDeletionReplacement, "a mandatory replacement drained inline (no park)");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, attacker), "the survivor stays on the battle area");
    AssertFalse(InZone(ctx, P1, ChoiceZone.Trash, attacker), "the survivor is not in the trash");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, security), "the checked security card was trashed");
    AssertFalse(ReadFlag(ctx, attacker, GameFlowProcessor.PendingDeletionKey), "the survivor's pending flag was cleared");
    AssertFalse(ReadFlag(ctx, attacker, "willBeRemoveField"), "no stale willBeRemoveField marker on the survivor");
    AssertFalse(ReadFlag(ctx, attacker, BattleResolver.BattlePreWindowedKey), "the transient BattlePreWindowed marker was cleared");
}

async Task ControlTrashed()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await PlaceBattle(ctx, P1, "PLAIN", dp: 3000);   // no PRE effect
    var security = await PlaceSecurity(ctx, P2, "SECDGM", dp: 9000, isDigimon: true);
    Register(ctx, attacker, P1);
    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    SecurityCheckLoopResult loop = await new SecurityResolver()
        .RunSecurityCheckLoopAsync(ctx, Zones(ctx), P1, attacker, P2, strike: 1);

    AssertTrue(loop.AttackerDeleted, "the plain attacker was deleted by the security battle (no PRE replacement fired)");
    AssertFalse(loop.DeferredForDeletionReplacement, "no interactive choice for a plain loser (false-green guard)");
    AssertTrue(InZone(ctx, P1, ChoiceZone.Trash, attacker), "the plain loser is trashed");
    AssertFalse(InZone(ctx, P1, ChoiceZone.BattleArea, attacker), "the plain loser left the battle area");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, security), "the checked security card was trashed");
    AssertFalse(ctx.ChoiceController.Current.IsPending, "no pending choice for a plain loser");
}

async Task ByBattleCauseSurvives()
{
    // TfxWouldBeDeletedByBattle's replacement REQUIRES IsByBattle. It survives the SECURITY-battle loss only if the
    // byBattle cause is threaded onto the security cut-in hashtable (WhenPermanentWouldRemoveFieldCheckHashtable
    // byBattle overload → ByBattleCauseKey → IsByBattle dual-read). AS-IS carries it as the "battle" key at :4700 —
    // proving the cause is read, not that the card is unconditionally saved (the effect-delete negative is the
    // 3c-1b sibling witness ByBattleCauseNotSavedFromEffect).
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await PlaceBattle(ctx, P1, "TfxWouldBeDeletedByBattle", dp: 3000);
    var security = await PlaceSecurity(ctx, P2, "SECDGM", dp: 9000, isDigimon: true);
    Register(ctx, attacker, P1);
    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    SecurityCheckLoopResult loop = await new SecurityResolver()
        .RunSecurityCheckLoopAsync(ctx, Zones(ctx), P1, attacker, P2, strike: 1);

    AssertFalse(loop.AttackerDeleted, "the by-battle replacement fired (IsByBattle true) — the attacker survived");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, attacker), "the by-battle survivor stays on the battle area");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, security), "the checked security card was trashed");
}

async Task InteractiveSurvivesParkResume()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await PlaceBattle(ctx, P1, "TfxWouldBeDeletedInteractive", dp: 3000);
    // Strike 2: check #1 is a 9000 DP security Digimon (attacker loses → interactive PRE pauses), check #2 is a
    // 1000 DP security Digimon (the RESUMED check: the spared attacker wins). Check order = insertion order.
    var secHigh = await PlaceSecurity(ctx, P2, "SECHIGH", dp: 9000, isDigimon: true);
    var secLow = await PlaceSecurity(ctx, P2, "SECLOW", dp: 1000, isDigimon: true);
    Register(ctx, attacker, P1);
    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    SecurityCheckLoopResult loop = await new SecurityResolver()
        .RunSecurityCheckLoopAsync(ctx, Zones(ctx), P1, attacker, P2, strike: 2);

    // The optional PRE replacement paused the cut-in drain — the check promoted-to-defer and parked.
    AssertTrue(loop.DeferredForDeletionReplacement, "the interactive replacement deferred the security check (park at DeletionReplacement)");
    AssertEqual(1, loop.RemainingChecks, "one security check is still owed after the parked battle");
    AssertTrue(ctx.ChoiceController.Current.IsPending, "the parked ForCutIn window surfaced 'will you use it?'");
    AssertTrue(ReadFlag(ctx, attacker, GameFlowProcessor.PendingDeletionKey), "the attacker stays pendingDeletion while the choice pends");
    AssertTrue(ReadFlag(ctx, attacker, BattleResolver.BattlePreWindowedKey), "the attacker was PRE-windowed by the new security cut-in");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, attacker), "the attacker is NOT trashed while the choice pends");
    AssertTrue(HasMarker(ctx, attacker, SecurityResolver.SecurityCheckRemainingKey), "the remaining-checks park marker is stamped on the attacker");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, secHigh), "the first (9000) security card was already revealed+trashed");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Security, secLow), "the second (1000) security card is still owed (not yet checked)");

    // Resolve YES (ForCutIn pool resume), then finalize/resume the deferred security check.
    ChoiceRequest optional = ctx.ChoiceController.PendingRequest!;
    await ResolveWindowOptional(ctx, ChoiceResult.Select(optional.Candidates[0].Id));
    AssertFalse(ReadFlag(ctx, attacker, "willBeRemoveField"), "the resumed replacement body cleared willBeRemoveField (survives)");

    SecurityDeferredCheckResult resumed = await new SecurityResolver().FinalizeDeferredSecurityCheckAsync(ctx);

    AssertFalse(resumed.IsDeferred, "the deferred security check completed after the survivor resumed the remaining check");
    AssertFalse(resumed.AttackerDeleted, "the attacker survived and won the resumed 1000 DP security battle");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, attacker), "the interactive survivor was spared on the battle area");
    AssertFalse(InZone(ctx, P1, ChoiceZone.Trash, attacker), "the survivor is not in the trash");
    AssertFalse(ReadFlag(ctx, attacker, GameFlowProcessor.PendingDeletionKey), "the deferral cleared for the survivor");
    AssertFalse(HasMarker(ctx, attacker, SecurityResolver.SecurityCheckRemainingKey), "the park marker cleared");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, secLow), "the SECOND security card was checked (the loop RESUMED — secheck progress consistent)");
    AssertEqual(0, Zones(ctx).GetCards(P2, ChoiceZone.Security).Count, "no security left after the resumed check");
}

async Task InteractiveDeclinesParkResume()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await PlaceBattle(ctx, P1, "TfxWouldBeDeletedInteractive", dp: 3000);
    var secHigh = await PlaceSecurity(ctx, P2, "SECHIGH", dp: 9000, isDigimon: true);
    var secLow = await PlaceSecurity(ctx, P2, "SECLOW", dp: 1000, isDigimon: true);
    Register(ctx, attacker, P1);
    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    SecurityCheckLoopResult loop = await new SecurityResolver()
        .RunSecurityCheckLoopAsync(ctx, Zones(ctx), P1, attacker, P2, strike: 2);
    AssertTrue(loop.DeferredForDeletionReplacement, "the interactive replacement deferred the security check");

    // Resolve NO (decline the optional): the body never runs (willBeRemoveField stays set) → the attacker dies.
    await ResolveWindowOptional(ctx, ChoiceResult.Skip());
    SecurityDeferredCheckResult resumed = await new SecurityResolver().FinalizeDeferredSecurityCheckAsync(ctx);

    AssertFalse(resumed.IsDeferred, "declining the replacement lets the security-battle deletion finalize");
    AssertTrue(resumed.AttackerDeleted, "the declined attacker was deleted");
    AssertTrue(InZone(ctx, P1, ChoiceZone.Trash, attacker), "the declined attacker was trashed");
    AssertFalse(InZone(ctx, P1, ChoiceZone.BattleArea, attacker), "the declined attacker left the battle area");
    AssertFalse(ReadFlag(ctx, attacker, "willBeRemoveField"), "willBeRemoveField was reset before the trash (no stale marker)");
    AssertFalse(HasMarker(ctx, attacker, SecurityResolver.SecurityCheckRemainingKey), "the park marker cleared");
    // AS-IS StopSecurityCheck: with the attacker gone, no further security is checked.
    AssertTrue(InZone(ctx, P2, ChoiceZone.Security, secLow), "the SECOND security card was NOT checked (StopSecurityCheck: the attacker died)");
    _ = secHigh;
}

async Task RetiredKeywordFiresViaWindow()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    // 3c-2b flip: the PRE replacement keywords no longer PARK the security battle via the invented gate. A
    // window-collectible would-be-deleted survive replacement (the window form of the retired Evade keyword — the
    // printed TfxWouldBeDeleted ActivateClass) is HasPreOption(byBattle) FALSE, so the check is NOT gate-deferred
    // (DeferredForDeletionReplacement false). The keyword now fires through the SecurityResolver PRE cut-in window
    // ALONE — a single firing path (no gate/window double-fire) — sparing the attacker inline; the AS-IS
    // StopSecurityCheck does NOT break (the attacker is still on the field), so the check continues.
    var attacker = await PlaceBattle(ctx, P1, "TfxWouldBeDeleted", dp: 3000);
    var security = await PlaceSecurity(ctx, P2, "SECDGM", dp: 9000, isDigimon: true);
    Register(ctx, attacker, P1);
    ctx.AttackController.DeclareAttack(P1, attacker, P2, targetId: null, isDirectAttack: true);

    SecurityCheckLoopResult loop = await new SecurityResolver()
        .RunSecurityCheckLoopAsync(ctx, Zones(ctx), P1, attacker, P2, strike: 1);

    AssertFalse(loop.DeferredForDeletionReplacement, "the retired keyword is NOT gate-deferred (the gate firing half is retired) — no park");
    AssertFalse(loop.AttackerDeleted, "the keyword fired through the SecurityResolver PRE cut-in window — the attacker survived (check continues)");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, attacker), "the window-fired survivor stays on the battle area");
    AssertFalse(InZone(ctx, P1, ChoiceZone.Trash, attacker), "the survivor is not in the trash");
    AssertFalse(ReadFlag(ctx, attacker, BattleResolver.BattlePreWindowedKey), "the transient BattlePreWindowed marker was cleared after the inline drain");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, security), "the checked security card was trashed");
}

// ============================================================ HARNESS

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 31171, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

IZoneStateReader Zones(EngineContext ctx) => (IZoneStateReader)ctx.ZoneMover;

async Task<HeadlessEntityId> PlaceBattle(EngineContext ctx, HeadlessPlayerId owner, string num, int dp)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 };
    cards.Upsert(new CardRecord(defId, num, num, defMeta, CardType: "Digimon"));
    var id = new HeadlessEntityId($"{owner.Value}:battle:{num}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false };
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceSecurity(EngineContext ctx, HeadlessPlayerId owner, string num, int dp, bool isDigimon)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 };
    cards.Upsert(new CardRecord(defId, num, num, defMeta, CardType: isDigimon ? "Digimon" : "Option"));
    var id = new HeadlessEntityId($"{owner.Value}:sec:{num}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp };
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Security));
    return id;
}

void Register(EngineContext ctx, HeadlessEntityId id, HeadlessPlayerId owner) =>
    CardEffectRegistrar.RegisterCard(ctx, id, owner);

void SetFlag(EngineContext ctx, HeadlessEntityId cardId, string key, bool value)
{
    if (!ctx.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) || r is null) return;
    var meta = new Dictionary<string, object?>(r.Metadata, StringComparer.Ordinal) { [key] = value };
    ctx.CardInstanceRepository.Upsert(r with { Metadata = meta });
}

// Resolve the parked PRE cut-in optional (ForCutIn pool): record the answer + resume the cut-in pool; the deferred
// provider replays it. Same shape as the C-Del-3C1B-BattlePre witness.
async Task ResolveWindowOptional(EngineContext ctx, ChoiceResult answer)
{
    ctx.ChoiceController.ResolveChoice(answer);
    try { await AutoProcessing.ForCutIn(ctx).ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* re-parked */ }
}

bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    Zones(ctx).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext ctx, HeadlessEntityId cardId, string key) =>
    ctx.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

bool HasMarker(EngineContext ctx, HeadlessEntityId cardId, string key) =>
    ctx.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.ContainsKey(key);

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"{label}: expected {expected}, got {actual}."); }
