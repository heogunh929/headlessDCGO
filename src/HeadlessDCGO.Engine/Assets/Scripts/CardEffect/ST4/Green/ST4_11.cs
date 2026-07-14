// Source: DCGO/Assets/Scripts/CardEffect/ST4/Green/ST4_11.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS ST4_11.
//   [Your Turn][Once Per Turn] When this Digimon deletes an opponent's Digimon in battle and survives,
//   trash the top card of your opponent's security stack.
// AS-IS: ActivateClass on OnEndBattle, SetIsInheritedEffect(true), SetHashString("TrashSecurity_ST4_11"),
// ORDER=1 ([Once Per Turn]), ISOPTIONAL=false. CanUseCondition = IsExistOnBattleArea && IsOwnerTurn &&
// CanTriggerWhenDeleteOpponentDigimonByBattle(winner = permanent.cardSources.Contains(card), loser =
// IsOpponentPermanent, isOnlyWinnerSurvive:true). CanActivateCondition = IsExistOnBattleArea. ActivateCoroutine
// = new IDestroySecurity(opponent, 1, cardEffect, fromTop:true).DestroySecurity().
// Substrate translations only: IEnumerator->Task, StartCoroutine->await; AS-IS `new IDestroySecurity(player:
// card.Owner.Enemy, destroySecurityCount: 1, cardEffect: activateClass, fromTop: true)` -> the mirror carrier
// `(card.Context, CardEffectCommons.OpponentOf(card), 1, activateClass.EffectSourceCard.InstanceId,
// fromTop: true)` (established BT8_057 idiom). AS-IS `permanent.cardSources.Contains(card)` kept verbatim
// (mirror Permanent exposes cardSources).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.ST4.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class ST4_11 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEndBattle)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash the top card of opponent's security", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("TrashSecurity_ST4_11");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn][Once Per Turn] When this Digimon deletes an opponent's Digimon in battle and survives, trash the top card of your opponent's security stack.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        bool WinnerCondition(Permanent permanent) => permanent.cardSources.Contains(card);
                        bool LoserCondition(Permanent permanent) => CardEffectCommons.IsOpponentPermanent(permanent, card);

                        if (CardEffectCommons.CanTriggerWhenDeleteOpponentDigimonByBattle(hashtable: hashtable, winnerCondition: WinnerCondition, loserCondition: LoserCondition, isOnlyWinnerSurvive: true))
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
                    return true;
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new IDestroySecurity(
                    card.Context,
                    CardEffectCommons.OpponentOf(card),
                    1,
                    activateClass.EffectSourceCard.InstanceId,
                    fromTop: true).DestroySecurity();
            }
        }

        return cardEffects;
    }
}
