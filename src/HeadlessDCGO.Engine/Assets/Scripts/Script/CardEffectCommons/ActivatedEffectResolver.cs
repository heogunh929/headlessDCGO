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

    /// <summary>(P6 stage A; R7 종점) Whether the effect is ACTIVATED — the AS-IS contract
    /// <see cref="ActivateICardEffect"/> (the new-model ActivateClass/kind classes; the AS-IS collection filter
    /// <c>is ActivateICardEffect</c>, AutoProcessing.cs:780/799/…). The legacy <c>IActivatedCardEffect</c> marker
    /// retired with the old-model activated corpus (R7 종점 소진), so this reduces to the single new-model test.</summary>
    private static bool IsActivated(ICardEffect effect) => effect is ActivateICardEffect;

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

    /// <summary>(A-2 / RD-6) Whether the card has any ACTIVATED effect (<see cref="ActivateICardEffect"/>)
    /// registered at <paramref name="timing"/> — the resolver's actual domain (the <see cref="ResolveListAsync"/>
    /// switch is the single ActivateICardEffect case; a plain scheduler <see cref="IHeadlessCardEffect"/>
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

        // Effect invalidation is the AS-IS per-effect gate (ICardEffect.IsDisabled → CheckEffectDisabledClass,
        // consulted by CanTrigger) — the invented card-level EffectInvalidation registry check that used to sit
        // here is retired (RC-3: producer 0, corpus 0; the AS-IS gate below covers it via each effect's CanUse).
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
            // (이연③-h EXHAUSTED) the `else if (effects[i] is DigiBurstActivatedEffect burst)` declare-gate special
            // case DELETED with the invented carrier — a [Main] Digi-Burst is now a plain ActivateClass whose
            // CanUseCondition is `new IDigiBurst(...).CanDigiBurst()` (ST4_13 idiom), so the generic ActivateICardEffect
            // branch above (CanUse(null) -> CanTrigger -> CanUseCondition -> CanDigiBurst) already gates it exactly.
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
        bool inheritedScan = false,
        Action<ICardEffect>? effectStamp = null)
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
            context.CardInstanceRepository, context.LogSink, context.ZoneMover, context.MemoryController, context.GameEventQueue,
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

        // (RD-W3-5) AS-IS ActivateMainOfOptionSide stamps the RESOLVED [Main] ActivateClass instance
        // (SetIsDigimonEffect/SetIsTamerEffect) BEFORE Activate — the resolver enumerates the card's OWN effect
        // instances (CardSource.EffectList), so a caller can stamp those very instances that ResolveListAsync then
        // activates. Faithful substrate translation of the AS-IS `mainActivateClass.SetIs*(...)` lines.
        if (effectStamp is not null)
        {
            foreach (ICardEffect e in effects)
            {
                effectStamp(e);
            }
        }

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

                // (R7 종점) `case ModeChoiceEffect mode:` DELETED with the invented carrier — a "choose one mode"
                // menu is now the AS-IS inline `new ActivateClass()` whose ActivateCoroutine itself presents the
                // ChoiceType.ModeChoice request and runs the chosen branch's ActivateClass (TfxSelectMode idiom),
                // driven by the ActivateICardEffect case above.

                // (이연③-h EXHAUSTED) `case DigiBurstActivatedEffect burst:` DELETED with the invented carrier — a
                // Digi-Burst is now the literal AS-IS inline `new IDigiBurst(permanent, N, activateClass)` (ST4_13
                // idiom) wrapped in a plain ActivateClass. The full AS-IS sequence (CanDigiBurst gate,
                // controller-selected sources, OnUseDigiburst window emit + journaling, ITrashDigivolutionCards
                // trash) lives in the IDigiBurst class (Script/CardController.cs region "Digi-Burst"); the inner
                // body (draw / keyword-grant AddEffectToPermanent at the keyword's live-read timing) runs in the
                // card's ActivateCoroutine after the pay. The ActivateICardEffect case above drives it.

                // (R7 종점) `case DnaFromHandOrTrashActivatedEffect dna:` DELETED with the invented carrier — the
                // effect-driven DNA digivolution (auto-match into-card/permanent/material then FusionDigivolveHelpers.
                // FuseAsync) is now the AS-IS inline `new ActivateClass()` coroutine (TfxDnaFromHand idiom), driven
                // by the ActivateICardEffect case above.

                // (R7 종점) `case PlayOptionCardEffect playOption:` DELETED with the invented carrier — the
                // effect-driven option play is now the AS-IS inline `new ActivateClass()` that selects the
                // Option(s) then drives the LIVE `CardEffectCommons.PlayOptionCards` bridge (trash → OnUseOption
                // window → resolve [Main]); driven by the ActivateICardEffect case above.

                // (이연③-e EXHAUSTED) invented `RevealSelectThenPlaySelectedEffect` case DELETED — BT1_078 is
                // re-pointed to the literal AS-IS inline ActivateClass (coroutine-callable commons
                // `SimplifiedRevealDeckTopCardsAndSelect` + `new PlayCardClass(...).PlayCard()` digivolve).

                // (R7 종점) `case ActivatedDrawThenDiscardEffect drawDiscard:` DELETED with the invented carrier —
                // draw-N-then-discard-M is the LIVE `CardEffectCommons.DrawAndDiscardCards` coroutine inline in an
                // ActivateClass (BT3_006 / BT3_088 idiom), driven by the ActivateICardEffect case above.

                // (R7 종점) The G10/G12/G13 primitive cases DELETED with their invented carriers
                // (`ChooseCountThenTrashDigivolutionEffect`, `OpponentBinaryChoiceEffect`,
                // `SelectDeDigivolveThenConditionalDestroyEffect`, `MassDeDigivolveThenConditionalDestroyEffect`,
                // `SelectHandAttachToOwnStackThenMemoryEffect`) — the printed cards BT3_019 / BT3_100 / BT3_102 /
                // BT3_107 / BT3_112 are ported and drive the live substrate directly (DeDigivolveHelpers /
                // DigivolutionStackHelpers / DestroyPermanent / the Trash & sink mutations); these bespoke arms
                // were unreachable (never dispatched — the retired tests drove the carriers white-box).

                // (이연③-d EXHAUSTED) `case SelectDigivolutionSourceToHandThenSelfFollowUpEffect` DELETED — the
                // invented select-source-to-hand-then-self carrier is retired. BT1_084 [When Attacking] drives the
                // AS-IS inline SelectCardEffect(AddHand, Custom root) + IUnsuspendPermanents, run by the
                // ActivateICardEffect case.

                // (이연③-d EXHAUSTED) `case SecuritySelectToHandColorRecoveryShuffleEffect` DELETED — the invented
                // security-select-recovery-shuffle carrier is retired. BT1_087 [On Play] drives the AS-IS inline
                // SelectCardEffect(AddHand, Security) + IRecovery + ShuffleSecurityAsync, run by the
                // ActivateICardEffect case.

                // (R6-Da'-3) `case ActivatedTargetRestrictionEffect` DELETED — the producer was census-0 (no printed
                // card creates one: ST2_14 / ST4_12 / BT1_113 are all re-ported to the inline AS-IS ActivateClass +
                // SelectPermanentEffect(Mode.Custom) driving GainCanNotAttack/GainCanNotBlock per pick, and the
                // CardEffectFactory.SelectAndRestrictEffect helper had no live caller). Class + factory deleted.

                // (이연③-d EXHAUSTED) `case ActivatedMemoryEffect` DELETED — the invented direct-memory carrier is
                // retired (census-0 producer: BT2_087 / ST2_13 re-ported to inline `new ActivateClass()` +
                // `card.Owner.AddMemory(N, activateClass)`, the AS-IS live memory path). Class removed.

                // (이연③-d EXHAUSTED) `case AddThisCardToHandEffect` DELETED — the invented add-to-hand composite is
                // retired. Its one live producer (ST4_15 [Security] afterMainEffect) now runs the AS-IS
                // `AddThisCardToHand` coroutine through ReuseMainOptionEffect.AfterMainEffect (below); every other
                // [Security] add-to-hand (ST3_13 / BT9_109) already calls the live coroutine inline.

                // (uniform-사멸 flip) `case ActivatedEffect uniform:` DELETED — the invented uniform kind died
                // consumer-0 (fixtures re-written to the AS-IS inline ActivateClass; the last producer, the
                // Commons afterMainBody carrier, now emits the sequential follow-up effect). Its cap/refund/
                // executed/resume accounting lives on the AS-IS path this switch's ActivateICardEffect case
                // already drives: CEntity_EffectController (register-before-body + IsSameEffect partition)
                // + CEntityUseCycle (staged replay + mutation journal).

                // (이연③-h EXHAUSTED) `case DrawEffect draw:` DELETED with the invented declarative stub — a draw is
                // the AS-IS `new DrawClass(...).Draw()` coroutine (Script/CardController.cs) inline in the card/fixture
                // ActivateClass body, run by the ActivateICardEffect case above (BT1_046 idiom).

                // (이연③-f EXHAUSTED) `case SimplifiedRevealAndSelectEffect` DELETED — the invented declarative
                // reveal-select carrier is retired (census-0). The AS-IS reveal-select is the coroutine-callable
                // commons `CardEffectCommons.SimplifiedRevealDeckTopCardsAndSelect` / `RevealDeckTopCardsAndProcessForAll`
                // (RevealLibrary.cs) driven inline through an ActivateClass (BT2_044 / ST4_03 / ST4_10), run by the
                // ActivateICardEffect case above. Witnesses G9-016 / G9-029 drive the commons directly (fixtures
                // TfxRevealSelect / TfxSelectCardCond retired), the established commons-witness pattern.

                // (이연③-b RETIRED) `case ArtsDigivolveSelfEffect` DELETED — the orphaned invented Arts-Digivolve
                // self duplicate is retired. The live surface is CardEffectFactory.ArtsDigivolveEffect →
                // OptionResolutionClass → PlayCardClass (RD-P6C2-10 resolved; real cards BT9_109/BT25_104/092/089),
                // the cost-free digivolve rule covered by G3.5-D6.FreeDigivolve. Class removed.

                // (R7 종점) `case SelectAndDigivolveEffect selectDigivolve:` DELETED with the invented carrier —
                // AS-IS DigivolveIntoHandOrTrashCard (select target + source, pay cost, fold) is now the inline
                // `new ActivateClass()` coroutine driving DigivolveAction directly (TfxSelectDigivolve idiom),
                // driven by the ActivateICardEffect case above.

                // (이연③-f EXHAUSTED) `case RevealMultiSelectEffect` DELETED — the invented FULL multi-condition
                // reveal carrier is retired (census-0; BT10_096/097 / ST17_11 are unported skeletons). The AS-IS
                // multi-pass reveal is the commons `CardEffectCommons.RevealDeckTopCardsAndSelect` (RevealLibrary.cs,
                // shared-pool passes + Custom via per-card selectCardCoroutine); G9-029 FullMultiCondition re-pointed.

                // (이연③-b RE-TARGETED) `case DestroyPermanentsEffect` DELETED — TfxDestroy drives the AS-IS
                // DeleteKind sink path (NewSink + CardEffectCommons.DestroyPermanent per target + FlushAsync, the
                // centralised immunity gate filtering) through an inline ActivateClass. Class removed.

                // (이연③-d EXHAUSTED) `case TrashSelfThenGainMemoryDelayEffect` DELETED — the invented [Main] <Delay>
                // trash-self-then-gain carrier is retired. AS-IS Gain2MemoryOptionDelayEffect is now an inline
                // ActivateClass (LM_047 OnDeclaration), run by the ActivateICardEffect case.

                // (이연③-d EXHAUSTED) `case DeckBottomBounceEffect` DELETED — the invented deck-bottom-bounce carrier
                // is retired. The AS-IS bounce is the ReturnToDeckBottomKind sink mutation
                // (CardEffectCommons.ReturnToDeckBottom), driven inline by AD1_025's shared OP/WD arm.

                // (이연③-b RETIRED) `case LinkSelfEffect` DELETED — the orphaned invented <Link> self-play
                // duplicate is retired. The live <Link> surface is CardEffectFactory.LinkEffect → ActivateClass
                // → ILinkCard.LinkCard() (RD-P6C2-7 resolved), covered by G9-031.LinkSecurity. Class removed.

                // (이연③-b RE-TARGETED) `case HatchDigiEggEffect` DELETED — TfxHatch drives the AS-IS hatch
                // (empty-breeding + available-egg guard → ZoneMover.HatchDigitamaAsync, BT1_089 idiom) through an
                // inline ActivateClass, resolved by the ActivateICardEffect case above. Class removed.

                // (이연③-b RE-TARGETED) `case PlayCardEffect` DELETED — TfxPlayCard drives the AS-IS PlayCardKind
                // sink path (NewSink + PlayCardKind → ApplyPlayCard → PlayCardClass.PlayCard() + FlushAsync)
                // through an inline ActivateClass. Class removed.

                // (이연③-A DEAD) `case PlayThisCardToBattleEffect` DELETED — the mirror-invented Tamer
                // [Security] "play this Tamer" carrier was census-0 at HEAD (all producers re-pointed to the
                // AS-IS PlayCardClass factory / ActivateClass flow). Class deleted in ActivatedEffects.cs.

                // (R6-Db D4 EXHAUSTED) `case PlaySelfAtEndOfBattleSecurityEffect` DELETED — the mirror-invented
                // [Security] end-of-battle carrier is retired; the AS-IS UntilEndBattleEffects idiom is landed in
                // CardEffectFactory.PlaySelfDigimonAfterBattleSecurityEffect (resolved via the ActivateClass flow).

                // (이연③-d EXHAUSTED) `case BeforePayCostReductionEffect` DELETED — the invented before-pay reducer
                // is retired. The AS-IS BeforePayCost ActivateClass (registers a self ChangeCostClass into
                // UntilCalculateFixedCostEffect) is now inline (TfxBeforePayCostReduction), run by the
                // ActivateICardEffect case during the BeforePayCost window.

                // (이연③-g EXHAUSTED) `case ReuseMainOptionEffect` DELETED — the invented "[Security] reuse this
                // card's [Main]" carrier is retired (census-0). The commons factory
                // `AddActivateMainOptionSecurityEffect` now emits the AS-IS `CardEffectFactory.
                // ActivateMainOptionSecurityEffect` ActivateClass (SetIsSecurityEffect + CanTriggerSecurityEffect
                // gate + a coroutine that runs the reused [Main] via `mainActivateClass.Activate(
                // OptionMainCheckHashtable(card))` then the afterMainEffect callback — AS-IS CardEffectFactory.cs:551
                // verbatim), resolved by the ActivateICardEffect case above. The reused [Main] ActivateClass is the
                // same `OptionMainEffect(card)` object this case formerly re-ran through ResolveListAsync; the
                // afterMain follow-up (ST4_15) now runs inside that ActivateClass's own coroutine.

                // (이연③-b RETIRED) `case ReuseWhenDigivolvingEffect` DELETED — the test-only "[All Turns]
                // re-activate [When Digivolving]" marker is retired. The AS-IS delivery (EX8_074 region #6:
                // OnEnterFieldAnyone + the play-window StackSkillInfos broadcast) is live and covered by
                // G9-012.LiveAllTurnsReactivation. Class removed in ActivatedEffects.cs.

                // DeferredCardEffect / non-activated effects: not resolved here.
            }
        }

        return resolved;
    }

    // (R7 종점) EmitJournaled / RunJournaledImmediate / RunJournaledImmediateAsync DELETED — their only callers
    // were the retired bespoke resolver arms (Dna auto-match fuse, PlayOptionCard trash+emit). The surviving
    // ActivateICardEffect path journals its own direct mutations through CEntityUseCycle within the ActivateClass
    // coroutines; no resolver-owned immediate-mutation journal remains.

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

    // (R7 종점) FirstMatch DELETED — its only caller was the retired DnaFromHandOrTrashActivatedEffect resolver
    // arm (the auto-match now lives inline in the TfxDnaFromHand ActivateClass coroutine).
}
