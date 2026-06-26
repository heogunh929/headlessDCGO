namespace HeadlessDCGO.Engine.Headless.Effects;

/// <summary>
/// Canonical trigger-timing vocabulary (W1). This is the contract between the engine — which emits
/// game events and derives timings via <see cref="TriggerTimingMap"/> — and ported card effects,
/// which register under these exact strings (<see cref="AutoProcessingTriggerCollector"/> matches by
/// ordinal string equality). Names mirror the original Unity <c>EffectTiming</c> enum so Phase 4
/// card porting is a 1:1 mapping. Add new constants here as more emission points are wired.
/// </summary>
public static class TriggerTimings
{
    // Play / field entry / exit.
    public const string OnPlay = "OnPlay";
    public const string OnEnterField = "OnEnterFieldAnyone";
    public const string OnLeaveField = "OnLeaveFieldAnyone";
    public const string WhenRemoveField = "WhenRemoveField";

    // Deletion / return.
    public const string OnDeletion = "OnDestroyedAnyone";
    public const string OnReturnToHand = "WhenReturntoHandAnyone";
    public const string OnReturnToLibrary = "WhenReturntoLibraryAnyone";

    // Hand / security movement.
    public const string OnAddToHand = "OnAddHand";
    public const string OnAddToSecurity = "OnAddSecurity";
    public const string OnLoseSecurity = "OnLoseSecurity";

    // Attack / counter / block / security check.
    public const string OnAttack = "OnUseAttack";
    public const string OnCounter = "OnCounterTiming";
    public const string OnBlock = "OnBlockAnyone";
    public const string OnSecurityCheck = "OnSecurityCheck";

    // Turn boundaries.
    public const string OnStartTurn = "OnStartTurn";
    public const string OnEndTurn = "OnEndTurn";

    // Digivolution / draw.
    public const string WhenDigivolving = "WhenDigivolving";
    public const string OnDraw = "OnDraw";
}
