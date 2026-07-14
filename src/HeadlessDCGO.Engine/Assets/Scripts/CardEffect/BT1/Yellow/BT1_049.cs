// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_049.cs
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS
// BT1_049 (BT1/Yellow) — inline `new ActivateClass()` + SetIsInheritedEffect(true) + local functions.
//   [Your Turn] When an opponent's Digimon is deleted by dropping to 0 DP, trigger <Draw 1> (Draw 1 card from
//   your deck).
// AS-IS: ActivateClass on OnDestroyedAnyone, SetIsInheritedEffect(true). PermanentCondition =
//   IsPermanentExistsOnOpponentBattleAreaDigimon. CanUseCondition = IsExistOnBattleArea && IsOwnerTurn &&
//   CanTriggerOnPermanentDeleted(hashtable, PermanentCondition) && IsDPZeroDelete(hashtable). CanActivateCondition
//   = IsExistOnBattleArea && card.Owner.LibraryCards.Count >= 1. ORDER=-1, ISOPTIONAL=false. ActivateCoroutine =
//   new DrawClass(card.Owner, 1, activateClass).Draw().
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `card.Owner.LibraryCards` ->
//   `new Player(card.Context, card.Owner).LibraryCards`; DrawClass ctor -> mirror shape.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_049 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDestroyedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When an opponent's Digimon is deleted by dropping to 0 DP, trigger <Draw 1> (Draw 1 card from your deck).";
            }

            bool PermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card);
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentDeleted(hashtable, PermanentCondition))
                        {
                            if (CardEffectCommons.IsDPZeroDelete(hashtable))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).LibraryCards.Count >= 1)
                    {
                        return true;
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
