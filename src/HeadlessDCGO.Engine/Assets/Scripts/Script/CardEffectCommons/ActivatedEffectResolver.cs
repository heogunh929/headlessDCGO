namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;

// (G6-002) Resolves a card's ACTIVATED effects (Option [Main] / Security skills: select-and-delete,
// select-and-buff, player-scope buff) at the ACTION layer, which has the live EngineContext — and thus
// the engine's IChoiceProvider. This is the seam the per-effect IHeadlessCardEffect.ResolveAsync lacks
// (no choice provider in its signature). The action that activates the card (e.g. OptionActivateAction)
// calls this instead of enqueueing onto the generic scheduler.
//
// Choice handling is delegated to context.ChoiceProvider:
//   - in tests / RL drivers it is a ScriptedChoiceProvider that answers immediately;
//   - in a live interactive match it is the DeferredChoiceProvider, whose ChooseAsync suspends via
//     DeferredChoicePendingException — driving that suspend/resume across the action boundary is the
//     remaining loop-integration step (see docs/audit/live_integration_goals.md G6-002).
public static class ActivatedEffectResolver
{
    /// <summary>Resolve all activated effects of <paramref name="cardInstanceId"/> for
    /// <paramref name="timing"/>. Returns the number of activated effects resolved (0 if the card has no
    /// ported activated effect — the caller can then fall back to its legacy path).</summary>
    /// <summary>(#13) A filter that keeps only the [Main]-tagged option effect — AS-IS <c>OptionMainEffect</c>
    /// selects <c>e is ActivateClass &amp;&amp; e.EffectDiscription.Contains("[Main]")</c>. In headless the "[Main]"
    /// activation is not a single base type (it can be ActivatedEffect, SelectAndDestroyEffect, PlayOptionCardEffect,
    /// …) — they all carry a <c>Description</c> that begins with "[Main]", which IS the AS-IS discriminator — so we
    /// match on the description. Re-running the [Main] side (security / option-main reuse) resolves ONLY these.</summary>
    internal static bool IsMainOptionEffect(ICardEffect effect)
    {
        if (effect is null)
        {
            return false;
        }

        string? description = effect.GetType().GetProperty("Description")?.GetValue(effect) as string;
        return description is not null && description.Contains("[Main]", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>(F1-M1-INHERITSCAN) The AS-IS <c>Permanent.EffectList_ForCard</c> membership split
    /// (Permanent.cs:1526-1541) applied to ONE scanned card's effect list: a SOURCE scan
    /// (<paramref name="inheritedScan"/> == true) keeps only INHERITED activated effects (AS-IS a NON-TOP source
    /// contributes only its <c>IsInheritedEffect</c> effects), a TOP scan (false — the default for every
    /// non-bridge caller: option / security / declaration / on-play / digivolve) keeps only NON-inherited effects
    /// (AS-IS the top card contributes only its non-inherited effects). Inherited-ness lives on the uniform
    /// <see cref="ActivatedEffect.IsInheritedEffect"/> flag; a non-uniform <see cref="IActivatedCardEffect"/>
    /// carries no inherited flag, so it counts as NON-inherited (a top/main effect) — matching the accepted
    /// uniform-migration gap (no non-uniform inherited activated effect is ported). Behaviour-neutral for the
    /// default (false) path: no effect ported before this flag is inherited, so the top scan keeps them all.</summary>
    private static bool MembershipKeeps(ICardEffect effect, bool inheritedScan) =>
        (effect is ActivatedEffect ae && ae.IsInheritedEffect) == inheritedScan;

    /// <summary>(Stage 5, 3b-iii) Whether the card has ANY activated effects registered at <paramref name="timing"/>.
    /// The window's unified-seed collect uses this so an activated-effect BRIDGE marker is only synthesised for a
    /// card that actually reacts at that timing — the batch bridge scanned every battle-area card and let the
    /// resolver no-op, but in the ONE window a no-op marker would spuriously compete with a real effect for the
    /// player's order choice. Pure (builds the effect list, runs nothing).</summary>
    public static bool HasEffectsAt(
        EngineContext context, HeadlessEntityId cardInstanceId, HeadlessPlayerId controller, EffectTiming timing)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardInstanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardInstanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return false;
        }

        var card = new CardSource(context, cardInstanceId, controller, instance.OwnerId);
        return effect.CardEffects(timing, card).Count > 0;
    }

    /// <summary>(A-2 / RD-6) Whether the card has any ACTIVATED effect (<see cref="IActivatedCardEffect"/>)
    /// registered at <paramref name="timing"/> — the resolver's actual domain (the <see cref="ResolveListAsync"/>
    /// switch is ENTIRELY over IActivatedCardEffect subtypes; a plain scheduler <see cref="IHeadlessCardEffect"/>
    /// mutation body hits no case and no-ops). Distinct from <see cref="HasEffectsAt"/> (ANY effect, existence):
    /// a card whose only effect at the timing is a SCHEDULER effect (e.g. TriggeredGainMemoryEffect / BT1_021
    /// EoTLose3Memory) is already collected by the scheduler half of the unified window seed, so synthesising an
    /// activated-bridge marker for it would DOUBLE-collect the same effect — the marker only no-ops in the resolver
    /// yet still competes for the window's order choice (spurious). The activated bridge must mark a card ONLY when
    /// it has a genuine activated effect the resolver will handle. Pure (builds the effect list, runs nothing).</summary>
    public static bool HasActivatedEffectsAt(
        EngineContext context, HeadlessEntityId cardInstanceId, HeadlessPlayerId controller, EffectTiming timing,
        bool inheritedScan = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardInstanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardInstanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return false;
        }

        var card = new CardSource(context, cardInstanceId, controller, instance.OwnerId);
        IReadOnlyList<ICardEffect> effects = effect.CardEffects(timing, card);
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is IActivatedCardEffect && MembershipKeeps(effects[i], inheritedScan))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>(RDx-A3, predicate SPLIT per the 2026-07-11 adversarial review) The window's PER-PASS re-check on
    /// an already-stacked activated marker. AS-IS re-checks ONLY <c>CanActivate</c> every pass
    /// (MultipleSkills.cs:122/164-165, pick :366, execution entry AutoProcessing.cs:1068) = the once-per-turn cap
    /// (ICardEffect.cs:366-372) + CanActivateCondition (:377) — it NEVER re-evaluates the collect gate
    /// (CanTrigger / CanUseCondition) on a stacked skill (the execution path ICardEffect.cs:1116-1286 has no
    /// CanTrigger call). So for a uniform <see cref="ActivatedEffect"/> this evaluates the CanActivate HALF
    /// (<see cref="ActivatedEffect.CanResolveActivateHalf"/>) plus the cap — NOT <c>CanResolve</c>: conflating the
    /// CanUse half here suppressed effects AS-IS resolves (an event-scoped CanUseCondition whose subject died
    /// mid-window — ST4_14 — stays resolvable in AS-IS because only CanActivateCondition is re-checked) and the
    /// missing cap offered a spent effect for the order choice AS-IS's per-pass CanActivate excludes. The collect
    /// gate lives in <see cref="CanCollectAt"/>, evaluated ONCE when the marker is synthesised. A non-uniform
    /// activated effect has no split gate (it self-no-ops in the resolver), so it counts as potentially-active
    /// (a known under-gate versus AS-IS per-pass CanActivateCondition — shrinking with the B-5 uniform
    /// migration). Pure (builds the effect list + gates, runs no body).</summary>
    public static bool CanActivateAt(
        EngineContext context, HeadlessEntityId cardInstanceId, HeadlessPlayerId controller, EffectTiming timing,
        GameEvent? drivingEvent = null, bool inheritedScan = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardInstanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardInstanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return false;
        }

        // AS-IS CanActivate also gates on effect INVALIDATION (IsDisabled, ICardEffect.cs:421) — a card whose
        // effects are currently nullified is per-pass excluded (2026-07-11 re-review: this was missing from the
        // marker path while the scheduler half checked it in SchedulerGate).
        if (EffectInvalidation.IsEffectsDisabled(context, cardInstanceId))
        {
            return false;
        }

        var card = new CardSource(context, cardInstanceId, controller, instance.OwnerId);
        IReadOnlyList<ICardEffect> effects = effect.CardEffects(timing, card);
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is not IActivatedCardEffect || !MembershipKeeps(effects[i], inheritedScan))
            {
                continue;
            }

            if (effects[i] is ActivatedEffect uniform)
            {
                if (uniform.CanResolveActivateHalf()
                    && context.OnceFlags.CanActivate(
                        BuildUniformResolveContext(uniform, drivingEvent).Request, uniform.MaxCountPerTurn))
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>(RDx-A3 split) The COLLECT-time gate for synthesising an activated-bridge marker — the AS-IS
    /// collect filter <c>CanTrigger</c> (ICardEffect.cs:319-358: the once-per-turn cap :339 + CanUseCondition
    /// :350), which AS-IS evaluates ONCE when GetSkillInfos/EffectList stacks the skill and never again. A
    /// uniform effect whose CanUse half (or cap) is false at collect is NOT stacked (AS-IS never collects it —
    /// even if the condition would become true later in the window); one whose CanUse half is true at collect
    /// STAYS stacked even if the condition later turns false (only the CanActivate half re-checks per pass —
    /// <see cref="CanActivateAt"/>). Non-uniform effects count by existence (their gates live in their own
    /// resolution). Pure.</summary>
    public static bool CanCollectAt(
        EngineContext context, HeadlessEntityId cardInstanceId, HeadlessPlayerId controller, EffectTiming timing,
        GameEvent? drivingEvent = null, bool inheritedScan = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardInstanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardInstanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return false;
        }

        var card = new CardSource(context, cardInstanceId, controller, instance.OwnerId);
        IReadOnlyList<ICardEffect> effects = effect.CardEffects(timing, card);
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is not IActivatedCardEffect || !MembershipKeeps(effects[i], inheritedScan))
            {
                continue;
            }

            if (effects[i] is ActivatedEffect uniform)
            {
                CardEffectResolveContext resolveCtx = BuildUniformResolveContext(uniform, drivingEvent);
                if (uniform.CanResolveUseHalf(resolveCtx)
                    && context.OnceFlags.CanActivate(resolveCtx.Request, uniform.MaxCountPerTurn))
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>(B-2 / P1-5) The legal-move gate for the declarative [Main] skill-declaration action
    /// (<see cref="Headless.Runtime.MainSkillActivateAction"/>) — AS-IS <c>Permanent.CanDeclareSkillList</c>
    /// (Permanent.cs:1618): each battle-area permanent's <c>EffectList(OnDeclaration)</c> filtered to
    /// <c>ActivateICardEffect</c> where <c>CanUse(null)</c> holds. AS-IS <c>CanUse = CanTrigger &amp;&amp; CanActivate</c>,
    /// and <c>CanActivate</c> INCLUDES the once-per-turn cap (<c>isOverMaxCountPerTurn</c>, ICardEffect.cs:363).
    /// So this mirrors <see cref="CanActivateAt"/> (CanResolve = scope + precondition) but ADDITIONALLY excludes a
    /// capped-out uniform effect via the <see cref="OnceFlagController.CanActivate"/> gate, keeping a spent [Main]
    /// skill out of the offered set for the rest of the turn — exactly as CanUse's cap check keeps it out of
    /// CanDeclareSkillList.</summary>
    public static bool CanDeclareAt(
        EngineContext context, HeadlessEntityId cardInstanceId, HeadlessPlayerId controller, EffectTiming timing,
        GameEvent? drivingEvent = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardInstanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardInstanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return false;
        }

        var card = new CardSource(context, cardInstanceId, controller, instance.OwnerId);
        IReadOnlyList<ICardEffect> effects = effect.CardEffects(timing, card);
        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] is not IActivatedCardEffect)
            {
                continue;
            }

            if (effects[i] is ActivatedEffect uniform)
            {
                CardEffectResolveContext resolveCtx = BuildUniformResolveContext(uniform, drivingEvent);
                if (uniform.CanResolve(resolveCtx)
                    && context.OnceFlags.CanActivate(resolveCtx.Request, uniform.MaxCountPerTurn))
                {
                    return true;
                }
            }
            else if (effects[i] is DigiBurstActivatedEffect burst)
            {
                // (B-2) Mirror AS-IS IDigiBurst.CanDigiBurst / ST4_13's CanUseCondition (ST4_13.cs:37-48): a [Main]
                // Digi-Burst is declarable only when (a) the card's own permanent is NOT stack-trash-immune
                // (CanDigiBurst's FIRST check, CardController.cs:2141 — the immunity blocks the whole burst, not
                // just the trash) and (b) it holds >= Count TRASHABLE digivolution sources — the SAME gate the
                // resolver's DigiBurst case applies before paying. Without these the skill is offered even when it
                // cannot pay, a phantom legal move AS-IS's CanDeclareSkillList never surfaces.
                if (!RestrictionScan.IsRestricted(
                        context, MatchStateMutationSink.ImmuneStackTrashingKey, burst.Card.InstanceId, burst.Card.InstanceId)
                    && CardEffectCommons.TrashableDigivolutionCount(burst.Card, burst.Card.InstanceId) >= burst.Count)
                {
                    return true;
                }
            }
            else
            {
                // Other non-uniform IActivatedCardEffect: no ported OnDeclaration witness exists today. Mirror
                // CanActivateAt and offer it (its resolution self-gates); add a declare-gate here when such a card
                // is ported so an unpayable [Main] skill is not surfaced.
                return true;
            }
        }

        return false;
    }

    /// <summary>(RDx-A3) Build the resolve-context for a uniform <see cref="ActivatedEffect"/> EXACTLY as the
    /// resolution loop does — TriggerEntityId is the driving event's subject (broadcast bridge) or the card itself
    /// (subject-scoped), and the event's primitive metadata is threaded as "event.&lt;key&gt;" values. Shared by the
    /// resolution loop (uniform case) and <see cref="CanActivateAt"/> so the per-pass gate's CanResolve reads the
    /// IDENTICAL context the resolver will.</summary>
    internal static CardEffectResolveContext BuildUniformResolveContext(ActivatedEffect uniform, GameEvent? drivingEvent)
    {
        ArgumentNullException.ThrowIfNull(uniform);
        HeadlessEntityId triggerId =
            drivingEvent?.Subject is HeadlessEntityId eventSubject && !eventSubject.IsEmpty
                ? eventSubject
                : uniform.Card.InstanceId;
        Dictionary<string, object?>? eventValues = null;
        if (drivingEvent is not null)
        {
            eventValues = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [GameFlowProcessor.EventTypeKey] = drivingEvent.Type.ToString(),
            };
            foreach (KeyValuePair<string, object?> pair in drivingEvent.Metadata)
            {
                if (pair.Value is string or int or bool or long)
                {
                    eventValues[$"{GameFlowProcessor.EventValuePrefix}{pair.Key}"] = pair.Value;
                }
            }
        }

        var subjectCtx = new EffectContext(
            uniform.Card.Controller, uniform.Card.Owner, uniform.Card.InstanceId,
            triggerEntityId: triggerId, targetEntityIds: Array.Empty<HeadlessEntityId>(),
            values: eventValues);
        return new CardEffectResolveContext(new EffectRequest(
            uniform.EffectId, uniform.Card.Controller, EffectTimings.ToTriggerName(uniform.Timing), subjectCtx));
    }

    public static async Task<int> ResolveAsync(
        EngineContext context,
        HeadlessEntityId cardInstanceId,
        HeadlessPlayerId controller,
        EffectTiming timing,
        CancellationToken cancellationToken = default,
        bool skipReactivationHolder = false,
        GameEvent? drivingEvent = null,
        Func<ICardEffect, bool>? effectFilter = null,
        bool declarative = false,
        bool windowDispatched = false,
        bool inheritedScan = false)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (cardInstanceId.IsEmpty
            || !context.CardInstanceRepository.TryGetInstance(cardInstanceId, out CardInstanceRecord? instance)
            || instance is null
            || !context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            || def is null
            || !CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            || effect is null)
        {
            return 0;
        }

        var card = new CardSource(context, cardInstanceId, controller, instance.OwnerId);
        IReadOnlyList<HeadlessPlayerId> players = ResolvePlayers(context, controller);
        var sink = new MatchStateMutationSink(
            context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.EffectRegistry, context.GameEventQueue,
            // (FR-P3) pass the EngineContext so a deletion/suspend/return honours PLAYER-SCOPE restrictions with
            // an arbitrary permanentCondition ("your <X> Digimon cannot be ..."), not just the card's own self.
            // Passing `context` also means an activated play (e.g. ActivatedPlayFromUnderEffect →
            // PlayDigivolutionAsDigimonKind, G9) auto-registers the entered card's effects via the sink's default
            // enter-play hook (context.RegisterEnteredCardEffects) — AS-IS PlayCardClass.PlayCard() semantics.
            context: context);

        // G7-005: participate in the W7 deferred-choice cycle. With an interactive DeferredChoiceProvider,
        // a ChooseAsync below throws DeferredChoicePendingException to SUSPEND — we then do NOT flush the
        // (fresh, unflushed) sink or complete the cycle, so nothing is partially applied; the caller treats
        // it as pending and re-invokes once the agent answers, when BeginResolution replays the answer.
        //
        // (B-1 rework) the OnceFlags uniform-cycle transaction runs the SAME lifecycle: consumes staged during
        // the run are kept across a suspend (replayed on the re-invocation, alongside the replayed answers) and
        // committed exactly once on completion — BEFORE the flush, so windows opened by the flushed events read
        // committed caps. This is what lets the uniform case register a use BEFORE the body (AS-IS
        // register-before-body) without the resumed re-run reading its own consume as capped-out (the original
        // B-1 bug) and without an earlier effect's consume outliving its discarded sink when a LATER effect in
        // the same list suspends (both replay together).
        // Build the effect list BEFORE opening the cycle: a throw here (e.g. a runtime-STOP in a card's
        // CardEffects) must not leak an open cycle (the leak would make every later resolution a non-owner —
        // no commits — and desync the journal; 2026-07-11 re-review P2-3).
        IReadOnlyList<ICardEffect> effects = effect.CardEffects(timing, card);
        if (effectFilter is not null)
        {
            // (#13) e.g. re-run only the [Main] option effect, not every OptionSkill effect.
            effects = effects.Where(effectFilter).ToList();
        }

        // (F1-M1-INHERITSCAN) apply the AS-IS EffectList_ForCard membership split: a SOURCE-scan resolution
        // (inheritedScan, the digivolution-source activated bridge) runs ONLY the inherited activated effects;
        // every other (default) path runs only the non-inherited ones. Behaviour-neutral for the default path
        // (no ported effect is inherited yet, so the non-inherited filter keeps them all).
        effects = effects.Where(e => MembershipKeeps(e, inheritedScan)).ToList();

        if (skipReactivationHolder)
        {
            // The [On Play] play path resolves a card's own OnEnterFieldAnyone [On Play] effects, but the
            // [All Turns] reactivation-holder effect (ReuseWhenDigivolvingEffect) shares that timing and must NOT
            // self-fire — it reacts to OTHER cards' plays (driven by OnPlayReactivation, which excludes the
            // just-played card). Skip it here so playing a holder doesn't wrongly trigger its own reactivation.
            effects = effects.Where(e => e is not ReuseWhenDigivolvingEffect).ToList();
        }

        var coordinator = context.ChoiceProvider as IDeferredChoiceCoordinator;
        coordinator?.BeginResolution();
        bool cycleOwner = context.OnceFlags.BeginUniformCycle();

        int resolved;
        try
        {
            resolved = await ResolveListAsync(
                context, effect, card, players, sink, effects, cancellationToken, drivingEvent,
                declarative: declarative, windowDispatched: windowDispatched).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DeferredChoicePendingException or WindowChoicePendingException)
        {
            context.OnceFlags.SuspendUniformCycle(cycleOwner);
            throw;
        }
        catch
        {
            context.OnceFlags.AbortUniformCycle(cycleOwner);
            throw;
        }

        context.OnceFlags.CompleteUniformCycle(cycleOwner);
        await sink.FlushAsync(cancellationToken).ConfigureAwait(false);
        coordinator?.CompleteResolution();
        return resolved;
    }

    private static async Task<int> ResolveListAsync(
        EngineContext context,
        CEntity_Effect effectClass,
        CardSource card,
        IReadOnlyList<HeadlessPlayerId> players,
        MatchStateMutationSink sink,
        IReadOnlyList<ICardEffect> cardEffects,
        CancellationToken cancellationToken,
        GameEvent? drivingEvent = null,
        bool declarative = false,
        bool windowDispatched = false)
    {
        int resolved = 0;
        foreach (ICardEffect cardEffect in cardEffects)
        {
            switch (cardEffect)
            {
                case ModeChoiceEffect mode:
                {
                    // (PRIM-P0-flow) present the mode menu (available modes only), then dispatch the chosen
                    // branch through this same resolver — sharing the one sink and deferred-choice cycle.
                    IReadOnlyList<ModeChoiceEffect.Mode> available = mode.AvailableModes();
                    if (available.Count > 0)
                    {
                        ChoiceResult result = await context.ChoiceProvider
                            .ChooseAsync(mode.BuildRequest(available), cancellationToken).ConfigureAwait(false);
                        if (!result.IsSkipped && result.SelectedIds.Count > 0)
                        {
                            ICardEffect branch = mode.BranchFor(available, result.SelectedIds[0]);
                            resolved += await ResolveListAsync(
                                context, effectClass, card, players, sink, new[] { branch }, cancellationToken, drivingEvent).ConfigureAwait(false);
                        }
                    }

                    break;
                }

                case DigiBurstActivatedEffect burst:
                {
                    // (B-2 rework, AS-IS IDigiBurst.DigiBurst — CardController.cs:2135-2233) the full sequence:
                    //   1. CanDigiBurst gate (:2135-2160): FIRST the permanent-scope
                    //      `ImmuneFromStackTrashing(_cardEffect)` (:2141 — blocks the ENTIRE burst, not just the
                    //      trash), THEN the permanent holds >= Count TRASHABLE sources (per-source
                    //      !CanNotTrashFromDigivolutionCards).
                    //   2. The CONTROLLER SELECTS which sources to discard (SelectCardEffect over
                    //      _permanent.DigivolutionCards, exactly Count, canNoSelect:false, face-down :2171-2195)
                    //      — NOT an automatic bottom-N.
                    //   3. When >= 1 selected: the OnUseDigiburst window opens BEFORE the trash (:2228) — here the
                    //      emit precedes the staged trash, so the queue drains them in the AS-IS relative order.
                    //   4. ITrashDigivolutionCards trashes exactly the SELECTED cards (:2233).
                    //   5. The body resolves after the pay.
                    // Design item B2-UPTO: the AS-IS _upToMaxCount ("Digi-Burst up to N") variant — a
                    // canEndNotMax select + `Some` gate — lands with its first ported witness; every current
                    // witness (ST4_13) is a fixed count.
                    IReadOnlyList<HeadlessEntityId> pool = CardEffectCommons.TrashableDigivolutionSourceIds(burst.Card, burst.Card.InstanceId);
                    if (!RestrictionScan.IsRestricted(
                            context, MatchStateMutationSink.ImmuneStackTrashingKey, burst.Card.InstanceId, burst.Card.InstanceId)
                        && pool.Count >= burst.Count)
                    {
                        var candidates = pool
                            .Select(id => new ChoiceCandidate(id, id.Value, ChoiceZone.DigivolutionCards, IsSelectable: true, ownerId: burst.Card.Controller))
                            .ToList();
                        var request = new ChoiceRequest(
                            ChoiceType.Card, burst.Card.Controller, "Select digivolution cards to discard.",
                            minCount: burst.Count, maxCount: burst.Count, canSkip: false, ChoiceZone.DigivolutionCards, candidates);
                        ChoiceResult selection = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);

                        if (!selection.IsSkipped && selection.SelectedIds.Count >= 1)
                        {
                            EmitJournaled(context, TriggerTimings.OnUseDigiburst, burst.Card.Controller, burst.Card.InstanceId);

                            sink.Apply(new EffectMutation(
                                MatchStateMutationSink.TrashDigivolutionCardsKind, burst.Card.InstanceId,
                                new Dictionary<string, object?>(StringComparer.Ordinal)
                                {
                                    [MatchStateMutationSink.SelectedCardIdsKey] = string.Join(",", selection.SelectedIds.Select(id => id.Value)),
                                }));

                            // The Digi-Burst body is either an ACTIVATED effect (draw/delete/trash — resolve it) or
                            // a CONTINUOUS grant (e.g. "your Digimon gain <keyword>" — register it, as at enter-play).
                            if (burst.InnerEffect is IActivatedCardEffect)
                            {
                                resolved += await ResolveListAsync(
                                    context, effectClass, burst.Card, players, sink, new[] { burst.InnerEffect }, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                // Journaled + deterministic id: a resumed replay of this already-performed
                                // registration must not register a second binding.
                                RunJournaledImmediate(context, () => context.EffectRegistry.Register(burst.InnerEffect.ToBinding(
                                    $"{burst.Card.InstanceId.Value}:digiburst:{burst.InnerEffect.GetType().Name}")));
                            }
                        }
                    }

                    resolved++;
                    break;
                }

                case DnaFromHandOrTrashActivatedEffect dna:
                {
                    // (PRIM special-play) AS-IS DNADigivolveWithHandOrTrashCardIntoHandOrTrash: auto-match an
                    // into-card (hand/trash), a battle-area permanent, and a hand/trash material, then fuse the
                    // permanent + material under the into-card (DNA digivolution). Consistent with the other
                    // special plays' auto-match model.
                    var reader = (Headless.Services.IZoneStateReader)context.ZoneMover;
                    HeadlessPlayerId owner = dna.Card.Owner;
                    ChoiceZone intoZone = dna.IntoFromHand ? ChoiceZone.Hand : ChoiceZone.Trash;
                    ChoiceZone materialZone = dna.MaterialFromHand ? ChoiceZone.Hand : ChoiceZone.Trash;

                    HeadlessEntityId? into = FirstMatch(context, reader.GetCards(owner, intoZone), owner, dna.IntoCondition, exclude: default);
                    HeadlessEntityId? permanent = FirstMatch(context, reader.GetCards(owner, ChoiceZone.BattleArea), owner, dna.PermanentCondition, exclude: default);
                    HeadlessEntityId? material = into is HeadlessEntityId intoId
                        ? FirstMatch(context, reader.GetCards(owner, materialZone), owner, dna.MaterialCondition, exclude: intoId)
                        : null;

                    if (into is HeadlessEntityId topId && permanent is HeadlessEntityId permId && material is HeadlessEntityId matId)
                    {
                        // (B-3 tuck reset) DNA/Jogress resets every source of the fused stack (CardController.cs:1509-1512).
                        // Journaled: the fuse performs DIRECT zone moves — a resumed replay must not re-fuse.
                        int fusedCount = 0;
                        await RunJournaledImmediateAsync(context, async () =>
                        {
                            IReadOnlyList<HeadlessEntityId> merged = await FusionDigivolveHelpers.FuseAsync(
                                context.CardInstanceRepository, context.ZoneMover, topId, intoZone,
                                new[] { permId, matId }, gameEventQueue: context.GameEventQueue,
                                kind: FusionKind.DnaDigivolve, cancellationToken: cancellationToken,
                                onceFlags: context.OnceFlags).ConfigureAwait(false);
                            fusedCount = merged.Count;
                        }).ConfigureAwait(false);
                        if (fusedCount > 0)
                        {
                            resolved++;
                        }
                    }

                    resolved++;
                    break;
                }

                case PlayOptionCardEffect playOption:
                {
                    // (PRIM-P0 B.O.5) select Option card(s) from a zone and play each as a nested effect: trash it
                    // (matching the headless OptionActivate order: trash-before-resolve), open OnUseOption, then
                    // resolve its [Main] (OptionSkill) through the SAME sink / deferred-choice cycle (recursive
                    // ResolveListAsync, NOT a nested ResolveAsync — same reason as ReuseMainOptionEffect).
                    ChoiceResult result = await context.ChoiceProvider
                        .ChooseAsync(playOption.BuildRequest(players), cancellationToken).ConfigureAwait(false);
                    if (!result.IsSkipped)
                    {
                        foreach (HeadlessEntityId optionId in result.SelectedIds)
                        {
                            if (optionId.IsEmpty ||
                                !context.CardInstanceRepository.TryGetInstance(optionId, out CardInstanceRecord? optInstance) || optInstance is null ||
                                !context.CardRepository.TryGetCard(optInstance.DefinitionId, out CardRecord? optDef) || optDef is null ||
                                !CardEffectDispatch.TryCreateForCard(optDef, out CEntity_Effect? optEffect) || optEffect is null)
                            {
                                continue;
                            }

                            // Journaled AS ONE ENTRY: the trash move is a DIRECT (immediately-applied) zone
                            // mutation — a resumed replay re-running it would throw (the card already left the
                            // source zone) — and the OnUseOption emit must not re-queue either.
                            await RunJournaledImmediateAsync(context, async () =>
                            {
                                await context.ZoneMover.MoveAsync(
                                    new ZoneMoveRequest(optInstance.OwnerId, optionId, playOption.SourceZone, ChoiceZone.Trash),
                                    cancellationToken).ConfigureAwait(false);
                                TriggerEventEmitter.Emit(context.GameEventQueue, TriggerTimings.OnUseOption, actor: card.Controller, subject: optionId);
                            }).ConfigureAwait(false);

                            var optCard = new CardSource(context, optionId, card.Controller, optInstance.OwnerId);
                            resolved += await ResolveListAsync(
                                context, optEffect, optCard, players, sink,
                                optEffect.CardEffects(EffectTiming.OptionSkill, optCard), cancellationToken).ConfigureAwait(false);
                        }
                    }

                    resolved++;
                    break;
                }

                case RevealSelectThenPlaySelectedEffect revealPlay:
                {
                    // (BT1_078 / BT3_063 / BT3_070 / BT3_073) reveal top N -> optional select 1 -> remaining to
                    // deck bottom -> play-as-new-permanent OR free-digivolve onto self (RevealPlayMode). Drives
                    // the ChoiceProvider itself; the follow-up is a direct play/digivolve.
                    await revealPlay.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case ActivatedDrawThenDiscardEffect drawDiscard:
                {
                    // (G4) draw N -> discard M (atomic; DrawAndDiscardCards flushes the draw before building the
                    // discard pool). Drives the ChoiceProvider itself for the discard select.
                    await drawDiscard.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case ChooseCountThenTrashDigivolutionEffect chooseCount:
                {
                    // (G12 / BT3_100) choose count 0..N -> trash that many (capped) digivolution cards from every matching target.
                    await chooseCount.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case OpponentBinaryChoiceEffect oppBinary:
                {
                    // (G13 / BT3_102) opponent yes/no decision -> branch (auto-no when nothing to decide).
                    await oppBinary.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case SelectDeDigivolveThenConditionalDestroyEffect selDeDig:
                {
                    // (G10 / BT3_107) select 1 -> de-digivolve N (flush) -> destroy if post-state predicate holds.
                    await selDeDig.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case MassDeDigivolveThenConditionalDestroyEffect massDeDig:
                {
                    // (G10 / BT3_112 WD) de-digivolve all matching (flush) -> destroy each satisfying post-state predicate.
                    await massDeDig.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case SelectHandAttachToOwnStackThenMemoryEffect attachStack:
                {
                    // (G8 / BT3_019) optional select 1 hand card -> attach on top of this card's own stack ->
                    // gain memory (only if placed). Drives the ChoiceProvider + a direct attach move; the
                    // memory is staged on the sink.
                    await attachStack.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case SelectDigivolutionSourceToHandThenSelfFollowUpEffect sourceToHand:
                {
                    // (BT1_084 br2 / BT3_112 br2) select 1 source from this card's own stack -> hand -> self
                    // follow-up. Drives the ChoiceProvider + a direct source-return move; the follow-up
                    // (unsuspend via sink / GainCanNotBeBlocked via registry) runs after the return.
                    await sourceToHand.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case SecuritySelectToHandColorRecoveryShuffleEffect securitySelect:
                {
                    // (BT1_087) select 1 security card -> hand, color-gated Recovery+1, then shuffle security.
                    // Drives the ChoiceProvider; the add-to-hand / recovery / shuffle stage on the sink in order.
                    await securitySelect.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case ActivatedTargetRestrictionEffect restrict:
                {
                    // (ST2_14 / ST4_12 / BT1_113) select up to maxCount matching permanents, then register the
                    // duration-tagged can't-attack / can't-block restriction binding(s) on the pick(s) — the
                    // AS-IS SelectPermanentEffect(Mode.Custom) whose SelectPermanentCoroutine runs
                    // GainCanNotAttack + GainCanNotBlock per selected permanent (ST2_14.cs:44-86). Same shape as
                    // the other interactive cases: BuildRequest -> ChoiceProvider -> apply. The AS-IS
                    // ActivateCoroutine is guarded by HasMatchConditionPermanent (ST2_14.cs:46) — no select (and
                    // no registration) when nothing matches.
                    ChoiceRequest request = restrict.BuildRequest(players);
                    if (request.Candidates.Count > 0)
                    {
                        ChoiceResult result = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
                        if (!result.IsSkipped && result.SelectedIds.Count > 0)
                        {
                            // Journaled: a registry registration is an immediately-applied side effect — a
                            // resumed replay must not register the restriction twice.
                            RunJournaledImmediate(context, () => restrict.ApplyRestriction(result.SelectedIds));
                        }
                    }

                    resolved++;
                    break;
                }

                case ActivatedMemoryEffect memory:
                {
                    // (BT2_087) direct memory gain/loss — no choice; stage on the shared sink. Formerly missing
                    // from this switch: the effect fell through to the silent default and BT2_087's
                    // [Start of Your Turn] +1 memory was a NO-OP (2026-07-11 re-review finding).
                    memory.Apply(sink);
                    resolved++;
                    break;
                }

                case AddThisCardToHandEffect addHand:
                {
                    // (E-3) "Then, add this card to its owner's hand" (AS-IS AddThisCardToHand) — no choice; stage
                    // the ReturnToHand mutation on the shared sink. Formerly missing from this switch: the effect
                    // fell through to the silent default (same missing-case class as ActivatedMemoryEffect above),
                    // so EX1_072's [Security] add-to-hand and BT9_109's [Security] add-to-hand were both no-ops.
                    addHand.Apply(sink);
                    resolved++;
                    break;
                }

                case ActivatedSelectAndDeDigivolveEffect selectDeDigivolve:
                {
                    // Select up to maxCount matching permanents, then de-digivolve each by `count` — same
                    // BuildRequest -> ChoiceProvider -> Apply shape as the other interactive cases. No ported
                    // caller today; wired so the factory (CardEffectFactory.SelectAndDeDigivolveEffect) is not a
                    // silent no-op when one lands (same missing-case class as ActivatedMemoryEffect above).
                    ChoiceRequest ddRequest = selectDeDigivolve.BuildRequest(players);
                    if (ddRequest.Candidates.Count > 0)
                    {
                        ChoiceResult ddResult = await context.ChoiceProvider.ChooseAsync(ddRequest, cancellationToken).ConfigureAwait(false);
                        if (!ddResult.IsSkipped && ddResult.SelectedIds.Count > 0)
                        {
                            selectDeDigivolve.Apply(sink, ddResult.SelectedIds);
                        }
                    }

                    resolved++;
                    break;
                }

                case ActivatedEffect uniform:
                {
                    // Uniform activated effect (mirror of AS-IS ActivateClass). Without a driving event the card
                    // being resolved IS the event subject (subject-scoped bridge/onplay/digivolve route by
                    // subject), so TriggerEntityId falls back to the card itself. A BROADCAST bridge timing
                    // (AS-IS StackSkillInfos offers the event to every field card) passes the driving event:
                    // TriggerEntityId is then the event's subject (e.g. the Digimon whose sources were trashed,
                    // not this listener) and the event's primitive metadata is threaded as "event.<key>" values
                    // so gates (CanTriggerOnTrashDigivolutionCard …) read the AS-IS hashtable mirror.
                    // (RDx-A3) build the resolve-context via the SHARED helper so the window's per-pass gate
                    // (CanActivateAt → MarkerGate) reads the IDENTICAL context this resolver will.
                    CardEffectResolveContext resolveCtx = BuildUniformResolveContext(uniform, drivingEvent);

                    // Gate: a WINDOW-dispatched marker re-checks only the CanActivate half at execution entry —
                    // AS-IS AutoProcessing.cs:1068 runs `CanActivate(hashtable)` on the stacked skill and NEVER
                    // re-evaluates CanTrigger/CanUseCondition after collect (the collect gate already ran in
                    // CanCollectAt when the marker was synthesised). A DIRECT path (option play / on-play /
                    // declaration) collects-and-resolves inline, so it evaluates BOTH halves here, mirroring the
                    // AS-IS collect filter + execution gate run back-to-back.
                    bool gate = windowDispatched ? uniform.CanResolveActivateHalf() : uniform.CanResolve(resolveCtx);
                    if (!gate)
                    {
                        break;
                    }

                    // AS-IS CanActivate includes the once-per-turn cap (ICardEffect.cs:366-372) — re-checked with
                    // the condition half above at every gate site. Cycle-aware: a resumed re-run sees the same
                    // view its original run saw (staged consumes replay).
                    if (!context.OnceFlags.CanActivate(resolveCtx.Request, uniform.MaxCountPerTurn))
                    {
                        break;
                    }

                    // (B-1 rework) register the per-turn use BEFORE the body — AS-IS register-before-body. The
                    // register point differs by path, and both are mirrored:
                    //   - DECLARATIVE ([Main] skill declaration, TurnStateMachine.cs:1183-1186): register fires
                    //     BEFORE the optional prompt — declining a declared capped skill leaves the cap consumed
                    //     (that path has no RemoveUse). AS-IS then bypasses its own consumed cap with the
                    //     IsDeclarative flag (AutoProcessing.cs:1068 `CanActivate || IsDeclarative`); here the cap
                    //     was checked above, before the register, so no bypass flag is needed — same net gate.
                    //   - WINDOW / standard (ICardEffect.cs:1117-1124 Activate_Execute): the OnProcessCallbuck
                    //     register fires AFTER the optional gate (`UseOptional || !IsOptional`) and BEFORE the
                    //     body coroutine — declining registers nothing.
                    // Suspend safety is the OnceFlags uniform-cycle transaction (see ResolveAsync), NOT a consume
                    // re-order: a suspend keeps the staged use and the resumed replay neither double-consumes nor
                    // reads itself as capped-out.
                    if (declarative)
                    {
                        context.OnceFlags.Consume(resolveCtx.Request, uniform.MaxCountPerTurn);
                    }

                    // (RD-13) an optional effect ("you may ...") asks the controller yes/no before it runs (AS-IS
                    // OptionalSkill / Activate_Optional_Effect_Execute).
                    if (uniform.IsOptional && !await ConfirmOptionalAsync(context, uniform, cancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }

                    if (!declarative)
                    {
                        context.OnceFlags.Consume(resolveCtx.Request, uniform.MaxCountPerTurn);
                    }

                    // (B-4 rework) run the body; the use stays consumed even when the body does nothing — the
                    // AS-IS DEFAULT (~1,170 [Once Per Turn] cards never call RemoveUse). ONLY a card whose AS-IS
                    // body explicitly runs `if (!executed) RemoveUse()` opts in via RefundWhenNotExecuted
                    // (executed defaults to "selection not skipped", overridable per card via ExecutedPredicate —
                    // AS-IS executed is card-defined: AD1_024's 3-branch OR, BT14_029's board predicate).
                    bool executed = await uniform.ResolveBodyAsync(sink, context.ChoiceProvider, players, cancellationToken).ConfigureAwait(false);
                    if (!executed && uniform.RefundWhenNotExecuted)
                    {
                        context.OnceFlags.Refund(resolveCtx.Request, uniform.MaxCountPerTurn);
                    }

                    resolved++;
                    break;
                }

                case DrawEffect draw:
                {
                    // (BT-PRE-A1) "draw N" — no choice; stage the DrawCards mutation on the shared sink.
                    draw.Apply(sink);
                    resolved++;
                    break;
                }

                case SimplifiedRevealAndSelectEffect reveal:
                {
                    // (BT-PRE-A2) reveal top N + per-condition select + destination routing. Drives the
                    // ChoiceProvider itself (multi-step), staging every move on the shared sink.
                    await reveal.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case ArtsDigivolveSelfEffect arts:
                {
                    // (W6-A2) Arts Digivolve: cost-free evolution out of the executing area.
                    await arts.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case SelectAndDigivolveEffect selectDigivolve:
                {
                    // (PRIM-P0-flow B.O.3) select target + source card (hand/trash) then digivolve, paying cost.
                    await selectDigivolve.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case RevealMultiSelectEffect revealMulti:
                {
                    // (P4) FULL multi-condition reveal (shared pool, per-pass destination incl. Custom,
                    // opt-out, mutual rule, remaining ordering). ChoiceProvider-driven, sink-staged.
                    await revealMulti.ResolveAsync(sink, cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case DestroyPermanentsEffect destroy:
                {
                    // (BT-PRE-A3) direct-delete a pre-computed target list — no choice; the sink's centralised
                    // immunity / deletion-prevention gates filter.
                    destroy.Apply(sink);
                    resolved++;
                    break;
                }

                case TrashSelfThenGainMemoryDelayEffect delayGain:
                {
                    // (#9) [Main] <Delay>: trash this card's own permanent, then gain memory ONLY if it was
                    // trashed (self-contained delete + success branch); does not use the shared sink.
                    await delayGain.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case DeckBottomBounceEffect bounce:
                {
                    // (PRIM-W2) direct-return a pre-computed target list to the deck bottom — no choice.
                    bounce.Apply(sink);
                    resolved++;
                    break;
                }

                case LinkSelfEffect link:
                {
                    // (PRIM-W2) <Link>: choose a host + attach this card as a link card (LinkHelpers).
                    await link.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case HatchDigiEggEffect hatch:
                {
                    // (BT-PRE-A4) CanHatch-gated digi-egg hatch — no choice; a direct ZoneMover move (no sink
                    // kind for hatch), re-run safe via the empty-breeding-area guard.
                    await hatch.ResolveAsync(cancellationToken).ConfigureAwait(false);
                    resolved++;
                    break;
                }

                case PlayCardEffect playCard:
                {
                    // (BT-PRE-A5) cost-free play of a pre-selected card — no choice; stage the PlayCard mutation.
                    playCard.Apply(sink);
                    resolved++;
                    break;
                }

                case PlayThisCardToBattleEffect playSelf:
                {
                    // (G10-003) A Tamer's [Security] "play this Tamer": play the revealed card onto the
                    // battle area cost-free; the PlayCard mutation auto-registers its effects.
                    playSelf.Apply(sink);
                    resolved++;
                    break;
                }

                case PlaySelfAtEndOfBattleSecurityEffect playAfterBattle:
                {
                    // (#10) A [Security] "at end of battle, play this Digimon": register the OnEndBattle trigger
                    // instead of playing now, so the play (and any turn-end delete) resolves after the battle.
                    playAfterBattle.Apply(sink);
                    resolved++;
                    break;
                }

                case BeforePayCostReductionEffect beforePayReduce:
                {
                    // (PRIM-P0 B.O.4) non-interactive one-shot before-pay reduction of this play's own cost.
                    beforePayReduce.Apply();
                    resolved++;
                    break;
                }

                case ReuseMainOptionEffect:
                {
                    // (G8-004 / #13) "[Security] activate this card's [Main] effect" — resolve ONLY the card's
                    // [Main]-tagged OptionSkill effect (AS-IS OptionMainEffect), not every OptionSkill effect,
                    // through the same sink / choice provider.
                    resolved += await ResolveListAsync(
                        context, effectClass, card, players, sink,
                        effectClass.CardEffects(EffectTiming.OptionSkill, card).Where(IsMainOptionEffect).ToList(),
                        cancellationToken).ConfigureAwait(false);
                    break;
                }

                case ReuseWhenDigivolvingEffect:
                {
                    // (EX8-2 brick) "[All Turns] activate this card's [When Digivolving] effects" — resolve the
                    // card's WhenDigivolving activated effects, recursively, through the same sink / choice
                    // provider (same shape as ReuseMainOptionEffect, different timing).
                    resolved += await ResolveListAsync(
                        context, effectClass, card, players, sink,
                        effectClass.CardEffects(EffectTiming.WhenDigivolving, card), cancellationToken).ConfigureAwait(false);
                    break;
                }

                // DeferredCardEffect / non-activated effects: not resolved here.
            }
        }

        return resolved;
    }

    /// <summary>(B-1 rework) Emit a trigger-timing event through the uniform-cycle MUTATION JOURNAL so a
    /// suspended resolution's REPLAY does not re-queue it (a bare queue push is an immediately-applied side
    /// effect the fresh re-run would double). Outside a cycle this is a plain emit.</summary>
    private static void EmitJournaled(EngineContext context, string timing, HeadlessPlayerId actor, HeadlessEntityId subject) =>
        RunJournaledImmediate(context, () => TriggerEventEmitter.Emit(context.GameEventQueue, timing, actor: actor, subject: subject));

    /// <summary>(B-1 rework) Run an IMMEDIATELY-APPLIED side effect (direct zone move / registry registration /
    /// event emit — anything not staged on the sink) through the uniform-cycle mutation journal: a resumed
    /// replay SKIPS it (its effect already persists in game state) instead of doubling it. Outside a cycle it
    /// just runs. The action must be synchronous-in-effect by the time it returns (an awaited direct move is
    /// wrapped by the async overload below).</summary>
    private static void RunJournaledImmediate(EngineContext context, Action action)
    {
        OnceFlagController.MutationReplay replay = context.OnceFlags.BeginMutationApply();
        if (replay == OnceFlagController.MutationReplay.Skip)
        {
            return;
        }

        action();
        if (replay == OnceFlagController.MutationReplay.Fresh)
        {
            context.OnceFlags.RecordFreshMutation(purelyImmediate: true);
        }
    }

    /// <summary>Async twin of <see cref="RunJournaledImmediate(EngineContext, Action)"/> for awaited direct
    /// mutations (ZoneMover moves, FuseAsync).</summary>
    private static async Task RunJournaledImmediateAsync(EngineContext context, Func<Task> action)
    {
        OnceFlagController.MutationReplay replay = context.OnceFlags.BeginMutationApply();
        if (replay == OnceFlagController.MutationReplay.Skip)
        {
            return;
        }

        await action().ConfigureAwait(false);
        if (replay == OnceFlagController.MutationReplay.Fresh)
        {
            context.OnceFlags.RecordFreshMutation(purelyImmediate: true);
        }
    }

    /// <summary>(RD-13) Ask the effect's controller whether to use an OPTIONAL effect (AS-IS OptionalSkill
    /// "Will you use ~?"). A single "use" candidate that the agent selects (yes) or skips (no) — the same
    /// <see cref="ChoiceType.OptionalEffect"/> the trigger-window optional prompt uses.</summary>
    private static async Task<bool> ConfirmOptionalAsync(EngineContext context, ActivatedEffect uniform, CancellationToken cancellationToken)
    {
        var request = new ChoiceRequest(
            ChoiceType.OptionalEffect,
            uniform.Card.Controller,
            $"Use optional effect? {uniform.Description}",
            minCount: 0,
            maxCount: 1,
            canSkip: true,
            ChoiceZone.Custom,
            new[]
            {
                new ChoiceCandidate(uniform.EffectId, uniform.Description, ChoiceZone.Custom, IsSelectable: true, ownerId: uniform.Card.Controller),
            });

        ChoiceResult decision = await context.ChoiceProvider.ChooseAsync(request, cancellationToken).ConfigureAwait(false);
        return !decision.IsSkipped && decision.SelectedIds.Count > 0;
    }

    private static IReadOnlyList<HeadlessPlayerId> ResolvePlayers(EngineContext context, HeadlessPlayerId controller)
    {
        var players = new List<HeadlessPlayerId>();
        void Add(HeadlessPlayerId? candidate)
        {
            if (candidate is HeadlessPlayerId id && !id.IsEmpty && !players.Contains(id))
            {
                players.Add(id);
            }
        }

        Add(controller);
        Add(context.TurnController.Current.TurnPlayerId);
        Add(context.TurnController.Current.NonTurnPlayerId);
        return players;
    }

    /// <summary>(DNA-from-hand/trash) The first card in <paramref name="pool"/> that satisfies
    /// <paramref name="condition"/> (evaluated as a <see cref="CardSource"/>), other than <paramref name="exclude"/>.</summary>
    private static HeadlessEntityId? FirstMatch(
        EngineContext context, IReadOnlyList<HeadlessEntityId> pool, HeadlessPlayerId owner,
        Func<CardSource, bool> condition, HeadlessEntityId exclude)
    {
        foreach (HeadlessEntityId id in pool)
        {
            if (id != exclude && condition(new CardSource(context, id, owner, owner)))
            {
                return id;
            }
        }

        return null;
    }
}
