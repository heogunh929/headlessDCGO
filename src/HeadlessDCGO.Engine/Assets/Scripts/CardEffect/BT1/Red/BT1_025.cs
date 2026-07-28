// TRUE AS-IS-verbatim re-port (P5, bridge-complete pass) of the [When Digivolving] half.
// 1:1 mirror of the original BT1_025 (BT1/Red).
//   [When Digivolving] This Digimon gains <Security Attack +1> (This Digimon checks 1 additional security
//                       card) for the turn.
//   AS-IS: ActivateClass on EffectTiming.WhenDigivolving, CanUseCondition = CanTriggerWhenDigivolving(hashtable,
//   card), CanActivateCondition = IsExistOnBattleArea(card), ORDER=-1, ISOPTIONAL=false, ActivateCoroutine =
//   ChangeDigimonSAttack(targetPermanent: card.PermanentOfThisCard(), changeValue: 1, UntilEachTurnEnd) — a
//   fixed SELF-target buff, no player selection. Kept as the AS-IS inline `new ActivateClass()` +
//   SetUpICardEffect/SetUpActivateClass + local-function structure (the prior pass had this on the old
//   ActivatedEffect/IEffectBody model; now resolves verbatim via the bridge's AS-IS-signature
//   ChangeDigimonSAttack overload).
//
//   [All Turns] "Ignore Security Effect": a SECOND, independent effect — timing None, DisableEffectClass.
//   SetUpDisableEffectClass(InvalidateCondition), InvalidateCondition(cardEffect) = IsExistOnBattleArea(card)
//   && IsOwnerTurn(card) && cardEffect?.EffectSourceCard?.IsOption == true && cardEffect.IsSecurityEffect &&
//   attackProcess.AttackingPermanent == card.PermanentOfThisCard() — while this Digimon is the attacker on its
//   owner's turn, negate any Option-card-sourced [Security] effect. Unchanged by this pass — already AS-IS
//   verbatim (see docs/audit/rebuild_p5_cards_missing.md for the dispatch-wiring caveat: DisableEffectClass/
//   CheckEffectDisabledClass gates the LEGACY ICardEffect.CanUse path; whether it is consulted by the newer
//   ActivatedEffect-based [Security] resolution used for most ported Option cards is unconfirmed).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT1.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT1_025 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("This Digimon gains Security Attack +1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] This Digimon gains <Security Attack +1> (This Digimon checks 1 additional security card) for the turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.ChangeDigimonSAttack(
                    targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                    changeValue: 1,
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    activateClass: activateClass);
            }
        }

        if (timing == EffectTiming.None)
        {
            // AS-IS: DisableEffectClass invalidationClass; SetUpICardEffect("Ignore Security Effect",
            // CanUseCondition, card); SetUpDisableEffectClass(InvalidateCondition).
            var invalidationClass = new DisableEffectClass();
            invalidationClass.SetUpICardEffect("Ignore Security Effect", CanUseCondition, card);
            invalidationClass.SetUpDisableEffectClass(InvalidateCondition);
            cardEffects.Add(invalidationClass);

            bool CanUseCondition(Hashtable hashtable) => true;

            bool InvalidateCondition(ICardEffect cardEffect)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (cardEffect != null)
                        {
                            if (cardEffect.EffectSourceCard != null)
                            {
                                if (cardEffect.EffectSourceCard.IsOption)
                                {
                                    if (cardEffect.IsSecurityEffect)
                                    {
                                        if (card.Context.AttackController.Current.AttackerId
                                            == ICardEffect.ResolvePermanentOfThisCard(card).InstanceId)
                                        {
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }
        }

        return cardEffects;
    }
}
