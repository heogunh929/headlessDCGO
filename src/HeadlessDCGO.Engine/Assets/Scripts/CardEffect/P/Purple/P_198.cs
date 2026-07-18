// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S4 카드 — P_198 (DemiDevimon, Digimon / Purple)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/P/Purple/P_198.cs (122 lines, 3 regions)
//    * Alternate Digivolution Requirement :15-24 (None — [TS] 레벨2 위 대체진화 조건 cost 0)
//    * [Start of Your Main Phase]         :28-75 (OnStartMainPhase — 메모리 4 이하면 [Fallen Angel]/[TS]
//      손패 카드로 무료 진화)
//    * ESS [When Attacking]               :79-115 (OnAllyAttack, isInheritedEffect — <Draw 1> + 트래시 1)
//
// ② 프리미티브 매핑:
//    * P:AddSelfDigivolutionRequirementStaticEffect — Alt Digivolution Requirement (AS-IS :23).
//    * P:DigivolveIntoHandOrTrashCard — [Start of Your Main Phase] 몸통 (AS-IS :63-73).
//    * P:DrawAndDiscardCards — ESS 몸통 (AS-IS :107).
//    * T:OnStartMainPhase — 신규 창 타이밍 소비자. 표면 실존 확인: EffectTiming.OnStartMainPhase 키 실존
//      (TurnStateMachine.cs:428 StackSkillInfos 발화, CardEffectFactory.cs:1780 소비 실례). 그대로 포팅.
//
// ③ 배선 관례 근거: [Start of Your Main Phase] → OnStartMainPhase 그대로(방언 변환 대상 아님);
//    [When Attacking] → OnAllyAttack + CanTriggerOnAttack 그대로.
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, StartCoroutine→await (BT8_092 idiom).
//    * `targetPermanent.TopCard.HasTSTraits` → `.EqualsTraits("TS")` (EX10_029 established idiom).
//    * AS-IS :58 `cardSource.CanPlayCardTargetFrame(card.PermanentOfThisCard().PermanentFrame, false,
//      activateClass)` — 미러 frame/slot 모델 부재(RD-P6C1-2). BT17_026 헤더의 OCCUPIED-frame 환원 관례를
//      receiver=cardSource(디지몬 후보), targetPermanent=이 카드의 소속 permanent, ignoreBool=false로 적용:
//      `<targetPermanent>.TopCard.Owner == cardSource.Owner && cardSource.CanEvolve(<targetPermanent>, false)`.
//    * `cardSource.HasFallenAngelTraits` → `cardSource.EqualsTraits("Fallen Angel")` (AS-IS CardSource.cs:3979
//      getter 몸통 1:1 인라인 — §3 조립 선례; HasTSTraits와 동일 패턴).
//    * `card.PermanentOfThisCard()`(1st arg, targetPermanent 필요) → `ICardEffect.ResolvePermanentOfThisCard(card)`.
//    * `DigivolveIntoHandOrTrashCard(...)` — mirror는 `sourceCard:`(=activateClass의 EffectSourceCard=card)
//      + `failedProcess`/`isOptional` 파라미터가 AS-IS와 동형 기본값(null/true)이라 AS-IS 호출대로 생략.
//    * `DrawAndDiscardCards(player:, drawAmount:, trashAmount:, card:, activateClass:)`(AS-IS 5 named args) →
//      mirror는 `activateClass` 파라미터가 없음(원인은 sourceCard.InstanceId에서 내부 도출) — `sourceCard: card`만
//      전달.
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.P.Purple;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class P_198 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        #region Alternate Digivolution Requirement

        if (timing == EffectTiming.None)
        {
            static bool PermanentCondition(Permanent targetPermanent)
            {
                return targetPermanent.TopCard.EqualsTraits("TS") &&
                       targetPermanent.TopCard.HasLevel && targetPermanent.TopCard.Level == 2;
            }

            cardEffects.Add(CardEffectFactory.AddSelfDigivolutionRequirementStaticEffect(permanentCondition: PermanentCondition, digivolutionCost: 0, ignoreDigivolutionRequirement: false, card: card, condition: null));
        }

        #endregion

        #region Start Of Your Main Phase

        if (timing == EffectTiming.OnStartMainPhase)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Digivolve into a [Fallen Angel]/[TS] digimon in hand", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, true, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[Start of Your Main Phase] If you have 4 or less memory, this Digimon may digivolve into a Digimon card with the [Fallen Angel] or [TS] trait in the hand without paying the cost.";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleAreaDigimon(card)
                    && CardEffectCommons.IsOwnerTurn(card)
                    && new Player(card.Context, card.Owner).MemoryForPlayer <= 4;
            }

            bool CanSelectCardCondition(CardSource cardSource)
            {
                Permanent thisPermanent = ICardEffect.ResolvePermanentOfThisCard(card);

                return cardSource.IsDigimon
                    && (cardSource.EqualsTraits("Fallen Angel") || cardSource.EqualsTraits("TS"))
                    && thisPermanent.TopCard.Owner == cardSource.Owner
                    && cardSource.CanEvolve(thisPermanent, false);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.DigivolveIntoHandOrTrashCard(
                                ICardEffect.ResolvePermanentOfThisCard(card),
                                digivolvingCard => CanSelectCardCondition(digivolvingCard),
                                payCost: false,
                                reduceCostTuple: null,
                                fixedCostTuple: null,
                                ignoreDigivolutionRequirementFixedCost: -1,
                                isHand: true,
                                sourceCard: card,
                                successProcess: null
                            );
            }
        }

        #endregion

        #region ESS - When Attacking

        if (timing == EffectTiming.OnAllyAttack)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("Draw 1, trash 1", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, 1, false, EffectDiscription());
            activateClass.SetIsInheritedEffect(true);
            activateClass.SetHashString("P_198_Draw1Trash1");
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[When Attacking] [Once Per Turn] <Draw 1> and trash 1 card in your hand";
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnAttack(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                return CardEffectCommons.IsExistOnBattleArea(card);
            }

            async Task ActivateCoroutine(Hashtable hashtable)
            {
                await CardEffectCommons.DrawAndDiscardCards(
                    player: (card.Owner, card.Owner),
                    drawAmount: 1,
                    trashAmount: 1,
                    sourceCard: card
                );
            }
        }

        #endregion

        return cardEffects;
    }
}
