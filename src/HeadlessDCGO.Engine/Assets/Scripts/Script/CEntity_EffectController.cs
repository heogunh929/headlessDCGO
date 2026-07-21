// Source: DCGO/Assets/Scripts/Script/CEntity_EffectController.cs
// (EFFECT-MODEL REBUILD / FOUNDATION) 1:1 mirror of the original `CEntity_EffectController` — the per-card-
// instance use-count / effect-list layer `ICardEffect.CanTrigger/CanActivate` reach through
// `CardSource.cEntity_EffectController`. `class CEntity_EffectController : MonoBehaviour` -> plain class
// (MonoBehaviour stripped). The reflection-based `AddCardEffect(string ID, string ClassName)` (AS-IS :179-241,
// `gameObject.AddComponent(Type.GetType(...))`) is STRIPPED per the FOUNDATION brief — the mirror's
// `CardEffectDispatch` (CardEffectCommons/CardEffectDispatch.cs) already plays that structural role
// (card-number -> ported `CEntity_Effect` subclass lookup), so there is nothing left to port here; a caller
// that needs a controller's `cEntity_Effect` populated should look up the dispatched type directly and set
// `cEntity_Effect` on the returned controller (no call site exists yet — MISSING.md).
//
// PER-INSTANCE STORE: AS-IS attaches one `CEntity_EffectController` Component per card GameObject (lifetime =
// the GameObject's). The headless `CardSource` is a lightweight VIEW reconstructed on every access (no stable
// per-card object), so a persistent per-(match, card-instance) store is needed for `CardSource.
// cEntity_EffectController` (added by this goal, see CardEffectCommons/CardSource.cs) to keep returning the
// SAME controller for the same card across accesses (so `UseEffectsThisTurn` actually accumulates). Backed here
// by <see cref="CEntity_EffectControllerStore"/>, keyed off the live `EngineContext` (one match) + the card's
// `HeadlessEntityId` — a minimal, additive store, not an `EngineContext` field (keeps this goal's diff to the
// FOUNDATION files only; promoting it onto `EngineContext` alongside `OnceFlags`/`PlayerTurnCounters` is a later-
// phase call, MISSING.md).

namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>AS-IS <c>CEntity_EffectController</c> (CEntity_EffectController.cs:5).</summary>
public class CEntity_EffectController
{
    // AS-IS CEntity_EffectController.cs:8: "Number of skills used this turn (referenced by use limit)".
    List<ICardEffect> UseEffectsThisTurn = new List<ICardEffect>();

    // (RD-C5W-ACTIVATEBODY, substrate) The live match this controller belongs to, set by
    // CEntity_EffectControllerStore.Create. Used only to reach the match-scoped register-before-body transaction
    // (CEntityUseCycle) so the per-turn use list becomes suspend/resume-cycle-aware for the deferred-choice
    // REPLAY-resume of a [Once Per Turn] optional/interactive effect (see CEntityUseCycle). Null when a controller
    // is constructed outside the store — then the register/gate degrade to the plain AS-IS immediate list ops.
    internal EngineContext Context { get; set; }

    #region CEntity_Effect

    // AS-IS CEntity_EffectController.cs:11.
    public CEntity_Effect cEntity_Effect { get; set; }

    #endregion

    #region Get effect list

    // AS-IS CEntity_EffectController.cs:15-28.
    public List<ICardEffect> GetCardEffects_ExceptAddedEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> GetCardEffects = new List<ICardEffect>();

        if (cEntity_Effect != null)
        {
            foreach (ICardEffect cardEffect in cEntity_Effect.GetCardEffects(timing, card))
            {
                GetCardEffects.Add(cardEffect);
            }
        }

        return GetCardEffects;
    }

    // AS-IS CEntity_EffectController.cs:29-168.
    // MISSING.md: Permanent.cardSources (Permanent.cs — the mirror `Permanent` class exposes
    // `DigivolutionCards`, not the AS-IS raw `cardSources` list including the top card itself);
    // Player.SecurityCards; CardSource.EffectList(EffectTiming) / Player.EffectList(EffectTiming) —
    // referenced verbatim throughout, per the FOUNDATION brief.
    public List<ICardEffect> GetCardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> GetCardEffects = new List<ICardEffect>();

        foreach (ICardEffect cardEffect in GetCardEffects_ExceptAddedEffects(timing, card))
        {
            GetCardEffects.Add(cardEffect);
        }

        // ADAPTATION: bridge `PermanentOfThisCard()`'s `PermanentView` to the real mirror `Permanent` — see
        // ICardEffect.ResolvePermanentOfThisCard (ICardEffect.cs, "ADAPTATION (2)") and
        // PERMANENT-PERMANENTVIEW-DUALITY in docs/audit/rebuild_p1_missing.md. `.Contains(card)` below is
        // reference-equality on a freshly-constructed `CardSource` — DESIGN ITEM CARDSOURCE-EQUALITY.
        Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);
        bool isDigivolutionCard = thisPermanent != null && thisPermanent.DigivolutionCards.Contains(card);

        if (!isDigivolutionCard)
        {
            // Effects added by other card effects
            if (timing != EffectTiming.None)
            {
                #region Effects added by other card effects
                foreach (Player player in GManager.instance.turnStateMachine.gameContext.Players_ForTurnPlayer)
                {
                    if (player != null)
                    {
                        #region Effects of permanents in play
                        foreach (Permanent permanent in player.GetFieldPermanents())
                        {
                            if (permanent.TopCard.cEntity_EffectController.cEntity_Effect != null)
                            {
                                foreach (CardSource cardSource in permanent.cardSources)
                                {
                                    if (cardSource != permanent.TopCard)
                                    {
                                        if (!permanent.IsDigimon)
                                        {
                                            continue;
                                        }
                                    }

                                    foreach (ICardEffect cardEffect in cardSource.cEntity_EffectController.cEntity_Effect.GetCardEffects(EffectTiming.None, permanent.TopCard))
                                    {
                                        if (cardEffect is IAddSkillEffect)
                                        {
                                            if (cardEffect.IsInheritedEffect == (cardSource == permanent.TopCard) || cardSource.IsFlipped)
                                            {
                                                continue;
                                            }

                                            if (((IAddSkillEffect)cardEffect).ShouldAddEffect(timing) && cardEffect.CanUse(null))
                                            {
                                                // ADAPTATION: AS-IS `card.CanNotBeAffected(cardEffect)` takes the
                                                // `ICardEffect` itself; the mirror `CardSource.CanNotBeAffected`
                                                // (CardEffectCommons/CardSource.cs:349-358, an EARLIER "MIG5
                                                // goal-5" adaptation, predating this goal) already takes the
                                                // causing effect's SOURCE CARD id instead (`HeadlessEntityId?`) —
                                                // headless `ICardEffect` objects have no stable identity to scan
                                                // immunity bindings by (see this file's DESIGN ITEM
                                                // CARDSOURCE-EQUALITY in ICardEffect.cs). Adapted to that existing
                                                // shape rather than referencing a non-existent overload.
                                                if (!card.CanNotBeAffected(cardEffect))
                                                    GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        #endregion

                        #region Effects from security
                        foreach (CardSource source in player.SecurityCards)
                        {
                            if (source.IsFlipped)
                                continue;

                            foreach (ICardEffect cardEffect in source.EffectList(EffectTiming.None))
                            {
                                if (cardEffect is IAddSkillEffect)
                                {
                                    if (((IAddSkillEffect)cardEffect).ShouldAddEffect(timing) && cardEffect.CanUse(null))
                                    {
                                        GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                                    }
                                }
                            }
                        }
                        #endregion

                        #region Effects added by players
                        foreach (ICardEffect cardEffect in player.EffectList(EffectTiming.None))
                        {
                            if (cardEffect is IAddSkillEffect)
                            {
                                if (((IAddSkillEffect)cardEffect).ShouldAddEffect(timing) && cardEffect.CanUse(null))
                                {
                                    GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                                }
                            }
                        }
                        #endregion
                    }
                }
                #endregion
            }

            // Explore about EffectTiming.None only if added by me
            else
            {
                if (thisPermanent != null)
                {
                    if (thisPermanent.TopCard.cEntity_EffectController.cEntity_Effect != null)
                    {
                        foreach (CardSource cardSource in thisPermanent.cardSources)
                        {
                            foreach (ICardEffect cardEffect in cardSource.cEntity_EffectController.cEntity_Effect.GetCardEffects(EffectTiming.None, thisPermanent.TopCard))
                            {
                                if (cardEffect is IAddSkillEffect)
                                {
                                    if (cardEffect.IsInheritedEffect == (cardSource == thisPermanent.TopCard) || cardSource.IsFlipped)
                                    {
                                        continue;
                                    }

                                    if (cardEffect.CanUse(null))
                                    {
                                        //GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                                    }
                                }
                            }
                        }
                    }
                }

                if (CardEffectCommons.IsExistInSecurity(card))
                {
                    foreach (ICardEffect cardEffect in card.cEntity_EffectController.cEntity_Effect.GetCardEffects(EffectTiming.None, card))
                    {
                        if (cardEffect is IAddSkillEffect)
                        {
                            if (((IAddSkillEffect)cardEffect).ShouldAddEffect(timing) && cardEffect.CanUse(null))
                            {
                                GetCardEffects = ((IAddSkillEffect)cardEffect).GetCardEffect(card, GetCardEffects, timing);
                            }
                        }
                    }
                }
            }
        }

        return GetCardEffects.Filter(cardEfect => cardEfect != null);
    }

    #endregion

    #region Reset the number of uses during that turn

    // AS-IS CEntity_EffectController.cs:172-176.
    public void InitUseCountThisTurn()
    {
        UseEffectsThisTurn = new List<ICardEffect>();
    }

    #endregion

    #region set card effects

    // AS-IS CEntity_EffectController.cs:179-241: `AddCardEffect(string ID, string ClassName)` — reflection-based
    // Unity Component instantiation (`gameObject.AddComponent(Type.GetType(ClassName))`, with a
    // `DCGO.CardEffects.{ID}.{ClassName}` / `DCGO.CardEffects.Tokens.{ClassName}` fallback, and an
    // `EmptyEffectClass` default when no matching type exists). STRIPPED per the FOUNDATION brief: the mirror's
    // `CardEffectDispatch.TryCreateForCard` already performs the structural equivalent (card-number ->
    // `CEntity_Effect` subclass lookup by reflection over the loaded assembly) — no call site assigns
    // `cEntity_Effect` through this controller yet (MISSING.md).

    #endregion

    #region Gets the number of times the effect was used this turn

    // AS-IS CEntity_EffectController.cs:245-258.
    public int GetUseCountThisTurn(ICardEffect cardEffect)
    {
        int useCount = 0;

        foreach (ICardEffect cardEffect1 in UseEffectsThisTurn)
        {
            if (cardEffect.IsSameEffect(cardEffect1))
            {
                useCount++;
            }
        }

        // (RD-C5W-ACTIVATEBODY) add uses REGISTERED-before-body but still STAGED in the open resolution cycle
        // (not yet committed to UseEffectsThisTurn) so the cap view matches AS-IS's single-coroutine mid-body view:
        // while executing, the resolving effect's own gate re-check reads only what it saw the first time (staged
        // uses replayed so far), so the REPLAY-resume of its body is NOT capped-out by its own in-flight use; while
        // suspended, an outside observer reads the staged use as consumed. No cycle open -> extra is 0 (AS-IS).
        if (Context != null)
        {
            useCount += CEntityUseCycle.For(Context).StagedCount(cardEffect);
        }

        return useCount;
    }

    #endregion

    #region Whether the effect has reached the maximum number of times it can be used this turn.

    // AS-IS CEntity_EffectController.cs:262-265.
    public bool isOverMaxCountPerTurn(ICardEffect cardEffect, int MaxCountPerTurn)
    {
        return GetUseCountThisTurn(cardEffect) >= MaxCountPerTurn;
    }

    #endregion

    #region Register as effects used this turn

    // AS-IS CEntity_EffectController.cs:269-272.
    public void RegisterUseEffectThisTurn(ICardEffect cardEffect)
    {
        // (RD-C5W-ACTIVATEBODY) register-before-body: inside an open resolution cycle STAGE the use (committed on
        // cycle completion, kept across a deferred-choice suspend) so a REPLAY-resume neither double-registers nor
        // reads its own in-flight use as capped-out; outside a cycle commit directly (AS-IS immediate list add).
        if (Context != null && CEntityUseCycle.For(Context).TryStage(cardEffect, () => UseEffectsThisTurn.Add(cardEffect)))
        {
            return;
        }

        UseEffectsThisTurn.Add(cardEffect);
    }

    #endregion

    #region Remove a use of the effect this turn

    // AS-IS CEntity_EffectController.cs:276-279.
    public void RemoveUseEffectThisTurn(ICardEffect cardEffect)
    {
        // (RD-C5W-ACTIVATEBODY) RemoveUse (AS-IS card body's `if (!executed) RemoveUse()`): inside the open cycle
        // the paired register is only STAGED, so refund the staged use (net zero commit); outside a cycle remove
        // the committed list entry (AS-IS).
        if (Context != null && CEntityUseCycle.For(Context).TryRefund(cardEffect))
        {
            return;
        }

        UseEffectsThisTurn.Remove(cardEffect);
    }

    #endregion
}

// AS-IS CEntity_EffectController.cs:283-286: `public class EmptyEffectClass : CEntity_Effect { }` — the
// reflection fallback `AddCardEffect` attached when no ported type matched. Ported as the same trivial
// no-effects subclass (still meaningful without the reflection path: any FOUNDATION-era caller that needs an
// explicit "no effects" controller can new this up directly).
public class EmptyEffectClass : CEntity_Effect
{
}

/// <summary>(FOUNDATION, not AS-IS) Per-match, per-card-instance store backing
/// <see cref="CardSource.cEntity_EffectController"/> — see this file's header ("PER-INSTANCE STORE").
/// (P6 stage A) The controller's <see cref="CEntity_EffectController.cEntity_Effect"/> is now POPULATED on
/// creation from <see cref="CardEffectDispatch.TryCreateForCard"/> — the mirror of the AS-IS setup-time
/// reflection attach (AS-IS CEntity_EffectController.cs:179-241 <c>AddCardEffect</c>: every card GameObject
/// carries its effect component from game start, so <c>CardSource.EffectList(timing)</c> enumerates LIVE for
/// any card in any zone — the flip's enumeration model, replacing enter-play binding registration as the
/// availability source). An un-ported card gets no component (the AS-IS <c>EmptyEffectClass</c> fallback is
/// behaviourally the null component: <c>GetCardEffects_ExceptAddedEffects</c> returns empty either way).</summary>
public static class CEntity_EffectControllerStore
{
    private static readonly ConditionalWeakTable<EngineContext, ConcurrentDictionary<HeadlessEntityId, CEntity_EffectController>> ByContext = new();

    public static CEntity_EffectController GetOrCreate(EngineContext context, HeadlessEntityId instanceId)
    {
        ConcurrentDictionary<HeadlessEntityId, CEntity_EffectController> perContext =
            ByContext.GetOrCreateValue(context);
        return perContext.GetOrAdd(instanceId, id => Create(context, id));
    }

    /// <summary>(C2) AS-IS turn-boundary use-count reset (TurnStateMachine.cs:3204-3208: every
    /// <c>gameContext.ActiveCardList</c> card's <c>cEntity_EffectController.InitUseCountThisTurn()</c>). The
    /// mirror walks every controller that EXISTS for this match — a card whose controller was never created has
    /// no recorded uses, so the set is equivalent. Called by the turn advance alongside the legacy OnceFlags
    /// reset; before the C2 flip the new-model per-instance caps were not load-bearing, so this reset was absent.</summary>
    public static void ResetUseCountsForTurn(EngineContext context)
    {
        if (ByContext.TryGetValue(context, out ConcurrentDictionary<HeadlessEntityId, CEntity_EffectController>? perContext))
        {
            foreach (CEntity_EffectController controller in perContext.Values)
            {
                controller.InitUseCountThisTurn();
            }
        }
    }

    private static CEntity_EffectController Create(EngineContext context, HeadlessEntityId instanceId)
    {
        var controller = new CEntity_EffectController();
        // (RD-C5W-ACTIVATEBODY) anchor the controller to its match so the per-turn use list can participate in the
        // match-scoped register-before-body transaction (CEntityUseCycle).
        controller.Context = context;
        if (!instanceId.IsEmpty
            && context.CardInstanceRepository.TryGetInstance(instanceId, out CardInstanceRecord? instance)
            && instance is not null
            && context.CardRepository.TryGetCard(instance.DefinitionId, out CardRecord? def)
            && def is not null
            && CardEffectDispatch.TryCreateForCard(def, out CEntity_Effect? effect)
            && effect is not null)
        {
            controller.cEntity_Effect = effect;
        }
        else
        {
            // AS-IS CEntity_EffectController.AddCardEffect attaches an EmptyEffectClass when NO ported effect
            // type matches the card (vanilla cards) — so every card's controller ALWAYS has a non-null
            // cEntity_Effect. The mirror previously left it null for unmatched cards, which NRE'd the
            // GetCardEffects security/added scan (`card...cEntity_Effect.GetCardEffects`, CEntity_EffectController.cs:203,
            // AS-IS:153 — likewise unguarded because AS-IS is never null here). Restore the AS-IS fallback.
            controller.cEntity_Effect = new EmptyEffectClass();
        }

        return controller;
    }
}

/// <summary>(RD-C5W-ACTIVATEBODY, substrate — not AS-IS) Match-scoped register-before-body TRANSACTION for the
/// AS-IS <see cref="CEntity_EffectController"/> per-turn use list — the ported-card <c>[Once Per Turn]</c> cap path
/// (<c>ICardEffect.CanActivate</c>/<c>CanUse</c> → <c>isOverMaxCountPerTurn</c>). It mirrors the verified
/// <see cref="Headless.Effects.OnceFlagController"/> uniform-cycle (the substrate <c>ActivatedEffect</c> cap path),
/// applied to the EXTERNAL <see cref="CEntity_EffectController.UseEffectsThisTurn"/> store instead of an internal
/// key/count.
///
/// <para>AS-IS is a SINGLE coroutine: <c>CanActivate</c> is checked ONCE at activation start, the use is registered
/// BEFORE the body (register-before-body), and the interactive-select SUSPEND resumes the SAME coroutine mid-body —
/// the cap gate is never re-evaluated. The headless resume model REPLAYS the whole body from the top on each
/// deferred-choice answer (<c>MultipleSkills.RunPickBodyAsync</c> / <c>AutoProcessing.ActivateEffectProcess</c>),
/// which re-runs <c>CanActivate</c>; without this transaction the already-registered use makes the re-check read
/// <c>isOverMaxCountPerTurn = true</c> and the body is wrongly skipped (RD-C5W-ACTIVATEBODY).</para>
///
/// <para>The transaction is driven by the ONE resolution lifecycle seam,
/// <c>ActivatedEffectResolver.ResolveWithinCycleAsync</c>, alongside the <c>OnceFlags</c> calls:
/// <list type="bullet">
/// <item>a <c>Register</c> during an open, actively-executing cycle STAGES the use (per-cycle <c>_pending</c>) with
///   a deferred commit thunk, instead of committing to the list;</item>
/// <item>a re-invocation (<see cref="Begin"/> resume) resets the positional <c>_cursor</c>; a <c>Register</c> whose
///   cursor lags the staged count is a REPLAY of an earlier register (cursor advances, nothing staged);</item>
/// <item><see cref="StagedCount"/> reads staged-up-to-cursor while executing (the gate sees exactly what it saw the
///   first time) and the full staged set while SUSPENDED (outside observers see the cap consumed — AS-IS mid-body
///   view);</item>
/// <item>the owning invocation COMMITS every staged use on <see cref="Complete"/> (before the sink flush, so windows
///   opened by the flushed events read committed caps) or keeps them staged across a <see cref="Suspend"/>; a
///   non-choice failure <see cref="Abort"/>s the stage (nothing consumed).</list></para></summary>
public sealed class CEntityUseCycle
{
    private static readonly ConditionalWeakTable<EngineContext, CEntityUseCycle> ByContext = new();

    public static CEntityUseCycle For(EngineContext context) => ByContext.GetValue(context, _ => new CEntityUseCycle());

    private readonly List<StagedUse> _pending = new();
    private int _cursor;
    private bool _cycleOpen;
    private bool _cycleSuspended;
    private bool _invocationActive;

    // (R6-Da'-6, D1=A) The MUTATION replay journal for the SAME cycle — moved here from
    // <c>OnceFlagController</c> so the model-independent shared substrate (consumed by EVERY resolution path,
    // uniform and new-model alike) lives on the ONE surviving cycle instead of the retiring uniform-cap holder.
    // Behaviour is identical because this cycle is driven in EXACT lockstep with the OnceFlags uniform-cycle
    // (ActivatedEffectResolver.ResolveWithinCycleAsync :508↔:513 / :522↔:523 / :531↔:532 / :538↔:539), so the
    // <c>_cycleOpen</c>/<c>_invocationActive</c> flags the journal gates on are the same at every point.
    //
    // The sink applies most non-zone-move mutations IMMEDIATELY (metadata flags, memory, DP) and defers zone moves
    // to FlushAsync — so across a suspend the deferred half is discarded-and-restaged by the replay (correct), but
    // the immediate half is ALREADY in game state and a naive replay re-applies it (double memory / double DP /
    // double timing events). The journal records, per Apply call of the original run, whether the call was PURELY
    // IMMEDIATE (true → the replay SKIPS the whole call: its effects persist in state) or staged pending work
    // (false → the replay re-executes it so the fresh sink re-stages the discarded thunks; any immediate
    // side-effects of such a MIXED call still replay — a known residual, status quo ante). Fresh calls beyond the
    // journal execute and record. Journal entries are deterministic across replays because the answer replay is.
    private readonly List<bool> _mutationJournal = new();
    private int _mutationCursor;

    private sealed class StagedUse
    {
        public StagedUse(ICardEffect effect, Action commit)
        {
            Effect = effect;
            Commit = commit;
        }

        public ICardEffect Effect { get; }

        public Action Commit { get; }
    }

    /// <summary>Sink-side replay decision for one <c>Apply</c> call (see the mutation-journal remarks). Moved from
    /// <c>OnceFlagController.MutationReplay</c> (R6-Da'-6 D1=A); identical values.</summary>
    public enum MutationReplay
    {
        /// <summary>No open cycle — apply normally, no journaling.</summary>
        None,

        /// <summary>Replaying a purely-immediate call — SKIP it (its effects already persist in game state).</summary>
        Skip,

        /// <summary>Replaying a call that staged pending work — re-execute it (the fresh sink must re-stage).</summary>
        Execute,

        /// <summary>Beyond the journal — execute and report back via <see cref="RecordFreshMutation"/>.</summary>
        Fresh,
    }

    /// <summary>Open (or resume) the transaction. Returns <c>true</c> when this invocation OWNS the cycle — a fresh
    /// open, or the re-invocation resuming a suspended cycle (the positional cursor is reset so staged registers
    /// replay in order); <c>false</c> for a nested resolution inside an already-executing cycle. Mirrors
    /// <c>OnceFlagController.BeginUniformCycle</c>.</summary>
    public bool Begin()
    {
        if (!_cycleOpen)
        {
            _cycleOpen = true;
            _cycleSuspended = false;
            _pending.Clear();
            _cursor = 0;
            _invocationActive = true;
            return true;
        }

        if (_cycleSuspended)
        {
            _cycleSuspended = false;
            _cursor = 0;
            _mutationCursor = 0;
            _invocationActive = true;
            return true;
        }

        return false;
    }

    /// <summary>Consulted by the sink at the top of each <c>Apply</c> call while a cycle is executing (moved from
    /// <c>OnceFlagController.BeginMutationApply</c>, R6-Da'-6 D1=A). Gates on the SAME cycle open/active state the
    /// uniform cap transaction does — lockstep-identical by construction.</summary>
    public MutationReplay BeginMutationApply()
    {
        if (!_cycleOpen || !_invocationActive)
        {
            return MutationReplay.None;
        }

        if (_mutationCursor < _mutationJournal.Count)
        {
            bool purelyImmediate = _mutationJournal[_mutationCursor];
            _mutationCursor++;
            return purelyImmediate ? MutationReplay.Skip : MutationReplay.Execute;
        }

        return MutationReplay.Fresh;
    }

    /// <summary>Record a FRESH <c>Apply</c> call's classification (<paramref name="purelyImmediate"/> = it staged
    /// no pending thunk, so a replay can skip it wholesale). Moved from <c>OnceFlagController.RecordFreshMutation</c>.</summary>
    public void RecordFreshMutation(bool purelyImmediate)
    {
        if (!_cycleOpen || !_invocationActive)
        {
            return;
        }

        _mutationJournal.Add(purelyImmediate);
        _mutationCursor = _mutationJournal.Count;
    }

    /// <summary>Mark the owning invocation suspended (an agent choice is pending). Staged registers stay staged;
    /// outside observers now read them as consumed (<see cref="StagedCount"/>'s suspended view).</summary>
    public void Suspend(bool owner)
    {
        if (!owner)
        {
            return;
        }

        _cycleSuspended = true;
        _invocationActive = false;
    }

    /// <summary>Commit every staged register to the real per-turn use lists and close the transaction. Call before
    /// the sink flush so any window the flushed events open reads committed caps.</summary>
    public void Complete(bool owner)
    {
        if (!owner)
        {
            return;
        }

        foreach (StagedUse staged in _pending)
        {
            staged.Commit();
        }

        Close();
    }

    /// <summary>Abandon the transaction WITHOUT committing (a non-choice failure aborted the resolution — nothing
    /// was applied, so nothing is consumed).</summary>
    public void Abort(bool owner)
    {
        if (!owner)
        {
            return;
        }

        Close();
    }

    private void Close()
    {
        _pending.Clear();
        _cursor = 0;
        _mutationJournal.Clear();
        _mutationCursor = 0;
        _cycleOpen = false;
        _cycleSuspended = false;
        _invocationActive = false;
    }

    /// <summary>Register-before-body. Returns <c>true</c> when the use was STAGED (the caller must NOT commit to its
    /// list); <c>false</c> when there is no active cycle and the caller should commit immediately (AS-IS).</summary>
    public bool TryStage(ICardEffect effect, Action commit)
    {
        if (!_cycleOpen || !_invocationActive)
        {
            return false;
        }

        if (_cursor < _pending.Count)
        {
            // Replay of a register the suspended run already staged — advance the cursor, stage nothing.
            _cursor++;
            return true;
        }

        _pending.Add(new StagedUse(effect, commit));
        _cursor++;
        return true;
    }

    /// <summary>Staged uses of the same effect (AS-IS <c>IsSameEffect</c>) this open cycle to count ON TOP of the
    /// committed list: the cursor-limited prefix while executing, the full staged set while suspended / for outside
    /// observers.</summary>
    public int StagedCount(ICardEffect effect)
    {
        if (!_cycleOpen)
        {
            return 0;
        }

        int limit = _invocationActive ? _cursor : _pending.Count;
        int count = 0;
        for (int i = 0; i < limit; i++)
        {
            if (_pending[i].Effect.IsSameEffect(effect))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Refund the most-recent staged register of the same effect (AS-IS <c>RemoveUse</c> paired with a
    /// staged register). Returns <c>true</c> when a staged use was removed; <c>false</c> when nothing is staged and
    /// the caller should remove a committed list entry.</summary>
    public bool TryRefund(ICardEffect effect)
    {
        if (!_cycleOpen || !_invocationActive)
        {
            return false;
        }

        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (_pending[i].Effect.IsSameEffect(effect))
            {
                _pending.RemoveAt(i);
                if (_cursor > _pending.Count)
                {
                    _cursor = _pending.Count;
                }

                return true;
            }
        }

        return false;
    }
}
