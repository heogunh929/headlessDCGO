// TEST FIXTURE (not a real card). A DELIBERATE corpus DEFECT shape for the 리뷰3 P2-② double-key guard:
// registers the SAME [When Digivolving] effect (same description, same source card) under BOTH the literal
// AS-IS key (EffectTiming.OnEnterFieldAnyone, gated by CanTriggerWhenDigivolving — the AS-IS single-key
// idiom) AND the mirror's DISPATCH-REMAP dialect key (EffectTiming.WhenDigivolving — the batch-2
// BT1_025/BT1_062 convention). With the executor's WhenDigivolving bridge open on an evolution play, this
// would fire TWICE per digivolve — a state AS-IS (one key only) cannot express. The guard at the bridge seat
// (CardController.cs PlayPermanentClass evolution arm) must SURFACE it as a STOP instead of stacking it.
// Used by tests/R4S3b-MainDispatch.Tests. Inert in actual play (no real card numbered
// "TfxDoubleKeyWhenDigivolving"; live corpus double-key registrations: 0).

namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.TestFixtures;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class TfxDoubleKeyWhenDigivolving : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        // The DEFECT: the same effect under both keys (a correctly-ported card uses exactly ONE of these).
        if (timing == EffectTiming.WhenDigivolving || timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1 card", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Draw 1 card. (Tfx double-key defect)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }

        return cardEffects;
    }
}
