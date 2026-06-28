// Source: Assets/Scripts/Script/CardEffectFactory/KeyWordEffects/Progress.cs
// AS-IS mirror of CardEffectFactory.ProgressStaticEffect — a convenience factory that builds the Progress
// keyword effect (KeywordBaseBatch2). 1:1 map: static (IsBackgroundProcess) effect, CardCondition = the
// attacker, SkillCondition = IsOpponentEffect; CanActivateProgress (IsAttacking && AttackingPermanent == this)
// -> ProgressImmunity.TryRegister at attack declaration; the CanNotAffectedClass -> ContinuousImmunityGate
// (opponent-only, UntilEndAttack), enforced by the mutation sink.
namespace HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects;

using HeadlessDCGO.Engine.Headless.Effects;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons.KeyWordEffects;
using HeadlessDCGO.Engine.Headless.Services;

public static class Progress
{
    public static KeywordBaseBatch2Effect Create(
        HeadlessEntityId sourceEntityId,
        HeadlessEntityId? targetEntityId = null)
    {
        return KeywordBaseBatch2Factory.Create(
            KeywordBaseBatch2Kind.Progress,
            sourceEntityId,
            targetEntityId);
    }
}
