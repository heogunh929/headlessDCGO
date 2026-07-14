// Source: DCGO/Assets/Scripts/CardEffect/BT8/Black/BT8_092.cs
// R5-A re-port. All three AS-IS branches now mirrored as new-model ActivateClass:
//   * [Your Turn] OnMove: gain 1 memory and <Draw 1> when an [X-Antibody] Digimon moves to the battle area.
//   * [Your Turn] OnAllyAttack (RD-P8-01 RESOLVED — the mirror SelectHandEffect now exists,
//     Script/SelectHandEffect.cs): when a black [X-Antibody] Digimon attacks, you may suspend this Tamer to place
//     1 [X-Antibody] card from hand under that Digimon as its bottom digivolution card.
//   * [Security] SecuritySkill: PlaySelfTamerSecurityEffect.
// AS-IS structure kept verbatim (BT8_092.cs:15-201): inline ActivateClass + local functions; the ActivateCoroutines
// mirror the gold 1:1 (Draw-1-THEN-gain-1-memory order; suspend-cost → SelectHandEffect(Mode.Custom) →
// AddDigivolutionCardsBottom onto the attacking permanent). Substrate translations only: IEnumerator->Task,
// StartCoroutine->await; `new DrawClass(card.Owner, 1, activateClass)` -> the mirror ctor
// (EngineContext/HeadlessPlayerId/cause-id); `card.Owner.AddMemory`/`CanAddMemory` -> the HeadlessPlayerId
// extensions; `card.Owner.LibraryCards`/`HandCards` -> new Player(card.Context, card.Owner).*; `card.
// PermanentOfThisCard()` -> ICardEffect.ResolvePermanentOfThisCard(card); `GManager.instance.attackProcess.
// AttackingPermanent` (bridge W5); `SuspendPermanentsClass(list, CardEffectHashtable(activateClass))` /
// `AddDigivolutionCardsBottom(cards, activateClass)` -> the mirror ctors (cause = effect-source id);
// `TopCard.CardColors.Contains(CardColor.Black)` -> `.CardColors.Contains("Black")` (BT2_099 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT8.Black;

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;

public sealed class BT8_092 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.OnMove)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Memory +1 and Draw 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When one of your Digimon with [X-Antibody] in its traits is moved from your breeding area to your battle area, gain 1 memory and <Draw 1>.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.HasXAntibodyTraits)
                    {
                        return true;
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
                        if (CardEffectCommons.CanTriggerOnMove(hashtable, PermanentCondition))
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
                    if (card.Owner.CanAddMemory(activateClass))
                    {
                        return true;
                    }

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

                await card.Owner.AddMemory(1, activateClass);
            }
        }

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Place a Card to digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Your Turn] When one of your black Digimon with [X-Antibody] in its traits attacks, you may suspend this Tamer to place 1 card with [X-Antibody] in its traits from your hand under that Digimon as its bottom digivolution card.";
            }

            bool PermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOwnerBattleAreaDigimon(permanent, card))
                {
                    if (permanent.TopCard.HasXAntibodyTraits)
                    {
                        if (permanent.TopCard.CardColors.Contains("Black"))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                return cardSource.HasXAntibodyTraits;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (CardEffectCommons.IsOwnerTurn(card))
                    {
                        if (CardEffectCommons.CanTriggerOnPermanentAttack(hashtable, PermanentCondition))
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
                    if (CardEffectCommons.CanActivateSuspendCostEffect(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await new SuspendPermanentsClass(new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass.EffectSourceCard?.InstanceId, isBlock: false).Tap();

                Permanent? attackingPermanent = GManager.instance.attackProcess.AttackingPermanent;

                if (attackingPermanent != null)
                {
                    // AS-IS `AttackingPermanent.TopCard != null && !IsToken` — the mirror Permanent.TopCard is always
                    // non-null, so the AS-IS "still has a top card" guard maps to the battle-area existence check.
                    if (CardEffectCommons.IsPermanentExistsOnBattleArea(attackingPermanent) && !attackingPermanent.IsToken)
                    {
                        Permanent selectedPermanent = attackingPermanent;

                        if (new Player(card.Context, card.Owner).HandCards.Count(CanSelectCardCondition) >= 1)
                        {
                            List<CardSource> selectedCards = new List<CardSource>();

                            int maxCount = 1;

                            SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                            selectHandEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanSelectCardCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: null,
                                maxCount: maxCount,
                                canNoSelect: true,
                                canEndNotMax: false,
                                isShowOpponent: true,
                                selectCardCoroutine: SelectCardCoroutine,
                                afterSelectCardCoroutine: null,
                                mode: SelectHandEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectHandEffect.SetUpCustomMessage("Select 1 card to place on bottom of digivolution cards.", "The opponent is selecting 1 card to place on bottom of digivolution cards.");

                            await selectHandEffect.Activate();

                            Task SelectCardCoroutine(CardSource cardSource)
                            {
                                selectedCards.Add(cardSource);

                                return Task.CompletedTask;
                            }

                            if (selectedCards.Count >= 1)
                            {
                                await selectedPermanent.AddDigivolutionCardsBottom(selectedCards, activateClass.EffectSourceCard?.InstanceId);
                            }
                        }
                    }
                }
            }
        }

        if (timing == EffectTiming.SecuritySkill)
        {
            cardEffects.Add(CardEffectFactory.PlaySelfTamerSecurityEffect(card));
        }

        return cardEffects;
    }
}
