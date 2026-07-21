namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections;
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
        // (EXEMPLAR-T1, first new-model consumer — P_223 [On Play] option-from-trash) a NEW-model
        // ActivateClass carries its text on the AS-IS-named EffectDiscription surface (ICardEffect.cs:216,
        // set by SetUpActivateClass), not a "Description" property — the reflection probe above only served
        // the legacy uniform shape, silently filtering every new-model [Main] option out of the
        // PlayOptionCards / ReuseMainOptionEffect route.
        description ??= effect.EffectDiscription;
        return description is not null && description.Contains("[Main]", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>(F1-M1-INHERITSCAN) The AS-IS <c>Permanent.EffectList_ForCard</c> membership split
    /// (Permanent.cs:1526-1541) applied to ONE scanned card's effect list: a SOURCE scan
    /// (<paramref name="inheritedScan"/> == true) keeps only INHERITED activated effects (AS-IS a NON-TOP source
    /// contributes only its <c>IsInheritedEffect</c> effects), a TOP scan (false — the default for every
    /// non-bridge caller: option / security / declaration / on-play / digivolve) keeps only NON-inherited effects
    /// (AS-IS the top card contributes only its non-inherited effects).
    /// (P6 stage A; uniform-사멸 flip) inherited-ness reads the AS-IS base flag
    /// <see cref="ICardEffect.IsInheritedEffect"/> (cards call <c>SetIsInheritedEffect(true)</c> verbatim); a
    /// legacy corpus type carries no flag (base false = non-inherited). Linked-effect membership (AS-IS
    /// <c>IsLinkedEffect</c> branch) stays the C2-01 latent.</summary>
    private static bool MembershipKeeps(ICardEffect effect, bool inheritedScan) =>
        effect.IsInheritedEffect == inheritedScan;

    /// <summary>(P6 stage A) Whether the effect is ACTIVATED — the legacy marker
    /// (<see cref="IActivatedCardEffect"/>, old-model corpus) or the AS-IS contract
    /// (<see cref="ActivateICardEffect"/>, the new-model ActivateClass/kind classes — the AS-IS collection
    /// filter <c>is ActivateICardEffect</c>, AutoProcessing.cs:780/799/…).</summary>
    private static bool IsActivated(ICardEffect effect) => effect is IActivatedCardEffect or ActivateICardEffect;

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
        using var scope = Headless.Bridge.AmbientMatchContext.Enter(context);
        return card.EffectList(timing).Count > 0;
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
        // (P6 stage A) live per-card enumeration through the AS-IS surface (CardSource.EffectList →
        // cEntity_EffectController.GetCardEffects — includes IAddSkillEffect grants + EffectSourceCard
        // back-fill), under the ambient match scope the GManager-based scan reads.
        using var scope = Headless.Bridge.AmbientMatchContext.Enter(context);
        IReadOnlyList<ICardEffect> effects = card.EffectList(timing);
        for (int i = 0; i < effects.Count; i++)
        {
            if (IsActivated(effects[i]) && MembershipKeeps(effects[i], inheritedScan))
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
        using var scope = Headless.Bridge.AmbientMatchContext.Enter(context);
        IReadOnlyList<ICardEffect> effects = card.EffectList(timing);
        for (int i = 0; i < effects.Count; i++)
        {
            if (!IsActivated(effects[i]) || !MembershipKeeps(effects[i], inheritedScan))
            {
                continue;
            }

            if (effects[i] is ActivateICardEffect)
            {
                // (P6 stage A) NEW-model per-pass re-check = AS-IS `CanActivate(hashtable)` on the stacked
                // skill (MultipleSkills.cs:122/164-165/366) — the cap + CanActivateCondition + disabled +
                // inherited/linked liveness, over the SAME payload the emit threaded (rebuilt per timing).
                // PermanentWhenTriggered is not stamped on this freshly-enumerated instance (the mirror window
                // holds markers, not effect objects — design item P6A-STAMP-PERSISTENCE), so that AS-IS
                // same-permanent re-check is skipped by its own null guard.
                Hashtable? hashtable = ActivatedHashtableBridge.Build(context, timing, drivingEvent!, card);
                if (effects[i].CanActivate(hashtable!))
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
        using var scope = Headless.Bridge.AmbientMatchContext.Enter(context);
        IReadOnlyList<ICardEffect> effects = card.EffectList(timing);
        for (int i = 0; i < effects.Count; i++)
        {
            if (!IsActivated(effects[i]) || !MembershipKeeps(effects[i], inheritedScan))
            {
                continue;
            }

            if (effects[i] is ActivateICardEffect)
            {
                // (P6 stage A) NEW-model collect gate = the AS-IS GetSkillInfos filter
                // (AutoProcessing.cs:770-887): `is ActivateICardEffect && !IsBackgroundProcess &&
                // CanTrigger(hashtable)` — the once-per-turn cap + CanUseCondition over the emit payload.
                Hashtable? hashtable = ActivatedHashtableBridge.Build(context, timing, drivingEvent!, card);
                if (!effects[i].IsBackgroundProcess && effects[i].CanTrigger(hashtable!))
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
        using var scope = Headless.Bridge.AmbientMatchContext.Enter(context);
        IReadOnlyList<ICardEffect> effects = card.EffectList(timing);
        for (int i = 0; i < effects.Count; i++)
        {
            if (!IsActivated(effects[i]))
            {
                continue;
            }

            if (effects[i] is ActivateICardEffect)
            {
                // (P6 stage A) NEW-model declaration legal-move gate = AS-IS Permanent.CanDeclareSkillList
                // (Permanent.cs:1618): `EffectList(OnDeclaration)` filtered to ActivateICardEffect where
                // `CanUse(null)` — hashtable is NULL on the declaration path (also TurnStateMachine.cs:1178).
                if (effects[i].CanUse(null!))
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
                // (R3-W3c B6) rehomed from the ImmuneStackTrashingKey registry scan to the AS-IS-literal live getter
                // Permanent.ImmuneFromStackTrashing(_cardEffect): the burst card's permanent, cause = the burst's
                // own effect (collapsed to its source card via BareCauseEffect), exactly IDigiBurst.CanDigiBurst.
                if (!new Permanent(context, burst.Card.InstanceId).ImmuneFromStackTrashing(BareCauseEffect.For(context, burst.Card.InstanceId))
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

    // (uniform-사멸 flip) BuildUniformResolveContext DELETED — it existed solely to feed the retired uniform
    // ActivatedEffect gate/resolution seats (the OnceFlags string-key cap path); the new-model gates read the
    // AS-IS hashtable payload (ActivatedHashtableBridge) instead.


    public static async Task<int> ResolveAsync(
        EngineContext context,
        HeadlessEntityId cardInstanceId,
        HeadlessPlayerId controller,
        EffectTiming timing,
        CancellationToken cancellationToken = default,
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
        // (P6 stage A) live enumeration through the AS-IS surface (CardSource.EffectList) under the ambient
        // match scope; the AS-IS emit payload for this timing is rebuilt once and threaded through the
        // gates + Activate exactly as AS-IS threads the one hashtable object.
        using var ambientScope = Headless.Bridge.AmbientMatchContext.Enter(context);
        Hashtable? hashtable = ActivatedHashtableBridge.Build(context, timing, drivingEvent!, card);
        IReadOnlyList<ICardEffect> effects = card.EffectList(timing);
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

        return await ResolveWithinCycleAsync(
            context, sink,
            () => ResolveListAsync(
                context, effect, card, players, sink, effects, cancellationToken, drivingEvent,
                declarative: declarative, windowDispatched: windowDispatched, hashtable: hashtable, timing: timing),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>(R3-W1b step a) The resolver's VERIFIED per-resolution suspend/resume cycle, extracted as a
    /// substrate wrapper so the rehoused trigger window (AS-IS <c>MultipleSkills</c>, which resolves ONE
    /// stacked skill per pick) reuses the IDENTICAL lifecycle instead of re-inventing it:
    /// <list type="bullet">
    /// <item><c>IDeferredChoiceCoordinator.BeginResolution</c> / <c>CompleteResolution</c> — the W7 deferred-choice
    /// cycle (a <c>ChooseAsync</c> that suspends replays its answer on the re-invocation);</item>
    /// <item><c>OnceFlags.BeginUniformCycle</c> — the register-before-body transaction: consumes staged during the
    /// run are kept across a suspend (replayed) and committed once on completion, BEFORE the sink flush so windows
    /// opened by the flushed events read committed caps;</item>
    /// <item>on a <c>DeferredChoicePendingException</c> / <c>WindowChoicePendingException</c> the (fresh, unflushed)
    /// sink is NOT flushed and the cycle is SUSPENDED (nothing partially applied) — the caller treats it as pending
    /// and re-invokes once the agent answers; any other throw ABORTS the cycle.</item>
    /// </list>
    /// The caller holds the <see cref="AmbientMatchContext"/> scope and owns the <paramref name="sink"/> (so the
    /// same sink flushes exactly once here); <paramref name="resolveEffects"/> runs the body (a full effect list for
    /// <see cref="ResolveAsync"/>, or a single stacked skill for the window). Behaviour-identical to the former
    /// in-line cycle in <see cref="ResolveAsync"/>.</summary>
    internal static async Task<int> ResolveWithinCycleAsync(
        EngineContext context,
        MatchStateMutationSink sink,
        Func<Task<int>> resolveEffects,
        CancellationToken cancellationToken)
    {
        var coordinator = context.ChoiceProvider as IDeferredChoiceCoordinator;
        coordinator?.BeginResolution();
        // (RD-C5W-ACTIVATEBODY; R6-Da'-6 D1=A single-drive) the register-before-body transaction for the AS-IS
        // CEntity_EffectController per-turn use list (the [Once Per Turn] cap — ICardEffect.CanActivate/
        // isOverMaxCountPerTurn) + the mutation replay journal. Formerly driven in exact lockstep with the
        // OnceFlags uniform-cycle (the invented string-key cap holder) — that cycle died with the uniform
        // ActivatedEffect corpus, leaving CEntityUseCycle the single cycle driver.
        bool ceCycleOwner = CEntityUseCycle.For(context).Begin();

        int resolved;
        try
        {
            resolved = await resolveEffects().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DeferredChoicePendingException or WindowChoicePendingException)
        {
            CEntityUseCycle.For(context).Suspend(ceCycleOwner);
            // (C-Del 3c-2b nested cycles) park this cycle's replay frame — a NESTED cycle's suspension must not
            // leave the coordinator's active depth pointing at the parent's frame (see DeferredChoiceProvider).
            coordinator?.SuspendResolution();
            throw;
        }
        catch
        {
            CEntityUseCycle.For(context).Abort(ceCycleOwner);
            // The cycle is dead — discard its replay frame (positional, like completion).
            coordinator?.CompleteResolution();
            throw;
        }

        CEntityUseCycle.For(context).Complete(ceCycleOwner);
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
        bool windowDispatched = false,
        Hashtable? hashtable = null,
        EffectTiming timing = EffectTiming.None)
    {
        int resolved = 0;
        foreach (ICardEffect cardEffect in cardEffects)
        {
            switch (cardEffect)
            {
                case ActivateICardEffect activate:
                {
                    // ============================================================================================
                    // (P6 stage A) NEW-MODEL execution — the AS-IS stacked-skill sequence, collapsed to the one
                    // marker this resolution serves (window ORDERING stays with WindowResolver = the verified
                    // MultipleSkills mirror; EXECUTION is the AS-IS flow):
                    //   1. collect filter — AS-IS GetSkillInfos (AutoProcessing.cs:770-887): !IsBackgroundProcess
                    //      && CanTrigger(hashtable). A WINDOW-dispatched marker already ran it at synthesis
                    //      (CanCollectAt) and AS-IS never re-runs CanTrigger on a stacked skill; a DIRECT path
                    //      (option play / on-play / declaration) collects-and-executes inline, so it runs here.
                    //      The DECLARED [Main] path gates CanUse(null) at declaration instead
                    //      (TurnStateMachine.cs:1178, CanDeclareAt) and is stacked declarative.
                    //   2. stack stamping — AS-IS PutStackedSkill (AutoProcessing.cs:57-118): kind flags +
                    //      PermanentWhenTriggered/TopCardWhenTriggered snapshot (then immediately un-stack, the
                    //      MultipleSkills.Activate `StackedSkillInfos.Remove` of the executing skill).
                    //   3. register-before-body — AS-IS MultipleSkills.cs:358-362: SetOnProcessCallbuck(() =>
                    //      RegisterUseEffectThisTurn), fired inside Activate_Execute AFTER the optional gate
                    //      (ICardEffect.cs:1116-1126). The DECLARED path registered at declaration already
                    //      (TurnStateMachine.cs:1183-1186 for MaxCountPerTurn < 100). (SkillInfos_used journal =
                    //      design item P6A-USED-JOURNAL.)
                    //   4. execute — AS-IS ActivateEffectProcess (AutoProcessing.cs:1063-1088):
                    //      `CanActivate(hashtable) || IsDeclarative` → Activate_Optional_Effect_Execute.
                    // ============================================================================================
                    var ce = (ICardEffect)activate;
                    if (!windowDispatched && !declarative)
                    {
                        if (ce.IsBackgroundProcess || !ce.CanTrigger(hashtable!))
                        {
                            break;
                        }
                    }

                    AutoProcessing autoProcessing = AutoProcessing.For(context);
                    var skillInfo = new SkillInfo(ce, hashtable!, timing);
                    // Stamp the effect via the 1:1 PutStackedSkill and immediately un-stack
                    // (MultipleSkills.Activate removes the executing skill from StackedSkillInfos).
                    autoProcessing.PutStackedSkill(skillInfo);
                    autoProcessing.StackedSkillInfos.Remove(skillInfo);

                    if (declarative)
                    {
                        // AS-IS declared path (TurnStateMachine.cs:1178-1192): CanUse(null) was the declaration
                        // gate; SetIsDeclarative(true) + register-use BEFORE the run; ActivateEffectProcess then
                        // bypasses its CanActivate with IsDeclarative.
                        ce.SetIsDeclarative(true);
                        if (ce.MaxCountPerTurn < 100)
                        {
                            ce.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(ce);
                        }
                    }
                    else
                    {
                        ce.SetOnProcessCallbuck(() =>
                        {
                            ce.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(ce);
                        });
                    }

                    await autoProcessing.ActivateEffectProcess(ce, hashtable!, isCheckOptional: true).ConfigureAwait(false);
                    resolved++;
                    break;
                }

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
                                context, effectClass, card, players, sink, new[] { branch }, cancellationToken, drivingEvent,
                                hashtable: hashtable, timing: timing).ConfigureAwait(false);
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
                    // (R3-W3c B6) rehomed to the AS-IS-literal live getter (see the CanDeclare pass above).
                    if (!new Permanent(context, burst.Card.InstanceId).ImmuneFromStackTrashing(BareCauseEffect.For(context, burst.Card.InstanceId))
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

                            // (R6-Da'-4 / RD-P6B-6) the Digi-Burst body is either a CONTINUOUS keyword-static grant
                            // ("This gains <keyword>") or an ACTIVATED body (draw/delete/trash). AS-IS a card's
                            // Digi-Burst coroutine registers the continuous grant into the permanent's duration
                            // bucket via CardEffectCommons.AddEffectToPermanent at the KEYWORD's live-read timing
                            // (e.g. Pierce reads OnDetermineDoSecurityCheck — NewModelContinuousScan.HasPierce),
                            // exactly the GainPierce/GainBlocker idiom; an activated body runs its coroutine. The
                            // continuous case carries burst.GrantTiming (!= None), supplied by the card that knows
                            // the keyword's timing. The former blanket `is IActivatedCardEffect or ActivateICardEffect
                            // -> resolve now` MISROUTED a keyword-static ActivateClass (PierceSelfEffect builds an
                            // ActivateClass) into the coroutine path, where its no-op ActivateCoroutine ran and the
                            // grant was lost (RD-P6B-6). The old-model LegacyBindingBridge registry-lowering `else`
                            // branch is deleted with the rest of the invented registry (a NEW-model ActivateClass has
                            // no ToBinding — the branch was inert; census-0 old-model producer).
                            if (burst.GrantTiming != EffectTiming.None)
                            {
                                // Journaled: a resumed replay must not re-add a second bucket delegate (the AS-IS
                                // duration bucket is a LIST). AS-IS 1:1 — the self permanent's None-relative duration
                                // bucket, read live by the keyword gate.
                                RunJournaledImmediate(context, () => CardEffectCommons.AddEffectToPermanent(
                                    targetPermanent: new Permanent(context, burst.Card.InstanceId),
                                    effectDuration: EffectDuration.UntilOwnerTurnEnd, card: burst.Card,
                                    cardEffect: burst.InnerEffect, timing: burst.GrantTiming));
                            }
                            else
                            {
                                resolved += await ResolveListAsync(
                                    context, effectClass, burst.Card, players, sink, new[] { burst.InnerEffect }, cancellationToken,
                                    hashtable: hashtable, timing: timing).ConfigureAwait(false);
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
                                context: context).ConfigureAwait(false);
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
                                optEffect.CardEffects(EffectTiming.OptionSkill, optCard), cancellationToken,
                                // (P6 stage A) the nested option [Main] runs with the AS-IS option-main payload
                                // {Card} (HashtableSetting.cs:264-271).
                                hashtable: CardEffectCommons.OptionMainCheckHashtable(optCard),
                                timing: EffectTiming.OptionSkill).ConfigureAwait(false);
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

                // (R6-Da'-3) `case ActivatedTargetRestrictionEffect` DELETED — the producer was census-0 (no printed
                // card creates one: ST2_14 / ST4_12 / BT1_113 are all re-ported to the inline AS-IS ActivateClass +
                // SelectPermanentEffect(Mode.Custom) driving GainCanNotAttack/GainCanNotBlock per pick, and the
                // CardEffectFactory.SelectAndRestrictEffect helper had no live caller). Class + factory deleted.

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

                // (uniform-사멸 flip) `case ActivatedEffect uniform:` DELETED — the invented uniform kind died
                // consumer-0 (fixtures re-written to the AS-IS inline ActivateClass; the last producer, the
                // Commons afterMainBody carrier, now emits the sequential follow-up effect). Its cap/refund/
                // executed/resume accounting lives on the AS-IS path this switch's ActivateICardEffect case
                // already drives: CEntity_EffectController (register-before-body + IsSameEffect partition)
                // + CEntityUseCycle (staged replay + mutation journal).

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

                // (이연③-A DEAD) `case PlayThisCardToBattleEffect` DELETED — the mirror-invented Tamer
                // [Security] "play this Tamer" carrier was census-0 at HEAD (all producers re-pointed to the
                // AS-IS PlayCardClass factory / ActivateClass flow). Class deleted in ActivatedEffects.cs.

                // (R6-Db D4 EXHAUSTED) `case PlaySelfAtEndOfBattleSecurityEffect` DELETED — the mirror-invented
                // [Security] end-of-battle carrier is retired; the AS-IS UntilEndBattleEffects idiom is landed in
                // CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect (resolved via the ActivateClass flow).

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
                        cancellationToken,
                        hashtable: CardEffectCommons.OptionMainCheckHashtable(card),
                        timing: EffectTiming.OptionSkill).ConfigureAwait(false);
                    break;
                }

                case ReuseWhenDigivolvingEffect:
                {
                    // (EX8-2 brick) "[All Turns] activate this card's [When Digivolving] effects" — resolve the
                    // card's WhenDigivolving activated effects, recursively, through the same sink / choice
                    // provider (same shape as ReuseMainOptionEffect, different timing).
                    resolved += await ResolveListAsync(
                        context, effectClass, card, players, sink,
                        effectClass.CardEffects(EffectTiming.WhenDigivolving, card), cancellationToken,
                        hashtable: CardEffectCommons.WhenDigivolvingCheckHashtableOfCard(card),
                        timing: EffectTiming.WhenDigivolving).ConfigureAwait(false);
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
        // (R6-Da'-6 D1=A) journal moved to CEntityUseCycle (lockstep with the OnceFlags uniform-cycle).
        CEntityUseCycle.MutationReplay replay = CEntityUseCycle.For(context).BeginMutationApply();
        if (replay == CEntityUseCycle.MutationReplay.Skip)
        {
            return;
        }

        action();
        if (replay == CEntityUseCycle.MutationReplay.Fresh)
        {
            CEntityUseCycle.For(context).RecordFreshMutation(purelyImmediate: true);
        }
    }

    /// <summary>Async twin of <see cref="RunJournaledImmediate(EngineContext, Action)"/> for awaited direct
    /// mutations (ZoneMover moves, FuseAsync).</summary>
    private static async Task RunJournaledImmediateAsync(EngineContext context, Func<Task> action)
    {
        // (R6-Da'-6 D1=A) journal moved to CEntityUseCycle (lockstep with the OnceFlags uniform-cycle).
        CEntityUseCycle.MutationReplay replay = CEntityUseCycle.For(context).BeginMutationApply();
        if (replay == CEntityUseCycle.MutationReplay.Skip)
        {
            return;
        }

        await action().ConfigureAwait(false);
        if (replay == CEntityUseCycle.MutationReplay.Fresh)
        {
            CEntityUseCycle.For(context).RecordFreshMutation(purelyImmediate: true);
        }
    }

    // (uniform-사멸 flip) ConfirmOptionalAsync DELETED — the uniform optional prompt; the new-model optional
    // gate is the AS-IS OptionalSkill.SelectOptional (Activate_Optional, ICardEffect.cs).


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
