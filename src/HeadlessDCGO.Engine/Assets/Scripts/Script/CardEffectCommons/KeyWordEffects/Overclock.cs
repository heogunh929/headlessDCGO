// Source: Assets/Scripts/Script/CardEffectCommons/KeyWordEffects/Overclock.cs
// AS-IS mirror: per-keyword partial of KeywordBaseBatch2Effect (Overclock). Shared scaffolding lives in
// KeywordBaseBatch2.cs; this file holds only Overclock's resolution branch (1:1 with the original
// CardEffectCommons.CanActivateOverclock). The LIVE end-of-turn "delete a trait/token ally -> untapped
// player attack" path is engine plumbing in OverclockEffect (S3 trait + DeletionReplacementGate.SacrificeAsync
// + the S1 hub EffectDrivenAttack) — this branch is the grant/mirror layer (resolving emits GrantOverclock
// -> hasOverclock).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Headless.State;

public sealed partial class KeywordBaseBatch2Effect
{
    private CardEffectCanResolveResult CanResolveOverclock(
        CardEffectResolveContext context,
        CardInstanceState target)
    {
        // AS-IS CanActivateOverclock: this Digimon is on the battle area (the dispatch already enforces
        // battle area). The full "a trait/token ally != self exists" eligibility is enforced live by
        // OverclockEffect.GetTraitAllyCandidates when the end-of-turn window opens.
        return CardEffectCanResolveResult.Success("Overclock can delete a trait ally for an untapped attack.", BaseValues(context, target));
    }
}
