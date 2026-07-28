// Source: Assets/Scripts/Script/AttackProcess.cs — 1:1 substrate mirror (migration goal 1, 2026-07-12).
//
// This file is the AS-IS AttackProcess (628 lines) translated onto the headless substrate with the SAME class /
// state / methods / control flow. Substrate translation only (docs/audit/asis_mirror_migration_design_2026-07-12.md §3):
//   * coroutines -> async Task; the driving loop (AS-IS TurnStateMachine `while (ActiveAttack()) ProcessNextState()`
//     + AutoProcessCheck) is GameFlowProcessor.RunToStableAsync, which calls ProcessNextState once per iteration and
//     drains the emitted trigger windows between calls — so a `yield return StackSkillInfos(...)` followed by more
//     code becomes "emit, PARK (return), resume on the next ProcessNextState entry" (the coroutine suspension made
//     explicit). Suspension points inside one AS-IS stage are sub-parks of that stage.
//   * GManager.instance.* -> EngineContext services; `turnStateMachine.IsSelecting = true` lines are dropped (the
//     pause semantic is a pending choice: GameFlowProcessor pauses while a choice is pending).
//   * UI / target arrows / outlines / play-log / Photon are silently stripped (UnityNullObjectPolicy).
//   * AS-IS state lives on THIS instance (one per EngineContext, like GManager.attackProcess); the shared fields
//     project onto IHeadlessAttackController (the observation/action substrate view) so the RL/action layers are
//     unchanged. Fields with no controller counterpart (IsEndAttack / DoSecurityCheck / SecurityDigimon /
//     CounterSourcesSnapshot / counter-pass progress) are instance fields — exactly the AS-IS shape.
//
// AS-IS state <-> substrate view:
//   AttackState.None     <-> AttackPhase.None
//   AttackState.Counter  <-> AttackPhase.Declared   (counter passes pending; pass progress = instance fields)
//   AttackState.Block    <-> AttackPhase.Blocking   (block choice pending = the BlockTiming() coroutine suspension)
//   AttackState.Battle   <-> AttackPhase.Combat | DeletionReplacement | PiercingSecurity (the DetermineAttackOutcome()
//                            coroutine suspensions: would-be-deleted PRE window / battle-trigger drain, F-6.8 / B2)
//   AttackState.End      <-> AttackPhase.Resolved
//   AttackState.CleanUp  <-> AttackPhase.Completed
//
// Fidelity corrections carried by this mirror (divergences the freeform Headless rewrite had introduced):
//   * IsEndAttack force-end surface restored — public EndAttack() + the AS-IS boundary checks at every stage
//     (AttackProcess.cs:106,221,258,277,301,325,386,410). Cards like BT25_103/EX7_052 call attackProcess.EndAttack().
//   * SwitchDefender() restored as the effect-facing FULL sequence (AS-IS :514-626): guards -> retarget -> block
//     suspend + OnBlockAnyone -> death checks -> OnAttackTargetChanged emitted HERE (centralized — resolves design
//     item F1-ATC-EMIT-CENTRALIZE; card-driven redirects (~30+ cards) get the whole sequence for free).
//   * UntilEndAttack expiry moved to Cleanup() (AS-IS :489-496) — it previously ran BEFORE the OnEndAttack window
//     resolved, so [End of Attack] reactors could not see UntilEndAttack buffs.
//   * CounterEffectHashtable snapshot semantics (AS-IS :99 — the counter gate hashtable is built over a SNAPSHOT of
//     the attacker's cardSources at declaration, not the live stack).
//   * Counter/Block/Battle boundary guards include `TopCard == null || !IsDigimon` (AS-IS :301,325,386,410).
//
// Known relocations kept AT THE SAME POSITION for now (each a design item, not silently absorbed):
//   * Raid / Alliance / Progress run at the head of the counter stage — AS-IS implements them as KEYWORD effects
//     (CardEffectCommons.RaidProcess/AllianceProcess), not AttackProcess logic. Relocation to the keyword-effect
//     mirrors is design item MIG1-KEYWORD-RELOCATE.
//   * Execute's end-of-attack self-delete runs at the tail of the End stage — AS-IS is an UntilEndAttack
//     DeleteSelfEffect. Relocation is design item MIG1-EXECUTE-RELOCATE.
//   * The contract-level legacy counter emit (no live attacker record) is a substrate-only branch.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Globalization;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Inside this namespace the identifier `CardEffectCommons` binds to the CHILD NAMESPACE first — alias the static
// commons class (the AS-IS CardEffectCommons mirror) explicitly.
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;

public sealed class AttackProcess
{
    private readonly EngineContext _context;

    public AttackProcess(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <summary>The per-context instance (AS-IS <c>GManager.instance.attackProcess</c>).</summary>
    public static AttackProcess For(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGetService(out AttackProcess? existing) && existing is not null)
        {
            return existing;
        }

        var created = new AttackProcess(context);
        context.RegisterService(created);
        return created;
    }

    // ===== AS-IS fields (AttackProcess.cs:11-24) ==================================================================
    // Shared fields project onto the attack-controller substrate view (write-through) so observation / legal-action
    // layers keep reading HeadlessAttackState unchanged.

    /// <summary>AS-IS <c>AttackingPermanent</c> (:11) — a live view over the controller's AttackerId.</summary>
    public Permanent? AttackingPermanent =>
        _context.AttackController.Current is { AttackerId: HeadlessEntityId id, AttackingPlayerId: HeadlessPlayerId owner }
            ? new Permanent(_context, id, owner)
            : null;

    /// <summary>AS-IS <c>DefendingPermanent</c> (:12) — a live view over the controller's TargetId.</summary>
    public Permanent? DefendingPermanent =>
        _context.AttackController.Current is { TargetId: HeadlessEntityId id, DefendingPlayerId: HeadlessPlayerId owner }
            ? new Permanent(_context, id, owner)
            : null;

    /// <summary>AS-IS <c>AttackCount</c> (:13) — the controller's attack counter.</summary>
    public int AttackCount => _context.AttackController.Current.AttackCount;

    /// <summary>AS-IS <c>IsAttacking</c> (:14).</summary>
    public bool IsAttacking => _context.AttackController.Current.IsPending;

    /// <summary>AS-IS <c>HasDefender</c> (:15).</summary>
    public bool HasDefender => _context.AttackController.Current.TargetId.HasValue;

    /// <summary>AS-IS <c>IsBlocking</c> (:16).</summary>
    public bool IsBlocking => _context.AttackController.Current.IsBlocked;

    /// <summary>AS-IS <c>SecurityDigimon</c> (:17) — the security Digimon currently battling (set by the security
    /// check; UI-facing in AS-IS, kept as state for effect gates).</summary>
    public HeadlessEntityId? SecurityDigimon { get; set; }

    /// <summary>AS-IS <c>DoSecurityCheck</c> (:18).</summary>
    public bool DoSecurityCheck { get; set; }

    /// <summary>AS-IS <c>IsEndAttack</c> (:19) — the force-end flag. Effects set it via <see cref="EndAttack"/>;
    /// every stage boundary checks it (AS-IS :106,221,258,277,301,325,386,410).</summary>
    public bool IsEndAttack { get; set; }

    /// <summary>AS-IS <c>CounterEffectHashtable</c> (:23,:99) — the counter-timing gate payload is built over a
    /// SNAPSHOT of the attacker's cardSources at declaration (`new Permanent(AttackingPermanent.cardSources)`), so
    /// counter gates match the declaration-time stack even if sources change mid-attack. Threaded into the counter
    /// emits as event metadata.</summary>
    public IReadOnlyList<HeadlessEntityId>? CounterSourcesSnapshot { get; private set; }

    /// <summary>AS-IS <c>State</c> (:24) — projected from the controller phase (header mapping).</summary>
    public AttackState State => _context.AttackController.Current.Phase switch
    {
        AttackPhase.None => AttackState.None,
        AttackPhase.Declared => AttackState.Counter,
        AttackPhase.Blocking => AttackState.Block,
        AttackPhase.Combat => AttackState.Battle,
        AttackPhase.Resolved => AttackState.End,
        AttackPhase.Completed => AttackState.CleanUp,
        _ => AttackState.None,
    };

    /// <summary>AS-IS <c>AttackState</c> (:25-33).</summary>
    public enum AttackState
    {
        None,
        Counter,
        Block,
        Battle,
        End,
        CleanUp,
    }


    /// <summary>AS-IS <c>ActiveAttack()</c> (:35-38).</summary>
    public bool ActiveAttack() => State != AttackState.None;

    // ===== AS-IS ProcessNextState (:40-62) ========================================================================
    /// <summary>AS-IS <c>ProcessNextState()</c> (:40-62) 1:1 — one state-machine step: dispatch to the current
    /// stage. AS-IS returns void; the pump loop (<c>while (ActiveAttack()) { ProcessNextState(); AutoProcessCheck();
    /// }</c>) re-reads <see cref="State"/> after each step. Battle / security / block resolve INLINE inside the
    /// stages (IBattle / ISecurityCheck / SelectPermanentEffect), suspending at choices on the pump gate — the
    /// park/resume AttackAdvanceResult machine is retired.</summary>
    public async Task ProcessNextState(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (State)
        {
            case AttackState.Counter:
                await CounterTiming(cancellationToken).ConfigureAwait(false);
                break;
            case AttackState.Block:
                await BlockTiming(cancellationToken).ConfigureAwait(false);
                break;
            case AttackState.Battle:
                await DetermineAttackOutcome(cancellationToken).ConfigureAwait(false);
                break;
            case AttackState.End:
                await EndAttackStage(cancellationToken).ConfigureAwait(false);
                break;
            case AttackState.CleanUp:
                Cleanup();
                break;
            default:
                break;
        }
    }

    // ===== AS-IS Attack (:73-253) — declaration ===================================================================
    /// <summary>AS-IS <c>Attack(attackingPermanent, defendingPermanent, attackEffect, withoutTap,
    /// beforeOnAttackCoroutine)</c>. Called by the attack actions (player / effect-driven) after their legality
    /// validation, with the controller's DeclareAttack as the state-substrate write (AS-IS SetAttackerDefender +
    /// IsAttacking + AttackCount++). Emits OnAttack/OnAllyAttack and leaves State=Counter (or End on the AS-IS
    /// else-branch).
    /// (design item MIG1-BEFOREONATTACK, latent) The <paramref name="beforeOnAttack"/> callback exists here 1:1,
    /// but neither wiring path threads it yet — AttackDeclarationCommons.Declare and EffectDrivenAttack.Initiate
    /// pass null. The only AS-IS consumer is ST13_06 (Blitz BeforeOnAttackCoroutine — a conditional Jogress destroy
    /// BEFORE the [On Attack] window, ST13_06.cs:168-170), currently an unported skeleton, so no live damage; when
    /// ST13_06 (or any beforeOnAttack card) is ported, thread the callback through Declare/Initiate.</summary>
    /// <summary>(DECLARATION re-migration) AS-IS <c>AttackProcess.Attack(attackingPermanent, defendingPermanent,
    /// attackEffect, withoutTap, beforeOnAttackCoroutine)</c> (AttackProcess.cs:73) in its FULL AS-IS shape — the
    /// attacker/defender pair is set FIRST (AS-IS <c>SetAttackerDefender</c>, :64-71, here the substrate
    /// <c>AttackController.DeclareAttack</c> write) and the declaration sequence then runs. Re-homed from the
    /// retired substrate <c>AttackDeclarationCommons.Declare</c> / <c>.DeclareAsync</c>, which existed only to give
    /// the two callers (the main-phase attack action and <c>SelectAttackEffect</c>) one shared entry — that IS this
    /// method in AS-IS. The retired sync <c>Declare</c> blocked on <c>GetAwaiter().GetResult()</c> (documented as
    /// synchronous because nothing inside awaits an agent choice); both callers now simply await.</summary>
    public async Task<HeadlessAttackState> Attack(
        HeadlessPlayerId declaringPlayer,
        HeadlessEntityId attackerId,
        HeadlessPlayerId defendingPlayer,
        HeadlessEntityId? targetId,
        bool isDirectAttack,
        HeadlessEntityId? attackEffectSourceId = null,
        bool withoutTap = false,
        Func<CancellationToken, Task>? beforeOnAttack = null,
        CancellationToken cancellationToken = default)
    {
        // AS-IS :96 SetAttackerDefender(attackingPermanent, defendingPermanent) — the substrate state write
        // (AttackingPermanent / DefendingPermanent / HasDefender / IsAttacking / AttackCount++).
        _context.AttackController.DeclareAttack(
            declaringPlayer, attackerId, defendingPlayer, targetId, isDirectAttack);

        await Attack(attackerId, attackEffectSourceId, withoutTap, beforeOnAttack, cancellationToken).ConfigureAwait(false);
        return _context.AttackController.Current;
    }

    public async Task Attack(
        HeadlessEntityId attackerId,
        HeadlessEntityId? attackEffectSourceId = null,
        bool withoutTap = false,
        Func<CancellationToken, Task>? beforeOnAttack = null,
        CancellationToken cancellationToken = default)
    {
        // AS-IS :75-83 — re-entry guard: an in-flight OTHER attack only runs the callback.
        if (IsAttacking && _context.AttackController.Current.AttackerId != attackerId)
        {
            if (beforeOnAttack is not null)
            {
                await beforeOnAttack(cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        // AS-IS :85-94 — field reset (the controller record was freshly created by DeclareAttack).
        DoSecurityCheck = false;
        SecurityDigimon = null;
        IsEndAttack = false;
        CounterSourcesSnapshot = null;

        // AS-IS :96-99 — SetAttackerDefender (caller's DeclareAttack) + the hashtables. EffectHashtable
        // {AttackingPermanent, CardEffect} rides the emits' subject+cause below; CounterEffectHashtable is the
        // DECLARATION-TIME cardSources snapshot (:99).
        Permanent? attacker = AttackingPermanent;
        CounterSourcesSnapshot = SnapshotCardSources(attackerId);

        // AS-IS :106-111 — force to end attack (a pre-declaration effect set the flag).
        if (IsEndAttack)
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (pre-declaration).");
            return;
        }

        // AS-IS :114 — the attacker must be a battle-area Digimon; else-branch (:244-251) runs the callback and
        // goes straight to End.
        if (attacker is not null && Commons.IsPermanentExistsOnBattleAreaDigimon(attacker))
        {
            // AS-IS :116 AttackCount++ — counted by the controller at DeclareAttack (substrate).
            // AS-IS :124-155 — outlines / break-glass / play log: stripped.

            // AS-IS :158-167 — suspend the attacker unless withoutTap. Routed through the mirror
            // SuspendPermanentsClass (CardController.cs:1761) exactly as AS-IS does, so the tap carries the full
            // AS-IS semantics the raw metadata write skipped: already-suspended filter, !CanSuspend filter,
            // CanNotBeAffected filter, DPWhenSuspended = DP, and the OnTappedAnyone window (design item RD9-87
            // resolved for this call site). AS-IS ctor hashtable is `{"IsAttack", true}` (:160-163) —
            // GetCardEffectFromHashtable -> null cause, IsBlock(...) -> false (key absent), and IsAttack is read at
            // CardController.cs:5583 but never referenced again (verified dead), so the mirror ctor's
            // (cardEffect: null, isBlock: false) is the exact translation of that hashtable.
            // Tap() opens the OnTappedAnyone window via GManager.instance.autoProcessing — scoped on _context for
            // direct-call AttackProcess harnesses exactly like the StackSkillInfos inserts below (the live path
            // already runs under RunToStableAsync's scope, where Enter/Dispose is a no-op re-entry).
            if (!withoutTap)
            {
                using AmbientMatchContext.Scope _attackerTapScope = AmbientMatchContext.Enter(_context);
                await new SuspendPermanentsClass(new List<Permanent>() { attacker }, cardEffect: null, isBlock: false)
                    .Tap(cancellationToken).ConfigureAwait(false);
            }

            // AS-IS :170-188 — target arrows: stripped.

            // AS-IS :191-194 — callback processed before [On Attack].
            if (beforeOnAttack is not null)
            {
                await beforeOnAttack(cancellationToken).ConfigureAwait(false);
            }

            // AS-IS :197-199 — StackSkillInfos(EffectHashtable, OnAllyAttack), opened by the inline insert below.
            // (TriggerEventEmitter retirement) the vestigial raw-OnAttack ("OnUseAttack") queue emit is REMOVED: AS-IS
            // opens NO OnUseAttack window (only OnAllyAttack), the event-queue collector that once drained the emit is
            // gone, and no ported effect binds OnUseAttack as a firing site (the IsOnAttack / OnAttackCheckHashtableOfCard
            // gate is served by the OnAllyAttack window whose payload is OnAttackCheckHashtableOfPermanent below).

            // (P1-2 C2r — RD-C1-ATTACK-BGFX resolved) AS-IS :197-199 StackSkillInfos(EffectHashtable, OnAllyAttack) —
            // NOW ENABLED as the SOLE window opener (the OnAllyAttack emit above was removed, so SkillWindowSupply no
            // longer sees an OnAllyAttack attack event — its conversion for this timing is now unreachable). Unlike a
            // plain declared attack (which supply DID convert), an EFFECT-driven attack (attackEffectSourceId present)
            // was DROPPED by supply.TryBuildAttack (returns false on attackCauseEffectId) — the inline insert opens the
            // window for BOTH now, matching AS-IS which opens OnAllyAttack for effect-driven attacks too. Payload =
            // OnAttackCheckHashtableOfPermanent(AttackingPermanent, attackEffect): the live ICardEffect is still a GAP
            // (the mirror threads only attackEffectSourceId, RD-C1-CARDEFFECT-IDTHREAD / RDW-05), so cardEffect = null
            // here exactly as AS-IS's plain-attack case; the effect-driven cardEffect member remains the documented
            // RDW-05 residual. Guarded by AmbientMatchContext.Enter so the StackSkillInfos ActivateBackgroundEffects
            // sees THIS match (direct-call unit harnesses have no ambient scope; the live path runs under
            // RunToStableAsync's scope) — the same seam-carrier pattern as OnBlockAnyone / OnAttackTargetChanged below.
            {
                using AmbientMatchContext.Scope _onAllyAttackScope = AmbientMatchContext.Enter(_context);
                await GManager.instance!.autoProcessing
                    .StackSkillInfos(
                        Commons.OnAttackCheckHashtableOfPermanent(attacker, null),
                        EffectTiming.OnAllyAttack).ConfigureAwait(false);
            }

            // AS-IS :221-226 — force to end attack after the [On Attack] window. The window drains on the NEXT
            // loop iteration (the AS-IS StackSkillInfos only stacks too), so this boundary re-fires at the head of
            // CounterTiming (:258) — the effective post-drain boundary in both engines.
            if (IsEndAttack)
            {
                _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (post [On Attack]).");
                return;
            }

            // AS-IS :242 — State = Counter (the controller is already at Declared = the Counter projection).
        }
        else
        {
            // AS-IS :244-251.
            if (beforeOnAttack is not null)
            {
                await beforeOnAttack(cancellationToken).ConfigureAwait(false);
            }

            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Attacker not a battle-area Digimon at declaration.");
        }
    }

    // ===== AS-IS CounterTiming (:255-320) =========================================================================
    private async Task CounterTiming(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        await Task.CompletedTask.ConfigureAwait(false);
        HeadlessAttackState attack = _context.AttackController.Current;

        // AS-IS :258-263 — force to end attack.
        if (IsEndAttack)
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (counter head).");
            return;
        }

        // (C-Atk RETIRED) Raid / Alliance were run here by the invented RaidAttackSwitch / AllianceAttackBoost
        // gates (MIG1-KEYWORD-RELOCATE). Both are AS-IS KEYWORD ActivateClass effects (RaidSelfEffect /
        // AllianceSelfEffect) that fire in the OnAllyAttack window opened at the [On Attack] point
        // (AttackProcess.Attack, StackSkillInfos(OnAllyAttack)) — AS-IS AttackProcess.cs:197-199. That window is
        // the SOLE firing path now (a printed keyword makes Permanent.HasRaid/HasAlliance true off the SAME
        // ActivateClass the gate read, so keeping the gate here double-fired). The gate's RequestChoice firing-half
        // is de-wired; the class survives only until G-clean.
        // (PROGRESS re-migration) The `ProgressImmunity.TryRegister(_context)` call that stood here is RETIRED
        // for the SAME reason: AS-IS has no engine-side Progress seam at all. [Progress] is a printed SELF-STATIC
        // `CanNotAffectedClass` (AS-IS CardEffectFactory.ProgressSelfStaticEffect / ProgressStaticEffect —
        // mirrored 1:1 in CardEffectFactory/KeyWordEffects/Progress.cs), carried on the card's own effect list and
        // evaluated LIVE by `CardSource.CanNotBeAffected`; its CardCondition already gates on
        // "IsAttacking && AttackingPermanent == this permanent", which is exactly the window the substrate gate
        // hand-rolled. The gate additionally keyed off an AS-IS-ABSENT `hasProgress` instance flag and the
        // (now deleted) ContinuousKeywordGate. AS-IS's effect-driven counterpart, `CardEffectCommons.ProgressProcess`,
        // is mirrored and reachable from the keyword ActivateClass path.

        if (attack.AttackerId is HeadlessEntityId counterAttackerId
            && _context.CardInstanceRepository.TryGetInstance(counterAttackerId, out CardInstanceRecord? counterAttacker)
            && counterAttacker is not null)
        {
            // AS-IS :99 CounterEffectHashtable = OnAttackCheckHashtableOfPermanent(new Permanent(cardSources), attackEffect)
            // — {AttackingPermanent, CardEffect}, reused by both passes. The AS-IS DECLARATION-TIME cardSources snapshot
            // (new Permanent(cardSources)) has no source-list Permanent ctor in the mirror, so the live AttackingPermanent
            // view is used (non-null in this branch — AttackerId is present), consistent with the OnAllyAttack (:289) /
            // OnEndAttack (:597) windows. cardEffect = null is the RDW-05 / RD-C1-CARDEFFECT-IDTHREAD residual.
            System.Collections.Hashtable counterEffectHashtable = Commons.OnAttackCheckHashtableOfPermanent(
                AttackingPermanent!, null);

            // AS-IS :266-272 — pass 1: non-[Counter] OnCounterTiming effects (autoProcessing_CutIn), then the cut-in
            // drain (TriggeredSkillProcess). The dead queue emit + collector two-pass filter is replaced by the AS-IS
            // predicate. AmbientMatchContext.Enter guards StackSkillInfos' ActivateBackgroundEffects for direct-call
            // harnesses (the live path already runs under RunToStableAsync's scope).
            {
                using AmbientMatchContext.Scope _counterPass1Scope = AmbientMatchContext.Enter(_context);
                await GManager.instance!.autoProcessing_CutIn
                    .StackSkillInfos(counterEffectHashtable, EffectTiming.OnCounterTiming, cardEffect => !cardEffect.IsCounterEffect)
                    .ConfigureAwait(false);
            }
            await GManager.instance!.autoProcessing_CutIn.TriggeredSkillProcess(true, null).ConfigureAwait(false);
            GManager.instance!.turnStateMachine.IsSelecting = true; // AS-IS :274

            // AS-IS :277-282 — force to end attack between the passes.
            if (IsEndAttack)
            {
                _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (between counter passes).");
                return;
            }

            // AS-IS :285-296 — pass 2: [Counter] effects (OnCounterTiming, IsCounterEffect predicate), then the cut-in
            // drain gated by HasCounterEffect (AS-IS :290-293).
            {
                using AmbientMatchContext.Scope _counterPass2Scope = AmbientMatchContext.Enter(_context);
                await GManager.instance!.autoProcessing_CutIn
                    .StackSkillInfos(counterEffectHashtable, EffectTiming.OnCounterTiming, cardEffect => cardEffect.IsCounterEffect)
                    .ConfigureAwait(false);
            }
            await GManager.instance!.autoProcessing_CutIn.TriggeredSkillProcess(true, HasCounterEffect).ConfigureAwait(false);
            GManager.instance!.turnStateMachine.IsSelecting = true; // AS-IS :298

            // AS-IS :290-293 — the pass-2 cut-in drain fires only while a [Counter] effect is stacked.
            static bool HasCounterEffect(List<SkillInfo> skillInfos, SkillInfo skillInfo)
                => skillInfos.Count(si => si.CardEffect.IsCounterEffect) >= 1;
        }
        // (substrate-only fallback retired) AS-IS CounterTiming (:255-320) always has a live AttackingPermanent — there
        // is no no-attacker branch in AS-IS; the former legacy no-attacker counter queue emit is removed with the queue.

        // AS-IS :301-306 — post-counter boundary: force-end OR the attacker died / stopped being a Digimon during
        // the counter windows.
        if (IsEndAttack || !AttackerAliveDigimon())
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (post counter).");
            return;
        }

        // AS-IS :319 — State = Block (unconditional). The block SELECTION now lives in BlockTiming() (dispatched
        // next by the pump), which opens the SelectPermanentEffect choice; with no candidate it falls through to
        // Battle. This retires the invented RequestBlockChoice park (async-suspend supersedes it).
        _context.AttackController.AdvancePhase(AttackPhase.Blocking, "Counter resolved -> block timing (AS-IS :319).");
    }

    // ===== AS-IS BlockTiming (:322-405) 1:1 =======================================================================
    /// <summary>AS-IS <c>BlockTiming()</c> (:322-405) — the blocker SELECTION stage: if the attacker's enemy has a
    /// legal blocker, open the SelectPermanentEffect choice (suspends inline on the pump gate via
    /// <c>ChoiceProvider.ChooseAsync</c>) and apply it via <see cref="SwitchDefender"/> (AS-IS :376-379); then
    /// transition to Battle. UI/outline strips per the established pattern.</summary>
    private async Task BlockTiming(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        // AS-IS :325-330 — end attack / the attacker died or stopped being a Digimon.
        if (IsEndAttack || !AttackerAliveDigimon())
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (block head).");
            return;
        }

        Permanent attacker = AttackingPermanent!;

        // AS-IS :333-339 — a legal blocker is an opponent battle-area Digimon (not the current defender) with the
        // Blocker keyword that CanBlock this attacker.
        bool CanSelectBlockerCondition(Permanent permanent)
        {
            return Commons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, attacker.TopCard)
                && (DefendingPermanent is null || permanent.InstanceId != DefendingPermanent.InstanceId)
                && permanent.HasBlocker
                && permanent.CanBlock(attacker);
        }

        Player attackerEnemy = new Player(_context, attacker.TopCard.Owner).Enemy!;

        // AS-IS :341 — at least one candidate blocker.
        if (attackerEnemy.GetBattleAreaDigimons().Count(CanSelectBlockerCondition) >= 1)
        {
            Permanent? selectedPermanent = null;

            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

            // AS-IS :349-360 — SetUp (selectPlayer = the enemy who chooses the blocker; canNoSelect unless
            // Collision forces a block).
            selectPermanentEffect.SetUp(
                selectPlayer: attackerEnemy.PlayerId,
                canTargetCondition: CanSelectBlockerCondition,
                canTargetCondition_ByPreSelecetedList: null,
                canEndSelectCondition: null,
                maxCount: 1,
                canNoSelect: !attacker.HasCollision,
                canEndNotMax: false,
                selectPermanentCoroutine: SelectPermanentCoroutine,
                afterSelectPermanentCoroutine: null,
                mode: SelectPermanentEffect.Mode.Custom,
                cardEffect: null);

            selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon that will block.", "The opponent is selecting 1 Digimon that will block.");

            // AS-IS :364-365 — the "Not Block" back-button only when not already blocking.
            if (!IsBlocking)
            {
                selectPermanentEffect.SetUpCustomBackButtonMessage("Not Block");
            }

            // AS-IS :367 — the choice suspends inline on the pump gate (ChoiceProvider.ChooseAsync).
            await selectPermanentEffect.Activate().ConfigureAwait(false);

            // AS-IS :369-374 — the selection callback.
            async Task SelectPermanentCoroutine(Permanent permanent)
            {
                selectedPermanent = permanent;
                await Task.CompletedTask.ConfigureAwait(false);
            }

            // AS-IS :376-379 — apply the block via SwitchDefender.
            if (selectedPermanent is not null)
            {
                await SwitchDefender(null, true, selectedPermanent.InstanceId).ConfigureAwait(false);
            }
        }

        // AS-IS :386-391 — end attack / attacker died during the block window.
        if (IsEndAttack || !AttackerAliveDigimon())
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (post block).");
            return;
        }

        // AS-IS :404 — State = Battle.
        _context.AttackController.AdvancePhase(AttackPhase.Combat, "Block timing resolved.");
    }

    // ===== AS-IS DetermineAttackOutcome (:407-468) 1:1 ============================================================
    /// <summary>AS-IS <c>DetermineAttackOutcome()</c> (:407-468) — resolve the attack: no defender = a direct
    /// (security) attack (EndGame on an empty security stack, else set <c>DoSecurityCheck</c>); a live defender =
    /// battle INLINE via <see cref="IBattle"/> + the post-battle trigger drain; then, when <c>DoSecurityCheck</c>,
    /// the INLINE <see cref="ISecurityCheck"/>. The would-be-deleted PRE cut-in and the [Pierce] follow-up now
    /// resolve inline inside IBattle / ISecurityCheck / DestroyPermanentsClass (choices suspend on the pump gate) —
    /// the DeletionReplacement / PiercingSecurity park sub-phases are retired.</summary>
    private async Task DetermineAttackOutcome(CancellationToken cancellationToken)
    {
        // AS-IS :410-415 — end attack / attacker dead or non-Digimon.
        if (IsEndAttack || !AttackerAliveDigimon())
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (battle head).");
            return;
        }

        Permanent attacker = AttackingPermanent!;
        Permanent? defender = DefendingPermanent;

        // AS-IS :417-432 — no defending permanent: a direct (security) attack.
        if (defender is null)
        {
            // AS-IS :420-428 — Strike >= 1 against an EMPTY security stack ends the game (the attacker's owner wins).
            if (attacker.Strike >= 1)
            {
                Player attackerOwner = new Player(_context, attacker.TopCard.Owner);
                if (attackerOwner.Enemy!.SecurityCards.Count == 0)
                {
                    GManager.instance.turnStateMachine.EndGame(attackerOwner, false);
                    _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Direct attack: enemy has no security -> EndGame.");
                    return;
                }
            }

            // AS-IS :430 — DoSecurityCheck = true.
            DoSecurityCheck = true;
        }

        // AS-IS :434-448 — there IS a defending permanent (both battle-area Digimons): battle inline.
        else if (Commons.IsPermanentExistsOnBattleAreaDigimon(defender) && Commons.IsPermanentExistsOnBattleAreaDigimon(attacker))
        {
            // AS-IS :443-444 — battle (the would-be-deleted PRE cut-in resolves inside IBattle / DestroyPermanentsClass).
            IBattle battle = new IBattle(AttackingPermanent: attacker, DefendingPermanent: defender, DefendingCard: null);
            await battle.Battle(cancellationToken).ConfigureAwait(false);

            // AS-IS :446 — post-battle trigger drain (main-stack cut-in).
            await GManager.instance.autoProcessing.TriggeredSkillProcess(true, null).ConfigureAwait(false);
            GManager.instance.turnStateMachine.IsSelecting = true;
        }

        // AS-IS :451-456 — the attacker died during the battle / triggers.
        if (!AttackerAlive())
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Attacker did not survive the battle.");
            return;
        }

        // AS-IS :459-465 — the security check (the [Pierce] follow-up resolves inline inside ISecurityCheck's
        // StopSecurityCheck loop). Enemy = the attacker's opponent = the non-turn player.
        if (DoSecurityCheck
            && new Player(_context, AttackingPermanent!.TopCard.Owner).Enemy!.SecurityCards.Count >= 1)
        {
            await new ISecurityCheck(
                AttackingPermanent: AttackingPermanent!,
                player: GManager.instance.turnStateMachine.gameContext.NonTurnPlayer!).SecurityCheck(cancellationToken).ConfigureAwait(false);
        }

        // AS-IS :467 — State = End.
        _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Determine attack outcome resolved.");
    }

    // ===== AS-IS EndAttack (:473-484) =============================================================================
    /// <summary>AS-IS <c>EndAttack()</c> — PUBLIC: the effect-facing force-end surface (BT25_103 / EX7_052 /
    /// EX7_054 / BT13_088 … call <c>attackProcess.EndAttack()</c> to end an attack mid-flight). Sets IsEndAttack
    /// (:475); the stage boundaries route to End on the next step (the AS-IS coroutine checks the flag at each
    /// boundary). The [On End Attack] window itself runs when the state machine reaches the End stage.</summary>
    public void EndAttack() => IsEndAttack = true;

    private async Task EndAttackStage(CancellationToken cancellationToken)
    {
        IsEndAttack = true; // AS-IS :475 — the End stage always latches the flag.

        HeadlessAttackState attack = _context.AttackController.Current;
        if (attack.AttackingPlayerId is HeadlessPlayerId turnPlayer)
        {
            // AS-IS :478-481 — [On End Attack] fires ONLY while the attacker is still alive
            // (`AttackingPermanent != null && AttackingPermanent.TopCard != null`). Activated half: the
            // EventBroadcast window (subject = the attacker). The emit guard mirrors the AS-IS alive guard
            // (ScanZones also scans the trash; design item F1-ENDATTACK-LIVENESS covers the gate-side re-check).
            if (attack.AttackerId is HeadlessEntityId endAttackerId
                && _context.ZoneMover is IZoneStateReader endAttackZones
                && endAttackZones.GetCards(turnPlayer, ChoiceZone.BattleArea).Contains(endAttackerId))
            {
                // (P1-2 C2r — RD-C1-ATTACK-BGFX resolved) AS-IS :480 StackSkillInfos(EffectHashtable, OnEndAttack)
                // inside the AS-IS alive guard (:478 `AttackingPermanent != null && AttackingPermanent.TopCard != null`,
                // mirrored by the battle-area membership check above). NOW ENABLED as the SOLE opener: the OnEndAttack
                // EMIT is REMOVED (keeping both would double-fire — supply converts an OnEndAttack event to the SAME
                // GetSkillInfos(OnEndAttack) window). The retired EndAttackTriggerHook (comment above) was the OTHER
                // half; this inline insert now owns the whole OnEndAttack window. Payload =
                // OnAttackCheckHashtableOfPermanent(AttackingPermanent, attackEffect) — the live ICardEffect is the
                // RDW-05 / RD-C1-CARDEFFECT-IDTHREAD gap, so cardEffect = null (AS-IS's plain-attack case is exact; the
                // effect-driven cardEffect member is the documented residual). AmbientMatchContext.Enter guards the
                // StackSkillInfos ActivateBackgroundEffects for direct-call unit harnesses (live path already scoped).
                using AmbientMatchContext.Scope _onEndAttackScope = AmbientMatchContext.Enter(_context);
                await GManager.instance!.autoProcessing
                    .StackSkillInfos(
                        Commons.OnAttackCheckHashtableOfPermanent(AttackingPermanent!, null),
                        EffectTiming.OnEndAttack).ConfigureAwait(false);
            }
        }

        // (MIG1-EXECUTE-RELOCATE) C-9 Execute — a Digimon flagged to self-delete at end of attack. AS-IS this is an
        // UntilEndAttack DeleteSelfEffect (Execute's keyword registration), not AttackProcess logic; kept at the
        // position the freeform pipeline ran it.
        await DeleteSelfAtEndOfAttackAsync(attack).ConfigureAwait(false);

        // AS-IS :483 — State = CleanUp. NOTE: the UntilEndAttack expiry does NOT happen here — AS-IS resets
        // UntilEndAttackEffects in Cleanup() (:489-496), i.e. AFTER the [On End Attack] window resolved, so
        // [End of Attack] reactors still see UntilEndAttack buffs. (The freeform pipeline expired them here — a
        // divergence this mirror corrects.)
        _context.AttackController.AdvancePhase(AttackPhase.Completed, "End attack triggers collected.");
    }

    /// <summary>The Execute end-of-attack self-delete flag key.</summary>
    public const string DeleteSelfAtEndOfAttackKey = "deleteSelfAtEndOfAttack";

    private async Task DeleteSelfAtEndOfAttackAsync(HeadlessAttackState attack)
    {
        if (attack.AttackerId is not HeadlessEntityId attackerId ||
            !_context.CardInstanceRepository.TryGetInstance(attackerId, out CardInstanceRecord? attacker) ||
            attacker is null ||
            !(attacker.Metadata.TryGetValue(DeleteSelfAtEndOfAttackKey, out object? raw) && raw is true) ||
            _context.ZoneMover is not IZoneStateReader zones ||
            !zones.GetCards(attacker.OwnerId, ChoiceZone.BattleArea).Contains(attackerId))
        {
            return;
        }

        // Consume the one-shot flag, then run a REAL effect deletion (AS-IS DeleteSelfEffect is a normal
        // DeletePermanent: would-be-deleted replacements may respond; leave-play cleanup / deletion triggers apply).
        var metadata = new Dictionary<string, object?>(attacker.Metadata, StringComparer.Ordinal);
        metadata.Remove(DeleteSelfAtEndOfAttackKey);
        _context.CardInstanceRepository.Upsert(attacker with { Metadata = metadata });

        // (RDW re-migration off the retired MatchStateMutationSink) ONE DestroyPermanentsClass call over the attacker
        // runs the full AS-IS deletion pipeline (would-be-deleted PRE cut-in, OnDestroyedAnyone / OnLeaveFieldAnyone,
        // per-permanent trash). Cause = the attacker card itself collapsed to a BareCauseEffect — the exact source-id
        // fidelity the sink's DeleteKind carried (mutation.SourceEntityId == attackerId).
        await new DestroyPermanentsClass(
            new List<Permanent> { new Permanent(_context, attackerId, attacker.OwnerId) },
            Commons.CardEffectHashtable(BareCauseEffect.For(_context, attackerId))).Destroy().ConfigureAwait(false);
    }

    // ===== AS-IS Cleanup (:486-512) ===============================================================================
    private void Cleanup()
    {
        // (③-B) The registry attack-end sweep (EffectDurationExpiry.ExpireAttackEnd) is RETIRED — the EffectRegistry
        // continuous-binding producer is 0, so it was a dead write. The live AS-IS attack-end duration expiry is the
        // per-permanent UntilEndAttack BUCKET reset below.
        // (W3 / P6A-PERMANENT-EFFECTLIST-ADDED) AS-IS :489-495 — reset the NEW-model per-permanent grant store:
        // `foreach permanent in field: permanent.UntilEndAttackEffects = new List<…>()`. This is the AS-IS expiry
        // POSITION: after the [On End Attack] window resolved.
        if (_context.ZoneMover is IZoneStateReader cleanupZones)
        {
            foreach (HeadlessPlayerId playerId in _context.TurnController.Current.PlayerOrder)
            {
                foreach (ChoiceZone zone in new[] { ChoiceZone.BattleArea, ChoiceZone.BreedingArea })
                {
                    foreach (HeadlessEntityId topId in cleanupZones.GetCards(playerId, zone).ToArray())
                    {
                        new Permanent(_context, topId, playerId).UntilEndAttackEffects = new();
                    }
                }
            }
        }

        // AS-IS :501 OffTargetArrow — stripped.

        if (_context.AttackController.Current.AttackerId is HeadlessEntityId attackerId)
        {
            // (C-Atk RETIRED) the per-attack Raid/Alliance offered-once markers were substrate for the retired
            // counter-head gate choices — no longer set (the OnAllyAttack window is per-collection, not a
            // persistent per-attack marker), so nothing to clear.
            // (PROGRESS re-migration) the Progress per-attack dedup marker clear is RETIRED with its writer
            // (see CounterTiming): the AS-IS Progress immunity is a printed self-static read live off the card's
            // effect list, so there is no per-attack marker to reset — the UntilEndAttack bucket reset above is
            // the whole of AS-IS Cleanup :489-495.
            _ = attackerId;
        }

        // AS-IS :503-509 — field resets + State = None.
        DoSecurityCheck = false;
        SecurityDigimon = null;
        IsEndAttack = false;
        CounterSourcesSnapshot = null;
        _context.AttackController.ClearAttack();

        // (EFFECT-ATTACK re-migration) The `EffectDrivenAttack.TryOpenNextQueued(_context)` pump that stood here is
        // RETIRED with its queue. It re-opened the NEXT attacker of a multi-attacker Attack-mode selection, because
        // the substrate offer was DEFERRED (park the choice, return, resume from Cleanup). AS-IS has no such queue:
        // SelectPermanentEffect's Mode.Attack (:1009-1028) simply awaits one `SelectAttackEffect.Activate()` per
        // selected attacker in a sequential foreach, which the mirror now does inline — the loop's own await IS the
        // continuation, so there is nothing to dequeue here.
    }

    // ===== AS-IS SwitchDefender (:514-626) ========================================================================
    /// <summary>AS-IS <c>SwitchDefender(cardEffect, isBlock, newDefendingPermanent)</c> — PUBLIC: the effect-facing
    /// retarget surface (block resolution AND the ~30+ card-driven "switch the attack target" effects: BT9_044,
    /// EX8_050, BT18_073, …). Runs the FULL AS-IS sequence: guards -> retarget -> (block) blocker suspend +
    /// OnBlockAnyone -> attacker/defender death checks -> OnAttackTargetChanged (emitted HERE, centralized —
    /// design item F1-ATC-EMIT-CENTRALIZE resolved).</summary>
    public async Task SwitchDefender(
        HeadlessEntityId? causeEffectSourceId,
        bool isBlock,
        HeadlessEntityId? newDefendingPermanentId)
    {
        HeadlessAttackState attack = _context.AttackController.Current;

        // AS-IS :516-519 — guards: live attacker, live new defender (when any), CanSwitchAttackTarget.
        if (attack.AttackerId is not HeadlessEntityId attackerId || !AttackerAlive())
        {
            return;
        }

        if (newDefendingPermanentId is HeadlessEntityId newDefender && !PermanentAlive(newDefender))
        {
            return;
        }

        // (SWITCH-GATE re-migration) AS-IS :519 `if (!AttackingPermanent.CanSwitchAttackTarget) yield break` —
        // the substrate `AttackTargetSwitchGate.IsLocked` wrapper re-implemented the AS-IS
        // ICanNotSwitchAttackTargetEffect scan; that scan is the live mirror getter
        // `Permanent.CanSwitchAttackTarget` (Permanent.cs:4020), so the AS-IS call is used directly.
        if (AttackingPermanent is { } attackingPermanent && !attackingPermanent.CanSwitchAttackTarget)
        {
            return;
        }

        HeadlessEntityId? oldDefendingPermanentId = attack.TargetId;

        // AS-IS :521-525 — retarget + IsBlocking (the controller write-through).
        if (isBlock && newDefendingPermanentId is HeadlessEntityId blockerId)
        {
            _context.AttackController.SelectBlocker(blockerId);
        }
        else
        {
            _context.AttackController.RetargetDefender(newDefendingPermanentId, "SwitchDefender retarget.");
        }

        // AS-IS :514 `SwitchDefender(ICardEffect cardEffect, …)` — the mirror threads only the cause SOURCE ID
        // (RD-C1-CARDEFFECT-IDTHREAD), so the id is lifted back to an ICardEffect for the hashtable slot with
        // BareCauseEffect.ForOrNull: an absent / unresolvable id yields NULL, exactly as AS-IS carries a null
        // cardEffect for the rule-sourced (block) path, while a card-driven redirect (AD1_012, BT15_078, BT25_039,
        // Raid) now carries its real cause into BOTH windows below AND into the blocker tap (:557).
        ICardEffect? cardEffect = BareCauseEffect.ForOrNull(_context, causeEffectSourceId ?? default);

        // (C1b) AS-IS AttackProcess.cs:536-545 — the StackSkillInfos window hashtable, built ONCE (capturing the
        // permanents post-retarget) and SHARED by both emits below AND by the blocker tap, exactly like AS-IS.
        var attackSwitchWindow = new System.Collections.Hashtable
        {
            { "AttackingPermanent", AttackingPermanent },
            { "DefendingPermanent", DefendingPermanent },
            { "CardEffect", cardEffect },
        };
        if (isBlock)
        {
            attackSwitchWindow.Add("IsBlock", isBlock); // AS-IS :544 — the IsBlock key present only on the block path.
        }

        // AS-IS :547-564 — block: suspend the blocker, then the [When Blocking] window (OnBlockAnyone). The gate
        // reads the ATTACKER (CanTriggerOnAttack over AttackingPermanent) — subject = the attacker.
        if (isBlock && newDefendingPermanentId is HeadlessEntityId && DefendingPermanent is { } blockingPermanent)
        {
            // AS-IS :557 `new SuspendPermanentsClass(new List<Permanent>(){ DefendingPermanent }, hashtable).Tap()`
            // — the SHARED SwitchDefender hashtable, so GetCardEffectFromHashtable -> cardEffect and IsBlock -> true
            // (the key added at :544). Routed through the mirror class so the tap carries the already-suspended /
            // !CanSuspend / CanNotBeAffected filters, DPWhenSuspended = DP and the OnTappedAnyone window.
            {
                using AmbientMatchContext.Scope _blockerTapScope = AmbientMatchContext.Enter(_context);
                await new SuspendPermanentsClass(new List<Permanent>() { blockingPermanent }, cardEffect, isBlock)
                    .Tap().ConfigureAwait(false);
            }

            // (C2 seam-carrier) AS-IS AttackProcess.cs:560-562 StackSkillInfos(attackSwitchWindow, OnBlockAnyone) —
            // NOW ENABLED. SkillWindowSupply GAP-drops OnBlockAnyone (RDW-02), so this inline insert at the AS-IS emit
            // position is the SOLE window opener; the coexisting emit above is drained and GAP-dropped (no double).
            // Guarded on GManager.instance for direct-call AttackProcess harnesses with no ambient scope (the live
            // path runs under RunToStableAsync's AmbientMatchContext scope).
            {
                using AmbientMatchContext.Scope _onBlockScope = AmbientMatchContext.Enter(_context);
                await GManager.instance!.autoProcessing
                    .StackSkillInfos(attackSwitchWindow, EffectTiming.OnBlockAnyone).ConfigureAwait(false);
            }
        }

        // AS-IS :566-582 — death checks: the attacker or the new defender died during the block sequence ->
        // IsBlocking is cleared and the switch aborts before OnAttackTargetChanged.
        if (!AttackerAlive())
        {
            _context.AttackController.ClearBlockingFlag("Attacker died during the switch.");
            return;
        }

        if (newDefendingPermanentId is HeadlessEntityId defenderCheck && !PermanentAlive(defenderCheck))
        {
            _context.AttackController.ClearBlockingFlag("New defender died during the switch.");
            return;
        }

        // AS-IS :584-617 — target arrows: stripped.

        // AS-IS :619-625 — the effects when the attack target switched (only on a REAL change).
        if (newDefendingPermanentId != oldDefendingPermanentId)
        {
            // (C2 seam-carrier) AS-IS AttackProcess.cs:622-624 StackSkillInfos(attackSwitchWindow,
            // OnAttackTargetChanged) — NOW ENABLED (reuses the SAME shared hashtable, so the IsBlock key rides along
            // when isBlock was true, as AS-IS). SkillWindowSupply GAP-drops this timing (RDW-02), so the inline insert
            // is the sole opener; the coexisting emit is GAP-dropped.
            {
                using AmbientMatchContext.Scope _onSwitchScope = AmbientMatchContext.Enter(_context);
                await GManager.instance!.autoProcessing
                    .StackSkillInfos(attackSwitchWindow, EffectTiming.OnAttackTargetChanged).ConfigureAwait(false);
            }
        }
    }

    // ===== Substrate helpers ======================================================================================

    private bool AttackerAlive()
    {
        HeadlessAttackState attack = _context.AttackController.Current;
        return attack.AttackerId is HeadlessEntityId attackerId
            && attack.AttackingPlayerId is HeadlessPlayerId attackingPlayer
            && _context.ZoneMover is IZoneStateReader zones
            && zones.GetCards(attackingPlayer, ChoiceZone.BattleArea).Contains(attackerId);
    }

    // AS-IS `AttackingPermanent.TopCard == null || !AttackingPermanent.IsDigimon` boundary (:301,325,386,410).
    private bool AttackerAliveDigimon()
    {
        if (!AttackerAlive())
        {
            return false;
        }

        Permanent? attacker = AttackingPermanent;
        return attacker is not null && attacker.IsDigimon;
    }

    private bool PermanentAlive(HeadlessEntityId id)
    {
        if (_context.ZoneMover is not IZoneStateReader zones ||
            !_context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) || record is null)
        {
            return false;
        }

        return zones.GetCards(record.OwnerId, ChoiceZone.BattleArea).Contains(id);
    }

    // (RD9-87 resolved) The private raw-metadata `SuspendPermanent(id, byAttack)` helper is RETIRED: both AS-IS tap
    // sites (:160-166 attacker, :557 blocker) now call the mirror `SuspendPermanentsClass(...).Tap()` exactly as
    // AS-IS does. The helper's `suspendedByAttack` marker had NO reader anywhere in the repo (it stood in for the
    // AS-IS `IsAttack` hashtable key, itself read at CardController.cs:5583 and never used again), so nothing is
    // lost by the retirement.

    // AS-IS :99 `new Permanent(AttackingPermanent.cardSources)` — the declaration-time stack snapshot (top +
    // digivolution sources).
    private IReadOnlyList<HeadlessEntityId>? SnapshotCardSources(HeadlessEntityId attackerId)
    {
        if (attackerId.IsEmpty)
        {
            return null;
        }

        var snapshot = new List<HeadlessEntityId> { attackerId };
        DigivolutionStack stack = DigivolutionStackReader.Read(_context.CardInstanceRepository, _context.CardRepository, attackerId);
        foreach (StackedCard source in stack.UnderCards)
        {
            snapshot.Add(source.InstanceId);
        }

        return snapshot;
    }

}
