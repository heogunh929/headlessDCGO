// Source: DCGO/Assets/Scripts/CardEffect/BT1/Blue/BT1_003.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the original
// BT1_003 (BT1/Blue).
//   [When Attacking][Once Per Turn] If your opponent has a Digimon with no digivolution cards in play,
//   trigger <Draw 1>.
// AS-IS structure kept verbatim: inline `new ActivateClass()` + SetUpICardEffect/SetUpActivateClass + local
// functions, SetIsInheritedEffect(true) (AS-IS BT1_003.cs:20). Substrate translations only: `card.Owner.
// HasMatchConditionOpponentsPermanent`'s AS-IS `Func<Permanent,bool>` predicate -> the mirror
// `HasMatchConditionOpponentsPermanent(card, Func<HeadlessEntityId,bool>)` overload (established idiom, same
// predicate split as the previous pass's id-shape adapter: IsBattleAreaDigimon + HasNoDigivolutionCards);
// `new DrawClass(card.Owner, 1, activateClass).Draw()` -> the mirror ctor (EngineContext/HeadlessPlayerId/
// HeadlessEntityId? cause), the established BT1_029/BT1_092 idiom.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT1_003 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking][Once Per Turn] If your opponent has a Digimon with no digivolution cards in play, trigger <Draw 1> (Draw 1 card from your deck).";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionOpponentsPermanent(
                        card, id => CardEffectCommons.IsBattleAreaDigimon(card, id) && CardEffectCommons.HasNoDigivolutionCards(card, id)))
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
