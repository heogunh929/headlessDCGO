// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// Sonnet 트랜치 S2 카드 — BT2_066 (Digimon / Black)
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════
// ① AS-IS 앵커: DCGO/Assets/Scripts/CardEffect/BT2/Black/BT2_066.cs (146 lines, no region markers)
//    * All Turns              :15-18 (timing == EffectTiming.None) — BlockerSelfStaticEffect.
//    * [On Play]               :20-142 (timing == EffectTiming.OnEnterFieldAnyone + CanTriggerOnPlay 게이트)
//      — 상대 디지몬 최대 2체에 <De-Digivolve N> 발화(N은 SelectCountEffect로 최대 2 선택).
//
// ② 프리미티브 매핑:
//    * P:BlockerSelfStaticEffect  — 상시 [Blocker] (AS-IS :17).
//    * P:SelectCountEffect        — "얼마나 De-Digivolve?" 0~2 카운트 선택 (AS-IS :74-93).
//    * P:IDegeneration            — 선택된 각 퍼머넌트에 De-Digivolve N 적용 (AS-IS :133).
//
// ③ 배선 관례 근거 (trigger-wiring-porting-rules):
//    * [On Play] → EffectTiming.OnEnterFieldAnyone + CanTriggerOnPlay(hashtable, card) 게이트(AS-IS :47 그대로).
//
// 치환(substrate translations only):
//    * IEnumerator→async Task, `yield return ContinuousController.instance.StartCoroutine(X)`→`await X`
//      (BT17_026 idiom); lone `yield return null`→`Task.CompletedTask`(BT8_092 idiom).
//    * AS-IS card-less `HasMatchConditionPermanent(cond)`/`MatchConditionPermanentCount(cond)`(전역 스캔) →
//      미러 `(card, cond)` 오버로드(ST2_06/ST1_08 관례) — 술어 CanSelectPermanentCondition 자체가 이미
//      `IsPermanentExistsOnOpponentBattleAreaDigimon`으로 스코프하므로 결과 동일.
//    * `permanent.CanSelectBySkill(activateClass)` — 미러 Permanent.CanSelectBySkill(ICardEffect) 1:1(R1-d).
//    * `isExistOnField(card)`/`card.Owner.GetBattleAreaDigimons().Contains(card.PermanentOfThisCard())` —
//      AS-IS-verbatim 중복 가드 그대로 유지(단순화 금지); `card.Owner` → `new Player(card.Context, card.Owner)`.
//    * `GManager.instance.GetComponent<SelectCountEffect>().SetUp(...)` — AS-IS 7-인자 시그니처 그대로
//      (SelectPlayer, targetPermanent, MaxCount, CanNoSelect, Message, Message_Enemy, SelectCountCoroutine);
//      targetPermanent는 AS-IS도 null이므로 `null!`(non-null 파라미터형에 대한 널 억제, BT1_040 idiom).
//    * `new IDegeneration(selectedPermanent, degenrationCount, activateClass)` → 미러 ctor
//      `(Permanent, count, causeEffectSourceId, cardEffect)` (EX8_051 idiom).
namespace HeadlessDCGO.Engine.Assets.Scripts.CardEffect.BT2.Black;

using System.Collections;
using System.Threading.Tasks;
using HeadlessDCGO.Engine.Assets.Scripts.Script;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffectCommons;
using HeadlessDCGO.Engine.Assets.Scripts.Script.CardEffects;
using HeadlessDCGO.Engine.Headless.Services;

public sealed class BT2_066 : CEntity_Effect
{
    public override List<ICardEffect> CardEffects(EffectTiming timing, CardSource card)
    {
        List<ICardEffect> cardEffects = new List<ICardEffect>();

        if (timing == EffectTiming.None)
        {
            cardEffects.Add(CardEffectFactory.BlockerSelfStaticEffect(isInheritedEffect: false, card: card, condition: null));
        }

        if (timing == EffectTiming.OnEnterFieldAnyone)
        {
            ActivateClass activateClass = new ActivateClass();
            activateClass.SetUpICardEffect("De-Digivolve 2 to 2 Digimon", CanUseCondition, card);
            activateClass.SetUpActivateClass(CanActivateCondition, ActivateCoroutine, -1, false, EffectDiscription());
            cardEffects.Add(activateClass);

            string EffectDiscription()
            {
                return "[On Play] Trigger <De-Digivolve 2>  on 2 of your opponent's Digimon. (Trash up to 2 cards from the top of one of your opponent's Digimon. If it has no digivolution cards—or becomes a level 3 Digimon—u can't trash any more cards.)";
            }

            bool CanSelectPermanentCondition(Permanent permanent)
            {
                if (CardEffectCommons.IsPermanentExistsOnOpponentBattleAreaDigimon(permanent, card))
                {
                    if (permanent.CanSelectBySkill(activateClass))
                    {
                        return true;
                    }
                }

                return false;
            }

            bool CanUseCondition(Hashtable hashtable)
            {
                return CardEffectCommons.CanTriggerOnPlay(hashtable, card);
            }

            bool CanActivateCondition(Hashtable hashtable)
            {
                if (CardEffectCommons.IsExistOnBattleArea(card))
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
                if (CardEffectCommons.IsExistOnBattleArea(card))
                {
                    if (new Player(card.Context, card.Owner).GetBattleAreaDigimons().Contains(ICardEffect.ResolvePermanentOfThisCard(card)))
                    {
                        if (CardEffectCommons.HasMatchConditionPermanent(card, CanSelectPermanentCondition))
                        {
                            int degenrationMaxCount = 2;
                            int degenrationCount = 0;

                            SelectCountEffect selectCountEffect = GManager.instance.GetComponent<SelectCountEffect>();
                            if (selectCountEffect != null)
                            {
                                selectCountEffect.SetUp(
                                    SelectPlayer: card.Owner,
                                    targetPermanent: null!,
                                    MaxCount: degenrationMaxCount,
                                    CanNoSelect: false,
                                    Message: "How much will you De-Digivolve?",
                                    Message_Enemy: "The opponent is choosing how much to De-Digivolve.",
                                    SelectCountCoroutine: SelectCountCoroutine);

                                await selectCountEffect.Activate();

                                Task SelectCountCoroutine(int count)
                                {
                                    degenrationCount = count;
                                    return Task.CompletedTask;
                                }
                            }

                            int maxCount = Math.Min(2, CardEffectCommons.MatchConditionPermanentCount(card, CanSelectPermanentCondition));

                            SelectPermanentEffect selectPermanentEffect = GManager.instance.GetComponent<SelectPermanentEffect>();

                            selectPermanentEffect.SetUp(
                                selectPlayer: card.Owner,
                                canTargetCondition: CanSelectPermanentCondition,
                                canTargetCondition_ByPreSelecetedList: null,
                                canEndSelectCondition: CanEndSelectCondition,
                                maxCount: maxCount,
                                canNoSelect: false,
                                canEndNotMax: false,
                                selectPermanentCoroutine: SelectPermanentCoroutine,
                                afterSelectPermanentCoroutine: null,
                                mode: SelectPermanentEffect.Mode.Custom,
                                cardEffect: activateClass);

                            selectPermanentEffect.SetUpCustomMessage("Select Digimon to De-Digivolve.", "The opponent is selecting Digimon to De-Digivolve.");

                            await selectPermanentEffect.Activate();

                            bool CanEndSelectCondition(List<Permanent> permanents)
                            {
                                if (permanents.Count <= 0)
                                {
                                    return false;
                                }

                                return true;
                            }

                            async Task SelectPermanentCoroutine(Permanent permanent)
                            {
                                Permanent selectedPermanent = permanent;

                                if (selectedPermanent != null)
                                {
                                    await new IDegeneration(selectedPermanent, degenrationCount, activateClass.EffectSourceCard?.InstanceId, cardEffect: activateClass).Degeneration();
                                }
                            }
                        }
                    }
                }
            }
        }

        return cardEffects;
    }
}
