// Source: Assets/Scripts/Script/MultipleSkills.cs
// Decision: PORT
// Category: CardEffect
// Migration: AS-IS mirror (R3-W1b, batch W1) — the re-entrant trigger WINDOW loop, built DORMANT.
// Namespace hint: HeadlessDCGO.Engine.Assets.Scripts.Script
//
// ============================================================================================================
// (R3-W1b / RD-R3W1b-01) 1:1 mirror of AS-IS MultipleSkills.ActivateMultipleSkills /
// ActivateMultipleSkills_OnePlayer (MultipleSkills.cs:21-423): the collect → turn/non-turn split →
// while(true) re-evaluate → order-select → optional-confirm → resolve → RuleProcess → TriggeredSkillProcess
// cut-in recursion window loop, in SkillInfo currency. This REPLACES the RD-R3-01 STOP stub.
//
// DORMANT: no live caller reaches this loop. It is entered only via AutoProcessing.TriggeredSkillProcess ->
// availableMultipleSkills.ActivateMultipleSkills, and TriggeredSkillProcess's drain body is gated on a
// NON-EMPTY StackedSkillInfos; the three live cut-in callers (CardController.cs:2623/2887/3258) run over an
// EMPTY cut-in stack in every currently-exercised scenario (0 ported cards return Before/AfterPayCost cut-in
// skills), so the loop body is never reached. The live window today is still WindowResolver (untouched).
//
// SUBSTRATE TRANSLATION (task-approved, per design §1.4/§4):
//  - IEnumerator -> async Task; ContinuousController.StartCoroutine(X)/yield return -> await X.
//  - MonoBehaviourPunCallbacks / photonView.RPC("SetTargetSkill", ...) / SetTargetSkill PunRPC /
//    player.QueuePlayerSelection/HasPlayerSelection/DequeuePlayerSelection: the whole local-player UI +
//    network round-trip that computes and syncs `skillIndex` collapses to ONE call to the SkillInfo choice
//    port (ADAPTATION A2 — ISkillWindowChoicePort.ChooseOrderAsync); it returns the picked index (or null =
//    decline). The AS-IS default (`valueSelection != null ? ValueAsInt() : 0`) is subsumed: the port returns
//    the chosen index directly and null maps to -1 (the AS-IS skip sentinel the Blast/normal branches set).
//  - player.securityObject.securityBreakGlass.* (oldIsSecurityGlassBlue and its SetActive/ShowBlueMatarial
//    guards, AS-IS :169/:195-198/:348-351/:390-392), GManager.commandText.*, player.FieldPermanentObjects,
//    selectHandEffect.SetUpCustomMessage/SetNotShow*/SetIsLocal, IsAI branch: all UI — stripped.
//  - AutomaticOrder.GetSkillIndexAutomaticOrder (AS-IS :304) + ContinuousController.autoEffectOrder: a
//    client-side auto-order setting with NO mirror -> the order pick always routes to the port (substrate note).
//  - CardSource.Owner is a HeadlessPlayerId; gameContext.TurnPlayer/NonTurnPlayer are mirror Players -> the
//    turn/non-turn split compares Owner == player.PlayerId (ADAPTATION). card.PermanentOfThisCard() ->
//    ICardEffect.ResolvePermanentOfThisCard (established adaptation (2)). attackProcess.SecurityDigimon is an
//    instance id -> compare card.InstanceId. turnStateMachine.endGame has no mirror -> terminal via
//    Context.RuleQueryService.IsTerminal().
// The Blast-branch (SelectHandEffect custom mode) vs normal-branch (selectCardPanel) DECISION LOGIC is mirrored
// 1:1; only the UI selection inside each branch is routed through the port.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Assets.Scripts.Script;

using System.Collections;
using System.Threading;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Commons = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.CardEffectCommons;
// Disambiguate the AS-IS mirror SkillInfo from the same-import substrate `Headless.Effects.SkillInfo` record.
using SkillInfo = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.SkillInfo;

public sealed class MultipleSkills
{
    private readonly EngineContext _context;

    // (design §2 / A1) the per-instance suspend state (stacked list, in-flight pick, externally-suspended flag,
    // recorded-answer replay). The AS-IS `StackedSkillInfos` field IS this continuation's list (below).
    private readonly SkillWindowContinuation _continuation = new();
    private readonly ISkillWindowChoicePort _choicePort;

    public MultipleSkills(EngineContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        // (ADAPTATION A1/A2, DORMANT) the SkillInfo-currency choice port over this instance's continuation. Not
        // driven live yet — the cutover batch (C) records answers on the continuation from the agent's action.
        _choicePort = new AgentSkillWindowChoicePort(context.ChoiceController, _continuation);
    }

    // AS-IS MultipleSkills.cs:10.
    public bool IsUsing { get; private set; } = false;

    AutoProcessing _autoProcessing = null;

    // (batch W1b) The per-instance suspend state + externalised loop cursor. Exposed so the cutover seam (batch C)
    // can RECORD the agent's order answer on it (the AS-IS-key A2 replay) before driving <see cref="ResumeAsync"/>,
    // and so the drain-level resumer can read the cursor. The old WindowResolutionController is the live analogue.
    public SkillWindowContinuation Continuation => _continuation;

    // (batch W1b) The AutoProcessing whose TriggeredSkillProcess spawned this window (AS-IS the `autoProcessing`
    // argument threaded into ActivateMultipleSkills). Determines WHOSE TriggeredSkillProcess tail
    // (StackSkillInfos(null, AfterEffectsActivate)) the resumer replays after this instance completes.
    public AutoProcessing OwningAutoProcessing => _autoProcessing;

    // AS-IS MultipleSkills.cs:12.
    public List<SkillInfo> SkillInfos_used { get; private set; } = new List<SkillInfo>();

    // AS-IS MultipleSkills.cs:13 — the stacked skills the window re-evaluates each pass. Backed by the
    // continuation (design §2) so the suspend state genuinely holds it; the AS-IS field name/behaviour is intact.
    public List<SkillInfo> StackedSkillInfos
    {
        get => _continuation.StackedSkillInfos;
        set => _continuation.StackedSkillInfos = value;
    }

    // AS-IS MultipleSkills.cs:14-15.
    public bool IsOnlyHandEffectStacked => StackedSkillInfos.Every(skillInfo =>
        skillInfo.CardEffect != null && skillInfo.CardEffect.EffectSourceCard != null && Commons.IsExistOnHand(skillInfo.CardEffect.EffectSourceCard) && skillInfo.CardEffect.EffectDiscription.Contains("[Hand]"));

    // AS-IS MultipleSkills.cs:17.
    bool IsOnlyOptionalEffectStacked => StackedSkillInfos.Every(skillInfo => skillInfo.CardEffect != null && skillInfo.CardEffect.IsSkippable(null));

    // AS-IS MultipleSkills.cs:18-20.
    bool IsEachStackedEffectHasDistinctSourceCard => StackedSkillInfos.Filter(skillInfo1 => skillInfo1.CardEffect != null && skillInfo1.CardEffect.EffectSourceCard != null)
        .Every(skillInfo => StackedSkillInfos.Filter(skillInfo1 => skillInfo1.CardEffect != null && skillInfo1.CardEffect.EffectSourceCard != null)
            .Count(otherSkillInfo => otherSkillInfo != skillInfo && skillInfo.CardEffect.EffectSourceCard == otherSkillInfo.CardEffect.EffectSourceCard) == 0);

    // AS-IS MultipleSkills.cs:21-61 — the ActivateMultipleSkills entry. Partition into the turn/non-turn halves,
    // externalise the loop cursor (batch W1b), then run the phases. Fresh-run and resume share ONE implementation
    // (RunPhasesAsync): a fresh run is "resume from the initial cursor" (Phase=TurnPlayer, both halves seeded).
    public async Task ActivateMultipleSkills(List<SkillInfo> skillInfos, AutoProcessing autoProcessing, bool CheckNewTriggredSkill_mainStack, Func<List<SkillInfo>, SkillInfo, bool> skipCondition)
    {
        _skillIndex = 0;
        IsUsing = true;

        SkillInfos_used = new List<SkillInfo>();
        StackedSkillInfos = new List<SkillInfo>();

        _autoProcessing = autoProcessing;

        List<SkillInfo> TurnPlayerSkillInfos = new List<SkillInfo>();
        List<SkillInfo> NonTurnPlayerSkillInfos = new List<SkillInfo>();

        foreach (SkillInfo skillInfo in skillInfos)
        {
            if (skillInfo != null)
            {
                ICardEffect cardEffect = skillInfo.CardEffect;

                if (cardEffect.EffectSourceCard != null)
                {
                    // ADAPTATION: AS-IS compares CardSource.Owner (a Player) to gameContext.TurnPlayer; the mirror
                    // Owner is a HeadlessPlayerId and TurnPlayer is a mirror Player -> compare against PlayerId.
                    if (GManager.instance.turnStateMachine.gameContext.TurnPlayer is { } turnPlayer && cardEffect.EffectSourceCard.Owner == turnPlayer.PlayerId)
                    {
                        TurnPlayerSkillInfos.Add(skillInfo);
                    }

                    else if (GManager.instance.turnStateMachine.gameContext.NonTurnPlayer is { } nonTurnPlayer && cardEffect.EffectSourceCard.Owner == nonTurnPlayer.PlayerId)
                    {
                        NonTurnPlayerSkillInfos.Add(skillInfo);
                    }
                }
            }
        }

        // (batch W1b, A1) Externalise the loop cursor BEFORE running so any suspend inside a phase is resumable
        // from its pass head. The turn half seeds immediately below; the non-turn half is captured (AS-IS the
        // second _OnePlayer argument) and seeded only when the turn half completes.
        _continuation.CheckNewTriggeredSkillMainStack = CheckNewTriggredSkill_mainStack;
        _continuation.SkipCondition = skipCondition;
        _continuation.Phase = SkillWindowPhase.TurnPlayer;
        _continuation.PhasePlayer = GManager.instance.turnStateMachine.gameContext.TurnPlayer;
        _continuation.PendingOtherPhaseSkillInfos = NonTurnPlayerSkillInfos;
        _continuation.InFlightPick = null;

        await RunPhasesAsync(TurnPlayerSkillInfos, freshCurrentPhase: true, CancellationToken.None);
    }

    // (batch W1b) Re-entry after a suspend — DORMANT (no live caller; the C cutover wires it: the seam records the
    // order answer on the continuation, then drives the drain-level AutoProcessing.ResumeSuspendedWindowsAsync,
    // which calls this). Continues from the PERSISTED cursor: the current half is NOT re-seeded (its
    // StackedSkillInfos already holds the remaining stack), while a not-yet-started other half seeds fresh. If a
    // body pick was in flight it replays first (see RunOnePlayerAsync); otherwise the pass head re-runs and the
    // choice port replays the recorded answer.
    public Task ResumeAsync(CancellationToken cancellationToken = default)
        => RunPhasesAsync(currentPhaseSeed: null, freshCurrentPhase: false, cancellationToken);

    // (batch W1b) The single fresh/resume driver — AS-IS ActivateMultipleSkills:51-60 (the two _OnePlayer calls +
    // the tail). A fresh run enters with Phase=TurnPlayer and both halves seeded; a resume enters at the persisted
    // Phase with the current half NOT re-seeded (StackedSkillInfos persists) and the other half seeded from
    // PendingOtherPhaseSkillInfos. The AS-IS pass body itself (PassLoopAsync) is unchanged — this is control-flow
    // refactoring around it so a suspend that unwinds the C# call stack re-enters at the recorded cursor.
    async Task RunPhasesAsync(List<SkillInfo> currentPhaseSeed, bool freshCurrentPhase, CancellationToken cancellationToken)
    {
        // AS-IS :51 — turn-player half. Entered on a fresh run, or a resume whose cursor is still in the turn half.
        if (_continuation.Phase == SkillWindowPhase.TurnPlayer)
        {
            await RunOnePlayerAsync(_continuation.PhasePlayer, currentPhaseSeed, freshCurrentPhase, cancellationToken);

            // AS-IS :52 — advance to the non-turn half, seeded fresh from the captured list.
            _continuation.Phase = SkillWindowPhase.NonTurnPlayer;
            _continuation.PhasePlayer = GManager.instance.turnStateMachine.gameContext.NonTurnPlayer;
            currentPhaseSeed = _continuation.PendingOtherPhaseSkillInfos;
            freshCurrentPhase = true;
        }

        // AS-IS :52 — non-turn-player half.
        if (_continuation.Phase == SkillWindowPhase.NonTurnPlayer)
        {
            await RunOnePlayerAsync(_continuation.PhasePlayer, currentPhaseSeed, freshCurrentPhase, cancellationToken);
            _continuation.Phase = SkillWindowPhase.Done;
        }

        // AS-IS ActivateMultipleSkills:59-60 tail.
        SkillInfos_used = new List<SkillInfo>();

        IsUsing = false;
    }

    int _skillIndex;

    // AS-IS MultipleSkills.cs:65 — a cut-in effect = a non-main-stack drain on the CUT-IN AutoProcessing instance.
    bool IsCutinEffect(bool CheckNewTriggredSkill_mainStack) => !CheckNewTriggredSkill_mainStack && _autoProcessing != GManager.instance.autoProcessing;

    // AS-IS MultipleSkills.cs:67-73 — seed the phase stack (fresh only), then run the pass loop. On a RESUME
    // (fresh:false) the stack already holds the persisted remaining skills, so an in-flight body pick replays
    // first (finishing its pass tail) and then the pass head re-runs; an order-choice / cut-in suspend has no
    // in-flight pick and simply re-enters the pass head (the port replays the recorded answer).
    async Task RunOnePlayerAsync(Player player, List<SkillInfo> seed, bool fresh, CancellationToken cancellationToken)
    {
        if (fresh)
        {
            StackedSkillInfos = new List<SkillInfo>();

            foreach (SkillInfo skillInfo in seed)
            {
                StackedSkillInfos.Add(skillInfo);
            }
        }
        else
        {
            _continuation.ExternallySuspended = false;

            if (_continuation.InFlightPick is SkillInfo inflight)
            {
                _continuation.InFlightPick = null;

                bool terminal = await ResumeInFlightPassAsync(inflight, _continuation.InFlightIsCheckOptional, player, cancellationToken);
                if (terminal)
                {
                    return;
                }
            }
        }

        await PassLoopAsync(player, cancellationToken);
    }

    // (batch W1b) Replay a suspended BODY pick, then finish ITS pass tail, before re-entering the loop head. The
    // pick was already removed from StackedSkillInfos before it suspended (AS-IS Activate :360), so nothing
    // re-removes it, and the register-use callback is NOT re-armed (RunPickBodyAsync freshPick:false).
    async Task<bool> ResumeInFlightPassAsync(SkillInfo pick, bool isCheckOptional, Player player, CancellationToken cancellationToken)
    {
        await RunPickBodyAsync(pick, isCheckOptional, freshPick: false, cancellationToken);

        return await PassTailAsync(cancellationToken);
    }

    // AS-IS MultipleSkills.cs:75-397 — the per-phase while(true) pass loop, verbatim except: the local coroutine
    // `Activate` is hoisted to RunPickBodyAsync (so the resume path can replay it), and the post-pick tail
    // (RuleProcess -> terminal -> TriggeredSkillProcess) is hoisted to PassTailAsync (shared with the in-flight
    // replay). CheckNewTriggredSkill_mainStack / skipCondition now read from the externalised cursor.
    async Task PassLoopAsync(Player player, CancellationToken cancellationToken)
    {
        while (true)
        {
            List<SkillInfo> skillInfos_active = new List<SkillInfo>();

            foreach (SkillInfo skillInfo in StackedSkillInfos)
            {
                #region set the flag whether it is Digimon's effect or Tamer's effect
                if (skillInfo.CardEffect != null)
                {
                    if (skillInfo.CardEffect.EffectSourceCard != null)
                    {
                        CardSource card = skillInfo.CardEffect.EffectSourceCard;

                        if (card != null)
                        {
                            if (!skillInfo.CardEffect.IsDigimonEffect)
                            {
                                if (skillInfo.CardEffect.IsInheritedEffect || skillInfo.CardEffect.IsLinkedEffect)
                                {
                                    skillInfo.CardEffect.SetIsDigimonEffect(true);
                                }
                                else
                                {
                                    // ADAPTATION(2): AS-IS `card.PermanentOfThisCard()` -> ResolvePermanentOfThisCard.
                                    Permanent permanentOfThisCard = ICardEffect.ResolvePermanentOfThisCard(card);

                                    if (permanentOfThisCard != null)
                                    {
                                        skillInfo.CardEffect.SetIsDigimonEffect(permanentOfThisCard.IsDigimon);
                                        skillInfo.CardEffect.SetIsTamerEffect(permanentOfThisCard.IsTamer);
                                    }

                                    else
                                    {
                                        skillInfo.CardEffect.SetIsTamerEffect(card.IsTamer);
                                    }

                                    // ADAPTATION(gap1 SECURITYDIGIMON): AS-IS `card == attackProcess.SecurityDigimon`
                                    // compares CardSources; the mirror SecurityDigimon is the card INSTANCE id.
                                    if (GManager.instance.attackProcess.SecurityDigimon is { } securityDigimon && card.InstanceId == securityDigimon)
                                    {
                                        skillInfo.CardEffect.SetIsDigimonEffect(true);
                                    }
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Check if the effect can be activated
                if (!skillInfo.CardEffect.CanActivate(skillInfo.Hashtable))
                {
                    continue;
                }

                if (_continuation.SkipCondition != null)
                {
                    if (_continuation.SkipCondition(_autoProcessing.skillInfos_used, skillInfo))
                    {
                        continue;
                    }
                }

                if (skillInfo.CardEffect.ChainActivations > 0)
                {
                    if (GManager.instance.autoProcessing.IsCutInEffectUsedMaxCount(skillInfo.CardEffect))
                    {
                        continue;
                    }
                }

                if (IsCutinEffect(_continuation.CheckNewTriggeredSkillMainStack))
                {
                    if (GManager.instance.autoProcessing.IsCutInEffectHasUsed(skillInfo.CardEffect))
                    {
                        continue;
                    }
                }

                skillInfos_active.Add(skillInfo);
                #endregion
            }

            skillInfos_active = skillInfos_active.Filter(skillInfo => skillInfo != null && skillInfo.CardEffect != null
                && skillInfo.CardEffect.CanActivate(skillInfo.Hashtable) && skillInfo.CardEffect.EffectSourceCard != null);

            if (skillInfos_active.Count > 0)
            {
                // AS-IS :169 `oldIsSecurityGlassBlue` (player.securityObject.securityBreakGlass.IsBlueGlass && ...)
                // only gates UI (SetActive / ShowBlueMatarial at :195-198/:348-351/:390-392) — stripped.

                #region If the effect list is one, process normally.
                if (skillInfos_active.Count == 1)
                {
                    _skillIndex = 0;

                    await ActivatePickAsync(skillInfos_active, true, cancellationToken);
                }
                #endregion

                #region If there are multiple effect lists, select which one to process first
                else
                {
                    // Blast Digivolution vs normal DECISION LOGIC (AS-IS :187 vs :264), 1:1. The UI selection in
                    // each branch (SelectHandEffect custom mode / selectCardPanel) is routed through the port.
                    bool canDecline;

                    if (IsOnlyHandEffectStacked && IsOnlyOptionalEffectStacked && IsEachStackedEffectHasDistinctSourceCard)
                    {
                        // AS-IS :187-262 Blast branch: SelectHandEffect custom mode, canNoSelect: true (:208).
                        canDecline = true;
                    }

                    else
                    {
                        // AS-IS :264-324 normal branch: selectCardPanel, _CanNoSelect = all skippable (:284).
                        // (AutomaticOrder / autoEffectOrder has no mirror — always route to the port; substrate note.)
                        canDecline = skillInfos_active.All(skillInfo => skillInfo.CardEffect.IsSkippable(skillInfo.Hashtable));
                    }

                    // AS-IS :326-329: WaitUntil(HasPlayerSelection) + DequeuePlayerSelection<ValueSelection>() ->
                    // the port returns the picked index (or null = decline, the AS-IS -1 sentinel). A first
                    // encounter throws WindowChoicePendingException to suspend (the port already set
                    // ExternallySuspended); the externalised cursor (phase/player/flags, set at entry) is already
                    // recorded, so a resume re-enters this same pass head and the port replays the recorded answer.
                    HeadlessPlayerId controller = player is { } p ? p.PlayerId : default;
                    int? pick = await _choicePort.ChooseOrderAsync(skillInfos_active, controller, canDecline, cancellationToken);
                    _skillIndex = pick ?? -1;

                    await ActivatePickAsync(skillInfos_active, !(IsOnlyHandEffectStacked && IsOnlyOptionalEffectStacked && IsEachStackedEffectHasDistinctSourceCard), cancellationToken);
                }
                #endregion

                bool terminal = await PassTailAsync(cancellationToken);
                if (terminal)
                {
                    return;
                }
            }

            else
            {
                break;
            }
        }
    }

    // AS-IS MultipleSkills.cs:340-355 (the Activate local, bounds + un-stack half): guard the picked index, then
    // remove the picked skill from the stack and run its body (RunPickBodyAsync). A decline (_skillIndex < 0, the
    // AS-IS -1 sentinel) clears the whole stack and ends this player's window.
    async Task ActivatePickAsync(List<SkillInfo> skillInfos_active, bool isCheckOptional, CancellationToken cancellationToken)
    {
        // AS-IS :342-346 — bounds guard (note the AS-IS `Count < _skillIndex` quirk, preserved verbatim).
        if (_skillIndex < 0 || skillInfos_active.Count < _skillIndex)
        {
            StackedSkillInfos = new List<SkillInfo>();
            return;
        }

        // AS-IS :348-351 securityBreakGlass SetActive = UI (stripped).

        SkillInfo pick = skillInfos_active[_skillIndex];

        StackedSkillInfos.Remove(pick);

        await RunPickBodyAsync(pick, isCheckOptional, freshPick: true, cancellationToken);
    }

    // AS-IS MultipleSkills.cs:356-392 (the Activate local, register + execute half). Hoisted so the RESUME path
    // can replay a suspended body. `freshPick` distinguishes the first run (arm the register-use OnProcessCallbuck,
    // AS-IS :358-362) from an in-flight REPLAY (do NOT re-arm — the pick's ICardEffect object persists across the
    // suspend, so its callback is already fired-and-cleared or still armed; re-arming would double the once-use,
    // because RegisterUseEffectThisTurn is a plain per-turn list add, NOT cycle-journaled). The body runs through
    // the VERIFIED per-resolution cycle (ActivatedEffectResolver.ResolveWithinCycleAsync — extracted at 394a8402
    // for exactly this reuse: the deferred-choice answer replay + the OnceFlags register-before-body transaction +
    // the single sink flush, A1). A body suspend records the in-flight pick so the drain resumer replays it.
    async Task RunPickBodyAsync(SkillInfo pick, bool isCheckOptional, bool freshPick, CancellationToken cancellationToken)
    {
        ICardEffect cardEffect = pick.CardEffect;
        Hashtable hashtable = pick.Hashtable;

        if (freshPick)
        {
            pick.CardEffect.SetOnProcessCallbuck(() =>
                {
                    SkillInfos_used.Add(pick);
                    cardEffect.EffectSourceCard.cEntity_EffectController.RegisterUseEffectThisTurn(cardEffect);
                });
        }

        if (cardEffect is ActivateICardEffect)
        {
            if (cardEffect.CanActivate(hashtable))
            {
                var sink = new MatchStateMutationSink(
                    _context.CardInstanceRepository, _context.LogSink, _context.ZoneMover, _context.MemoryController,
                    _context.EffectRegistry, _context.GameEventQueue, context: _context);

                try
                {
                    await ActivatedEffectResolver.ResolveWithinCycleAsync(
                        _context,
                        sink,
                        async () =>
                        {
                            // For interrupt processing
                            if (IsCutinEffect(_continuation.CheckNewTriggeredSkillMainStack))
                            {
                                // AS-IS :371 `// GManager.instance.autoProcessing.AddCutinEffect(cardEffect);` — the
                                // add is instead passed as the useEffectCallback below (AS-IS :377).
                                await ((ActivateICardEffect)cardEffect)
                                    .Activate_Optional_Effect_Execute(
                                        hashtable,
                                        isCheckOptional,
                                        useEffectCallback: GManager.instance.autoProcessing.AddCutinEffect);
                            }

                            else
                            {
                                await GManager.instance.autoProcessing.ActivateEffectProcess(
                                    cardEffect,
                                    hashtable,
                                    isCheckOptional);
                            }

                            return 0;
                        },
                        cancellationToken);
                }
                catch (Exception ex) when (ex is WindowChoicePendingException or DeferredChoicePendingException)
                {
                    // The body paused for an agent choice; the once-use may already be consumed (callback fired).
                    // Record the in-flight pick + the optional flag so the resumer replays THIS body (freshPick:
                    // false) and re-throw so the pending exception unwinds to the seam, same as a fresh suspend.
                    _continuation.InFlightPick = pick;
                    _continuation.InFlightIsCheckOptional = isCheckOptional;
                    throw;
                }
            }
        }

        // AS-IS :390-392 securityBreakGlass ShowBlueMatarial = UI (stripped).
    }

    // AS-IS MultipleSkills.cs:398-415 — the post-pick pass tail: rule processing, terminal check, then the
    // newly-triggered-effect recursion (main vs cut-in branch). Returns true when the phase must end (terminal).
    // Shared by the normal pass and the in-flight replay.
    async Task<bool> PassTailAsync(CancellationToken cancellationToken)
    {
        //Rule processing (AS-IS :398)
        await GManager.instance.autoProcessing.RuleProcess();

        // AS-IS :400-403 `if (turnStateMachine.endGame) yield break;` — ADAPTATION: the mirror
        // TurnStateMachine has no `endGame`; terminal state reads through RuleQueryService.
        if (GManager.instance.Context.RuleQueryService.IsTerminal())
        {
            return true;
        }

        #region If there are any newly triggered effects, resolve those first. (AS-IS :405-415)
        if (!_continuation.CheckNewTriggeredSkillMainStack)
        {
            await _autoProcessing.TriggeredSkillProcess(_continuation.CheckNewTriggeredSkillMainStack, _continuation.SkipCondition);
        }

        else
        {
            await GManager.instance.autoProcessing.TriggeredSkillProcess(_continuation.CheckNewTriggeredSkillMainStack, null);
        }
        #endregion

        return false;
    }
}
