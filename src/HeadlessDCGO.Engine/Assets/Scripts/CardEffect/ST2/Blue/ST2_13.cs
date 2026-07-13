// Source: DCGO/Assets/Scripts/CardEffect/ST2/Blue/ST2_13.cs
// TRUE AS-IS-verbatim re-port (batch: ST2 Blue). 1:1 mirror of the original ST2_13 (an Option).
//   [Main] Gain 1 memory.
//   [Security] Gain 2 memory.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.GainMemoryActivatedEffect(...)` calls (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure per timing.
// AS-IS structure kept verbatim: [Main] uses `card.BaseENGCardNameFromEntity` as the ICardEffect name (not the
// effect description); [Security] uses the interpolated (but non-substituting) literal `$"Memory +2"` and calls
// `SetIsSecurityEffect(true)`; both pass `null` for CanActivateCondition (SetUpActivateClass's first arg) —
// AS-IS has no extra activate gate beyond CanUseCondition for this card, and neither block checks
// CanAddMemory before adding (kept verbatim, not "fixed").
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// AS-IS `card.Owner.AddMemory(N, activateClass)` -> the `HeadlessPlayerId` extension established in BT2_010.cs.

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST2.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST2_13 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect($"Memory +2", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Gain 2 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(2, activateClass);
            }
        }

        return cardEffects;
    }
}
