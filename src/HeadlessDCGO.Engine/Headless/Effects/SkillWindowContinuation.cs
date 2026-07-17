// (R3-W1b / design item RD-R3W1b-01 — window SkillInfo-currency cutover, batch W1)
// ============================================================================================================
// SUBSTRATE (ADAPTATION A1/A2 of docs/audit/window_skillinfo_cutover_design_2026-07-14.md §1.4/§4): the
// SkillInfo-currency window's suspend/resume state + choice port. This is the NEW loop's analogue of the
// existing (still-LIVE) Headless/Effects WindowContinuation / WindowResolutionController / AgentWindowChoicePort
// — built DORMANT alongside them, NOT replacing them. The MultipleSkills mirror (the AS-IS while(true) window
// loop) drives ONLY this port; nothing live consumes it yet (the cutover batch C wires the real resolution).
//
// Currency contrast (design §2 phenotype table):
//   OLD (live):  IWindowChoicePort keys by TimingWindowTrigger.Request.EffectId (registry currency).
//   NEW (this):  ISkillWindowChoicePort keys by (source card InstanceId, effect ordinal, offered order) — the
//                SkillInfo equivalent (ADAPTATION A2), so a resolved pick whose choice point vanishes from a
//                later pass is never re-asked (the AS-IS re-run-until-stable semantics, adversarial finding
//                P1-a carried over from AgentWindowChoicePort).
//
// The window's currency is the AS-IS mirror SkillInfo (`Cec.SkillInfo` = ICardEffect + Hashtable + EffectTiming);
// this file lives in namespace Headless.Effects, which ALSO defines a structurally-unrelated substrate
// `SkillInfo` record — so the mirror type is referenced through the `Cec` namespace alias throughout.
// The old WindowContinuation/WindowResolver/WindowResolverWiring/AgentWindowChoicePort files are UNTOUCHED.
// ============================================================================================================

namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
using Cec = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

/// <summary>(ADAPTATION A2) The stable identity of one order-choice point in the SkillInfo window: the set of
/// offered skills, each identified by its source card <see cref="HeadlessEntityId"/> and an ordinal (its
/// occurrence index among offered skills sharing that same card — the SkillInfo stand-in for the AS-IS
/// per-card effect ordinal). Order-independent: a re-run pass offers the same set until one resolves, so the
/// recorded answer replays for exactly that choice point.</summary>
public readonly record struct SkillWindowChoiceKey(string Offered)
{
    public static SkillWindowChoiceKey ForOffered(IReadOnlyList<Cec.SkillInfo> active)
    {
        var counts = new Dictionary<string, int>();
        var parts = new List<string>();
        foreach (Cec.SkillInfo skillInfo in active)
        {
            string card = skillInfo.CardEffect?.EffectSourceCard is { } source ? source.InstanceId.Value : "<null>";
            counts.TryGetValue(card, out int ordinal);
            counts[card] = ordinal + 1;
            parts.Add(card + "#" + ordinal);
        }

        parts.Sort(StringComparer.Ordinal);
        return new SkillWindowChoiceKey("O:" + string.Join(",", parts));
    }
}

/// <summary>(ADAPTATION A2) A recorded window-order answer — a picked (source card, ordinal) pair, or a
/// decline. Stored on <see cref="SkillWindowContinuation"/> and replayed on every re-drive of the same choice
/// point.</summary>
public readonly record struct SkillWindowAnswer(bool Declined, HeadlessEntityId CardInstanceId, int Ordinal)
{
    public static SkillWindowAnswer Decline => new(true, default, 0);

    public static SkillWindowAnswer Pick(HeadlessEntityId cardInstanceId, int ordinal) =>
        new(false, cardInstanceId, ordinal);
}

/// <summary>(R3-W1b batch W1b / A1) Which of the AS-IS <c>ActivateMultipleSkills</c> two-phase halves the
/// externalised loop cursor is in (turn player first, then non-turn player — AS-IS MultipleSkills.cs:51-52). The
/// AS-IS C# call stack held this as "which of the two <c>_OnePlayer</c> awaits is running"; a choice-port unwind
/// loses that stack, so the phase is externalised here (A1) and <see cref="MultipleSkills.ResumeAsync"/> re-enters
/// the recorded phase's pass loop. <c>Done</c> (default 0) = a never-run or fully-finished instance whose resume
/// is a no-op.</summary>
public enum SkillWindowPhase
{
    /// <summary>No phase in flight — the instance has not started or has finished both halves.</summary>
    Done = 0,

    /// <summary>The turn-player half (AS-IS first <c>_OnePlayer</c> call, MultipleSkills.cs:51).</summary>
    TurnPlayer,

    /// <summary>The non-turn-player half (AS-IS second <c>_OnePlayer</c> call, MultipleSkills.cs:52).</summary>
    NonTurnPlayer,
}

/// <summary>(design §2 / A1) The per-<c>MultipleSkills</c>-instance suspend state for the NEW window loop: the
/// stacked SkillInfo list(s), the in-flight pick carried across a suspend, the externally-suspended flag, and
/// the recorded-answer replay map keyed by <see cref="SkillWindowChoiceKey"/>. Mirrors what the live
/// <c>WindowContinuation</c> + <c>WindowResolutionController</c> hold for the registry-currency loop, in
/// SkillInfo currency.
/// <para>(batch W1b) It ALSO externalises the loop CURSOR — the pieces of the AS-IS
/// <c>ActivateMultipleSkills</c>/<c>_OnePlayer</c> call stack that must survive a choice-port unwind so a
/// suspended window can re-enter from the pass head (the pass loop is state-idempotent — every pass re-derives
/// <c>skillInfos_active</c> from live <c>CanActivate</c>, resolved picks are already removed from
/// <see cref="StackedSkillInfos"/>, and the order answer replays from the answer map). Cursor members audited
/// against <c>_OnePlayer</c>: the current-phase <see cref="PhasePlayer"/> survives an await; the
/// <see cref="CheckNewTriggeredSkillMainStack"/> flag and the (non-serialisable, held live in memory — A1)
/// <see cref="SkipCondition"/> Func survive; the not-yet-started other half is captured in
/// <see cref="PendingOtherPhaseSkillInfos"/>; the pass-local <c>_skillIndex</c> is NOT persisted (recomputed
/// every pass from the re-offered active set); an in-flight body pick + the optional-check flag it launched with
/// are <see cref="InFlightPick"/> / <see cref="InFlightIsCheckOptional"/>.</para>
/// DORMANT.</summary>
public sealed class SkillWindowContinuation
{
    /// <summary>(batch W1b, A1) The externalised phase cursor (turn/non-turn half). Set at
    /// <c>ActivateMultipleSkills</c> entry and advanced across the two halves; read by
    /// <c>MultipleSkills.ResumeAsync</c> to re-enter the correct half.</summary>
    public SkillWindowPhase Phase { get; set; } = SkillWindowPhase.Done;

    /// <summary>(batch W1b, A1) The current phase's <c>player</c> (the AS-IS <c>_OnePlayer</c> <c>player</c>
    /// parameter) — survives across the choice-port await, so it is externalised for the resume.</summary>
    public Cec.Player? PhasePlayer { get; set; }

    /// <summary>(batch W1b, A1) The not-yet-started NON-turn-player half's skill list, captured at
    /// <c>ActivateMultipleSkills</c> entry (AS-IS <c>NonTurnPlayerSkillInfos</c>). When the turn half suspends and
    /// later completes, the resume seeds the non-turn half from this — the AS-IS second <c>_OnePlayer</c> call.</summary>
    public List<Cec.SkillInfo> PendingOtherPhaseSkillInfos { get; set; } = new();

    /// <summary>(batch W1b, A1) The AS-IS <c>CheckNewTriggredSkill_mainStack</c> parameter — survives across the
    /// await (it selects the main-vs-cut-in <c>TriggeredSkillProcess</c> recursion branch and the
    /// <c>IsCutinEffect</c> gate).</summary>
    public bool CheckNewTriggeredSkillMainStack { get; set; }

    /// <summary>(batch W1b, A1) The AS-IS <c>skipCondition</c> parameter — a <see cref="Func{T1,T2,TResult}"/>
    /// that cannot be serialised but IS held live in memory across the suspend (the approved A1 in-memory
    /// continuation substrate).</summary>
    public Func<List<Cec.SkillInfo>, Cec.SkillInfo, bool>? SkipCondition { get; set; }

    /// <summary>(batch W1b) The <c>isCheckOptional</c> the <see cref="InFlightPick"/>'s body was launched with
    /// (the AS-IS <c>Activate(isCheckOptional)</c> argument) — persisted so the in-flight body REPLAY re-enters
    /// <c>Activate_Optional_Effect_Execute</c> with the identical optional-prompt decision.</summary>
    public bool InFlightIsCheckOptional { get; set; }

    /// <summary>(design §2) The per-instance stacked SkillInfo list — the AS-IS
    /// <c>MultipleSkills.StackedSkillInfos</c> frame the window re-evaluates each pass.</summary>
    public List<Cec.SkillInfo> StackedSkillInfos { get; set; } = new();

    /// <summary>(design §2) The skill being resolved across a suspend (the AS-IS in-flight pick whose body
    /// re-runs on resume — body-replay semantics owned by the reused <c>ResolveWithinCycleAsync</c> cycle).</summary>
    public Cec.SkillInfo? InFlightPick { get; set; }

    /// <summary>(design §2) Set while the window is parked awaiting an agent answer (the AS-IS coroutine's
    /// <c>WaitUntil(player.HasPlayerSelection())</c> suspension point, externalised — A1).</summary>
    public bool ExternallySuspended { get; set; }

    private readonly Dictionary<SkillWindowChoiceKey, SkillWindowAnswer> _answers = new();

    public bool TryGetAnswer(SkillWindowChoiceKey key, out SkillWindowAnswer answer) =>
        _answers.TryGetValue(key, out answer);

    public void RecordAnswer(SkillWindowChoiceKey key, SkillWindowAnswer answer) => _answers[key] = answer;

    public void ClearAnswers() => _answers.Clear();
}

/// <summary>(ADAPTATION A2) The SkillInfo window's order-choice port — the NEW-currency analogue of the live
/// <c>IWindowChoicePort</c>. <c>MultipleSkills</c> drives its order/optional pick through this; the concrete
/// <see cref="AgentSkillWindowChoicePort"/> record-replays the answer and suspends by throwing
/// <see cref="WindowChoicePendingException"/> (the same suspend contract the reused
/// <c>ActivatedEffectResolver.ResolveWithinCycleAsync</c> cycle already catches).</summary>
public interface ISkillWindowChoicePort
{
    /// <summary>Choose which of <paramref name="active"/> resolves first (AS-IS OpenSelectCardPanel /
    /// SelectHandEffect custom-mode order pick, _MaxCount:1). Returns the chosen index, or null when the
    /// controller declines (only allowed when <paramref name="canDecline"/>).</summary>
    Task<int?> ChooseOrderAsync(
        IReadOnlyList<Cec.SkillInfo> active,
        HeadlessPlayerId controller,
        bool canDecline,
        CancellationToken cancellationToken);
}

/// <summary>(Stage 5 / R3-W1b, DORMANT) The agent-driven <see cref="ISkillWindowChoicePort"/>: the SkillInfo
/// analogue of <see cref="AgentWindowChoicePort"/>. On the FIRST encounter of a distinct choice (keyed by
/// <see cref="SkillWindowChoiceKey"/>) it opens a <see cref="ChoiceType.WindowChoice"/> and throws
/// <see cref="WindowChoicePendingException"/> to suspend; on every re-drive it REPLAYS the recorded answer from
/// its <see cref="SkillWindowContinuation"/>. No live caller drives it yet — the cutover batch (C) records
/// answers on the continuation from the agent's ResolveChoice action (the parallel of
/// <c>WindowResolutionController</c>).</summary>
public sealed class AgentSkillWindowChoicePort : ISkillWindowChoicePort
{
    private readonly IHeadlessChoiceController _choiceController;
    private readonly SkillWindowContinuation _continuation;

    public AgentSkillWindowChoicePort(IHeadlessChoiceController choiceController, SkillWindowContinuation continuation)
    {
        _choiceController = choiceController ?? throw new ArgumentNullException(nameof(choiceController));
        _continuation = continuation ?? throw new ArgumentNullException(nameof(continuation));
    }

    public async Task<int?> ChooseOrderAsync(
        IReadOnlyList<Cec.SkillInfo> active,
        HeadlessPlayerId controller,
        bool canDecline,
        CancellationToken cancellationToken)
    {
        SkillWindowChoiceKey key = SkillWindowChoiceKey.ForOffered(active);

        if (_continuation.TryGetAnswer(key, out SkillWindowAnswer recorded))
        {
            if (recorded.Declined)
            {
                return null;
            }

            // Replay by (source card InstanceId, ordinal) — the pick's choice point may have shrunk, so a
            // vanished pick is simply not found and never re-asked (P1-a semantics).
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < active.Count; i++)
            {
                Cec.CardSource? source = active[i].CardEffect?.EffectSourceCard;
                if (source is null)
                {
                    continue;
                }

                string cardKey = source.InstanceId.Value;
                seen.TryGetValue(cardKey, out int ordinal);
                seen[cardKey] = ordinal + 1;

                if (source.InstanceId.Equals(recorded.CardInstanceId) && ordinal == recorded.Ordinal)
                {
                    return i;
                }
            }

            return null;
        }

        var counts = new Dictionary<string, int>();
        var candidates = new List<ChoiceCandidate>(active.Count);
        foreach (Cec.SkillInfo skillInfo in active)
        {
            Cec.CardSource? source = skillInfo.CardEffect?.EffectSourceCard;
            string cardKey = source is { } s ? s.InstanceId.Value : "<null>";
            counts.TryGetValue(cardKey, out int ordinal);
            counts[cardKey] = ordinal + 1;

            candidates.Add(new ChoiceCandidate(
                new HeadlessEntityId(cardKey + "#" + ordinal),
                skillInfo.CardEffect?.EffectName ?? "effect",
                ChoiceZone.Custom,
                IsSelectable: true,
                ownerId: controller));
        }

        var request = new ChoiceRequest(
            ChoiceType.WindowChoice,
            controller,
            "Multiple effects are triggered. Choose which to resolve first.",
            minCount: canDecline ? 0 : 1,
            maxCount: 1,
            canSkip: canDecline,
            ChoiceZone.Custom,
            candidates.ToArray());

        // (R4 S3a, decision 3 = B) AWAIT-mode: on the pump stack the order pick parks the pump IN PLACE (the
        // AS-IS in-coroutine OpenSelectCardPanel wait). The window's C# stack survives, so NO answer is
        // recorded on the continuation and NO resume/re-entry happens — the deposited pick maps straight back
        // to the offered index (each candidate id is "cardInstanceId#ordinal", generated above in order).
        if (Runtime.TurnFlowPumpHost.FindExecuting() is { } pumpHost)
        {
            _choiceController.RequestChoice(request, new HeadlessEntityId(key.Offered));
            pumpHost.MarkPumpChoice();
            await pumpHost.Gate.WaitUntilAsync(() => pumpHost.HasDepositedAnswer).ConfigureAwait(false);
            Choices.ChoiceResult deposited = pumpHost.TakeDepositedAnswer();
            if (deposited.IsSkipped || deposited.SelectedIds.Count == 0)
            {
                return null;
            }

            string picked = deposited.SelectedIds[0].Value;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Id.Value == picked)
                {
                    return i;
                }
            }

            return null;
        }

        _choiceController.RequestChoice(request, new HeadlessEntityId(key.Offered));
        _continuation.ExternallySuspended = true;
        throw new WindowChoicePendingException(key.Offered);
    }
}
