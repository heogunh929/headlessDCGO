// Source: DCGO/Assets/Scripts/CardEffect/BT6/Black/BT6_067.cs (88 lines) — TRUE AS-IS 1:1 re-port.
// Witness card for the freshly-relocated IsMinCost predicate (MinMax_DP_Cost_Level/Cost/IsMinCost.cs).
//   [When Digivolving] Delete all of your opponent's Digimon with the lowest play cost (OnEnterFieldAnyone).
//   [None]             +1 SAttack while this Digimon is on the battle area during the owner's turn and the
//                      opponent has an unsuspended Digimon (ChangeSelfSAttackStaticEffect).
// Structure kept verbatim (inline `new ActivateClass()` + `DestroyPermanentsClass(...).Destroy()`, the BT9_111
// sibling idiom); no invented helpers.
// Substrate translations only:
//   * IEnumerator -> async Task, `yield return ContinuousController.instance.StartCoroutine(X)` -> `await X`
//     (BT9_111 idiom).
//   * `card.Owner.Enemy.GetBattleAreaDigimons()` -> `new Player(card.Context, card.Owner).Enemy!
//     .GetBattleAreaDigimons()` (BT9_111 idiom).
//   * AS-IS `IsMinCost(permanent, card.Owner.Enemy, true)` (`Player Enemy` owner) -> mirror overload
//     `IsMinCost(Permanent?, HeadlessPlayerId, bool, ...)`; `card.Owner.Enemy` -> `CardEffectCommons
//     .OpponentOf(card)`. Predicate evaluated, NOT flattened (IsPermanentExists guard kept verbatim).
//   * AS-IS card-less `HasMatchConditionPermanent(PermanentCondition)` -> the mirror `(card, cond)` overload
//     (BT9_111 convention).
//   * AS-IS `HasMatchConditionOpponentsPermanent(card, (permanent) => permanent.IsDigimon && !permanent
//     .IsSuspended)` — the mirror overload takes `Func<HeadlessEntityId,bool>`, so the VERBATIM Permanent
//     predicate is wrapped by an id-resolving adapter (EX8_051 idiom); the predicate itself is unchanged.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT6.Black;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT6_067 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete opponent's all Digimon with the lowest play cost", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] Delete all of your opponent's Digimon with the lowest play cost.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (CardEffectCommons.IsMinCost(permanent, CardEffectCommons.OpponentOf(card), true))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, PermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<Permanent> destroyTargetPermanents = new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Filter(PermanentCondition);
                await new DestroyPermanentsClass(destroyTargetPermanents, CardEffectCommons.CardEffectHashtable(activateClass)).Destroy();
            }
        }

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent permanent)
            {
                return permanent.IsDigimon && !permanent.IsSuspended;
            }

            // Id-shape adapter for the W4-bridged HasMatchConditionOpponentsPermanent (takes Func<HeadlessEntityId,
            // bool>): resolve the mirror Permanent for the candidate id (EX8_051 idiom) and evaluate the VERBATIM
            // AS-IS Permanent predicate above.
            bool PermanentConditionById(HeadlessEntityId id) =>
                card.Context.CardInstanceRepository.TryGetInstance(id, out CardInstanceRecord? rec) && rec is not null
                && PermanentCondition(new Permanent(card.Context, id, rec.OwnerId));

            bool Condition()
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.HasMatchConditionOpponentsPermanent(card, PermanentConditionById))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(changeValue: 1, isInheritedEffect: false, card: card, condition: Condition));
        }

        return cardEffects;
    }
}
