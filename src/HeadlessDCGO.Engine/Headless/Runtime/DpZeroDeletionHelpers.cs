namespace HeadlessDCGO.Engine.Headless.Runtime;

/// <summary>
/// (F-8.5) DP-zero deletion metadata keys: a battle-area Digimon deleted because its RESOLVED DP
/// (printed + continuous modifiers) reached 0 or less is stamped <c>DPZero</c> on its metadata so an
/// <c>IsDpZeroDelete</c> condition can distinguish it from a battle/effect deletion. The DP-zero sweep
/// itself is carried by <c>GameFlowProcessor.StateBasedDeletionSweepAsync</c>.
/// </summary>
public static class DpZeroDeletionHelpers
{
    public const string DpKey = "dp";
    public const string DpZeroKey = "DPZero";
    public const string DeletedByEffectKey = "deletedByEffect";
}
