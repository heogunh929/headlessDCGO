// TEST FIXTURE (not a real card). "When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory"
// with NO once-per-turn cap — so it fires once PER deletion. Inert in actual play (no real card is numbered
// "TfxOnDeleteGainMemory").
//
// (RC-6) The old-model probe (this fixture -> TfxTriggeredMemoryEffect, a ToBinding(string) ICardEffect lowered
// into the invented EffectRegistry) is retired: the registry trigger-reader half (AutoProcessingTriggerCollector
// GetEffectsForTiming) had zero real-card producers and is excised. This fixture now follows the current-model
// canon — a card-registered ActivateClass surfaced via the live SkillInfo scan (CEntity_Effect /
// AutoProcessing.GetSkillInfos), identical in shape to the RD11 TfxOnOpponentDpZeroDeleteMemory reactor: scoped to
// OnDestroyedAnyone + opponent-owned + DP-zero, uncapped, firing an observable AddMemory the way the deleted probe did.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnDeleteGainMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false,
                "[All Turns] When an opponent's Digimon is deleted by dropping to 0 DP, gain 1 memory (uncapped). (test fixture)");
            activateClass.SetIsInheritedEffect(true);
            effects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable) =>
                CardEffectCommons.IsExistOnBattleArea(card)
                && CardEffectCommons.IsOwnerTurn(card)
                && CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, permanent => permanent.OwnerId != card.Owner)
                && CardEffectCommons.IsDPZeroDelete(hashtable);

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            async Task ActivateCoroutine(Hashtable _hashtable) => await card.Owner.AddMemory(1, activateClass);
        }

        return effects;
    }
}
