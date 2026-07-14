// TEST FIXTURE (not a real card). An UNCAPPED [All Turns] OnAllyAttack ([When Attacking]) reactor: whenever THIS
// Digimon attacks, its owner gains 1 memory — with NO once-per-turn cap. The OnAllyAttack sibling of
// TfxOnEndAttackCounter, used to witness that the P1-2 (C2r) inline StackSkillInfos insert in AttackProcess.Attack()
// opens the OnAllyAttack window — for BOTH a plain declared attack AND an EFFECT-driven attack (attackEffectSourceId
// present), the latter being the case SkillWindowSupply.TryBuildAttack previously DROPPED (returns false on
// attackCauseEffectId), so the window never opened before the flip. Self-scope: the reacting card must be a
// cardSource of the AttackingPermanent (AS-IS CanTriggerOnAttack), mirroring every AS-IS OnAllyAttack reactor.
// Inert in actual play.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnAllyAttackCounter : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            effects.Add(activateClass);

            string EffectDiscription() =>
                "[All Turns] When this Digimon attacks, gain 1 memory (uncapped).";

            // AS-IS CanUseCondition mirror: CanTriggerOnAttack (the reacting card is a cardSource of the
            // AttackingPermanent — the self/attacker gate shared by every OnAllyAttack reactor).
            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.CanTriggerOnAttack(hashtable, card);

            bool CanActivateCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleAreaDigimon(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return effects;
    }
}
