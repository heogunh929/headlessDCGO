// Source: Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Overclock.cs
// AS-IS mirror of CardEffectFactory.OverclockSelfEffect / OverclockEffect — a convenience factory that
// builds the Overclock keyword effect (KeywordBaseBatch2). 1:1 map: trigger OnEndTurn (IsOwnerTurn);
// CanActivateOverclock (trait/token ally != self) -> OverclockEffect.GetTraitAllyCandidates; OverclockProcess
// (delete the chosen ally -> if deleted, untapped player-only attack) -> OverclockEffect.ResolveChoice
// (DeletionReplacementGate.SacrificeAsync + EffectDrivenAttack with WithoutTap, player only).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Services;

public static class Overclock
{
    public static KeywordBaseBatch2Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null)
    {
        return KeywordBaseBatch2Factory.Create(
            KeywordBaseBatch2Kind.Overclock,
            sourceEntityId,
            targetEntityId);
    }
}
