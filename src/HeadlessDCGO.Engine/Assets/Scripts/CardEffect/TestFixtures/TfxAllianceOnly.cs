// TEST FIXTURE (not a real card). A single printed [Alliance] keyword: returns AllianceSelfEffect at
// OnAllyAttack, nothing else. Witnesses the C-Atk rehoming — the OnAllyAttack window (StackSkillInfos ->
// MultipleSkills) is the SOLE firing path for a printed Alliance after the AllianceAttackBoost gate retired.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxAllianceOnly : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnAllyAttack)
        {
            effects.Add(CardEffectFactory.AllianceSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return effects;
    }
}
