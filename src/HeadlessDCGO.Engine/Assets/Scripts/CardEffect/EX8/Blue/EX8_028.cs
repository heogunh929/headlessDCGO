// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// PILOT-S1 카드 — EX8_028 (Digimon / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/EX8/Blue/EX8_028.cs (343 lines, 5 regions)
//    * Alternate Digivolution Requirement :17-26  (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * Iceclad                            :30-35  (timing == None — IcecladSelfStaticEffect)
//    * Barrier                            :38-42  (timing == WhenPermanentWouldBeDeleted — BarrierSelfEffect)
//    * When Digivolving                   :45-144 (timing == OnEnterFieldAnyone — 무료 [Ice-Snow] 플레이)
//    * When Digivolving - OPT / When Attacking - OPT :148-338 (동일 몸통: 디지몬을 바닥 시큐리티로 두고 언서스펜드)
//
// ② 프리미티브 매핑:
//    * P:AddDigivolutionRequirementClass(AddSelfDigivolutionRequirementStaticEffect) — [Ice-Snow] Lv.5 위
//      코스트 3 (AS-IS :19-24)
//    * P:IcecladClass(IcecladSelfStaticEffect) — 상시 self (AS-IS :32)
//    * P:BarrierClass(BarrierSelfEffect) — WhenPermanentWouldBeDeleted self (AS-IS :40)
//    * E:SelectHandEffect Mode.Custom + P:PlayPermanentCards(payCost:false) — [When Digivolving] 무료 [Ice-Snow]
//      플레이 (AS-IS :104-142)
//    * E:SelectPermanentEffect Mode.Custom + P:PlacePermanentInSecurityAndProcessAccordingToResult(IPutSecurityPermanent
//      래핑) + P:IUnsuspendPermanents — [When Digivolving]-OPT / [When Attacking]-OPT 동일 몸통, 언서스펜드
//      (AS-IS :193-239 / :290-336)
//
// ③ 배선 관례 근거:
//    * [When Digivolving] 두 효과 모두 → OnEnterFieldAnyone + CanTriggerWhenDigivolving(hashtable, card) 게이트
//      (AS-IS :87-89 / :176-178 그대로).
//    * [When Attacking] OPT → OnAllyAttack + CanTriggerOnAttack(hashtable, card) 게이트(AS-IS :273-275 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`,
//      `yield return StartCoroutine(X)`→`await X` (BT17_026 idiom).
//    * `card.Owner.Enemy.GetBattleAreaDigimons()` → `new Player(card.Context, card.Owner).Enemy!
//      .GetBattleAreaDigimons()` (BT18_042/EX7_014 idiom).
//    * `new IPutSecurityPermanent(selectedCard, hashtable, false).PutSecurity()` — 미러에 해당 클래스 없음.
//      AS-IS `CardEffectCommons.PlacePermanentInSecurityAndProcessAccordingToResult`가 이 클래스를 그대로 래핑
//      한다는 사실이 AS-IS 원본(CardEffectCommons.cs:644-669)에 명시돼 있고, 미러에 그 AS-IS-시그니처 오버로드
//      (ProcessAccordingToResultBridge.cs — `(Permanent, ICardEffect, bool toTop, Func<CardSource,Task>
//      successProcess, Func<Task> failureProcess, bool isFaceUp)`)가 존재 — successProcess 콜백 인자가 AS-IS의
//      `_topCard` 캡처와 동일 페이로드(CardSource)이므로 그 안에 AS-IS 잔여 로직(!IsExistOnBattleArea 재확인→
//      IUnsuspendPermanents)을 그대로 옮긴다(발명 아님 — AS-IS 자체가 명시한 1:1 위임 관계 활용).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (BT8_092 관례).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.EX8.Blue;

using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class EX8_028 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Requirement
        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Ice-Snow") && targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 5;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }
        #endregion

        #region Iceclad

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.IcecladSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        #endregion

        #region Barrier
        if (timing == EffectTiming.WhenPermanentWouldBeDeleted)
        {
            cardEffects.Add(CardEffectFactory.BarrierSelfEffect(isInheritedEffect: false, card: card, condition: null));
        }
        #endregion

        #region When Digivolving
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Play 1 [Ice-Snow] trait Digimon card", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] You may play 1 level 4 or lower [Ice-Snow] trait Digimon card from your hand without paying the cost. For each of your opponent's Digimon with no digivolution cards, add 1 to this effect's level maximum.";
            }

            int MaxLevel()
            {
                int level = 4;

                level += new Player(card.Context, card.Owner).Enemy!.GetBattleAreaDigimons().Count(permanent => permanent.DigivolutionCards.Count == 0);

                return level;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                if (cardSource.IsDigimon)
                {
                    if (CardEffectCommons.CanPlayAsNewPermanent(cardSource: cardSource, payCost: false, cardEffect: activateClass))
                    {
                        if (cardSource.EqualsTraits("Ice-Snow"))
                        {
                            if (cardSource.HasLevel && cardSource.Level <= MaxLevel())
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card) &&
                       CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionOwnersHand(card, CanSelectCardCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                List<CardSource> selectedCards = new List<CardSource>();

                SelectHandEffect selectHandEffect = GManager.instance.GetComponent<SelectHandEffect>();

                selectHandEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectCardCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: true,
                    canEndNotMax: false,
                    isShowOpponent: true,
                    selectCardCoroutine: SelectCardCoroutine,
                    afterSelectCardCoroutine: null,
                    mode: SelectHandEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectHandEffect.SetUpCustomMessage("Select 1 card to play.", "The opponent is selecting 1 card to play.");
                selectHandEffect.SetUpCustomMessage_ShowCard("Played Card");

                await selectHandEffect.Activate();

                Task SelectCardCoroutine(CardSource cardSource)
                {
                    selectedCards.Add(cardSource);

                    return Task.CompletedTask;
                }

                await CardEffectCommons.PlayPermanentCards(
                    cardSources: selectedCards,
                    activateClass: activateClass,
                    payCost: false,
                    isTapped: false,
                    root: SelectCardEffect.Root.Hand,
                    activateETB: true);
            }
        }
        #endregion

        #region When Digivolving - OPT
        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By placing 1 digimon to bottom of security, unsuspend", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("Unsuspend_EX8_028");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Digivolving] [Once Per Turn] By placing 1 Digimon with no digivolution cards as the bottom security card, this Digimon unsuspends.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent))
                {
                    if (permanent.DigivolutionCards.Count == 0)
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card) &&
                       CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent? selectedCard = null;

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition_ByPreSelecetedList: null,
                    canTargetCondition: CanSelectPermanentCondition,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: CardSelected,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to place at bottom of security.", "The opponent is selecting 1 Digimon to place at bottom of security.");

                await selectPermanentEffect.Activate();

                Task CardSelected(Permanent permanent)
                {
                    if (permanent != null)
                        selectedCard = permanent;

                    return Task.CompletedTask;
                }

                if (selectedCard != null)
                {
                    if (!selectedCard.TopCard.CanNotBeAffected(activateClass))
                    {
                        await CardEffectCommons.PlacePermanentInSecurityAndProcessAccordingToResult(
                            selectedCard, activateClass, false, SuccessProcess, null);

                        async Task SuccessProcess(CardSource _topCard)
                        {
                            if (!CardEffectCommons.IsExistOnBattleArea(_topCard))
                                await new IUnsuspendPermanents(new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass).Unsuspend();
                        }
                    }
                }
            }
        }
        #endregion

        #region When Attacking - OPT
        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("By placing 1 digimon to bottom of security, unsuspend", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, true, EffectDiscription());
            activateClass.SetHashString("Unsuspend_EX8_028");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] [Once Per Turn] By placing 1 Digimon with no digivolution cards as the bottom security card, this Digimon unsuspends.";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnBattleAreaDigimon(permanent))
                {
                    if (permanent.DigivolutionCards.Count == 0)
                    {

                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card) &&
                       CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleAreaDigimon(card))
                {
                    if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                    {
                        return true;
                    }
                }

                return false;
            }

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                Permanent? selectedCard = null;

                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition_ByPreSelecetedList: null,
                    canTargetCondition: CanSelectPermanentCondition,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: CardSelected,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Custom,
                    cardEffect: activateClass);

                selectPermanentEffect.SetUpCustomMessage("Select 1 Digimon to place at bottom of security.", "The opponent is selecting 1 Digimon to place at bottom of security.");

                await selectPermanentEffect.Activate();

                Task CardSelected(Permanent permanent)
                {
                    if (permanent != null)
                        selectedCard = permanent;

                    return Task.CompletedTask;
                }

                if (selectedCard != null)
                {
                    if (!selectedCard.TopCard.CanNotBeAffected(activateClass))
                    {
                        await CardEffectCommons.PlacePermanentInSecurityAndProcessAccordingToResult(
                            selectedCard, activateClass, false, SuccessProcess, null);

                        async Task SuccessProcess(CardSource _topCard)
                        {
                            if (!CardEffectCommons.IsExistOnBattleArea(_topCard))
                                await new IUnsuspendPermanents(new List<Permanent>() { ICardEffect.ResolvePermanentOfThisCard(card) }, activateClass).Unsuspend();
                        }
                    }
                }
            }
        }
        #endregion

        return cardEffects;
    }
}
