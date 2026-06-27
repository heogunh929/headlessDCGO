// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Blocker.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch1Effect (Blocker). Shared scaffolding lives in
// KeywordBaseBatch1.cs; this file holds only Blocker's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch1Effect
{
    private CardEffectCanResolveResult CanResolveBlocker(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        return CardEffectCanResolveResult.Success("Blocker target can block.", BaseValues(context, target));
    }
}
