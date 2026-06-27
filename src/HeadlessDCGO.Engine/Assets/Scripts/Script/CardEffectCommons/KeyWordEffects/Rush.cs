// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Rush.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Rush). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Rush's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch2Effect
{
    private CardEffectCanResolveResult CanResolveRush(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        return CardEffectCanResolveResult.Success("Rush target can attack immediately.", BaseValues(context, target));
    }
}
