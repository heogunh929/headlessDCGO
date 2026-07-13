// Source: DCGO/Assets/Scripts/Script/TurnStateMachine.cs
// (EFFECT-MODEL REBUILD) File at the AS-IS path Script/TurnStateMachine.cs; namespace kept ...CardEffectCommons
// so existing references are unaffected — namespace normalisation is a later, separate pass.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Bridge;
using HeadlessDCGO.Engine.Headless.Runtime;

/// <summary>(MIG6 goal-6 surface) Thin mirror of the original <c>TurnStateMachine</c> — the ONLY member card
/// effects reach on it is <c>.gameContext</c> (no non-gameContext <c>turnStateMachine.X</c> card call site
/// exists). The turn-flow logic itself (StartGame/SetMainPhase/EndTurn/EndGame) lives in the verified
/// substrate <c>GameFlowProcessor</c> / <c>HeadlessGameLoop</c> "temporary home" — re-housing it here is
/// high-risk / zero behavioral value (goal-3 lesson) and is deferred. This wrapper only reproduces the AS-IS
/// <c>GManager.instance.turnStateMachine.gameContext</c> access path so card ports mirror it mechanically.</summary>
public sealed class TurnStateMachine
{
    private readonly EngineContext _context;

    private TurnStateMachine(EngineContext context)
    {
        _context = context;
        gameContext = new GameContext(context);
    }

    /// <summary>The per-match <see cref="GameContext"/> (AS-IS <c>turnStateMachine.gameContext</c>).</summary>
    public GameContext gameContext { get; }

    /// <summary>(MIG6/rebuild) AS-IS <c>TurnStateMachine.DoneStartGame</c>: the initial setup sequence
    /// (mulligan + security deal) has completed, so triggered effects may fire (ICardEffect.CanTrigger gates on
    /// this). Headless proxy: a match is active and past the Setup/None phases — effect resolution only runs
    /// during live play, so this reads true throughout normal effect processing.</summary>
    public bool DoneStartGame =>
        _context.TurnController.Current.Phase is not HeadlessPhase.None and not HeadlessPhase.Setup;

    // (EFFECT-MODEL REBUILD / P2, design item P2-ISEXECUTING) AS-IS TurnStateMachine.isExecuting
    // (TurnStateMachine.cs:23, a plain mutable public bool). The foundation `ActivateICardEffectExtensionClass`
    // saves/restores it around an effect execution (read old -> set true -> ... -> restore). The mirror
    // TurnStateMachine is a per-access VIEW (a fresh instance on every `GManager.instance`), so a per-instance
    // field would not survive that save/restore across two `GManager.instance` reads. Backed by a match-scoped
    // box (keyed by EngineContext, same idea as CEntity_EffectControllerStore) so the flag is stable per match.
    // The mirror's async execution model does not depend on this re-entrancy guard (no live coroutine frame); it
    // exists only to reproduce the AS-IS save/restore verbatim.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EngineContext, System.Runtime.CompilerServices.StrongBox<bool>> _isExecutingStore = new();

    public bool isExecuting
    {
        get => _isExecutingStore.GetValue(_context, static _ => new System.Runtime.CompilerServices.StrongBox<bool>(false)).Value;
        set => _isExecutingStore.GetValue(_context, static _ => new System.Runtime.CompilerServices.StrongBox<bool>(false)).Value = value;
    }

    /// <summary>The per-context instance (AS-IS <c>GManager.instance.turnStateMachine</c>).</summary>
    public static TurnStateMachine For(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new TurnStateMachine(context);
    }
}
