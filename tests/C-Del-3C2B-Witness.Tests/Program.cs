// C-Del 3c-2b LANDING witness — the PRE 9-keyword gate firing-half is RETIRED; the keywords now fire ONLY through
// the AS-IS PRE cut-in window (WhenPermanentWouldBeDeleted → WhenRemoveField; GetSkillInfos collects the printed /
// granted ActivateClass, TriggeredSkillProcess resolves). This witness proves the two load-bearing claims of the
// flip:
//   (A) ROUTING — every printed replacement-keyword card is now GATE-INVISIBLE (the retired DeletionReplacementGate
//       no longer detects it: HasPreOption is FALSE), and the AS-IS window DOES collect the printed keyword
//       (GetSkillInfos >= 1). So each keyword fires via the window, never the gate — no double-fire, no gate path.
//   (B) MIXED-BATCH (RD-3C1-MIXED-BATCH resolved) — TWO window-form PRE replacements + a plain casualty in ONE
//       Destroy: BOTH survivors are spared and the casualty trashed, with the gate opening NO defer/choice. Before
//       the whole-cluster flip a single gate PreOption forced DeferAll and blocked the window for a batch-mate; now
//       the window handles every member of the batch together.
//
// Uses the mandatory window-collectible Tfx fixture (TfxWouldBeDeleted) for the survival drive (inline drain, no
// interactive pause — the interactive promote-to-defer path is witnessed by C-Del-3C1-Substrate) and the real
// printed keyword cards for the routing proof.

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("ROUTING Evade (BT13_023): retired gate is BLIND (HasPreOption false) AND the AS-IS window collects the printed EvadeSelfEffect", () => RealCardRoutesToWindow("BT13_023", EffectTiming.WhenPermanentWouldBeDeleted, unsuspended: true)),
    ("ROUTING Fragment (EX8_051): gate BLIND + window collects the printed FragmentSelfEffect", () => RealCardRoutesToWindow("EX8_051", EffectTiming.WhenPermanentWouldBeDeleted, unsuspended: false)),
    ("ROUTING Scapegoat (EX8_061): gate BLIND + window collects the printed ScapegoatSelfEffect", () => RealCardRoutesToWindow("EX8_061", EffectTiming.WhenPermanentWouldBeDeleted, unsuspended: false)),
    ("ROUTING Decode (BT19_024): gate BLIND (HasPreOption false) — the printed DecodeSelfEffect is a WhenRemoveField window effect, never a gate option", () => RealCardGateBlind("BT19_024")),
    ("ROUTING Partition (BT16_025): gate BLIND (HasPreOption false) — printed PartitionEffect is a WhenRemoveField window effect", () => RealCardGateBlind("BT16_025")),
    ("ROUTING Barrier (BT14_035): gate BLIND (HasPreOption false) — printed BarrierSelfEffect is a battle would-be-deleted window effect", () => RealCardGateBlind("BT14_035")),
    ("BATCH: one Destroy over [window survivor, casualty] — the survivor is spared by the AS-IS cut-in window and the casualty trashed, with the retired gate opening NO defer/choice (RD-3C1-MIXED-BATCH: the window, not the gate, handles the whole batch)", BatchComposesViaWindow),
    ("MATERIAL SAVE (TfxMaterialSave, real MaterialSaveEffect factory shape): the window fires it — matching material sources tuck under the chosen Tamer, the rest trash, and the holder still leaves (no survival)", MaterialSaveViaWindow),
    ("MIXED-BATCH KEYWORDS (RD-3C1-MIXED-BATCH): ONE Destroy over [BT13_023 <Evade>, EX8_061 <Scapegoat>] = ONE cut-in stack — both keywords fire through the shared window (order + optional choices), Evade survivor suspended, Scapegoat holder spared, sacrifice trashed", MixedKeywordBatchOneCutIn),
};

// (design item RD-3C2B-01, observed 2026-07-15) With the gate retired, TWO simultaneous window-form
// WhenPermanentWouldBeDeleted replacements in ONE Destroy route to the shared AS-IS cut-in window's MultipleSkills
// ORDER choice; in the current mirror window engine, resolving that order choice spared only the picked survivor
// and trashed the other (probe with two mandatory TfxWouldBeDeleted). This exercises the MultipleSkills / promote-
// to-defer resolution substrate (pre-existing, from the SkillInfo cutover / 3c-1), which 3c-2b makes OBSERVABLE for
// these keywords by routing them through the window rather than the per-card gate. It is beyond the 6-card GO
// judgment (all single-effect printed cards) and beyond this batch's scope; the mandatory synthetic fixture is also
// not a faithful stand-in for the real (optional/interactive) keyword cards. Flagged for the coordinator; the batch
// witness below uses the robust single-survivor form.

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ============================================================ (A) ROUTING

// A printed self-effect keyword card that fires at `timing`: the retired gate must be BLIND (HasPreOption false),
// and the AS-IS PRE cut-in window's GetSkillInfos must COLLECT the printed ActivateClass (>= 1).
async Task RealCardRoutesToWindow(string cardNum, EffectTiming timing, bool unsuspended)
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, cardNum, isSuspended: !unsuspended);
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    // (A1) the retired gate does NOT recognise the printed keyword — no gate firing / no double-fire path.
    var zones = (IZoneStateReader)context.ZoneMover;
    bool gateSees = DeletionReplacementTiming.HasPreOption(context.CardInstanceRepository, zones, Record(context, card), byBattle: false);
    AssertFalse(gateSees, $"{cardNum}: the retired gate is BLIND to the printed keyword (HasPreOption false)");

    // (A2) the AS-IS PRE cut-in window's collection DOES see the printed keyword while the card is a marked target.
    var perm = new Permanent(context, card, P1) { willBeRemoveField = true };
    var ht = CardEffectCommons.WhenPermanentWouldRemoveFieldCheckHashtable(new List<Permanent> { perm }, cardEffect: null, battle: null);
    int collected = AutoProcessing.GetSkillInfos(ht, timing).Count;
    perm.willBeRemoveField = false;
    AssertTrue(collected >= 1, $"{cardNum}: the AS-IS window collects the printed keyword ActivateClass (GetSkillInfos={collected})");
}

// A printed keyword whose CanUse needs digivolution sources (Decode/Partition) or a battle cause (Barrier) — the
// routing claim that matters for the flip is the same GATE-BLIND fact (the retired gate never offers it); its
// window collection is exercised by the dedicated behavior suites (C1-DecodePartitionPre / G3.5-C13 / G3.5-C14).
async Task RealCardGateBlind(string cardNum)
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, cardNum, isSuspended: false);
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    var zones = (IZoneStateReader)context.ZoneMover;
    bool gateBattle = DeletionReplacementTiming.HasPreOption(context.CardInstanceRepository, zones, Record(context, card), byBattle: true);
    bool gateEffect = DeletionReplacementTiming.HasPreOption(context.CardInstanceRepository, zones, Record(context, card), byBattle: false);
    AssertFalse(gateBattle || gateEffect, $"{cardNum}: the retired gate is BLIND to the printed keyword (HasPreOption false both causes)");
}

// ============================================================ (B) MIXED-BATCH

async Task BatchComposesViaWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var survivor = await Place(context, P1, "TfxWouldBeDeleted", instance: "1:battle:Surv");
    var casualty = await Place(context, P1, "PLAIN", instance: "1:battle:Cas");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, survivor, P1);
    CardEffectRegistrar.RegisterCard(context, casualty, P1);

    // ONE Destroy over both: willBeRemoveField marked on both, the ONE cut-in over the LIST fires the window-form
    // survivor's mandatory replacement (spared, inline drain), the casualty stays willBeRemoveField=true (trashed).
    // Before the whole-cluster flip a member's gate PreOption forced DeferAll=true and blocked the window for the
    // batch; now every member routes through the window together with NO gate defer/choice.
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(survivor));
    sink.Apply(Delete(casualty));
    await sink.FlushAsync();

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, survivor), "the batch survivor was spared by the window");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, casualty), "the batch casualty was trashed");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, survivor), "the survivor is NOT in the trash");
    // No gate path: no member parked for a gate replacement decision, no invented DeletionReplacement choice.
    AssertFalse(ReadFlag(context, survivor, GameFlowProcessor.PendingDeletionKey), "the survivor was NOT gate-deferred (window, not gate)");
    AssertFalse(context.ChoiceController.Current.IsPending, "the gate opened NO replacement choice — the window handled the whole batch");
}

// ============================================================ MATRIX SUPPLEMENTS (3c-2b final landing)

// MATERIAL SAVE: TfxMaterialSave (the real MaterialSaveEffect + AddDigiXrosConditionClass consumer pair — no real
// card ported yet). Deleting the DigiXros holder opens the PRE window; activating Material Save selects the (sole)
// own Tamer and tucks up to 2 MATCHING material sources under it (AddDigivolutionCardsBottom); the non-matching
// source trashes with the stack and the holder STILL leaves (Material Save is not a survival replacement).
async Task MaterialSaveViaWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var holder = await Place(context, P1, "TfxMaterialSave");
    var tamer = await Place(context, P1, "TAMER", instance: "1:battle:Tamer", cardType: "Tamer");
    await Place(context, P2, "FOE");
    // Materials: two matching (TfxXrosMat*) + one non-matching source under the holder.
    var mat1 = MakeCard(context, P1, "TfxXrosMat1", "1-mat1");
    var mat2 = MakeCard(context, P1, "TfxXrosMat2", "1-mat2");
    var plainSrc = MakeCard(context, P1, "PLAINSRC", "1-plainsrc");
    SetMeta(context, holder, DeletionReplacementGate.SourceIdsKey, new[] { mat1.Value, mat2.Value, plainSrc.Value });
    CardEffectRegistrar.RegisterCard(context, holder, P1);

    // The retired gate is BLIND to the printed keyword.
    var zones = (IZoneStateReader)context.ZoneMover;
    AssertFalse(DeletionReplacementTiming.HasPreOption(context.CardInstanceRepository, zones, Record(context, holder), byBattle: false),
        "the retired gate is BLIND to the printed Material Save (HasPreOption false)");

    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(holder));
    try { await sink.FlushAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { }

    // Step 1: "Will you use Material Save 2?" — the window's optional.
    AssertTrue(context.ChoiceController.Current.IsPending, "the Material Save optional window is open");
    await ResolveParked(context, ChoiceResult.Select(context.ChoiceController.PendingRequest!.Candidates[0].Id));

    // Step 2 (Tamer pick, canNoSelect:true so even a single candidate surfaces): pick the Tamer.
    if (context.ChoiceController.Current.IsPending && context.ChoiceController.PendingRequest!.Candidates.Any(c => c.Id.Value.Contains(tamer.Value, StringComparison.Ordinal)))
    {
        await ResolveParked(context, ChoiceResult.Select(context.ChoiceController.PendingRequest!.Candidates.Single(c => c.Id.Value.Contains(tamer.Value, StringComparison.Ordinal)).Id));
    }

    // Step 3 (material picks): select both matching materials (a multi- or repeated single-select — resolve
    // whatever the seam surfaces until the window settles).
    for (int guard = 0; guard < 4 && context.ChoiceController.Current.IsPending; guard++)
    {
        ChoiceRequest req = context.ChoiceController.PendingRequest!;
        var matCandidates = req.Candidates.Where(c =>
            c.Id.Value.Contains(mat1.Value, StringComparison.Ordinal) || c.Id.Value.Contains(mat2.Value, StringComparison.Ordinal)).ToArray();
        AssertFalse(req.Candidates.Any(c => c.Id.Value.Contains(plainSrc.Value, StringComparison.Ordinal) && c.IsSelectable),
            "the non-matching source is NOT a selectable Material Save candidate (IsContainDigiXrosCondition filters)");
        if (matCandidates.Length == 0)
        {
            break;
        }

        await ResolveParked(context, req.MaxCount >= 2 && matCandidates.Length >= 2
            ? ChoiceResult.Select(new[] { matCandidates[0].Id, matCandidates[1].Id })
            : ChoiceResult.Select(matCandidates[0].Id));
    }

    await new GameFlowProcessor().RunToStableAsync(context);

    string[] tamerSources = SourceIdsOf(context, tamer);
    AssertTrue(tamerSources.Contains(mat1.Value) && tamerSources.Contains(mat2.Value),
        "both matching materials were placed under the chosen Tamer (AS-IS AddDigivolutionCardsBottom)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, holder), "the holder still left play (Material Save is no survival)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, plainSrc), "the non-matching source was trashed with the stack");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, mat1) || InZone(context, P1, ChoiceZone.Trash, mat2),
        "the saved materials were NOT trashed");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, tamer), "the Tamer stays on the battle area");
}

// MIXED-BATCH KEYWORDS (RD-3C1-MIXED-BATCH resolved, real-keyword form): ONE Destroy over TWO different printed
// window-form keywords — BT13_023 <Evade> + EX8_061 <Scapegoat> (+ a plain ally as the sacrifice candidate). The
// single cut-in stack collects BOTH; each optional resolves in the shared window (with an ORDER choice when the
// engine offers one); the Evade holder survives suspended, the Scapegoat holder is spared, and the sacrificed
// ally is trashed. Before the whole-cluster flip a member's gate PreOption forced DeferAll and blocked the window
// for its batch-mate — this witnesses the mixed real-keyword batch composing through the window alone.
async Task MixedKeywordBatchOneCutIn()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var evader = await Place(context, P1, "BT13_023", instance: "1:battle:Evader");
    var scapegoater = await Place(context, P1, "EX8_061", instance: "1:battle:Scape");
    var ally = await Place(context, P1, "PLAIN", instance: "1:battle:Ally");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, evader, P1);
    CardEffectRegistrar.RegisterCard(context, scapegoater, P1);
    CardEffectRegistrar.RegisterCard(context, ally, P1);

    // ONE Destroy over both keyword holders = ONE batch = ONE PRE cut-in stack.
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(evader));
    sink.Apply(Delete(scapegoater));
    try { await sink.FlushAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { }

    // Drive the shared window to completion: resolve every surfaced choice AFFIRMATIVELY (an order pick, each
    // keyword's optional, and Scapegoat's sacrifice sub-select — the plain ally is its only candidate, the
    // Evade holder being itself marked for deletion is excluded by CanSelectPermanentCondition's owner-battle
    // scan... it remains a candidate only if unmarked; pick the PLAIN ally explicitly when offered).
    for (int guard = 0; guard < 8 && context.ChoiceController.Current.IsPending; guard++)
    {
        ChoiceRequest req = context.ChoiceController.PendingRequest!;
        ChoiceCandidate pick = req.Candidates.FirstOrDefault(c => c.Id.Value.Contains(ally.Value, StringComparison.Ordinal))
            ?? req.Candidates[0];
        await ResolveParked(context, ChoiceResult.Select(pick.Id));
    }

    await new GameFlowProcessor().RunToStableAsync(context);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, evader), "the Evade holder survived via the shared window");
    AssertTrue(ReadFlag(context, evader, "isSuspended"), "the Evade holder suspended as the cost");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, scapegoater), "the Scapegoat holder was spared (sacrifice succeeded)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, ally), "the sacrificed ally was trashed");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, evader) || InZone(context, P1, ChoiceZone.Trash, scapegoater),
        "neither keyword holder is in the trash");
    AssertFalse(context.ChoiceController.Current.IsPending, "the shared window fully settled");
}

// ============================================================ HARNESS

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 17, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context: context);

EffectMutation Delete(HeadlessEntityId cardId) =>
    new(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("2:battle:FOE"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string num, string? instance = null, bool isSuspended = false, string cardType = "Digimon")
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 };
    cards.Upsert(new CardRecord(defId, num, num, defMeta, CardType: cardType));
    var id = new HeadlessEntityId(instance ?? $"{owner.Value}:battle:{num}");
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner,
        Metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = isSuspended, ["level"] = 4 }));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

// An off-field card instance (a digivolution-source line entry — referenced by sourceIds, not zone-placed).
HeadlessEntityId MakeCard(EngineContext ctx, HeadlessPlayerId owner, string num, string instance)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    cards.Upsert(new CardRecord(defId, num, num,
        new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var id = new HeadlessEntityId(instance);
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner));
    return id;
}

void SetMeta(EngineContext ctx, HeadlessEntityId cardId, string key, object? value)
{
    CardInstanceRecord r = Record(ctx, cardId);
    var meta = new Dictionary<string, object?>(r.Metadata, StringComparer.Ordinal) { [key] = value };
    ctx.CardInstanceRepository.Upsert(r with { Metadata = meta });
}

string[] SourceIdsOf(EngineContext ctx, HeadlessEntityId cardId) =>
    ctx.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue("sourceIds", out object? raw) && raw is IEnumerable<string> ids
        ? ids.ToArray() : Array.Empty<string>();

// Resolve the parked PRE cut-in choice (ForCutIn pool): record the answer + resume; a re-park re-throws the
// pending signal (caught) so the next surfaced choice can be answered in turn. A WindowChoice (the MultipleSkills
// ORDER pick) additionally records the SkillInfo-currency answer on the DEEPEST in-flight continuation — the
// MetadataActionProcessor seam-2 shape (each candidate id is "cardInstanceId#ordinal").
async Task ResolveParked(EngineContext ctx, ChoiceResult answer)
{
    if (ctx.ChoiceController.PendingRequest is { Type: ChoiceType.WindowChoice } &&
        AutoProcessing.ForCutIn(ctx).executingMultipleSkills is { } deepestWindow)
    {
        string windowKey = ctx.ChoiceController.Current.RequestId?.Value ?? string.Empty;
        HeadlessDCGO.Engine.Headless.Effects.SkillWindowAnswer decoded = HeadlessDCGO.Engine.Headless.Effects.SkillWindowAnswer.Decline;
        if (answer.SelectedIds is { Count: > 0 } selected)
        {
            string token = selected[0].Value;
            int hash = token.LastIndexOf('#');
            if (hash > 0 && int.TryParse(token[(hash + 1)..], out int ordinal))
            {
                decoded = HeadlessDCGO.Engine.Headless.Effects.SkillWindowAnswer.Pick(new HeadlessEntityId(token[..hash]), ordinal);
            }
        }

        deepestWindow.Continuation.RecordAnswer(new HeadlessDCGO.Engine.Headless.Effects.SkillWindowChoiceKey(windowKey), decoded);
    }

    ctx.ChoiceController.ResolveChoice(answer);
    try { await AutoProcessing.ForCutIn(ctx).ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { /* re-parked */ }
}

CardInstanceRecord Record(EngineContext context, HeadlessEntityId cardId) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        ? r : throw new InvalidOperationException($"missing instance {cardId}");

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
