namespace HeadlessDCGO.Engine.Headless.Bridge;

/// <summary>
/// (EFFECT-MODEL REBUILD, substrate) The headless replacement for Unity's global <c>GManager.instance</c>
/// singleton. The AS-IS effect object model reads game state through the process-global <c>GManager.instance</c>
/// (a scene-component singleton in the AS-IS engine); a 1:1 mirror of that model therefore needs an ambient handle to "the current
/// match" so ported effect code can read <c>GManager.instance.turnStateMachine.gameContext...</c> almost
/// verbatim — WITHOUT threading an <see cref="EngineContext"/> parameter into every effect (which AS-IS does
/// not do).
///
/// Unlike Unity's truly-global singleton, this is an <see cref="AsyncLocal{T}"/> so it is MATCH-SCOPED: the
/// engine sets it (via <see cref="Enter"/>) at the start of a match operation and the value flows down the
/// async call chain, so N concurrent matches (RuleAudit's 20 games, parallel tests) never see each other's
/// context. This is the game-logic-agnostic substrate the mirror <c>GManager</c> reads from.
/// </summary>
public static class AmbientMatchContext
{
    private static readonly AsyncLocal<EngineContext?> _current = new();

    /// <summary>The context of the match currently executing on this async flow, or null outside any match
    /// operation. The mirror <c>GManager.instance</c> resolves from here.</summary>
    public static EngineContext? Current => _current.Value;

    /// <summary>Whether an ambient context is set (mirrors AS-IS <c>GManager.instance != null</c>).</summary>
    public static bool HasContext => _current.Value is not null;

    /// <summary>The current context, or throw when none is set (a mirror effect ran outside a match scope —
    /// a wiring bug, surfaced loudly rather than silently reading stale/global state).</summary>
    public static EngineContext Require() =>
        _current.Value ?? throw new InvalidOperationException(
            "No ambient match context is set on this async flow. A mirror effect (GManager.instance.*) ran " +
            "outside AmbientMatchContext.Enter(context). The engine must scope the current match before " +
            "resolving effects.");

    /// <summary>Scope <paramref name="context"/> as the current match for the duration of the returned scope
    /// (and everything it awaits). Restores the previous value on dispose, so nested enters (a sub-operation on
    /// another context) unwind correctly.</summary>
    public static Scope Enter(EngineContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EngineContext? previous = _current.Value;
        _current.Value = context;
        return new Scope(previous);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly EngineContext? _previous;

        internal Scope(EngineContext? previous)
        {
            _previous = previous;
        }

        public void Dispose() => _current.Value = _previous;
    }
}
