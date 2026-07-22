namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Aliased (not a namespace import) to avoid pulling the sibling `...Script.CardEffectFactory` namespace
// into scope, which would clash with the CardEffectFactory type below.
using SelectPermanentEffect = HeadlessDCGO.Engine.Assets.Scripts.Script.SelectPermanentEffect;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;


/// <summary>
/// Headless mirror of the original <c>CardEffectCommons</c> condition predicates used inside card
/// <c>condition</c> lambdas. Each reads live state from the <see cref="CardSource"/>'s engine context.
/// </summary>
// (EFFECT-MODEL REBUILD) `partial` so the AS-IS `partial class CardEffectCommons` file split
// (HashtableSetting.cs / GetFromHashtable.cs / GameContextDeterminarion.cs …) mirrors 1:1 as sibling partial
// files in this directory, exactly as AS-IS organises them.
public static partial class CardEffectCommons
{
    /// <summary>1:1 mirror of AS-IS <c>CardEffectCommons.IgnoreRequirement</c> (CardEffectCommons.cs:11, NESTED
    /// inside the <c>CardEffectCommons</c> class) — which part of a digivolution requirement a "digivolve into"
    /// effect waives: <c>None</c> enforces color AND level; <c>All</c> waives the whole requirement; <c>Level</c>
    /// waives level only (color still checked); <c>Color</c> waives color only (level still checked). Passed to
    /// the eligibility check (AS-IS <c>CanPlayCardTargetFrame</c>'s <c>ignore:</c>). Nested here (not a top-level
    /// namespace enum) so every reference reads <c>CardEffectCommons.IgnoreRequirement</c> exactly as AS-IS.</summary>
    public enum IgnoreRequirement
    {
        None,
        All,
        Level,
        Color,
    }

    /// <summary>(AD1-G) 1:1 mirror of AS-IS <c>CardEffectCommons.GainCanNotBeDeletedByBattle</c>
    /// (GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs:11-54): grant the TARGET permanent a
    /// timed battle-deletion immunity. Registers a duration-tagged, card-TARGETED restriction binding
    /// (consumed by <see cref="BattleDeletionGate"/>): the flag + the caller's 4-arg battle predicate stored
    /// verbatim + a LIVE condition (target still on the battle area — the AS-IS <c>CanUseCondition</c>).
    /// The AS-IS grant-time <c>CanNotBeAffected</c> guard is mirrored: an immune target refuses the grant.
    /// Synchronous (all ported Gain-commons are; the AS-IS coroutine only drove UI). Returns true when the
    /// grant registered.</summary>
    public static bool GainCanNotBeDeletedByBattle(
        Permanent targetPermanent,
        Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition,
        EffectDuration effectDuration,
        ICardEffect activateClass,
        string effectName)
    {
        // (R3-W3c-1) AS-IS 1:1 signature restored: the causing effect is the live `ICardEffect activateClass`
        // (GiveEffectToPermanent/CanNotBeDeletedByBattle.cs:11), NOT a bare source card. AS-IS :15-18 guards.
        if (targetPermanent is null)
        {
            return false;
        }

        if (activateClass is null || activateClass.EffectSourceCard is null)
        {
            return false;   // AS-IS :15-16.
        }

        CardSource sourceCard = activateClass.EffectSourceCard;   // AS-IS :18 `CardSource card = activateClass.EffectSourceCard`.
        EngineContext context = sourceCard.Context;
        HeadlessEntityId targetId = targetPermanent.InstanceId;
        var zones = (IZoneStateReader)context.ZoneMover;
        if (targetId.IsEmpty || !zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BattleArea).Contains(targetId))
        {
            return false;   // AS-IS :14 IsPermanentExistsOnBattleArea guard.
        }

        // (R3-W3c-1) AS-IS grant-time guard `!targetPermanent.TopCard.CanNotBeAffected(activateClass)` (AS-IS :26)
        // — rehomed from the registry gate (ContinuousImmunityGate.BlocksOpponentEffect, which read the joint
        // predicate registered by the OLD-model CanNotAffectedStaticEffect) to the AS-IS-literal live
        // ICanNotAffectedEffect scan (CardSource.CanNotBeAffected, R1-e). This is the RD-W3A-01 consumer-side
        // rehousing that unblocks the CanNotAffectedStaticEffect→CanNotAffectedClass factory flip.
        if (targetPermanent.TopCard.CanNotBeAffected(activateClass))
        {
            return false;
        }

        // (R3-W3c-2 / RD-W3B-BATTLEDEL-TESTWELD RESOLVED) BUCKET TRANSITION — AS-IS 1:1
        // (GiveEffect/GiveEffectToPermanent/CanNotBeDeletedByBattle.cs:36-47): build the
        // CanNotBeDestroyedByBattleClass via the factory (carrying the 4-arg battle predicate + the
        // PermanentCondition `permanent == targetPermanent` + the live CanUseCondition) and store it in the target's
        // duration bucket via AddEffectToPermanent(timing:None). Read back by BattleDeletionGate via
        // NewModelContinuousScan.HasCanNotBeDestroyedByBattle (BattleDeletionGate.cs:67 — the permanent.EffectList(None)
        // scan). Expiry is now the AS-IS reset site (HeadlessEndTurnCleanupFlow for the turn-end durations), NOT the
        // invented registry-expiry sweep. This retires the registry-lowering (0 card callers; the G9-054 test is
        // re-aimed to drive the bucket cleanup).
        bool PermanentCondition(Permanent permanent) => permanent == targetPermanent;

        bool CanUseCondition()
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (!targetPermanent.TopCard.CanNotBeAffected(activateClass))
                {
                    return true;
                }
            }

            return false;
        }

        CardEffects.CanNotBeDestroyedByBattleClass canNotBeDestroyedByBattleClass = CardEffectFactory.CanNotBeDestroyedByBattleStaticEffect(
            canNotBeDestroyedByBattleCondition: canNotBeDestroyedByBattleCondition!,
            permanentCondition: PermanentCondition,
            isInheritedEffect: false,
            card: sourceCard,
            condition: CanUseCondition,
            effectName: effectName);

        AddEffectToPermanent(
            targetPermanent: targetPermanent,
            effectDuration: effectDuration,
            card: sourceCard,
            cardEffect: canNotBeDestroyedByBattleClass,
            timing: EffectTiming.None);
        return true;
    }

    // ===== (W6-S) "...AndProcessAccordingToResult" commons — 1:1 mirrors of CardEffectCommons.cs:437-644 =====
    // AS-IS shape: run the action via its I-class, then branch on whether it ACTUALLY happened (success =
    // real occurrence, not the attempt). The Delete form runs the FULL deletion pipeline — a target's
    // would-be-deleted replacement may respond across a game-loop pause, so the continuation parks on the
    // DeletionOutcomeWatcher context service (W6-S; the P6 parking generalised). The non-delete siblings
    // settle synchronously in the port. Original spelling ("Peremanent") kept for name parity.

    /// <summary>AS-IS <c>DeletePeremanentAndProcessAccordingToResult</c> (CardEffectCommons.cs:463-483).
    /// Success = at least one target ACTUALLY left the field (AS-IS <c>DestroyedPermanents</c> membership);
    /// <paramref name="successProcess"/> receives the destroyed permanents. Deferred targets (a would-be-
    /// deleted window opened) park the continuation until every target settles.</summary>
    public static async Task DeletePeremanentAndProcessAccordingToResult(
        IReadOnlyList<Permanent> targetPermanents,
        CardSource sourceCard,
        Func<IReadOnlyList<Permanent>, Task>? successProcess,
        Func<Task>? failureProcess,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetPermanents);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;

        var targets = targetPermanents.Where(p => p is not null && !p.InstanceId.IsEmpty).Select(p => p.InstanceId).ToList();
        if (targets.Count == 0)
        {
            if (failureProcess is not null)
            {
                await failureProcess().ConfigureAwait(false);
            }

            return;
        }

        var sink = new MatchStateMutationSink(
            context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
            context.EffectRegistry, context.GameEventQueue, context: context);
        foreach (HeadlessEntityId target in targets)
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.DeleteKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);

        DeletionOutcomeWatcher watcher = GetOutcomeWatcher(context);
        watcher.Register(targets, async (destroyed, spared) =>
        {
            if (destroyed.Count > 0)
            {
                if (successProcess is not null)
                {
                    IReadOnlyList<Permanent> views = destroyed
                        .Select(id => new Permanent(context, id, OwnerOfInstance(context, id)))
                        .ToArray();
                    await successProcess(views).ConfigureAwait(false);
                }
            }
            else if (failureProcess is not null)
            {
                await failureProcess().ConfigureAwait(false);
            }
        });

        // Settle immediately when nothing deferred (the common case: no replacement windows opened).
        await watcher.SettleAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>SuspendPeremanentAndProcessAccordingToResult</c> (CardEffectCommons.cs:437):
    /// suspend the targets, then branch on whether any ACTUALLY became suspended.</summary>
    public static async Task SuspendPeremanentAndProcessAccordingToResult(
        IReadOnlyList<Permanent> targetPermanents,
        CardSource sourceCard,
        Func<IReadOnlyList<Permanent>, Task>? successProcess,
        Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(targetPermanents);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;

        var sink = new MatchStateMutationSink(
            context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
            context.EffectRegistry, context.GameEventQueue, context: context);
        foreach (Permanent target in targetPermanents.Where(p => p is not null && !p.InstanceId.IsEmpty))
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.SuspendKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.InstanceId.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);

        IReadOnlyList<Permanent> suspended = targetPermanents
            .Where(p => p is not null && !p.InstanceId.IsEmpty && IsSuspended(p.TopCard, p.InstanceId))
            .ToArray();
        if (suspended.Count > 0)
        {
            if (successProcess is not null)
            {
                await successProcess(suspended).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>BouncePeremanentAndProcessAccordingToResult</c> (CardEffectCommons.cs:489):
    /// return the targets to hand, then branch on whether any ACTUALLY left the field.</summary>
    public static async Task BouncePeremanentAndProcessAccordingToResult(
        IReadOnlyList<Permanent> targetPermanents,
        CardSource sourceCard,
        Func<Task>? successProcess,
        Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(targetPermanents);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;

        var sink = new MatchStateMutationSink(
            context.CardInstanceRepository, log: null, context.ZoneMover, memory: null,
            context.EffectRegistry, context.GameEventQueue, context: context);
        foreach (Permanent target in targetPermanents.Where(p => p is not null && !p.InstanceId.IsEmpty))
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.ReturnToHandKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.InstanceId.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);

        var zones = (IZoneStateReader)context.ZoneMover;
        bool bounced = targetPermanents.Any(p => p is not null && !p.InstanceId.IsEmpty
            && !zones.GetCards(p.OwnerId, ChoiceZone.BattleArea).Contains(p.InstanceId));
        if (bounced)
        {
            if (successProcess is not null)
            {
                await successProcess().ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>DeckBouncePeremanentAndProcessAccordingToResult</c> (CardEffectCommons.cs:515):
    /// return the targets to the deck bottom; success = any actually left the field.</summary>
    public static async Task DeckBouncePeremanentAndProcessAccordingToResult(
        IReadOnlyList<Permanent> targetPermanents, CardSource sourceCard,
        Func<Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(targetPermanents);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var sink = NewSink(context);
        foreach (Permanent target in targetPermanents.Where(p => p is not null && !p.InstanceId.IsEmpty))
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.ReturnToDeckBottomKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.InstanceId.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);
        var zones = (IZoneStateReader)context.ZoneMover;
        bool bounced = targetPermanents.Any(p => p is not null && !p.InstanceId.IsEmpty
            && !zones.GetCards(p.OwnerId, ChoiceZone.BattleArea).Contains(p.InstanceId));
        await Branch(bounced, successProcess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>TrashDigivolutionCardsAndProcessAccordingToResult</c> (CardEffectCommons.cs:541):
    /// trash <paramref name="trashCount"/> digivolution sources; success = any actually trashed.</summary>
    public static async Task TrashDigivolutionCardsAndProcessAccordingToResult(
        Permanent? targetPermanent, int trashCount, bool isFromTop, CardSource sourceCard,
        Func<int, Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        int trashed = targetPermanent is null || targetPermanent.InstanceId.IsEmpty
            // (C-3 재상환 P2-2) AS-IS routes through ITrashDigivolutionCards.TrashDigivolutionCards, which
            // yield-breaks on the host's ImmuneFromStackTrashing AND the top card's general CanNotBeAffected
            // (CardController.cs:5154-5156) — the same gates the sink path applies; this direct-call mirror
            // must not bypass them.
            || IsHostStackTrashGated(targetPermanent.InstanceId, sourceCard)
            ? 0
            : await Headless.Runtime.DigivolutionStackHelpers.TrashSourcesAsync(
                sourceCard.Context.CardInstanceRepository, sourceCard.Context.ZoneMover,
                targetPermanent.InstanceId, trashCount, fromBottom: !isFromTop,
                gameEventQueue: sourceCard.Context.GameEventQueue,
                // (C-3) effect-trash honours CanNotTrashFromDigivolutionCards (BT9_109) via TrashProtectionScan.
                effectRegistry: sourceCard.Context.EffectRegistry, context: sourceCard.Context,
                causingEffectSourceId: sourceCard.InstanceId).ConfigureAwait(false);
        if (trashed > 0)
        {
            if (successProcess is not null)
            {
                await successProcess(trashed).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>TrashDigivolutionCardsFromTopOrBottom</c> (GiveEffect …, 121 card files): the plain
    /// trash (no success branch).</summary>
    public static Task<int> TrashDigivolutionCardsFromTopOrBottom(
        Permanent? targetPermanent, int trashCount, bool isFromTop, CardSource sourceCard) =>
        targetPermanent is null || targetPermanent.InstanceId.IsEmpty
            // (C-3 재상환 P2-2) AS-IS TrashDigivolutionCardsFromTopOrBottom pre-gates on the top card's
            // CanNotBeAffected (CardEffectCommons.cs:681) and then ITrashDigivolutionCards re-gates
            // ImmuneFromStackTrashing + CanNotBeAffected (CardController.cs:5154-5156). (Its trashable-count==0
            // pre-gate, :680, is result-equivalent to the helper's protection-filtered window trashing nothing.)
            || IsHostStackTrashGated(targetPermanent.InstanceId, sourceCard)
            ? Task.FromResult(0)
            : Headless.Runtime.DigivolutionStackHelpers.TrashSourcesAsync(
                sourceCard.Context.CardInstanceRepository, sourceCard.Context.ZoneMover,
                targetPermanent.InstanceId, trashCount, fromBottom: !isFromTop,
                gameEventQueue: sourceCard.Context.GameEventQueue,
                // (C-3) effect-trash honours CanNotTrashFromDigivolutionCards (BT9_109) via TrashProtectionScan.
                effectRegistry: sourceCard.Context.EffectRegistry, context: sourceCard.Context,
                causingEffectSourceId: sourceCard.InstanceId);

    // (C-3 재상환 P2-2) The stack-trash gates of the sink path (MatchStateMutationSink, TrashDigivolutionCardsKind):
    // ImmuneFromStackTrashing (continuous ImmuneStackTrashingKey restriction honouring cardEffectCondition) OR the
    // host top card's general effect immunity (CanNotBeAffected). Mirrors AS-IS ITrashDigivolutionCards
    // (CardController.cs:5154-5156) for the helpers-direct mirrors that skip the sink.
    private static bool IsHostStackTrashGated(HeadlessEntityId hostId, CardSource sourceCard) =>
        // (R3-W3c B6) ImmuneFromStackTrashing rehomed from the ImmuneStackTrashingKey registry scan to the
        // AS-IS-literal live getter (host permanent, cause = the causing effect collapsed to its source card).
        new Permanent(sourceCard.Context, hostId).ImmuneFromStackTrashing(BareCauseEffect.For(sourceCard.Context, sourceCard.InstanceId))
        // (B군 P0-1) The general-immunity OR-arm (AS-IS CardController.cs:5155 TopCard.CanNotBeAffected) is likewise
        // rehomed from the now-dead BlocksOpponentEffect registry scan to the live TopCard.CanNotBeAffected getter,
        // symmetric with the sink stack-trash gate (MatchStateMutationSink :1959) — B6 parked this C-arm for the
        // CanNotBeAffected batch (no partial flip).
        || new Permanent(sourceCard.Context, hostId).TopCard.CanNotBeAffected(BareCauseEffect.For(sourceCard.Context, sourceCard.InstanceId));

    /// <summary>AS-IS <c>TrashLinkCardsAndProcessAccordingToResult</c> (CardEffectCommons.cs:567): trash the
    /// given link cards off their host; success = any actually trashed.</summary>
    public static async Task TrashLinkCardsAndProcessAccordingToResult(
        Permanent? hostPermanent, IReadOnlyList<HeadlessEntityId> linkCardIds, CardSource sourceCard,
        Func<int, Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(linkCardIds);
        ArgumentNullException.ThrowIfNull(sourceCard);
        int trashed = 0;
        if (hostPermanent is not null && !hostPermanent.InstanceId.IsEmpty)
        {
            foreach (HeadlessEntityId linkCard in linkCardIds)
            {
                if (await Headless.Runtime.LinkHelpers.RemoveLinkCardAsync(
                        sourceCard.Context.CardInstanceRepository, sourceCard.Context.ZoneMover,
                        hostPermanent.InstanceId, linkCard).ConfigureAwait(false))
                {
                    trashed++;
                }
            }
        }

        if (trashed > 0)
        {
            if (successProcess is not null)
            {
                await successProcess(trashed).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>TrashSecurityAndProcessAccordingToResult</c> (CardEffectCommons.cs:593): trash
    /// <paramref name="trashAmount"/> of <paramref name="player"/>'s security (top/bottom); success = any
    /// actually trashed.</summary>
    public static async Task TrashSecurityAndProcessAccordingToResult(
        HeadlessPlayerId player, int trashAmount, bool fromTop, CardSource sourceCard,
        Func<int, Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        int before = zones.GetCards(player, ChoiceZone.Security).Count;
        var sink = NewSink(context);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.TrashSecurityKind, sourceCard.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.PlayerIdKey] = player.Value,
                [MatchStateMutationSink.CountKey] = trashAmount,
                [MatchStateMutationSink.FromTopKey] = fromTop,
            }));
        await sink.FlushAsync().ConfigureAwait(false);
        int trashed = before - zones.GetCards(player, ChoiceZone.Security).Count;
        if (trashed > 0)
        {
            if (successProcess is not null)
            {
                await successProcess(trashed).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>AS-IS <c>TrashHandAndProcessAccordingToResult</c> (CardEffectCommons.cs:619): discard a
    /// specific hand card; success = it actually reached the trash.</summary>
    public static async Task TrashHandAndProcessAccordingToResult(
        CardSource? handCard, CardSource sourceCard,
        Func<Task>? successProcess, Func<Task>? failureProcess)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        bool discarded = false;
        if (handCard is not null && !handCard.InstanceId.IsEmpty &&
            zones.GetCards(handCard.Owner, ChoiceZone.Hand).Contains(handCard.InstanceId))
        {
            var sink = NewSink(context);
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.TrashCardKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = handCard.InstanceId.Value }));
            await sink.FlushAsync().ConfigureAwait(false);
            discarded = zones.GetCards(handCard.Owner, ChoiceZone.Trash).Contains(handCard.InstanceId);
        }

        await Branch(discarded, successProcess, failureProcess).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>PlacePermanentInSecurityAndProcessAccordingToResult</c> (CardEffectCommons.cs:644):
    /// put the target permanent's TOP CARD into its owner's security (top/bottom, face-up/down per
    /// <paramref name="isFaceUp"/>), gated by the owner's <c>CanAddSecurity</c>; success = actually placed.
    /// Routed through the sink's AddToSecurity mutation so BOTH the AS-IS <c>isFaceUp</c> flag AND the
    /// CanAddSecurity restriction (previously bypassed by the direct ZoneMover call) are honoured, and a face-up
    /// add opens the OnFaceUpSecurityIncreased window.</summary>
    public static async Task PlacePermanentInSecurityAndProcessAccordingToResult(
        Permanent? targetPermanent, bool toTop, CardSource sourceCard,
        Func<CardSource, Task>? successProcess, Func<Task>? failureProcess, bool isFaceUp = false)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        bool placed = false;
        CardSource? placedTop = null;
        if (targetPermanent is not null && !targetPermanent.InstanceId.IsEmpty &&
            zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BattleArea).Contains(targetPermanent.InstanceId))
        {
            HeadlessEntityId topId = targetPermanent.InstanceId;
            var sink = NewSink(context);
            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = topId.Value,
            };
            if (isFaceUp)
            {
                values[MatchStateMutationSink.FaceUpKey] = true;
            }

            if (!toTop)
            {
                values[MatchStateMutationSink.ToBottomKey] = true;
            }

            sink.Apply(new EffectMutation(MatchStateMutationSink.AddToSecurityKind, sourceCard.InstanceId, values));
            await sink.FlushAsync().ConfigureAwait(false);
            placed = zones.GetCards(targetPermanent.OwnerId, ChoiceZone.Security).Contains(topId);
            placedTop = new CardSource(context, topId, targetPermanent.OwnerId, targetPermanent.OwnerId);
        }

        if (placed && placedTop is not null)
        {
            if (successProcess is not null)
            {
                await successProcess(placedTop).ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    /// <summary>(W6-D) AS-IS <c>PlaceDelayOptionCards</c> (CardEffectCommons.cs:113-134): play the [Delay]
    /// Option COST-FREE as a face-up permanent on the owner's battle area (gated by
    /// <see cref="CanPlayAsNewPermanent"/> with <c>isPlayOption:true</c>), then tag
    /// <c>IsPlayedOptionPermanent</c> — the tag alone exempts it from the "Option with no DP → trash" rule
    /// (P7 models that exemption). The [Delay] ability itself is an ordinary OnDeclaration activated skill
    /// gated by <see cref="CanDeclareOptionDelayEffect"/>; its resolution typically self-deletes via
    /// <see cref="DeletePeremanentAndProcessAccordingToResult"/>. Returns true when placed.</summary>
    public static async Task<bool> PlaceDelayOptionCards(CardSource card, ICardEffect? cardEffect = null, ChoiceZone root = ChoiceZone.Execution)
    {
        ArgumentNullException.ThrowIfNull(card);
        _ = cardEffect;
        if (!CanPlayAsNewPermanent(card, payCost: false, null, isPlayOption: true))
        {
            return false;
        }

        EngineContext context = card.Context;
        var sink = NewSink(context);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.PlayCardKind, card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value,
                [MatchStateMutationSink.FromZoneKey] = root,
            }));
        await sink.FlushAsync().ConfigureAwait(false);

        var zones = (IZoneStateReader)context.ZoneMover;
        if (!zones.GetCards(card.Owner, ChoiceZone.BattleArea).Contains(card.InstanceId))
        {
            return false;
        }

        if (context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? record) && record is not null)
        {
            context.CardInstanceRepository.Upsert(record with
            {
                Metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal)
                {
                    [Headless.Runtime.GameFlowProcessor.IsPlayedOptionPermanentKey] = true,
                }
            });
        }

        return true;
    }

    private static MatchStateMutationSink NewSink(EngineContext context) =>
        // Passing `context` means an effect-driven play through this sink auto-registers the entered card's
        // ported continuous/trigger effects (MatchStateMutationSink defaults its enter-play hook to
        // context.RegisterEnteredCardEffects) — the AS-IS PlayCardClass.PlayCard() enter-play semantics.
        new(context.CardInstanceRepository, log: null, context.ZoneMover, memory: context.MemoryController,
            context.EffectRegistry, context.GameEventQueue, context: context);

    private static async Task Branch(bool success, Func<Task>? successProcess, Func<Task>? failureProcess)
    {
        if (success)
        {
            if (successProcess is not null)
            {
                await successProcess().ConfigureAwait(false);
            }
        }
        else if (failureProcess is not null)
        {
            await failureProcess().ConfigureAwait(false);
        }
    }

    private static DeletionOutcomeWatcher GetOutcomeWatcher(EngineContext context)
    {
        if (context.TryGetService(out DeletionOutcomeWatcher? watcher) && watcher is not null)
        {
            return watcher;
        }

        var created = new DeletionOutcomeWatcher();
        context.RegisterService(created);
        return created;
    }

    private static HeadlessPlayerId OwnerOfInstance(EngineContext context, HeadlessEntityId id) =>
        context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) && record is not null
            ? record.OwnerId
            : default;

    // ===== (W6-T) trigger-gate commons batch — 1:1 mirrors of CanUseEffects/*.cs =====
    // The AS-IS gates read the driving Hashtable; the port mirror reads the enriched resolve context
    // (subject = TriggerEntityId; the event's primitive metadata under "event.<key>" —
    // GameFlowProcessor.EnrichWithEventSubject, W6-T). Verbatim AS-IS bodies verified
    // (primitive_w6_design.md W6-T). Translation: `CanActivateCondition(Hashtable h)` bodies become
    // `triggerGate: ctx => CardEffectCommons.CanTriggerX(ctx, card, ...) && ...` with the same names.

    /// <summary>AS-IS <c>CanTriggerOnPlay</c> (CanUseEffects/PermanentEnterField/OnPlay.cs:11): the entered
    /// permanent CONTAINS this card and the entry was a PLAY (not a digivolve).
    /// <paramref name="rootCondition"/> = the AS-IS Root filter over the source zone (headless: the
    /// event's from-zone).</summary>
    public static bool CanTriggerOnPlay(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<ChoiceZone, bool>? rootCondition = null) =>
        !EventIsDigivolve(ctx) && SubjectPermanentContains(ctx, card) && EventRootPasses(ctx, rootCondition);

    /// <summary>AS-IS <c>CanTriggerWhenDigivolving</c> (.../WhenDigivolving.cs:10).</summary>
    public static bool CanTriggerWhenDigivolving(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<ChoiceZone, bool>? rootCondition = null) =>
        EventIsDigivolve(ctx) && SubjectPermanentContains(ctx, card) && EventRootPasses(ctx, rootCondition);

    /// <summary>AS-IS <c>CanTriggerOnPermanentPlay</c> (.../OnPlay.cs:18) — arbitrary predicate over the
    /// ENTERED permanent.</summary>
    public static bool CanTriggerOnPermanentPlay(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, Func<ChoiceZone, bool>? rootCondition = null) =>
        !EventIsDigivolve(ctx) && SubjectPermanentPasses(ctx, card, permanentCondition) && EventRootPasses(ctx, rootCondition);

    /// <summary>AS-IS <c>CanTriggerWhenPermanentDigivolving</c> (.../WhenDigivolving.cs:17).</summary>
    public static bool CanTriggerWhenPermanentDigivolving(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, Func<ChoiceZone, bool>? rootCondition = null) =>
        EventIsDigivolve(ctx) && SubjectPermanentPasses(ctx, card, permanentCondition) && EventRootPasses(ctx, rootCondition);

    /// <summary>AS-IS <c>CanTriggerOnAttack</c> (CanUseEffects/OnAttack.cs:10): the ATTACKING permanent
    /// contains this card.</summary>
    public static bool CanTriggerOnAttack(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerOnPermanentAttack</c> (.../OnAttack.cs:17).</summary>
    public static bool CanTriggerOnPermanentAttack(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        SubjectPermanentPasses(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>CanTriggerOnEndAttack</c> (.../OnEndAttack.cs:10) — delegates to the attack gate.</summary>
    public static bool CanTriggerOnEndAttack(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanTriggerOnAttack(ctx, card);

    /// <summary>AS-IS <c>CanTriggerOptionMainEffect</c> (CanUseEffects/OptionEffect.cs:10): the resolving
    /// card IS this card.</summary>
    public static bool CanTriggerOptionMainEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && subject == card.InstanceId;

    /// <summary>AS-IS <c>CanTriggerSecurityEffect</c> (CanUseEffects/SecurityEffect.cs:10) — delegates to
    /// the option-main gate.</summary>
    public static bool CanTriggerSecurityEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanTriggerOptionMainEffect(ctx, card);

    /// <summary>AS-IS <c>CanTriggerOnDeletion</c> (CanUseEffects/OnDeletion.cs:13): the deleted permanent
    /// contained this card.</summary>
    public static bool CanTriggerOnDeletion(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanActivateOnDeletion</c> (CanUseEffects/OnDeletion.cs:113): a token activates
    /// unconditionally; otherwise the permanent this card belonged to just before leaving the field is the
    /// deletion subject AND its top card is in the trash (a true deletion — a bounce fails this).</summary>
    public static bool CanActivateOnDeletion(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (card.IsToken)
        {
            return true;
        }

        if (!SubjectPermanentContains(ctx, card) ||
            ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject)
        {
            return false;
        }

        // AS-IS: return IsExistOnTrash(TopCard) — the deleted permanent's top card actually reached the trash.
        HeadlessPlayerId owner = card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null
            ? dead.OwnerId
            : default;
        return !owner.IsEmpty
            && ((IZoneStateReader)card.Context.ZoneMover).GetCards(owner, ChoiceZone.Trash).Contains(subject);
    }

    /// <summary>AS-IS <c>CanTriggerWhenLoseSecurity</c> (CanUseEffects/WhenLoseSecurity.cs:10): the
    /// security-losing PLAYER (headless: the moved security card's owner = the event subject's owner)
    /// passes <paramref name="playerCondition"/>.</summary>
    public static bool CanTriggerWhenLoseSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<HeadlessPlayerId, bool>? playerCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty ||
            !card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? record) || record is null)
        {
            return false;
        }

        return playerCondition is null || playerCondition(record.OwnerId);
    }

    /// <summary>AS-IS <c>CanTriggerWhenRemoveField</c> (CanUseEffects/WhenRemoveField.cs:11).</summary>
    public static bool CanTriggerWhenRemoveField(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerWhenPermanentRemoveField</c> (.../WhenRemoveField.cs:19).</summary>
    public static bool CanTriggerWhenPermanentRemoveField(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        SubjectPermanentPasses(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>CanTriggerWhenPermanentSuspends</c> (CanUseEffects/OnSuspend.cs:17).</summary>
    public static bool CanTriggerWhenPermanentSuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        SubjectPermanentPasses(ctx, card, permanentCondition, requireOnBattleArea: true);

    /// <summary>AS-IS <c>IsByEffect</c> (CanUseEffects/OnDeletion.cs:89): the deletion was caused by an
    /// EFFECT (the AS-IS hashtable carried a CardEffect) — headless the dead card's metadata carries the
    /// <c>deletedByEffect</c> flag + the causing source card id; the AS-IS
    /// <c>Func&lt;ICardEffect,bool&gt;</c> condition maps to a predicate over that source card.</summary>
    public static bool IsByEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty ||
            !card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) || dead is null ||
            !dead.Metadata.TryGetValue(MatchStateMutationSink.DeletedByEffectKey, out object? raw) || raw is not true)
        {
            return false;
        }

        if (cardEffectSourceCondition is null)
        {
            return true;
        }

        if (!dead.Metadata.TryGetValue(MatchStateMutationSink.DeletedBySourceEntityIdKey, out object? rawSource) ||
            rawSource?.ToString() is not { Length: > 0 } sourceValue)
        {
            return false;
        }

        var sourceId = new HeadlessEntityId(sourceValue);
        HeadlessPlayerId sourceOwner = card.Context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? src) && src is not null
            ? src.OwnerId
            : default;
        return cardEffectSourceCondition(new CardSource(card.Context, sourceId, sourceOwner, sourceOwner));
    }

    /// <summary>AS-IS <c>IsJogress</c> (GetFromHashtable.cs:782): the driving event carried the DNA flag.</summary>
    public static bool IsJogress(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isJogress", out object? raw) && raw is true;

    /// <summary>AS-IS <c>CanTriggerWhenPermanentWouldPlay</c> (CanUseEffects/WhenPermanentWouldPlay.cs:11):
    /// a card is about to be PLAYED (not digivolved) — headless the BeforePayCost window (the EX8_074
    /// "would be played" seam), the event carrying <c>isEvolution:false</c>.</summary>
    public static bool CanTriggerWhenPermanentWouldPlay(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)
    {
        if (EventIsDigivolve(ctx) || ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        return cardCondition is null
            || cardCondition(new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject)));
    }

    /// <summary>AS-IS <c>CanTriggerWhenPermanentWouldDigivolve</c> (…/WhenPermanentWouldDigivolve.cs:23):
    /// a card is about to DIGIVOLVE — the event carries <c>isEvolution:true</c> + the target permanent.</summary>
    public static bool CanTriggerWhenPermanentWouldDigivolve(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? cardCondition = null)
    {
        if (!EventIsDigivolve(ctx) || ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        if (cardCondition is not null &&
            !cardCondition(new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject))))
        {
            return false;
        }

        if (permanentCondition is null)
        {
            return true;
        }

        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}targetCardId", out object? raw) ||
            raw?.ToString() is not { Length: > 0 } targetValue)
        {
            return false;
        }

        var targetId = new HeadlessEntityId(targetValue);
        return permanentCondition(new Permanent(card.Context, targetId, OwnerOfId(card.Context, targetId)));
    }

    /// <summary>AS-IS <c>CanTriggerWhenLinked</c> (CanUseEffects/WhenLinked.cs:45): a link attached — the
    /// HOST passes <paramref name="permanentCondition"/> and the LINK CARD passes
    /// <paramref name="sourceCondition"/>.</summary>
    public static bool CanTriggerWhenLinked(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition = null, Func<CardSource, bool>? sourceCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host || host.IsEmpty)
        {
            return false;
        }

        if (permanentCondition is not null &&
            !permanentCondition(new Permanent(card.Context, host, OwnerOfId(card.Context, host))))
        {
            return false;
        }

        if (sourceCondition is null)
        {
            return true;
        }

        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}linkCardId", out object? raw) ||
            raw?.ToString() is not { Length: > 0 } linkValue)
        {
            return false;
        }

        var linkId = new HeadlessEntityId(linkValue);
        return sourceCondition(new CardSource(card.Context, linkId, OwnerOfId(card.Context, linkId), OwnerOfId(card.Context, linkId)));
    }

    /// <summary>AS-IS <c>CanTriggerOnAddDigivolutionCard</c> (CanUseEffects/OnAddDigivolutionCards.cs:10):
    /// digivolution sources were added — the receiving permanent, the causing effect's source, and at
    /// least one added card pass their predicates.</summary>
    public static bool CanTriggerOnAddDigivolutionCard(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition = null,
        Func<CardSource, bool>? cardEffectSourceCondition = null,
        Func<CardSource, bool>? cardCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host || host.IsEmpty)
        {
            return false;
        }

        EngineContext context = card.Context;
        if (permanentCondition is not null && !permanentCondition(new Permanent(context, host, OwnerOfId(context, host))))
        {
            return false;
        }

        // (F1-Tier2) AS-IS OnAddDigivolutionCards.cs:24 hard-requires `CardEffect != null` — the add MUST be
        // effect-driven. The causing effect's SOURCE id is carried on the event as `causeSourceId` (distinct from
        // subject=host). A natural-digivolve remnant or an Assembly-style add (AS-IS `AddDigivolutionCardsBottom(card,
        // null)`) carries no cause, so EVERY reactor gates false here. (Previously this read SourceEntityId, which the
        // emit set to the HOST — never the cause — so cardEffectSourceCondition evaluated the wrong card and the
        // mandatory-cause requirement was missing entirely.)
        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}causeSourceId", out object? causeRaw) ||
            causeRaw?.ToString() is not { Length: > 0 } causeValue)
        {
            return false;
        }

        var causeId = new HeadlessEntityId(causeValue);
        if (cardEffectSourceCondition is not null &&
            !cardEffectSourceCondition(new CardSource(context, causeId, OwnerOfId(context, causeId), OwnerOfId(context, causeId))))
        {
            return false;
        }

        if (cardCondition is null)
        {
            return true;
        }

        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}addedCardIds", out object? raw) ||
            raw?.ToString() is not { Length: > 0 } addedValue)
        {
            return false;
        }

        return addedValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(id => cardCondition(new CardSource(context, new HeadlessEntityId(id), OwnerOfId(context, new HeadlessEntityId(id)), OwnerOfId(context, new HeadlessEntityId(id)))));
    }

    /// <summary>AS-IS <c>CanTriggerOnMove</c> (CanUseEffects/OnMove.cs:10, verbatim): the moved permanent is
    /// STILL on the battle area (AS-IS <c>IsPermanentExistsOnBattleArea(permanent)</c>) AND passes the predicate.
    /// The battle-area guard is load-bearing 1:1 — a promotion subject that has already been removed (deleted by a
    /// concurrent effect before this reactor's pass) must gate FALSE (AS-IS returns false), so pass
    /// <c>requireOnBattleArea: true</c> rather than relying on the per-card predicate to re-check membership.</summary>
    public static bool CanTriggerOnMove(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null) =>
        SubjectPermanentPasses(ctx, card, permanentCondition, requireOnBattleArea: true);

    /// <summary>AS-IS <c>IsByBattle</c>: the deletion driving this window came from a BATTLE — headless the
    /// dead card carries the <c>deletedByBattle</c> marker (BattleResolver).</summary>
    public static bool IsByBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        return ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty &&
            card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null &&
            dead.Metadata.TryGetValue(BattleResolver.DeletedByBattleKey, out object? raw) && raw is true;
    }

    // --- (W6-T) hashtable-accessor mirrors — the event subject IS the AS-IS hashtable payload ---------

    /// <summary>AS-IS <c>GetPermanentFromHashtable</c> (GetFromHashtable.cs:700): the event subject as a
    /// Permanent view.</summary>
    public static Permanent? GetPermanentFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty
            ? new Permanent(card.Context, subject, OwnerOfId(card.Context, subject))
            : null;

    /// <summary>AS-IS <c>GetPermanentsFromHashtable</c> (:500) — headless events carry ONE subject per
    /// firing (broadcast timings fire per permanent), so the list has at most one element.</summary>
    public static List<Permanent> GetPermanentsFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        GetPermanentFromHashtable(ctx, card) is Permanent p ? new List<Permanent> { p } : new List<Permanent>();

    /// <summary>AS-IS <c>GetCardFromHashtable</c> (:316): the event subject as a CardSource.</summary>
    public static CardSource? GetCardFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty
            ? new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject))
            : null;

    /// <summary>AS-IS <c>GetPlayedPermanentsFromEnterFieldHashtable</c> (:234): the entered permanent(s)
    /// whose play ROOT (headless: the event's from-zone) passes the filter.</summary>
    public static List<Permanent> GetPlayedPermanentsFromEnterFieldHashtable(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<ChoiceZone, bool>? rootCondition = null) =>
        EventRootPasses(ctx, rootCondition) ? GetPermanentsFromHashtable(ctx, card) : new List<Permanent>();

    private static HeadlessPlayerId OwnerOfId(EngineContext context, HeadlessEntityId id) =>
        context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) && record is not null
            ? record.OwnerId
            : default;

    /// <summary>AS-IS <c>CanTriggerWhenDeleteOpponentDigimonByBattle</c>
    /// (CanUseEffects/WhenDeleteOpponentDigimonByBattle.cs:10, verbatim verified): reads the battle result
    /// (winners / losers / actually-destroyed) — headless the OnEndBattle event carries them
    /// (winnerIds/loserIds/loserRealIds). Headless winners are the SURVIVORS, so the AS-IS *_real
    /// distinction collapses (replacement survivors never enter the deleted set) — documented reduction.</summary>
    public static bool CanTriggerWhenDeleteOpponentDigimonByBattle(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? winnerCondition,
        Func<Permanent, bool>? loserCondition,
        bool isOnlyWinnerSurvive,
        Func<Permanent, bool>? winnerRealCondition = null,
        Func<Permanent, bool>? loserRealCondition = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        EngineContext context = card.Context;
        IReadOnlyList<Permanent> winners = EventPermanents(ctx, context, "winnerIds");
        IReadOnlyList<Permanent> losers = EventPermanents(ctx, context, "loserIds");
        IReadOnlyList<Permanent> losersReal = EventPermanents(ctx, context, "loserRealIds");

        // AS-IS WinnerCondition(): empty winners passes only an absent condition; otherwise some winner matches.
        bool winnerOk = winners.Count == 0
            ? winnerCondition is null
            : winners.Any(p => winnerCondition is null || winnerCondition(p));
        if (!winnerOk)
        {
            return false;
        }

        // AS-IS isOnlyWinnerSurvive: no LOSER may also satisfy the winner condition.
        if (isOnlyWinnerSurvive && winnerCondition is not null &&
            losers.Any(p => winnerCondition(p)))
        {
            return false;
        }

        if (loserCondition is not null && !losers.Any(p => loserCondition(p)))
        {
            return false;
        }

        if (loserRealCondition is not null && !losersReal.Any(p => loserRealCondition(p)))
        {
            return false;
        }

        if (winnerRealCondition is not null && !winners.Any(p => winnerRealCondition(p)))
        {
            return false;
        }

        return true;
    }

    /// <summary>AS-IS <c>CanTriggerWhenWinBattle</c>: this card's permanent is among the battle's winners.</summary>
    public static bool CanTriggerWhenWinBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        EventPermanents(ctx, card.Context, "winnerIds").Any(p =>
            p.InstanceId == card.InstanceId ||
            (card.Context.CardInstanceRepository.TryGetInstance(p.InstanceId, out CardInstanceRecord? rec) && rec is not null
                && rec.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw)
                && raw is IEnumerable<string> ids && ids.Contains(card.InstanceId.Value)));

    private static IReadOnlyList<Permanent> EventPermanents(Headless.Effects.CardEffectResolveContext ctx, EngineContext context, string key)
    {
        // (F1-M0-2) reads the CSV-of-id-values collection payload via the shared flattening convention helper
        // (mirror of the emit-side EventCollectionMetadata.Flatten). Same split semantics as the prior inline
        // Split — behavior-neutral.
        return Headless.Effects.EventCollectionMetadata.ReadIds(ctx.EffectContext.Values, key)
            .Select(id => new Permanent(context, id, OwnerOfId(context, id)))
            .ToArray();
    }

    /// <summary>AS-IS <c>CanTriggerWhenLinking</c> (WhenLinked.cs:10): a WOULD-LINK window where the LINK
    /// card is this card and the HOST passes the predicate.</summary>
    public static bool CanTriggerWhenLinking(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host || host.IsEmpty)
        {
            return false;
        }

        if (permanentCondition is not null &&
            !permanentCondition(new Permanent(card.Context, host, OwnerOfId(card.Context, host))))
        {
            return false;
        }

        return ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}linkCardId", out object? raw)
            && raw?.ToString() == card.InstanceId.Value;
    }

    /// <summary>AS-IS <c>CanTriggerWhenWouldLink</c> (WhenWouldLink.cs:11).</summary>
    public static bool CanTriggerWhenWouldLink(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<CardSource, bool>? cardCondition = null, Func<Permanent, bool>? permanentCondition = null,
        Func<ChoiceZone, bool>? rootCondition = null, Func<CardSource, bool>? cardEffectSourceCondition = null)
    {
        EngineContext context = card.Context;
        HeadlessEntityId linkId = ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}linkCardId", out object? rawLink)
            && rawLink?.ToString() is { Length: > 0 } linkValue
            ? new HeadlessEntityId(linkValue)
            : ctx.EffectContext.TriggerEntityId ?? default;
        if (linkId.IsEmpty)
        {
            return false;
        }

        if (cardCondition is not null && !cardCondition(new CardSource(context, linkId, OwnerOfId(context, linkId), OwnerOfId(context, linkId))))
        {
            return false;
        }

        if (permanentCondition is not null &&
            (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host ||
             !permanentCondition(new Permanent(context, host, OwnerOfId(context, host)))))
        {
            return false;
        }

        if (!EventRootPasses(ctx, rootCondition))
        {
            return false;
        }

        return cardEffectSourceCondition is null
            || cardEffectSourceCondition(new CardSource(context, ctx.EffectContext.SourceEntityId, OwnerOfId(context, ctx.EffectContext.SourceEntityId), OwnerOfId(context, ctx.EffectContext.SourceEntityId)));
    }

    /// <summary>AS-IS <c>CanTriggerOnTrashHand</c> (OnTrashHand.cs:17): a hand card was discarded BY AN EFFECT
    /// (<c>CardEffect != null</c>) — the causing effect's source and at least one discarded card pass their
    /// predicates. The AS-IS payload is {DiscardedCards, CardEffect}; headless threads the discarded card as the
    /// event subject and the causing effect's source card id as <c>event.discardCauseEffectId</c> (stamped on the
    /// effect-driven trash by <c>MatchStateMutationSink.ApplyTrashCard</c> / <c>TrashSecurityAsync</c>). A NON-effect
    /// trash (attack security-CHECK reveal, hand-size trim) carries NO cause id, so — like AS-IS <c>CardEffect ==
    /// null</c> — it is rejected here regardless of the predicates.</summary>
    public static bool CanTriggerOnTrashHand(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition)
    {
        // AS-IS: CardEffect != null (the discard must be effect-driven) — the cause effect's source card.
        CardSource? cause = DiscardCauseEffect(ctx, card);
        if (cause is null)
        {
            return false;
        }

        if (cardEffectSourceCondition is not null && !cardEffectSourceCondition(cause))
        {
            return false;
        }

        return EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));
    }

    /// <summary>The causing effect's <c>EffectSourceCard</c> (AS-IS hashtable <c>CardEffect</c>) for an effect-driven
    /// discard, threaded as <c>event.discardCauseEffectId</c>; null when the trash carried no effect cause (mirrors
    /// <c>CardEffect == null</c>).</summary>
    private static CardSource? DiscardCauseEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}{Headless.Effects.MatchStateMutationSink.DiscardCauseEffectIdKey}", out object? raw)
            || raw?.ToString() is not { Length: > 0 } value)
        {
            return null;
        }

        var id = new HeadlessEntityId(value);
        EngineContext context = card.Context;
        return new CardSource(context, id, OwnerOfId(context, id), OwnerOfId(context, id));
    }

    /// <summary>AS-IS <c>CanTriggerOnTrashSelfHand</c> (OnTrashHand.cs:10).</summary>
    public static bool CanTriggerOnTrashSelfHand(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null) =>
        CanTriggerOnTrashHand(ctx, card, cardEffectSourceCondition, cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerOnTrashSecurity</c> / <c>CanTriggerOnTrashSelfSecurity</c>
    /// (WhenDiscardSecurity.cs) — delegate to the trash-hand shape (AS-IS does the same).</summary>
    public static bool CanTriggerOnTrashSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition) =>
        CanTriggerOnTrashHand(ctx, card, cardEffectSourceCondition, cardCondition);

    public static bool CanTriggerOnTrashSelfSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null) =>
        CanTriggerOnTrashSecurity(ctx, card, cardEffectSourceCondition, cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerWhenDiscardLibrary</c> (WhenDiscardLibrary.cs:17-30): any-match the discarded
    /// list on <c>!IsBeingRevealed &amp;&amp; cardCondition</c>. The <c>!IsBeingRevealed</c> exclusion DOES have a
    /// headless surface: the reveal path (<c>SimplifiedRevealAndSelectEffect</c> / <c>RevealMultiSelectEffect</c>)
    /// routes its unselected remainder to the trash via a <c>TrashCard</c> mutation, which F1 threads as a
    /// Library-&gt;Trash CardMoved that derives <c>OnDiscardLibrary</c>. That reveal-remainder trash is stamped with
    /// <see cref="MatchStateMutationSink.RevealTrashFlagKey"/> — the mirror of <c>IsBeingRevealed==true</c> at the
    /// trash moment (AS-IS resets the flag only AFTER <c>TrashRevealedCards</c>, RevealLibrary.cs:174/464) — so this
    /// gate rejects it, exactly as AS-IS excludes revealed cards. A DIRECT effect-driven library trash (a plain
    /// <c>TrashDeckCards</c>, <c>IsBeingRevealed==false</c>) carries no flag and fires normally.</summary>
    public static bool CanTriggerWhenDiscardLibrary(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null) =>
        !IsRevealTrashEvent(ctx) && EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));

    /// <summary>(F1 reveal-remainder) Reads the AS-IS <c>IsBeingRevealed</c> mirror off the driving
    /// Library-&gt;Trash CardMoved (stamped by the reveal-remainder trash path). True ⇒ the discarded card was a
    /// revealed remainder ⇒ <c>CanTriggerWhenDiscardLibrary</c> excludes it (WhenDiscardLibrary.cs:23-26).</summary>
    private static bool IsRevealTrashEvent(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue(
            $"{GameFlowProcessor.EventValuePrefix}{MatchStateMutationSink.RevealTrashFlagKey}", out object? raw) && raw is true;

    /// <summary>AS-IS <c>CanTriggerWhenSelfDiscardLibrary</c> (WhenDiscardLibrary.cs:10).</summary>
    public static bool CanTriggerWhenSelfDiscardLibrary(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanTriggerWhenDiscardLibrary(ctx, card, cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerOnTrashDigivolutionCard</c> (OnTrashDigivolutionCard.cs:35).</summary>
    public static bool CanTriggerOnTrashDigivolutionCard(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host || host.IsEmpty)
        {
            return false;
        }

        if (permanentCondition is not null &&
            !permanentCondition(new Permanent(card.Context, host, OwnerOfId(card.Context, host))))
        {
            return false;
        }

        if (cardEffectSourceCondition is not null &&
            !cardEffectSourceCondition(new CardSource(card.Context, ctx.EffectContext.SourceEntityId, OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId), OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId))))
        {
            return false;
        }

        return EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));
    }

    /// <summary>AS-IS <c>CanTriggerOnTrashSelfDigivolutionCard</c> (OnTrashDigivolutionCard.cs:10-31). The AS-IS
    /// self shape fixes BOTH conditions: PermanentCondition = the trashing host permanent exists on the battle
    /// area AND its <c>DigivolutionCards</c> contain THIS card (:12-23); CardCondition = the discarded card IS
    /// this card (:25-28). NOTE the AS-IS window fires while the discarded cards are still listed in the host's
    /// stack (the trigger stacks BEFORE the physical move), so <c>DigivolutionCards.Contains(card)</c> holds; the
    /// headless emit (DigivolutionStackHelpers) drops the ids from <c>sourceIds</c> before emitting, so the
    /// membership half is satisfied vacuously-false — mirror it as "host still on battle area" + the discarded
    /// list containing this card (the CardCondition half), which is the same decidable set.</summary>
    public static bool CanTriggerOnTrashSelfDigivolutionCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardEffectSourceCondition = null) =>
        CanTriggerOnTrashDigivolutionCard(
            ctx, card,
            // (C-3 재상환 P2-4④) AS-IS PermanentCondition restored: the host permanent is on the battle area.
            // The AS-IS `DigivolutionCards.Contains(card)` half is evaluated over the PRE-move stack; headless
            // emits post-drop, so the equivalent discriminator is the event's discarded-cards list containing
            // this card — exactly the CardCondition below (AS-IS evaluates both; on this event ordering they
            // coincide: the discarded list containing the card ⇒ it was one of that host's sources).
            permanentCondition: host => IsPermanentExistsOnBattleArea(host),
            cardEffectSourceCondition,
            cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerOnTrashLinkedCard</c> (OnTrashLinkedCard.cs:35) — same shape over the
    /// link-discard window.</summary>
    public static bool CanTriggerOnTrashLinkedCard(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition, Func<CardSource, bool>? cardCondition) =>
        CanTriggerOnTrashDigivolutionCard(ctx, card, permanentCondition, cardEffectSourceCondition, cardCondition);

    /// <summary>AS-IS <c>CanTriggerOnTrashBySelfDigiBurst</c> (OnTrashBySelfDigiBurst.cs:10) — Digi-Burst
    /// is not a modeled headless mechanism; the source-description probe has no surface. STOP.</summary>
    public static bool CanTriggerOnTrashBySelfDigiBurst(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        throw new NotSupportedException("Digi-Burst is not modeled — STOP (strong model).");

    /// <summary>AS-IS <c>CanTriggerWhenPermanentUnsuspends</c> (OnUnsuspend.cs:17 — delegates to the
    /// suspend shape).</summary>
    public static bool CanTriggerWhenPermanentUnsuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        CanTriggerWhenPermanentSuspends(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>CanTriggerWhenSelfPermanentUnsuspends</c> (OnUnsuspend.cs:10).</summary>
    public static bool CanTriggerWhenSelfPermanentUnsuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerWhenSelfPermanentSuspends</c> (OnSuspend.cs:10).</summary>
    public static bool CanTriggerWhenSelfPermanentSuspends(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerOnPermanentAttackTargetSwitch</c> (OnAttackTargetSwitch.cs:17 —
    /// delegates to the attack shape).</summary>
    public static bool CanTriggerOnPermanentAttackTargetSwitch(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition) =>
        CanTriggerOnPermanentAttack(ctx, card, permanentCondition);

    /// <summary>AS-IS <c>CanTriggerOnAttackTargetSwitch</c> (OnAttackTargetSwitch.cs:10).</summary>
    public static bool CanTriggerOnAttackTargetSwitch(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectPermanentContains(ctx, card);

    /// <summary>AS-IS <c>CanTriggerWhenAddHand</c> (WhenAddHand.cs:10): cards were added to a player's hand. The
    /// PLAYER-SCOPE half — AS-IS <c>Players.Count(playerCondition) &gt;= 1</c> (any gaining player passes) — is
    /// reproduced per driving event: <paramref name="playerCondition"/> is applied to the added card's owner (the
    /// event subject's owner = the gaining player), and the activated-bridge batch collapse offers each added card so
    /// an any-match over the batch is preserved. The CAUSE half — AS-IS <c>cardEffectCondition == null ||
    /// cardEffectCondition(CardEffect)</c>, where <c>CardEffect</c> MAY be null (a turn/mulligan draw passes
    /// cardEffect=null) — is reproduced by threading the causing effect's source card as
    /// <c>event.addHandCauseEffectId</c> (stamped by the effect-driven draw / return-to-hand sink paths) and passing
    /// it (or NULL when the add carried no effect cause) to <paramref name="cardEffectCondition"/>. Unlike
    /// <see cref="CanTriggerOnHandAdded"/> this does NOT itself require a cause — a caller that needs "effect-driven
    /// only" supplies <c>cause =&gt; cause is not null</c> (the AS-IS <c>cardEffect =&gt; cardEffect != null</c>
    /// idiom), exactly as the real cards do (BT9_021 / EX4_022 / BT15_083).</summary>
    public static bool CanTriggerWhenAddHand(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<HeadlessPlayerId, bool>? playerCondition = null, Func<CardSource?, bool>? cardEffectCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        if (playerCondition is not null && !playerCondition(OwnerOfId(card.Context, subject)))
        {
            return false;
        }

        return cardEffectCondition is null || cardEffectCondition(AddHandCauseEffect(ctx, card));
    }

    /// <summary>The causing effect's <c>EffectSourceCard</c> (AS-IS hashtable <c>CardEffect</c>) for an effect-driven
    /// hand add, threaded as <c>event.addHandCauseEffectId</c>; NULL when the add carried no effect cause (a turn /
    /// mulligan / setup draw — AS-IS <c>CardEffect == null</c>). Mirror of <see cref="DiscardCauseEffect"/>.</summary>
    private static CardSource? AddHandCauseEffect(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (!ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}{Headless.Effects.MatchStateMutationSink.AddHandCauseEffectIdKey}", out object? raw)
            || raw?.ToString() is not { Length: > 0 } value)
        {
            return null;
        }

        var id = new HeadlessEntityId(value);
        EngineContext context = card.Context;
        return new CardSource(context, id, OwnerOfId(context, id), OwnerOfId(context, id));
    }

    /// <summary>AS-IS <c>CanTriggerOnHandAdded</c> (OnCardsAddedToHand.cs:12) — the player-SPECIFIC form that
    /// additionally REQUIRES the add be effect-driven (<c>CardEffect != null</c>, :19) before testing
    /// <paramref name="cardEffectSourceCondition"/> on the causing effect's source card. A NON-effect add (turn /
    /// mulligan draw, no cause id threaded) is rejected regardless of the predicate, mirroring the built-in null
    /// check — so unlike the bare <see cref="CanTriggerWhenAddHand"/> this never fires on a plain draw.</summary>
    public static bool CanTriggerOnHandAdded(Headless.Effects.CardEffectResolveContext ctx, CardSource card, HeadlessPlayerId player, Func<CardSource, bool>? cardEffectSourceCondition = null) =>
        CanTriggerWhenAddHand(ctx, card, p => p == player,
            cause => cause is not null && (cardEffectSourceCondition is null || cardEffectSourceCondition(cause)));

    /// <summary>AS-IS <c>CanTriggerWhenAddSecurity</c> (WhendAddSecurity.cs:10) — delegates to the
    /// lose-security shape (the gaining player's condition over the moved card's owner).</summary>
    public static bool CanTriggerWhenAddSecurity(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<HeadlessPlayerId, bool>? playerCondition = null) =>
        CanTriggerWhenLoseSecurity(ctx, card, playerCondition);

    /// <summary>AS-IS <c>CanTriggerWhenUseOption</c> (WhenUseOption.cs:21): an Option was used — the card
    /// and its paid cost pass their predicates.</summary>
    public static bool CanTriggerWhenUseOption(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<CardSource, bool>? cardCondition = null, Func<int, bool>? costCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId option || option.IsEmpty)
        {
            return false;
        }

        if (cardCondition is not null &&
            !cardCondition(new CardSource(card.Context, option, OwnerOfId(card.Context, option), OwnerOfId(card.Context, option))))
        {
            return false;
        }

        if (costCondition is null)
        {
            return true;
        }

        return ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}cost", out object? raw)
            && raw is int cost && costCondition(cost);
    }

    /// <summary>AS-IS <c>CanTriggerWhenOwnerUseOption</c> (WhenUseOption.cs:11).</summary>
    public static bool CanTriggerWhenOwnerUseOption(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null, Func<int, bool>? costCondition = null) =>
        CanTriggerWhenUseOption(ctx, card, cs => cs.Owner == card.Owner && (cardCondition is null || cardCondition(cs)), costCondition);

    /// <summary>AS-IS <c>CanTriggerWhenCardsReturnToHandFromTrash</c> (OnCardsReturnToHandFromTrash.cs:21).</summary>
    public static bool CanTriggerWhenCardsReturnToHandFromTrash(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null) =>
        EventCards(ctx, card).Any(cs => !IsDigiEggType(cs) && (cardCondition is null || cardCondition(cs)));

    /// <summary>AS-IS <c>CanTriggerWhenOwnerCardsReturnToLibraryFromTrash</c>
    /// (OnCardsReturnToLibraryFromTrash.cs:11).</summary>
    public static bool CanTriggerWhenOwnerCardsReturnToLibraryFromTrash(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null) =>
        EventCards(ctx, card).Any(cs => !IsDigiEggType(cs) && cs.Owner == card.Owner && (cardCondition is null || cardCondition(cs)));

    /// <summary>AS-IS <c>CanTriggerOnReturnToLibraryBottomDigivolutionCard</c>
    /// (OnReturnLibraryBottomDigivolutionCards.cs:10): this card's OWN permanent returned digivolution
    /// cards to the deck bottom.</summary>
    public static bool CanTriggerOnReturnToLibraryBottomDigivolutionCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)
    {
        if (!IsExistOnBattleArea(card) ||
            ctx.EffectContext.TriggerEntityId is not HeadlessEntityId host ||
            (card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default) != host)
        {
            return false;
        }

        return EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));
    }

    /// <summary>AS-IS <c>CanTriggerWhenUseDigiBurst</c> — Digi-Burst is not modeled. STOP.</summary>
    public static bool CanTriggerWhenUseDigiBurst(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        throw new NotSupportedException("Digi-Burst is not modeled — STOP (strong model).");

    /// <summary>AS-IS <c>CanTriggerWhenTopCardTrashed</c> (WhenRemoveField.cs:37).</summary>
    public static bool CanTriggerWhenTopCardTrashed(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool> cardCondition)
    {
        ArgumentNullException.ThrowIfNull(cardCondition);
        return EventCards(ctx, card).Any(cardCondition);
    }

    /// <summary>AS-IS <c>CanTriggerOnPermanentLeave</c> (OnDeletion.cs:51). (D-2) The leaving subject has already
    /// moved to the trash by headless collect time, but AS-IS evaluates this gate while it is STILL on the field
    /// (the leave batch is stacked before RemoveField — CardController.cs:3748), so the subject view answers its
    /// field-membership from the driving event's PRE-removal <c>ZoneFrom</c> (Permanent.SnapshotZone) — reproducing
    /// the AS-IS truth for a battle-area gate like <c>IsPermanentExistsOnOpponentBattleAreaDigimon</c>.</summary>
    public static bool CanTriggerOnPermanentLeave(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool> permanentCondition) =>
        LeaveSubjectPermanentPasses(ctx, card, permanentCondition);

    /// <summary>(D-2) Evaluate <paramref name="permanentCondition"/> against a LEAVE subject view carrying the
    /// event's pre-removal field zone (see <see cref="Permanent.SnapshotZone"/>).</summary>
    private static bool LeaveSubjectPermanentPasses(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool> permanentCondition)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        ChoiceZone? snapshot = null;
        if (ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}fromZone", out object? raw)
            && raw is string zoneName && Enum.TryParse(zoneName, out ChoiceZone fromZone))
        {
            snapshot = fromZone;
        }

        var view = new Permanent(card.Context, subject, OwnerOfId(card.Context, subject), snapshot);
        return permanentCondition is null || permanentCondition(view);
    }

    /// <summary>AS-IS <c>CanTriggerOnFaceUpSecurityIncreases</c> (OnFaceUpSecurityIncrease.cs:11).</summary>
    public static bool CanTriggerOnFaceUpSecurityIncreases(Headless.Effects.CardEffectResolveContext ctx, CardSource card, HeadlessPlayerId? player = null, Func<CardSource, bool>? cardCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        if (player is HeadlessPlayerId p && OwnerOfId(card.Context, subject) != p)
        {
            return false;
        }

        return EventCards(ctx, card).Any(cs => cardCondition is null || cardCondition(cs));
    }

    /// <summary>AS-IS <c>IsTopCardInTrashOnDeletion</c> (OnDeletion.cs:144): the deletion subject's top
    /// actually reached the trash (or is a token).</summary>
    public static bool IsTopCardInTrashOnDeletion(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        var view = new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject));
        return view.IsToken || IsExistOnTrash(view);
    }

    /// <summary>AS-IS <c>IsExistOnBattleAreaTrigger</c>/<c>IsExistOnBattleAreaActivate</c>
    /// (GameContextDeterminarion.cs:41/89): the AS-IS pair caches "the permanent at trigger time" and
    /// re-checks it at activation — headless the permanent identity IS the instance id (stacks keep their
    /// top instance across the window), so both collapse to the live battle-area check.</summary>
    public static bool IsExistOnBattleAreaTrigger(CardSource card, ICardEffect? cardEffect = null) =>
        IsExistOnBattleArea(card);

    public static bool IsExistOnBattleAreaActivate(CardSource card, ICardEffect? cardEffect = null) =>
        IsExistOnBattleArea(card);

    /// <summary>AS-IS <c>CanActivateOnDeletionWithContainingCardName</c> (OnDeletion.cs, verbatim): the
    /// deleted stack contains a card passing the predicate AND a deleted-card NAME contains
    /// <paramref name="name"/>. Headless: subject = the deleted top; the stack = subject + its snapshot
    /// sources.</summary>
    public static bool CanActivateOnDeletionWithContainingCardName(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, string name, Func<CardSource, bool>? cardCondition = null)
    {
        return DeletedStackPasses(ctx, card, cardCondition) &&
            DeletedStackCards(ctx, card).Any(cs => cs.ContainsCardName(name));
    }

    /// <summary>AS-IS <c>CanActivateOnDeletionWithContainingTrait</c>.</summary>
    public static bool CanActivateOnDeletionWithContainingTrait(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, string name, Func<CardSource, bool>? cardCondition = null)
    {
        return DeletedStackPasses(ctx, card, cardCondition) &&
            SubjectCard(ctx, card) is CardSource top && top.ContainsTraits(name);
    }

    /// <summary>AS-IS <c>CanActivateOnDeletionWithCardColors</c> — the deleted top's colour list passes.</summary>
    public static bool CanActivateOnDeletionWithCardColors(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card,
        Func<IReadOnlyList<string>, bool>? cardColorCondition, Func<CardSource, bool>? cardCondition = null)
    {
        return DeletedStackPasses(ctx, card, cardCondition) &&
            SubjectCard(ctx, card) is CardSource top &&
            (cardColorCondition is null || cardColorCondition(top.CardColors));
    }

    /// <summary>AS-IS <c>CanActivateOnDeletionWithSaveText</c> — the deleted top HAD [Save] (the P1/A4
    /// deletion-time keyword snapshot preserves it past the binding drop).</summary>
    public static bool CanActivateOnDeletionWithSaveText(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)
    {
        if (!DeletedStackPasses(ctx, card, cardCondition) ||
            ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject)
        {
            return false;
        }

        return card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null
            && dead.Metadata.TryGetValue(Headless.Runtime.DeletionReplacementGate.HasSaveKey, out object? raw) && raw is true;
    }

    /// <summary>AS-IS self wrappers (OnDeletion.cs:200/254/305/359 — original "Selef" spelling kept).</summary>
    public static bool CanActivateSelfOnDeletionWithContainingCardName(Headless.Effects.CardEffectResolveContext ctx, string name, CardSource card) =>
        CanActivateOnDeletionWithContainingCardName(ctx, card, name, cs => cs.InstanceId == card.InstanceId);

    public static bool CanActivateSelfOnDeletionWithContainingTrait(Headless.Effects.CardEffectResolveContext ctx, string name, CardSource card) =>
        CanActivateOnDeletionWithContainingTrait(ctx, card, name, cs => cs.InstanceId == card.InstanceId);

    public static bool CanActivateSelfOnDeletionWithCardColors(Headless.Effects.CardEffectResolveContext ctx, Func<IReadOnlyList<string>, bool>? cardColorCondition, CardSource card) =>
        CanActivateOnDeletionWithCardColors(ctx, card, cardColorCondition, cs => cs.InstanceId == card.InstanceId);

    public static bool CanActivateSelefOnDeletionWithSaveText(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanActivateOnDeletionWithSaveText(ctx, card, cs => cs.InstanceId == card.InstanceId);

    /// <summary>AS-IS <c>CanTriggerWhenPermanentWouldDigivolveOfCard</c> (WhenPermanentWouldDigivolve.cs:11):
    /// the would-digivolve target is THIS card's own permanent.</summary>
    public static bool CanTriggerWhenPermanentWouldDigivolveOfCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition = null)
    {
        HeadlessEntityId ownTop = card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default;
        return !ownTop.IsEmpty &&
            CanTriggerWhenPermanentWouldDigivolve(ctx, card, p => p.InstanceId == ownTop, cardCondition);
    }

    /// <summary>AS-IS <c>CanJogressWithHandOrTrash</c> (DNADigivolveEffects.cs:231): the DNA card sits in
    /// the hand/trash and its recipe can be filled. The hand/trash-MATERIAL half rides the unmodeled
    /// temporary-permanent machinery (STOP) — battle-area materials are the modeled path.</summary>
    public static bool CanJogressWithHandOrTrash(
        CardSource source, HeadlessPlayerId owner, bool isWithHandCard, bool isIntoHandCard,
        Func<CardSource, bool>? targetCardCondition = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _ = isWithHandCard;
        if (!(isIntoHandCard ? IsExistOnHand(source) : IsExistOnTrash(source)) ||
            (targetCardCondition is not null && !targetCardCondition(source)))
        {
            return false;
        }

        return SpecialPlayRecipeRegistry.TryGet(source.CardNumber, out SpecialPlayRecipe? recipe) && recipe is not null
            && recipe.Kind == SpecialPlayKind.DnaDigivolve;
    }

    /// <summary>AS-IS <c>ChangeSecurityDigimonCardDPPlayerEffect</c> (GiveEffectToPlayer/ChangeCardDP.cs:10,
    /// verbatim): security Digimon gain ±DP for security battles (SecurityResolver folds the grant).</summary>
    public static bool ChangeSecurityDigimonCardDPPlayerEffect(
        Func<CardSource, bool>? cardCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard)
    {
        if (changeValue == 0)
        {
            return false;
        }

        var extra = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Headless.Runtime.SecurityResolver.SecurityCardDpDeltaKey] = changeValue,
        };
        if (cardCondition is not null)
        {
            extra[Headless.Runtime.SecurityResolver.SecurityCardPredicateKey] = cardCondition;
        }

        return GainToPlayerScope(effectDuration, sourceCard, "changeSecurityCardDp", permanentCondition: null,
            extraValues: extra, scopeOverride: ContinuousModifierGate.Scope);
    }

    /// <summary>AS-IS <c>StartOfMainAttack</c> (GiveEffect/StartOfMainAttack.cs:5, verbatim): until the
    /// owner's turn end, at the start of the owner's main phase this Digimon MUST attack (the offer cannot
    /// be declined; player or any Digimon). Registered as a duration-tagged trigger binding whose effect
    /// opens the attack offer.</summary>
    public static void StartOfMainAttack(Permanent? targetPermanent, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return;
        }

        EngineContext context = sourceCard.Context;
        HeadlessEntityId attackerId = targetPermanent.InstanceId;
        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, attackerId,
            triggerEntityId: null, targetEntityIds: new[] { attackerId },
            values: new Dictionary<string, object?>(StringComparer.Ordinal));
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:startOfMainAttack:{attackerId.Value}"),
                sourceCard.Controller, Headless.Effects.TriggerTimings.OnStartMainPhase, effectContext),
            keywords: null, EffectQueryRole.None, queryScopes: null,
            effect: new StartOfMainAttackEffect(context, attackerId),
            duration: EffectDuration.UntilOwnerTurnEnd));
    }

    /// <summary>AS-IS <c>GetCardEffectFromHashtable</c> (GetFromHashtable.cs:10) — headless the CAUSING
    /// effect is represented by its SOURCE CARD.</summary>
    public static CardSource? GetCardEffectFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.SourceEntityId.IsEmpty
            ? null
            : new CardSource(card.Context, ctx.EffectContext.SourceEntityId, OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId), OwnerOfId(card.Context, ctx.EffectContext.SourceEntityId));

    /// <summary>AS-IS <c>GetAttackerFromHashtable</c> (:250): the attacking permanent = the event subject.</summary>
    public static Permanent? GetAttackerFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        GetPermanentFromHashtable(ctx, card);

    /// <summary>AS-IS <c>GetMovedPermanentFromHashtable</c> (OnMove.cs:30).</summary>
    public static Permanent? GetMovedPermanentFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        GetPermanentFromHashtable(ctx, card);

    /// <summary>AS-IS <c>GetTopCardFromOneHashtable</c> (:295) / <c>GetTopCardFromEffectHashtable</c>
    /// (:178): the deletion subject's top card.</summary>
    public static CardSource? GetTopCardFromOneHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectCard(ctx, card);

    public static CardSource? GetTopCardFromEffectHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        SubjectCard(ctx, card);

    /// <summary>AS-IS <c>GetFaceDownFromHashtable</c> (:337) — default true, like the original.</summary>
    public static bool GetFaceDownFromHashtable(Headless.Effects.CardEffectResolveContext ctx) =>
        !ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isFaceDown", out object? raw)
        || raw is not bool b || b;

    /// <summary>AS-IS <c>GetCardSourcesFromHashtable</c> (:592) / <c>GetDiscardedCardsFromHashtable</c>
    /// (:569): the event's card list.</summary>
    public static List<CardSource> GetCardSourcesFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        EventCards(ctx, card).ToList();

    public static List<CardSource> GetDiscardedCardsFromHashtable(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        EventCards(ctx, card).ToList();

    /// <summary>AS-IS <c>GetDigivolutionRootsFromEnterFieldHashtable</c> (:661): the entered permanent's
    /// digivolution sources (all cards under the subject).</summary>
    public static List<CardSource> GetDigivolutionRootsFromEnterFieldHashtable(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return new List<CardSource>();
        }

        EngineContext context = card.Context;
        if (permanentCondition is not null &&
            !permanentCondition(new Permanent(context, subject, OwnerOfId(context, subject))))
        {
            return new List<CardSource>();
        }

        return DeletedStackCards(ctx, card).Skip(1).ToList();   // stack minus the top = the roots
    }

    /// <summary>AS-IS <c>GetEvoRootTopsFromEnterFieldHashtable</c> (:200): the PRE-digivolve top(s) — the
    /// digivolve event carries the previous top (targetCardId).</summary>
    public static List<CardSource> GetEvoRootTopsFromEnterFieldHashtable(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition = null)
    {
        EngineContext context = card.Context;
        if (ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty &&
            permanentCondition is not null &&
            !permanentCondition(new Permanent(context, subject, OwnerOfId(context, subject))))
        {
            return new List<CardSource>();
        }

        if (ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}targetCardId", out object? raw) &&
            raw?.ToString() is { Length: > 0 } value)
        {
            var id = new HeadlessEntityId(value);
            return new List<CardSource> { new(context, id, OwnerOfId(context, id), OwnerOfId(context, id)) };
        }

        return new List<CardSource>();
    }

    // --- (W6 tail) shared event-card reader -----------------------------------------------------------

    /// <summary>The cards the driving event is about: an id-list value when the emission carries one
    /// (cardIds / addedCardIds), else the single subject.</summary>
    private static IReadOnlyList<CardSource> EventCards(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        EngineContext context = card.Context;
        foreach (string key in new[] { "cardIds", "addedCardIds", "discardedCardIds" })
        {
            if (ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}{key}", out object? raw) &&
                raw?.ToString() is { Length: > 0 } value)
            {
                return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(id => new HeadlessEntityId(id))
                    .Select(id => new CardSource(context, id, OwnerOfId(context, id), OwnerOfId(context, id)))
                    .ToArray();
            }
        }

        return ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty
            ? new[] { new CardSource(context, subject, OwnerOfId(context, subject), OwnerOfId(context, subject)) }
            : Array.Empty<CardSource>();
    }

    private static bool DeletedStackPasses(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<CardSource, bool>? cardCondition) =>
        cardCondition is null
            ? ctx.EffectContext.TriggerEntityId is HeadlessEntityId s && !s.IsEmpty
            : DeletedStackCards(ctx, card).Any(cardCondition);

    private static CardSource? SubjectCard(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        ctx.EffectContext.TriggerEntityId is HeadlessEntityId subject && !subject.IsEmpty
            ? new CardSource(card.Context, subject, OwnerOfId(card.Context, subject), OwnerOfId(card.Context, subject))
            : null;

    /// <summary>The deleted permanent's stack (subject top + its snapshot digivolution sources).</summary>
    private static IReadOnlyList<CardSource> DeletedStackCards(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return Array.Empty<CardSource>();
        }

        EngineContext context = card.Context;
        var stack = new List<CardSource> { new(context, subject, OwnerOfId(context, subject), OwnerOfId(context, subject)) };
        if (context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null &&
            dead.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw) &&
            raw is IEnumerable<string> ids)
        {
            stack.AddRange(ids.Select(v => new HeadlessEntityId(v))
                .Select(id => new CardSource(context, id, OwnerOfId(context, id), OwnerOfId(context, id))));
        }

        return stack;
    }

    private static bool IsDigiEggType(CardSource cs) =>
        cs.Context.CardInstanceRepository.TryGetInstance(cs.InstanceId, out CardInstanceRecord? i) && i is not null
        && cs.Context.CardRepository.TryGetCard(i.DefinitionId, out CardRecord? d) && d is not null
        && (d.IsCardType("DigiEgg") || d.IsCardType("Digitama"));

    // --- (W6-T) shared readers over the enriched resolve context ------------------------------------

    private static bool EventIsDigivolve(Headless.Effects.CardEffectResolveContext ctx) =>
        (ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}{AutoProcessingTriggerCollector.TriggerTimingKey}", out object? raw)
            && raw is string timing && timing == Headless.Effects.TriggerTimings.WhenDigivolving)
        || (ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isEvolution", out object? evo) && evo is true);

    private static bool EventRootPasses(Headless.Effects.CardEffectResolveContext ctx, Func<ChoiceZone, bool>? rootCondition)
    {
        if (rootCondition is null)
        {
            return true;
        }

        return ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}fromZone", out object? raw)
            && raw is string zoneName && Enum.TryParse(zoneName, out ChoiceZone fromZone)
            && rootCondition(fromZone);
    }

    /// <summary>Mirror of the AS-IS <c>permanent.cardSources.Contains(card)</c> subject checks: the event
    /// subject is this card, or this card rides the subject's stack (digivolution source).</summary>
    private static bool SubjectPermanentContains(Headless.Effects.CardEffectResolveContext ctx, CardSource card)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        if (subject == card.InstanceId)
        {
            return true;
        }

        return card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? record) && record is not null
            && record.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw)
            && raw is IEnumerable<string> sources && sources.Contains(card.InstanceId.Value);
    }

    private static bool SubjectPermanentPasses(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? permanentCondition, bool requireOnBattleArea = false)
    {
        if (ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject || subject.IsEmpty)
        {
            return false;
        }

        HeadlessPlayerId owner = card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? record) && record is not null
            ? record.OwnerId
            : default;
        var view = new Permanent(card.Context, subject, owner);
        if (requireOnBattleArea && !IsPermanentExistsOnBattleArea(view))
        {
            return false;
        }

        return permanentCondition is null || permanentCondition(view);
    }

    // (G-clean-2) The invented GainKeywordToPermanent registry-marker funnel is DELETED. Its 16 keyword
    // callers now grant AS-IS 1:1 through the KeyWordEffects/*.cs Task Gain* overloads (AddEffectToPermanent
    // duration bucket → EffectList_Added, read by Permanent.Has<Keyword> / the deletion & OnEndTurn & OnAllyAttack
    // windows). BecomeDigimonThatCantDigivolve — the sole non-keyword caller — now grants TreatAsDigimon as a
    // TreatAsDigimonStaticEffect in the None bucket, AS-IS 1:1.

    // (R3-W3c-3) The DP/SAttack-delta grants no longer register a ContinuousModifierGate binding into the
    // registry — that consumer (ContinuousEffectEvaluator.ResolveDp) is dead (0 live callers), so the delta
    // never fired. They are restored to AS-IS 1:1: build the factory kind-class (ChangeDPClass /
    // ChangeSAttackClass, an IChangeDPEffect / IChangeSAttackEffect) and store it into the target permanent's
    // EffectTiming.None duration bucket via AddEffectToPermanent — which Permanent.DP / Permanent.SAttack scan
    // LIVE (Permanent.cs:348 / :2500). ADAPTATION: the AS-IS ICardEffect activateClass is collapsed to its
    // EffectSourceCard at the bridge boundary; real callers re-thread the original activateClass so the outer
    // CanUseCondition's `!TopCard.CanNotBeAffected(activateClass)` immunity is verbatim (fallback: the produced
    // kind-class, whose EffectSourceCard==sourceCard, so immunity resolves identically when null).

    /// <summary>AS-IS <c>ChangeDigimonDP</c> (GiveEffect/GiveEffectToPermanent/ChangeDP.cs:10, verbatim):
    /// timed ±DP on the target permanent, stored in its None duration bucket.</summary>
    public static bool ChangeDigimonDP(
        Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard,
        ICardEffect? activateClass = null)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null) return false;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return false;
        if (changeValue == 0) return false;

        CardSource card = sourceCard;
        CardEffects.ChangeDPClass changeDPClass = null!;

        bool CanUseCondition()
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (!targetPermanent.TopCard.CanNotBeAffected(activateClass ?? changeDPClass))
                {
                    return true;
                }
            }

            return false;
        }

        changeDPClass = CardEffectFactory.ChangeTargetDPStaticEffect(
            targetPermanent: targetPermanent,
            changeValue: changeValue,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition);

        AddEffectToPermanent(
            targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
            cardEffect: changeDPClass, timing: EffectTiming.None);
        return true;
    }

    /// <summary>AS-IS <c>ChangeDigimonSAttack</c> (…/ChangeSAttack.cs:10; the overload's
    /// <paramref name="activateAnimation"/>/<paramref name="hashstring"/> are UI-only in the original).</summary>
    public static bool ChangeDigimonSAttack(Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard,
        bool activateAnimation = true, string? hashstring = null, ICardEffect? activateClass = null)
    {
        _ = activateAnimation;
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null) return false;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return false;
        if (changeValue == 0) return false;

        CardSource card = sourceCard;
        CardEffects.ChangeSAttackClass changeSAttackClass = null!;

        bool CanUseCondition()
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (!targetPermanent.TopCard.CanNotBeAffected(activateClass ?? changeSAttackClass))
                {
                    return true;
                }
            }

            return false;
        }

        changeSAttackClass = CardEffectFactory.ChangeTargetSAttackStaticEffect(
            targetPermanent: targetPermanent,
            changeValue: changeValue,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            hashstring: hashstring);

        AddEffectToPermanent(
            targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
            cardEffect: changeSAttackClass, timing: EffectTiming.None);
        return true;
    }

    /// <summary>AS-IS <c>ChangeDigimonDPPlayerEffect</c> (GiveEffect/GiveEffectToPlayer/ChangeDP.cs:10):
    /// timed ±DP on EVERY permanent matching the predicate — a duration-tagged PLAYER-SCOPE modifier.
    /// (R3-W3c-3) Restored to AS-IS 1:1: the dead registry ContinuousModifierGate binding is replaced by the
    /// factory ChangeDPClass (a player-scope IChangeDPEffect whose PermanentCondition folds the battle-area +
    /// !CanNotBeAffected(activateClass) + user predicate) stored in the OWNING PLAYER's None duration bucket
    /// via AddEffectToPlayer — Permanent.DP scans it in its player-effect region (Permanent.cs:428).</summary>
    public static bool ChangeDigimonDPPlayerEffect(
        Func<Permanent, bool>? permanentCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard,
        ICardEffect? activateClass = null)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (changeValue == 0)
        {
            return false;
        }

        CardSource card = sourceCard;
        CardEffects.ChangeDPClass changeDPClass = null!;

        bool PermanentCondition(Permanent permanent)
        {
            if (IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(activateClass ?? changeDPClass))
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanUseCondition() => true;

        changeDPClass = CardEffectFactory.ChangeDPStaticEffect(
            permanentCondition: PermanentCondition,
            changeValue: changeValue,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            effectName: null);

        AddEffectToPlayer(effectDuration: effectDuration, card: card, cardEffect: changeDPClass, timing: EffectTiming.None);
        return true;
    }

    /// <summary>AS-IS <c>AddThisCardToHand</c> (CardEffectCommons.cs:424, UI waits elided): move this card
    /// to its owner's hand via the sink (immunity/centralised gates apply).</summary>
    public static async Task AddThisCardToHand(CardSource card1, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(card1);
        ArgumentNullException.ThrowIfNull(sourceCard);
        var sink = NewSink(card1.Context);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToHandKind, sourceCard.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card1.InstanceId.Value }));
        await sink.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>PlayPermanentCards(cardSources, activateClass, payCost, isTapped, root,
    /// activateETB, isBreedingArea, fixedCost)</c> (CardEffectCommons.cs:23, verbatim verified): filter by
    /// <see cref="CanPlayAsNewPermanent"/> then play each as a new permanent via the sink's PlayCard
    /// mutation (cost = fixed / resolved play cost when <paramref name="payCost"/>). (G3) When
    /// <paramref name="activateETB"/> is false the PlayCard mutation carries
    /// <see cref="MatchStateMutationSink.SuppressOnPlayKey"/>, which threads a one-shot suppressOnPlay marker
    /// onto the entering CardMoved event so the played card's OWN [On Play]/OnEnterField triggers are dropped
    /// ("Any [On Play] effects on the Digimon played with this effect don't activate", BT3_109/110); other
    /// cards' reactions to it entering are unaffected.</summary>
    /// <summary>(R2-C) Map the play-source <see cref="ChoiceZone"/> to the AS-IS <see cref="SelectCardEffect.Root"/>
    /// threaded into the cost pipeline (root-conditioned <see cref="IChangeCostEffect"/> gates). Zones with no
    /// Root analog (battle/breeding area, digitama library) map to <c>None</c>.</summary>
    internal static SelectCardEffect.Root RootFromZone(ChoiceZone zone) => zone switch
    {
        ChoiceZone.Library => SelectCardEffect.Root.Library,
        ChoiceZone.Trash => SelectCardEffect.Root.Trash,
        ChoiceZone.Clock => SelectCardEffect.Root.Clock,
        ChoiceZone.Security => SelectCardEffect.Root.Security,
        ChoiceZone.Custom => SelectCardEffect.Root.Custom,
        ChoiceZone.Hand => SelectCardEffect.Root.Hand,
        ChoiceZone.Recollection => SelectCardEffect.Root.Recollection,
        ChoiceZone.Execution => SelectCardEffect.Root.Execution,
        ChoiceZone.DigivolutionCards => SelectCardEffect.Root.DigivolutionCards,
        ChoiceZone.LinkedCards => SelectCardEffect.Root.LinkedCards,
        _ => SelectCardEffect.Root.None,
    };

    public static async Task PlayPermanentCards(
        IReadOnlyList<CardSource> cardSources, CardSource sourceCard, bool payCost, bool isTapped,
        ChoiceZone root, bool activateETB, bool isBreedingArea = false, int fixedCost = -1)
    {
        ArgumentNullException.ThrowIfNull(cardSources);
        ArgumentNullException.ThrowIfNull(sourceCard);

        EngineContext context = sourceCard.Context;
        var playable = cardSources
            .Where(cs => cs is not null && CanPlayAsNewPermanent(cs, payCost, null, isPlayOption: false, fixedCost: fixedCost))
            .ToList();
        if (playable.Count == 0)
        {
            return;
        }

        var sink = NewSink(context);
        foreach (CardSource cs in playable)
        {
            int cost = 0;
            if (payCost)
            {
                int baseCost = context.CardInstanceRepository.TryGetInstance(cs.InstanceId, out CardInstanceRecord? inst) && inst is not null
                    && context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
                    ? def.PlayCost ?? 0
                    : 0;
                cost = fixedCost >= 0 ? fixedCost : Math.Max(0, cs.GetPayingCostWithBaseCost(baseCost, RootFromZone(root), targetPermanents: null));
            }

            var values = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = cs.InstanceId.Value,
                [MatchStateMutationSink.FromZoneKey] = root,
            };
            if (cost > 0)
            {
                values[MatchStateMutationSink.MemoryCostKey] = cost;
            }

            // (G3) activateETB:false suppresses the played card's OWN [On Play]/OnEnterField triggers.
            if (!activateETB)
            {
                values[MatchStateMutationSink.SuppressOnPlayKey] = true;
            }

            sink.Apply(new EffectMutation(MatchStateMutationSink.PlayCardKind, sourceCard.InstanceId, values));
        }

        await sink.FlushAsync().ConfigureAwait(false);

        var zones = (IZoneStateReader)context.ZoneMover;
        foreach (CardSource cs in playable)
        {
            if (isBreedingArea &&
                zones.GetCards(cs.Owner, ChoiceZone.BattleArea).Contains(cs.InstanceId))
            {
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(cs.Owner, cs.InstanceId, ChoiceZone.BattleArea, ChoiceZone.BreedingArea)).ConfigureAwait(false);
            }

            if (isTapped &&
                context.CardInstanceRepository.TryGetInstance(cs.InstanceId, out CardInstanceRecord? played) && played is not null)
            {
                context.CardInstanceRepository.Upsert(played with
                {
                    Metadata = new Dictionary<string, object?>(played.Metadata, StringComparer.Ordinal) { ["isSuspended"] = true }
                });
            }
        }
    }

    /// <summary>AS-IS <c>DigivolveIntoHandOrTrashCard</c> (CardEffectCommons.cs:756-1100, verbatim
    /// verified): choose a Digimon card from the HAND (or TRASH) that satisfies <paramref name="cardCondition"/>
    /// + the digivolution requirement onto <paramref name="targetPermanent"/> (waived under
    /// <paramref name="ignoreRequirements"/> / <paramref name="ignoreDigivolutionRequirementFixedCost"/>),
    /// digivolve it onto the target (cost = fixed / requirement-ignore fixed / evolution cost −
    /// <paramref name="reduceCostTuple"/> when <paramref name="payCost"/>), then branch on whether the
    /// digivolution ACTUALLY happened. NOTE: the recipe previously mis-mapped this commons to the
    /// de-digivolve factory — it is the OPPOSITE direction (digivolve INTO from hand/trash).</summary>
    public static Task DigivolveIntoHandOrTrashCard(
        Permanent? targetPermanent,
        Func<CardSource, bool>? cardCondition,
        bool payCost,
        (int reduceCost, Func<CardSource, bool>? reduceCostCardCondition)? reduceCostTuple,
        (int fixedCost, Func<CardSource, bool>? fixedCostCardCondition)? fixedCostTuple,
        int ignoreDigivolutionRequirementFixedCost,
        bool isHand,
        CardSource sourceCard,
        Func<Task>? successProcess,
        bool ignoreSelection = false,
        IgnoreRequirement ignoreRequirements = IgnoreRequirement.None,
        Func<Task>? failedProcess = null,
        bool isOptional = true,
        CancellationToken cancellationToken = default) =>
        DigivolveIntoZoneCoreAsync(
            targetPermanent, cardCondition, payCost, reduceCostTuple, fixedCostTuple,
            ignoreDigivolutionRequirementFixedCost, isHand ? ChoiceZone.Hand : ChoiceZone.Trash, sourceCard,
            successProcess, failedProcess, ignoreSelection, ignoreRequirements, isOptional, cancellationToken);

    private static async Task DigivolveIntoZoneCoreAsync(
        Permanent? targetPermanent,
        Func<CardSource, bool>? cardCondition,
        bool payCost,
        (int reduceCost, Func<CardSource, bool>? reduceCostCardCondition)? reduceCostTuple,
        (int fixedCost, Func<CardSource, bool>? fixedCostCardCondition)? fixedCostTuple,
        int ignoreDigivolutionRequirementFixedCost,
        ChoiceZone rootZone,
        CardSource sourceCard,
        Func<Task>? successProcess,
        Func<Task>? failedProcess,
        bool ignoreSelection,
        IgnoreRequirement ignoreRequirements,
        bool isOptional,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        bool successful = false;
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty || context.ZoneMover is not IZoneStateReader zones)
        {
            await Branch(false, successProcess, failedProcess).ConfigureAwait(false);
            return;
        }

        HeadlessEntityId targetId = targetPermanent.InstanceId;

        bool CanSelect(HeadlessEntityId id)
        {
            var view = new CardSource(context, id, targetPermanent.OwnerId, targetPermanent.OwnerId);
            if (!view.IsDigimon || (cardCondition is not null && !cardCondition(view)))
            {
                return false;
            }

            if (ContinuousRestrictionGate.EvaluateDigivolve(context, targetId).IsRestricted)
            {
                return false;   // AS-IS !CanNotEvolve(targetPermanent)
            }

            // (IgnoreRequirement) AS-IS passes the enum to CanPlayCardTargetFrame(ignore: …): None checks both
            // color+level; Level waives level (color still enforced); Color waives color (level enforced); All
            // waives the whole digivolution requirement. The fixed-cost path only changes the COST, not
            // eligibility, so it is applied downstream — not here.
            return ignoreRequirements == IgnoreRequirement.All
                || Headless.Runtime.DigivolveAction.TryGetEvolutionCost(
                    context, id, targetId, out _, out _,
                    ignoreLevel: ignoreRequirements == IgnoreRequirement.Level,
                    ignoreColor: ignoreRequirements == IgnoreRequirement.Color);
        }

        HeadlessEntityId selected = default;
        if (ignoreSelection)
        {
            selected = sourceCard.InstanceId;
        }
        else
        {
            List<ChoiceCandidate> candidates = zones.GetCards(targetPermanent.OwnerId, rootZone)
                .Where(CanSelect)
                .Select(id => new ChoiceCandidate(id, id.Value, rootZone, IsSelectable: true, ownerId: targetPermanent.OwnerId))
                .ToList();
            if (candidates.Count > 0)
            {
                var request = new ChoiceRequest(
                    ChoiceType.Card, targetPermanent.OwnerId, "Select 1 card to digivolve.",
                    minCount: isOptional ? 0 : 1, maxCount: 1, canSkip: isOptional, rootZone, candidates);
                ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
                if (!result.IsSkipped && result.SelectedIds.Count > 0)
                {
                    selected = result.SelectedIds[0];
                }
            }
        }

        if (!selected.IsEmpty)
        {
            // Cost (AS-IS): fixed / requirement-ignore fixed wins; else evolution cost − reduceCost; 0 floor.
            int cost = 0;
            if (payCost)
            {
                if (ignoreDigivolutionRequirementFixedCost >= 0)
                {
                    cost = ignoreDigivolutionRequirementFixedCost;
                }
                else if (fixedCostTuple is { } fixedTuple &&
                         (fixedTuple.fixedCostCardCondition is null || fixedTuple.fixedCostCardCondition(new CardSource(context, selected, targetPermanent.OwnerId, targetPermanent.OwnerId))))
                {
                    cost = fixedTuple.fixedCost;
                }
                else
                {
                    Headless.Runtime.DigivolveAction.TryGetEvolutionCost(context, selected, targetId, out cost, out _);
                    if (reduceCostTuple is { } reduceTuple &&
                        (reduceTuple.reduceCostCardCondition is null || reduceTuple.reduceCostCardCondition(new CardSource(context, selected, targetPermanent.OwnerId, targetPermanent.OwnerId))))
                    {
                        cost -= reduceTuple.reduceCost;
                    }
                }

                cost = Math.Max(0, cost);
            }

            if (!payCost || context.MemoryController.CanPay(cost))
            {
                // The Arts/ArtsDigivolve stacking sequence (target off -> card on -> fold under -> window).
                // (RD-R3-02) both halves marked as top-swap continuity — the AS-IS Permanent persists across
                // the digivolve; AttachTargetAsSource ReKeys the bookkeeping below.
                ChoiceZone targetZone = zones.GetCards(targetPermanent.OwnerId, ChoiceZone.BreedingArea).Contains(targetId)
                    ? ChoiceZone.BreedingArea
                    : ChoiceZone.BattleArea;
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(targetPermanent.OwnerId, targetId, targetZone, ChoiceZone.None,
                        Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata), cancellationToken).ConfigureAwait(false);
                await context.ZoneMover.MoveAsync(
                    new ZoneMoveRequest(targetPermanent.OwnerId, selected, rootZone, targetZone,
                        Metadata: PermanentBookkeepingStore.ContinuityMoveMetadata), cancellationToken).ConfigureAwait(false);
                if (payCost && cost > 0)
                {
                    context.MemoryController.Pay(cost);
                }

                Headless.Runtime.DigivolveAction.AttachTargetAsSource(context.CardInstanceRepository, selected, targetId);
                // (W6 tail) stamp the causing effect (AS-IS Permanent.DigivolvingEffect — IsDigivolvedByTheEffect reads it).
                if (context.CardInstanceRepository.TryGetInstance(selected, out CardInstanceRecord? placedRec) && placedRec is not null)
                {
                    context.CardInstanceRepository.Upsert(placedRec with
                    {
                        Metadata = new Dictionary<string, object?>(placedRec.Metadata, StringComparer.Ordinal)
                        {
                            ["digivolvedByEffectSourceId"] = sourceCard.InstanceId.Value,
                        }
                    });
                }

                TriggerEventEmitter.Emit(context.GameEventQueue, Headless.Effects.TriggerTimings.WhenDigivolving,
                    actor: targetPermanent.OwnerId, subject: selected,
                    extraMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["isEvolution"] = true });
                CardEffectRegistrar.RegisterCard(context, selected, targetPermanent.OwnerId);
                successful = zones.GetCards(targetPermanent.OwnerId, targetZone).Contains(selected);
            }
        }

        await Branch(successful, successProcess, failedProcess).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>SelectTrashDigivolutionCards</c> (TrashDigivolutionCards.cs:11-192, verbatim
    /// verified): repeatedly pick a battle-area permanent matching <paramref name="permanentCondition"/>,
    /// then trash up to the remaining budget of its digivolution sources matching
    /// <paramref name="cardCondition"/> — until <paramref name="maxCount"/> sources are trashed (or one
    /// permanent when <paramref name="isFromOnly1Permanent"/>).</summary>
    public static async Task SelectTrashDigivolutionCards(
        Func<Permanent, bool>? permanentCondition,
        Func<CardSource, bool>? cardCondition,
        int maxCount,
        bool canNoTrash,
        bool isFromOnly1Permanent,
        CardSource sourceCard,
        string selectString = "Digimon",
        Func<Permanent, IReadOnlyList<CardSource>, Task>? afterSelectionCoroutine = null,
        bool canEndNotMax = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (maxCount <= 0)
        {
            return;
        }

        EngineContext context = sourceCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        int trashedTotal = 0;
        var usedHosts = new HashSet<HeadlessEntityId>();

        bool HostQualifies(HeadlessEntityId id, HeadlessPlayerId owner)
        {
            var view = new Permanent(context, id, owner);
            if (permanentCondition is not null && !permanentCondition(view))
            {
                return false;
            }

            return SourcesOf(id).Any(sid => SourceQualifies(sid, owner));
        }

        bool SourceQualifies(HeadlessEntityId sid, HeadlessPlayerId owner)
        {
            // (C-3 재상환 P1-B) AS-IS CanSelectCardCondition (TrashDigivolutionCards.cs:33) embeds
            // `!cardSource.CanNotTrashFromDigivolutionCards(activateClass)` BEFORE the card condition — a
            // protected source is excluded from the candidate pool (and the availability counts), not merely
            // filtered at execution.
            if (IsTrashProtectedSource(sourceCard, sid))
            {
                return false;
            }

            var view = new CardSource(context, sid, owner, owner);
            return cardCondition is null || cardCondition(view);
        }

        IReadOnlyList<HeadlessEntityId> SourcesOf(HeadlessEntityId hostId) =>
            context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? host) && host is not null
                && host.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw)
                && raw is IEnumerable<string> ids
                ? ids.Select(v => new HeadlessEntityId(v)).ToArray()
                : Array.Empty<HeadlessEntityId>();

        while (trashedTotal < maxCount)
        {
            var hostCandidates = new List<ChoiceCandidate>();
            foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
            {
                if (player.IsEmpty)
                {
                    continue;
                }

                foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
                {
                    if (!usedHosts.Contains(id) && HostQualifies(id, player))
                    {
                        hostCandidates.Add(new ChoiceCandidate(id, id.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: player));
                    }
                }
            }

            if (hostCandidates.Count == 0)
            {
                break;
            }

            bool optionalNow = (canNoTrash && trashedTotal == 0) || canEndNotMax;
            var hostRequest = new ChoiceRequest(
                ChoiceType.Card, sourceCard.Owner, $"Select 1 {selectString} that will trash digivolution cards.",
                minCount: optionalNow ? 0 : 1, maxCount: 1, canSkip: optionalNow, ChoiceZone.BattleArea, hostCandidates);
            ChoiceResult hostResult = await context.ChoiceProvider.ChooseAsync(hostRequest, cancellationToken).ConfigureAwait(false);
            if (hostResult.IsSkipped || hostResult.SelectedIds.Count == 0)
            {
                break;
            }

            HeadlessEntityId hostId = hostResult.SelectedIds[0];
            usedHosts.Add(hostId);
            HeadlessPlayerId hostOwner = context.CardInstanceRepository.TryGetInstance(hostId, out CardInstanceRecord? hostRec) && hostRec is not null
                ? hostRec.OwnerId
                : sourceCard.Owner;

            var sourceCandidates = SourcesOf(hostId)
                .Where(sid => SourceQualifies(sid, hostOwner))
                .Select(sid => new ChoiceCandidate(sid, sid.Value, ChoiceZone.DigivolutionCards, IsSelectable: true, ownerId: hostOwner))
                .ToList();
            int budget = Math.Min(maxCount - trashedTotal, sourceCandidates.Count);
            var sourceRequest = new ChoiceRequest(
                ChoiceType.Card, sourceCard.Owner, "Select digivolution cards to trash.",
                minCount: budget >= 2 && !isFromOnly1Permanent ? 1 : budget, maxCount: budget,
                canSkip: false, ChoiceZone.DigivolutionCards, sourceCandidates);
            ChoiceResult sourceResult = await context.ChoiceProvider.ChooseAsync(sourceRequest, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<HeadlessEntityId> picks = sourceResult.SelectedIds;
            int trashed = await Headless.Runtime.DigivolutionStackHelpers.TrashSpecificSourcesAsync(
                context.CardInstanceRepository, context.ZoneMover, hostId, picks, cancellationToken, context.GameEventQueue,
                // (C-3) effect-trash (DigiBurst) honours CanNotTrashFromDigivolutionCards (BT9_109) via TrashProtectionScan.
                context.EffectRegistry, context, sourceCard.InstanceId).ConfigureAwait(false);
            trashedTotal += trashed;

            if (afterSelectionCoroutine is not null)
            {
                await afterSelectionCoroutine(
                    new Permanent(context, hostId, hostOwner),
                    picks.Select(id => new CardSource(context, id, hostOwner, hostOwner)).ToArray()).ConfigureAwait(false);
            }

            if (isFromOnly1Permanent)
            {
                break;
            }
        }
    }

    /// <summary>AS-IS <c>DNADigivolvePermanentsIntoHandOrTrashCard</c> (DNADigivolveEffects.cs:458-624,
    /// verbatim verified): choose a DNA-capable card from the HAND (or TRASH), then perform the DNA
    /// digivolution (two battle-area materials, via the special-play pipeline). Material selection follows
    /// the port's parameterized-action policy (first valid backtracking assignment — the DigiXros/DNA
    /// reduction, fidelity_debt). <paramref name="permanentConditions"/> overrides the material predicates
    /// (AS-IS SetUpCustomPermanentConditions). Success = the fused card actually entered the battle area.</summary>
    public static async Task DNADigivolvePermanentsIntoHandOrTrashCard(
        Func<CardSource, bool>? canSelectDNACardCondition,
        bool payCost,
        bool isHand,
        CardSource sourceCard,
        Func<Permanent, bool>[]? permanentConditions = null,
        Func<CardSource, Task>? successProcess = null,
        bool ignoreSelection = false,
        Func<Task>? failedProcess = null,
        bool isOptional = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        _ = payCost;   // AS-IS predicate-form DNA is cost 0 (the recipe carries the cost when nonzero).
        EngineContext context = sourceCard.Context;
        HeadlessPlayerId owner = sourceCard.Owner;
        var zones = (IZoneStateReader)context.ZoneMover;

        int battleDigimon = zones.GetCards(owner, ChoiceZone.BattleArea)
            .Count(id => new CardSource(context, id, owner, owner).IsDigimon);
        if (battleDigimon < 2)
        {
            await Branch(false, null, failedProcess).ConfigureAwait(false);
            return;
        }

        ChoiceZone rootZone = isHand ? ChoiceZone.Hand : ChoiceZone.Trash;
        HeadlessEntityId dnaTarget = default;
        if (ignoreSelection)
        {
            dnaTarget = sourceCard.InstanceId;
        }
        else
        {
            List<ChoiceCandidate> candidates = zones.GetCards(owner, rootZone)
                .Where(id =>
                {
                    var view = new CardSource(context, id, owner, owner);
                    return (canSelectDNACardCondition is null || canSelectDNACardCondition(view))
                        && SpecialPlayRecipeRegistry.TryGet(view.CardNumber, out SpecialPlayRecipe? r) && r is not null
                        && r.Kind == SpecialPlayKind.DnaDigivolve;
                })
                .Select(id => new ChoiceCandidate(id, id.Value, rootZone, IsSelectable: true, ownerId: owner))
                .ToList();
            if (candidates.Count > 0)
            {
                var request = new ChoiceRequest(
                    ChoiceType.Card, owner, "Select 1 card to DNA digivolve.",
                    minCount: isOptional ? 0 : 1, maxCount: 1, canSkip: isOptional, rootZone, candidates);
                ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
                if (!result.IsSkipped && result.SelectedIds.Count > 0)
                {
                    dnaTarget = result.SelectedIds[0];
                }
            }
        }

        bool successful = false;
        if (!dnaTarget.IsEmpty)
        {
            var view = new CardSource(context, dnaTarget, owner, owner);
            SpecialPlayRecipe? original = null;
            bool overridden = false;
            if (permanentConditions is { Length: > 0 })
            {
                // AS-IS SetUpCustomPermanentConditions: the caller's material predicates replace the card's.
                SpecialPlayRecipeRegistry.TryGet(view.CardNumber, out original);
                var custom = permanentConditions
                    .Select((cond, i) => new SpecialPlayMaterial(
                        cs => cs.IsDigimon && cs.Owner == owner && cond(new Permanent(cs.Context, cs.InstanceId, cs.Owner)),
                        $"custom-{i}"))
                    .ToArray();
                SpecialPlayRecipeRegistry.Register(view.CardNumber, new SpecialPlayRecipe(
                    SpecialPlayKind.DnaDigivolve, custom, MemoryCost: original?.MemoryCost ?? 0, Condition: original?.Condition));
                overridden = true;
            }

            try
            {
                LegalAction? dna = new SpecialPlayAction().GetLegalActions(context, owner)
                    .FirstOrDefault(a => a.Parameters[HeadlessActionParameterKeys.CardId]?.ToString() == dnaTarget.Value);
                if (dna is not null && rootZone == ChoiceZone.Hand)
                {
                    // The special-play pipeline plays from hand (the AS-IS trash-root DNA is a rarer shape —
                    // the card must reach the hand-play seam; a trash-root caller is a STOP for now).
                    var result = await new SpecialPlayAction().ProcessAsync(dna, context, cancellationToken).ConfigureAwait(false);
                    successful = result.IsSuccess &&
                        zones.GetCards(owner, ChoiceZone.BattleArea).Contains(dnaTarget);
                }
            }
            finally
            {
                if (overridden && original is not null)
                {
                    SpecialPlayRecipeRegistry.Register(view.CardNumber, original);
                }
            }
        }

        if (successful && successProcess is not null)
        {
            await successProcess(new CardSource(context, dnaTarget, owner, owner)).ConfigureAwait(false);
        }
        else if (!successful && failedProcess is not null)
        {
            await failedProcess().ConfigureAwait(false);
        }
    }

    /// <summary>(W6 tail) a token's printed data — 1:1 the inline <c>new CEntity_Base{…}</c> specs in
    /// AS-IS <c>ContinuousController.CreateTokenData()</c> (ContinuousController.cs:151-506, verbatim
    /// verified). <see cref="EffectClassName"/> maps to the dispatch <c>effectClass</c> alias so a token
    /// with a card effect resolves it like any ported card.</summary>
    public sealed record TokenSpec(
        string CardNumber, string Name, string Color, int PlayCost, int Level, int Dp,
        string? EffectClassName = null, string? Type = null, string? Form = null, string? Attribute = null);

    /// <summary>The AS-IS token table (ContinuousController.cs:151-506).</summary>
    public static readonly IReadOnlyDictionary<string, TokenSpec> TokenSpecs =
        new Dictionary<string, TokenSpec>(StringComparer.Ordinal)
        {
            ["Diaboromon"] = new("BT2-082-token", "Diaboromon", "White", 14, 6, 3000, null, "Unidentified", "Mega", "Unknown"),
            ["Amon"] = new("BT14-018-token-red", "Amon of Crimson Flame", "Red", -1, 0, 6000, "BT4_038"),
            ["Umon"] = new("BT14-018-token-yellow", "Umon of Blue Thunder", "Yellow", -1, 0, 6000, "BT1_031"),
            ["Fujitsumon"] = new("EX5-058-token", "Fujitsumon", "Purple", -1, 0, 3000, "EX5_058_token"),
            ["Gyuukimon"] = new("LM-018-token", "Gyuukimon", "Purple", 7, 5, 3000, null, "Dark Animal", "Ultimate", "Virus"),
            ["KoHagurumon"] = new("BT16-052-token", "KoHagurumon", "Black", -1, 0, 1000, "BT16_052_token"),
            ["Familiar"] = new("EX7-030-token", "Familiar", "Yellow", -1, 0, 3000, "EX7_030_token"),
            ["SelfDeleteFamiliar"] = new("EX7-030-token-sd", "Familiar", "Yellow", -1, 0, 3000, "P_165_token"),
            ["VoleeZerdrucken"] = new("EX7-058-token", "Volée & Zerdrücken", "Purple", -1, 4, 5000, "EX7_058_token"),
            ["UkaNoMitama"] = new("EX8-037-token", "Uka-no-Mitama", "Yellow", -1, 0, 9000, "EX8_037_token"),
            ["WarGrowlmon"] = new("BT19-091-token-red", "WarGrowlmon", "Red", -1, 0, 6000),
            ["Taomon"] = new("BT19-091-token-yellow", "Taomon", "Yellow", -1, 0, 6000),
            ["Rapidmon"] = new("BT19-091-token-green", "Rapidmon", "Green", -1, 0, 6000),
            ["PipeFox"] = new("BT19-040-token", "Pipe-Fox", "Yellow", -1, 0, 6000, "BT19_040_token"),
            ["AthoRenePor"] = new("BT20-017-token", "Atho, René & Por", "White", -1, 0, 6000, "BT20_017_token"),
            ["Hinukamuy"] = new("BT23-057-token", "HinukamuyToken", "White", -1, 0, 6000, "BT23_057_token"),
            ["Petrification"] = new("BT21-029-token", "Petrification", "White", -1, 0, 3000, "BT21_029_token"),
        };

    /// <summary>AS-IS <c>PlayToken</c> (CardEffectCommons.cs:140-176, verbatim verified): materialize
    /// <paramref name="quantity"/> copies of the token as fresh instances and play them COST-FREE onto the
    /// chosen player's battle area (the AS-IS empty-frame count has no port model — no field-size limit is
    /// modeled anywhere). Tokens carry <c>isToken</c> and register their effect class via the dispatch
    /// alias. Returns the played instance ids.</summary>
    public static async Task<IReadOnlyList<HeadlessEntityId>> PlayToken(
        TokenSpec tokenData, CardSource sourceCard, bool isOwnerPermanent, bool isTapped, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(tokenData);
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        HeadlessPlayerId player = isOwnerPermanent ? sourceCard.Owner : OpponentOf(sourceCard);
        if (player.IsEmpty || quantity <= 0)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        var definitionId = new HeadlessEntityId($"TOKEN:{tokenData.CardNumber}");
        var defMeta = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dp"] = tokenData.Dp,
            ["level"] = tokenData.Level,
            ["colors"] = new[] { tokenData.Color },
        };
        if (tokenData.EffectClassName is not null)
        {
            defMeta["effectClass"] = tokenData.EffectClassName;
        }

        if (tokenData.Type is not null)
        {
            defMeta["traits"] = new[] { tokenData.Type };
        }

        // (AS-IS ContinuousController.CreateTokenData) tokens carry their Form / Attribute too — e.g.
        // Diaboromon (Mega / Unknown), Gyuukimon (Ultimate / Virus). Stored under the same "forms"/"attributes"
        // metadata keys the card loader uses, so a form/attribute-querying effect sees the correct values.
        if (tokenData.Form is not null)
        {
            defMeta["forms"] = new[] { tokenData.Form };
        }

        if (tokenData.Attribute is not null)
        {
            defMeta["attributes"] = new[] { tokenData.Attribute };
        }

        if (context.CardRepository is Headless.DataLoading.CardDatabase database)
        {
            database.Upsert(new CardRecord(
                definitionId, tokenData.CardNumber, tokenData.Name, defMeta, CardType: "Digimon",
                PlayCost: tokenData.PlayCost >= 0 ? tokenData.PlayCost : null));
        }

        var played = new List<HeadlessEntityId>();
        var sink = NewSink(context);
        for (int index = 0; index < quantity; index++)
        {
            var tokenId = new HeadlessEntityId($"{player.Value}:token:{tokenData.CardNumber}:{Guid.NewGuid():N}");
            context.CardInstanceRepository.Upsert(new CardInstanceRecord(
                tokenId, definitionId, player,
                Metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["dp"] = tokenData.Dp,
                    ["isToken"] = true,
                    ["isSuspended"] = isTapped,
                }));
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.PlayCardKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.TargetEntityIdKey] = tokenId.Value,
                    [MatchStateMutationSink.FromZoneKey] = ChoiceZone.None,
                }));
            played.Add(tokenId);
        }

        await sink.FlushAsync().ConfigureAwait(false);
        return played;
    }

    /// <summary>AS-IS <c>PlayDiaboromonToken</c> (CardEffectCommons.cs:182).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayDiaboromonToken(CardSource sourceCard, int quantity = 1) =>
        PlayToken(TokenSpecs["Diaboromon"], sourceCard, isOwnerPermanent: true, isTapped: false, quantity);

    /// <summary>AS-IS <c>PlayAmonToken</c> (:197).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayAmonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Amon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayUmonToken</c> (:211).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayUmonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Umon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayFujitsumonToken</c> (:225) — enters SUSPENDED.</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayFujitsumonToken(CardSource sourceCard, bool isOwnerPermanent) =>
        PlayToken(TokenSpecs["Fujitsumon"], sourceCard, isOwnerPermanent, isTapped: true);

    /// <summary>AS-IS <c>PlayGyuukimonToken</c> (:239).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayGyuukimonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Gyuukimon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayKoHagurumonToken</c> (:253).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayKoHagurumonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["KoHagurumon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayFamiliarToken</c> (:267).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayFamiliarToken(CardSource sourceCard, int quantity = 1) =>
        PlayToken(TokenSpecs["Familiar"], sourceCard, isOwnerPermanent: true, isTapped: false, quantity);

    /// <summary>AS-IS <c>PlaySelfDeleteFamiliarToken</c> (:282).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlaySelfDeleteFamiliarToken(CardSource sourceCard, int quantity = 1) =>
        PlayToken(TokenSpecs["SelfDeleteFamiliar"], sourceCard, isOwnerPermanent: true, isTapped: false, quantity);

    /// <summary>AS-IS <c>PlayVoleeZerdrucken</c> (:297).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayVoleeZerdrucken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["VoleeZerdrucken"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayUkaNoMitama</c> (:311).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayUkaNoMitama(CardSource sourceCard) =>
        PlayToken(TokenSpecs["UkaNoMitama"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayWarGrowlmonToken</c> (:325).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayWarGrowlmonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["WarGrowlmon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayTaomonToken</c> (:339).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayTaomonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Taomon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayRapidmonToken</c> (:353).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayRapidmonToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Rapidmon"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayPipeFox</c> (:367).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayPipeFox(CardSource sourceCard) =>
        PlayToken(TokenSpecs["PipeFox"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayAthoRenePorToken</c> (:381).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayAthoRenePorToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["AthoRenePor"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayHinukamuyToken</c> (:395).</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayHinukamuyToken(CardSource sourceCard) =>
        PlayToken(TokenSpecs["Hinukamuy"], sourceCard, isOwnerPermanent: true, isTapped: false);

    /// <summary>AS-IS <c>PlayPetrificationToken</c> (:409) — always the OPPONENT'S board.</summary>
    public static Task<IReadOnlyList<HeadlessEntityId>> PlayPetrificationToken(CardSource sourceCard, int quantity = 1) =>
        PlayToken(TokenSpecs["Petrification"], sourceCard, isOwnerPermanent: false, isTapped: false, quantity);

    /// <summary>AS-IS <c>CanActivateSave</c> (KeyWordEffects/Save.cs:10, verbatim): the deletion subject's
    /// top reached the trash AND a receiving permanent matching the predicate exists.</summary>
    public static bool CanActivateSave(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? CanSelectPermanentCondition) =>
        IsTopCardInTrashOnDeletion(ctx, card) &&
        HasMatchConditionPermanent(card, p => CanSelectPermanentCondition is null || CanSelectPermanentCondition(p));

    /// <summary>AS-IS <c>SaveProcess</c> (Save.cs:25): choose 1 matching permanent; this card goes from the
    /// trash to the BOTTOM of its digivolution cards.</summary>
    public static async Task SaveProcess(
        Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<Permanent, bool>? CanSelectPermanentCondition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!CanActivateSave(ctx, card, CanSelectPermanentCondition))
        {
            return;
        }

        EngineContext context = card.Context;
        List<ChoiceCandidate> candidates = EnumerateFieldPermanentViews(card, isContainBreedingArea: false)
            .Where(p => CanSelectPermanentCondition is null || CanSelectPermanentCondition(p))
            .Select(p => new ChoiceCandidate(p.InstanceId, p.InstanceId.Value, ChoiceZone.BattleArea, IsSelectable: true, ownerId: p.OwnerId))
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        var request = new ChoiceRequest(
            ChoiceType.Card, card.Owner, "Select 1 permanent that will get a digivolution card.",
            minCount: 0, maxCount: 1, canSkip: true, ChoiceZone.BattleArea, candidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSkipped || result.SelectedIds.Count == 0)
        {
            return;
        }

        await Headless.Runtime.DigivolutionStackHelpers.AddSourcesBottomAsync(
            context.CardInstanceRepository, context.ZoneMover, result.SelectedIds[0],
            new[] { card.InstanceId }, ChoiceZone.Trash, cancellationToken,
            context: context,
            // (F1-Tier2 OnAddDigivolutionCards) Save place-under — the saved card's own effect is the cause.
            gameEventQueue: context.GameEventQueue, causeSourceId: card.InstanceId).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>CanActivateBlitz</c> (KeyWordEffects/Blitz.cs:10, verbatim): on the battle area,
    /// able to attack, the MEMORY sits on the opponent's side (>= 1 for them ⇔ turn-axis current <= -1 —
    /// Blitz fires on its controller's own turn), and no attack is in flight.</summary>
    public static bool CanActivateBlitz(CardSource cardSource)
    {
        ArgumentNullException.ThrowIfNull(cardSource);
        EngineContext context = cardSource.Context;
        return IsExistOnBattleArea(cardSource)
            && !IsSuspended(cardSource, cardSource.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default)
            && context.MemoryController.Current.Current <= -1
            && !context.AttackController.Current.IsPending;
    }

    /// <summary>AS-IS <c>BlitzProcess</c> (Blitz.cs:31): open the attack offer (player + any Digimon,
    /// AS-IS SelectAttackEffect canAttackPlayer/defender = true).</summary>
    public static bool BlitzProcess(CardSource cardSource)
    {
        ArgumentNullException.ThrowIfNull(cardSource);
        if (!CanActivateBlitz(cardSource))
        {
            return false;
        }

        HeadlessEntityId attackerId = cardSource.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default;
        return !attackerId.IsEmpty && Headless.Runtime.EffectDrivenAttack.RequestChoice(
            cardSource.Context, attackerId,
            new Headless.Runtime.EffectAttackOptions(WithoutTap: false, AllowPlayerTarget: true, AllowDigimonTarget: true, TargetUnsuspended: true));
    }

    /// <summary>AS-IS <c>CanActivateFortitude</c> (KeyWordEffects/Fortitude.cs:16): this card is in the
    /// trash, was part of the deleted stack WITH at least one digivolution source, and can re-enter.</summary>
    public static bool CanActivateFortitude(Headless.Effects.CardEffectResolveContext ctx, CardSource card, bool isInheritedEffect = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!IsExistOnTrash(card) || (isInheritedEffect && !CanActivateOnDeletion(ctx, card)))
        {
            return false;
        }

        if (!SubjectPermanentContains(ctx, card) ||
            ctx.EffectContext.TriggerEntityId is not HeadlessEntityId subject)
        {
            return false;
        }

        bool hadSources = card.Context.CardInstanceRepository.TryGetInstance(subject, out CardInstanceRecord? dead) && dead is not null
            && dead.Metadata.TryGetValue(Headless.State.DigivolutionStackReader.SourceIdsKey, out object? raw)
            && raw is IEnumerable<string> ids && ids.Any();
        return hadSources && CanPlayAsNewPermanent(card, payCost: false, null);
    }

    /// <summary>AS-IS <c>FortitudeProcess</c> (Fortitude.cs:54): replay this card from the trash, free.</summary>
    public static Task FortitudeProcess(CardSource card, CardSource sourceCard) =>
        PlayPermanentCards(new[] { card }, sourceCard, payCost: false, isTapped: false, root: ChoiceZone.Trash, activateETB: true);

    /// <summary>AS-IS <c>CanUseIgnoreBattle</c> (CanUseEffects/IgnoreBattle.cs:10) — delegates to the
    /// option-main gate.</summary>
    public static bool CanUseIgnoreBattle(Headless.Effects.CardEffectResolveContext ctx, CardSource card) =>
        CanTriggerOptionMainEffect(ctx, card);

    /// <summary>AS-IS <c>EnforceLocationCheck</c> (GameContextDeterminarion.cs:12): invalidates the AS-IS
    /// trigger/activate permanence cache — headless the cache collapsed (permanent identity = instance id),
    /// so this is a no-op mirror.</summary>
    public static void EnforceLocationCheck()
    {
    }

    /// <summary>AS-IS <c>AddSelfDeleteEffect</c> (GiveEffect/DeleteSelf.cs:14): the permanent deletes
    /// itself at turn end (own / opponent's / each — <paramref name="deleteTiming"/>). Headless: a metadata
    /// marker the turn-end sweep consumes.</summary>
    public static void AddSelfDeleteEffect(Permanent? permanent, string deleteTiming, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (permanent is null || permanent.InstanceId.IsEmpty ||
            !sourceCard.Context.CardInstanceRepository.TryGetInstance(permanent.InstanceId, out CardInstanceRecord? rec) || rec is null)
        {
            return;
        }

        sourceCard.Context.CardInstanceRepository.Upsert(rec with
        {
            Metadata = new Dictionary<string, object?>(rec.Metadata, StringComparer.Ordinal)
            {
                [Headless.Runtime.GameFlowProcessor.DeleteAtTurnEndKey] = deleteTiming,
                [Headless.Runtime.GameFlowProcessor.DeleteAtTurnEndSourceKey] = sourceCard.InstanceId.Value,
            }
        });
    }

    // (G-clean-2) The invented bool BecomeDigimonThatCantDigivolve(...CardSource) substrate — which routed
    // TreatAsDigimon through the GainKeywordToPermanent funnel and the base-DP/no-evolve through the invented
    // ChangeBaseDigimonDP / GainRestrictionToPermanent helpers — is DELETED. The AS-IS-signature
    // Task BecomeDigimonThatCantDigivolve(Permanent, int, EffectDuration, ICardEffect) in
    // GiveEffect/GiveEffectToPermanent/TamerBecomesDigimonThatCanNotDigivolve.cs now builds the three
    // StaticEffects (TreatAsDigimon / ChangeBaseDP / CanNotDigivolve) and stores them in the None bucket, AS-IS 1:1.

    /// <summary>AS-IS <c>DrawAndDiscardCards</c> (CardEffectCommons.cs:1408, verbatim): draw N, then the
    /// trash player discards up to M chosen hand cards.</summary>
    public static async Task DrawAndDiscardCards(
        (HeadlessPlayerId drawPlayer, HeadlessPlayerId trashPlayer) player,
        int drawAmount, int trashAmount, CardSource sourceCard,
        Func<CardSource, bool>? canTrashTargetCondition = null,
        bool canNoSelect = false, bool canEndNotMax = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        var sink = NewSink(context);
        if (drawAmount > 0)
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.DrawCardsKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    [MatchStateMutationSink.PlayerIdKey] = player.drawPlayer.Value,
                    [MatchStateMutationSink.CountKey] = drawAmount,
                }));
            await sink.FlushAsync().ConfigureAwait(false);
        }

        var zones = (IZoneStateReader)context.ZoneMover;
        List<ChoiceCandidate> candidates = zones.GetCards(player.trashPlayer, ChoiceZone.Hand)
            .Where(id => canTrashTargetCondition is null || canTrashTargetCondition(new CardSource(context, id, player.trashPlayer, player.trashPlayer)))
            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.Hand, IsSelectable: true, ownerId: player.trashPlayer))
            .ToList();
        int max = Math.Min(trashAmount, candidates.Count);
        if (max <= 0)
        {
            return;
        }

        var request = new ChoiceRequest(
            ChoiceType.Card, player.trashPlayer, $"Discard {max} card(s).",
            minCount: canNoSelect ? 0 : (canEndNotMax ? 1 : max), maxCount: max,
            canSkip: canNoSelect, ChoiceZone.Hand, candidates);
        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.SelectedIds.Count == 0)
        {
            return;
        }

        var discardSink = NewSink(context);
        foreach (HeadlessEntityId id in result.SelectedIds)
        {
            discardSink.Apply(new EffectMutation(
                MatchStateMutationSink.TrashCardKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = id.Value }));
        }

        await discardSink.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>ReturnRevealedCardsToLibraryBottom</c> (RevealLibrary.cs:469, verbatim): one card
    /// goes straight to the bottom; two-plus open the AS-IS ordering pick (pick order = placement,
    /// lower numbers on top of the bottom stack).</summary>
    public static async Task ReturnRevealedCardsToLibraryBottom(
        IReadOnlyList<CardSource> remainingCards, CardSource sourceCard, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remainingCards);
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (remainingCards.Count == 0)
        {
            return;
        }

        EngineContext context = sourceCard.Context;
        IReadOnlyList<CardSource> ordered = remainingCards;
        if (remainingCards.Count >= 2)
        {
            var request = new ChoiceRequest(
                ChoiceType.Card, sourceCard.Owner, "Specify the order to place the cards at the bottom of the deck.",
                minCount: remainingCards.Count, maxCount: remainingCards.Count, canSkip: false, ChoiceZone.Library,
                remainingCards.Select(cs => new ChoiceCandidate(cs.InstanceId, cs.InstanceId.Value, ChoiceZone.Library, IsSelectable: true, ownerId: cs.Owner)).ToArray());
            ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
            if (result.SelectedIds.Count == remainingCards.Count)
            {
                ordered = result.SelectedIds
                    .Select(id => remainingCards.First(cs => cs.InstanceId == id))
                    .ToArray();
            }
        }

        var sink = NewSink(context);
        foreach (CardSource cs in ordered)
        {
            sink.Apply(new EffectMutation(
                MatchStateMutationSink.ReturnToDeckBottomKind, sourceCard.InstanceId,
                new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = cs.InstanceId.Value }));
        }

        await sink.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>DigivolveIntoExcecutingAreaCard</c> (CardEffectCommons.cs:1106, verbatim —
    /// original spelling kept): the EXECUTION-zone variant of <see cref="DigivolveIntoHandOrTrashCard"/>;
    /// with a single candidate the effect's own card digivolves without a pick.</summary>
    public static async Task DigivolveIntoExcecutingAreaCard(
        Permanent? targetPermanent,
        Func<CardSource, bool>? cardCondition,
        bool payCost,
        (int reduceCost, Func<CardSource, bool>? reduceCostCardCondition)? reduceCostTuple,
        (int fixedCost, Func<CardSource, bool>? fixedCostCardCondition)? fixedCostTuple,
        int ignoreDigivolutionRequirementFixedCost,
        CardSource sourceCard,
        Func<Task>? successProcess,
        bool ignoreSelection = false,
        IgnoreRequirement ignoreRequirements = IgnoreRequirement.None,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        bool onlySelf = ignoreSelection ||
            (context.ZoneMover is IZoneStateReader z &&
             z.GetCards(sourceCard.Owner, ChoiceZone.Execution).Count(id =>
                new CardSource(context, id, sourceCard.Owner, sourceCard.Owner).IsDigimon &&
                (cardCondition is null || cardCondition(new CardSource(context, id, sourceCard.Owner, sourceCard.Owner)))) <= 1);
        await DigivolveIntoZoneCoreAsync(
            targetPermanent, cardCondition, payCost, reduceCostTuple, fixedCostTuple,
            ignoreDigivolutionRequirementFixedCost, ChoiceZone.Execution, sourceCard,
            successProcess, failedProcess: null, ignoreSelection: onlySelf, ignoreRequirements, isOptional: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>AS-IS <c>ActivateMainOfOptionSide</c> (CardEffectCommons.cs:733): re-run the card's [Main]
    /// (OptionSkill) activated effect — headless the activation resolver drives it.</summary>
    public static Task<int> ActivateMainOfOptionSide(CardSource card, CardSource sourceCard, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        // (#13) AS-IS OptionMainEffect(card): re-run ONLY the [Main]-tagged OptionSkill effect, not every one.
        return ActivatedEffectResolver.ResolveAsync(card.Context, card.InstanceId, card.Owner, EffectTiming.OptionSkill, cancellationToken,
            effectFilter: ActivatedEffectResolver.IsMainOptionEffect);
    }

    /// <summary>AS-IS <c>DNADigivolveWithHandOrTrashCardIntoHandOrTrash</c> (DNADigivolveEffects.cs:256)
    /// — plays a TEMPORARY permanent from hand/trash as one DNA material mid-flow (PlayTempPermanent +
    /// rollback). That transient-permanent machinery has no headless surface. STOP.</summary>
    public static Task DNADigivolveWithHandOrTrashCardIntoHandOrTrash(CardSource sourceCard) =>
        throw new NotSupportedException("DNA-with-temporary-material is not modeled — STOP (strong model).");

    // AS-IS AddEffectToPermanent(targetPermanent, effectDuration, card, cardEffect, timing) lives at its AS-IS path
    // in the sibling partial file CardEffectCommons/GiveEffect/GiveEffectToPermanentOrPlayer.cs (mirror-into-asis-file
    // rule) — W3 resolved RD-P6C3-C1 there (AS-IS duration-bucket store for new-model effects; the OLD-model
    // registry-lowering path is preserved there as the batch-C transitional).

    /// <summary>(PRIM-P0 B.O.5-tail) AS-IS temp <c>AddEffectToPermanent</c> for a SELF-[On Deletion] grant — the
    /// nested effect must fire ON the target's OWN removal (e.g. EX8_059 "1 Digimon gains '[On Deletion] ...'
    /// until end of turn"). Same as <see cref="AddEffectToPermanent"/> but stamps the binding SurviveOwnLeave (so
    /// leave-play cleanup does not drop it before OnDeletion resolves) + DelayedOneShot (removed after it fires),
    /// with the <paramref name="effectDuration"/> as the backstop for a non-deletion departure. The nested effect
    /// should be built with the TARGET's CardSource and self-gate on the deletion subject (TriggerEntityId).</summary>
    public static void AddSelfRemovalEffectToPermanent(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource card, ICardEffect cardEffect, EffectTiming timing)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardEffect);
        _ = timing;
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return;
        }

        // (P6 cluster3) old-model lowering via LegacyBindingBridge — NEW-model effect = STOP (RD-P6C3-C1).
        if (!LegacyBindingBridge.TryToBinding(
                cardEffect,
                $"{card.InstanceId.Value}:addSelfRemovalEffect:{targetPermanent.InstanceId.Value}:{Guid.NewGuid():N}",
                out EffectBinding? binding) || binding is null)
        {
            throw new NotSupportedException(
                $"AddSelfRemovalEffectToPermanent: '{cardEffect.GetType().Name}' is a NEW-model effect — no new-model permanent grant store exists yet (design item RD-P6C3-C1).");
        }

        var values = new Dictionary<string, object?>(binding.Request.Context.Values, StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.SurviveOwnLeaveKey] = true,
            [AutoProcessingTriggerCollector.DelayedOneShotKey] = true,
        };
        var retargeted = new EffectContext(
            binding.Request.Context.SourcePlayerId,
            binding.Request.Context.OwnerPlayerId,
            binding.Request.Context.SourceEntityId,
            binding.Request.Context.TriggerEntityId,
            targetEntityIds: new[] { targetPermanent.InstanceId },
            values: values);
        card.Context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(binding.Request.EffectId, binding.Request.ControllerId, binding.Request.Timing, retargeted),
            binding.Keywords, binding.QueryRoles, binding.QueryScopes, binding.Effect, effectDuration));
    }

    // (R3-C2b-2) AS-IS `AddEffectToPlayer(effectDuration, card, cardEffect, timing, getCardEffect = null)` — the
    // SINGLE AS-IS method — now lives in its AS-IS home file next to AddEffectToPermanent:
    // CardEffectCommons/GiveEffect/GiveEffectToPermanentOrPlayer.cs (bucket storage into the owning Player's
    // Until*Effects lists, AS-IS 1:1). The pre-R3 registry-lowering / DelayedOneShot overloads that used to live
    // here are retired (RD-P6C3-C1 resolved): every live caller passes a new-model effect, and the flipped window
    // enumerates player.EffectList(timing), so the bucket path is the sole path.

    /// <summary>AS-IS <c>CardEffectCommons.GetCardEffectByEffectTiming(timing, cardEffect)</c>
    /// (CardEffectCommons.cs:1402): the deferred selector that yields <paramref name="cardEffect"/> only when
    /// re-queried at <paramref name="timing"/> (else null) — the delegate the AS-IS Player buckets store.</summary>
    public static Func<EffectTiming, ICardEffect> GetCardEffectByEffectTiming(EffectTiming timing, ICardEffect cardEffect)
        => (_timing) => _timing == timing ? cardEffect : null!;


    // (이연④-f) AddContinuousEffectToPlayer + AddCanNotPlayOptionToPlayer DELETED with the CanNotPlayOption registry
    // teardown. They were the substrate carrier for EX1_072's player-bucket CanNotPlay grant (old-model
    // ContinuousCanNotPlayOptionEffect registry-half, re-sourced to a synthetic player id + EffectDurationExpiry).
    // RD-P6C3-C1 is now RESOLVED: EX1_072 emits the AS-IS kind-class `CanNotPlayClass` and stores it via the AS-IS
    // `AddEffectToPlayer(duration, card, cardEffect, timing: None)` player Until*Effects bucket (GiveEffect/
    // GiveEffectToPermanentOrPlayer.cs), read LIVE by CanNotPlayOptionScan region ① and cleared by the AS-IS
    // turn-end bucket reset (HeadlessEndTurnCleanupFlow) — no registry, no ToBinding.

    /// <summary>(W6-G) shared restriction-grant core — AS-IS GiveEffectToPermanent shape: target-locked,
    /// duration-tagged restriction binding with the LIVE CanUse (on field && !CanNotBeAffected) plus an
    /// optional counterpart predicate (attackerCondition / defenderCondition) evaluated by the gates.</summary>
    private static bool GainRestrictionToPermanent(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard,
        string restrictionKey, string gainName,
        Func<Permanent, bool>? counterpartCondition = null,
        Func<bool>? extraCondition = null,
        Func<CardSource, bool>? causingEffectPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null || targetPermanent.InstanceId.IsEmpty)
        {
            return false;
        }

        EngineContext context = sourceCard.Context;
        HeadlessEntityId targetId = targetPermanent.InstanceId;
        HeadlessPlayerId targetOwner = targetPermanent.OwnerId;
        var zones = (IZoneStateReader)context.ZoneMover;
        if (!zones.GetCards(targetOwner, ChoiceZone.BattleArea).Contains(targetId))
        {
            return false;
        }

        // (B군 P0-1) AS-IS grant-time guard `!targetPermanent.TopCard.CanNotBeAffected(cardEffect)` — rehomed from
        // the now-dead BlocksOpponentEffect registry scan (0 producers post W3c-1/2) to the AS-IS-literal live
        // ICanNotAffectedEffect scan (W3c-1 idiom). The causing effect is collapsed to its source card
        // (BareCauseEffect); an immune target refuses the grant.
        if (targetPermanent.TopCard.CanNotBeAffected(BareCauseEffect.For(sourceCard)))
        {
            return false;
        }

        HeadlessEntityId grantSourceId = sourceCard.InstanceId;
        Func<bool> liveCondition = () =>
            ((IZoneStateReader)context.ZoneMover).GetCards(targetOwner, ChoiceZone.BattleArea).Contains(targetId)
            && !new Permanent(context, targetId, targetOwner).TopCard.CanNotBeAffected(BareCauseEffect.For(context, grantSourceId))
            && (extraCondition is null || extraCondition());
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [restrictionKey] = true,
            [ContinuousScopeEvaluation.ConditionKey] = liveCondition,
        };
        if (causingEffectPredicate is not null)
        {
            // The restriction fires only when the CAUSING effect's source matches (AS-IS cardEffectCondition),
            // read by the sink's IsRestrictedFromCause. Without it the restriction is unconditional.
            values[RestrictionHelpers.CausingEffectPredicateKey] = causingEffectPredicate;
        }

        // (joint-migration) additively emit the canonical joint predicate: subject = the granted target permanent;
        // the 2nd arg (counterpart participant for Attack/Block/…, or the causing effect source) must satisfy any
        // provided predicate.
        HeadlessEntityId subjectId = targetId;
        Func<Permanent, bool>? cpCond = counterpartCondition;
        Func<CardSource, bool>? causingP2 = causingEffectPredicate;
        values[JointRestrictionEffect.PredicateKey(restrictionKey)] = (Func<CardSource, CardSource?, bool>)((subject, cp) =>
            subject.InstanceId == subjectId
            && (cpCond is null || (cp is not null && cpCond(new Permanent(cp.Context, cp.InstanceId, cp.Owner))))
            && (causingP2 is null || (cp is not null && causingP2(cp))));

        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, sourceCard.InstanceId,
            triggerEntityId: null, targetEntityIds: new[] { targetId }, values: values);
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:{gainName}:{targetId.Value}"),
                sourceCard.Controller, "Continuous", effectContext),
            keywords: null, EffectQueryRole.Continuous, new[] { ContinuousRestrictionGate.Scope },
            effect: null, duration: effectDuration));
        return true;
    }

    /// <summary>AS-IS <c>GainCanNotAttack</c> (GiveEffect/GiveEffectToPermanent/CanNotAttack.cs:10) —
    /// <paramref name="defenderCondition"/> narrows WHICH defenders this permanent cannot attack.
    /// (J-1) CardSource-only substrate entry: routes to the AS-IS 1:1 body (home file) with the cause collapsed
    /// to <see cref="BareCauseEffect"/> (this signature carries no live <c>activateClass</c>).</summary>
    public static bool GainCanNotAttack(
        Permanent? targetPermanent, Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't attack") =>
        GainCanNotAttackImpl(targetPermanent, defenderCondition, effectDuration,
            card: sourceCard, cause: BareCauseEffect.For(sourceCard), effectName);

    /// <summary>AS-IS <c>GainCanNotBlock</c> (…/CanNotBlock.cs:10) — <paramref name="attackerCondition"/>
    /// narrows WHICH attackers this permanent cannot block.
    /// (J-1) CardSource-only substrate entry: routes to the AS-IS 1:1 body with the cause collapsed to
    /// <see cref="BareCauseEffect"/>.</summary>
    public static bool GainCanNotBlock(
        Permanent? targetPermanent, Func<Permanent, bool>? attackerCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't block") =>
        GainCanNotBlockImpl(targetPermanent, attackerCondition, effectDuration,
            card: sourceCard, cause: BareCauseEffect.For(sourceCard), effectName);

    /// <summary>AS-IS <c>GainCanNotBeAttacked</c> (…/CanNotBeAttacked.cs:10).</summary>
    public static bool GainCanNotBeAttacked(
        Permanent? targetPermanent, Func<Permanent, bool>? attackerCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be attacked") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotBeAttackedKey, "gainCanNotBeAttacked", attackerCondition);

    /// <summary>AS-IS <c>GainCanNotBeBlocked</c> (…/CanNotBeBlocked.cs:10).</summary>
    public static bool GainCanNotBeBlocked(
        Permanent? targetPermanent, Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be blocked") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotBeBlockedKey, "gainCanNotBeBlocked", defenderCondition);

    /// <summary>AS-IS <c>GainCanNotSuspend</c> (…/CanNotSuspend.cs:34).</summary>
    public static bool GainCanNotSuspend(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard,
        Func<bool>? condition = null, string effectName = "Can't suspend") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotSuspendKey, "gainCanNotSuspend", extraCondition: condition);

    /// <summary>AS-IS <c>GainCantSuspendUntilOpponentTurnEnd</c> (…/CanNotSuspend.cs:8).</summary>
    public static bool GainCantSuspendUntilOpponentTurnEnd(Permanent? targetPermanent, CardSource sourceCard) =>
        GainCanNotSuspend(targetPermanent, EffectDuration.UntilOpponentTurnEnd, sourceCard);

    /// <summary>AS-IS <c>GainCanNotUnsuspend</c> (…/CanNotUnsuspend.cs:69).
    /// (J-2) CardSource-only substrate core: routes to the AS-IS 1:1 body (home file) — the target permanent's
    /// duration bucket gets the <c>CanNotUnsuspendClass</c> read LIVE by <see cref="Permanent.CanUnsuspend"/> —
    /// with the cause collapsed to <see cref="BareCauseEffect"/> (this signature carries no live
    /// <c>activateClass</c>). The invented registry <c>CannotUnsuspendKey</c> arm is retired (it had no reader).</summary>
    public static bool GainCanNotUnsuspend(
        Permanent? targetPermanent, EffectDuration effectDuration, CardSource sourceCard,
        Func<bool>? condition = null, string effectName = "Can't unsuspend") =>
        GainCanNotUnsuspendImpl(targetPermanent, effectDuration,
            card: sourceCard, cause: BareCauseEffect.For(sourceCard), condition, effectName);

    /// <summary>AS-IS <c>GainCantUnsuspendUntilOpponentTurnEnd</c> (…/CanNotUnsuspend.cs:45).</summary>
    public static bool GainCantUnsuspendUntilOpponentTurnEnd(Permanent? targetPermanent, CardSource sourceCard) =>
        GainCanNotUnsuspend(targetPermanent, EffectDuration.UntilOpponentTurnEnd, sourceCard);

    /// <summary>AS-IS <c>GainCantUnsuspendNextActivePhase</c> (…/CanNotUnsuspend.cs:10) — the AS-IS CanUse
    /// ("opponent turn AND active phase") is equivalent headless: the CannotUnsuspend gate is only
    /// consulted BY the unsuspend step, and <see cref="EffectDuration.UntilNextUntap"/> expires the grant
    /// right after that step.</summary>
    public static bool GainCantUnsuspendNextActivePhase(Permanent? targetPermanent, CardSource sourceCard) =>
        GainCanNotUnsuspend(targetPermanent, EffectDuration.UntilNextUntap, sourceCard);

    /// <summary>(W6 tail) shared PLAYER-SCOPE timed grant core — the AS-IS GiveEffectToPlayer shape
    /// (verbatim verified): a duration-tagged player-scope binding whose PermanentCondition folds the
    /// battle-area + live !CanNotBeAffected guards around the caller's predicate.</summary>
    private static bool GainToPlayerScope(
        EffectDuration effectDuration, CardSource sourceCard, string gainName,
        Func<Permanent, bool>? permanentCondition,
        string? keyword = null, string? valueKey = null, object? value = null,
        IReadOnlyDictionary<string, object?>? extraValues = null,
        Func<bool>? extraCondition = null,
        string? scopeOverride = null,
        Func<CardSource, bool>? counterpartPredicate = null)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        EngineContext context = sourceCard.Context;
        HeadlessEntityId grantSourceId = sourceCard.InstanceId;

        // AS-IS _PermanentCondition: on the battle area && !CanNotBeAffected && caller predicate — LIVE.
        // (B군 P0-1) The !CanNotBeAffected term is rehomed from the now-dead BlocksOpponentEffect registry scan to
        // the AS-IS-literal live TopCard.CanNotBeAffected getter (cause = the granting effect's source card).
        Func<CardSource, bool> scopePredicate = cs =>
            !new Permanent(cs.Context, cs.InstanceId, cs.Owner).TopCard.CanNotBeAffected(BareCauseEffect.For(context, grantSourceId))
            && (permanentCondition is null || permanentCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner)));

        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Headless.Effects.PlayerScopeContinuousHelpers.PlayerScopeKey] = true,
            [Headless.Effects.PlayerScopeContinuousHelpers.ScopePlayerIdKey] = sourceCard.Owner.Value,
            [Headless.Effects.PlayerScopeContinuousHelpers.ScopePredicateKey] = scopePredicate,
        };
        if (valueKey is not null)
        {
            values[valueKey] = value;
        }

        if (extraValues is not null)
        {
            foreach (KeyValuePair<string, object?> pair in extraValues)
            {
                values[pair.Key] = pair.Value;
            }
        }

        if (extraCondition is not null)
        {
            values[ContinuousScopeEvaluation.ConditionKey] = extraCondition;
        }

        // (joint-migration) canonical joint for player-scope restriction grants: this scope player's permanents (that
        // pass the live scope predicate — battle area, immunity, caller filter) cannot X the counterpart, when the
        // counterpart (attacker/defender being blocked) also satisfies the caller's counterpart predicate.
        if (valueKey is not null && value is bool on && on && RestrictionHelpers.IsRestrictionKey(valueKey))
        {
            HeadlessPlayerId scopePlayer = sourceCard.Owner;
            Func<CardSource, bool> subjectPredicate = scopePredicate;
            Func<CardSource, bool>? cpPredicate = counterpartPredicate;
            values[JointRestrictionEffect.PredicateKey(valueKey)] = (Func<CardSource, CardSource?, bool>)((subject, counterpart) =>
                subject.Owner == scopePlayer
                && subjectPredicate(subject)
                && (cpPredicate is null || (counterpart is not null && cpPredicate(counterpart))));
        }

        var effectContext = new EffectContext(
            sourceCard.Controller, sourceCard.Owner, sourceCard.InstanceId,
            triggerEntityId: null, targetEntityIds: Array.Empty<HeadlessEntityId>(), values: values);
        string[]? scopes = keyword is not null ? null : new[] { scopeOverride ?? ContinuousRestrictionGate.Scope };
        context.EffectRegistry.Register(new EffectBinding(
            new EffectRequest(
                new HeadlessEntityId($"{sourceCard.InstanceId.Value}:{gainName}:{Guid.NewGuid():N}"),
                sourceCard.Controller, "Continuous", effectContext),
            keywords: keyword is null ? null : new[] { keyword },
            EffectQueryRole.Continuous, scopes, effect: null, duration: effectDuration));
        return true;
    }

    // (G-clean-2) GainBlockerPlayerEffect / GainRushPlayerEffect / GainIcecladPlayerEffect (the invented
    // GainToPlayerScope keyword-marker player-scope wrappers) are DELETED — the AS-IS-signature
    // Task GainBlockerPlayerEffect / GainRushPlayerEffect / GainIcecladPlayerEffect (KeyWordEffects/*.cs) now
    // build the keyword's StaticEffect and store it in the owning player's None bucket via AddEffectToPlayer,
    // AS-IS 1:1 (read by Permanent.Has<Keyword>'s player.EffectList(None) scan). GainAlliancePlayerEffect is
    // retained: Alliance is a firing-window keyword (C-Atk) whose player-scope grant still rides the surviving
    // GainToPlayerScope funnel (out of the 충실-7 grant scope).
    /// <summary>AS-IS <c>GainAlliancePlayerEffect</c> (KeyWordEffects/Alliance.cs:180).</summary>
    public static bool GainAlliancePlayerEffect(Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard) =>
        throw new NotSupportedException("GainAlliancePlayerEffect: player-scope Alliance grant has no live reader after the keyword registry-half retirement (design item RD-RC-03) — rehome to the AS-IS player-bucket StaticEffect idiom when a caller appears.");

    /// <summary>AS-IS <c>GainCanNotUnsuspendPlayerEffect</c> (GiveEffectToPlayer/CanNotUnsuspend.cs:10, verbatim).
    /// (J-2) CardSource-only substrate entry: routes to the AS-IS 1:1 body (home file) — the owning player's
    /// duration bucket gets the <c>CanNotUnsuspendClass</c> whose PermanentCondition folds battle-area +
    /// <c>!CanNotBeAffected</c> + caller filter + (when <paramref name="isOnlyActivePhase"/>) the turn-player
    /// narrowing, and whose CanUseCondition gates on the Active phase — read LIVE by
    /// <see cref="Permanent.CanUnsuspend"/> (player arm) — with the cause collapsed to <see cref="BareCauseEffect"/>
    /// (this signature carries no live <c>activateClass</c>).</summary>
    public static bool GainCanNotUnsuspendPlayerEffect(
        Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard,
        bool isOnlyActivePhase = false, string effectName = "Can't unsuspend") =>
        GainCanNotUnsuspendPlayerEffectImpl(permanentCondition, effectDuration,
            card: sourceCard, cause: BareCauseEffect.For(sourceCard), isOnlyActivePhase, effectName);

    /// <summary>AS-IS <c>GainCanNotSuspendPlayerEffect</c> (GiveEffectToPlayer/CanNotSuspend.cs:10).</summary>
    public static bool GainCanNotSuspendPlayerEffect(
        Func<Permanent, bool>? permanentCondition, EffectDuration effectDuration, CardSource sourceCard,
        bool isOnlyActivePhase = false, string effectName = "Can't suspend")
    {
        EngineContext context = sourceCard.Context;
        Func<Permanent, bool> composed = p =>
            (permanentCondition is null || permanentCondition(p))
            && (!isOnlyActivePhase || context.TurnController.Current.TurnPlayerId == p.OwnerId);
        return GainToPlayerScope(effectDuration, sourceCard, "gainCanNotSuspendPlayer", composed,
            valueKey: RestrictionHelpers.CannotSuspendKey, value: true);
    }

    /// <summary>AS-IS <c>GainCanNotAttackPlayerEffect</c> (GiveEffectToPlayer/CanNotAttack.cs:10, verbatim).
    /// (J-1) CardSource-only substrate entry: routes to the AS-IS 1:1 body (home file) — the owning player's
    /// duration bucket gets the <c>CanNotAttackTargetDefendingPermanentClass</c> whose AttackerCondition folds the
    /// battle-area + <c>!CanNotBeAffected</c> + caller filter — with the cause collapsed to
    /// <see cref="BareCauseEffect"/> (this signature carries no live <c>activateClass</c>).</summary>
    public static bool GainCanNotAttackPlayerEffect(
        Func<Permanent, bool>? attackerCondition, Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't attack") =>
        GainCanNotAttackPlayerEffectImpl(attackerCondition, defenderCondition, effectDuration,
            card: sourceCard, cause: BareCauseEffect.For(sourceCard), effectName);

    /// <summary>AS-IS <c>GainCanNotBlockPlayerEffect</c> (GiveEffectToPlayer/CanNotBlock.cs:10).</summary>
    public static bool GainCanNotBlockPlayerEffect(
        Func<Permanent, bool>? attackerCondition, Func<Permanent, bool>? defenderCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't block")
    {
        // AS-IS naming quirk: the SUBJECT filter arrives as attackerCondition; the counterpart (the
        // attacker being blocked) as defenderCondition — it rides the joint counterpart predicate.
        Func<CardSource, bool>? attackerBlockedPredicate = defenderCondition is null
            ? null
            : cs => defenderCondition(new Permanent(cs.Context, cs.InstanceId, cs.Owner));
        return GainToPlayerScope(effectDuration, sourceCard, "gainCanNotBlockPlayer", attackerCondition,
            valueKey: RestrictionHelpers.CannotBlockKey, value: true, counterpartPredicate: attackerBlockedPredicate);
    }

    /// <summary>AS-IS <c>GainCanNotBeDeletedPlayerEffect</c> (GiveEffectToPlayer/CanNotBeDeletedByBattle.cs:10)
    /// — the BATTLE-deletion immunity, player-scoped, with the 4-arg battle predicate.</summary>
    public static bool GainCanNotBeDeletedPlayerEffect(
        Func<Permanent, bool>? permanentCondition,
        Func<Permanent, Permanent, Permanent, CardSource, bool>? canNotBeDestroyedByBattleCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be deleted in battle")
    {
        var extra = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (canNotBeDestroyedByBattleCondition is not null)
        {
            extra[BattleDeletionGate.BattleConditionKey] = canNotBeDestroyedByBattleCondition;
        }

        return GainToPlayerScope(effectDuration, sourceCard, "gainCanNotBeDeletedPlayer", permanentCondition,
            valueKey: BattleDeletionGate.PreventBattleDeletionKey, value: true, extraValues: extra);
    }

    /// <summary>AS-IS <c>GainCanNotReturnToHand</c> (GiveEffectToPermanent/CanNotReturnToHand.cs:10) — the
    /// causing-effect predicate maps to the source-card predicate the return gate evaluates.</summary>
    public static bool GainCanNotReturnToHand(
        Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to hand") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotReturnToHandKey, "gainCanNotReturnToHand",
            causingEffectPredicate: cardEffectSourceCondition);

    /// <summary>AS-IS <c>GainCanNotReturnToDeck</c> (…/CanNoReturnToDeck.cs:10).</summary>
    public static bool GainCanNotReturnToDeck(
        Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to deck") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotReturnToDeckKey, "gainCanNotReturnToDeck",
            causingEffectPredicate: cardEffectSourceCondition);

    /// <summary>AS-IS <c>GainCanNotReturnToHandPlayerEffect</c> (GiveEffectToPlayer/CanNotReturnToHand.cs:10).</summary>
    public static bool GainCanNotReturnToHandPlayerEffect(
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to hand") =>
        GainToPlayerScope(effectDuration, sourceCard, "gainCanNotReturnToHandPlayer", permanentCondition,
            valueKey: RestrictionHelpers.CannotReturnToHandKey, value: true,
            extraValues: CausingEffectValues(cardEffectSourceCondition));

    /// <summary>AS-IS <c>GainCanNotReturnToDeckPlayerEffect</c> (GiveEffectToPlayer/CanNoReturnToDeck.cs:10).</summary>
    public static bool GainCanNotReturnToDeckPlayerEffect(
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't return to deck") =>
        GainToPlayerScope(effectDuration, sourceCard, "gainCanNotReturnToDeckPlayer", permanentCondition,
            valueKey: RestrictionHelpers.CannotReturnToDeckKey, value: true,
            extraValues: CausingEffectValues(cardEffectSourceCondition));

    /// <summary>AS-IS <c>GainImmuneFromDPMinus</c> (GiveEffectToPermanent/ImmuneFromDPMinus.cs:10):
    /// this permanent ignores DP-minus effects for the duration (only from matching causing effects).</summary>
    public static bool GainImmuneFromDPMinus(
        Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Immune from DP minus") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            ReplacementHelpers.ImmuneFromDpMinusKey, "gainImmuneFromDpMinus",
            causingEffectPredicate: cardEffectSourceCondition);

    /// <summary>AS-IS <c>GainImmuneFromDPMinusPlayerEffect</c> (GiveEffectToPlayer/ImmuneFromDPMinus.cs:10).</summary>
    public static bool GainImmuneFromDPMinusPlayerEffect(
        Func<Permanent, bool>? permanentCondition, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Immune from DP minus") =>
        GainToPlayerScope(effectDuration, sourceCard, "gainImmuneFromDpMinusPlayer", permanentCondition,
            valueKey: ReplacementHelpers.ImmuneFromDpMinusKey, value: true,
            extraValues: CausingEffectValues(cardEffectSourceCondition));

    /// <summary>Wrap a causing-effect predicate as continuous-binding values (or null when absent), so a
    /// player-scope grant carries the AS-IS <c>cardEffectCondition</c> the sink / DP gate evaluate.</summary>
    private static IReadOnlyDictionary<string, object?>? CausingEffectValues(Func<CardSource, bool>? cardEffectSourceCondition) =>
        cardEffectSourceCondition is null
            ? null
            : new Dictionary<string, object?>(StringComparer.Ordinal) { [RestrictionHelpers.CausingEffectPredicateKey] = cardEffectSourceCondition };

    /// <summary>AS-IS <c>GainCanNotBeDeletedByEffect</c> (GiveEffectToPermanent/CanNotBeDeletedByEffect.cs:10)
    /// — skill/effect-deletion immunity for the duration (the effect-delete gate's key).</summary>
    public static bool GainCanNotBeDeletedByEffect(
        Permanent? targetPermanent, Func<CardSource, bool>? cardEffectSourceCondition,
        EffectDuration effectDuration, CardSource sourceCard, string effectName = "Can't be deleted by effects") =>
        GainRestrictionToPermanent(targetPermanent, effectDuration, sourceCard,
            RestrictionHelpers.CannotBeDeletedBySkillKey, "gainCanNotBeDeletedByEffect",
            causingEffectPredicate: cardEffectSourceCondition);

    /// <summary>AS-IS <c>ChangeDigimonSAttackPlayerEffect</c> (GiveEffectToPlayer/ChangeSAttack.cs:10).
    /// (R3-W3c-3) Restored to AS-IS 1:1: the dead registry ContinuousModifierGate binding (SecurityAttackDelta,
    /// read by the retired ResolveSAttack fold) is replaced by the factory ChangeSAttackClass (a player-scope
    /// IChangeSAttackEffect) stored in the OWNING PLAYER's None duration bucket via AddEffectToPlayer —
    /// Permanent.SAttack scans it in its player-effect region.</summary>
    public static bool ChangeDigimonSAttackPlayerEffect(
        Func<Permanent, bool>? permanentCondition, int changeValue, EffectDuration effectDuration, CardSource sourceCard,
        ICardEffect? activateClass = null)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (changeValue == 0)
        {
            return false;
        }

        CardSource card = sourceCard;
        CardEffects.ChangeSAttackClass changeSAttackClass = null!;

        bool PermanentCondition(Permanent permanent)
        {
            if (IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(activateClass ?? changeSAttackClass))
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanUseCondition() => true;

        changeSAttackClass = CardEffectFactory.ChangeSAttackStaticEffect(
            permanentCondition: PermanentCondition,
            changeValue: changeValue,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition);

        AddEffectToPlayer(effectDuration: effectDuration, card: card, cardEffect: changeSAttackClass, timing: EffectTiming.None);
        return true;
    }

    /// <summary>AS-IS <c>ChangePlayCostPlayerEffect</c> (GiveEffectToPlayer/ChangePlayCost.cs:11):
    /// duration-tagged ±play cost on EVERY permanent matching the predicate — a duration-tagged PLAYER-SCOPE
    /// modifier; the AS-IS <c>setFixedCost</c> form pins the cost instead of shifting it.
    /// (W3c-final 3차) Restored to AS-IS 1:1: the mid-migration registry <see cref="ContinuousModifierGate"/>
    /// binding (a legacy cost-fold producer) is replaced by the factory <see cref="CardEffectFactory.ChangePlayCostStaticEffect{T}"/>
    /// (a player-scope <c>ChangeCostClass</c> whose PermanentCondition folds the battle-area + !CanNotBeAffected +
    /// user predicate) stored in the OWNING PLAYER's None duration bucket via <see cref="AddEffectToPlayer"/> —
    /// read by <c>CardSource.GetChangedCostItselef</c>'s "effects of players" scan (player.EffectList(None)).
    /// Mirrors <see cref="ChangeDigimonDPPlayerEffect"/> exactly (the DP sibling already restored this way).</summary>
    public static bool ChangePlayCostPlayerEffect(
        Func<Permanent, bool>? permanentCondition, int changeValue, bool setFixedCost,
        EffectDuration effectDuration, CardSource sourceCard)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (changeValue == 0)
        {
            return false;
        }

        CardSource card = sourceCard;
        CardEffects.ChangeCostClass changeCostClass = null!;

        bool PermanentCondition(Permanent permanent)
        {
            if (IsPermanentExistsOnBattleArea(permanent))
            {
                if (!permanent.TopCard.CanNotBeAffected(changeCostClass))
                {
                    if (permanentCondition == null || permanentCondition(permanent))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        bool CanUseCondition() => true;

        changeCostClass = CardEffectFactory.ChangePlayCostStaticEffect(
            changeValue: changeValue,
            permanentCondition: PermanentCondition,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition,
            setFixedCost: setFixedCost);

        AddEffectToPlayer(effectDuration: effectDuration, card: card, cardEffect: changeCostClass, timing: EffectTiming.None);
        return true;
    }

    /// <summary>AS-IS <c>ChangeBaseDigimonDP</c> (GiveEffectToPermanent/ChangeOriginDP.cs:10, verbatim):
    /// SET the target's base DP to <paramref name="changeValue"/> for the duration (a base-DP override,
    /// not a delta). (R3-W3c-3) Restored to AS-IS 1:1: the dead registry ContinuousModifierGate binding
    /// (baseDpDelta approximation) is replaced by the factory ChangeBaseDPClass (an IChangeBaseDPEffect whose
    /// GetDP OVERWRITES base DP) stamped with SetActivatedTime and stored in the target's None duration
    /// bucket via AddEffectToPermanent — Permanent.BaseDP scans it (ordered by ActivatedTime).</summary>
    public static bool ChangeBaseDigimonDP(
        Permanent? targetPermanent, int changeValue, EffectDuration effectDuration, CardSource sourceCard,
        ICardEffect? activateClass = null)
    {
        ArgumentNullException.ThrowIfNull(sourceCard);
        if (targetPermanent is null) return false;
        if (!IsPermanentExistsOnBattleArea(targetPermanent)) return false;
        if (changeValue == 0) return false;

        CardSource card = sourceCard;
        CardEffects.ChangeBaseDPClass changeBaseDPClass = null!;

        bool CanUseCondition()
        {
            if (IsPermanentExistsOnBattleArea(targetPermanent))
            {
                if (!targetPermanent.TopCard.CanNotBeAffected(activateClass ?? changeBaseDPClass))
                {
                    return true;
                }
            }

            return false;
        }

        changeBaseDPClass = CardEffectFactory.ChangeBaseDPStaticEffect(
            targetPermanent: targetPermanent,
            changeValue: changeValue,
            isInheritedEffect: false,
            card: card,
            condition: CanUseCondition);

        changeBaseDPClass.SetActivatedTime(DateTime.Now);

        AddEffectToPermanent(
            targetPermanent: targetPermanent, effectDuration: effectDuration, card: card,
            cardEffect: changeBaseDPClass, timing: EffectTiming.None);
        return true;
    }

    // (G-clean-2) The invented per-keyword bool Gain* wrappers (Blocker/Rush/Pierce/Retaliation/Collision/
    // Jamming/Reboot/Alliance/Evade/Raid/Vortex/Execute/Fortitude/Iceclad/Barrier/Blitz) that routed grants
    // through the GainKeywordToPermanent registry-marker funnel are DELETED. Every keyword's grant is now the
    // AS-IS-signature Task Gain<Keyword>(Permanent, EffectDuration, ICardEffect) in KeyWordEffects/<Keyword>.cs,
    // which builds the keyword's Static/ActivateClass effect and stores it in the target permanent's duration
    // bucket via AddEffectToPermanent (read by Permanent.Has<Keyword> / the deletion window / the OnEndTurn
    // window / OnAllyAttack), AS-IS 1:1.

    /// <summary>It is the card owner's turn.</summary>
    public static bool IsOwnerTurn(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return TurnOwnershipHelpers.IsOwnerTurn(card.Context.TurnController.Current.TurnPlayerId, card.Owner);
    }

    /// <summary>AS-IS <c>GManager.instance.turnStateMachine.gameContext.TurnPhase == GameContext.phase.Active</c>
    /// (the unsuspend step of the turn). The AS-IS phase enum folds active+unsuspend into a single
    /// <c>phase.Active</c>; the natural unsuspend runs at the substrate (Active, Unsuspending) step
    /// (<see cref="Headless.Runtime.HeadlessTurnState.IsUnsuspendPhase"/>, former HeadlessPhase.Unsuspend) — so the
    /// "unsuspend phase" gate reads that step-cursor. Distinguishes a natural unsuspend (a [Your Turn]
    /// OnUnTappedAnyone effect fires, BT8_057) from an effect-driven mid-turn unsuspend during the Main phase
    /// (it does not).</summary>
    public static bool IsUnsuspendPhase(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.Context.TurnController.Current.IsUnsuspendPhase;
    }

    /// <summary>AS-IS <c>Player.DigivolveCount_ThisTurn</c>: how many times this card's owner has digivolved
    /// this turn (0 at turn start, ++ on each digivolve — DigivolveAction). Gate "if you've digivolved this
    /// turn" with <c>&gt;= 1</c> (BT1_007) or the exact count the card requires.</summary>
    public static int DigivolveCountThisTurn(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.Context.PlayerTurnCounters.Get(
            card.Owner, Headless.Runtime.PlayerTurnCounterController.DigivolveCountKey);
    }

    /// <summary>(PRIM-P0 B.O.4 #1) True when the action currently paying cost matches <paramref name="root"/>.
    /// Gate a [BeforePayCost] effect with this so it fires only for the intended action (AS-IS ChangeCostClass
    /// rootCondition), since the BeforePayCost timing is shared by play / digivolve / option.</summary>
    public static bool IsPayCostRoot(CardSource card, Headless.Bridge.PayCostRoot root)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.Context.CurrentPayCostRoot == root;
    }

    /// <summary>It is the opponent's turn.</summary>
    public static bool IsOpponentTurn(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return TurnOwnershipHelpers.IsOpponentTurn(card.Context.TurnController.Current.TurnPlayerId, card.Owner);
    }

    /// <summary>The card is part of a battle-area permanent (as the top card or a buried source).</summary>
    public static bool IsExistOnBattleArea(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return !card.PermanentOfThisCard().IsEmpty;
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>IsExistOnHand</c> (<c>card.Owner.HandCards
    /// .Contains(card)</c>): this card is in its owner's hand.</summary>
    public static bool IsExistOnHand(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand).Contains(card.InstanceId);
    }

    // ===== (W6-P) predicate commons batch — 1:1 name mirrors of GameContextDeterminarion.cs et al. =====
    // Verbatim AS-IS bodies verified 2026-07-02 (primitive_w6_design.md W6-P). These let a ported card's
    // condition closures be copied literally instead of intent-translated.

    /// <summary>AS-IS <c>IsExistOnField</c> (GameContextDeterminarion.cs:117): the card is part of ANY field
    /// permanent (battle or breeding area, top or buried).</summary>
    public static bool IsExistOnField(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return IsExistOnBattleArea(card) || IsExistOnBreedingArea(card);
    }

    /// <summary>AS-IS <c>IsExistOnBreedingArea</c> (:134).</summary>
    public static bool IsExistOnBreedingArea(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(card.Owner, ChoiceZone.BreedingArea))
        {
            if (top == card.InstanceId)
            {
                return true;
            }

            DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, top);
            if (stack.UnderCards.Any(under => under.InstanceId == card.InstanceId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>IsExistOnBattleAreaDigimon</c> (:188): on the battle area AND the permanent is a
    /// Digimon.</summary>
    public static bool IsExistOnBattleAreaDigimon(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return IsExistOnBattleArea(card) && new Permanent(card.Context, (card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default), card.Owner).IsDigimon;
    }

    /// <summary>AS-IS <c>IsExistOnTrash</c> (:243).</summary>
    public static bool IsExistOnTrash(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Trash).Contains(card.InstanceId);
    }

    /// <summary>AS-IS <c>IsExistOnExecutingArea</c> (:277): the card is being resolved as an Option.</summary>
    public static bool IsExistOnExecutingArea(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Execution).Contains(card.InstanceId);
    }

    /// <summary>AS-IS <c>IsExistInSecurity</c> (:291): in the owner's security with the given face state
    /// (<c>card.IsFlipped == isFlipped</c>; headless face state = the <c>isFlipped</c> instance flag,
    /// default face-down).</summary>
    public static bool IsExistInSecurity(CardSource card, bool isFlipped = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Security).Contains(card.InstanceId))
        {
            return false;
        }

        bool flipped = card.Context.CardInstanceRepository.TryGetInstance(card.InstanceId, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue("isFlipped", out object? raw) && raw is true;
        return flipped == isFlipped;
    }

    /// <summary>AS-IS <c>CanPlayAsNewPermanent</c> (:303): the card could be played as a NEW permanent —
    /// (Option cards only with <paramref name="isPlayOption"/>) + cost affordable when
    /// <paramref name="payCost"/>. Headless notes (documented reductions): the empty-frame check has no
    /// port model (no field-size limit is modeled anywhere); the DigiXros/Assembly in-flight-selection
    /// locks don't apply (material choices are action parameters, not persistent state); and the
    /// <paramref name="cardEffect"/> argument is DISCARDED — AS-IS threads it into
    /// <c>CanPlayCardTargetFrame</c>, whose CanEnterField gate ("cannot be played by effects" restrictions)
    /// is therefore not evaluated here (callers pass <c>null</c>, e.g. BT9_081's [On Deletion] candidate
    /// filter; wire the gate when its first restriction producer is ported).</summary>
    public static bool CanPlayAsNewPermanent(CardSource cardSource, bool payCost, ICardEffect? cardEffect, bool isPlayOption = false, int fixedCost = -1)
    {
        _ = cardEffect;
        if (cardSource is null || (!isPlayOption && cardSource.IsOption))
        {
            return false;
        }

        if (!payCost)
        {
            return true;
        }

        int baseCost = cardSource.Context.CardInstanceRepository.TryGetInstance(cardSource.InstanceId, out CardInstanceRecord? inst) && inst is not null
            && cardSource.Context.CardRepository.TryGetCard(inst.DefinitionId, out CardRecord? def) && def is not null
            ? def.PlayCost ?? 0
            : 0;
        // (R2-C) single AS-IS orchestrator. Root.None — a can-pay availability gate carries no source-zone
        // context (root-dependent cost effects are threaded at the actual play choke).
        int cost = fixedCost >= 0 ? fixedCost : cardSource.GetPayingCostWithBaseCost(baseCost, SelectCardEffect.Root.None, targetPermanents: null);
        return cardSource.Context.MemoryController.CanPay(Math.Max(0, cost));
    }

    /// <summary>AS-IS <c>IsPermanentExistsOnBattleArea</c> (:348).</summary>
    public static bool IsPermanentExistsOnBattleArea(Permanent? permanent)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty)
        {
            return false;
        }

        // (D-2) a leave-gate subject view answers from its PRE-removal snapshot (see Permanent.SnapshotZone).
        if (permanent.SnapshotZone is ChoiceZone snapshot)
        {
            return snapshot == ChoiceZone.BattleArea;
        }

        return ((IZoneStateReader)permanent.TopCard.Context.ZoneMover)
            .GetCards(permanent.OwnerId, ChoiceZone.BattleArea).Contains(permanent.InstanceId);
    }

    /// <summary>AS-IS <c>IsOwnerPermanent</c> (:388).</summary>
    public static bool IsOwnerPermanent(Permanent? permanent, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return permanent is not null && !permanent.InstanceId.IsEmpty && permanent.OwnerId == card.Owner;
    }

    /// <summary>AS-IS <c>IsOpponentPermanent</c> (:411).</summary>
    public static bool IsOpponentPermanent(Permanent? permanent, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return permanent is not null && !permanent.InstanceId.IsEmpty && !permanent.OwnerId.IsEmpty && permanent.OwnerId != card.Owner;
    }

    /// <summary>AS-IS <c>IsPermanentExistsOnOwnerBattleArea</c> (:431).</summary>
    public static bool IsPermanentExistsOnOwnerBattleArea(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnBattleArea(permanent) && IsOwnerPermanent(permanent, card);

    /// <summary>(BT1_109) Register a PLAYER-SCOPE digivolution-cost delta whose applicability is gated on a
    /// TWO-sided predicate: <paramref name="toCardCondition"/> (the digivolving-TO card) AND
    /// <paramref name="targetPermanentCondition"/> (the digivolving-FROM target permanent). The digivolution-cost
    /// pipeline (ContinuousModifierGate.ResolveDigivolutionCost -> ContinuousScopeEvaluation) passes the target
    /// permanent so both sides evaluate; every other continuous query supplies no target, so this effect stays
    /// scoped to digivolve-cost resolution. Mirrors AS-IS ChangeCostClass(ChangeCost, CardSourceCondition,
    /// PermanentsCondition) registered via AddEffectToPlayer(UntilEachTurnEnd).
    ///
    /// FIDELITY DEBT (documented, not silent): the AS-IS pairs this with a background cleanup ActivateClass that
    /// removes the reduction after the FIRST matching digivolve ("the NEXT time... this turn"). Headless has no
    /// player-scope WhenDigivolving trigger delivery to a trashed Option card (WhenDigivolving is delivered only
    /// subject-scoped to the digivolved card), so the reduction here lasts <paramref name="duration"/> (turn end)
    /// and would apply to EVERY matching green-5→6 digivolve that turn rather than only the next one. Scope is
    /// exact (correct lines only); the sole deviation is one-shot vs. once-per-matching-line-this-turn.</summary>
    public static void RegisterDigivolutionCostDeltaForPlayer(
        CardSource card, int delta, Headless.Effects.EffectDuration duration,
        Func<CardSource, bool> toCardCondition, Func<CardSource, bool> targetPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(toCardCondition);
        ArgumentNullException.ThrowIfNull(targetPermanentCondition);
        // (R3-W3b) AS-IS 1:1: build the ChangeCostClass (digivolution-cost delta gated on the two-sided predicate)
        // and store it in the OWNER player's None duration bucket via AddEffectToPlayer. The digivolution-cost gate
        // reads it back through ContinuousModifierGate.ResolveDigivolutionCost, which UNIONs
        // NewModelContinuousScan.FoldPlayCost (ContinuousModifierGate.cs:87 — the player.EffectList(None) IChangeCostEffect
        // scan, threading the digivolving-TO card into CardCondition and the FROM permanent into PermanentsCondition),
        // replacing the invented ContinuousModifierGate.Scope registry delta. The two-sided predicate maps directly:
        // toCardCondition → cardCondition (the digivolving-TO card), targetPermanentCondition → permanentCondition via
        // its TopCard (the digivolving-FROM permanent). FIDELITY DEBT (documented above, unchanged by this relocation).
        var changeCostClass = CardEffectFactory.ChangeDigivolutionCostStaticEffect<int>(
            changeValue: delta,
            permanentCondition: permanent => targetPermanentCondition(permanent.TopCard),
            cardCondition: toCardCondition,
            rootCondition: null,
            isInheritedEffect: false,
            card: card,
            condition: null,
            setFixedCost: false);

        if (changeCostClass != null)
        {
            AddEffectToPlayer(effectDuration: duration, card: card, cardEffect: changeCostClass, timing: EffectTiming.None);
        }
    }

    /// <summary>AS-IS <c>IsPermanentExistsOnOpponentBattleArea</c> (:448).</summary>
    public static bool IsPermanentExistsOnOpponentBattleArea(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnBattleArea(permanent) && IsOpponentPermanent(permanent, card);

    /// <summary>AS-IS <c>IsPermanentExistsOnBattleAreaDigimon</c> (:499).</summary>
    public static bool IsPermanentExistsOnBattleAreaDigimon(Permanent? permanent) =>
        IsPermanentExistsOnBattleArea(permanent) && permanent!.IsDigimon;

    /// <summary>AS-IS <c>IsPermanentExistsOnOwnerBattleAreaDigimon</c> (:516).</summary>
    public static bool IsPermanentExistsOnOwnerBattleAreaDigimon(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnOwnerBattleArea(permanent, card) && permanent!.IsDigimon;

    /// <summary>AS-IS <c>IsPermanentExistsOnOpponentBattleAreaDigimon</c> (:533).</summary>
    public static bool IsPermanentExistsOnOpponentBattleAreaDigimon(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnOpponentBattleArea(permanent, card) && permanent!.IsDigimon;

    /// <summary>AS-IS <c>IsPermanentExistsOnBattleAreaTamer</c> (GameContextDeterminarion.cs:550 — the
    /// Tamer sibling of the verified Digimon trio).</summary>
    public static bool IsPermanentExistsOnBattleAreaTamer(Permanent? permanent) =>
        IsPermanentExistsOnBattleArea(permanent) && permanent!.TopCard.IsTamer;

    /// <summary>AS-IS <c>IsPermanentExistsOnOwnerBattleAreaTamer</c> (:567).</summary>
    public static bool IsPermanentExistsOnOwnerBattleAreaTamer(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnOwnerBattleArea(permanent, card) && permanent!.TopCard.IsTamer;

    /// <summary>AS-IS <c>IsPermanentExistsOnOpponentBattleAreaTamer</c> (:584).</summary>
    public static bool IsPermanentExistsOnOpponentBattleAreaTamer(Permanent? permanent, CardSource card) =>
        IsPermanentExistsOnOpponentBattleArea(permanent, card) && permanent!.TopCard.IsTamer;

    /// <summary>AS-IS <c>IsPermanentExistsOnOwnerBreedingArea</c> (the breeding sibling of the verified
    /// battle-area form).</summary>
    public static bool IsPermanentExistsOnOwnerBreedingArea(Permanent? permanent, CardSource card)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || !IsOwnerPermanent(permanent, card))
        {
            return false;
        }

        return ((IZoneStateReader)card.Context.ZoneMover)
            .GetCards(permanent.OwnerId, ChoiceZone.BreedingArea).Contains(permanent.InstanceId);
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersSecurity</c>: any of the owner's security cards passes.</summary>
    public static bool HasMatchConditionOwnersSecurity(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Security)
            .Any(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>IsExistOnBreedingAreaDigimon</c> (GameContextDeterminarion.cs:151).</summary>
    public static bool IsExistOnBreedingAreaDigimon(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!IsExistOnBreedingArea(card))
        {
            return false;
        }

        foreach (HeadlessEntityId top in ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.BreedingArea))
        {
            DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, top);
            if ((top == card.InstanceId || stack.UnderCards.Any(u => u.InstanceId == card.InstanceId)) &&
                new Permanent(card.Context, top, card.Owner).IsDigimon)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>IsExistDigivolutionCards</c> (:219): this card rides a field permanent as a
    /// digivolution source (not the top).</summary>
    public static bool IsExistDigivolutionCards(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        PermanentView host = card.PermanentOfThisCard();
        return !host.IsEmpty && host.DigivolutionCards.Any(u => u.InstanceId == card.InstanceId);
    }

    /// <summary>AS-IS <c>IsExistLinked</c> (:231): this card is one of a field permanent's LINK cards.</summary>
    public static bool IsExistLinked(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessEntityId top in zones.GetCards(card.Owner, ChoiceZone.BattleArea))
        {
            if (card.Context.CardInstanceRepository.TryGetInstance(top, out CardInstanceRecord? host) && host is not null &&
                Headless.Runtime.LinkHelpers.ReadLinkedCardIds(host.Metadata).Contains(card.InstanceId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>IsExistInAnyTrash</c> (:257).</summary>
    public static bool IsExistInAnyTrash(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            if (!player.IsEmpty && zones.GetCards(player, ChoiceZone.Trash).Contains(card.InstanceId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>IsPermanentExistsOnField</c> (:323): breeding OR battle area.</summary>
    public static bool IsPermanentExistsOnField(Permanent? permanent) =>
        IsPermanentExistsOnBattleArea(permanent) || IsPermanentExistsOnBreedingArea(permanent);

    /// <summary>AS-IS <c>IsPermanentExistsOnBreedingArea</c> (:368) — the unary form.</summary>
    public static bool IsPermanentExistsOnBreedingArea(Permanent? permanent)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty)
        {
            return false;
        }

        // (D-2) a leave-gate subject view answers from its PRE-removal snapshot (see Permanent.SnapshotZone).
        if (permanent.SnapshotZone is ChoiceZone snapshot)
        {
            return snapshot == ChoiceZone.BreedingArea;
        }

        return ((IZoneStateReader)permanent.TopCard.Context.ZoneMover)
            .GetCards(permanent.OwnerId, ChoiceZone.BreedingArea).Contains(permanent.InstanceId);
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersBreedingPermanent</c> (:693).</summary>
    public static bool HasMatchConditionOwnersBreedingPermanent(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.BreedingArea)
            .Select(id => new Permanent(card.Context, id, card.Owner))
            .Any(CanSelectPermanentCondition);
    }

    /// <summary>AS-IS <c>HasMatchConditionPermanentDigivolutionCards</c> (:705): any of THIS card's
    /// permanent's digivolution sources passes.</summary>
    public static bool HasMatchConditionPermanentDigivolutionCards(CardSource card, Func<CardSource, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return card.PermanentOfThisCard().DigivolutionCards
            .Any(u => CanSelectPermanentCondition(new CardSource(card.Context, u.InstanceId, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>MatchConditionOpponentsCardCountInTrash</c> (:747).</summary>
    public static int MatchConditionOpponentsCardCountInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        HeadlessPlayerId opponent = OpponentOf(card);
        return opponent.IsEmpty
            ? 0
            : ((IZoneStateReader)card.Context.ZoneMover).GetCards(opponent, ChoiceZone.Trash)
                .Count(id => CanSelectCardCondition(new CardSource(card.Context, id, opponent, opponent)));
    }

    /// <summary>AS-IS <c>HasMatchConditionOpponentsCardInTrash</c> (:765).</summary>
    public static bool HasMatchConditionOpponentsCardInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition) =>
        MatchConditionOpponentsCardCountInTrash(card, CanSelectCardCondition) >= 1;

    /// <summary>AS-IS <c>GetUniqueColourCountOnOwnerBattleArea</c> (:828).</summary>
    public static int GetUniqueColourCountOnOwnerBattleArea(CardSource card, Func<Permanent, bool> canGetCardColour) =>
        UniqueColourCount(card, card.Owner, canGetCardColour);

    /// <summary>AS-IS <c>GetUniqueColourCountOnOpponentsBattleArea</c> (:843).</summary>
    public static int GetUniqueColourCountOnOpponentsBattleArea(CardSource card, Func<Permanent, bool> canGetCardColour) =>
        UniqueColourCount(card, OpponentOf(card), canGetCardColour);

    private static int UniqueColourCount(CardSource card, HeadlessPlayerId player, Func<Permanent, bool> canGetCardColour)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canGetCardColour);
        if (player.IsEmpty)
        {
            return 0;
        }

        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(player, ChoiceZone.BattleArea)
            .Select(id => new Permanent(card.Context, id, player))
            .Where(canGetCardColour)
            .SelectMany(p => p.TopCard.CardColors)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    /// <summary>AS-IS <c>IsMinCost</c> (MinMax_DP_Cost_Level/Cost/IsMinCost.cs, verbatim verified): among
    /// the owner's battle-area Digimon (or Digimon+Tamer), this permanent's PRINTED play cost is minimal.</summary>
    public static bool IsMinCost(Permanent? permanent, HeadlessPlayerId owner, bool IsDigimonOnly, Func<Permanent, bool>? condition = null) =>
        IsCostExtremum(permanent, owner, IsDigimonOnly, condition, min: true);

    /// <summary>AS-IS <c>IsMaxCost</c> (…/IsMaxCost.cs).</summary>
    public static bool IsMaxCost(Permanent? permanent, HeadlessPlayerId owner, bool IsDigimonOnly) =>
        IsCostExtremum(permanent, owner, IsDigimonOnly, condition: null, min: false);

    private static bool IsCostExtremum(Permanent? permanent, HeadlessPlayerId owner, bool digimonOnly, Func<Permanent, bool>? condition, bool min)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnBattleArea(permanent) ||
            (!permanent.IsDigimon && !IsTamerPermanent(permanent)) ||
            (condition is not null && !condition(permanent)) ||
            !permanent.TopCard.HasPlayCost ||
            (digimonOnly && !permanent.IsDigimon))
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> costs = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => digimonOnly ? p.IsDigimon : (p.IsDigimon || IsTamerPermanent(p)))
            .Where(p => p.TopCard.HasPlayCost)
            .Select(p => p.TopCard.GetCostItself)
            .ToList();
        return costs.Count >= 1 && permanent.TopCard.GetCostItself == (min ? costs.Min() : costs.Max());
    }

    /// <summary>AS-IS <c>GetNonMaxCostPermanents</c> (…/IsMaxCost.cs:36): the owner's permanents whose
    /// printed cost is BELOW the current maximum (cost-undefined ones included, per the original).</summary>
    public static List<Permanent> GetNonMaxCostPermanents(CardSource card, HeadlessPlayerId owner, bool digimonOnly = true)
    {
        ArgumentNullException.ThrowIfNull(card);
        EngineContext context = card.Context;
        List<Permanent> candidates = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => digimonOnly ? p.IsDigimon : (p.IsDigimon || IsTamerPermanent(p)))
            .ToList();
        if (candidates.Count == 0)
        {
            return new List<Permanent>();
        }

        int maxCost = candidates.Max(p => p.TopCard.HasPlayCost ? p.TopCard.GetCostItself : -1);
        return candidates.Where(p => !p.TopCard.HasPlayCost || p.TopCard.GetCostItself < maxCost).ToList();
    }

    /// <summary>AS-IS <c>IsMinDigivolutionCards</c> (…/DigivolutionCards/IsMinDigivolutionCards.cs).</summary>
    public static bool IsMinDigivolutionCards(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? condition = null)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, permanent.TopCard) ||
            (condition is not null && !condition(permanent)))
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> counts = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => p.IsDigimon && (condition is null || condition(p)))
            .Select(p => p.TopCard.PermanentOfThisCard().DigivolutionCards.Count)
            .ToList();
        return counts.Count >= 1 &&
            permanent.TopCard.PermanentOfThisCard().DigivolutionCards.Count == counts.Min();
    }

    /// <summary>AS-IS <c>IsMinLevelBoard</c> (…/Level/IsMinLevel.cs:24): min level over BOTH players'
    /// battle-area Digimon.</summary>
    public static bool IsMinLevelBoard(Permanent? permanent)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty ||
            !IsPermanentExistsOnBattleAreaDigimon(permanent) ||
            !permanent.TopCard.HasLevel)
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        var levels = new List<int>();
        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            if (player.IsEmpty)
            {
                continue;
            }

            levels.AddRange(zones.GetCards(player, ChoiceZone.BattleArea)
                .Select(id => new Permanent(context, id, player))
                .Where(p => p.IsDigimon && p.TopCard.HasLevel)
                .Select(p => p.Level));
        }

        return levels.Count >= 1 && permanent.Level == levels.Min();
    }

    /// <summary>AS-IS <c>IsBlock</c> (GetFromHashtable.cs:88): the driving event carried the block flag.</summary>
    public static bool IsBlock(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isBlock", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsFromSameDigimon</c> (:124).</summary>
    public static bool IsFromSameDigimon(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isFromSameDigimon", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsFromDigimon</c> (:142).</summary>
    public static bool IsFromDigimon(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isFromDigimon", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsFromDigimonDigivolutionCards</c> (:160).</summary>
    public static bool IsFromDigimonDigivolutionCards(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isFromDigimonDigivolutionCards", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsLeavingForDigiXros</c> (:800).</summary>
    public static bool IsLeavingForDigiXros(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}digixros", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsDijiXros</c> (:817): this card's permanent entered via DigiXros and the material
    /// count passes.</summary>
    public static bool IsDijiXros(Headless.Effects.CardEffectResolveContext ctx, CardSource card, Func<int, bool>? digixrosCountCondition)
    {
        if (!SubjectPermanentContains(ctx, card))
        {
            return false;
        }

        int count = ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}DigiXrosCount", out object? raw) && raw is int c
            ? c
            : 0;
        return digixrosCountCondition is null || digixrosCountCondition(count);
    }

    /// <summary>AS-IS <c>IsAlliance</c> (:765): the driving effect is the Alliance keyword's own window.</summary>
    public static bool IsAlliance(Headless.Effects.CardEffectResolveContext ctx) =>
        ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}isAlliance", out object? raw) && raw is true;

    /// <summary>AS-IS <c>IsDigivolvedFromSameLevelFromEnterFieldHashtable</c> (:720): the permanent
    /// digivolved without changing level (the event carries the pre-digivolve level).</summary>
    public static bool IsDigivolvedFromSameLevelFromEnterFieldHashtable(Headless.Effects.CardEffectResolveContext ctx, Permanent? permanent)
    {
        if (permanent is null || !IsPermanentExistsOnBattleArea(permanent) || !permanent.TopCard.HasLevel)
        {
            return false;
        }

        return ctx.EffectContext.Values.TryGetValue($"{GameFlowProcessor.EventValuePrefix}oldLevel", out object? raw)
            && raw is int oldLevel && oldLevel == permanent.Level;
    }

    /// <summary>AS-IS <c>IsDigivolvedByTheEffect</c> (IsDigivolvedByTheEffect.cs:9): the permanent's top is
    /// this card and the digivolution was caused by the given effect source (the digivolve stamps the
    /// causing source id).</summary>
    public static bool IsDigivolvedByTheEffect(Permanent? permanent, CardSource cardSource, CardSource effectSourceCard)
    {
        ArgumentNullException.ThrowIfNull(cardSource);
        ArgumentNullException.ThrowIfNull(effectSourceCard);
        if (permanent is null || !IsPermanentExistsOnBattleArea(permanent) ||
            permanent.InstanceId != cardSource.InstanceId)
        {
            return false;
        }

        return cardSource.Context.CardInstanceRepository.TryGetInstance(cardSource.InstanceId, out CardInstanceRecord? rec) && rec is not null
            && rec.Metadata.TryGetValue("digivolvedByEffectSourceId", out object? raw)
            && raw?.ToString() == effectSourceCard.InstanceId.Value;
    }

    private static bool IsTamerPermanent(Permanent permanent) => permanent.TopCard.IsTamer;

    /// <summary>AS-IS <c>HasMatchConditionPermanent(Func&lt;Permanent,bool&gt;, isContainBreedingArea)</c> (:641)
    /// — the VIEW-predicate overload (both players' battle-area, optionally + breeding).</summary>
    public static bool HasMatchConditionPermanent(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition, bool isContainBreedingArea = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return EnumerateFieldPermanentViews(card, isContainBreedingArea).Any(CanSelectPermanentCondition);
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersPermanent</c> (:681).</summary>
    public static bool HasMatchConditionOwnersPermanent(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return EnumerateFieldPermanentViews(card, isContainBreedingArea: false)
            .Any(p => IsOwnerPermanent(p, card) && CanSelectPermanentCondition(p));
    }

    /// <summary>AS-IS <c>MatchConditionOwnersPermanentCount</c> (:623).</summary>
    public static int MatchConditionOwnersPermanentCount(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return EnumerateFieldPermanentViews(card, isContainBreedingArea: false)
            .Count(p => IsOwnerPermanent(p, card) && CanSelectPermanentCondition(p));
    }

    /// <summary>AS-IS <c>MatchConditionOpponentsPermanentCount</c> (:632).</summary>
    public static int MatchConditionOpponentsPermanentCount(CardSource card, Func<Permanent, bool> CanSelectPermanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectPermanentCondition);
        return EnumerateFieldPermanentViews(card, isContainBreedingArea: false)
            .Count(p => IsOpponentPermanent(p, card) && CanSelectPermanentCondition(p));
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersHand</c> (:663).</summary>
    public static bool HasMatchConditionOwnersHand(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand)
            .Any(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>MatchConditionOwnersCardCountInHand</c> (:672).</summary>
    public static int MatchConditionOwnersCardCountInHand(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Hand)
            .Count(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>HasMatchConditionOwnersCardInTrash</c> (:756).</summary>
    public static bool HasMatchConditionOwnersCardInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Trash)
            .Any(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>MatchConditionOwnersCardCountInTrash</c> (:738).</summary>
    public static int MatchConditionOwnersCardCountInTrash(CardSource card, Func<CardSource, bool> CanSelectCardCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(CanSelectCardCondition);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Trash)
            .Count(id => CanSelectCardCondition(new CardSource(card.Context, id, card.Owner, card.Owner)));
    }

    /// <summary>AS-IS <c>HasNoElement</c> (:774).</summary>
    public static bool HasNoElement<T>(List<T> list) => list is null || list.Count <= 0;

    /// <summary>AS-IS <c>IsOwnerEffect</c> (:788) — headless the effect SOURCE is a CardSource (the port has
    /// no live ICardEffect.EffectSourceCard); translate <c>IsOwnerEffect(cardEffect, card)</c> as
    /// <c>IsOwnerEffect(cardEffect's source card, card)</c>.</summary>
    public static bool IsOwnerEffect(CardSource? effectSourceCard, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return effectSourceCard is not null && effectSourceCard.Owner == card.Owner;
    }

    /// <summary>AS-IS <c>IsOpponentEffect</c> (:808) — see <see cref="IsOwnerEffect"/>.</summary>
    public static bool IsOpponentEffect(CardSource? effectSourceCard, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return effectSourceCard is not null && !effectSourceCard.Owner.IsEmpty && effectSourceCard.Owner != card.Owner;
    }

    /// <summary>AS-IS <c>CanActivateSuspendCostEffect</c> (CanUseEffects/CanSuspend.cs:10-39, verbatim
    /// verified): this card's permanent is on the battle area (or, with <paramref name="includeBreeding"/>,
    /// the breeding area), UNSUSPENDED, and not suspend-locked — i.e. it could pay a "suspend this
    /// permanent" cost right now.</summary>
    public static bool CanActivateSuspendCostEffect(CardSource card, bool includeBreeding = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (IsExistOnBattleArea(card))
        {
            HeadlessEntityId top = card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default;
            if (!top.IsEmpty && !IsSuspended(card, top) &&
                // (R1-d) !CanSuspend restriction — AS-IS Permanent.CanSuspend, now housed on the mirror getter.
                new Permanent(card.Context, top).CanSuspend)
            {
                return true;
            }
        }

        if (includeBreeding && IsExistOnBreedingArea(card))
        {
            var zones = (IZoneStateReader)card.Context.ZoneMover;
            foreach (HeadlessEntityId hostId in zones.GetCards(card.Owner, ChoiceZone.BreedingArea))
            {
                DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, hostId);
                bool contains = hostId == card.InstanceId || stack.UnderCards.Any(under => under.InstanceId == card.InstanceId);
                if (contains && !IsSuspended(card, hostId) &&
                    // (R1-d) !CanSuspend restriction — AS-IS Permanent.CanSuspend.
                    new Permanent(card.Context, hostId).CanSuspend)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>AS-IS <c>CanDeclareOptionDelayEffect</c> (CanUseEffects/OptionEffect.cs:27): the [Delay]
    /// gate — on the battle area AND not the turn this permanent entered play.</summary>
    public static bool CanDeclareOptionDelayEffect(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!IsExistOnBattleArea(card))
        {
            return false;
        }

        HeadlessEntityId top = (card.PermanentOfThisCard().Stack.TopCard?.InstanceId ?? default);
        return !(card.Context.CardInstanceRepository.TryGetInstance(top, out CardInstanceRecord? i) && i is not null
            && i.Metadata.TryGetValue("enteredThisTurn", out object? raw) && raw is true);
    }

    /// <summary>AS-IS <c>CanUnsuspend(Permanent)</c> (CanUseEffects/CanUnsuspend.cs:10): suspended AND not
    /// unsuspend-locked.</summary>
    public static bool CanUnsuspend(Permanent? permanent)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty)
        {
            return false;
        }

        CardSource top = permanent.TopCard;
        // (R1-d) AS-IS CanUnsuspend(Permanent) = permanent.IsSuspended && permanent.CanUnsuspend — the second
        // conjunct now reads the mirror Permanent.CanUnsuspend getter (was the unioned gate).
        return IsSuspended(top, permanent.InstanceId)
            && permanent.CanUnsuspend;
    }

    /// <summary>AS-IS <c>IsMinDP</c> (MinMax_DP_Cost_Level/DP/IsMinDP.cs): among the owner's battle-area
    /// Digimon with a defined DP (printed DP or BaseDP&gt;0), this permanent's effective DP is the minimum.</summary>
    public static bool IsMinDP(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? condition = null) =>
        IsDpExtremum(permanent, owner, condition, min: true);

    /// <summary>AS-IS <c>IsMaxDP</c> (…/IsMaxDP.cs).</summary>
    public static bool IsMaxDP(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? permanentCondition = null) =>
        IsDpExtremum(permanent, owner, permanentCondition, min: false);

    /// <summary>AS-IS <c>IsMinLevel</c> (…/Level/IsMinLevel.cs): among the owner's battle-area Digimon with
    /// a printed level, this permanent's level is the minimum.</summary>
    public static bool IsMinLevel(Permanent? permanent, HeadlessPlayerId owner) =>
        IsLevelExtremum(permanent, owner, min: true);

    /// <summary>AS-IS <c>IsMaxLevel</c> (…/Level/IsMaxLevel.cs).</summary>
    public static bool IsMaxLevel(Permanent? permanent, HeadlessPlayerId owner) =>
        IsLevelExtremum(permanent, owner, min: false);

    private static bool IsDpExtremum(Permanent? permanent, HeadlessPlayerId owner, Func<Permanent, bool>? condition, bool min)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnBattleAreaDigimon(permanent) ||
            (condition is not null && !condition(permanent)) ||
            (!permanent.TopCard.HasDP && permanent.BaseDP <= 0))
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> dps = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => p.IsDigimon && (condition is null || condition(p)) && (p.TopCard.HasDP || p.BaseDP > 0))
            .Select(p => p.DP)
            .ToList();
        return dps.Count >= 1 && permanent.DP == (min ? dps.Min() : dps.Max());
    }

    private static bool IsLevelExtremum(Permanent? permanent, HeadlessPlayerId owner, bool min)
    {
        if (permanent is null || permanent.InstanceId.IsEmpty || permanent.OwnerId != owner ||
            !IsPermanentExistsOnBattleAreaDigimon(permanent) ||
            !permanent.TopCard.HasLevel)
        {
            return false;
        }

        EngineContext context = permanent.TopCard.Context;
        List<int> levels = ((IZoneStateReader)context.ZoneMover).GetCards(owner, ChoiceZone.BattleArea)
            .Select(id => new Permanent(context, id, owner))
            .Where(p => p.IsDigimon && p.TopCard.HasLevel)
            .Select(p => p.Level)
            .ToList();
        return levels.Count >= 1 && permanent.Level == (min ? levels.Min() : levels.Max());
    }

    private static IEnumerable<Permanent> EnumerateFieldPermanentViews(CardSource card, bool isContainBreedingArea)
    {
        EngineContext context = card.Context;
        var zones = (IZoneStateReader)context.ZoneMover;
        foreach (HeadlessPlayerId player in context.TurnController.Current.PlayerOrder)
        {
            if (player.IsEmpty)
            {
                continue;
            }

            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                yield return new Permanent(context, id, player);
            }

            if (isContainBreedingArea)
            {
                foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BreedingArea))
                {
                    yield return new Permanent(context, id, player);
                }
            }
        }
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>IsSuspended</c>: <paramref name="id"/>'s permanent
    /// is currently suspended (tapped). Reads the live <c>isSuspended</c> instance-metadata flag the engine
    /// maintains on tap/unsuspend.</summary>
    public static bool IsSuspended(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        return !id.IsEmpty
            && card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null
            && instance.Metadata.TryGetValue("isSuspended", out object? raw) && raw is true;
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>MatchConditionPermanentCount(predicate,
    /// isContainBreedingArea)</c>: the number of battle-area (optionally + breeding) permanents, across BOTH
    /// players, that satisfy <paramref name="condition"/>. The original takes a <c>Func&lt;Permanent,bool&gt;</c>;
    /// the headless uses the established entity-id predicate idiom (see <see cref="IsOpponentBattleAreaDigimon"/>),
    /// so card-side predicates compose CardEffectCommons helpers (IsSuspended, …) on the id.</summary>
    public static int MatchConditionPermanentCount(CardSource card, Func<HeadlessEntityId, bool> condition, bool isContainBreedingArea = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(condition);
        int count = 0;
        foreach (HeadlessEntityId id in AllFieldPermanents(card, isContainBreedingArea))
        {
            if (condition(id))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>AS-IS <c>Player.MemoryForPlayer</c>: the shared memory gauge read from <paramref name="card"/>'s
    /// OWNER's perspective. The headless gauge (<c>MemoryController.Current.Current</c>) is single-signed and
    /// turn-player-relative (positive = the turn player, per AceOverflowGate), so it is negated when the owner is
    /// NOT the current turn player — giving the owner-relative value the AS-IS predicate compares (e.g. BT1_054
    /// <c>MemoryForPlayer &gt;= 3</c>).</summary>
    public static int MemoryForPlayer(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        int memory = card.Context.MemoryController.Current.Current;
        return card.Context.TurnController.Current.TurnPlayerId == card.Owner ? memory : -memory;
    }

    /// <summary>The field permanents matching <paramref name="condition"/> as a materialised id list — the
    /// enumeration mirror of <see cref="MatchConditionPermanentCount"/>. Used by the no-select "apply a mutation
    /// to EVERY matching permanent" bodies (the AS-IS <c>foreach (… GetBattleAreaDigimons().Filter(…))</c> loop),
    /// evaluated live at activation-resolve time.</summary>
    public static IReadOnlyList<HeadlessEntityId> MatchConditionPermanentIds(
        CardSource card, Func<HeadlessEntityId, bool> condition, bool isContainBreedingArea = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(condition);
        var ids = new List<HeadlessEntityId>();
        foreach (HeadlessEntityId id in AllFieldPermanents(card, isContainBreedingArea))
        {
            if (condition(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>Stage a delete (AS-IS <c>DestroyPermanentsClass(target).Destroy()</c>) on <paramref name="target"/>
    /// via the sink — the per-target action for select/derived-set destroy bodies (e.g. BT1_084 "delete every
    /// same-named opponent Digimon"). The sink's centralised immunity + deletion-prevention gates filter
    /// (source = <paramref name="card"/>), mirroring <c>DestroyPermanentsEffect</c>.</summary>
    public static void DestroyPermanent(MatchStateMutationSink sink, CardSource card, HeadlessEntityId target)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(card);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DeleteKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    }

    /// <summary>Stage a suspend (AS-IS <c>SuspendPermanentsClass(target).Tap()</c>) on <paramref name="target"/>
    /// via the sink — the per-target action for the no-select "suspend all matching" bodies. The sink's
    /// centralised immunity + cannot-suspend gates filter (source = <paramref name="card"/>).</summary>
    public static void SuspendPermanent(MatchStateMutationSink sink, CardSource card, HeadlessEntityId target)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(card);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.SuspendKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    }

    /// <summary>Stage a deck-bottom return (AS-IS <c>DeckBottomBounceClass(target).DeckBounce()</c>) on
    /// <paramref name="target"/> via the sink — the per-target action for no-select "return a pre-computed list
    /// to the bottom of the deck" bodies (e.g. AD1_025's shared OP/WD arm). The sink's ReturnToDeckBottomKind
    /// handler applies the AS-IS gates (CannotReturnToLibrary / CanNotBeRemoved) and opens the DeckBounce leave
    /// window (source = <paramref name="card"/>), mirroring <see cref="DestroyPermanent"/> for the delete case.</summary>
    public static void ReturnToDeckBottom(MatchStateMutationSink sink, CardSource card, HeadlessEntityId target)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(card);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.ReturnToDeckBottomKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = target.Value }));
    }

    /// <summary>Stage an unsuspend of <paramref name="card"/>'s own permanent (AS-IS
    /// <c>IUnsuspendPermanents(self).Unsuspend()</c>) via the sink — the reusable self follow-up for
    /// own-stack-return effects (BT1_084 br2). The sink's centralised gates filter.</summary>
    public static void UnsuspendSelf(MatchStateMutationSink sink, CardSource card)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(card);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.UnsuspendKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = card.InstanceId.Value }));
    }

    /// <summary>Stage a trash of ALL of <paramref name="host"/>'s digivolution cards (AS-IS
    /// <c>TrashDigivolutionCardsFromTopOrBottom(count: DigivolutionCards.Count)</c>) via the sink — the per-target
    /// action for the no-select "trash all digivolution cards under every matching Digimon" bodies. Passing
    /// <c>int.MaxValue</c> lets the sink clamp to the host's actual source count. The sink's centralised immunity
    /// gate filters (source = <paramref name="card"/>).</summary>
    public static void TrashAllDigivolutionCards(MatchStateMutationSink sink, CardSource card, HeadlessEntityId host, bool fromBottom) =>
        TrashDigivolutionCards(sink, card, host, count: int.MaxValue, fromBottom);

    /// <summary>Stage a trash of <paramref name="count"/> of <paramref name="host"/>'s digivolution cards from
    /// the top/bottom (AS-IS <c>TrashDigivolutionCardsFromTopOrBottom(trashCount, isFromTop)</c>) via the sink —
    /// the per-target action for suspend-cost / select bodies (e.g. BT1_086 "trash the bottom digivolution card
    /// of 1 opponent Digimon" -> count:1, fromBottom:true). The sink clamps to the host's actual source count.</summary>
    public static void TrashDigivolutionCards(MatchStateMutationSink sink, CardSource card, HeadlessEntityId host, int count, bool fromBottom)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(card);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.TrashDigivolutionCardsKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
                [MatchStateMutationSink.CountKey] = count,
                [MatchStateMutationSink.FromBottomKey] = fromBottom,
            }));
    }

    /// <summary>(C5-witness) Stage a De-Digivolve of <paramref name="count"/> on <paramref name="host"/> (AS-IS
    /// <c>new IDegeneration(permanent, count, activateClass).Degeneration()</c>) via the sink — the per-target
    /// follow-up for select bodies (e.g. EX8_051 ESS "&lt;De-Digivolve 1&gt; 1 of your opponent's Digimon").
    /// The sink's DeDigivolve consumer clamps to the host's stack and honours de-digivolve immunity — the
    /// canonical de-digivolve mutation (the invented ActivatedSelectAndDeDigivolveEffect was retired R6-Da'-5).</summary>
    public static void DeDigivolvePermanent(MatchStateMutationSink sink, CardSource card, HeadlessEntityId host, int count)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(card);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DeDigivolveKind,
            card.InstanceId,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [MatchStateMutationSink.TargetEntityIdKey] = host.Value,
                [MatchStateMutationSink.CountKey] = count,
            }));
    }

    /// <summary>(EX8_074 Stage 1) Mirror of the original <c>HasMatchConditionPermanent</c>: at least one
    /// matching permanent exists (count &gt;= 1).</summary>
    public static bool HasMatchConditionPermanent(CardSource card, Func<HeadlessEntityId, bool> condition, bool isContainBreedingArea = false) =>
        MatchConditionPermanentCount(card, condition, isContainBreedingArea) >= 1;

    /// <summary>Both players' battle-area cards (optionally + breeding-area), in turn order. Enumerates raw
    /// instance ids; the caller's predicate decides Digimon-ness / ownership / suspendability.</summary>
    private static IEnumerable<HeadlessEntityId> AllFieldPermanents(CardSource card, bool isContainBreedingArea)
    {
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                yield return id;
            }

            if (isContainBreedingArea)
            {
                foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BreedingArea))
                {
                    yield return id;
                }
            }
        }
    }

    // (W6-P) the earlier owner-only simplifications of IsPermanentExistsOn(Owner|Opponent)BattleAreaDigimon
    // were replaced by the faithful full-check mirrors above (battle-area + Digimon + ownership).

    /// <summary><paramref name="id"/> is an opponent's battle-area Digimon (entity-id predicate form used
    /// by SelectPermanentEffect target conditions).</summary>
    public static bool IsOpponentBattleAreaDigimon(CardSource card, HeadlessEntityId id) =>
        IsBattleAreaDigimon(card, id, opponent: true);

    /// <summary><paramref name="id"/> is one of the card owner's battle-area Digimon.</summary>
    public static bool IsOwnerBattleAreaDigimon(CardSource card, HeadlessEntityId id) =>
        IsBattleAreaDigimon(card, id, opponent: false);

    /// <summary>(AD1_025) Entity-id predicate form of AS-IS <c>IsPermanentExistsOnOpponentBattleArea(p) &amp;&amp;
    /// p.IsOption</c>: <paramref name="id"/> is an OPPONENT-owned battle-area Option permanent.</summary>
    public static bool IsOpponentBattleAreaOption(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return false;
        }

        if (instance.OwnerId == card.Owner || instance.OwnerId.IsEmpty)
        {
            return false;
        }

        return IsPermanentExistsOnBattleArea(new Permanent(card.Context, id, instance.OwnerId))
            && new CardSource(card.Context, id, instance.OwnerId, instance.OwnerId).IsOption;
    }

    /// <summary>(EX8-1) Mirror of the original <c>IsPermanentExistsOnBattleAreaDigimon(permanent)</c>:
    /// <paramref name="id"/> is a battle-area Digimon owned by EITHER player (used by "suspend 1 Digimon"
    /// targets and by the suspended-count threshold).</summary>
    public static bool IsBattleAreaDigimon(CardSource card, HeadlessEntityId id) =>
        IsOwnerBattleAreaDigimon(card, id) || IsOpponentBattleAreaDigimon(card, id);

    private static bool IsBattleAreaDigimon(CardSource card, HeadlessEntityId id, bool opponent)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return false;
        }

        bool isOpponentOwned = instance.OwnerId != card.Owner;
        if (isOpponentOwned != opponent)
        {
            return false;
        }

        var zones = (IZoneStateReader)card.Context.ZoneMover;
        if (!zones.GetCards(instance.OwnerId, ChoiceZone.BattleArea).Contains(id))
        {
            return false;
        }

        return card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            && def is not null
            && def.IsCardType("Digimon");
    }

    /// <summary>(R1-a) Resolved current DP of a battle-area card = AS-IS <c>Permanent.DP</c> (base printed DP
    /// folded LIVE with every field / face-up-security / player IChangeDPEffect, plus LinkedDP and Boosts,
    /// clamped at 0). Used by DP-threshold target predicates (e.g. ST1_15 "Digimon with 4000 DP or less").
    /// Returns -1 for a card with no DP (Permanent.DP's no-DP sentinel).</summary>
    public static int CurrentDp(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        HeadlessPlayerId owner = card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) && instance is not null
            ? instance.OwnerId
            : card.Owner;
        return new Permanent(card.Context, id, owner).DP;
    }

    private static int? ReadDp(IReadOnlyDictionary<string, object?> metadata)
    {
        foreach (string key in new[] { "dp", "DP" })
        {
            if (metadata.TryGetValue(key, out object? raw))
            {
                if (raw is int i) return i;
                if (raw is long l) return (int)l;
                if (raw is string s && int.TryParse(s, out int p)) return p;
            }
        }

        return null;
    }

    /// <summary>1:1 mirror of AS-IS <c>CardEffectCommons.AddActivateMainOptionSecurityEffect</c>
    /// (CardEffectCommons.cs:723): reuse the Option's [Main] skill from security. Guards on
    /// <see cref="OptionMainEffect(CardSource)"/> == null (AS-IS :725 — add NOTHING when the card has no [Main]
    /// <c>ActivateClass</c>), then adds the AS-IS <see cref="CardEffectFactory.ActivateMainOptionSecurityEffect"/>
    /// <c>ActivateClass</c> (AS-IS CardEffectFactory.cs:551): <c>SetIsSecurityEffect(true)</c> +
    /// <c>CanUseCondition = CanTriggerSecurityEffect(OptionMainCheckHashtable(card), card)</c> + a coroutine that
    /// runs the reused [Main] via <c>mainActivateClass.Activate(OptionMainCheckHashtable(card))</c> then
    /// <paramref name="afterMainEffect"/> in the SAME activation. Resolved by <see cref="ActivatedEffectResolver"/>'s
    /// <c>ActivateICardEffect</c> case at the SecuritySkill timing (driven live by
    /// <see cref="Headless.Runtime.SecurityResolver"/>). <paramref name="afterMainEffect"/> mirrors the AS-IS
    /// <c>afterMainEffect</c> callback (a follow-up run AFTER the reused [Main]; ST4_15 — "then, add this card to
    /// your hand" via <c>AddThisCardToHand</c>). Substrate: <c>Func&lt;ICardEffect, IEnumerator&gt;</c> →
    /// <c>Func&lt;ICardEffect, Task&gt;</c>. (이연③-g) factory-seat flip — the invented <c>ReuseMainOptionEffect</c>
    /// carrier is retired in favour of this AS-IS <c>ActivateClass</c> idiom.</summary>
    public static void AddActivateMainOptionSecurityEffect(
        CardSource card, ref List<ICardEffect> cardEffects, string effectName, Func<ICardEffect, Task>? afterMainEffect = null)
    {
        ArgumentNullException.ThrowIfNull(cardEffects);
        if (OptionMainEffect(card) is null)
        {
            return;
        }

        cardEffects.Add(CardEffectFactory.ActivateMainOptionSecurityEffect(card, effectName, afterMainEffect: afterMainEffect));
    }

    /// <summary>Mirror of the original <c>Permanent.HasNoDigivolutionCards</c> (entity-id form): the
    /// battle-area permanent topped by <paramref name="id"/> has no digivolution (under) cards.</summary>
    public static bool HasNoDigivolutionCards(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty)
        {
            return false;
        }

        DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, id);
        return stack.UnderCards.Count == 0;
    }

    /// <summary>Metadata flag marking a digivolution source as protected from being trashed (mirror of the
    /// original <c>CardSource.CanNotTrashFromDigivolutionCards</c>). Stamped on the source instance.</summary>
    public const string TrashProtectedKey = "cannotTrashFromDigivolution";

    /// <summary>Mirror of the original target gate
    /// <c>permanent.DigivolutionCards.Count(c =&gt; !c.CanNotTrashFromDigivolutionCards(...))</c>: the number of
    /// the host permanent's digivolution (under) cards that are NOT trash-protected.</summary>
    public static int TrashableDigivolutionCount(CardSource card, HeadlessEntityId hostId)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (hostId.IsEmpty)
        {
            return 0;
        }

        DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, hostId);
        int count = 0;
        foreach (StackedCard under in stack.UnderCards)
        {
            if (!IsTrashProtectedSource(card, under.InstanceId))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The host has at least one trashable (non-protected) digivolution card.</summary>
    public static bool HasTrashableDigivolutionCards(CardSource card, HeadlessEntityId hostId) =>
        TrashableDigivolutionCount(card, hostId) >= 1;

    /// <summary>(B-2 DigiBurst rework) The host's trashable (non-protected) digivolution source ids in stack
    /// order — the AS-IS Digi-Burst select pool (<c>SelectCardEffect</c> over <c>_permanent.DigivolutionCards</c>
    /// with <c>canTargetCondition = !CanNotTrashFromDigivolutionCards</c>, CardController.cs:2171-2189).</summary>
    public static IReadOnlyList<HeadlessEntityId> TrashableDigivolutionSourceIds(CardSource card, HeadlessEntityId hostId)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (hostId.IsEmpty)
        {
            return Array.Empty<HeadlessEntityId>();
        }

        DigivolutionStack stack = DigivolutionStackReader.Read(card.Context.CardInstanceRepository, card.Context.CardRepository, hostId);
        var ids = new List<HeadlessEntityId>();
        foreach (StackedCard under in stack.UnderCards)
        {
            if (!IsTrashProtectedSource(card, under.InstanceId))
            {
                ids.Add(under.InstanceId);
            }
        }

        return ids;
    }

    // (C-3 재상환 P1-B) AS-IS CanNotTrashFromDigivolutionCards(source, _cardEffect) = the legacy per-source stamp
    // (willBeRemoveSources) OR the field-effect SCAN (ICanNotTrashFromDigivolutionCardsEffect, e.g. BT9_109). The
    // eligibility/selection surfaces (CanDigiBurst's trashable count — CardController.cs:2145/2152 — and the
    // DigiBurst select pool) evaluate the FULL predicate, so the scan half must be consulted here too, not only
    // in the execution filter (DigivolutionStackHelpers). The causing effect is the surface's own activate class,
    // whose EffectSourceCard is <paramref name="card"/> — exactly the AS-IS `_cardEffect` argument collapsed.
    internal static bool IsTrashProtectedSource(CardSource card, HeadlessEntityId sourceId) =>
        IsTrashProtectedSource(card.Context, BareCauseEffect.For(card), sourceId);

    // (수리-2, ②군) Id-based overload of the causing effect. The DigivolutionStackHelpers effect-trash filter reaches
    // here with only the causing effect's SOURCE ID (a raw mutation cause), which — for an abstract/synthetic trash
    // (a test-harness cause with no live instance, or any rule-sourced trash) — has no resolvable owner. Routing the
    // cause through the id-based BareCauseEffect factory (RD-BCE-01) collapses such a cause to a source-LESS bare
    // cause instead of throwing on the CardSource ctor's non-empty-controller guard. A resolvable cause id rebuilds
    // the exact same CardSource-backed bare cause as the CardSource overload above, so real causes are unchanged.
    internal static bool IsTrashProtectedSource(EngineContext context, HeadlessEntityId causeSourceId, HeadlessEntityId sourceId) =>
        IsTrashProtectedSource(context, BareCauseEffect.For(context, causeSourceId), sourceId);

    private static bool IsTrashProtectedSource(EngineContext context, BareCauseEffect cause, HeadlessEntityId sourceId)
    {
        if (sourceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(sourceId, out CardInstanceRecord? instance)
            || instance is null)
        {
            return false;
        }

        // Headless mirror of the AS-IS in-flight willBeRemoveSources stamp (BT12_081-style play-out-of-stack;
        // test-stamped TrashProtectedKey). The R1-e getter below ALSO checks the "willBeRemoveSources" instance
        // flag, so both marks are honoured.
        if (instance.Metadata.TryGetValue(TrashProtectedKey, out object? raw) && raw is true)
        {
            return true;
        }

        // (R3-W3c-4) AS-IS-literal live scan: the source being trashed evaluated against the causing effect
        // (collapsed to its source card `cause`, the same reduction the retired registry TrashProtectionScan used)
        // via the R1-e getter CardSource.CanNotTrashFromDigivolutionCards — it scans every field/player/self
        // EffectList(None) for a usable ICanNotTrashFromDigivolutionCardsEffect (BT9_109). The old registry scan
        // never saw the kind-class (no ToBinding), so this rehousing is the load-bearing fidelity fix.
        var sourceBeingTrashed = new CardSource(context, sourceId, instance.OwnerId, instance.OwnerId);
        return sourceBeingTrashed.CanNotTrashFromDigivolutionCards(cause);
    }

    /// <summary>Mirror of the original <c>permanent.TopCard.HasLevel</c>: the host's top card carries a
    /// printed level (Digimon / DigiEgg do; Tamers / Options do not).</summary>
    public static bool TopCardHasLevel(CardSource card, HeadlessEntityId id) => LevelOf(card, id) > 0;

    /// <summary>Mirror of the original <c>Permanent.Level</c> (entity-id form): the printed level of the
    /// battle-area card topped by <paramref name="id"/> (0 when unknown), read from instance/def metadata.</summary>
    public static int LevelOf(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return 0;
        }

        return ReadLevel(instance.Metadata)
            ?? (card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def) && def is not null
                ? ReadLevel(def.Metadata) ?? 0
                : 0);
    }

    private static int? ReadLevel(IReadOnlyDictionary<string, object?> metadata)
    {
        foreach (string key in new[] { "level", "Level" })
        {
            if (metadata.TryGetValue(key, out object? raw))
            {
                if (raw is int i) return i;
                if (raw is long l) return (int)l;
                if (raw is string s && int.TryParse(s, out int p)) return p;
            }
        }

        return null;
    }

    /// <summary>Mirror of the original <c>HasMatchConditionOpponentsPermanent</c> (entity-id predicate form):
    /// the opponent has at least one battle-area Digimon matching <paramref name="condition"/>.</summary>
    public static bool HasMatchConditionOpponentsPermanent(CardSource card, Func<HeadlessEntityId, bool> condition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(condition);
        foreach (HeadlessEntityId id in OpponentBattleAreaDigimon(card))
        {
            if (condition(id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Mirror of the original <c>card.Owner.SecurityCards.Count</c>: the number of cards in the
    /// owner's security stack (used by security-count conditions, e.g. ST3_05 "4 or more", ST3_09 "3 or less").</summary>
    public static int SecurityCount(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Security).Count;
    }

    /// <summary>Mirror of the original <c>card.Owner.Enemy.SecurityCards.Count</c>: the number of cards in the
    /// OPPONENT's security stack (BT8_057 "[Your Turn] … trash the top card of your opponent's security"
    /// activate gate = opponent has >= 1 security).</summary>
    public static int OpponentSecurityCount(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return ((IZoneStateReader)card.Context.ZoneMover).GetCards(OpponentOf(card), ChoiceZone.Security).Count;
    }

    /// <summary>Mirror of the original <c>IsDPZeroDelete(hashtable)</c>: the just-deleted permanent (the
    /// trigger subject) was deleted by dropping to 0 DP — distinguished by the <c>DPZero</c> marker that
    /// <see cref="DpZeroDeletionHelpers"/> stamps (vs a battle or direct-Delete-effect deletion).</summary>
    public static bool IsDPZeroDelete(CardSource card, CardEffectResolveContext context)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Request.Context.TriggerEntityId is not { } deleted || deleted.IsEmpty)
        {
            return false;
        }

        return card.Context.CardInstanceRepository.TryGetInstance(deleted, out CardInstanceRecord? instance)
            && instance is not null
            && instance.Metadata.TryGetValue(DpZeroDeletionHelpers.DpZeroKey, out object? raw) && raw is true;
    }

    /// <summary>Mirror of the original <c>CanTriggerOnPermanentDeleted(hashtable, permanentCondition)</c>: a
    /// permanent was just deleted (the trigger subject) and it satisfies <paramref name="permanentCondition"/>.</summary>
    public static bool CanTriggerOnPermanentDeleted(CardSource card, CardEffectResolveContext context, Func<HeadlessEntityId, bool> permanentCondition)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(permanentCondition);
        return context.Request.Context.TriggerEntityId is { } deleted && !deleted.IsEmpty && permanentCondition(deleted);
    }

    /// <summary>The deleted-subject ownership/type predicate: <paramref name="id"/> is (was) an opponent's
    /// Digimon — zone-agnostic (the card may already be in the trash), so usable in deletion triggers.</summary>
    public static bool IsOpponentOwnedDigimon(CardSource card, HeadlessEntityId id)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (id.IsEmpty || !card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? instance) || instance is null)
        {
            return false;
        }

        if (instance.OwnerId == card.Owner)
        {
            return false;
        }

        return card.Context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            && def is not null
            && def.IsCardType("Digimon");
    }

    /// <summary>Mirror of the original <c>card.PermanentOfThisCard().battle.enemyPermanent(...)</c>: the
    /// entity this card's permanent is currently battling (the other participant of the in-progress attack),
    /// or empty when this permanent is not in a battle. Read from <c>AttackController.Current</c>.</summary>
    public static HeadlessEntityId CurrentBattleOpponent(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        HeadlessEntityId self = card.PermanentOfThisCard().TopInstanceId;
        if (self.IsEmpty)
        {
            return default;
        }

        HeadlessAttackState attack = card.Context.AttackController.Current;
        HeadlessEntityId attacker = attack.AttackerId ?? default;
        HeadlessEntityId defender = attack.BlockerId ?? attack.TargetId ?? default;
        if (self == attacker)
        {
            return defender;
        }

        if (self == defender)
        {
            return attacker;
        }

        return default;
    }

    /// <summary>The opponent player id (the first player in turn order that is not the card owner). Empty
    /// when there is no distinct opponent (e.g. uninitialized turn order).</summary>
    public static HeadlessPlayerId OpponentOf(CardSource card)
    {
        ArgumentNullException.ThrowIfNull(card);
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            if (player != card.Owner)
            {
                return player;
            }
        }

        return default;
    }

    /// <summary>The opponent's battle-area Digimon top cards (entity ids).</summary>
    private static IEnumerable<HeadlessEntityId> OpponentBattleAreaDigimon(CardSource card)
    {
        var zones = (IZoneStateReader)card.Context.ZoneMover;
        foreach (HeadlessPlayerId player in card.Context.TurnController.Current.PlayerOrder)
        {
            if (player == card.Owner)
            {
                continue;
            }

            foreach (HeadlessEntityId id in zones.GetCards(player, ChoiceZone.BattleArea))
            {
                if (IsOpponentBattleAreaDigimon(card, id))
                {
                    yield return id;
                }
            }
        }
    }
}

