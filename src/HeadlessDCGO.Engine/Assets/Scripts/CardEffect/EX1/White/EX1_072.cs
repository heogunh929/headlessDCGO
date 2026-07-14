// Source: DCGO/Assets/Scripts/CardEffect/EX1/White/EX1_072.cs
// P8 CUTOVER re-port (old-model ActivatedEffect -> new-model ActivateClass). 1:1 mirror of the AS-IS EX1_072
// (EX1/White Option). (E-3 witness — the CanNotPlayOption player-grant fidelity is load-bearing.)
//   [Main]     "Your opponent can't use Option cards until the end of their next turn."
//   [Security] "Your opponent can't use Option cards this turn. Then, add this card to its owner's hand."
// AS-IS: two ActivateClass effects. [Main] ActivateCoroutine builds a CanNotPlayClass (cardCondition =
// source.Owner == card.Owner.Enemy && source.IsOption) and CardEffectCommons.AddEffectToPlayer(
// UntilOpponentTurnEnd, ..., None). [Security] does the same with UntilEachTurnEnd, SetIsSecurityEffect(true),
// THEN AddThisCardToHand(card).
// Substrate translation: the AS-IS raw `new CanNotPlayClass()` + `AddEffectToPlayer(... , timing: None)` has NO
// working mirror path (AddEffectToPlayer / AddContinuousEffectToPlayer lower any effect via
// LegacyBindingBridge.TryToBinding, which throws for a new-model kind-class — RD-P6C3-C1: no new-model
// player-grant store). The mirror carrier of exactly this AS-IS CanNotPlayOption player grant is
// `CardEffectCommons.AddCanNotPlayOptionToPlayer(effectDuration, card, cardCondition)` (region ① player-bucket
// ContinuousCanNotPlayOptionEffect, playerScope:true, CanUse always true — scanned by CanNotPlayOptionScan,
// expired by EffectDurationExpiry at the matching turn end), the documented E-3 mirror. IEnumerator->Task,
// StartCoroutine->await; `card.Owner.Enemy` -> `CardEffectCommons.OpponentOf(card)`; `AddThisCardToHand(card,
// activateClass)` -> mirror `AddThisCardToHand(card, card)` (BT9_109 convention).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX1.White;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Effects;

public sealed class EX1_072 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OptionSkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect(card.BaseENGCardNameFromEntity, CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Main] Your opponent can't use Option cards until the end of their next turn.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOptionMainEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                CardEffectCommons.AddCanNotPlayOptionToPlayer(
                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                    card: card,
                    cardCondition: CardCondition);

                await Task.CompletedTask;

                bool CardCondition(CardSource cardSource)
                {
                    if (cardSource.Owner == CardEffectCommons.OpponentOf(card))
                    {
                        if (cardSource.IsOption)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Opponent can't play option and add this card to hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(null, ActivateCoroutine, -1, false, EffectDiscription());
            activateClass.SetIsSecurityEffect(true);
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Security] Your opponent can't use Option cards this turn. Then, add this card to its owner's hand.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerSecurityEffect(hashtable, card);
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                CardEffectCommons.AddCanNotPlayOptionToPlayer(
                    effectDuration: EffectDuration.UntilEachTurnEnd,
                    card: card,
                    cardCondition: CardCondition);

                await CardEffectCommons.AddThisCardToHand(card, card);

                bool CardCondition(CardSource cardSource)
                {
                    if (cardSource.Owner == CardEffectCommons.OpponentOf(card))
                    {
                        if (cardSource.IsOption)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        return cardEffects;
    }
}
