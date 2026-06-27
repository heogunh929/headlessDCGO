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
        if (maxCountPerTurn is not int max)
        {
            return true;
        }

        OnceFlagKey key = OnceFlagHelpers.ForRequest(request);
        OnceFlagResult canUse = OnceFlagHelpers.CanUse(_state, key, max);
        if (!canUse.IsSuccess || !canUse.CanUse)
        {
            return false;
        }

        OnceFlagResult registered = OnceFlagHelpers.RegisterUse(_state, key, max);
        if (registered.IsSuccess)
        {
            _state = registered.State;
        }

        return true;
    }

    public void ResetMatchState() => _state = OnceFlagState.Empty;
}
