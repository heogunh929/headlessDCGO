// Source: DCGO/Assets/Scripts/CardEffect/BT8/Green/BT8_057.cs
// TRUE AS-IS-verbatim re-port (bridge-W5 pass; E-3 WITNESS card — the AS-IS fidelity of BOTH halves is
// load-bearing). 1:1 mirror of the original BT8_057 (BT8/Green Digimon).
//   [None/All Turns] "While all of your Digimon are suspended, during your opponent's turn, they can't use
//          Option cards." — literal AS-IS `CanNotPlayClass` + SetUpCanNotPlayClass (the ported kind-class).
//   [Your Turn] "When this Digimon becomes unsuspended during your unsuspend phase, trash the top card of
//          your opponent's security stack." — literal AS-IS inline `new ActivateClass()`.
// Replaces the PREVIOUS pass's invented `CardEffectFactory.CanNotPlayOptionStaticEffect(...)` call and
// old-model `ActivatedEffect`/`TrashSecurityBody` half with the literal AS-IS structure.
// Substrate translation only: IEnumerator->Task, `ContinuousController.instance.StartCoroutine(X)`->`await X`;
// `card.Owner.GetBattleAreaDigimons()` -> the batch-3 `HeadlessPlayerId.GetBattleAreaDigimons()` extension;
// `card.Owner.Enemy` -> `new Player(card.Context, card.Owner).Enemy` (the established BT2_023 idiom; the
// owner-equality compares `.PlayerId`); `permanent => permanent == card.PermanentOfThisCard()` ->
// `permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId` (the established BT2_002/BT22_003
// Permanent-vs-PermanentView identity idiom); `card.Owner.Enemy.SecurityCards.Count` ->
// `CardEffectCommons.OpponentSecurityCount(card)` (the mirror helper documented against exactly this AS-IS
// line); AS-IS `new IDestroySecurity(player, count, cardEffect, fromTop)` -> the MIG3-3a mirror carrier's
// `(EngineContext, HeadlessPlayerId, int, HeadlessEntityId? cause, bool fromTop)` shape (player =
// `CardEffectCommons.OpponentOf(card)`, cause = `activateClass.EffectSourceCard.InstanceId`).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Green;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT8_057 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            CanNotPlayClass canNotPlayClass = new CanNotPlayClass();
            canNotPlayClass.SetUpICardEffect("Opponent can't us Option", CanUseCondition, card);
            canNotPlayClass.SetUpCanNotPlayClass(cardCondition: CardCondition);

            cardEffects.Add(canNotPlayClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (card.Owner.GetBattleAreaDigimons().Count == card.Owner.GetBattleAreaDigimons().Count((permanent) => permanent.IsSuspended))
                    {
                        if (CardEffectCommons.IsOpponentTurn(card))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }



            bool CardCondition(CardSource cardSource)
            {
                if (cardSource != null)
                {
                    if (cardSource.Owner == new Player(card.Context, card.Owner).Enemy?.PlayerId)
                    {
                        if (cardSource.IsOption)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        if (timing == EffectTiming.OnUnTappedAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash the top card of opponent's security", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When this Digimon becomes unsuspended during your unsuspend phase, trash the top card of your opponent's security stack.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (GManager.instance.turnStateMachine.gameContext.TurnPhase == GameContext.phase.Active)
                        {
                            if (CardEffectCommons.CanTriggerWhenPermanentUnsuspends(hashtable, permanent => permanent.InstanceId == card.PermanentOfThisCard().TopInstanceId))
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
                    if (CardEffectCommons.OpponentSecurityCount(card) >= 1)
                    {
                        return true;
                    }
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
