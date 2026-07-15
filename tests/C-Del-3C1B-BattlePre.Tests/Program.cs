// C-Del 3c-1b BATTLE PRE cut-in transport witness (RD-3C1-BATTLE-PRE-WINDOW). The BattleResolver now opens the
// AS-IS "would be deleted" PRE cut-in window (WhenPermanentWouldBeDeleted → WhenRemoveField) over its still-pending
// LOSERS — the battle analogue of the effect-delete sink's 3b/3c-1 work (the battle path never routes through the
// sink). A replacement body that clears willBeRemoveField SPARES the loser (survival). Non-interactive drains
// inline; an INTERACTIVE replacement promotes-to-defer and parks at DeletionReplacement (FinalizeDeferredAsync
// re-enters after the agent resolves the parked ForCutIn choice).
//
// GATE-INVISIBLE fixtures (TfxWouldBeDeleted / …Interactive / …ByBattle): non-keyword-named new-model ActivateClasses
// with no EffectRegistry binding, so HasPreOption(byBattle) is FALSE → they are NOT gate-deferred → the new window is
// the sole firing path. A REAL gate keyword (hasEvade) still parks via the gate — the new window EXCLUDES it
// (HasPreOption true), proving double-fire 0 with the un-retired gate.

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;

var P1 = new HeadlessPlayerId(1);   // attacker
var P2 = new HeadlessPlayerId(2);   // defender

var tests = new (string Name, Func<Task> Body)[]
{
    ("NON-INTERACTIVE SURVIVE: a battle loser with a mandatory would-be-deleted replacement is spared (PRE window opened, willBeRemoveField cleared)", NonInteractiveSurvives),
    ("CONTROL: a plain battle loser (no PRE effect) is trashed normally (false-green guard)", ControlTrashed),
    ("BY-BATTLE CAUSE: a replacement that REQUIRES IsByBattle survives a BATTLE deletion (the byBattle cause is threaded onto the cut-in hashtable)", ByBattleCauseSurvivesBattle),
    ("BY-BATTLE CAUSE (negative): the SAME IsByBattle replacement is NOT saved from an EFFECT deletion (byBattle false) — proves the cause is read, not synthesized", ByBattleCauseNotSavedFromEffect),
    ("MUTUAL BATCH: a tie deletes BOTH — the survivor (Tfx) is spared and the casualty (plain) is trashed in ONE battle finalize (per-card survivor fix)", MutualSurvivorAndCasualty),
    ("MUTUAL BOTH SURVIVE: a tie where both losers have a mandatory replacement — one ResolveAsync opens the PRE window ONCE over both and spares both", MutualBothSurvive),
    ("INTERACTIVE SURVIVE: an optional replacement pauses the drain → ResolveAsync defers; resolving YES then FinalizeDeferredAsync spares the loser", InteractiveSurvivesParkResume),
    ("INTERACTIVE DECLINE: resolving NO leaves willBeRemoveField set → FinalizeDeferredAsync trashes the loser", InteractiveDeclinesParkResume),
    ("RETIRED KEYWORD FIRES VIA WINDOW: a would-be-deleted survive replacement (the window form of the retired Evade keyword) fires through the BattleResolver PRE cut-in window ALONE — NOT gate-deferred (RequiresDeletionReplacement false) — and spares the loser", RetiredKeywordFiresViaWindow),
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
    var attacker = await Place(ctx, P1, "ATK", dp: 9000);
    var defender = await Place(ctx, P2, "TfxWouldBeDeleted", dp: 3000);   // loses the DP battle, then survives via PRE
    Register(ctx, attacker, P1);
    Register(ctx, defender, P2);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);

    AssertTrue(result.IsSuccess, "battle resolved");
    AssertFalse(result.DefenderDeleted, "the defender was spared by the PRE would-be-deleted window");
    AssertTrue(InZone(ctx, P2, ChoiceZone.BattleArea, defender), "the survivor stays on the battle area");
    AssertFalse(InZone(ctx, P2, ChoiceZone.Trash, defender), "the survivor is not in the trash");
    AssertFalse(ReadFlag(ctx, defender, GameFlowProcessor.PendingDeletionKey), "the survivor's pending flag was cleared");
    AssertFalse(ReadFlag(ctx, defender, "willBeRemoveField"), "no stale willBeRemoveField marker on the survivor");
    AssertFalse(ReadFlag(ctx, defender, BattleResolver.BattlePreWindowedKey), "the transient BattlePreWindowed marker was cleared");
}

async Task ControlTrashed()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await Place(ctx, P1, "ATK", dp: 9000);
    var defender = await Place(ctx, P2, "PLAIN", dp: 3000);   // no PRE effect
    Register(ctx, attacker, P1);
    Register(ctx, defender, P2);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);

    AssertTrue(result.IsSuccess && result.DefenderDeleted, "the plain defender was deleted in battle");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, defender), "the plain loser is trashed (no PRE replacement fired)");
    AssertFalse(InZone(ctx, P2, ChoiceZone.BattleArea, defender), "the plain loser left the battle area");
    AssertFalse(ctx.ChoiceController.Current.IsPending, "no interactive choice for a plain loser (false-green guard)");
}

async Task ByBattleCauseSurvivesBattle()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await Place(ctx, P1, "ATK", dp: 9000);
    var defender = await Place(ctx, P2, "TfxWouldBeDeletedByBattle", dp: 3000);   // replacement requires IsByBattle
    Register(ctx, attacker, P1);
    Register(ctx, defender, P2);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);

    AssertFalse(result.DefenderDeleted, "the by-battle replacement fired (IsByBattle true) — the loser survived");
    AssertTrue(InZone(ctx, P2, ChoiceZone.BattleArea, defender), "the by-battle survivor stays on the battle area");
}

async Task ByBattleCauseNotSavedFromEffect()
{
    // The SAME fixture, deleted by an EFFECT (the sink path, 3b): byBattle is FALSE, so IsByBattle-gated CanUse is
    // false and the replacement does NOT fire — the card is trashed. Proves the battle cause is read (dual-read),
    // not universally synthesized.
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var card = await Place(ctx, P1, "TfxWouldBeDeletedByBattle", dp: 3000);
    await Place(ctx, P2, "FOE", dp: 3000);
    Register(ctx, card, P1);

    var sink = new MatchStateMutationSink(ctx.CardInstanceRepository, log: null, ctx.ZoneMover, memory: null, ctx.EffectRegistry, context: ctx);
    sink.Apply(new EffectMutation(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("2:battle:FOE"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.Value }));
    await sink.FlushAsync();
    await new GameFlowProcessor().RunToStableAsync(ctx);

    AssertTrue(InZone(ctx, P1, ChoiceZone.Trash, card), "the by-battle replacement did NOT fire on an effect deletion — the card is trashed");
    AssertFalse(InZone(ctx, P1, ChoiceZone.BattleArea, card), "the card left the battle area (no battle cause = no survival)");
}

async Task MutualSurvivorAndCasualty()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    // Tie (equal DP) deletes BOTH participants in ONE battle finalize.
    var attacker = await Place(ctx, P1, "TfxWouldBeDeleted", dp: 6000);   // survives via PRE
    var defender = await Place(ctx, P2, "PLAIN", dp: 6000);               // no PRE — dies
    Register(ctx, attacker, P1);
    Register(ctx, defender, P2);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);

    AssertFalse(result.AttackerDeleted, "the attacker was spared (per-card survivor fix within the mutual batch)");
    AssertTrue(result.DefenderDeleted, "the plain defender was trashed in the same finalize");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, attacker), "the survivor stays on the battle area");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, defender), "the casualty is trashed");
}

async Task MutualBothSurvive()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await Place(ctx, P1, "TfxWouldBeDeleted", dp: 6000, instance: "1:battle:ATKa");
    var defender = await Place(ctx, P2, "TfxWouldBeDeleted", dp: 6000, instance: "2:battle:DEFa");
    Register(ctx, attacker, P1);
    Register(ctx, defender, P2);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);

    // One ResolveAsync opened the PRE cut-in ONCE over BOTH losers (batch atomicity) and both replacements spared them.
    AssertFalse(result.AttackerDeleted, "attacker spared");
    AssertFalse(result.DefenderDeleted, "defender spared");
    AssertTrue(InZone(ctx, P1, ChoiceZone.BattleArea, attacker), "attacker survivor on the battle area");
    AssertTrue(InZone(ctx, P2, ChoiceZone.BattleArea, defender), "defender survivor on the battle area");
}

async Task InteractiveSurvivesParkResume()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await Place(ctx, P1, "ATK", dp: 9000);
    var defender = await Place(ctx, P2, "TfxWouldBeDeletedInteractive", dp: 3000);
    Register(ctx, attacker, P1);
    Register(ctx, defender, P2);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);

    // The optional PRE replacement paused the cut-in drain — the battle promoted-to-defer and parks.
    AssertTrue(result.RequiresDeletionReplacement, "the interactive replacement deferred the battle (park at DeletionReplacement)");
    AssertTrue(ctx.ChoiceController.Current.IsPending, "the parked ForCutIn window surfaced 'will you use it?'");
    AssertTrue(ReadFlag(ctx, defender, GameFlowProcessor.PendingDeletionKey), "the loser stays pendingDeletion while the choice pends");
    AssertTrue(InZone(ctx, P2, ChoiceZone.BattleArea, defender), "the loser is NOT trashed while the choice pends");

    // Resolve YES (ForCutIn pool resume), then finalize the deferred battle.
    ChoiceRequest optional = ctx.ChoiceController.PendingRequest!;
    await ResolveWindowOptional(ctx, ChoiceResult.Select(optional.Candidates[0].Id));
    AssertFalse(ReadFlag(ctx, defender, "willBeRemoveField"), "the resumed replacement body cleared willBeRemoveField (survives)");

    BattleResolutionResult finalized = await new BattleResolver().FinalizeDeferredAsync(ctx);
    AssertTrue(finalized.IsSuccess && !finalized.DefenderDeleted, "the deferred battle finalized with the loser spared");
    AssertTrue(InZone(ctx, P2, ChoiceZone.BattleArea, defender), "the interactive survivor was spared on the battle area");
    AssertFalse(InZone(ctx, P2, ChoiceZone.Trash, defender), "the survivor is not in the trash");
    AssertFalse(ReadFlag(ctx, defender, GameFlowProcessor.PendingDeletionKey), "the deferral cleared for the survivor");
}

async Task InteractiveDeclinesParkResume()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await Place(ctx, P1, "ATK", dp: 9000);
    var defender = await Place(ctx, P2, "TfxWouldBeDeletedInteractive", dp: 3000);
    Register(ctx, attacker, P1);
    Register(ctx, defender, P2);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);
    AssertTrue(result.RequiresDeletionReplacement, "the interactive replacement deferred the battle");

    // Resolve NO (decline the optional): the body never runs (willBeRemoveField stays set) → the loser dies.
    await ResolveWindowOptional(ctx, ChoiceResult.Skip());
    BattleResolutionResult finalized = await new BattleResolver().FinalizeDeferredAsync(ctx);

    AssertTrue(finalized.IsSuccess && finalized.DefenderDeleted, "declining the replacement lets the battle deletion finalize");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, defender), "the declined loser was trashed");
    AssertFalse(InZone(ctx, P2, ChoiceZone.BattleArea, defender), "the declined loser left the battle area");
    AssertFalse(ReadFlag(ctx, defender, "willBeRemoveField"), "willBeRemoveField was reset before the trash (no stale marker)");
}

async Task RetiredKeywordFiresViaWindow()
{
    EngineContext ctx = NewContext();
    using var scope = AmbientMatchContext.Enter(ctx);
    var attacker = await Place(ctx, P1, "ATK", dp: 9000);
    // 3c-2b flip: the PRE replacement keywords no longer PARK via the invented gate. A window-collectible
    // would-be-deleted survive replacement (the window form of the retired Evade keyword — the printed
    // TfxWouldBeDeleted ActivateClass) is HasPreOption(byBattle) FALSE, so the battle is NOT gate-deferred
    // (RequiresDeletionReplacement false). The keyword now fires through the BattleResolver PRE cut-in window
    // ALONE — a single firing path (no gate/window double-fire), sparing the loser inline.
    var defender = await Place(ctx, P2, "TfxWouldBeDeleted", dp: 3000);
    Register(ctx, attacker, P1);
    Register(ctx, defender, P2);

    ctx.AttackController.DeclareAttack(P1, attacker, P2, defender, isDirectAttack: false);
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);

    AssertFalse(result.RequiresDeletionReplacement, "the retired keyword is NOT gate-deferred (the gate firing half is retired) — no park");
    AssertFalse(result.DefenderDeleted, "the keyword fired through the BattleResolver PRE cut-in window — the loser survived");
    AssertTrue(InZone(ctx, P2, ChoiceZone.BattleArea, defender), "the window-fired survivor stays on the battle area");
    AssertFalse(InZone(ctx, P2, ChoiceZone.Trash, defender), "the survivor is not in the trash");
    AssertFalse(ReadFlag(ctx, defender, BattleResolver.BattlePreWindowedKey), "the transient BattlePreWindowed marker was cleared after the inline drain");
}

// ============================================================ HARNESS

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 3117, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string num, int dp, string? instance = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["level"] = 4 };
    cards.Upsert(new CardRecord(defId, num, num, defMeta, CardType: "Digimon"));
    var id = new HeadlessEntityId(instance ?? $"{owner.Value}:battle:{num}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = dp, ["isSuspended"] = false };
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
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
// provider replays it. Same shape as the C-Del-3C1-Substrate witness.
async Task ResolveWindowOptional(EngineContext ctx, ChoiceResult answer)
{
    ctx.ChoiceController.ResolveChoice(answer);
    try { await AutoProcessing.ForCutIn(ctx).ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* re-parked */ }
}

bool InZone(EngineContext ctx, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext ctx, HeadlessEntityId cardId, string key) =>
    ctx.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
