// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Reboot.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch1Effect (Reboot). Shared scaffolding lives in
// KeywordBaseBatch1.cs; this file holds only Reboot's resolution branch (1:1 with the original layout).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch1Effect
{
    private CardEffectCanResolveResult CanResolveReboot(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        return CardEffectCanResolveResult.Success("Reboot target can unsuspend on opponent unsuspend.", BaseValues(context, target));
    }
}
