// Source: DCGO/Assets/Scripts/CardEffect/BT2/Black/BT2_112.cs (139 lines) — TRUE AS-IS 1:1 re-port.
// Witness card for the freshly-relocated IsMaxDP predicate (MinMax_DP_Cost_Level/DP/IsMaxDP.cs).
//   [None]           Play Cost -6 while in hand and the opponent has a Digimon with DP >= 10000
//                    (ChangeCostClass, the dynamic play-cost kind-class — BT2_099 sibling shape).
//   [When Attacking] When you attack the opponent's highest-DP Digimon, unsuspend this Digimon (OnAllyAttack).
// Structure kept verbatim (inline `new ChangeCostClass()` / `new ActivateClass()` + `IUnsuspendPermanents
// (...).Unsuspend()`, the BT2_099 / BT9_043 sibling idioms); no invented helpers.
// Substrate translations only:
//   * IEnumerator -> async Task, `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X`
//     (BT9_043 idiom).
//   * `card.Owner.HandCards.Contains(card)` -> `CardEffectCommons.IsExistOnHand(card)` (BT2_099 idiom).
//   * AS-IS `HasMatchConditionOpponentsPermanent(card, (permanent) => permanent.IsDigimon && permanent.DP >=
//     10000)` — the mirror overload is the canonical AS-IS `Func<Permanent,bool>` shape, so the VERBATIM
//     Permanent predicate (OpponentHighDp) is passed straight through; it is evaluated (NOT flattened) against
//     each opponent battle-area permanent.
//   * AS-IS `IsMaxDP(GManager.instance.attackProcess.DefendingPermanent, card.Owner.Enemy, null)` -> the mirror
//     overload `IsMaxDP(Permanent?, HeadlessPlayerId, Func<Permanent,bool>?)`; `GManager.instance.attackProcess
//     .DefendingPermanent` resolves 1:1 (BT2_012/ST4_04 idiom); `card.Owner.Enemy` -> `CardEffectCommons
//     .OpponentOf(card)`.
//   * AS-IS `card.PermanentOfThisCard()` -> `ICardEffect.ResolvePermanentOfThisCard(card)` (BT9_043 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Black;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_112 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect($"Play Cost -6", CanUseCondition, card);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);

            cardEffects.Add(changeCostClass);

            // VERBATIM AS-IS Permanent predicate `permanent.IsDigimon && permanent.DP >= 10000`, evaluated (NOT
            // flattened) by HasMatchConditionOpponentsPermanent for each opponent battle-area permanent.
            bool OpponentHighDp(Permanent permanent) => permanent.IsDigimon && permanent.DP >= 10000;

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, OpponentHighDp))
                    {
                        return true;
                    }
                }

                return false;
            }

            int ChangeCost(CardSource cardSource, int Cost, SelectCardEffect.Root root, List<Permanent> targetPermanents)
            {
                if (CardSourceCondition(cardSource))
                {
                    if (RootCondition(root))
                    {
                        if (PermanentsCondition(targetPermanents))
                        {
                            Cost -= 6;
                        }
                    }
                }

                return Cost;
            }

            bool PermanentsCondition(List<Permanent> targetPermanents)
            {
                if (targetPermanents == null)
                {
                    return true;
                }

                else
                {
                    if (targetPermanents.Count((targetPermanent) => targetPermanent != null) == 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CardSourceCondition(CardSource cardSource)
            {
                return cardSource == card;
            }

            bool RootCondition(SelectCardEffect.Root root)
            {
                if (root == SelectCardEffect.Root.Hand)
                {
                    return true;
                }

                return false;
            }

            bool isUpDown()
            {
                return true;
            }
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Unsuspend this Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] When you attack your opponent's Digimon with the highest DP, unsuspend this Digimon.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerOnAttack(hashtable, card))
                {
                    if (CardEffectCommons.IsMaxDP(GManager.instance.attackProcess.DefendingPermanent, CardEffectCommons.OpponentOf(card), null))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.CanUnsuspend(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent selectedPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                await new IUnsuspendPermanents(new List<Permanent>() { selectedPermanent }, activateClass).Unsuspend();
            }
        }

        return cardEffects;
    }
}
