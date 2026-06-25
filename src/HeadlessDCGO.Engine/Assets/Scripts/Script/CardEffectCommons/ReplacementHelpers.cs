namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static class ReplacementHelperFactory
{
    public static ReplacementEffect PreventRemoval(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return ReplacementEffect.Prevent(id, ReplacementEventKind.RemoveFromField, targetEntityId, reason);
    }

    public static ReplacementEffect PreventDeletion(string id, HeadlessEntityId targetEntityId, string? reason = null)
    {
        return ReplacementEffect.Prevent(id, ReplacementEventKind.Delete, targetEntityId, reason);
    }

    public static ReplacementEffect RedirectDeletion(
        string id,
        HeadlessEntityId targetEntityId,
        HeadlessEntityId replacementEntityId,
        string? reason = null)
    {
        return ReplacementEffect.Redirect(id, ReplacementEventKind.Delete, targetEntityId, replacementEntityId, reason);
    }

    public static ReplacementEffect ImmuneFromDpReduction(
        string id,
        HeadlessEntityId targetEntityId,
        HeadlessEntityId? sourceEntityId = null,
        string? reason = null)
    {
        return ReplacementEffect.Immune(id, ReplacementEventKind.DpReduction, targetEntityId, sourceEntityId, "ChangeDP", reason);
    }
}
