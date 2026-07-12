using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.DataLoading;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// C-5 WITNESS CARDS — real-card integration for the security/field battle PRE would-be-deleted window and
// its keyword grants. Unlike tests/C5-SecurityPreWindow.Tests (metadata keyword flags), every keyword here
// comes from the REAL card class discovered by CardEffectDispatch (definition CardNumber = class name) and
// registered through the production enter-play path (RegisterEnteredCardEffects), so these tests witness:
//   BT14_035 — <Barrier> (BarrierSelfEffect at WhenPermanentWouldBeDeleted)
//   BT13_023 — <Evade> + [When Attacking] trash the BOTTOM digivolution card of 1 opponent Digimon
//   EX8_051  — <Fragment <3>> (+Collision/Piercing statics) + trash-source ESS "<De-Digivolve 1>" from TRASH
//   EX8_061  — <Scapegoat> (by-own-effect excluded) + alternate digivolution requirement (DS Lv4, cost 3)
//              + [When Attacking][Once Per Turn] / [On Deletion] trash-plays

HeadlessPlayerId P1 = new(1);
HeadlessPlayerId P2 = new(2);

var tests = new (string Name, Func<Task> Body)[]
{
    ("BT14_035: registered <Barrier> grant survives a security battle loss (top own security paid)", Barrier_SecurityBattle),
    ("BT13_023: registered <Evade> grant suspends+survives a security battle loss and the check resumes", Evade_SecurityBattle_Resume),
    ("BT13_023: [When Attacking] trashes the BOTTOM digivolution card of the chosen opponent Digimon", WhenAttacking_TrashBottom),
    ("EX8_051: <Fragment <3>> pays exactly 3 sources through the PRE window and survives a field battle", Fragment_FieldBattle),
    ("EX8_051: only 2 sources -> Fragment unaffordable, NO offer, deleted outright", Fragment_Insufficient_NoOffer),
    ("EX8_051: trash-source ESS — effect-trashed from a [Mineral] host, De-Digivolves 1 opponent Digimon from the TRASH", TrashSourceEss_DeDigivolve),
    ("EX8_051: trash-source ESS does NOT fire off a non-[Mineral]/[Rock] host (trait gate)", TrashSourceEss_TraitGate),
    ("EX8_061: <Scapegoat> sacrifice routes the ally through the delete pipeline — the ally's [On Deletion] fires (nested)", Scapegoat_NestedAllyOnDeletion),
    ("EX8_061: <Scapegoat> is NOT offered when the deletion is by the owner's OWN effect", Scapegoat_OwnEffect_NoOffer),
    ("EX8_061: [When Attacking] memory>=1 gates the trash-play; [Once Per Turn] caps the second attack", WhenAttacking_TrashPlay_GateAndCap),
    ("EX8_061: [On Deletion] plays a matching Digimon from the trash after the real deletion", OnDeletion_TrashPlay),
    ("EX8_061: alternate digivolution (DS Lv4, cost 3) is read dispatch-first off the HAND card", AlternateDigivolution),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try { await test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex)
    {
        failures.Add(test.Name);
        Console.Error.WriteLine($"FAIL {test.Name}");
        Console.Error.WriteLine($"{ex}");
    }
}

if (failures.Count > 0) { Console.Error.WriteLine($"\n{failures.Count} test(s) failed."); Environment.Exit(1); }
Console.WriteLine($"\n{tests.Length} test(s) passed.");

// --- BT14_035 <Barrier> ---------------------------------------------------

async Task Barrier_SecurityBattle()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "BT14_035", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 3000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 2 });
    HeadlessEntityId ownSecurity = await Place(match, P1, "PLAIN", "p1sec", ChoiceZone.Security);
    HeadlessEntityId sec1 = await Place(match, P2, "PLAIN", "p2sec1", ChoiceZone.Security, new Dictionary<string, object?> { ["dp"] = 9000 });
    HeadlessEntityId sec2 = await Place(match, P2, "PLAIN", "p2sec2", ChoiceZone.Security, new Dictionary<string, object?> { ["dp"] = 1000 });
    // Security stacks like AS-IS reveal top-first: last added is on top -> reorder so 9000 is checked FIRST.
    // (ZoneMover appends; SecurityResolver reveals index 0.) sec1 was added first (index 0) -> already first.

    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: null, isDirectAttack: true);
    await match.StepAsync();   // security battle loss -> PRE window (Barrier via the REGISTERED grant)

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE would-be-deleted window is open");
    LegalAction activate = ResolveActions(match, P1).Single(a => a.Id.Value.Contains("#barrier", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertInZone(match, P1, ChoiceZone.BattleArea, attacker, "Barrier attacker survives the security battle");
    AssertTrue(ReadFlag(match, attacker, DeletionReplacementGate.BarrieredKey), "barriered marker");
    AssertInZone(match, P1, ChoiceZone.Trash, ownSecurity, "the attacker's own top security was paid");
    AssertInZone(match, P2, ChoiceZone.Trash, sec2, "the SECOND security card was checked (loop resumed)");
    AssertAttackEnded(match, "attack completed after the resumed check");
    _ = sec1;
}

// --- BT13_023 <Evade> + [When Attacking] ----------------------------------

async Task Evade_SecurityBattle_Resume()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "BT13_023", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 3000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 2 });
    HeadlessEntityId sec1 = await Place(match, P2, "PLAIN", "p2sec1", ChoiceZone.Security, new Dictionary<string, object?> { ["dp"] = 9000 });
    HeadlessEntityId sec2 = await Place(match, P2, "PLAIN", "p2sec2", ChoiceZone.Security, new Dictionary<string, object?> { ["dp"] = 1000 });

    // NOTE: the [When Attacking] trigger is collected at declare (canUse passes) but its canActivate half is
    // false — no opponent battle-area Digimon — so the window exhausts without a choice and the attack proceeds.
    // (MIG1) the mirror Attack() now faithfully SUSPENDS the attacker at declaration (AS-IS :158-167); Evade's
    // suspend cost needs an unsuspended attacker, so declare withoutTap (the Vortex-shape untapped attack).
    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: null, isDirectAttack: true, withoutTap: true);
    await match.StepAsync();

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE would-be-deleted window is open");
    LegalAction activate = ResolveActions(match, P1).Single(a => a.Id.Value.Contains("#evade", StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    AssertInZone(match, P1, ChoiceZone.BattleArea, attacker, "Evade attacker survives the security battle");
    AssertTrue(ReadFlag(match, attacker, DeletionReplacementGate.IsSuspendedKey), "suspended as the Evade cost");
    AssertTrue(ReadFlag(match, attacker, DeletionReplacementGate.EvadedKey), "evaded marker");
    AssertInZone(match, P2, ChoiceZone.Trash, sec2, "the SECOND security card was checked (loop resumed)");
    AssertAttackEnded(match, "attack completed after the resumed check");
    _ = sec1;
}

async Task WhenAttacking_TrashBottom()
{
    // The defender SURVIVES the battle (9000 vs the attacker's 500), so the only thing that can put a
    // digivolution source in the trash is the [When Attacking] bottom-trash itself — the assert is not
    // masked by a defender deletion sweeping its whole stack.
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "BT13_023", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 500, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 1 });
    HeadlessEntityId defender = await Place(match, P2, "PLAIN", "p2def", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 9000, ["isSuspended"] = true });
    HeadlessEntityId srcTop = await Place(match, P2, "SRC", "p2src0", ChoiceZone.None);
    HeadlessEntityId srcBottom = await Place(match, P2, "SRC", "p2src1", ChoiceZone.None);
    SetMetadata(match, defender, new Dictionary<string, object?> { ["sourceIds"] = new[] { srcTop.Value, srcBottom.Value } });

    // (MIG1) withoutTap so the losing attacker can still pay Evade's suspend cost (see the note above).
    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: defender, isDirectAttack: false, withoutTap: true);
    await match.StepAsync();   // [When Attacking] window: mandatory select of 1 opponent Digimon

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "[When Attacking] select window is open");
    LegalAction pick = ResolveActions(match, P1).Single(a => a.Id.Value.Contains(defender.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();   // bottom source trashed, then the battle: the 500 attacker loses -> Evade PRE window

    AssertInZone(match, P2, ChoiceZone.Trash, srcBottom, "the BOTTOM digivolution card was trashed");
    AssertFalse(InZone(match, P2, ChoiceZone.Trash, srcTop), "the TOP digivolution card stays (bottom-only)");
    AssertInZone(match, P2, ChoiceZone.BattleArea, defender, "the defender survived the battle");

    // The losing attacker's own <Evade> PRE window opened; decline it and the deletion finalizes.
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "the attacker's Evade PRE window is open");
    LegalAction decline = ResolveActions(match, P1).Single(a => a.Id.Value.EndsWith(":skip", StringComparison.Ordinal));
    await match.ApplyActionAsync(decline);
    await match.StepAsync();

    AssertInZone(match, P1, ChoiceZone.Trash, attacker, "the declined attacker was deleted by the battle");
}

// --- EX8_051 <Fragment <3>> + trash-source ESS -----------------------------

async Task Fragment_FieldBattle()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "EX8_051", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 3000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 1 });
    HeadlessEntityId defender = await Place(match, P2, "PLAIN", "p2def", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 9000, ["isSuspended"] = true });
    var sources = new List<HeadlessEntityId>();
    for (int i = 0; i < 3; i++)
    {
        sources.Add(await Place(match, P1, "SRC", $"p1src{i}", ChoiceZone.None));
    }

    SetMetadata(match, attacker, new Dictionary<string, object?> { ["sourceIds"] = sources.Select(s => s.Value).ToArray() });

    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: defender, isDirectAttack: false);
    await match.StepAsync();   // field battle loss -> PRE window (Fragment via the REGISTERED grant)

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE window is open");
    LegalAction activate = ResolveActions(match, P1).Single(a =>
        a.Id.Value.Contains("#fragment", StringComparison.Ordinal) && !sources.Any(s => a.Id.Value.Contains(s.Value, StringComparison.Ordinal)));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();

    // Pay 3 sources, one target pick per re-opened window (grant trashValue: 3).
    for (int i = 0; i < 3; i++)
    {
        AssertTrue(match.Context.ChoiceController.Current.IsPending, $"fragment source pick {i + 1} is open");
        LegalAction pick = ResolveActions(match, P1).First(a => sources.Any(s => a.Id.Value.Contains(s.Value, StringComparison.Ordinal)));
        await match.ApplyActionAsync(pick);
        await match.StepAsync();
    }

    AssertInZone(match, P1, ChoiceZone.BattleArea, attacker, "Fragment attacker survives the field battle");
    // (The PRE-window fragment path pays per-pick via ApplyFragmentSource — it clears the pending deletion
    // rather than stamping the legacy auto-path 'fragmented' marker; the stack must be fully paid out.)
    AssertFalse(ReadFlag(match, attacker, GameFlowProcessor.PendingDeletionKey), "pending deletion cleared");
    foreach (HeadlessEntityId source in sources)
    {
        AssertInZone(match, P1, ChoiceZone.Trash, source, $"source {source.Value} paid to the trash");
    }

    AssertInZone(match, P2, ChoiceZone.BattleArea, defender, "the 9000 defender survived the battle");
}

async Task Fragment_Insufficient_NoOffer()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "EX8_051", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 3000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 1 });
    HeadlessEntityId defender = await Place(match, P2, "PLAIN", "p2def", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 9000, ["isSuspended"] = true });
    HeadlessEntityId s0 = await Place(match, P1, "SRC", "p1src0", ChoiceZone.None);
    HeadlessEntityId s1 = await Place(match, P1, "SRC", "p1src1", ChoiceZone.None);
    SetMetadata(match, attacker, new Dictionary<string, object?> { ["sourceIds"] = new[] { s0.Value, s1.Value } });

    AttackDeclarationCommons.Declare(match.Context, P1, attacker, P2, targetId: defender, isDirectAttack: false);
    await match.StepAsync();

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no PRE window — Fragment <3> is unaffordable with 2 sources");
    AssertInZone(match, P1, ChoiceZone.Trash, attacker, "attacker deleted outright");
}

async Task TrashSourceEss_DeDigivolve()
{
    DcgoMatch match = await DriveTrashSourceEss(hostDefinition: "MINERAL");

    // The window opened FROM THE TRASH: EX8_051's De-Digivolve select.
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "the trash-resident ESS select window is open");
    HeadlessEntityId victim = new("wit:victim");
    LegalAction pick = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(victim.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();

    // De-Digivolve 1: the victim's top card peels to the trash, the under-source is promoted.
    AssertInZone(match, P1, ChoiceZone.Trash, victim, "the victim's top card was de-digivolved to the trash");
    AssertInZone(match, P1, ChoiceZone.BattleArea, new HeadlessEntityId("wit:vicsrc"), "the victim's under-source was promoted");
}

async Task TrashSourceEss_TraitGate()
{
    DcgoMatch match = await DriveTrashSourceEss(hostDefinition: "PLAIN");   // no [Mineral]/[Rock] trait
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no ESS window — the host lacks the [Mineral]/[Rock] trait");
    AssertInZone(match, P1, ChoiceZone.BattleArea, new HeadlessEntityId("wit:victim"), "the victim is untouched");
}

// Shared driver: P2 host (hostDefinition) has EX8_051 as its digivolution source; an effect trashes that
// source (emitting OnDigivolutionCardDiscarded), and P1 has a De-Digivolve-able Digimon on the field.
async Task<DcgoMatch> DriveTrashSourceEss(string hostDefinition)
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext context = match.Context;
    HeadlessEntityId host = await Place(match, P2, hostDefinition, "host", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    // EX8_051 as a digivolution source of the host — NOT registered (sources never are); the ESS is found
    // by the dispatch-based bridge scan once the card reaches the trash.
    HeadlessEntityId ess = await Place(match, P2, "EX8_051", "ess", ChoiceZone.None);
    SetMetadata(match, host, new Dictionary<string, object?> { ["sourceIds"] = new[] { ess.Value } });

    HeadlessEntityId victim = await Place(match, P1, "PLAIN", "victim", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    HeadlessEntityId victimSource = await Place(match, P1, "SRC", "vicsrc", ChoiceZone.None);
    SetMetadata(match, victim, new Dictionary<string, object?> { ["sourceIds"] = new[] { victimSource.Value } });

    // An effect trashes the host's digivolution source (AS-IS ITrashDigivolutionCards — fires the
    // OnDigivolutionCardDiscarded window before the physical move).
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, log: null, context.ZoneMover, context.MemoryController,
        context.EffectRegistry, context.GameEventQueue, context: context);
    sink.Apply(new EffectMutation(
        MatchStateMutationSink.TrashDigivolutionCardsKind,
        new HeadlessEntityId("wit:cause"),
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
            [MatchStateMutationSink.CountKey] = 1,
            [MatchStateMutationSink.FromBottomKey] = true,
        }));
    await sink.FlushAsync();
    await match.StepAsync();

    AssertInZone(match, P2, ChoiceZone.Trash, ess, "EX8_051 was trashed from the host's digivolution cards");
    return match;
}

// --- EX8_061 <Scapegoat> + trash-plays + alternate digivolution ------------

async Task Scapegoat_NestedAllyOnDeletion()
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext context = match.Context;
    HeadlessEntityId holder = await PlaceWitness(match, P2, "EX8_061", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    // The sacrifice candidate is a REAL [On Deletion] card (BT2_034: security <= 3 -> Recovery +1), so the
    // nested window is witnessed by an observable state change (P2 security 0 -> 1).
    HeadlessEntityId ally = await PlaceWitness(match, P2, "BT2_034", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    HeadlessEntityId deleter = await Place(match, P1, "PLAIN", "deleter", ChoiceZone.BattleArea);

    ApplyDelete(match, source: deleter, target: holder);
    await match.StepAsync();   // PRE window (scapegoat, via the REGISTERED grant)

    AssertTrue(match.Context.ChoiceController.Current.IsPending, "PRE window is open (opponent-effect deletion)");
    LegalAction activate = ResolveActions(match, P2).Single(a =>
        a.Id.Value.Contains("#scapegoat", StringComparison.Ordinal) && !a.Id.Value.Contains(ally.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(activate);
    await match.StepAsync();   // step 2: pick the ally

    LegalAction pickAlly = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(ally.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pickAlly);
    await match.StepAsync();   // the ally dies through the FULL pipeline -> its [On Deletion] fires

    AssertInZone(match, P2, ChoiceZone.BattleArea, holder, "the Scapegoat holder survives");
    AssertInZone(match, P2, ChoiceZone.Trash, ally, "the chosen ally was sacrificed");
    AssertEqual(1, SecurityCount(match, P2), "the sacrificed ally's [On Deletion] Recovery +1 fired (nested window)");
    _ = context;
}

async Task Scapegoat_OwnEffect_NoOffer()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId holder = await PlaceWitness(match, P2, "EX8_061", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    HeadlessEntityId ally = await Place(match, P2, "PLAIN", "ally", ChoiceZone.BattleArea);
    HeadlessEntityId ownDeleter = await Place(match, P2, "PLAIN", "owndeleter", ChoiceZone.BattleArea);

    // Deleted by the owner's OWN effect: AS-IS Scapegoat's by-cause clause (Scapegoat.cs:65-73
    // IsByEffect(IsOwnerEffect) -> no trigger) suppresses the offer entirely.
    ApplyDelete(match, source: ownDeleter, target: holder);
    await match.StepAsync();

    AssertFalse(match.Context.ChoiceController.Current.IsPending, "no Scapegoat offer for an own-effect deletion");
    AssertInZone(match, P2, ChoiceZone.Trash, holder, "the holder was deleted outright");
    AssertInZone(match, P2, ChoiceZone.BattleArea, ally, "the ally was NOT sacrificed");
}

async Task WhenAttacking_TrashPlay_GateAndCap()
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext context = match.Context;
    HeadlessEntityId attacker = await PlaceWitness(match, P1, "EX8_061", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["dp"] = 9000, ["isSuspended"] = false, [SecurityResolver.StrikeKey] = 1 });
    HeadlessEntityId d1 = await Place(match, P2, "PLAIN", "p2d1", ChoiceZone.BattleArea, new Dictionary<string, object?> { ["dp"] = 1000, ["isSuspended"] = true });
    HeadlessEntityId d2 = await Place(match, P2, "PLAIN", "p2d2", ChoiceZone.BattleArea, new Dictionary<string, object?> { ["dp"] = 1000, ["isSuspended"] = true });
    HeadlessEntityId d3 = await Place(match, P2, "PLAIN", "p2d3", ChoiceZone.BattleArea, new Dictionary<string, object?> { ["dp"] = 1000, ["isSuspended"] = true });
    HeadlessEntityId dsPlay = await Place(match, P1, "DSPLAY", "ds1", ChoiceZone.Trash);
    HeadlessEntityId dsPlay2 = await Place(match, P1, "DSPLAY", "ds2", ChoiceZone.Trash);
    HeadlessEntityId noMatch = await Place(match, P1, "NOMATCH", "nm", ChoiceZone.Trash);

    // (1) memory 0 -> the "1 or more memory" gate fails: no window, the attack just resolves.
    context.MemoryController.Set(0);
    AttackDeclarationCommons.Declare(context, P1, attacker, P2, targetId: d1, isDirectAttack: false);
    await match.StepAsync();
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "memory 0: no trash-play window");
    AssertInZone(match, P1, ChoiceZone.Trash, dsPlay, "memory 0: nothing was played");

    // (2) memory 1 -> the window opens; only trait+level matching cards are candidates; play one.
    SetMetadata(match, attacker, new Dictionary<string, object?> { ["isSuspended"] = false });
    context.MemoryController.Set(1);
    AttackDeclarationCommons.Declare(context, P1, attacker, P2, targetId: d2, isDirectAttack: false);
    await match.StepAsync();
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "memory 1: the trash-play select is open");
    AssertFalse(ResolveActions(match, P1).Any(a => a.Id.Value.Contains(noMatch.Value, StringComparison.Ordinal)),
        "a non-[DS/Mollusk/Crustacean] trash card is NOT a candidate");
    LegalAction pick = ResolveActions(match, P1).Single(a => a.Id.Value.Contains(dsPlay.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();
    AssertInZone(match, P1, ChoiceZone.BattleArea, dsPlay, "the selected [DS] Digimon was played from the trash");

    // (3) same turn, third attack -> [Once Per Turn] (capHash PlayDigimon_EX8_061) suppresses the window.
    SetMetadata(match, attacker, new Dictionary<string, object?> { ["isSuspended"] = false });
    context.MemoryController.Set(1);
    AttackDeclarationCommons.Declare(context, P1, attacker, P2, targetId: d3, isDirectAttack: false);
    await match.StepAsync();
    AssertFalse(match.Context.ChoiceController.Current.IsPending, "capped: no second trash-play this turn");
    AssertInZone(match, P1, ChoiceZone.Trash, dsPlay2, "capped: the second [DS] card stays in the trash");
    _ = d1;
}

async Task OnDeletion_TrashPlay()
{
    DcgoMatch match = await CreateMatchAsync();
    HeadlessEntityId holder = await PlaceWitness(match, P2, "EX8_061", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    HeadlessEntityId dsPlay = await Place(match, P2, "DS4", "ds4", ChoiceZone.Trash);
    HeadlessEntityId deleter = await Place(match, P1, "PLAIN", "deleter", ChoiceZone.BattleArea);

    // No other P2 Digimon -> no Scapegoat candidates -> the opponent-effect deletion resolves outright,
    // then the [On Deletion] trash-play window opens for the now-trashed EX8_061.
    ApplyDelete(match, source: deleter, target: holder);
    await match.StepAsync();

    AssertInZone(match, P2, ChoiceZone.Trash, holder, "EX8_061 was deleted (no sacrifice available)");
    AssertTrue(match.Context.ChoiceController.Current.IsPending, "[On Deletion] trash-play select is open");
    LegalAction pick = ResolveActions(match, P2).Single(a => a.Id.Value.Contains(dsPlay.Value, StringComparison.Ordinal));
    await match.ApplyActionAsync(pick);
    await match.StepAsync();

    AssertInZone(match, P2, ChoiceZone.BattleArea, dsPlay, "the level-4 [DS] Digimon was played from the trash");
}

async Task AlternateDigivolution()
{
    DcgoMatch match = await CreateMatchAsync();
    EngineContext context = match.Context;
    context.MemoryController.Set(5);
    // A level-4 [DS] Digimon on the field — the added requirement's target.
    HeadlessEntityId target = await Place(match, P1, "DS4", "target", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    // EX8_061 in HAND — never registered; the added requirement must be read dispatch-first.
    HeadlessEntityId evo = await Place(match, P1, "EX8_061", "evo", ChoiceZone.Hand);

    // The printed requirement (Purple@5) fails against a DS Lv4 target; the printed cost is rejected too.
    var atPrinted = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 2), context);
    AssertFalse(atPrinted.IsSuccess, "printed path (Purple@5, cost 2) is rejected");

    var result = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo, target, memoryCost: 3), context);
    AssertTrue(result.IsSuccess, $"legal via the added DS-Lv4 path at cost 3 ({result.Message})");
    AssertInZone(match, P1, ChoiceZone.BattleArea, evo, "EX8_061 became the new top");

    // Control: a NON-DS level-4 target stays illegal.
    DcgoMatch control = await CreateMatchAsync();
    control.Context.MemoryController.Set(5);
    HeadlessEntityId plainTarget = await Place(control, P1, "PLAIN4", "target", ChoiceZone.BattleArea,
        new Dictionary<string, object?> { ["isSuspended"] = false });
    HeadlessEntityId evo2 = await Place(control, P1, "EX8_061", "evo", ChoiceZone.Hand);
    var illegal = await new DigivolveAction().ProcessAsync(HeadlessActionFactory.Digivolve(P1, evo2, plainTarget, memoryCost: 3), control.Context);
    AssertFalse(illegal.IsSuccess, "a non-[DS] Lv4 target does not satisfy the added path");
}

// --- Harness ---------------------------------------------------------------

async Task<DcgoMatch> CreateMatchAsync()
{
    // deferredChoice: interactive ACTIVATED bodies (the trash-play / de-digivolve selects) surface as agent
    // ResolveChoice decisions instead of the enqueued-result fallback (G11-002 pattern).
    EngineContext context = EngineContext.CreateDefault(randomSeed: 75, deferredChoice: true);
    CardDatabase cards = (CardDatabase)context.CardRepository;
    for (int index = 1; index <= 12; index++)
    {
        cards.Upsert(Definition($"P1-M{index:D2}"));
        cards.Upsert(Definition($"P2-M{index:D2}"));
    }

    // Witness card definitions: CardNumber = ported class name (CardEffectDispatch resolves by it).
    cards.Upsert(Definition("BT14_035"));
    cards.Upsert(Definition("BT13_023"));
    cards.Upsert(Definition("EX8_051"));
    cards.Upsert(Definition("EX8_061", level: 6, playCost: 2, evolutionCondition: "Purple@5", extra: new Dictionary<string, object?> { ["fixedDigivolutionCost"] = 2 }));
    cards.Upsert(Definition("BT2_034"));
    // Support definitions.
    cards.Upsert(Definition("PLAIN"));
    cards.Upsert(Definition("PLAIN4", level: 4));
    cards.Upsert(Definition("SRC"));
    cards.Upsert(Definition("MINERAL", traits: new[] { "Mineral" }));
    cards.Upsert(Definition("DSPLAY", level: 3, traits: new[] { "DS" }));
    cards.Upsert(Definition("DS4", level: 4, traits: new[] { "DS" }));
    cards.Upsert(Definition("NOMATCH", level: 3, traits: new[] { "Machine" }));

    DcgoMatch match = new(context);
    MatchSetupConfig setup = MatchSetupConfig.Create(
        new[] { BuildDeck(P1, "P1"), BuildDeck(P2, "P2") },
        firstPlayerId: P1,
        initialSecuritySize: 0, shuffleDecks: false, shuffleDigitamaDecks: false);

    await match.InitializeAsync(MatchConfig.Create(new[] { P1, P2 }, randomSeed: 75, setup: setup));
    await AdvanceToMainAsync(match, P1);
    return match;
}

static CardRecord Definition(
    string id, int level = -1, IReadOnlyList<string>? traits = null, int? playCost = null,
    string? evolutionCondition = null, IReadOnlyDictionary<string, object?>? extra = null)
{
    var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
    if (level >= 0)
    {
        metadata["level"] = level;
    }

    if (traits is not null)
    {
        metadata["traits"] = traits.ToArray();
    }

    if (extra is not null)
    {
        foreach (KeyValuePair<string, object?> pair in extra)
        {
            metadata[pair.Key] = pair.Value;
        }
    }

    return new CardRecord(new HeadlessEntityId(id), id, $"{id} Card", metadata,
        CardType: "Digimon", PlayCost: playCost, EvolutionCondition: evolutionCondition);
}

static PlayerDeckSetup BuildDeck(HeadlessPlayerId playerId, string prefix) =>
    new(playerId,
        Enumerable.Range(1, 12).Select(i => new HeadlessEntityId($"{prefix}-M{i:D2}")).ToArray(),
        Enumerable.Range(1, 3).Select(i => new HeadlessEntityId($"{prefix}-D{i:D2}")).ToArray());

// Place a standalone instance of `definitionId` into `zone` (metadata optional).
async Task<HeadlessEntityId> Place(
    DcgoMatch match, HeadlessPlayerId owner, string definitionId, string tag, ChoiceZone zone,
    IReadOnlyDictionary<string, object?>? metadata = null)
{
    var id = new HeadlessEntityId($"wit:{tag}");
    match.Context.CardInstanceRepository.Upsert(new CardInstanceRecord(id, new HeadlessEntityId(definitionId), owner));
    if (zone != ChoiceZone.None)
    {
        await match.Context.ZoneMover.MoveAsync(new ZoneMoveRequest(owner, id, ChoiceZone.None, zone));
    }

    if (metadata is not null)
    {
        SetMetadata(match, id, metadata);
    }

    return id;
}

// Place a WITNESS card and register its ported effects through the production enter-play chokepoint.
async Task<HeadlessEntityId> PlaceWitness(
    DcgoMatch match, HeadlessPlayerId owner, string cardNumber, ChoiceZone zone,
    IReadOnlyDictionary<string, object?>? metadata = null)
{
    HeadlessEntityId id = await Place(match, owner, cardNumber, cardNumber.ToLowerInvariant() + ":" + owner.Value, zone, metadata);
    match.Context.RegisterEnteredCardEffects(id, owner);
    return id;
}

void ApplyDelete(DcgoMatch match, HeadlessEntityId source, HeadlessEntityId target)
{
    EngineContext context = match.Context;
    var sink = new MatchStateMutationSink(
        context.CardInstanceRepository, log: null, context.ZoneMover, context.MemoryController,
        context.EffectRegistry, context.GameEventQueue, context: context);
    sink.Apply(new EffectMutation(
        MatchStateMutationSink.DeleteKind,
        source,
        new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    sink.FlushAsync().GetAwaiter().GetResult();
}

static async Task AdvanceToMainAsync(DcgoMatch match, HeadlessPlayerId playerId)
{
    for (var attempt = 0; attempt < 8 && match.GetObservation().Turn.Phase != HeadlessPhase.Main; attempt++)
    {
        LegalAction[] advance = match.GetLegalActions(playerId)
            .Where(a => a.ActionType == HeadlessActionTypes.AdvancePhase).ToArray();
        AssertEqual(1, advance.Length, "advance phase count");
        await match.ApplyActionAsync(advance[0]);
        await match.StepAsync();
    }

    AssertEqual(HeadlessPhase.Main, match.GetObservation().Turn.Phase, "advance to main");
}

IEnumerable<LegalAction> ResolveActions(DcgoMatch match, HeadlessPlayerId player) =>
    match.GetLegalActions(player).Where(a => a.ActionType == HeadlessActionTypes.ResolveChoice);

void SetMetadata(DcgoMatch match, HeadlessEntityId cardId, IReadOnlyDictionary<string, object?> values)
{
    if (!match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? record) || record is null)
    {
        throw new InvalidOperationException($"Missing card instance '{cardId}'.");
    }

    Dictionary<string, object?> metadata = new(record.Metadata, StringComparer.Ordinal);
    foreach (KeyValuePair<string, object?> pair in values)
    {
        metadata[pair.Key] = pair.Value;
    }

    match.Context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
}

int SecurityCount(DcgoMatch match, HeadlessPlayerId player) =>
    ((IZoneStateReader)match.Context.ZoneMover).GetCards(player, ChoiceZone.Security).Count;

bool InZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId) =>
    match.Context.ZoneMover is IZoneStateReader reader && reader.GetCards(player, zone).Contains(cardId);

void AssertInZone(DcgoMatch match, HeadlessPlayerId player, ChoiceZone zone, HeadlessEntityId cardId, string label) =>
    AssertTrue(InZone(match, player, zone, cardId), label);

bool ReadFlag(DcgoMatch match, HeadlessEntityId cardId, string key) =>
    match.Context.CardInstanceRepository.TryGetInstance(cardId, out CardInstanceRecord? r) && r is not null
        && r.Metadata.TryGetValue(key, out object? raw) && raw is bool b && b;

void AssertAttackEnded(DcgoMatch match, string label)
{
    HeadlessAttackState attack = match.Context.AttackController.Current;
    AssertTrue(attack.Phase == AttackPhase.None && !attack.IsPending, $"{label} (phase={attack.Phase}, pending={attack.IsPending})");
}

// --- Assertions ----------------------------------------------------------

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void AssertFalse(bool value, string label)
{
    if (value) throw new InvalidOperationException($"{label}: expected false.");
}
