namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static class RestrictionHelperFactory
{
    public static CannotRestriction CannotAttack(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.Attack, targetEntityId, reason);
    }

    public static CannotRestriction CannotBlock(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.Block, targetEntityId, reason);
    }

    public static CannotRestriction CannotDelete(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.Delete, targetEntityId, reason);
    }

    public static CannotRestriction CannotReturnToHand(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.ReturnToHand, targetEntityId, reason);
    }

    public static CannotRestriction CannotReturnToDeck(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.ReturnToDeck, targetEntityId, reason);
    }

    public static CannotRestriction CannotSuspend(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return CannotRestriction.ForTarget(id, CannotRestrictionKind.Suspend, targetEntityId, reason);
    }
}
