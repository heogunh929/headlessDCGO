// Source: DCGO/Assets/Scripts/CardEffect/BT2/Blue/BT2_023.cs
// TRUE AS-IS-verbatim re-port (batch 3). 1:1 mirror of the original BT2_023 (BT2/Blue).
//   Play cost reduced by the number of opponent Digimon with no digivolution cards in the battle area.
// Replaces the PREVIOUS pass's old-model `CardEffectFactory.BeforePayCostReductionEffect(...)` call (an
// invented helper with no AS-IS counterpart) with the literal AS-IS inline `new ChangeCostClass()` structure
// (ChangeCostClass IS a real AS-IS kind-class, mirrored verbatim at Script/CardEffects/ChangeCostClass.cs).
// Substrate translations only: `card.Owner.HandCards.Contains(card)` -> `CardEffectCommons.IsExistOnHand(card)`
// (an established 1:1-semantics bridge: `GetCards(card.Owner, ChoiceZone.Hand).Contains(card.InstanceId)`);
// `card.Owner.Enemy.GetBattleAreaDigimons()` (AS-IS live `Player.Enemy`) -> `new Player(card.Context,
// card.Owner).Enemy` (mirror `CardSource.Owner` is a bare `HeadlessPlayerId`, so the Player handle is
// reconstructed — same idiom as BT1_104's `card.Owner.Enemy` translation), null-conditional + `?? 0` since
// `Player.Enemy` is nullable on the mirror.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Blue;

using System.Collections;
using System.Linq;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT2_023 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            int count()
            {
                return new Player(card.Context, card.Owner).Enemy?.GetBattleAreaDigimons().Count(permanent => permanent.HasNoDigivolutionCards) ?? 0;
            }

            ChangeCostClass changeCostClass = new ChangeCostClass();
            changeCostClass.SetUpICardEffect($"Play Cost -", CanUseCondition, card);
            changeCostClass.SetUpChangeCostClass(changeCostFunc: ChangeCost, cardSourceCondition: CardSourceCondition, rootCondition: RootCondition, isUpDown: isUpDown, isCheckAvailability: () => false, isChangePayingCost: () => true);

            cardEffects.Add(changeCostClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnHand(card))
                {
                    if (count() >= 1)
                    {
                        changeCostClass.SetEffectName($"Play Cost -{count()}");

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
                            Cost -= count();
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

        return cardEffects;
    }
}
