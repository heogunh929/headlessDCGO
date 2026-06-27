namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Services;

public static class Blitz
{
    public static KeywordBaseBatch2Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false,
        string triggerReason = "OnPlay")
    {
        return KeywordBaseBatch2Factory.Create(
            KeywordBaseBatch2Kind.Blitz,
            sourceEntityId,
            targetEntityId,
            isInherited,
            isLinked,
            triggerReason);
    }
}
