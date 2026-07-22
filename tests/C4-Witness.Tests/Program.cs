// C-4 (P1-10) WITNESS — the REAL card BT9_081 (Purple, Dorugoramon line) proves the battle knock-out window
// resolves the deleted card's [On Deletion] AFTER its own digivolution sources + top card are trashed
// (post-trash), matching AS-IS DestroyPermanentsClass.Destroy (stack OnDestroyedAnyone -> DiscardEvoRoots +
// AddTrashCard -> resolve, CardController.cs:3736/3846/3852).
//
// BT9_081 [On Deletion]: "play 1 purple/black level-3 Digimon from trash for free; if you have 5+ cards with
// [Dex] or [DeathX] in their names in your trash, play 1 [DeathXmon] from trash for free INSTEAD." The 5+ count
// is read LIVE when the [On Deletion] play choice is built. When BT9_081 dies by battle, its own [Dex]/[DeathX]
// digivolution sources (and top) enter the trash BEFORE that window resolves — so the DeathXmon branch must be
// reachable only if the count is POST-trash. A pre-trash count would under-count and never offer DeathXmon.
//
// The discriminator: after the battle + drive-to-stable, does the [On Deletion] play choice list DeathXmon as a
// candidate? Post-trash (correct) => yes at count 5; pre-trash (bug) => no (only the normal purple/black lv3).
using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

HeadlessPlayerId P1 = new(1); // turn player / attacker
HeadlessPlayerId P2 = new(2); // BT9_081 owner (defender that dies)

var tests = new (string Name, Func<Task> Body)[]
{
    ("(1) POST-trash count 5 (4 pre + 1 own source) => [On Deletion] offers DeathXmon AND plays it", PostTrashOffersDeathXmon),
    ("(2) POST-trash count 4 (<5) => DeathXmon NOT offered; the normal purple/black lv3 branch plays instead", UnderFiveNormalBranch),
    ("(3) boundary: the ONLY thing crossing 4->5 is BT9_081's own trashed source (pre-trash would read 4)", BoundaryIsOwnSource),
    // (C-3 재상환 P1-D) [When Digivolving] OR-gate lives in the PER-PASS half (AS-IS CanActivateCondition,
    // BT9_081.cs:52-71): the board half re-reads the LIVE stack each pass; the event half (from-trash root) is
    // latched at collect and overwritten by every later collect.
    ("(4) [When Digivolving] per-pass: collect stacks with OR false; adding a Dorugoramon source MID-WINDOW flips the gate", WhenDigivolvingPerPassBoardHalf),
    ("(5) [When Digivolving] latch: digivolving FROM THE TRASH activates with no Dorugoramon source", WhenDigivolvingFromTrashLatch),
    ("(6) [When Digivolving] latch overwrite: a later hand-root collect clears the stale from-trash latch", WhenDigivolvingLatchOverwrite),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- Tests ---------------------------------------------------------------

async Task PostTrashOffersDeathXmon()
{
    // 4 pre-existing [Dex]/[DeathX] in P2's trash (DeathXmon + 3 fillers) + BT9_081's 1 [Dex] source = 5 post-trash.
    EngineContext ctx = await Battlefield(preExistingDexFillers: 3);
    HeadlessEntityId deathXmon = new("2:trash:DEATHXMON");

    await RunBattle(ctx);
    HeadlessChoiceState choice = await AdvanceToChoice(ctx);

    AssertTrue(choice.IsPending, "the [On Deletion] play choice opened after the battle");
    IReadOnlyList<HeadlessEntityId> offered = OfferedIds(ctx);
    AssertTrue(offered.Contains(deathXmon),
        $"DeathXmon is offered (post-trash count 5); offered = [{string.Join(", ", offered.Select(c => c.Value))}]. " +
        "A pre-trash count (4) would omit it.");

    // Complete the play (pick DeathXmon) and confirm it lands on the battle area — the AS-IS "instead" branch.
    await DrivePreferring(ctx, deathXmon);
    AssertTrue(InZone(ctx, P2, ChoiceZone.BattleArea, deathXmon), "DeathXmon was played from trash to the battle area");
}

async Task UnderFiveNormalBranch()
{
    // 3 pre-existing [Dex]/[DeathX] (DeathXmon + 2 fillers) + BT9_081's 1 source = 4 post-trash (< 5).
    EngineContext ctx = await Battlefield(preExistingDexFillers: 2);
    HeadlessEntityId deathXmon = new("2:trash:DEATHXMON");
    HeadlessEntityId purpleLv3 = new("2:trash:PURPLELV3");

    await RunBattle(ctx);
    HeadlessChoiceState choice = await AdvanceToChoice(ctx);

    AssertTrue(choice.IsPending, "the [On Deletion] play choice opened (the effect still fires)");
    IReadOnlyList<HeadlessEntityId> offered = OfferedIds(ctx);
    AssertTrue(!offered.Contains(deathXmon),
        "DeathXmon is NOT offered (post-trash count 4 < 5) — the [Dex]/[DeathX] branch stays closed");
    AssertTrue(offered.Contains(purpleLv3),
        "the normal purple/black level-3 branch offers the purple lv3 Digimon");

    await DrivePreferring(ctx, purpleLv3);
    AssertTrue(InZone(ctx, P2, ChoiceZone.BattleArea, purpleLv3), "the normal branch played the purple lv3 Digimon");
    AssertTrue(!InZone(ctx, P2, ChoiceZone.BattleArea, deathXmon), "DeathXmon stayed in the trash");
}

async Task BoundaryIsOwnSource()
{
    // Same as (1) — 4 pre-existing + own source = 5 — but also assert the own source + top ARE in the trash at
    // choice time (proving the count includes BT9_081's own just-trashed cards, i.e. the window is post-trash).
    EngineContext ctx = await Battlefield(preExistingDexFillers: 3);
    HeadlessEntityId dexSource = new("2:src:DEXSOURCE");
    HeadlessEntityId bt9 = new("2:battle:BT9_081");

    await RunBattle(ctx);
    _ = await AdvanceToChoice(ctx);

    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, dexSource),
        "BT9_081's own [Dex] digivolution source is in the trash when the [On Deletion] window resolves (this is the 5th count)");
    AssertTrue(InZone(ctx, P2, ChoiceZone.Trash, bt9),
        "BT9_081's top card is in the trash when the window resolves");
}

// --- (C-3 재상환 P1-D) [When Digivolving] gate-half tests ------------------

// (4) (uniform-사멸 flip re-target) AS-IS gate split on the NEW-model ActivateClass: CanUseCondition =
// CanTriggerWhenDigivolving ONLY — a skill whose OR (Dorugoramon-in-sources / from-trash) is FALSE at collect
// still collects (CanTrigger true), and the board half re-reads the live stack per pass (CanActivate over the
// SAME hashtable): a Dorugoramon source added inside the window flips CanActivate to true.
async Task WhenDigivolvingPerPassBoardHalf()
{
    (EngineContext ctx, HeadlessEntityId bt9, ICardEffect eff) = await WhenDigivolvingSetup();
    using var scope = AmbientMatchContext.Enter(ctx);

    AssertTrue(eff.CanTrigger(DigivolveHashtable(ctx, bt9, SelectCardEffect.Root.Hand)),
        "collect half (CanTrigger/CanUseCondition) passes on a hand-root digivolve even with the OR false");
    AssertTrue(!eff.CanActivate(DigivolveHashtable(ctx, bt9, SelectCardEffect.Root.Hand)),
        "per-pass half is false: no [Dorugoramon] source, not from trash");

    // Mid-window board change: tuck a card named exactly [Dorugoramon] under BT9_081.
    HeadlessEntityId doru = Loose(ctx, P2, "src:DORU", "Dorugoramon", colors: new[] { "Purple" }, level: 6, type: "Digimon");
    SetSources(ctx, bt9, doru);

    AssertTrue(eff.CanActivate(DigivolveHashtable(ctx, bt9, SelectCardEffect.Root.Hand)),
        "per-pass half flips TRUE after the source appears — the stack is re-read per pass, not latched at collect");
}

// (5) The event half: digivolving from the trash satisfies the OR with no Dorugoramon source — AS-IS both
// halves read the driving hashtable per pass (the invented from-trash LATCH was dropped with the uniform model:
// BT9_081's CanActivateCondition evaluates CanTriggerWhenDigivolving(hashtable, RootCondition) directly).
async Task WhenDigivolvingFromTrashLatch()
{
    (EngineContext ctx, HeadlessEntityId bt9, ICardEffect eff) = await WhenDigivolvingSetup();
    using var scope = AmbientMatchContext.Enter(ctx);

    AssertTrue(eff.CanTrigger(DigivolveHashtable(ctx, bt9, SelectCardEffect.Root.Trash)), "collect half passes on a trash-root digivolve");
    AssertTrue(eff.CanActivate(DigivolveHashtable(ctx, bt9, SelectCardEffect.Root.Trash)),
        "per-pass half is TRUE via the from-trash root in the driving hashtable (no [Dorugoramon] source needed)");
}

// (6) No cross-window contamination: a trash-root pass being TRUE must not leak into a later hand-root pass —
// AS-IS each pass reads its OWN driving hashtable, so the hand-root re-check is FALSE (the property the old
// invented latch-overwrite case pinned, expressed on the AS-IS per-pass surface).
async Task WhenDigivolvingLatchOverwrite()
{
    (EngineContext ctx, HeadlessEntityId bt9, ICardEffect eff) = await WhenDigivolvingSetup();
    using var scope = AmbientMatchContext.Enter(ctx);

    AssertTrue(eff.CanTrigger(DigivolveHashtable(ctx, bt9, SelectCardEffect.Root.Trash)), "first window: trash root");
    AssertTrue(eff.CanActivate(DigivolveHashtable(ctx, bt9, SelectCardEffect.Root.Trash)), "first window: TRUE via the trash root");

    AssertTrue(eff.CanTrigger(DigivolveHashtable(ctx, bt9, SelectCardEffect.Root.Hand)), "second window: hand root re-collects");
    AssertTrue(!eff.CanActivate(DigivolveHashtable(ctx, bt9, SelectCardEffect.Root.Hand)), "second window: the hand-root pass reads FALSE (no stale from-trash carry-over)");
}

// Battle-area BT9_081 (P2) + one enemy Digimon (P1, the lowest-level delete target) and the card's
// [When Digivolving] NEW-model ActivateClass, built exactly as the resolver dispatches it.
async Task<(EngineContext Ctx, HeadlessEntityId Bt9, ICardEffect Effect)> WhenDigivolvingSetup()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 48);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // ICardEffect.CanTrigger gates on DoneStartGame (phase past None/Setup) — advance to Main.
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    await Combatant(ctx, P1, "ENEMY", dp: 5000, suspended: false, number: "ENEMY", name: "EnemyDigimon", colors: new[] { "Red" }, level: 4);
    HeadlessEntityId bt9 = await Combatant(ctx, P2, "BT9_081", dp: 6000, suspended: false,
        number: "BT9_081", name: "DoruBattlemon", colors: new[] { "Purple" }, level: 6);

    var card = new CardSource(ctx, bt9, P2, P2);
    using var scope = AmbientMatchContext.Enter(ctx);
    ICardEffect eff = new HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT9.Purple.BT9_081()
        .CardEffects(EffectTiming.OnEnterFieldAnyone, card)
        .OfType<ActivateClass>()
        .Single();
    return (ctx, bt9, eff);
}

// A [When Digivolving] driving hashtable exactly as the AS-IS emit threads it (HashtableSetting
// WhenDigivolvingCheckHashtable shape): isEvolution + per-entry {Permanent, Root}.
Hashtable DigivolveHashtable(EngineContext ctx, HeadlessEntityId subject, SelectCardEffect.Root root)
{
    return new Hashtable()
    {
        { "isEvolution", true },
        {
            "hashtables", new List<Hashtable>()
            {
                new Hashtable()
                {
                    { "Permanent", new Permanent(ctx, subject, P2) },
                    { "Root", root },
                }
            }
        },
    };
}

// --- Battle drive --------------------------------------------------------

async Task RunBattle(EngineContext ctx)
{
    ctx.AttackController.DeclareAttack(P1, new HeadlessEntityId("1:battle:ATK"), P2, new HeadlessEntityId("2:battle:BT9_081"));
    BattleResolutionResult result = await new BattleResolver().ResolveAsync(ctx);
    AssertTrue(result.IsSuccess && result.DefenderDeleted && !result.AttackerDeleted, "the defender (BT9_081) was deleted by battle");
}

// Drive RunToStable to the point where the [On Deletion] PLAY choice surfaces (or the pipeline settles). With
// deferredChoice, BT9_081's [On Deletion] is a "You may…" optional effect: it first surfaces its OptionalEffect
// yes/no prompt (AS-IS Activate_Optional), and only on ACCEPT does the interactive select-and-play body suspend
// and land its Card-select request. Accept the optional prompt(s) so the select-and-play (the choice this
// witness inspects) surfaces — post-trash, since the battle finalize already trashed the loser's sources + top.
async Task<HeadlessChoiceState> AdvanceToChoice(EngineContext ctx)
{
    var processor = new MetadataActionProcessor();
    await new GameFlowProcessor().RunToStableAsync(ctx);
    for (int guard = 0; guard < 8; guard++)
    {
        HeadlessChoiceState current = ctx.ChoiceController.Current;
        if (!current.IsPending || ctx.ChoiceController.PendingRequest!.Type != ChoiceType.OptionalEffect)
        {
            break;
        }

        // Accept the "you may" prompt (the non-skip candidate = the effect holder) so the play window opens.
        ChoiceResult accept = current.CandidateIds.Count > 0
            ? ChoiceResult.Select(current.CandidateIds[0])
            : ChoiceResult.Skip();
        await processor.ProcessAsync(HeadlessActionFactory.ResolveChoice(current.PlayerId!.Value, accept), ctx);
        await new GameFlowProcessor().RunToStableAsync(ctx);
    }

    return ctx.ChoiceController.Current;
}

// Answer every pending choice, PREFERRING `prefer` when it is a candidate; else the first candidate; else skip.
async Task DrivePreferring(EngineContext ctx, HeadlessEntityId prefer)
{
    var processor = new MetadataActionProcessor();
    for (int guard = 0; guard < 200; guard++)
    {
        HeadlessChoiceState choice = ctx.ChoiceController.Current;
        if (choice.IsPending)
        {
            ChoiceResult result;
            if (choice.CandidateIds.Contains(prefer))
            {
                result = ChoiceResult.Select(prefer);
            }
            else if (choice.CandidateIds.Count > 0)
            {
                result = ChoiceResult.Select(choice.CandidateIds[0]);
            }
            else
            {
                result = ChoiceResult.Skip();
            }

            await processor.ProcessAsync(
                HeadlessActionFactory.ResolveChoice(choice.PlayerId!.Value, result), ctx);
            continue;
        }

        await new GameFlowProcessor().RunToStableAsync(ctx);
        if (!ctx.ChoiceController.Current.IsPending)
        {
            return;
        }
    }

    throw new Exception("drive did not complete within the guard bound");
}

// --- Setup ---------------------------------------------------------------

async Task<EngineContext> Battlefield(int preExistingDexFillers)
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 47, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    // BT9_081's [On Deletion] ActivateClass gates on DoneStartGame (mirror proxy: phase past None/Setup) via
    // ICardEffect.CanTrigger — the OnDestroyedAnyone window collects it only once the game is underway. A battle
    // happens in the Main phase; advance past setup (matching WhenDigivolvingSetup and the sibling suites).
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    ctx.MemoryController.Set(10); // plenty; the [On Deletion] play is free (payCost:false) regardless.

    // P1 attacker — high DP, unsuspended, so it deletes the defender and survives.
    await Combatant(ctx, P1, "ATK", dp: 10000, suspended: false, number: "ATK", name: "Attacker", colors: null, level: 6);

    // P2 defender = BT9_081 (dp 3000, suspended so it can be attacked). Its printed name carries NO Dex/DeathX,
    // so only its SOURCE contributes to the count (keeps the 4->5 boundary crisp). Dispatch keys off CardNumber.
    HeadlessEntityId bt9 = await Combatant(ctx, P2, "BT9_081", dp: 3000, suspended: true,
        number: "BT9_081", name: "DoruBattlemon", colors: new[] { "Purple" }, level: 6);

    // BT9_081's single digivolution source — a [Dex]-named card. It is trashed (DiscardEvoRoots) when BT9_081
    // dies, becoming the 5th [Dex]/[DeathX] card in the trash.
    HeadlessEntityId dexSource = Loose(ctx, P2, "src:DEXSOURCE", "DexDorugamon", colors: new[] { "Purple" }, level: 4, type: "Digimon");
    SetSources(ctx, bt9, dexSource);

    // Pre-seed P2's trash.
    //   DeathXmon — the "instead" target (name contains DeathX; level 5 so it does NOT match the normal lv3 branch).
    await Trash(ctx, P2, "trash:DEATHXMON", "DeathXmon", colors: new[] { "Purple" }, level: 5, type: "Digimon");
    //   A purple level-3 Digimon — the NORMAL branch candidate (no Dex/DeathX in its name).
    await Trash(ctx, P2, "trash:PURPLELV3", "PurpleImp", colors: new[] { "Purple" }, level: 3, type: "Digimon");
    //   [Dex]/[DeathX] fillers (level 6 -> not normal-branch candidates; names carry Dex/DeathX -> counted).
    for (int i = 0; i < preExistingDexFillers; i++)
    {
        string nm = i % 2 == 0 ? $"DexFiller{i}" : $"DeathXFiller{i}";
        await Trash(ctx, P2, $"trash:FILLER{i}", nm, colors: new[] { "Black" }, level: 6, type: "Digimon");
    }

    return ctx;
}

async Task<HeadlessEntityId> Combatant(
    EngineContext ctx, HeadlessPlayerId owner, string tag, int dp, bool suspended,
    string number, string name, string[]? colors, int level)
{
    Def(ctx, $"DEF:{owner.Value}:{tag}", number, name, colors, level, dp, "Digimon");
    var id = new HeadlessEntityId($"{owner.Value}:battle:{tag}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["isSuspended"] = suspended,
        [BattleResolver.DpKey] = dp,
    };
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{owner.Value}:{tag}"), owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// A loose card instance (no zone) referenced as a digivolution source; trashed on the host's deletion.
HeadlessEntityId Loose(EngineContext ctx, HeadlessPlayerId owner, string tag, string name, string[] colors, int level, string type)
{
    Def(ctx, $"DEF:{owner.Value}:{tag}", $"{owner.Value}:{tag}", name, colors, level, dp: null, type);
    var id = new HeadlessEntityId($"{owner.Value}:{tag}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId($"DEF:{owner.Value}:{tag}"), owner, Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)));
    return id;
}

async Task<HeadlessEntityId> Trash(EngineContext ctx, HeadlessPlayerId owner, string tag, string name, string[] colors, int level, string type)
{
    HeadlessEntityId id = Loose(ctx, owner, tag, name, colors, level, type);
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.Trash));
    return id;
}

void Def(EngineContext ctx, string defId, string number, string name, string[]? colors, int level, int? dp, string type)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["level"] = level };
    if (colors is not null) meta["colors"] = colors;
    if (dp is int d) meta["dp"] = d;
    cards.Upsert(new CardRecord(new HeadlessEntityId(defId), number, name, meta, CardType: type));
}

void SetSources(EngineContext ctx, HeadlessEntityId host, params HeadlessEntityId[] sources)
{
    CardInstanceRecord h = ctx.CardInstanceRepository.TryGetInstance(host, out var r) && r is not null ? r : throw new Exception($"missing {host}");
    ctx.CardInstanceRepository.Upsert(h with
    {
        Metadata = new Dictionary<string, object?>(h.Metadata, StringComparer.Ordinal) { ["sourceIds"] = sources.Select(s => s.Value).ToArray() }
    });
}

bool InZone(EngineContext ctx, HeadlessPlayerId owner, ChoiceZone zone, HeadlessEntityId id) =>
    ((IZoneStateReader)ctx.ZoneMover).GetCards(owner, zone).Contains(id);

// The cards a select-and-play actually OFFERS (can be picked) = the SelectCardEffect's IsSelectable subset (AS-IS
// CanSelectCard / the card's canTargetCondition, which reads the LIVE post-trash [Dex]/[DeathX] count). The request
// also carries the whole display pool (all trash cards) as non-selectable candidates, so "offered" = selectable.
IReadOnlyList<HeadlessEntityId> OfferedIds(EngineContext ctx) =>
    ctx.ChoiceController.PendingRequest is { } req
        ? req.SelectableCandidates.Select(c => c.Id).ToList()
        : new List<HeadlessEntityId>();

void AssertTrue(bool cond, string what)
{
    if (!cond) throw new Exception($"expected true: {what}");
}
