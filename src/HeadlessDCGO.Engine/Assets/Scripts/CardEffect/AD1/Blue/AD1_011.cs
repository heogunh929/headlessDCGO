// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// 정본 트랜치2A 카드 — Paildramon (AD1_011, Digimon / Blue / Lv.5)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/AD1/Blue/AD1_011.cs (158 lines, 5 regions)
//    * Alternative Digivolve Condition :14-25  (timing == None, AddSelfDigivolutionRequirementStaticEffect)
//    * DNA                             :27-50  (timing == None, GetJogressConditionClass)
//    * Shared Partition + Inherited    :52-73  (timing == WhenRemoveField, PartitionSelfEffect ×2 own+inherited)
//    * When Digivolving                :75-116 (AS-IS timing == OnEnterFieldAnyone + CanTriggerWhenDigivolving 게이트)
//    * When Attacking                  :118-153(timing == OnAllyAttack + CanTriggerOnAttack 게이트)
//
// ② 프리미티브 매핑 (감사 축 이름):
//    * P:CanNotSwitchAttackTargetEffect — [When Digivolving] DNA 분기: IsJogress면 permanent의
//                                        UntilEachTurnEndEffects에 CanNotSwitchAttackTargetEffect 추가 (AS-IS :110-113)
//    * P:GetJogressConditionClass       — DNA 대체진화(Blue Lv4 + Green Lv4) 조건 (AS-IS :30-36)
//    * (+P:AddSelfDigivolutionRequirementStaticEffect (Free/Hero, level 4, cost 3) — AS-IS :23
//       +P:PartitionSelfEffect own+inherited (Blue4/Green4) — AS-IS :60-70
//       +P:GainCanNotBeDeletedByBattle (UntilOpponentTurnEnd) — AS-IS :103-108
//       +P:DigivolveIntoHandOrTrashCard ([Imperialdramon], reduceCost 2, isHand) — AS-IS :141-150
//       +T:WhenDigivolving, T:OnAllyAttack)
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * Alt-digivolve + DNA(GetJogressConditionClass) → EffectTiming.None 그대로 (상시/조건 등록).
//    * Partition → EffectTiming.WhenRemoveField 그대로 (leave-field 트리거).
//    * [When Digivolving] → EffectTiming.WhenDigivolving 전용 키(미러 방언; AS-IS는 OnEnterFieldAnyone에
//      두지만 미러 DigivolveAction은 WhenDigivolving만 해소, 이중-키 등록 금지 — rule 3).
//      CanUseCondition의 CanTriggerWhenDigivolving(hashtable, card) 게이트는 AS-IS :88-89 그대로 유지.
//    * [When Attacking] self-스코프 → CanTriggerOnAttack(hashtable, card) triggerGate 필수(AS-IS :134).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`.
//    * `card.PermanentOfThisCard()` → `ICardEffect.ResolvePermanentOfThisCard(card)` (this card의 다수 use 일관 치환).
//    * `permanent.Levels_ForJogress(card)` → `permanent.TopCard.JogressLevelsAgainst(card)` — 미러는 이 접근자를
//      Permanent→CardSource로 재하우징 + 개명(CardSource.cs:772 "P6C3 re-fold"): permanent 정체는 내부에서
//      ResolvePermanentOfThisCard로 live 해소되어 결과 1:1(BT16_025 확립 idiom).
//    * `permanent.TopCard.CardColors.Contains(CardColor.Blue)` → `permanent.TopCard.HasCardColor("Blue")` —
//      미러 CardColors는 IReadOnlyList<string>(문자열-색 표준; CardSource.cs:251/1205, BT16_025 idiom).
//    * `new PartitionCondition(4, CardColor.Blue)` → `new PartitionCondition(4, "Blue")` — 미러 ctor는 string 색
//      (Partition.cs:33 문자열-색 표준).
//    * `yield return StartCoroutine(CardEffectCommons.GainCanNotBeDeletedByBattle(...))` → 미러는 SYNC bool
//      (AS-IS 코루틴은 UI만 구동; CardEffectCommons.cs:47) — plain 호출(await 없음, 반환값 미사용).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.AD1.Blue;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;
using PartitionCondition = HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectFactory.KeyWordEffects.PartitionCondition;

public sealed class AD1_011 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternative Digivolve Condition

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("Free")
                        || targetPermanent.TopCard.EqualsTraits("Hero");
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(level: 4, permanentCondition: PermanentCondition, digivolutionCost: 3, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region DNA

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.GetJogressConditionClass(
                PermanentCondition1,
                "a level 4 Blue Digimon",
                PermanentCondition2,
                "a level 4 Green Digimon",
                card
            ));

            bool PermanentCondition1(Permanent permanent)
            {
                return permanent.TopCard.HasCardColor("Blue")
                    && permanent.TopCard.JogressLevelsAgainst(card).Contains(4);
            }

            bool PermanentCondition2(Permanent permanent)
            {
                return permanent.TopCard.HasCardColor("Green")
                    && permanent.TopCard.JogressLevelsAgainst(card).Contains(4);
            }
        }

        #endregion

        #region Shared Partition + Inherited

        List<PartitionCondition> partitionConditions = new List<PartitionCondition>();
        partitionConditions.Add(new PartitionCondition(4, "Blue"));
        partitionConditions.Add(new PartitionCondition(4, "Green"));

        if (timing == EffectTiming.WhenRemoveField)
        {
            cardEffects.Add(CardEffectFactory.PartitionSelfEffect
                (isInheritedEffect: false,
                card: card,
                condition: null,
                cardSourceConditions: partitionConditions));

            cardEffects.Add(CardEffectFactory.PartitionSelfEffect
                (isInheritedEffect: true,
                card: card,
                condition: null,
                cardSourceConditions: partitionConditions));
        }

        #endregion

        #region When Digivolving

        // ③ 배선: AS-IS는 OnEnterFieldAnyone(:76)이지만 미러 방언은 WhenDigivolving 전용 키
        //   (DigivolveAction이 WhenDigivolving만 해소; 이중-키 등록 금지 — trigger-wiring rule 3).
        if (timing == EffectTiming.WhenDigivolving)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Can't be deleted in battle. If DNA: Attack target can't be changed", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[When Digivolving] Until your opponent's turn ends, this Digimon can't be deleted in battle. Then, if DNA digivolving, this Digimon's attack target can't change for the turn.";

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerWhenDigivolving(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            // 치환: AS-IS IEnumerator ActivateCoroutine — 몸통에 실제 await가 없음
            //   (GainCanNotBeDeletedByBattle는 SYNC bool, UntilEachTurnEndEffects.Add는 sync) →
            //   non-async Task + return Task.CompletedTask (Partition.cs 확립 idiom).
            Task ActivateCoroutine(Hashtable hashtable)
            {
                bool CanNotBeDestroyedByBattleCondition(Permanent permanent1, Permanent attackingPermanent,
                                                Permanent defendingPermanent, CardSource defendingCard)
                {
                    return permanent1 == attackingPermanent || permanent1 == defendingPermanent;
                }

                CardEffectCommons.GainCanNotBeDeletedByBattle(
                    targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                    canNotBeDestroyedByBattleCondition: CanNotBeDestroyedByBattleCondition,
                    effectDuration: EffectDuration.UntilOpponentTurnEnd,
                    activateClass: activateClass,
                    effectName: "Can't be deleted in battle");

                if (CardEffectCommons.IsJogress(hashtable))
                {
                    ICardEffect.ResolvePermanentOfThisCard(card).UntilEachTurnEndEffects.Add(_timing => PermanentEffectFactory.CanNotSwitchAttackTargetEffect(ICardEffect.ResolvePermanentOfThisCard(card), activateClass));
                }

                return Task.CompletedTask;
            }
        }

        #endregion

        #region When Attacking

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Digivolve into Imperialdramon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDescription());
            cardEffects.Add(activateClass);

            string EffectDescription()
                => "[When Attacking] This Digimon may digivolve into a Digimon card with [Imperialdramon] in its name in the hand with the digivolution cost reduced by 2.";

            bool CanSelectCardCondition(CardSource cardSource) => cardSource.ContainsCardName("Imperialdramon");

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card)
                    && CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable) => CardEffectCommons.IsExistOnBattleArea(card);

            async Task ActivateCoroutine(Hashtable _hashtable)
            {
                await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                    targetPermanent: ICardEffect.ResolvePermanentOfThisCard(card),
                    cardCondition: CanSelectCardCondition,
                    payCost: true,
                    reduceCostTuple: (reduceCost: 2, reduceCostCardCondition: null),
                    fixedCostTuple: null,
                    ignoreDigivolutionRequirementFixedCost: -1,
                    isHand: true,
                    activateClass: activateClass,
                    successProcess: null);
            }
        }

        #endregion

        return cardEffects;
    }
}
