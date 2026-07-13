// Source: DCGO/Assets/Scripts/CardEffect/BT1/Green/BT1_076.cs
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_076 (BT1/Green).
//   [When Attacking][inherited] If your opponent has 2 or more suspended Digimon, gain 1 memory.
// AS-IS structure kept verbatim: `MatchConditionOpponentsPermanentCount` (Permanent-shape overload exists,
// matching AS-IS directly).
// UNRESOLVED (kept verbatim, logged): AS-IS `card.Owner.CanAddMemory(activateClass)` — see BT1_030.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_076 : CEntity_Effect
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
                return "[When Attacking] If your opponent has 2 or more suspended Digimon, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.MatchConditionOpponentsPermanentCount(card, (permanent) => permanent.IsDigimon && permanent.IsSuspended) >= 2)
                    {
                        // UNRESOLVED (see file header): AS-IS `card.Owner.CanAddMemory(activateClass)`.
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
