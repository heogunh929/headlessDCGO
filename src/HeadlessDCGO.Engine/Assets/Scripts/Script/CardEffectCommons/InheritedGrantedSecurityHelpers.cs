namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

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

    public static InheritedGrantedSecurityQueryResult Query(
        IEffectQueryService effectQueryService,
        EffectQueryRole role,
        EffectQueryContext context,
        EffectSourceKind? sourceKind = null)
    {
        return InheritedGrantedSecurityHelpers.Query(effectQueryService, role, context, sourceKind);
    }
}
