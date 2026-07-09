namespace HeadlessDCGO.Engine.Headless.Effects;

using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Headless.Services;

/// <summary>
/// (F-4) Match-scoped holder for once-per-turn / max-count-per-turn effect activation. The data layer
/// is <see cref="OnceFlagHelpers"/> (immutable <see cref="OnceFlagState"/>); this is the mutable holder
/// the trigger loop consults so an effect bound with <c>CardEffectDefinition.MaxCountPerTurn</c> does not
/// activate more than its cap allows in a turn. Mirrors the original CardController use-count tracking
/// (<c>isOverMaxCountPerTurn</c> + <c>InitUseCountThisTurn</c>).
/// </summary>
public sealed class OnceFlagController : IHeadlessMatchStateResettable
{
    private OnceFlagState _state = OnceFlagState.Empty;

    public OnceFlagState State => _state;

    /// <summary>Reset the per-turn use counts for a new turn (original <c>InitUseCountThisTurn</c>).</summary>
    public void ResetForTurn(long turnSequence, HeadlessPlayerId? turnPlayerId)
    {
        OnceFlagResult result = OnceFlagHelpers.ResetTurn(_state, turnSequence < 0 ? 0 : turnSequence, turnPlayerId);
        if (result.IsSuccess)
        {
            _state = result.State;
        }
    }

    /// <summary>
    /// Gate one activation of <paramref name="request"/>. An effect with no per-turn cap
    /// (<paramref name="maxCountPerTurn"/> is null) always passes. When capped, returns <c>false</c> if
    /// the cap is already reached this turn; otherwise registers the use and returns <c>true</c>.
    /// </summary>
    public bool TryActivate(EffectRequest request, int? maxCountPerTurn)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanActivate(request, maxCountPerTurn))
        {
            return false;
        }

        Consume(request, maxCountPerTurn);
        return true;
    }

    /// <summary>(RD-12/13) Whether the effect is still under its per-turn cap — WITHOUT consuming a use. Used to
    /// gate an OPTIONAL effect's yes/no prompt (don't offer a capped-out effect) so the actual use is registered
    /// only at execution (after the player accepts), mirroring AS-IS RegisterUseEffectThisTurn firing in the
    /// effect's OnProcess callback, not at collection.</summary>
    public bool CanActivate(EffectRequest request, int? maxCountPerTurn)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (maxCountPerTurn is not int max)
        {
            return true;
        }

        OnceFlagResult canUse = OnceFlagHelpers.CanUse(_state, OnceFlagHelpers.ForRequest(request), max);
        return canUse.IsSuccess && canUse.CanUse;
    }

    /// <summary>(RD-12/13) Register one use of the effect's per-turn cap (the AS-IS "register use" step). Call
    /// only after the effect actually resolves (and, for an optional, after the player accepts).</summary>
    public void Consume(EffectRequest request, int? maxCountPerTurn)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (maxCountPerTurn is not int max)
        {
            return;
        }

        OnceFlagResult registered = OnceFlagHelpers.RegisterUse(_state, OnceFlagHelpers.ForRequest(request), max);
        if (registered.IsSuccess)
        {
            _state = registered.State;
        }
    }

    public void ResetMatchState() => _state = OnceFlagState.Empty;
}
