// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_006.cs
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS
// BT1_006 (BT1/Yellow) — inline `new ActivateClass()` + SetIsInheritedEffect(true) + local functions.
//   [When Attacking] If you have 5 or more security cards, trigger <Draw 1> (Draw 1 card from your deck).
// AS-IS: ActivateClass on OnAllyAttack, SetIsInheritedEffect(true). CanUseCondition = CanTriggerOnAttack.
//   CanActivateCondition = IsExistOnBattleArea && card.Owner.SecurityCards.Count >= 5 &&
//   card.Owner.LibraryCards.Count >= 1. ORDER=-1, ISOPTIONAL=false. ActivateCoroutine =
//   new DrawClass(card.Owner, 1, activateClass).Draw().
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `card.Owner.SecurityCards`/
//   `LibraryCards` -> `new Player(card.Context, card.Owner)...`; DrawClass ctor -> mirror shape.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_006 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] If you have 5 or more security cards, trigger <Draw 1> (Draw 1 card from your deck).";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).SecurityCards.Count >= 5)
                    {
                        if (new Player(card.Context, card.Owner).LibraryCards.Count >= 1)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass).Draw();
            }
        }

        return cardEffects;
    }
}
