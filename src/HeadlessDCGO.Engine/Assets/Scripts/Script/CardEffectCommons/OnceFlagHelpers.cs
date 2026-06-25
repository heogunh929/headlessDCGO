namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static class OnceFlagHelperFactory
{
    public static OnceFlagKey ForRequest(
        EffectRequest request,
        OnceFlagScope scope = OnceFlagScope.Turn,
        string? timing = null)
    {
        return OnceFlagHelpers.ForRequest(request, scope, timing);
    }

    public static OnceFlagResult CanUse(
        OnceFlagState state,
        OnceFlagKey key,
        int maxCount = 1)
    {
        return OnceFlagHelpers.CanUse(state, key, maxCount);
    }

    public static OnceFlagResult RegisterUse(
        OnceFlagState state,
        OnceFlagKey key,
        int maxCount = 1)
    {
        return OnceFlagHelpers.RegisterUse(state, key, maxCount);
    }

    public static OnceFlagResult RemoveUse(
        OnceFlagState state,
        OnceFlagKey key)
    {
        return OnceFlagHelpers.RemoveUse(state, key);
    }

    public static OnceFlagResult ResetTurn(
        OnceFlagState state,
        long nextTurnSequence,
        HeadlessPlayerId? nextTurnPlayerId = null)
    {
        return OnceFlagHelpers.ResetTurn(state, nextTurnSequence, nextTurnPlayerId);
    }

    public static EffectContext WithUseCount(
        EffectContext context,
        OnceFlagState state,
        OnceFlagKey key)
    {
        return OnceFlagHelpers.WithUseCount(context, state, key);
    }
}
