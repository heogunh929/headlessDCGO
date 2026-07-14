// Source: DCGO/Assets/Scripts/CardEffect/EX10/Black/EX10_002.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [All Turns][Once Per Turn]
// OnAttackTargetChanged INHERITED (anyone) branch — the card's ONLY effect (F1-Tier2 OnAttackTargetChanged witness).
//   [All Turns][Once Per Turn] When attack targets change, <Draw 1>.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpActivateClass(..., 1, false, ...) (ORDER 1 =
// once per turn, mandatory) + SetIsInheritedEffect(true) + SetHashString("ESS_EX10-002") (EX10_002.cs:14-43).
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `new DrawClass(card.Owner, 1,
// activateClass)` -> the mirror carrier ctor `new DrawClass(card.Context, card.Owner, 1,
// activateClass.EffectSourceCard?.InstanceId)` (established BT2_070 idiom).
// ANYONE scope: permanentCondition = `_ => true` (reacts to ANY attacker's target switch), the reason the timing is
// EventBroadcast — mirrored by CanTriggerOnPermanentAttackTargetSwitch (NOT the self gate).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX10.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class EX10_002 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAttackTargetChanged)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("ESS_EX10-002");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[All Turns] [Once Per Turn] When attack targets change, <Draw 1> (Draw 1 card from your deck.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.CanTriggerOnPermanentAttackTargetSwitch(hashtable, _ => true);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }

        return cardEffects;
    }
}
