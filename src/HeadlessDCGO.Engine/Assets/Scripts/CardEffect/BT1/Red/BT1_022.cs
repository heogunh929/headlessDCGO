// Source: DCGO/Assets/Scripts/CardEffect/BT1/Red/BT1_022.cs
// P8/R6-A CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass) of the [When blocked] draw
// branch; the [Piercing] self-static branch is already CardEffectFactory.PierceSelfEffect (unchanged).
//   [Piercing] (self, static) -> OnDetermineDoSecurityCheck: CardEffectFactory.PierceSelfEffect.
//   [Your Turn] When this Digimon is blocked, trigger <Draw 1>. (Draw 1 card from your deck.)
// AS-IS: ActivateClass on OnBlockAnyone, SetIsInheritedEffect(true). CanUseCondition = CanTriggerOnAttack &&
//   IsOwnerTurn. CanActivateCondition = IsExistOnBattleArea && card.Owner.LibraryCards.Count >= 1. ORDER=-1,
//   ISOPTIONAL=false. ActivateCoroutine = new DrawClass(card.Owner, 1, activateClass).Draw().
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; `card.Owner.LibraryCards` ->
//   `new Player(card.Context, card.Owner).LibraryCards`; DrawClass ctor -> mirror shape.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_022 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnDetermineDoSecurityCheck)
        {
            cardEffects.Add(CardEffectFactory.PierceSelfEffect(
                isInheritedEffect: false,
                card: card,
                condition: null));
        }

        if (timing == EffectTiming.OnBlockAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When this Digimon is blocked, trigger <Draw 1>. (Draw 1 card from your deck.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.CanTriggerOnAttack(hashtable, card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
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
                    if (new Player(card.Context, card.Owner).LibraryCards.Count >= 1)
                    {
                        return true;
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
