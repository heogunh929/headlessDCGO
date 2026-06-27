namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Services;

public static class Jamming
{
    public static KeywordBaseBatch1Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null,
        bool isInherited = false,
        bool isLinked = false)
    {
        return KeywordBaseBatch1Factory.Create(
            KeywordBaseBatch1Kind.Jamming,
            sourceEntityId,
            targetEntityId,
            isInherited,
            isLinked);
    }
}
