// Source: Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Vortex.cs
// AS-IS mirror of CardEffectFactory.VortexEffect — a convenience factory that builds the Vortex keyword
// effect (KeywordBaseBatch2). 1:1 map: CanActivateVortex -> EffectDrivenAttack.GetTargets has a candidate;
// VortexProcess (SelectAttackEffect, isVortex: targets Digimon + players, unsuspended allowed) ->
// EffectDrivenAttack.RequestChoice/Initiate with EffectAttackOptions(AllowDigimonTarget, AllowPlayerTarget,
// TargetUnsuspended). The S1 hub drives the declared attack through the existing AttackPipeline.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Services;

public static class Vortex
{
    public static KeywordBaseBatch2Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null)
    {
        return KeywordBaseBatch2Factory.Create(
            KeywordBaseBatch2Kind.Vortex,
            sourceEntityId,
            targetEntityId);
    }
}
