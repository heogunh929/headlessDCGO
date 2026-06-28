// Source: Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Alliance.cs
// AS-IS mirror of CardEffectFactory.AllianceSelfEffect / AllianceEffect — a convenience factory that builds
// the Alliance keyword effect (KeywordBaseBatch2). 1:1 keyword-name/timing map: trigger OnAllyAttack
// (CanTriggerOnPermanentAttack where this permanent is the attacker); CanActivateAlliance -> a suspendable
// owner ally exists; AllianceProcess (suspend the chosen ally -> attacker +ally.DP / +1 Security Attack
// UntilEndAttack) -> AllianceAttackBoost (RequestChoice/ResolveChoice in AttackPipeline before block timing).
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Services;

public static class Alliance
{
    public static KeywordBaseBatch2Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null)
    {
        return KeywordBaseBatch2Factory.Create(
            KeywordBaseBatch2Kind.Alliance,
            sourceEntityId,
            targetEntityId);
    }
}
