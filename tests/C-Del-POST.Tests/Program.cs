// C-Del 3a POST-cluster witness — [Fortitude] / [Save] / [Ascension] deletion-response keywords RE-HOUSED to
// (or STOP-documented against) the AS-IS OnDestroyedAnyone cut-in window.
//
// CONTEXT (keyword_rehoming_design_2026-07-15.md §F.3a / cdel_wave3_investigation_2026-07-15.md): the POST
// deletion-response keywords fired through the INVENTED DeletionReplacementGate (TryFortitudeReplayAsync /
// TryAscensionAsync / Timing SaveOption). The universal effect-delete path (MatchStateMutationSink) already
// opens the AS-IS OnDestroyedAnyone cut-in window (StackSkillInfos collect-before-removal + FlushAsync drain),
// so a printed KEffect ActivateClass would DOUBLE-FIRE (gate + window). This batch retires the gate FIRING half
// and lets the window be the sole path — keyword by keyword, atomically.
//
// WITNESS-ADJUDICATED SPLIT (empirical, this file):
//   * Fortitude — window FIRES it (CanActivateFortitude = IsExistOnTrash + >=1 source, no
//                 PermanentJustBeforeRemoveField). Gate replay RETIRED (sink/battle/gameflow/security).
//   * Save      — window FIRES it (CanActivateSave = IsTopCardInTrashOnDeletion + a Tamer target). Gate
//                 SaveOption RETIRED.
//   * Ascension — (C-Del 3c-3, 2026-07-16) window FIRES it. RD-P6C3-A3 RESOLVED: the universal sink / battle /
//                 security deletion paths now CHARGE the PermanentJustBeforeRemoveField service store (AS-IS
//                 CardController.cs:3781), so CanActivateAscension -> CanActivateOnDeletion's identity gate
//                 (card.PermanentJustBeforeRemoveField == TopCard.PermanentJustBeforeRemoveField) is satisfied
//                 for the self-deleted card. Gate TryAscensionAsync / Timing AscensionOption RETIRED (atomic with
//                 the store charge, else double-fire). isOptional=false quirk (Commons/Ascension.cs:12) preserved:
//                 the window forces the effect (no "Will you use Ascension?" optional) straight into
//                 AscensionProcess's own "Will you place this card in security?" yes/no. Real cards BT25_034 /
//                 BT25_040 print this shape ([OnDestroyedAnyone] -> AscensionSelfEffect); TfxAscension mirrors it.
//
// Harness: EngineContext.CreateDefault(deferredChoice:true) + TurnController(P1,P2) + Main phase (DoneStartGame),
// under AmbientMatchContext.Enter. Consumers are Tfx fixtures (no real card ports these keywords: 미러 소비 0).

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

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

var tests = new (string Name, Func<Task> Body)[]
{
    // --- Fortitude ---
    ("Fortitude PRINTED: a deleted Digimon with a source replays from the trash via the OnDestroyedAnyone window", FortitudePrintedReplaysViaWindow),
    ("Fortitude: the retired gate (bare hasFortitude metadata, no printed effect) does NOT replay (single-fire)", FortitudeGateRetired),
    ("Fortitude GRANTED: GainFortitude stores an EvadeEffect (AS-IS quirk, NOT Fortitude) in the OnDestroyedAnyone bucket", FortitudeGrantedQuirk),
    ("Fortitude CONTROL: a plain Digimon (no Fortitude) is not replayed (false-green guard)", FortitudeControlStaysTrashed),
    ("Fortitude BATCH: two source-holders deleted in ONE Destroy sequence collect into ONE window (atomicity)", FortitudeBatchAtomicity),
    // --- Save ---
    ("Save PRINTED: a deleted card fires the OnDestroyedAnyone window optional and is placed under the chosen Tamer", SavePrintedFiresViaWindow),
    ("Save: the retired gate (bare hasSave metadata, no printed effect) opens NO POST option (single-fire)", SaveGateRetired),
    ("Save CONTROL: a plain deleted card opens no Save window (false-green guard)", SaveControlStaysTrashed),
    // --- Ascension (C-Del 3c-3: window fires it; RD-P6C3-A3 resolved, gate retired) ---
    ("Ascension PRINTED: fires through the OnDestroyedAnyone window (store charged) and is placed in security; isOptional=false quirk", AscensionPrintedFiresViaWindow),
    ("Ascension CONTROL: a plain deleted card (no [Ascension]) opens no window (false-green guard)", AscensionControlStaysTrashed),
    ("Ascension IDENTITY GATE: an on-field [Ascension] holder does NOT react to a DIFFERENT card's deletion (PermanentJustBeforeRemoveField mismatch)", AscensionIdentityGate),
};

var failures = new List<string>();
foreach (var t in tests)
{
    try { await t.Body(); Console.WriteLine($"PASS {t.Name}"); }
    catch (Exception ex) { failures.Add(t.Name); Console.Error.WriteLine($"FAIL {t.Name}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
}
if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// ============================================================ FORTITUDE

async Task FortitudePrintedReplaysViaWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    await PlaceNone(context, P1, "P1-Src");
    var card = await Place(context, P1, "TfxFortitude", sources: new[] { "P1-Src" });
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);
    AssertEqual(1, new Permanent(context, card, P1).DigivolutionCards.Count, "the Fortitude card has 1 digivolution source");

    await DeleteAndDrive(context, card);

    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, card), "the Fortitude card replayed onto the battle area (window path)");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, card), "the Fortitude card is no longer in the trash");
    // Single-fire proof: the replay came from the WINDOW (FortitudeProcess), NOT the retired gate — the gate
    // stamped a fortitudeReplayed marker; the window path does not.
    AssertFalse(ReadFlag(context, card, DeletionReplacementGate.FortitudeReplayedKey),
        "the replay was NOT done by the retired gate (no fortitudeReplayed marker) — single-fire via the window");
}

async Task FortitudeGateRetired()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    await PlaceNone(context, P1, "P1-Src");
    // A bare hasFortitude METADATA marker (the invented gate's trigger) with NO printed FortitudeEffect: the
    // retired gate no longer replays it, and there is nothing for the window to collect.
    var card = await Place(context, P1, "PLAIN", sources: new[] { "P1-Src" }, flags: (DeletionReplacementGate.HasFortitudeKey, true));
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndDrive(context, card);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, card), "a bare hasFortitude marker no longer replays (gate retired) — card stays trashed");
    AssertFalse(InZone(context, P1, ChoiceZone.BattleArea, card), "the card left the battle area");
}

async Task FortitudeGrantedQuirk()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var host = await Place(context, P1, "PLAIN");
    CardEffectRegistrar.RegisterCard(context, host, P1);

    ICardEffect grantSource = GrantSource(context, host, "GrantFortitude");
    await CardEffectCommons.GainFortitude(new Permanent(context, host), EffectDuration.UntilOwnerTurnEnd, grantSource);

    // AS-IS QUIRK (Fortitude.cs:89-91) preserved VERBATIM: GainFortitude stores an EvadeEffect — NOT a Fortitude
    // effect — in the target's OnDestroyedAnyone duration bucket (a copy/paste bug). We assert the bucket carries
    // exactly one effect and its name is "Evade" (not "Fortitude"): the quirk is mirrored, not corrected.
    var bucket = new Permanent(context, host).EffectList(EffectTiming.OnDestroyedAnyone);
    AssertEqual(1, bucket.Count, "GainFortitude stored exactly one effect in the OnDestroyedAnyone bucket");
    AssertEqual("Evade", bucket[0].EffectName, "the quirk stored an EvadeEffect (NOT a Fortitude effect) — AS-IS verbatim");
}

async Task FortitudeControlStaysTrashed()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    await PlaceNone(context, P1, "P1-Src");
    var card = await Place(context, P1, "PLAIN", sources: new[] { "P1-Src" });
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndDrive(context, card);

    AssertTrue(InZone(context, P1, ChoiceZone.Trash, card), "a plain Digimon (no Fortitude) is not replayed (false-green guard)");
}

async Task FortitudeBatchAtomicity()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    await PlaceNone(context, P1, "P1-SrcA");
    await PlaceNone(context, P1, "P1-SrcB");
    var a = await Place(context, P1, "TfxFortitude", instance: "1:battle:FortA", sources: new[] { "P1-SrcA" });
    var b = await Place(context, P1, "TfxFortitude", instance: "1:battle:FortB", sources: new[] { "P1-SrcB" });
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, a, P1);
    CardEffectRegistrar.RegisterCard(context, b, P1);

    // ONE Destroy sequence = ONE batch = ONE window drive (uncapped fixture — C-group atomicity lesson).
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(a));
    sink.Apply(Delete(b));
    await sink.FlushAsync();

    // ATOMICITY: the two deletions are ONE Destroy sequence -> ONE OnDestroyedAnyone window collecting BOTH
    // Fortitude reactors, so the window parks on a SINGLE MultipleSkills order choice offering exactly the two
    // (AS-IS "Multiple effects are triggered. Choose which to resolve first."). Two SEPARATE drives (a cap/split
    // bug) would surface them in unrelated windows with no shared order choice. (Uncapped fixture — C-group
    // atomicity lesson. The full dual-replay resolution rides the shared MultipleSkills ordering substrate,
    // exercised elsewhere; here we witness the single-window collection that IS the atomicity claim.)
    AssertTrue(context.ChoiceController.Current.IsPending, "the single batch window parked on the multi-skill order choice");
    ChoiceRequest order = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.WindowChoice, order.Type, "one window opened the AS-IS multi-effect order choice");
    AssertEqual(2, order.Candidates.Count, "the ONE window collected BOTH batch members' Fortitude reactors (single stack — atomicity)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, a) && InZone(context, P1, ChoiceZone.Trash, b),
        "both members reached the trash together before the shared window resolves (collect-before-removal)");

    // (P2-2 upgrade) Drive the shared window to its TERMINAL state: resolve the order pick and every follow-up,
    // resuming the parked MultipleSkills chain until it settles. AS-IS resolves the two collected reactors one at a
    // time (the "resolve which first" order), each FortitudeProcess replaying its holder from the trash back to the
    // battle area. Both members must end REVIVED — the atomicity claim proven all the way to both placements, not
    // just the single-window collection.
    var processor = new MetadataActionProcessor();
    for (int guard = 0; guard < 32 && context.ChoiceController.Current.IsPending; guard++)
    {
        HeadlessChoiceState choice = context.ChoiceController.Current;
        if (choice.IsPending && choice.Type == ChoiceType.WindowChoice)
        {
            await processor.ProcessAsync(
                HeadlessActionFactory.ResolveChoice(choice.PlayerId!.Value, ChoiceResult.Select(choice.CandidateIds[0])), context);
            continue;
        }

        await new GameFlowProcessor().RunToStableAsync(context);
    }

    AssertFalse(context.ChoiceController.Current.IsPending, "both Fortitude reactors resolved — the shared window settled");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, a), "batch member A revived to the battle area (Fortitude replay)");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, b), "batch member B revived to the battle area (Fortitude replay)");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, a), "member A is no longer in the trash (both revivals completed)");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, b), "member B is no longer in the trash (both revivals completed)");
}

// ============================================================ SAVE

async Task SavePrintedFiresViaWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var tamer = await Place(context, P1, "P1-Tamer", cardType: "Tamer");
    var card = await Place(context, P1, "TfxSave");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    // Delete: the window opens the AS-IS optional "Will you use Save?" (isOptional=true) and parks.
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(card));
    await sink.FlushAsync();

    AssertTrue(context.ChoiceController.Current.IsPending, "the OnDestroyedAnyone window parked on the Save optional");
    ChoiceRequest opt = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.OptionalEffect, opt.Type, "the window opened the AS-IS Save optional (MultipleSkills, NOT the retired gate)");
    AssertTrue(opt.Message.Contains("Save", StringComparison.Ordinal), "the optional names Save");

    // Answer "yes" -> SaveProcess -> SelectPermanentEffect offering the Tamer.
    ChoiceRequest select = await AnswerYesAndResume(context, opt);
    AssertTrue(select.Candidates.Any(c => c.Id == tamer || c.Label.Contains(tamer.Value, StringComparison.Ordinal)),
        "SaveProcess offered the owner's Tamer as the placement target");

    // Select the Tamer and resume -> the deleted card is placed under it as a digivolution source.
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(SelectableId(select, tamer)));
    try { await AutoProcessing.For(context).ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { }

    var underTamer = new Permanent(context, tamer, P1).DigivolutionCards.Any(u => u.InstanceId == card);
    AssertTrue(underTamer, "the deleted card was placed under the Tamer as a digivolution source (SaveProcess completed via the window)");
}

async Task SaveGateRetired()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    await Place(context, P1, "P1-Tamer", cardType: "Tamer"); // a valid Save target exists...
    // ...but a bare hasSave METADATA marker with NO printed SaveEffect: the retired POST option no longer opens.
    var card = await Place(context, P1, "PLAIN", flags: (DeletionReplacementGate.HasSaveKey, true));
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndDrive(context, card);
    // Also drive the deletion-replacement POST option loop to prove it opens NO Save choice.
    var timing = new DeletionReplacementTiming();
    bool opened = timing.RequestChoice(context);
    AssertFalse(opened, "the retired Save POST option opens no agent choice for a bare hasSave marker (single-fire)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, card), "the card stays in the trash (Save gate retired)");
}

async Task SaveControlStaysTrashed()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    await Place(context, P1, "P1-Tamer", cardType: "Tamer");
    var card = await Place(context, P1, "PLAIN");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndDrive(context, card);
    AssertFalse(context.ChoiceController.Current.IsPending, "a plain deleted card opens no Save window (false-green guard)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, card), "the plain card stays trashed");
}

// ============================================================ ASCENSION (C-Del 3c-3: window fires it)

async Task AscensionPrintedFiresViaWindow()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "TfxAscension");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    // The printed AscensionSelfEffect IS collected by the window while the card is on field (wiring live)...
    var perm = new Permanent(context, card, P1);
    var ht = CardEffectCommons.OnDeletionHashtable(new List<Permanent> { perm }, byEffectCause: true, byBattleCause: false, false);
    AssertEqual(1, AutoProcessing.GetSkillInfos(ht, EffectTiming.OnDestroyedAnyone).Count,
        "the printed AscensionSelfEffect IS collected by the OnDestroyedAnyone window");

    // Delete + drive: RD-P6C3-A3 resolved, so CanActivateAscension (identity gate) is now satisfied. Because
    // Ascension's ActivateClass is isOptional=false (Commons/Ascension.cs:12 quirk), the window does NOT open a
    // "Will you use Ascension?" optional first — it forces the effect straight into AscensionProcess's own
    // "Will you place this card in security?" ModeChoice (the observable proof of the forced-activation quirk).
    await DeleteAndDrive(context, card);
    AssertTrue(context.ChoiceController.Current.IsPending, "the printed Ascension FIRED through the window (store charged)");
    ChoiceRequest prompt = context.ChoiceController.PendingRequest!;
    AssertEqual(ChoiceType.ModeChoice, prompt.Type,
        "isOptional=false quirk: the window forced the effect straight into AscensionProcess's own yes/no (a ModeChoice, NOT an OptionalEffect 'Will you use Ascension?')");
    AssertTrue(prompt.Message.Contains("security", StringComparison.OrdinalIgnoreCase),
        "the ModeChoice is AscensionProcess's 'Will you place this card in security?'");

    // Answer "Yes" (userSelection#0) and resume: the effect completes, moving the card trash -> security top.
    await AnswerYesToCompletion(context, prompt);
    AssertTrue(InZone(context, P1, ChoiceZone.Security, card), "the printed Ascension placed the deleted card into security (window path)");
    AssertFalse(InZone(context, P1, ChoiceZone.Trash, card), "the card left the trash");
    // (K2) AS-IS AscensionProcess: AddSecurityCard(card, true) = the TOP of security.
    AssertEqual(card, ((IZoneStateReader)context.ZoneMover).GetCards(P1, ChoiceZone.Security)[0],
        "the card is the TOP security card (AS-IS toTop)");
}

async Task AscensionControlStaysTrashed()
{
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var card = await Place(context, P1, "PLAIN");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, card, P1);

    await DeleteAndDrive(context, card);
    AssertFalse(context.ChoiceController.Current.IsPending, "a plain deleted card (no [Ascension]) opens no window (false-green guard)");
    AssertTrue(InZone(context, P1, ChoiceZone.Trash, card), "the plain card stays trashed");
}

async Task AscensionIdentityGate()
{
    // The AS-IS identity gate (its "anyone"-timed name notwithstanding, [Ascension] is a SELF response): both
    // CanTriggerOnDeletion (OnDeletion.cs:33 = the reacting card must be a source of a DELETED permanent) and
    // CanActivateOnDeletion (:141 = card.PermanentJustBeforeRemoveField == the dead TopCard's) key the response
    // to the LEAVING permanent's identity. Two IDENTICAL printed-[Ascension] holders A and B; delete ONLY A. A
    // reacts (it is the deleted permanent); B — same printed effect, but a DIFFERENT permanent that did not leave
    // — does NOT react. This isolates the identity: the ONLY difference between A and B is which one left.
    EngineContext context = NewContext();
    using var scope = AmbientMatchContext.Enter(context);
    var a = await Place(context, P1, "TfxAscension", instance: "1:battle:AscA");
    var b = await Place(context, P1, "TfxAscension", instance: "1:battle:AscB");
    await Place(context, P2, "FOE");
    CardEffectRegistrar.RegisterCard(context, a, P1);
    CardEffectRegistrar.RegisterCard(context, b, P1);

    // A window over A's deletion collects ONLY A's reactor — B's identical [Ascension] is NOT collected because B
    // is not a source of the deleted permanent (CanTriggerOnDeletion identity), even though OnDestroyedAnyone is
    // the "anyone" timing.
    var deadPerm = new Permanent(context, a, P1);
    var ht = CardEffectCommons.OnDeletionHashtable(new List<Permanent> { deadPerm }, byEffectCause: true, byBattleCause: false, false);
    AssertEqual(1, AutoProcessing.GetSkillInfos(ht, EffectTiming.OnDestroyedAnyone).Count,
        "only A's reactor is collected for A's deletion — B's identical [Ascension] does NOT react to a different permanent's deletion (CanTriggerOnDeletion identity)");

    // Delete ONLY A. A fires (window prompt); B — same effect, different permanent — does not.
    await DeleteAndDrive(context, a);
    AssertTrue(context.ChoiceController.Current.IsPending, "A (the deleted permanent) reacted");
    AssertEqual(ChoiceType.ModeChoice, context.ChoiceController.PendingRequest!.Type, "A's Ascension opened its own place-in-security prompt");
    await AnswerYesToCompletion(context, context.ChoiceController.PendingRequest!);

    AssertTrue(InZone(context, P1, ChoiceZone.Security, a), "A placed ITSELF in security (its own deletion)");
    AssertTrue(InZone(context, P1, ChoiceZone.BattleArea, b), "B is still on the field (it was not deleted)");
    AssertFalse(InZone(context, P1, ChoiceZone.Security, b), "B did NOT react to A's deletion (identity gate: different permanent)");
}

// ============================================================ HARNESS

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault(randomSeed: 11, deferredChoice: true);
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);
    ctx.TurnController.SetPhase(HeadlessPhase.Main);
    return ctx;
}

MatchStateMutationSink Sink(EngineContext context) =>
    new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: null, context.EffectRegistry, context: context);

async Task DeleteAndDrive(EngineContext context, HeadlessEntityId card)
{
    MatchStateMutationSink sink = Sink(context);
    sink.Apply(Delete(card));
    await sink.FlushAsync(); // FlushAsync drains the OnDestroyedAnyone window it opened.
}

// Answer a pending optional "yes" (select its first candidate), resume the suspended MultipleSkills chain, and
// return the NEXT pending choice (the effect's own selection).
async Task<ChoiceRequest> AnswerYesAndResume(EngineContext context, ChoiceRequest optional)
{
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(optional.Candidates[0].Id));
    try { await AutoProcessing.For(context).ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { }
    AssertTrue(context.ChoiceController.Current.IsPending, "answering the optional 'yes' opened the effect's own choice");
    return context.ChoiceController.PendingRequest!;
}

// Answer a pending yes/no (ModeChoice / optional) "yes" (its first candidate) and resume the suspended window to
// COMPLETION — the effect has no further choice (e.g. AscensionProcess after "yes" just moves the card). Unlike
// AnswerYesAndResume, this does NOT assert a follow-up pending choice.
async Task AnswerYesToCompletion(EngineContext context, ChoiceRequest prompt)
{
    context.ChoiceController.ResolveChoice(ChoiceResult.Select(prompt.Candidates[0].Id));
    try { await AutoProcessing.For(context).ResumeSuspendedWindowsAsync(); }
    catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException) { }
}

ICardEffect GrantSource(EngineContext context, HeadlessEntityId hostId, string name)
{
    var host = new CardSource(context, hostId, P1, P1);
    var ac = new ActivateClass();
    ac.SetUpICardEffect(name, _ => true, host);
    return ac;
}

// Maps a SelectPermanent candidate (whose id may be a composite/label) back to a selectable id for the target.
HeadlessEntityId SelectableId(ChoiceRequest request, HeadlessEntityId target)
{
    foreach (ChoiceCandidate c in request.Candidates)
    {
        if (c.Id == target || c.Label.Contains(target.Value, StringComparison.Ordinal))
        {
            return c.Id;
        }
    }

    return target;
}

async Task<HeadlessEntityId> Place(EngineContext ctx, HeadlessPlayerId owner, string num,
    string cardType = "Digimon", string[]? sources = null, string? instance = null, (string Key, bool Value)? flags = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["level"] = 4 };
    cards.Upsert(new CardRecord(defId, num, num, defMeta, CardType: cardType));
    var id = new HeadlessEntityId(instance ?? $"{owner.Value}:battle:{num}");
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 4000, ["isSuspended"] = false };
    if (sources != null) meta["sourceIds"] = sources;
    if (flags is { } f) meta[f.Key] = f.Value;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await ctx.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, ChoiceZone.BattleArea));
    return id;
}

async Task<HeadlessEntityId> PlaceNone(EngineContext ctx, HeadlessPlayerId owner, string num, (string Key, bool Value)? flags = null)
{
    var cards = (CardDatabase)ctx.CardRepository;
    var defId = new HeadlessEntityId(num);
    cards.Upsert(new CardRecord(defId, num, num, new Dictionary<string, object?>(StringComparer.Ordinal) { ["dp"] = 3000, ["level"] = 3 }, CardType: "Digimon"));
    var id = new HeadlessEntityId(num);
    var meta = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (flags is { } f) meta[f.Key] = f.Value;
    ctx.CardInstanceRepository.Upsert(new CardInstanceRecord(id, defId, owner, Metadata: meta));
    await Task.CompletedTask;
    return id;
}

EffectMutation Delete(HeadlessEntityId cardId) =>
    new(MatchStateMutationSink.DeleteKind, new HeadlessEntityId("2:battle:FOE"),
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cardId.Value });

bool InZone(EngineContext context, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    ((IZoneStateReader)context.ZoneMover).GetCards(player, zone).Contains(cardId);

bool ReadFlag(EngineContext context, HeadlessEntityId cardId, string key) =>
    context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

static void AssertTrue(bool v, string label) { if (!v) throw new InvalidOperationException($"{label}: expected true."); }
static void AssertFalse(bool v, string label) { if (v) throw new InvalidOperationException($"{label}: expected false."); }
static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}
