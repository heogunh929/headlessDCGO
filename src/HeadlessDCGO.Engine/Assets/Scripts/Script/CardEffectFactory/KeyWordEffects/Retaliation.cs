namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.Services;

public static class Retaliation
{
    public static KeywordBaseBatch2Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false)
    {
        return KeywordBaseBatch2Factory.Create(
            KeywordBaseBatch2Kind.Retaliation,
            sourceEntityId,
            targetEntityId,
            isInherited,
            isLinked);
    }
}
