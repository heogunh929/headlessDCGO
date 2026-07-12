namespace HeadlessDCGO.Engine.Headless.Runtime;

using HeadlessDCGO.Engine.Headless.Bridge;
using ScriptAttackProcess = HeadlessDCGO.Engine.Assets.Scripts.Script.AttackProcess;

/// <summary>
/// (MIG1) Delegation shim over the AS-IS mirror <see cref="ScriptAttackProcess"/> — the attack state machine now
/// lives in the mirror layer (Assets/Scripts/Script/AttackProcess.cs, the 1:1 substrate translation of the AS-IS
/// AttackProcess), per the migration architecture: the mirror layer owns GAME LOGIC, Headless/ owns SUBSTRATE only
/// (docs/audit/asis_mirror_migration_design_2026-07-12.md).
///
/// This shim keeps the stable driving surface: <see cref="GameFlowProcessor"/> calls the virtual
/// <see cref="AdvanceAsync"/> once per loop iteration (one AS-IS <c>ProcessNextState()</c> step), and tests override
/// it for fault injection (GPT-#3). Each call resolves the per-context AttackProcess instance (AS-IS
/// <c>GManager.instance.attackProcess</c>) and delegates one step.
/// </summary>
public class AttackPipeline
{
    // Legacy per-attack counter-pass metadata keys — the pass progress now lives on the AttackProcess instance
    // (the AS-IS coroutine position); these consts remain for older tests that referenced them.
    public const string CounterPass1DoneKey = "counterPass1Done";
    public const string CounterPass2DoneKey = "counterPass2Done";

    /// <summary>The Execute end-of-attack self-delete flag key (forwarded — the logic lives in the mirror).</summary>
    public const string DeleteSelfAtEndOfAttackKey = ScriptAttackProcess.DeleteSelfAtEndOfAttackKey;

    private readonly BlockTiming? _blockTiming;
    private readonly BattleResolver? _battleResolver;
    private readonly SecurityResolver? _securityResolver;

    public AttackPipeline(
        BlockTiming? blockTiming = null,
        BattleResolver? battleResolver = null,
        SecurityResolver? securityResolver = null)
    {
        _blockTiming = blockTiming;
        _battleResolver = battleResolver;
        _securityResolver = securityResolver;
    }

    public virtual async Task<AttackAdvanceResult> AdvanceAsync(
        EngineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        ScriptAttackProcess attackProcess = ScriptAttackProcess.For(context, _blockTiming, _battleResolver, _securityResolver);
        return await attackProcess.ProcessNextState(cancellationToken).ConfigureAwait(false);
    }
}

public sealed record AttackAdvanceResult(
    bool Progressed,
    AttackPhase FromPhase,
    AttackPhase ToPhase,
    bool ChoiceRequested,
    bool BattleResolved,
    bool SecurityResolved,
    int EnqueuedEndAttackTriggers)
{
    public static AttackAdvanceResult Idle()
    {
        return new AttackAdvanceResult(
            Progressed: false,
            FromPhase: AttackPhase.None,
            ToPhase: AttackPhase.None,
            ChoiceRequested: false,
            BattleResolved: false,
            SecurityResolved: false,
            EnqueuedEndAttackTriggers: 0);
    }

    public static AttackAdvanceResult Transitioned(
        AttackPhase fromPhase,
        AttackPhase toPhase,
        bool choiceRequested = false,
        bool battleResolved = false,
        bool securityResolved = false,
        int enqueuedEndAttackTriggers = 0)
    {
        return new AttackAdvanceResult(
            Progressed: true,
            fromPhase,
            toPhase,
            choiceRequested,
            battleResolved,
            securityResolved,
            enqueuedEndAttackTriggers);
    }
}
