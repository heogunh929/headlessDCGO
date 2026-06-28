// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Vortex.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Vortex). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Vortex's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateVortex). The LIVE "this Digimon makes an effect-driven attack" path is the
// S1 hub EffectDrivenAttack (GetTargets/Initiate/RequestChoice with Vortex options: Digimon + player,
// unsuspended allowed) — this branch is the grant/mirror layer (resolving emits GrantVortex -> hasVortex).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch2Effect
{
    private CardEffectCanResolveResult CanResolveVortex(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        // AS-IS CanActivateVortex: this Digimon is on the battle area (the dispatch already enforces battle
        // area). The full "can attack a Digimon or player" eligibility is enforced live by
        // EffectDrivenAttack.GetTargets when the attack is offered.
        return CardEffectCanResolveResult.Success("Vortex can initiate an effect-driven attack.", BaseValues(context, target));
    }
}
