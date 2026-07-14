// Source: DCGO/Assets/Scripts/CardEffect/BT1/Yellow/BT1_053.cs
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS
// BT1_053 (BT1/Yellow) — inline `new ActivateClass()` + local functions.
//   [Your Turn] When you play a level 3 yellow Digimon, if this Digimon is suspended, trigger <Draw 1> (Draw 1
//   card from your deck).
// AS-IS: ActivateClass on OnEnterFieldAnyone. PermanentCondition = IsPermanentExistsOnOwnerBattleAreaDigimon &&
//   TopCard.CardColors.Contains(Yellow) && TopCard.HasLevel && TopCard.Level == 3. CanUseCondition =
//   IsExistOnBattleArea && IsOwnerTurn && CanTriggerOnPermanentPlay(hashtable, PermanentCondition).
//   CanActivateCondition = IsExistOnBattleArea && card.PermanentOfThisCard().IsSuspended &&
//   card.Owner.LibraryCards.Count >= 1. ORDER=-1, ISOPTIONAL=false. ActivateCoroutine =
//   new DrawClass(card.Owner, 1, activateClass).Draw().
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `TopCard.CardColors.Contains(
//   CardColor.Yellow)` -> `TopCard.HasCardColor("Yellow")` (mirror string colors); `card.PermanentOfThisCard()`
//   -> `ICardEffect.ResolvePermanentOfThisCard(card)`; `card.Owner.LibraryCards` -> `new Player(...).LibraryCards`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Yellow;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_053 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When you play a level 3 yellow Digimon, if this Digimon is suspended, trigger <Draw 1> (Draw 1 card from your deck).";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.HasCardColor("Yellow"))
                    {
                        if (permanent.TopCard.HasLevel)
                        {
                            if (permanent.TopCard.Level == 3)
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentPlay(hashtable, PermanentCondition))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (ICardEffect.ResolvePermanentOfThisCard(card).IsSuspended)
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
                await new DrawClass(card.Context, card.Owner, 1, activateClass.EffectSourceCard?.InstanceId).Draw();
            }
        }

        return cardEffects;
    }
}
