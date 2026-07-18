// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S3 카드 — EX7_010 (Digimon / Red)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX7/Red/EX7_010.cs (184 lines, 6 regions)
//    * Digivolution Condition :15-24  (timing None — AddSelfDigivolutionRequirementStaticEffect,
//                                       [Three Musketeers] lvl3, cost 2)
//    * When Digivolving       :73-94  (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving —
//                                       **미러 방언 재배선**: WhenDigivolving 전용 키)
//    * When Attacking         :97-120 (OnAllyAttack — CanTriggerOnAttack)
//    * Your Turn              :122-155(timing None — ChangeTraitsClass, self-scope trait grant)
//    * Inherit                :158-179(timing None — ChangeSelfDPStaticEffect isInheritedEffect true)
//
// ② 프리미티브 매핑:
//    * P:AddSelfDigivolutionRequirementStaticEffect, P:SelectTrashDigivolutionCards, P:ChangeTraitsClass,
//      P:ChangeSelfDPStaticEffect
//    * `cardSource.CanNotTrashFromDigivolutionCards(activateClass)` — 미러 public 인스턴스 표면 실존
//      (CardSource.cs:1540) 그대로.
//
// ③ 배선 관례 근거:
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(trigger-wiring rule 3, §2.7).
//    * [When Attacking] → OnAllyAttack + CanTriggerOnAttack(hashtable, card) 그대로.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)` → `await X`.
//    * `card.PermanentOfThisCard().TopCard == card` (값-동등, AS-IS :136) → `ICardEffect.ResolvePermanentOfThisCard(card).TopCard == card`.
//    * ActivateCoroutineShared는 AS-IS도 (Hashtable, ActivateClass) 2-파라미터 로컬 함수로 양쪽(When
//      Digivolving/When Attacking)에서 클로저 캡처(`(hashtable) => ActivateCoroutineShared(hashtable,
//      activateClass)`) — 그대로 유지(1:1).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX7.Red;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX7_010 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Digivolution Condition
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.HasText("Three Musketeers") && targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 3 || targetPermanent.TopCard.HasText("ThreeMusketeers") && targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 3;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 2, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Shared WD / WA

        string EffectDescription(string tag)
        {
            return $"[{tag}] [Once Per Turn] You may trash 1 Option card in 1 Digimon's digivolution cards.";
        }

        bool CanSelectPermanentCondition(Permanent permanent)
        {
            return CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent);
        }

        bool CanActivateConditionShared(Hashtable hashtable)
        {
            return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
        }

        async Task ActivateCoroutineShared(Hashtable _hashtable, ActivateClass activateClass)
        {
            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (!cardSource.CanNotTrashFromDigivolutionCards(activateClass))
                {
                    if (cardSource.IsOption)
                    {
                        return true;
                    }
                }

                return false;
            }

            await CardEffectCommons.SelectTrashDigivolutionCards(
                permanentCondition: CanSelectPermanentCondition,
                cardCondition: CanSelectCardCondition,
                maxCount: 1,
                canNoTrash: false,
                isFromOnly1Permanent: true,
                sourceCard: card
            );
        }

        #endregion

        #region When Digivolving
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 Option from the digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, (hashtable) => ActivateCoroutineShared(hashtable, activateClass), 1, true, EffectDescription("When Digivolving"));
            activateClass.SetHashString("TrashOption_EX7_010");
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        #endregion

        #region When Attacking
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Trash 1 Option from the digivolution cards", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateConditionShared, (hashtable) => ActivateCoroutineShared(hashtable, activateClass), 1, true, EffectDescription("When Attacking"));
            activateClass.SetHashString("TrashOption_EX7_010");
            cardEffects.Add(activateClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.CanTriggerOnAttack(hashtable, card))
                    {
                        return true;
                    }
                }

                return false;
            }

        }
        #endregion

        #region Your Turn
        if (timing == EffectTiming.None)
        {
            ChangeTraitsClass changeTraitsClass = new ChangeTraitsClass();
            changeTraitsClass.SetUpICardEffect("This Digimon gains the [Three Musketeers] trait.", CanUseCondition, card);
            changeTraitsClass.SetUpChangeTraitsClass(changeeTraits: changeTraits);
            cardEffects.Add(changeTraitsClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsOwnerTurn(card))
                {
                    if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                    {
                        if (ICardEffect.ResolvePermanentOfThisCard(card).TopCard == card)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            List<string> changeTraits(CardSource cardSource, List<string> CardTraits)
            {
                if (cardSource == card)
                {
                    CardTraits.Add("Three Musketeers");
                }

                return CardTraits;
            }
        }
        #endregion

        #region Inherit
        if (timing == EffectTiming.None)
        {
            bool Condition()
            {
                if (CardEffectCommons.IsOwnerTurn(card))
                {
                    if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                    {
                        return true;
                    }
                }

                return false;
            }

            cardEffects.Add(CardEffectFactory.ChangeSelfDPStaticEffect(
                changeValue: 2000,
                isInheritedEffect: true,
                card: card,
                condition: Condition));
        }
        #endregion

        return cardEffects;
    }
}
