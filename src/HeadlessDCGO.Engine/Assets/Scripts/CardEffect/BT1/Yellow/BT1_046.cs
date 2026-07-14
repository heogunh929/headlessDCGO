// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_046.cs
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS
// BT1_046 (BT1/Yellow) — inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local functions.
//   [When Attacking] Trigger <Draw 1>. (Draw 1 card from your deck.)
// AS-IS: ActivateClass on OnAllyAttack, CanUseCondition = CanTriggerOnAttack, CanActivateCondition =
//   IsExistOnBattleArea && card.Owner.LibraryCards.Count >= 1 && card.Owner.HandCards.Count <= 4, ORDER=-1,
//   ISOPTIONAL=false, ActivateCoroutine = new DrawClass(card.Owner, 1, activateClass).Draw().
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `card.Owner.LibraryCards`/`HandCards`
//   -> `new Player(card.Context, card.Owner).LibraryCards`/`.HandCards` (established BT2_023 idiom); DrawClass
//   ctor -> mirror (EngineContext, HeadlessPlayerId, count, cause) shape (BT1_003/BT1_029 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_046 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] Trigger <Draw 1>. (Draw 1 card from your deck.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).LibraryCards.Count >= 1)
                    {
                        if (new Player(card.Context, card.Owner).HandCards.Count <= 4)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Draw();
            }
        }

        return cardEffects;
    }
}
