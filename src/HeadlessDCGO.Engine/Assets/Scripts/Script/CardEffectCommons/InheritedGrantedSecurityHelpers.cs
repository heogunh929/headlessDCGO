namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using System.Collections.ObjectModel;
using HeadlessDCGO.Engine.Headless.Services;
using HeadlessDCGO.Engine.Headless.Effects;

public enum EffectSourceKind
{
    Native = 0,
    Inherited = 1,
    Granted = 2,
    Security = 3,
}

public static class InheritedGrantedSecurityHelpers
{
    public const string SourceKindKey = "effectSourceKind";
    public const string IsInheritedKey = "isInherited";
    public const string IsGrantedKey = "isGranted";
    public const string IsSecurityKey = "isSecurity";
    public const string GrantedTargetEntityIdKey = "grantedTargetEntityId";
    public const string HostEntityIdKey = "hostEntityId";
    public const string SecurityOwnerIdKey = "securityOwnerId";
    public const string QueryScopeKey = "queryScope";
    public const string QueryRoleKey = "queryRole";
    public const string EffectIdsKey = "effectIds";

    public static EffectBinding CreateInheritedBinding(
        EffectRequest request,
        HeadlessEntityId hostEntityId,
        EffectQueryRole queryRoles,
        IReadOnlyList<string> queryScopes,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        if (hostEntityId.IsEmpty)
        {
            throw new ArgumentException("Inherited effect host entity id must not be empty.", nameof(hostEntityId));
        }

        return CreateBinding(
            request,
            EffectSourceKind.Inherited,
            queryRoles,
            queryScopes,
            keywords,
            hostEntityId,
            values);
    }

    public static EffectBinding CreateGrantedBinding(
        EffectRequest request,
        HeadlessEntityId grantedTargetEntityId,
        EffectQueryRole queryRoles,
        IReadOnlyList<string> queryScopes,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        if (grantedTargetEntityId.IsEmpty)
        {
            throw new ArgumentException("Granted effect target entity id must not be empty.", nameof(grantedTargetEntityId));
        }

        return CreateBinding(
            request,
            EffectSourceKind.Granted,
            queryRoles,
            queryScopes,
            keywords,
            grantedTargetEntityId,
            values);
    }

    public static EffectBinding CreateSecurityBinding(
        EffectRequest request,
        HeadlessPlayerId securityOwnerId,
        EffectQueryRole queryRoles,
        IReadOnlyList<string> queryScopes,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        if (securityOwnerId.IsEmpty)
        {
            throw new ArgumentException("Security effect owner id must not be empty.", nameof(securityOwnerId));
        }

        var extraValues = CopyObjectValues(values);
        extraValues[SecurityOwnerIdKey] = securityOwnerId.Value;
        return CreateBinding(
            request,
            EffectSourceKind.Security,
            queryRoles,
            queryScopes,
            keywords,
            grantedTargetEntityId: null,
            extraValues);
    }

    public static EffectBinding CreateBinding(
        EffectRequest request,
        EffectSourceKind sourceKind,
        EffectQueryRole queryRoles,
        IReadOnlyList<string> queryScopes,
        IReadOnlyList<string>? keywords = null,
        HeadlessEntityId? grantedTargetEntityId = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), "Effect source kind must be known.");
        }

        if (grantedTargetEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Granted target entity id must not be empty.", nameof(grantedTargetEntityId));
        }

        EffectContext context = WithSourceKind(request.Context, sourceKind, grantedTargetEntityId, values);
        var nextRequest = new EffectRequest(request.EffectId, request.ControllerId, request.Timing, context);
        return new EffectBinding(nextRequest, keywords, queryRoles, queryScopes);
    }

    public static EffectContext WithSourceKind(
        EffectContext context,
        EffectSourceKind sourceKind,
        HeadlessEntityId? grantedTargetEntityId = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(sourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), "Effect source kind must be known.");
        }

        if (grantedTargetEntityId is { IsEmpty: true })
        {
            throw new ArgumentException("Granted target entity id must not be empty.", nameof(grantedTargetEntityId));
        }

        var merged = new Dictionary<string, object?>(context.Values, StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> pair in CopyObjectValues(values))
        {
            merged[pair.Key] = pair.Value;
        }

        merged[SourceKindKey] = sourceKind.ToString();
        merged[IsInheritedKey] = sourceKind == EffectSourceKind.Inherited;
        merged[IsGrantedKey] = sourceKind == EffectSourceKind.Granted;
        merged[IsSecurityKey] = sourceKind == EffectSourceKind.Security;
        if (sourceKind is EffectSourceKind.Inherited or EffectSourceKind.Granted)
        {
            merged[GrantedTargetEntityIdKey] = grantedTargetEntityId?.Value;
            merged[HostEntityIdKey] = grantedTargetEntityId?.Value;
        }

        return new EffectContext(
            context.SourcePlayerId,
            context.OwnerPlayerId,
            context.SourceEntityId,
            context.TriggerEntityId,
            context.TargetEntityIds,
            merged);
    }

    public static EffectSourceKind SourceKind(EffectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TryGetValue(SourceKindKey, out string? value)
            && Enum.TryParse(value, ignoreCase: false, out EffectSourceKind parsed))
        {
            return parsed;
        }

        if (context.TryGetValue(IsInheritedKey, out bool inherited) && inherited)
        {
            return EffectSourceKind.Inherited;
        }

        if (context.TryGetValue(IsGrantedKey, out bool granted) && granted)
        {
            return EffectSourceKind.Granted;
        }

        if (context.TryGetValue(IsSecurityKey, out bool security) && security)
        {
            return EffectSourceKind.Security;
        }

        return EffectSourceKind.Native;
    }

    private static Dictionary<string, object?> CopyObjectValues(
        IReadOnlyDictionary<string, object?>? values)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (values is null)
        {
            return copy;
        }

        foreach (KeyValuePair<string, object?> pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException("Inherited granted helper values must not contain null or whitespace keys.", nameof(values));
            }

            copy[pair.Key.Trim()] = pair.Value;
        }

        return copy;
    }

}


public static class InheritedGrantedSecurityHelperFactory
{
    public static EffectBinding CreateInheritedBinding(
        EffectRequest request,
        HeadlessEntityId hostEntityId,
        EffectQueryRole queryRoles,
        IReadOnlyList<string> queryScopes,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return InheritedGrantedSecurityHelpers.CreateInheritedBinding(
            request,
            hostEntityId,
            queryRoles,
            queryScopes,
            keywords,
            values);
    }

    public static EffectBinding CreateGrantedBinding(
        EffectRequest request,
        HeadlessEntityId grantedTargetEntityId,
        EffectQueryRole queryRoles,
        IReadOnlyList<string> queryScopes,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return InheritedGrantedSecurityHelpers.CreateGrantedBinding(
            request,
            grantedTargetEntityId,
            queryRoles,
            queryScopes,
            keywords,
            values);
    }

    public static EffectBinding CreateSecurityBinding(
        EffectRequest request,
        HeadlessPlayerId securityOwnerId,
        EffectQueryRole queryRoles,
        IReadOnlyList<string> queryScopes,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyDictionary<string, object?>? values = null)
    {
        return InheritedGrantedSecurityHelpers.CreateSecurityBinding(
            request,
            securityOwnerId,
            queryRoles,
            queryScopes,
            keywords,
            values);
    }

}
