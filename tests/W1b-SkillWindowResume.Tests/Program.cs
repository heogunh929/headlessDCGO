// (R3-W1b batch W1b) SkillWindowContinuation cursor externalisation + MultipleSkills.ResumeAsync +
// AutoProcessing.ResumeSuspendedWindowsAsync tests. Drives the DORMANT window loop SYNTHETICALLY with scripted
// SkillInfos (ActivateClass bodies) whose choice port / body suspends and resumes. Exercises the four approved
// resume seams:
//   (a) order-choice suspend records the loop cursor (phase/player/flags, stack intact, no in-flight pick);
//   (b) resume replays the recorded order answer, remaining skills resolve, the pass loop re-derives actives;
//   (c) a nested cut-in suspension resumes DEEPEST-FIRST (executingMultipleSkills chain);
//   (d) an in-flight BODY suspension does NOT double the once-use on resume (register-before-body idempotency).
// Harness: EngineContext.CreateDefault + TurnController.Initialize(P1,P2) + SetPhase(Main) so DoneStartGame is
// true and gameContext.TurnPlayer/NonTurnPlayer resolve (design §5.5 F4 DoneStartGame-gate finding), all driven
// under AmbientMatchContext.Enter (the GManager.instance the mirror window reads).

using System.Collections;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Runtime;
using HeadlessDCGO.Engine.Headless.Services;
// Disambiguate the AS-IS mirror SkillInfo from the Headless.Effects substrate SkillInfo record.
using SkillInfo = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.SkillInfo;

int failures = 0;
void Check(bool cond, string label)
{
    if (!cond) { Console.Error.WriteLine($"FAIL {label}"); failures++; }
    else { Console.WriteLine($"PASS {label}"); }
}

var P1 = new HeadlessPlayerId(1);
var P2 = new HeadlessPlayerId(2);

EngineContext NewContext()
{
    EngineContext ctx = EngineContext.CreateDefault();
    ctx.TurnController.Initialize(new[] { P1, P2 }, P1);   // turn player = P1
    ctx.TurnController.SetPhase(HeadlessPhase.Main);        // past Setup -> DoneStartGame true
    return ctx;
}

// A scripted activated skill: an ActivateClass whose body is the supplied closure. Off-field CardSource (no
// permanent), so CanActivate passes (no once-cap unless maxCount says so, no membership gate).
SkillInfo MakeSkill(EngineContext ctx, HeadlessEntityId cardId, Func<Hashtable, Task> body, int maxCount = 100)
{
    var card = new CardSource(ctx, cardId, P1);   // controller = owner = P1 (turn player)
    var effect = new ActivateClass();
    effect.SetUpICardEffect("scripted", canUseCondition: null, card);
    effect.SetUpActivateClass(
        canActivateCondition: null,
        activateCoroutine: body,
        maxCountPerTurn: maxCount,
        isOptional: false,
        effectDiscription: "scripted effect");
    return new SkillInfo(effect, new Hashtable(), EffectTiming.RulesTiming);
}

// A body-suspend: the AS-IS coroutine parking for an agent choice mid-resolution (DeferredChoicePendingException).
static Task SuspendBody()
{
    var candidate = new ChoiceCandidate(new HeadlessEntityId("x"), "x", ChoiceZone.Custom, IsSelectable: true, ownerId: new HeadlessPlayerId(1));
    var request = new ChoiceRequest(ChoiceType.Card, new HeadlessPlayerId(1), "suspend", 0, 1, canSkip: true, ChoiceZone.Custom, new[] { candidate });
    throw new DeferredChoicePendingException(request);
}

bool IsPending(Exception ex) => ex is WindowChoicePendingException or DeferredChoicePendingException;

// ============================================================================================================
// 0. Fresh-run sanity: the refactored fresh path still resolves a lone skill fully (dormancy / no regression).
// ============================================================================================================
{
    EngineContext ctx = NewContext();
    var ran = new List<string>();
    AutoProcessing ap = AutoProcessing.For(ctx);
    MultipleSkills ms = ap.availableMultipleSkills;
    SkillInfo s = MakeSkill(ctx, new HeadlessEntityId("solo"), _ => { ran.Add("solo"); return Task.CompletedTask; });

    using (AmbientMatchContext.Enter(ctx))
    {
        await ms.ActivateMultipleSkills(new List<SkillInfo> { s }, ap, false, null);
    }

    Check(ran.SequenceEqual(new[] { "solo" }), "0: fresh run resolves the lone skill body once");
    Check(!ms.IsUsing, "0: instance is released (IsUsing false) after a fresh run");
    Check(ms.Continuation.Phase == SkillWindowPhase.Done, "0: cursor is Done after a fresh run");
    Check(ap.executingMultipleSkills is null, "0: no in-use instance remains");
}

// ============================================================================================================
// (a) Order-choice suspend records the loop cursor. Two turn-player skills -> the pass offers an order choice ->
//     the AgentSkillWindowChoicePort suspends (WindowChoicePendingException) on first encounter.
// ============================================================================================================
MultipleSkills? savedMs = null;
EngineContext? savedCtx = null;
AutoProcessing? savedAp = null;
SkillInfo? skillA = null, skillB = null;
var abLog = new List<string>();
{
    EngineContext ctx = NewContext();
    AutoProcessing ap = AutoProcessing.For(ctx);
    MultipleSkills ms = ap.availableMultipleSkills;
    SkillInfo a = MakeSkill(ctx, new HeadlessEntityId("A"), _ => { abLog.Add("A"); return Task.CompletedTask; });
    SkillInfo b = MakeSkill(ctx, new HeadlessEntityId("B"), _ => { abLog.Add("B"); return Task.CompletedTask; });

    bool suspended = false;
    using (AmbientMatchContext.Enter(ctx))
    {
        try
        {
            await ms.ActivateMultipleSkills(new List<SkillInfo> { a, b }, ap, false, null);
        }
        catch (Exception ex) when (IsPending(ex))
        {
            suspended = true;
        }
    }

    Check(suspended, "a: two simultaneous skills suspend on the order choice");
    Check(ms.IsUsing, "a: the window instance stays in use (parked) after the suspend");
    Check(ms.Continuation.Phase == SkillWindowPhase.TurnPlayer, "a: cursor phase = TurnPlayer");
    Check(ms.Continuation.PhasePlayer is { } pp && pp.PlayerId == P1, "a: cursor player = the turn player P1");
    Check(ms.Continuation.ExternallySuspended, "a: ExternallySuspended is set by the port");
    Check(ms.Continuation.InFlightPick is null, "a: no in-flight body pick (the suspend is before Activate)");
    Check(ms.StackedSkillInfos.Count == 2, "a: both skills remain stacked (nothing removed yet)");
    Check(ap.executingMultipleSkills == ms, "a: this parked instance is the deepest in-use window");

    savedMs = ms; savedCtx = ctx; savedAp = ap; skillA = a; skillB = b;
}

// ============================================================================================================
// (b) Resume: record the order answer (pick B first, A2 key) on the continuation, then resume. The port replays
//     the recorded answer; B resolves, then the pass loop re-derives A as the lone active and resolves it.
// ============================================================================================================
{
    MultipleSkills ms = savedMs!;
    EngineContext ctx = savedCtx!;

    // A2 answer key = the offered active set (both skills, distinct cards -> ordinal 0 each). Record "pick B".
    var key = SkillWindowChoiceKey.ForOffered(new List<SkillInfo> { skillA!, skillB! });
    ms.Continuation.RecordAnswer(key, SkillWindowAnswer.Pick(new HeadlessEntityId("B"), 0));

    using (AmbientMatchContext.Enter(ctx))
    {
        await ms.ResumeAsync();
    }

    Check(abLog.SequenceEqual(new[] { "B", "A" }), $"b: replay picks B first, then the re-derived lone active A (got [{string.Join(",", abLog)}])");
    Check(!ms.IsUsing, "b: the window instance is released after both skills resolve");
    Check(ms.Continuation.Phase == SkillWindowPhase.Done, "b: cursor advances to Done");
    Check(savedAp!.executingMultipleSkills is null, "b: no in-use instance remains after resume");
}

// ============================================================================================================
// (c) Nested cut-in suspension -> deepest-first resume. The parent pick's body stacks a child trigger, so the
//     pass tail's TriggeredSkillProcess spawns a child window whose body suspends. Resume the deepest (child)
//     first; when it completes the parent resumes at its pass head.
// ============================================================================================================
{
    EngineContext ctx = NewContext();
    AutoProcessing ap = AutoProcessing.For(ctx);
    MultipleSkills parent = ap.availableMultipleSkills;

    int parentRuns = 0, childRuns = 0;
    SkillInfo child = MakeSkill(ctx, new HeadlessEntityId("child"), _ =>
    {
        childRuns++;
        if (childRuns == 1)
        {
            return SuspendBody();   // deepest window parks mid-body on its first run
        }

        return Task.CompletedTask;
    });
    SkillInfo parentSkill = MakeSkill(ctx, new HeadlessEntityId("parent"), _ =>
    {
        parentRuns++;
        // Stack a follow-up trigger so the pass tail's TriggeredSkillProcess opens a nested (child) window.
        ap.PutStackedSkill(child);
        return Task.CompletedTask;
    });

    bool suspended = false;
    using (AmbientMatchContext.Enter(ctx))
    {
        try
        {
            await parent.ActivateMultipleSkills(new List<SkillInfo> { parentSkill }, ap, false, null);
        }
        catch (Exception ex) when (IsPending(ex))
        {
            suspended = true;
        }
    }

    MultipleSkills? childInstance = ap.multipleSkills.FirstOrDefault(m => m != parent && m.IsUsing);

    Check(suspended, "c: the nested child window suspension unwinds to the top");
    Check(parent.IsUsing && childInstance is not null && childInstance.IsUsing, "c: both parent and child instances stay in use");
    Check(ap.executingMultipleSkills == childInstance, "c: executingMultipleSkills is the DEEPEST (child) window");
    Check(childInstance!.Continuation.InFlightPick is not null, "c: the child carries the in-flight body pick");
    Check(parent.Continuation.InFlightPick is null, "c: the parent has NO in-flight pick (suspended inside TriggeredSkillProcess)");
    Check(parentRuns == 1 && childRuns == 1, "c: parent body ran once, child body ran once before the suspend");

    using (AmbientMatchContext.Enter(ctx))
    {
        await ap.ResumeSuspendedWindowsAsync();
    }

    Check(childRuns == 2, "c: the child (deepest) body was REPLAYED on resume");
    Check(parentRuns == 1, "c: the parent body was NOT re-run (parent resumes at its pass head, body already done)");
    Check(!parent.IsUsing && !childInstance.IsUsing, "c: both instances are released after deepest-first resume");
    Check(ap.executingMultipleSkills is null, "c: no in-use instance remains after the chain drains");
}

// ============================================================================================================
// (d) In-flight body suspension -> no double once-use on resume. A lone skill (maxCountPerTurn=2) whose body
//     suspends on its first run; the register-use fired once (before the body). On resume the body replays but
//     the register must NOT fire again (the callback was cleared and is not re-armed) -> use count stays 1.
// ============================================================================================================
{
    EngineContext ctx = NewContext();
    AutoProcessing ap = AutoProcessing.For(ctx);
    MultipleSkills ms = ap.availableMultipleSkills;

    var cardId = new HeadlessEntityId("once");
    int bodyRuns = 0;
    SkillInfo s = MakeSkill(ctx, cardId, _ =>
    {
        bodyRuns++;
        if (bodyRuns == 1)
        {
            return SuspendBody();
        }

        return Task.CompletedTask;
    }, maxCount: 2);
    ICardEffect effect = s.CardEffect;

    bool suspended = false;
    using (AmbientMatchContext.Enter(ctx))
    {
        try
        {
            await ms.ActivateMultipleSkills(new List<SkillInfo> { s }, ap, false, null);
        }
        catch (Exception ex) when (IsPending(ex))
        {
            suspended = true;
        }
    }

    CEntity_EffectController controller = new CardSource(ctx, cardId, P1).cEntity_EffectController;

    Check(suspended, "d: the lone skill body suspends mid-resolution");
    Check(ms.Continuation.InFlightPick is not null, "d: the in-flight body pick is recorded");
    Check(controller.GetUseCountThisTurn(effect) == 1, "d: the once-use registered exactly once before the suspend");

    using (AmbientMatchContext.Enter(ctx))
    {
        await ms.ResumeAsync();
    }

    Check(bodyRuns == 2, "d: the in-flight body was replayed on resume");
    Check(controller.GetUseCountThisTurn(effect) == 1, "d: NO double once-use — the register did not re-fire on replay");
    Check(!controller.isOverMaxCountPerTurn(effect, 2), "d: use count is 1 (< 2), not the doubled 2");
    Check(!ms.IsUsing, "d: the window instance is released after the in-flight replay completes");
}

Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILURE(S)");
return failures == 0 ? 0 : 1;
