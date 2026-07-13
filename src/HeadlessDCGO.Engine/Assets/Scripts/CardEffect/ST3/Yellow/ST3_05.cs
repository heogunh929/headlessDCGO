// Source: DCGO/Assets/Scripts/CardEffect/ST3/Yellow/ST3_05.cs
// TRUE AS-IS-verbatim re-port (ST3 Yellow batch). 1:1 mirror of the original ST3_05 (ST3/Yellow).
//   [When Attacking] If you have 4 or more security cards, gain 1 memory.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.AddMemoryTriggerEffect(...)` call (an invented
// helper with no AS-IS counterpart) with the literal AS-IS inline `new ActivateClass()` structure.
// AS-IS structure kept verbatim: inline ActivateClass, SetIsInheritedEffect(true) (no SetHashString here).
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`.
// AS-IS `card.Owner.SecurityCards.Count` -> `CardEffectCommons.SecurityCount(card)` (established mirror helper,
// its own doc comment cites this exact card). AS-IS `card.Owner.CanAddMemory(activateClass)` /
// `card.Owner.AddMemory(1, activateClass)` resolve against the `HeadlessPlayerId` extensions (Player.cs).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST3.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST3_05 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] If you have 4 or more security cards, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.SecurityCount(card) >= 4)
                    {
                        if (card.Owner.CanAddMemory(activateClass))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await card.Owner.AddMemory(1, activateClass);
            }
        }

        return cardEffects;
    }
}
