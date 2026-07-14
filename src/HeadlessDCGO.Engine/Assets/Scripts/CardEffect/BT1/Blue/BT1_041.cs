// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_041.cs
// TRUE AS-IS-verbatim re-port (P5 batch 2). 1:1 mirror of the original BT1_041 (BT1/Blue).
//   [On Play] Trigger <Draw 2>. (Draw 2 card from your deck.)
//   [When Attacking][inherited] If your opponent has a Digimon with no digivolution cards in play, gain 1 memory.
// AS-IS structure kept verbatim. `HasMatchConditionOpponentsPermanent` only has the id-shape overload
// (no Permanent-shape sibling), so the AS-IS `permanent => permanent.IsDigimon && permanent.HasNoDigivolutionCards`
// predicate is expressed via the established id idiom (IsBattleAreaDigimon + HasNoDigivolutionCards), same
// translation already used by the prior (pre-verbatim) mirror of this card.
// UNRESOLVED (kept verbatim, logged): AS-IS `card.Owner.CanAddMemory(activateClass)` — see BT1_030.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Choices;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_041 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 2", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Trigger <Draw 2>. (Draw 2 card from your deck.)";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (((IZoneStateReader)card.Context.ZoneMover).GetCards(card.Owner, ChoiceZone.Library).Count >= 1)
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new DrawClass(card.Context, card.Owner, 2, activateClass).Draw();
            }
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] If your opponent has a Digimon with no digivolution cards in play, gain 1 memory.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    // AS-IS `HasMatchConditionOpponentsPermanent(card, (permanent) => permanent.IsDigimon &&
                    // permanent.HasNoDigivolutionCards)` — id-shape idiom (see file header).
                    if (CardEffectCommons.HasMatchConditionOpponentsPermanent(
                        card, id => CardEffectCommons.IsBattleAreaDigimon(card, id) && CardEffectCommons.HasNoDigivolutionCards(card, id)))
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
