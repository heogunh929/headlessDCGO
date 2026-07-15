// TEST FIXTURE (not a real card). A single printed [Raid] keyword: returns RaidSelfEffect at OnAllyAttack,
// nothing else. Used to witness the C-Atk rehoming — that the OnAllyAttack window (StackSkillInfos ->
// MultipleSkills) is the SOLE firing path for a printed Raid after the RaidAttackSwitch gate is retired.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxRaidOnly : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnAllyAttack)
        {
            effects.Add(CardEffectFactory.RaidSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }

        return effects;
    }
}
