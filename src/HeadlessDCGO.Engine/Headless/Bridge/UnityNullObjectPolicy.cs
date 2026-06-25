namespace HeadlessDCGO.Engine.Headless.Bridge;

public sealed class UnityNullObjectPolicy
{
    public static UnityNullObjectPolicy Default { get; } = new();

    public UnityNullObjectDecision Evaluate(UnityOnlyAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);
        ValidateAccess(access);

        return access.Category switch
        {
            UnityOnlyAccessCategory.RenderingUi => Exclude(access, "Rendering/UI state is visual-only in headless execution."),
            UnityOnlyAccessCategory.SceneLifecycle => Exclude(access, "Scene lifecycle is excluded outside DcgoMatch and HeadlessGameLoop."),
            UnityOnlyAccessCategory.Animation => Exclude(access, "Animation and cut-in playback do not mutate headless gameplay state."),
            UnityOnlyAccessCategory.Audio => Exclude(access, "Audio playback is excluded from headless gameplay state."),
            UnityOnlyAccessCategory.Camera => Exclude(access, "Camera state is excluded from headless gameplay state."),
            UnityOnlyAccessCategory.NetworkClient => Exclude(access, "Client networking is outside the local deterministic headless session."),
            UnityOnlyAccessCategory.GameObjectTransform => Replace(
                access,
                "Use stable ids, zone state, IZoneMover, and repositories instead of scene hierarchy."),
            UnityOnlyAccessCategory.GlobalSingleton => Replace(
                access,
                "Use EngineContext, GManagerBridge, ContinuousContext, or explicit services instead of Unity singletons."),
            UnityOnlyAccessCategory.EffectContext => Replace(
                access,
                "Use typed EffectContext and explicit metadata instead of Unity object or Hashtable coupling."),
            _ => Reject(access, "Unity-only access category is unknown.")
        };
    }

    public UnityNullObjectDecision Evaluate(
        string sourcePath,
        string memberName,
        string accessExpression,
        UnityOnlyAccessCategory category,
        bool mutatesGameplayState = false)
    {
        return Evaluate(new UnityOnlyAccess(sourcePath, memberName, accessExpression, category, mutatesGameplayState));
    }

    public bool ShouldExclude(UnityOnlyAccess access)
    {
        return Evaluate(access).Decision == UnityNullObjectDecisionKind.ExcludeNoOp;
    }

    public bool TryEvaluate(UnityOnlyAccess access, out UnityNullObjectDecision decision)
    {
        ArgumentNullException.ThrowIfNull(access);

        try
        {
            decision = Evaluate(access);
            return decision.Decision != UnityNullObjectDecisionKind.RejectInvalid;
        }
        catch (ArgumentException ex)
        {
            decision = UnityNullObjectDecision.Reject(
                access.SourcePath,
                access.MemberName,
                access.AccessExpression,
                access.Category,
                ex.Message);
            return false;
        }
    }

    private static UnityNullObjectDecision Exclude(UnityOnlyAccess access, string reason)
    {
        if (access.MutatesGameplayState)
        {
            return Reject(access, "Gameplay state mutation must use a headless service, not a Unity-only no-op.");
        }

        return UnityNullObjectDecision.ExcludeNoOp(
            access.SourcePath,
            access.MemberName,
            access.AccessExpression,
            access.Category,
            reason);
    }

    private static UnityNullObjectDecision Replace(UnityOnlyAccess access, string replacement)
    {
        return UnityNullObjectDecision.ReplaceWithService(
            access.SourcePath,
            access.MemberName,
            access.AccessExpression,
            access.Category,
            replacement,
            access.MutatesGameplayState);
    }

    private static UnityNullObjectDecision Reject(UnityOnlyAccess access, string reason)
    {
        return UnityNullObjectDecision.Reject(
            access.SourcePath,
            access.MemberName,
            access.AccessExpression,
            access.Category,
            reason);
    }

    private static void ValidateAccess(UnityOnlyAccess access)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(access.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(access.MemberName);
        ArgumentException.ThrowIfNullOrWhiteSpace(access.AccessExpression);
    }
}

public sealed record UnityOnlyAccess(
    string SourcePath,
    string MemberName,
    string AccessExpression,
    UnityOnlyAccessCategory Category,
    bool MutatesGameplayState = false);

public sealed record UnityNullObjectDecision(
    UnityNullObjectDecisionKind Decision,
    string SourcePath,
    string MemberName,
    string AccessExpression,
    UnityOnlyAccessCategory Category,
    bool MutatesGameplayState,
    string Reason,
    string Replacement)
{
    public bool IsExcluded => Decision == UnityNullObjectDecisionKind.ExcludeNoOp;

    public bool IsReplacementRequired => Decision == UnityNullObjectDecisionKind.ReplaceWithHeadlessService;

    public bool IsRejected => Decision == UnityNullObjectDecisionKind.RejectInvalid;

    public static UnityNullObjectDecision ExcludeNoOp(
        string sourcePath,
        string memberName,
        string accessExpression,
        UnityOnlyAccessCategory category,
        string reason)
    {
        return new UnityNullObjectDecision(
            UnityNullObjectDecisionKind.ExcludeNoOp,
            sourcePath,
            memberName,
            accessExpression,
            category,
            MutatesGameplayState: false,
            reason,
            "No-op in headless execution.");
    }

    public static UnityNullObjectDecision ReplaceWithService(
        string sourcePath,
        string memberName,
        string accessExpression,
        UnityOnlyAccessCategory category,
        string replacement,
        bool mutatesGameplayState)
    {
        return new UnityNullObjectDecision(
            UnityNullObjectDecisionKind.ReplaceWithHeadlessService,
            sourcePath,
            memberName,
            accessExpression,
            category,
            mutatesGameplayState,
            "Unity object access is gameplay-relevant and must be expressed through headless APIs.",
            replacement);
    }

    public static UnityNullObjectDecision Reject(
        string sourcePath,
        string memberName,
        string accessExpression,
        UnityOnlyAccessCategory category,
        string reason)
    {
        return new UnityNullObjectDecision(
            UnityNullObjectDecisionKind.RejectInvalid,
            sourcePath,
            memberName,
            accessExpression,
            category,
            MutatesGameplayState: false,
            reason,
            "Provide an explicit UnityOnlyAccessCategory and headless replacement policy.");
    }
}

public enum UnityNullObjectDecisionKind
{
    ExcludeNoOp,
    ReplaceWithHeadlessService,
    RejectInvalid
}

public enum UnityOnlyAccessCategory
{
    Unknown,
    RenderingUi,
    SceneLifecycle,
    Animation,
    Audio,
    Camera,
    NetworkClient,
    GameObjectTransform,
    GlobalSingleton,
    EffectContext
}
