// TEST FIXTURE (not a real card). "[On Play] gain 2 memory" — a live new-model ActivateClass reactor at
// OnEnterFieldAnyone. Inert in actual play beyond this (no real card is numbered "TfxOnPlayGainMemory").
//
// (RC-6) The old-model probe (this fixture -> TfxTriggeredMemoryEffect, a ToBinding(string) ICardEffect lowered
// into the invented EffectRegistry) is retired: the registry trigger-reader half (AutoProcessingTriggerCollector
// GetEffectsForTiming) had zero real-card producers and is excised. This fixture now follows the current-model
// canon used by real ported cards — a card-registered ActivateClass surfaced via the live SkillInfo scan
// (CEntity_Effect / AutoProcessing.GetSkillInfos) — mirroring TfxOnKnockOutDeleteOpponent / the RD11
// TfxOnOpponentDpZeroDeleteMemory reactor, gated + firing an observable AddMemory the same way the deleted probe was.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxOnPlayGainMemory : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        var effects = new List<ICardEffect>();
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +2", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false,
                "[On Play] Gain 2 memory. (test fixture)");
            activateClass.SetIsInheritedEffect(false);
            effects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            async Task ActivateCoroutine(Hashtable _hashtable) => await card.Owner.AddMemory(2, activateClass);
        }

        return effects;
    }
}
