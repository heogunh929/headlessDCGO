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
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.State;
// Inside this namespace the identifier `CardEffectCommons` binds to the CHILD NAMESPACE first — alias the static
// commons class (the AS-IS CardEffectCommons mirror) explicitly.
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;

public sealed class AttackProcess
{
    private readonly EngineContext _context;
    private readonly BlockTiming _blockTiming;
    private readonly BattleResolver _battleResolver;
    private readonly SecurityResolver _securityResolver;

    public AttackProcess(
        EngineContext context,
        BlockTiming? blockTiming = null,
        BattleResolver? battleResolver = null,
        SecurityResolver? securityResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _blockTiming = blockTiming ?? new BlockTiming();
        _battleResolver = battleResolver ?? new BattleResolver();
        _securityResolver = securityResolver ?? new SecurityResolver();
    }

    /// <summary>The per-context instance (AS-IS <c>GManager.instance.attackProcess</c>).</summary>
    public static AttackProcess For(
        EngineContext context,
        BlockTiming? blockTiming = null,
        BattleResolver? battleResolver = null,
        SecurityResolver? securityResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGetService(out AttackProcess? existing) && existing is not null)
        {
            return existing;
        }

        var created = new AttackProcess(context, blockTiming, battleResolver, securityResolver);
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
        AttackPhase.DeletionReplacement => AttackState.Battle,
        AttackPhase.PiercingSecurity => AttackState.Battle,
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

    // Counter-pass progress — the AS-IS CounterTiming() coroutine position (pass 1 / pass 2 emitted), explicit
    // because each pass parks for the loop to drain (W6/P5 two-pass, AS-IS :266-296).
    private bool _counterPass1Emitted;
    private bool _counterPass2Emitted;

    /// <summary>AS-IS <c>ActiveAttack()</c> (:35-38).</summary>
    public bool ActiveAttack() => State != AttackState.None;

    // ===== AS-IS ProcessNextState (:40-62) ========================================================================
    /// <summary>One state-machine step. AS-IS returns void and the driving loop re-reads State; the headless loop
    /// consumes an <see cref="AttackAdvanceResult"/> record (substrate seam — the loop cannot observe coroutine
    /// suspension, so each step reports its transition).</summary>
    public async Task<AttackAdvanceResult> ProcessNextState(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return State switch
        {
            AttackState.Counter => await CounterTiming(cancellationToken).ConfigureAwait(false),
            AttackState.Block => BlockStage(),
            AttackState.Battle => await DetermineAttackOutcome(cancellationToken).ConfigureAwait(false),
            AttackState.End => await EndAttackStage(cancellationToken).ConfigureAwait(false),
            AttackState.CleanUp => Cleanup(),
            _ => AttackAdvanceResult.Idle(),
        };
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
        _counterPass1Emitted = false;
        _counterPass2Emitted = false;

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

            // AS-IS :158-167 — suspend the attacker ({"IsAttack", true} tap) unless withoutTap.
            if (!withoutTap)
            {
                SuspendPermanent(attackerId, byAttack: true);
            }

            // AS-IS :170-188 — target arrows: stripped.

            // AS-IS :191-194 — callback processed before [On Attack].
            if (beforeOnAttack is not null)
            {
                await beforeOnAttack(cancellationToken).ConfigureAwait(false);
            }

            // AS-IS :197-199 — StackSkillInfos(EffectHashtable, OnAllyAttack). The headless declaration still emits the
            // raw OnAttack ("OnUseAttack") for the RD-9 self-scoped choke point (subject = the attacker). The
            // OnAllyAttack EMIT is REMOVED at the C2r flip (see the inline insert below): keeping both the emit AND the
            // insert would double-fire (supply converts an OnAllyAttack event to the SAME GetSkillInfos(OnAllyAttack)
            // window the insert opens).
            TriggerEventEmitter.Emit(
                _context.GameEventQueue,
                TriggerTimings.OnAttack,
                actor: _context.AttackController.Current.AttackingPlayerId,
                subject: attackerId,
                extraMetadata: AttackEventMetadata(attackEffectSourceId));

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
    private async Task<AttackAdvanceResult> CounterTiming(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        HeadlessAttackState attack = _context.AttackController.Current;

        // AS-IS :258-263 — force to end attack.
        if (IsEndAttack)
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (counter head).");
            return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Resolved);
        }

        // (C-Atk RETIRED) Raid / Alliance were run here by the invented RaidAttackSwitch / AllianceAttackBoost
        // gates (MIG1-KEYWORD-RELOCATE). Both are AS-IS KEYWORD ActivateClass effects (RaidSelfEffect /
        // AllianceSelfEffect) that fire in the OnAllyAttack window opened at the [On Attack] point
        // (AttackProcess.Attack, StackSkillInfos(OnAllyAttack)) — AS-IS AttackProcess.cs:197-199. That window is
        // the SOLE firing path now (a printed keyword makes Permanent.HasRaid/HasAlliance true off the SAME
        // ActivateClass the gate read, so keeping the gate here double-fired). The gate's RequestChoice firing-half
        // is de-wired; the class survives only until G-clean. Progress stays here (a separate keyword, still
        // engine-plumbed pending its own rehousing).
        ProgressImmunity.TryRegister(_context);

        if (attack.AttackerId is HeadlessEntityId counterAttackerId
            && _context.CardInstanceRepository.TryGetInstance(counterAttackerId, out CardInstanceRecord? counterAttacker)
            && counterAttacker is not null)
        {
            // AS-IS :266-272 — pass 1: non-[Counter] OnCounterTiming effects, then the cut-in drain
            // (autoProcessing_CutIn.TriggeredSkillProcess). The drain is the loop's next iteration — PARK.
            if (!_counterPass1Emitted)
            {
                _counterPass1Emitted = true;
                TriggerEventEmitter.Emit(
                    _context.GameEventQueue,
                    TriggerTimings.OnCounter,
                    actor: attack.AttackingPlayerId,
                    subject: counterAttackerId,
                    extraMetadata: CounterEventMetadata(AutoProcessingTriggerCollector.CounterPassRegular));
                return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Declared);
            }

            // AS-IS :277-282 — force to end attack between the passes.
            if (IsEndAttack)
            {
                _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (between counter passes).");
                return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Resolved);
            }

            // AS-IS :285-296 — pass 2: [Counter] effects (the HasCounterEffect gate rides the collector's
            // CounterPassKey filter), then the cut-in drain — PARK.
            if (!_counterPass2Emitted)
            {
                _counterPass2Emitted = true;
                TriggerEventEmitter.Emit(
                    _context.GameEventQueue,
                    TriggerTimings.OnCounter,
                    actor: attack.AttackingPlayerId,
                    subject: counterAttackerId,
                    extraMetadata: CounterEventMetadata(AutoProcessingTriggerCollector.CounterPassCounter));
                return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Declared);
            }
        }
        else
        {
            // Contract-level pipelines with no live attacker record: the single legacy counter emit (substrate-only
            // branch; AS-IS always has a live AttackingPermanent here).
            TriggerEventEmitter.Emit(
                _context.GameEventQueue,
                TriggerTimings.OnCounter,
                actor: attack.AttackingPlayerId);
        }

        // AS-IS :301-306 — post-counter boundary: force-end OR the attacker died / stopped being a Digimon during
        // the counter windows.
        if (IsEndAttack || !AttackerAliveDigimon())
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (post counter).");
            return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Resolved);
        }

        // AS-IS :319 — State = Block. The block stage opens the blocker choice immediately (the AS-IS BlockTiming()
        // coroutine starts with the selection); with no candidate it falls through to Battle.
        BlockTimingResult block = _blockTiming.RequestBlockChoice(_context);
        if (block.IsSuccess && block.ChoiceRequested)
        {
            _context.AttackController.AdvancePhase(AttackPhase.Blocking, "Block timing opened.");
            return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Blocking, choiceRequested: true);
        }

        _context.AttackController.AdvancePhase(AttackPhase.Combat, "No blocker candidates.");
        return AttackAdvanceResult.Transitioned(AttackPhase.Declared, AttackPhase.Combat);
    }

    // ===== AS-IS BlockTiming (:322-405) ===========================================================================
    // The blocker SELECTION half lives in BlockTiming (the SelectPermanentEffect mirror): RequestBlockChoice opened
    // the choice (the coroutine suspension = the Blocking park); the external ResolveChoice action routes to
    // BlockTiming.ResolveBlockChoice, which applies the selection via SwitchDefender (AS-IS :376-379). This stage
    // method is the POST-selection tail (:383-404).
    private AttackAdvanceResult BlockStage()
    {
        // AS-IS :386-391 — end attack / the attacker died or stopped being a Digimon during the block window.
        if (IsEndAttack || !AttackerAliveDigimon())
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (post block).");
            return AttackAdvanceResult.Transitioned(AttackPhase.Blocking, AttackPhase.Resolved);
        }

        // AS-IS :404 — State = Battle.
        _context.AttackController.AdvancePhase(AttackPhase.Combat, "Block timing resolved.");
        return AttackAdvanceResult.Transitioned(AttackPhase.Blocking, AttackPhase.Combat);
    }

    // ===== AS-IS DetermineAttackOutcome (:407-468) ================================================================
    // The AS-IS method suspends twice (the would-be-deleted PRE window inside IBattle/ISecurityCheck, and the
    // post-battle trigger drain). Those suspensions are the Combat sub-parks: DeletionReplacement (F-6.8/C-5) and
    // PiercingSecurity (B2 drain-then-check). Entry dispatches on the sub-park.
    private async Task<AttackAdvanceResult> DetermineAttackOutcome(CancellationToken cancellationToken)
    {
        HeadlessAttackState attack = _context.AttackController.Current;
        switch (attack.Phase)
        {
            case AttackPhase.DeletionReplacement:
                return await ResumeDeletionReplacement(attack, cancellationToken).ConfigureAwait(false);
            case AttackPhase.PiercingSecurity:
                return await ResumePiercingSecurity(attack, cancellationToken).ConfigureAwait(false);
        }

        // AS-IS :410-415 — end attack / attacker dead or non-Digimon.
        if (IsEndAttack || !AttackerAliveDigimon())
        {
            _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Force end attack (battle head).");
            return AttackAdvanceResult.Transitioned(AttackPhase.Combat, AttackPhase.Resolved);
        }

        // AS-IS :417-432 — no defending permanent: a direct attack.
        bool directCheck = attack.IsDirectAttack && !attack.TargetId.HasValue && !attack.IsBlocked;
        if (directCheck)
        {
            // AS-IS :420-430 — Strike >= 1 against an empty security stack ends the game (:425 EndGame; surfaced
            // by the resolver as DefenderHasNoSecurity -> MarkLose, X-02); otherwise DoSecurityCheck = true.
            DoSecurityCheck = true;

            SecurityResolutionResult security = await _securityResolver
                .ResolveAsync(_context, cancellationToken)
                .ConfigureAwait(false);

            // (C-5/VR-6) a security-battle loss deferred for the attacker's would-be-deleted window — the AS-IS PRE
            // cut-in suspends the coroutine chain mid-SecurityCheck: PARK at DeletionReplacement.
            if (security.RequiresDeletionReplacement)
            {
                _context.AttackController.AdvancePhase(AttackPhase.DeletionReplacement, "Security battle deletion replacement window.");
                return AttackAdvanceResult.Transitioned(AttackPhase.Combat, AttackPhase.DeletionReplacement);
            }

            if (!security.IsSuccess && _context.AttackController.Current.IsPending)
            {
                if (security.DefenderHasNoSecurity && attack.DefendingPlayerId is HeadlessPlayerId defendingPlayer)
                {
                    _context.PlayerStatusController.MarkLose(
                        defendingPlayer,
                        "Direct attack resolved with no security to check.");
                }

                _context.LogSink.Warn($"[AttackProcess] security resolution failed: {security.FailureReason}");
                _context.AttackController.ResolveAttack($"Security resolution failed: {security.FailureReason}");
            }

            // AS-IS :467 — State = End.
            return AttackAdvanceResult.Transitioned(AttackPhase.Combat, AttackPhase.Resolved, securityResolved: true);
        }

        // AS-IS :434-448 — there is a defending permanent: battle (the IBattle mirror = BattleResolver).
        BattleResolutionResult battle = await _battleResolver
            .ResolveAsync(_context, cancellationToken)
            .ConfigureAwait(false);

        // F-6.8 — battle deletion deferred for the would-be-deleted PRE window: PARK.
        if (battle.RequiresDeletionReplacement)
        {
            _context.AttackController.AdvancePhase(AttackPhase.DeletionReplacement, "Battle deletion replacement window.");
            return AttackAdvanceResult.Transitioned(AttackPhase.Combat, AttackPhase.DeletionReplacement);
        }

        if (!battle.IsSuccess && _context.AttackController.Current.IsPending)
        {
            _context.LogSink.Warn($"[AttackProcess] battle resolution failed: {battle.FailureReason}");
            _context.AttackController.ResolveAttack($"Battle resolution failed: {battle.FailureReason}");
        }

        // AS-IS :446 post-battle TriggeredSkillProcess, then :451-456 the attacker-survived re-check, then
        // :459-465 the security check when DoSecurityCheck. (RD-CBTL-01) TriggersPiercingSecurityCheck is now the
        // OnDetermineDoSecurityCheck WINDOW result (a [Pierce] ActivateClass was stacked by BattleResolver), not a
        // pre-computed flag — so DoSecurityCheck is NOT set here; PierceProcess sets it when the next loop iteration
        // drains the stacked skill (before this park's ResumePiercingSecurity re-entry). PARK at PiercingSecurity.
        if (battle.IsSuccess && battle.TriggersPiercingSecurityCheck)
        {
            _context.AttackController.AdvancePhase(AttackPhase.PiercingSecurity, "Piercing security check pending (triggers drain first).");
            return AttackAdvanceResult.Transitioned(AttackPhase.Combat, AttackPhase.PiercingSecurity, battleResolved: true);
        }

        return AttackAdvanceResult.Transitioned(AttackPhase.Combat, AttackPhase.Resolved, battleResolved: true);
    }

    // (B2) AS-IS :446-465 resume — the battle triggers drained; re-check the attacker survived
    // (`if (AttackingPermanent.TopCard == null) { State = End; }` :451-456) before the DoSecurityCheck half.
    private async Task<AttackAdvanceResult> ResumePiercingSecurity(HeadlessAttackState attack, CancellationToken cancellationToken)
    {
        bool attackerAlive = AttackerAlive();
        if (attackerAlive && DoSecurityCheck)
        {
            SecurityCheckLoopResult? piercing = await ApplyPiercingSecurityAsync(attack, cancellationToken).ConfigureAwait(false);

            // (C-5/VR-6) the piercing check's security battle deferred — PARK at DeletionReplacement.
            if (piercing is { DeferredForDeletionReplacement: true })
            {
                _context.AttackController.AdvancePhase(AttackPhase.DeletionReplacement, "Piercing security battle deletion replacement window.");
                return AttackAdvanceResult.Transitioned(AttackPhase.PiercingSecurity, AttackPhase.DeletionReplacement);
            }
        }

        _context.AttackController.AdvancePhase(AttackPhase.Resolved, attackerAlive
            ? "Piercing security check resolved."
            : "Piercing skipped: the attacker did not survive the battle triggers.");
        return AttackAdvanceResult.Transitioned(AttackPhase.PiercingSecurity, AttackPhase.Resolved, securityResolved: attackerAlive);
    }

    // F-6.8 / C-5 — the would-be-deleted replacement windows resolved; finalize (the AS-IS coroutine resuming after
    // the PRE cut-in).
    private async Task<AttackAdvanceResult> ResumeDeletionReplacement(HeadlessAttackState attack, CancellationToken cancellationToken)
    {
        if (attack.AttackerId is HeadlessEntityId parkedAttackerId &&
            SecurityResolver.HasDeferredSecurityCheck(_context, parkedAttackerId))
        {
            SecurityDeferredCheckResult security = await _securityResolver
                .FinalizeDeferredSecurityCheckAsync(_context, cancellationToken)
                .ConfigureAwait(false);
            if (security.IsDeferred)
            {
                return AttackAdvanceResult.Transitioned(AttackPhase.DeletionReplacement, AttackPhase.DeletionReplacement);
            }

            if (attack.IsDirectAttack)
            {
                _context.AttackController.ResolveAttack("Security check resolved.");
            }
            else
            {
                _context.AttackController.AdvancePhase(AttackPhase.Resolved, "Deferred piercing security check resolved.");
            }

            return AttackAdvanceResult.Transitioned(AttackPhase.DeletionReplacement, AttackPhase.Resolved, securityResolved: true);
        }

        BattleResolutionResult battle = await _battleResolver
            .FinalizeDeferredAsync(_context, cancellationToken)
            .ConfigureAwait(false);
        if (!battle.IsSuccess && _context.AttackController.Current.IsPending)
        {
            _context.LogSink.Warn($"[AttackProcess] deferred battle finalize failed: {battle.FailureReason}");
            _context.AttackController.ResolveAttack($"Battle finalize failed: {battle.FailureReason}");
        }

        // (RD-CBTL-01) PierceProcess (drained next iteration) sets DoSecurityCheck — see the DetermineAttackOutcome
        // twin above; the deferred re-entry parks the same way when the window stacked a [Pierce] skill.
        if (battle.IsSuccess && battle.TriggersPiercingSecurityCheck)
        {
            _context.AttackController.AdvancePhase(AttackPhase.PiercingSecurity, "Piercing security check pending (triggers drain first).");
            return AttackAdvanceResult.Transitioned(AttackPhase.DeletionReplacement, AttackPhase.PiercingSecurity, battleResolved: true);
        }

        return AttackAdvanceResult.Transitioned(AttackPhase.DeletionReplacement, AttackPhase.Resolved, battleResolved: true);
    }

    // AS-IS :459-465 ISecurityCheck for the piercing follow-up — SecurityResolver's shared per-card loop (identical
    // to the direct check: the OnSecurityCheck W4 window + the security-Digimon battle W5).
    private async Task<SecurityCheckLoopResult?> ApplyPiercingSecurityAsync(HeadlessAttackState attack, CancellationToken cancellationToken)
    {
        if (attack.DefendingPlayerId is not HeadlessPlayerId defender ||
            attack.AttackingPlayerId is not HeadlessPlayerId attackingPlayer ||
            attack.AttackerId is not HeadlessEntityId attackerId ||
            _context.ZoneMover is not IZoneStateReader zoneReader)
        {
            return null;
        }

        int strike = ReadStrike(attackerId);
        if (strike <= 0)
        {
            return null;
        }

        // (A1) AS-IS CanActivatePierce (Pierce.cs:20-42): Pierce fires only while the defending player has >= 1
        // security. Losing on an empty stack belongs ONLY to the direct-attack path (AS-IS :423 EndGame).
        if (zoneReader.GetCards(defender, ChoiceZone.Security).Count == 0)
        {
            return null;
        }

        return await _securityResolver.RunSecurityCheckLoopAsync(
            _context, zoneReader, attackingPlayer, attackerId, defender, strike, cancellationToken).ConfigureAwait(false);
    }

    // ===== AS-IS EndAttack (:473-484) =============================================================================
    /// <summary>AS-IS <c>EndAttack()</c> — PUBLIC: the effect-facing force-end surface (BT25_103 / EX7_052 /
    /// EX7_054 / BT13_088 … call <c>attackProcess.EndAttack()</c> to end an attack mid-flight). Sets IsEndAttack
    /// (:475); the stage boundaries route to End on the next step (the AS-IS coroutine checks the flag at each
    /// boundary). The [On End Attack] window itself runs when the state machine reaches the End stage.</summary>
    public void EndAttack() => IsEndAttack = true;

    private async Task<AttackAdvanceResult> EndAttackStage(CancellationToken cancellationToken)
    {
        IsEndAttack = true; // AS-IS :475 — the End stage always latches the flag.
        int enqueued = 0;

        HeadlessAttackState attack = _context.AttackController.Current;
        if (attack.AttackingPlayerId is HeadlessPlayerId turnPlayer)
        {
            // (C2 / design item F1-ENDATTACK-HOOK RETIRED) The scheduler-half EndAttackTriggerHook is REMOVED at the
            // flip: it collected bound OnEndAttack reactors onto the EffectScheduler off-queue, which — now that the
            // queued OnEndAttack event drives the MIRROR window (SkillWindowSupply → GetSkillInfos(OnEndAttack) on the
            // main stack) — would DOUBLE-FIRE a reactor found by both halves. The unified mirror path (supply-converted
            // OnEndAttack emit + inline main-stack drain) owns both halves now; the WindowResolverWiring.cs OnEndAttack
            // scheduler-skip guard that existed only to bridge the hook is likewise superseded (dead after the flip).
            // enqueued stays 0 (no scheduler enqueue), carried through AttackAdvanceResult unchanged.

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
        return AttackAdvanceResult.Transitioned(AttackPhase.Resolved, AttackPhase.Completed, enqueuedEndAttackTriggers: enqueued);
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

        // Consume the one-shot flag, then run a REAL effect deletion through the sink (AS-IS DeleteSelfEffect is a
        // normal DeletePermanent: would-be-deleted replacements may respond; leave-play cleanup / deletion triggers
        // apply).
        var metadata = new Dictionary<string, object?>(attacker.Metadata, StringComparer.Ordinal);
        metadata.Remove(DeleteSelfAtEndOfAttackKey);
        _context.CardInstanceRepository.Upsert(attacker with { Metadata = metadata });

        var sink = new MatchStateMutationSink(
            _context.CardInstanceRepository, log: null, _context.ZoneMover, memory: null,
            _context.EffectRegistry, _context.GameEventQueue, context: _context);
        sink.Apply(new EffectMutation(
            MatchStateMutationSink.DeleteKind,
            attackerId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [MatchStateMutationSink.TargetEntityIdKey] = attackerId.Value }));
        await sink.FlushAsync().ConfigureAwait(false);
    }

    // ===== AS-IS Cleanup (:486-512) ===============================================================================
    private AttackAdvanceResult Cleanup()
    {
        // AS-IS :489-496 — reset the effects that continue until the end of attack (every field permanent's
        // UntilEndAttackEffects). This is the AS-IS expiry POSITION: after the [On End Attack] window resolved.
        EffectDurationExpiry.ExpireAttackEnd(_context.EffectRegistry);

        // (W3 / P6A-PERMANENT-EFFECTLIST-ADDED) AS-IS :489-495 — the same reset for the NEW-model per-permanent
        // grant store: `foreach permanent in field: permanent.UntilEndAttackEffects = new List<…>()`. Dormant today
        // (no live writer until batch C), so behavior-neutral — it keeps the store correct for cutover.
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
            // (C-5/VR-6) drop any stale deferred-security-check park marker with the attack.
            SecurityResolver.ClearDeferredRemaining(_context, attackerId);
            // (R3-W3c-2) clear the Progress per-attack dedup marker alongside the UntilEndAttack bucket reset
            // above (the bucket held the Progress CanNotAffectedClass; both expire here — AS-IS Cleanup :489-495).
            Headless.Runtime.ProgressImmunity.ClearApplied(_context, attackerId);
        }

        // AS-IS :503-509 — field resets + State = None.
        DoSecurityCheck = false;
        SecurityDigimon = null;
        IsEndAttack = false;
        CounterSourcesSnapshot = null;
        _counterPass1Emitted = false;
        _counterPass2Emitted = false;
        _context.AttackController.ClearAttack();

        // (P3) a queued multi-attacker Attack-mode selection continues with the next attacker (the AS-IS sequential
        // SelectAttackEffect loop) — substrate seam over the effect-driven attack queue.
        EffectDrivenAttack.TryOpenNextQueued(_context);

        return AttackAdvanceResult.Transitioned(AttackPhase.Completed, AttackPhase.None);
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

        if (AttackTargetSwitchGate.IsLocked(_context, attackerId))
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

        // AS-IS :536-545 — the hashtable payload {AttackingPermanent, DefendingPermanent, CardEffect, IsBlock};
        // carried in full on both emits below (no payload reduction).
        Dictionary<string, object?> switchMetadata = SwitchEventMetadata(causeEffectSourceId, newDefendingPermanentId, isBlock);

        // (C1b) AS-IS AttackProcess.cs:536-545 — the StackSkillInfos window hashtable, built ONCE (capturing the
        // permanents post-retarget) and SHARED by both emits below, exactly like AS-IS. CardEffect is null here:
        // both mirror callers (BlockTiming block-select, RaidAttackSwitch redirect) pass a null cause — the AS-IS
        // block path also passes null, and the card-redirect live-effect loss is the pre-existing RaidAttackSwitch
        // null-cause substrate gap (F1-ATC-EMIT-CENTRALIZE / RDW-05), NOT a C1b re-thread target (no card call site).
        var attackSwitchWindow = new System.Collections.Hashtable
        {
            { "AttackingPermanent", AttackingPermanent },
            { "DefendingPermanent", DefendingPermanent },
            { "CardEffect", null },
        };
        if (isBlock)
        {
            attackSwitchWindow.Add("IsBlock", isBlock); // AS-IS :544 — the IsBlock key present only on the block path.
        }

        // AS-IS :547-564 — block: suspend the blocker, then the [When Blocking] window (OnBlockAnyone). The gate
        // reads the ATTACKER (CanTriggerOnAttack over AttackingPermanent) — subject = the attacker.
        if (isBlock && newDefendingPermanentId is HeadlessEntityId suspendTarget)
        {
            SuspendPermanent(suspendTarget); // AS-IS SuspendPermanentsClass.Tap() (:557)
            TriggerEventEmitter.Emit(
                _context.GameEventQueue,
                TriggerTimings.OnBlock,
                actor: attack.AttackingPlayerId,
                subject: attackerId,
                extraMetadata: switchMetadata);

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
            TriggerEventEmitter.Emit(
                _context.GameEventQueue,
                TriggerTimings.OnAttackTargetChanged,
                actor: attack.AttackingPlayerId,
                subject: attackerId,
                extraMetadata: switchMetadata);

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

    // AS-IS SuspendPermanentsClass.Tap() — the raw metadata write the actions use today (design item RD9-87: route
    // through the sink SuspendKind so OnTappedAnyone / CanSuspend / DPWhenSuspended apply). The ATTACK tap carries
    // the AS-IS `{"IsAttack", true}` hashtable (AttackProcess.cs:160-163) — mirrored as the `suspendedByAttack`
    // marker; the blocker tap (SwitchDefender) carries the switch hashtable instead and sets no attack marker.
    private void SuspendPermanent(HeadlessEntityId id, bool byAttack = false)
    {
        if (_context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? record) && record is not null)
        {
            var metadata = new Dictionary<string, object?>(record.Metadata, StringComparer.Ordinal) { ["isSuspended"] = true };
            if (byAttack)
            {
                metadata["suspendedByAttack"] = true;
            }

            _context.CardInstanceRepository.Upsert(record with { Metadata = metadata });
        }
    }

    private int ReadStrike(HeadlessEntityId? attackerId)
    {
        if (attackerId is not HeadlessEntityId id ||
            !_context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? attacker) ||
            attacker is null)
        {
            return 1;
        }

        // (R1-b) number of security cards checked = AS-IS Permanent.Strike: a constant-1 seed folded LIVE with
        // every applicable IChangeSAttackEffect (bucketed UpToConstant → UpDownValue → DownToConstant), clamped
        // at 0 inside the getter (Permanent.Strike → Strike_AllowMinus). The metadata StrikeKey base is not an
        // AS-IS concept (AS-IS hardcodes the 1 seed); discarded per the R1-a base-discard precedent.
        return new Permanent(_context, id, attacker.OwnerId).Strike;
    }

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

    private static Dictionary<string, object?> AttackEventMetadata(HeadlessEntityId? attackEffectSourceId)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (attackEffectSourceId is HeadlessEntityId cause && !cause.IsEmpty)
        {
            metadata["attackCauseEffectId"] = cause.Value;
        }

        return metadata;
    }

    private Dictionary<string, object?> CounterEventMetadata(string pass)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [AutoProcessingTriggerCollector.CounterPassKey] = pass,
        };

        // AS-IS :99 — the counter gates evaluate over the DECLARATION-TIME cardSources snapshot.
        if (CounterSourcesSnapshot is { Count: > 0 } snapshot)
        {
            metadata["counterSourcesSnapshot"] = string.Join(",", snapshot.Select(id => id.Value));
        }

        return metadata;
    }

    private static Dictionary<string, object?> SwitchEventMetadata(
        HeadlessEntityId? causeEffectSourceId, HeadlessEntityId? newDefendingPermanentId, bool isBlock)
    {
        var metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["isBlock"] = isBlock,
        };
        if (newDefendingPermanentId is HeadlessEntityId defender && !defender.IsEmpty)
        {
            metadata["defendingPermanentId"] = defender.Value;
        }

        if (causeEffectSourceId is HeadlessEntityId cause && !cause.IsEmpty)
        {
            metadata["switchCauseEffectId"] = cause.Value;
        }

        return metadata;
    }
}
