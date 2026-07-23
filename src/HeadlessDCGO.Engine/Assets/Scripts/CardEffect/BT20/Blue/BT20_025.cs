// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — BT20_025 (Digimon / Blue)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT20/Blue/BT20_025.cs (217 lines, 5 regions)
//    * Alternate Digivolution :14-25  (timing == None — AddSelfDigivolutionRequirementStaticEffect)
//    * [On Play]               :29-77  (timing == OnEnterFieldAnyone + CanTriggerOnPlay 게이트)
//    * [When Digivolving]      :83-131 (timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving 게이트,
//                                        [On Play]와 동일 몸통)
//    * All Turns               :137-197(timing == None — AddJogressLevelsClass + ChangeCardNamesClass)
//    * Security Attack         :203-210(timing == None — ChangeSelfSAttackStaticEffect)
//
// ② 프리미티브 매핑:
//    * P:AddSelfDigivolutionRequirementStaticEffect — [Coredramon] 위 코스트 3 (AS-IS :21-22)
//    * P:SelectPermanentEffect Mode.Destroy — [On Play]/[When Digivolving] 6000DP 이하 삭제 (AS-IS :60-77/112-129)
//    * P:AddJogressLevelsClass — 손패 [Examon]이 이 퍼머넌트를 대상으로 DNA진화할 때 레벨 6 취급 추가 (AS-IS :139-172)
//    * P:ChangeCardNamesClass — 자신을 [Slayerdramon]으로도 취급 (AS-IS :177-196)
//    * P:ChangeSelfSAttackStaticEffect — 시큐리티 어택 +1, 계승 (AS-IS :205-209)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [On Play] → EffectTiming.OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card) 게이트(AS-IS :43 그대로).
//    * [When Digivolving] → EffectTiming.OnEnterFieldAnyone + CanTriggerWhenDigivolving(hashtable, card)
//      게이트(AS-IS :97 그대로) — AS-IS 원본이 [On Play]/[When Digivolving] 둘 다 OnEnterFieldAnyone에 두므로
//      미러도 동일 키(OnEnterFieldAnyone)에 두 ActivateClass를 등재(AS-IS 원본 구조 그대로, 이중-키 문제 아님
//      — 두 효과 모두 같은 AS-IS timing).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT17_026 idiom).
//    * AS-IS card-less `HasMatchConditionPermanent(cond)`(전역 스캔) → 미러 `(card, cond)` 오버로드
//      (ST2_06/ST1_08 관례); `MatchConditionPermanentCount`는 id-형 오버로드 사용(BT17_026 idiom).
//    * `new Player(card.Context, card.Owner).MaxDP_DeleteEffect(6000, activateClass)` — 미러 Player.MaxDP_DeleteEffect(int, ICardEffect)
//      1:1(R6-P).
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)`(BT8_092 관례).
//    * AS-IS :160 `cardSource.Owner.HandCards.Contains(cardSource)` — mirror `new Player(card.Context,
//      cardSource.Owner).HandCards.Contains(cardSource)`.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT20.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT20_025 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution

        if (timing == EffectTiming.None)
        {
            bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsCardName("Coredramon");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition,
                digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region On Play

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon with 6000 DP or less", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[On Play] Delete 1 of your opponent's Digimon with 6000 DP or less.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) &&
                       permanent.DP <= new Player(card.Context, card.Owner).MaxDP_DeleteEffect(6000, activateClass);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    cardEffect: activateClass);

                await selectPermanentEffect.Activate();
            }
        }

        #endregion

        #region When Digivolving

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Delete 1 Digimon with 6000 DP or less", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
            {
                return "[When Digivolving] Delete 1 of your opponent's Digimon with 6000 DP or less.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                return CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card) &&
                       permanent.DP <= new Player(card.Context, card.Owner).MaxDP_DeleteEffect(6000, activateClass);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card) &&
                       CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                selectPermanentEffect.SetUp(
                    selectPlayer: card.Owner,
                    canTargetCondition: CanSelectPermanentCondition,
                    canTargetCondition_ByPreSelecetedList: null,
                    canEndSelectCondition: null,
                    maxCount: 1,
                    canNoSelect: false,
                    canEndNotMax: false,
                    selectPermanentCoroutine: null,
                    afterSelectPermanentCoroutine: null,
                    mode: SelectPermanentEffect.Mode.Destroy,
                    cardEffect: activateClass);

                await selectPermanentEffect.Activate();
            }
        }

        #endregion

        #region All Turns

        if (timing == EffectTiming.None)
        {
            AddJogressLevelsClass addJogressLevelsClass = new AddJogressLevelsClass();
            addJogressLevelsClass.SetUpICardEffect("Also treated as level 6 for DNA Digivolution", CanUseCondition, card);
            addJogressLevelsClass.SetUpAddJogressLevelsClass(AddJogressLevels);

            cardEffects.Add(addJogressLevelsClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            List<int> AddJogressLevels(CardSource cardSource, Permanent permanent)
            {
                List<int> levels = new List<int>();

                if (permanent == ICardEffect.ResolvePermanentOfThisCard(card))
                {
                    if (cardSource != null)
                    {
                        if (cardSource.Owner == card.Owner)
                        {
                            if (new Player(card.Context, cardSource.Owner).HandCards.Contains(cardSource))
                            {
                                if (cardSource.CardNames.Contains("Examon"))
                                {
                                    levels.Add(6);
                                }
                            }
                        }
                    }
                }

                return levels;
            }
        }

        if (timing == EffectTiming.None)
        {
            ChangeCardNamesClass changeCardNamesClass = new ChangeCardNamesClass();
            changeCardNamesClass.SetUpICardEffect("Also treated as [Slayerdramon]", CanUseCondition, card);
            changeCardNamesClass.SetUpChangeCardNamesClass(changeCardNames: ChangeCardNames);

            cardEffects.Add(changeCardNamesClass);

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            List<string> ChangeCardNames(CardSource cardSource, List<string> cardNames)
            {
                if (cardSource == card)
                {
                    cardNames.Add("Slayerdramon");
                }

                return cardNames;
            }
        }

        #endregion

        #region Security Attack

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.ChangeSelfSAttackStaticEffect(
                changeValue: 1,
                isInheritedEffect: true,
                card: card,
                condition: null));
        }

        #endregion

        return cardEffects;
    }
}
